using System.Collections.Generic;
using UnityEngine;

namespace AlarmClock
{
    // Connects one native chronometer (ShipItemClock) to its saved alarm record,
    // the modifier controls and the look text. Scheduling and ringing live elsewhere.
    internal sealed class ChronometerAlarm : MonoBehaviour
    {
        private static readonly Dictionary<int, ChronometerAlarm> live = new Dictionary<int, ChronometerAlarm>();
        private static int editFrame = -10;
        private static AudioClip click;

        private ShipItem item;
        private SaveablePrefab saveable;
        private AlarmRinger ringer;
        private int registeredId;
        private bool wroteText;
        private float wheelAccumulated;

        // The wheel belongs to the alarm for the frame in which a chronometer is being set.
        public static bool EditingNow => Time.frameCount - editFrame <= 1;
        public AlarmRinger Ringer => ringer;

        public static bool TryGetLive(int instanceId, out ChronometerAlarm alarm) => live.TryGetValue(instanceId, out alarm);

        public static void StopAll()
        {
            foreach (var alarm in live.Values) alarm.ringer.StopRinging();
        }

        private void Awake()
        {
            item = GetComponent<ShipItem>();
            saveable = GetComponent<SaveablePrefab>();
            ringer = gameObject.AddComponent<AlarmRinger>();
        }

        private void Update()
        {
            int id = InstanceId;
            if (id != registeredId)
            {
                Unregister();
                if (id > 0) { live[id] = this; registeredId = id; }
            }
            if (!Owned) return;
            if (IsEditTarget())
            {
                editFrame = Time.frameCount;
                HandleScroll();
            }
            else wheelAccumulated = 0f;
            if (item.held != null) ApplyLookText();
        }

        private void OnDestroy() => Unregister();

        private void Unregister()
        {
            if (registeredId > 0 && live.TryGetValue(registeredId, out var current) && current == this)
                live.Remove(registeredId);
            registeredId = 0;
        }

        private int InstanceId => saveable != null ? saveable.instanceId : 0;
        private bool Owned => item != null && item.sold && InstanceId > 0;

        // A held chronometer takes the controls, so looking at another one while
        // holding it never changes both.
        private bool Targeted => item.held != null || (item.IsLookedAt() && !AnyHeld());

        private static bool AnyHeld()
        {
            foreach (var alarm in live.Values)
                if (alarm.item.held != null) return true;
            return false;
        }

        public bool IsEditTarget() => Owned && Targeted && Plugin.ModifierHeld();

        private AlarmRecord Record(bool create)
        {
            if (create) return Plugin.Store.GetOrCreate(InstanceId, saveable.prefabIndex);
            return Plugin.Store.TryGet(InstanceId, saveable.prefabIndex, out var record) ? record : null;
        }

        private void HandleScroll()
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel == 0f || !AlarmScheduler.TryNow(out double now)) return;
            int steps = AlarmTime.WheelSteps(ref wheelAccumulated, wheel);
            if (steps == 0) return;
            var record = Record(true);
            record.Minute = AlarmTime.Step(record.Minute, steps);
            record.Enabled = true;
            record.NextDue = AlarmTime.NextOccurrenceAfter(record.Minute, now);
            PlayClick();
        }

        // Returns true when the alt action was consumed by the alarm.
        public bool OnAltActivate()
        {
            if (ringer.IsRinging)
            {
                ringer.StopRinging();
                return true;
            }
            if (!Owned || !Plugin.ModifierHeld() || !AlarmScheduler.TryNow(out double now)) return false;
            var record = Record(true);
            record.Enabled = !record.Enabled;
            if (record.Enabled) record.NextDue = AlarmTime.NextOccurrenceAfter(record.Minute, now);
            PlayClick();
            return true;
        }

        // Shown over a looked-at chronometer only while the modifier is held.
        public string Tooltip()
        {
            if (!Plugin.ControlsTooltip || !IsEditTarget()) return null;
            string key = Plugin.ModifierName();
            return "hold " + key + " and scroll to set the alarm, " + key + " and R to switch it on or off";
        }

        public void ApplyLookText()
        {
            if (!Owned) return;
            var record = Record(false);
            bool enabled = record != null && record.Enabled;
            bool editing = Targeted && Plugin.ModifierHeld();
            // While ringing, the controls prompt shows how to stop it.
            if (ringer.IsRinging) item.lookText = "ringing";
            else if (enabled || editing)
            {
                int minute = record != null ? record.Minute : AlarmRecord.DefaultMinute;
                item.lookText = enabled ? "alarm " + AlarmTime.Format(minute) : "alarm off";
            }
            else if (wroteText) item.lookText = "";
            wroteText = ringer.IsRinging || enabled || editing;
        }

        private void PlayClick()
        {
            if (click == null) click = Resources.Load<AudioClip>("Juicebox/Sounds/wood click");
            if (click != null) AudioSource.PlayClipAtPoint(click, transform.position, Plugin.Volume * 0.5f);
        }
    }
}
