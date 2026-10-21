using UnityEngine;

namespace AlarmClock
{
    // Checks every saved alarm against absolute game time once per frame. An alarm
    // rings only if its chronometer is loaded and near the player when the time is
    // crossed, so a clock left elsewhere in the world never wakes anyone.
    internal sealed class AlarmScheduler : MonoBehaviour
    {
        public static bool TryNow(out double now)
        {
            now = 0;
            if (!GameState.playing || GameState.currentlyLoading || Sun.sun == null) return false;
            now = AlarmTime.Absolute(GameState.day, Sun.sun.globalTime);
            return true;
        }

        private void Update()
        {
            if (!TryNow(out double now)) return;
            foreach (var record in Plugin.Store.Records)
            {
                if (!record.Enabled) continue;
                var outcome = AlarmTime.Evaluate(record.Minute, record.NextDue, now, out double next);
                if (outcome == AlarmOutcome.None) continue;
                record.NextDue = next;
                string time = AlarmTime.Format(record.Minute);
                if (outcome == AlarmOutcome.Skip)
                {
                    Plugin.Log.LogInfo($"Alarm {time} on chronometer {record.InstanceId} was missed and skipped.");
                }
                else if (GameState.sleepingInTavern)
                {
                    // Inn rest keeps its own wake-up. The alarm stays on for the next day.
                    Plugin.Log.LogInfo($"Alarm {time} on chronometer {record.InstanceId} stayed silent during inn rest.");
                }
                else if (ChronometerAlarm.TryGetLive(record.InstanceId, out var alarm) && alarm.Ringer.PlayerInRange())
                {
                    alarm.Ringer.StartRinging();
                    Plugin.Log.LogInfo($"Alarm {time} on chronometer {record.InstanceId} is ringing.");
                }
                else
                {
                    // Also the daily case for a chronometer that was sold or destroyed, so keep it quiet.
                    Plugin.Log.LogDebug($"Alarm {time} on chronometer {record.InstanceId} was out of range and stayed silent.");
                }
            }
        }
    }
}
