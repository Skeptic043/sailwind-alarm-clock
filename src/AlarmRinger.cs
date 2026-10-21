using UnityEngine;

namespace AlarmClock
{
    // Sound and wake-up for one physical alarm. It knows nothing about the item it
    // is attached to, so a dedicated clock model could reuse it unchanged.
    internal sealed class AlarmRinger : MonoBehaviour
    {
        private const float StrikeInterval = 0.075f;
        private const float BurstLength = 1.4f;
        private const float BurstPause = 0.8f;
        // Each strike gets its own voice for a few strikes, so the bell keeps ringing
        // under the hammer instead of being cut off at every blow.
        private const int Voices = 4;
        private static AudioClip bell;
        private static bool bellMissing;

        // Matches the measured partial layout of real small alarm bells at the default
        // pitch of 2.5 (cutoff 900 Hz). See the timepieces research page.
        private const float HighPassPerPitch = 360f;

        private readonly AudioSource[] voices = new AudioSource[Voices];
        private GameObject bellObject;
        private AudioHighPassFilter filter;
        private int nextVoice;
        private bool secondHead;
        private float ringStarted;
        private float nextStrike;

        public bool IsRinging { get; private set; }

        public void StartRinging()
        {
            EnsureVoices();
            IsRinging = true;
            secondHead = false;
            ringStarted = Time.unscaledTime;
            nextStrike = ringStarted;
        }

        // Dismissing silences the bell at once.
        public void StopRinging()
        {
            IsRinging = false;
            foreach (var voice in voices)
                if (voice != null) voice.Stop();
        }

        public bool PlayerInRange()
        {
            var camera = Camera.main;
            return camera != null &&
                (camera.transform.position - transform.position).sqrMagnitude <= Plugin.RingRange * Plugin.RingRange;
        }

        private void Update()
        {
            if (!IsRinging) return;
            float now = Time.unscaledTime;
            if (now - ringStarted >= Plugin.RingDuration)
            {
                // Running out stops the hammer and lets the last blows ring out.
                IsRinging = false;
                return;
            }
            // A ringing alarm keeps the player awake, as native impacts do. Passing out
            // has its own recovery sequence, inn rest keeps its own wake-up, and WakeUp is
            // ignored until the eyes close.
            if (GameState.sleeping && GameState.eyesFullyClosed && !GameState.recovering &&
                !GameState.sleepingInTavern && PlayerInRange() &&
                Sleep.instance != null)
                Sleep.instance.WakeUp();
            if (now < nextStrike) return;
            float cycle = (now - ringStarted) % (BurstLength + BurstPause);
            if (cycle >= BurstLength)
            {
                nextStrike = now + (BurstLength + BurstPause - cycle);
                secondHead = false;
                return;
            }
            nextStrike = now + StrikeInterval * Random.Range(0.94f, 1.06f);
            Strike();
        }

        private void Strike()
        {
            var clip = Bell();
            if (clip == null) return;
            // The double-headed hammer alternates, and its second head lands a little
            // softer and at a slightly different spot on the bell.
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            voice.maxDistance = Plugin.RingRange;
            voice.volume = Plugin.Volume * (secondHead ? 0.75f : 1f) * Random.Range(0.9f, 1f);
            // The native ding is a large bell. Raising the pitch also shortens its ring,
            // and the high-pass removes the low hum, so together it sounds like a small
            // alarm bell. Both follow the live setting.
            float pitch = Plugin.BellPitch;
            filter.cutoffFrequency = HighPassPerPitch * pitch;
            voice.pitch = pitch * (secondHead ? 1.015f : 1f) * Random.Range(0.99f, 1.01f);
            voice.clip = clip;
            voice.Stop();
            voice.Play();
            secondHead = !secondHead;
        }

        private void EnsureVoices()
        {
            if (bellObject == null)
            {
                // The voices and their filter live on their own child, so the filter
                // cannot affect any other sound the item might carry.
                bellObject = new GameObject("Alarm bell");
                bellObject.transform.SetParent(transform, false);
            }
            for (int i = 0; i < Voices; i++)
            {
                if (voices[i] != null) continue;
                var voice = bellObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 1f;
                voice.maxDistance = Plugin.RingRange;
                voice.dopplerLevel = 0f;
                voices[i] = voice;
            }
            // Unity refuses an audio filter on an object without an AudioSource or
            // AudioListener, so the filter is added after the voices.
            if (filter == null) filter = bellObject.AddComponent<AudioHighPassFilter>();
        }

        private static AudioClip Bell()
        {
            if (bell == null && !bellMissing)
            {
                bell = Resources.Load<AudioClip>("Juicebox/Sounds/ding");
                if (bell == null)
                {
                    bellMissing = true;
                    Plugin.Log.LogWarning("The native bell sound Juicebox/Sounds/ding was not found. Alarms will ring silently.");
                }
            }
            return bell;
        }

        private void OnDisable() => StopRinging();
    }
}
