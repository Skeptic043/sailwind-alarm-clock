using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AlarmClock
{
    // Sound and wake-up for one physical alarm. It knows nothing about the item it
    // is attached to, so a dedicated clock model could reuse it unchanged.
    // The original bell is struck repeatedly from the native ding. A recorded alarm
    // loops a ringing clip, then plays a tail in which the hammer stops.
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

        // Matches the measured partial layout of real small alarm bells at the
        // original bell's natural pitch of 2.5 (cutoff 900 Hz). See the timepieces research page.
        private const float HighPassPerPitch = 360f;

        // Time for the audio thread to start a scheduled clip exactly on its sample.
        private const double ScheduleLead = 0.1;

        private sealed class Recording
        {
            public AudioClip Loop;
            public AudioClip Tail;
        }

        // Loaded once at startup, so starting an alarm never reads or decodes a file.
        private static readonly Dictionary<AlarmSound, Recording> recordings = new Dictionary<AlarmSound, Recording>();

        private readonly AudioSource[] voices = new AudioSource[Voices];
        private GameObject bellObject;
        private AudioHighPassFilter filter;
        private int nextVoice;
        private bool secondHead;
        private float ringStarted;
        private float nextStrike;

        private AudioSource loopSource;
        private AudioSource tailSource;
        // The sound this ring started with. A changed setting applies from the next alarm.
        private AlarmSound ringingSound;
        // Null while the original bell rings.
        private Recording recording;
        // The loop had played anchorSample samples at audio time anchorTime.
        private double anchorTime;
        private double anchorSample;
        private float recordedPitch;
        private double tailSample;
        private double tailStart = -1;

        // True while the alarm can be heard and dismissed, including a recorded tail.
        public bool IsRinging { get; private set; }
        // The timed part of a ring, which wakes the player and ends at the ring duration.
        private bool waking;

        public static void LoadRecordings(string directory)
        {
            foreach (AlarmSound sound in Enum.GetValues(typeof(AlarmSound)))
            {
                string stem = AlarmSounds.FileStem(sound);
                if (stem == null) continue;
                try
                {
                    recordings[sound] = new Recording
                    {
                        Loop = LoadClip(directory, stem + "-loop"),
                        Tail = LoadClip(directory, stem + "-tail"),
                    };
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogWarning($"The {sound} alarm sound could not be loaded, so the original bell rings instead. {exception.Message}");
                }
            }
        }

        private static AudioClip LoadClip(string directory, string name)
        {
            string path = Path.Combine(directory, name + ".wav");
            if (!Wav.TryRead(File.ReadAllBytes(path), out float[] samples, out int rate, out string problem))
                throw new InvalidDataException(path + " is " + problem + ".");
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            // Scene loads unload assets that no scene object references.
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        // An idle alarm needs no per-frame callback. StartRinging enables the
        // component only for the active ring and wake-up lifecycle.
        private void Awake() => enabled = false;

        public void StartRinging()
        {
            StopSound();
            ringingSound = Plugin.Sound;
            if (!recordings.TryGetValue(ringingSound, out recording)) ringingSound = AlarmSound.OriginalBell;
            IsRinging = true;
            waking = true;
            ringStarted = Time.unscaledTime;
            if (recording != null) StartRecording();
            else
            {
                EnsureVoices();
                secondHead = false;
                nextStrike = ringStarted;
            }
            enabled = true;
        }

        // Dismissing silences the alarm at once, including a recorded tail.
        public void StopRinging()
        {
            IsRinging = false;
            waking = false;
            StopSound();
            enabled = false;
        }

        private void StopSound()
        {
            foreach (var voice in voices)
                if (voice != null) voice.Stop();
            if (loopSource != null) loopSource.Stop();
            if (tailSource != null) tailSource.Stop();
        }

        public bool PlayerInRange()
        {
            var camera = Camera.main;
            return camera != null &&
                (camera.transform.position - transform.position).sqrMagnitude <= Plugin.RingRange * Plugin.RingRange;
        }

        private void Update()
        {
            // An alarm silenced by deactivation stays enabled, so it switches itself off here.
            if (!IsRinging)
            {
                enabled = false;
                return;
            }
            float now = Time.unscaledTime;
            if (waking && now - ringStarted >= Plugin.RingDuration)
            {
                // Running out stops the hammer and lets the bell ring out. The original
                // bell's last blows decay on their own. A recorded alarm stays dismissable
                // until its tail ends.
                waking = false;
                if (recording == null)
                {
                    IsRinging = false;
                    enabled = false;
                    return;
                }
                ScheduleTail();
            }
            if (recording != null) UpdateRecording();
            if (!waking) return;
            // A ringing alarm keeps the player awake, as native impacts do. Passing out
            // has its own recovery sequence, inn rest keeps its own wake-up, and WakeUp is
            // ignored until the eyes close.
            if (GameState.sleeping && GameState.eyesFullyClosed && !GameState.recovering &&
                !GameState.sleepingInTavern && PlayerInRange() &&
                Sleep.instance != null)
                Sleep.instance.WakeUp();
            if (recording == null) UpdateStrikes(now);
        }

        private void UpdateStrikes(float now)
        {
            if (now < nextStrike) return;
            float cycle = (now - ringStarted) % (BurstLength + BurstPause);
            if (cycle >= BurstLength)
            {
                nextStrike = now + (BurstLength + BurstPause - cycle);
                secondHead = false;
                return;
            }
            nextStrike = now + StrikeInterval * UnityEngine.Random.Range(0.94f, 1.06f);
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
            voice.volume = Plugin.Volume * (secondHead ? 0.75f : 1f) * UnityEngine.Random.Range(0.9f, 1f);
            // The native ding is a large bell. Raising the pitch also shortens its ring,
            // and the high-pass removes the low hum, so together it sounds like a small
            // alarm bell. Both follow the live setting.
            float pitch = AlarmSounds.Pitch(AlarmSound.OriginalBell, Plugin.BellTone);
            filter.cutoffFrequency = HighPassPerPitch * pitch;
            voice.pitch = pitch * (secondHead ? 1.015f : 1f) * UnityEngine.Random.Range(0.99f, 1.01f);
            voice.clip = clip;
            voice.Stop();
            voice.Play();
            secondHead = !secondHead;
        }

        private void StartRecording()
        {
            EnsureRecordingSources();
            loopSource.clip = recording.Loop;
            tailSource.clip = recording.Tail;
            recordedPitch = AlarmSounds.Pitch(ringingSound, Plugin.BellTone);
            loopSource.pitch = tailSource.pitch = recordedPitch;
            // A scheduled start gives the exact audio time the loop began, which the
            // tail needs to start exactly at the end of a loop.
            anchorTime = AudioSettings.dspTime + ScheduleLead;
            anchorSample = 0;
            tailStart = -1;
            UpdateRecording();
            loopSource.PlayScheduled(anchorTime);
        }

        // Volume, range and tone follow the live settings for the whole ring, tail included.
        private void UpdateRecording()
        {
            loopSource.volume = tailSource.volume = Plugin.Volume;
            loopSource.maxDistance = tailSource.maxDistance = Plugin.RingRange;
            float pitch = AlarmSounds.Pitch(ringingSound, Plugin.BellTone);
            if (pitch != recordedPitch) ChangePitch(pitch);
            if (tailStart >= 0 && AudioSettings.dspTime > tailStart + ScheduleLead && !tailSource.isPlaying)
            {
                IsRinging = false;
                enabled = false;
            }
        }

        // The tail starts where the loop would wrap, so the ring continues without a seam.
        private void ScheduleTail()
        {
            double played = anchorSample + Math.Max(0, AudioSettings.dspTime - anchorTime) * SamplesPerSecond;
            double length = recording.Loop.samples;
            tailSample = Math.Ceiling((played + ScheduleLead * SamplesPerSecond) / length) * length;
            tailStart = TimeAt(tailSample);
            loopSource.SetScheduledEndTime(tailStart);
            tailSource.PlayScheduled(tailStart);
        }

        // A tone change alters how fast the loop plays, so the position reached so far
        // is banked at the old rate and a pending tail is moved to the same loop end.
        private void ChangePitch(float pitch)
        {
            double now = AudioSettings.dspTime;
            if (now > anchorTime)
            {
                anchorSample += (now - anchorTime) * SamplesPerSecond;
                anchorTime = now;
            }
            recordedPitch = pitch;
            loopSource.pitch = tailSource.pitch = pitch;
            if (tailStart > now)
            {
                tailStart = TimeAt(tailSample);
                loopSource.SetScheduledEndTime(tailStart);
                tailSource.SetScheduledStartTime(tailStart);
            }
        }

        private double SamplesPerSecond => recording.Loop.frequency * (double)recordedPitch;
        private double TimeAt(double sample) => anchorTime + (sample - anchorSample) / SamplesPerSecond;

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
                if (voices[i] == null) voices[i] = AddSource(bellObject);
            // Unity refuses an audio filter on an object without an AudioSource or
            // AudioListener, so the filter is added after the voices.
            if (filter == null) filter = bellObject.AddComponent<AudioHighPassFilter>();
        }

        private void EnsureRecordingSources()
        {
            if (loopSource != null) return;
            // A separate child keeps the original bell's high-pass filter off the recording.
            var holder = new GameObject("Alarm recording");
            holder.transform.SetParent(transform, false);
            loopSource = AddSource(holder);
            loopSource.loop = true;
            tailSource = AddSource(holder);
        }

        private static AudioSource AddSource(GameObject holder)
        {
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = Plugin.RingRange;
            source.dopplerLevel = 0f;
            return source;
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

        private void OnDisable()
        {
            // A finished ring clears IsRinging before disabling this component so the
            // original bell's last blows ring out. Disabling an audible clock still stops it.
            if (!IsRinging) return;
            IsRinging = false;
            waking = false;
            StopSound();
        }
    }
}
