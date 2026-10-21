using System;
using System.Collections.Generic;
using System.Linq;

namespace AlarmClock
{
    internal static class Checks
    {
        private static int failures;

        private static void Check(bool condition, string name)
        {
            Console.WriteLine((condition ? "PASS: " : "FAIL: ") + name);
            if (!condition) failures++;
        }

        private static double At(int day, int hour, int minute) => day * 24.0 + hour + minute / 60.0;

        // Drives one alarm through successive frame times, as the scheduler does.
        private static List<AlarmOutcome> Run(int minute, double nextDue, params double[] frames)
        {
            var outcomes = new List<AlarmOutcome>();
            foreach (double now in frames)
            {
                var outcome = AlarmTime.Evaluate(minute, nextDue, now, out nextDue);
                if (outcome != AlarmOutcome.None) outcomes.Add(outcome);
            }
            return outcomes;
        }

        private static int Main()
        {
            const int six = 6 * 60;
            double due = AlarmTime.NextOccurrenceAfter(six, At(2, 5, 0));
            Check(due == At(2, 6, 0), "next occurrence is later the same day");
            Check(AlarmTime.NextOccurrenceAfter(six, At(2, 6, 0)) == At(3, 6, 0), "an occurrence at the current moment is not counted");

            var normal = Run(six, due, At(2, 5, 59), At(2, 6, 1), At(2, 6, 2), At(2, 7, 0));
            Check(normal.SequenceEqual(new[] { AlarmOutcome.Fire }), "crossing 05:59 to 06:01 fires once");

            var timeskip = Run(six, due, At(2, 5, 30), At(2, 6, 20), At(2, 6, 40));
            Check(timeskip.SequenceEqual(new[] { AlarmOutcome.Fire }), "a large sleep frame 05:30 to 06:20 fires once");

            double midnight = AlarmTime.NextOccurrenceAfter(0, At(3, 23, 50));
            Check(midnight == At(4, 0, 0), "a midnight alarm is due at the start of the next day");
            var rollover = Run(0, midnight, At(3, 23, 50), At(4, 0, 10), At(4, 0, 20));
            Check(rollover.SequenceEqual(new[] { AlarmOutcome.Fire }), "midnight rollover fires once");

            AlarmTime.Evaluate(six, due, At(2, 6, 1), out double afterFire);
            Check(afterFire == At(3, 6, 0), "after firing the next due time is the following day");

            var reload = Run(six, afterFire, At(2, 6, 1), At(2, 6, 5));
            Check(reload.Count == 0, "a reload with a saved future due time does not fire again");

            var missed = Run(six, due, At(2, 7, 30));
            Check(missed.SequenceEqual(new[] { AlarmOutcome.Skip }), "an alarm more than an hour late is skipped");

            Check(AlarmTime.Step(23 * 60 + 45, 1) == 0, "23:45 steps up to 00:00");
            Check(AlarmTime.Step(0, -1) == 23 * 60 + 45, "00:00 steps down to 23:45");
            Check(AlarmTime.Step(6 * 60 + 7, 1) == 6 * 60 + 15, "an off-grid time snaps up to the next step");
            Check(AlarmTime.Step(6 * 60 + 7, -1) == 6 * 60, "an off-grid time snaps down to the previous step");
            Check(AlarmTime.Format(6 * 60 + 30) == "06:30", "time formats as 24-hour HH:MM");

            Check(AlarmTime.Step(6 * 60, 3) == 6 * 60 + 45, "several wheel steps in one frame add up");

            float wheel = 0f;
            int notches = 0;
            for (int i = 0; i < 7; i++) notches += AlarmTime.WheelSteps(ref wheel, 0.0999f);
            Check(notches == 7, "every wheel notch counts even when its value is slightly under one step");
            wheel = 0f;
            notches = 0;
            for (int i = 0; i < 10; i++) notches += AlarmTime.WheelSteps(ref wheel, 0.033f);
            Check(notches == 3, "small smooth-scroll deltas add up instead of each counting as a step");
            wheel = 0f;
            Check(AlarmTime.WheelSteps(ref wheel, -0.2f) == -2, "two notches in one frame step twice downward");

            var store = new AlarmStore();
            var record = store.GetOrCreate(123456, 171);
            record.Minute = 7 * 60 + 15;
            record.Enabled = true;
            record.NextDue = At(5, 7, 15);
            store.GetOrCreate(42, 170);
            string json = store.Serialize();
            var loaded = new AlarmStore();
            Check(loaded.Load(json) && loaded.Writable && loaded.Count == 2, "saved alarms load back");
            Check(loaded.TryGet(123456, 171, out var back) && back.Minute == 7 * 60 + 15 && back.Enabled &&
                back.NextDue == At(5, 7, 15), "a record keeps its time, state and due time");
            Check(!loaded.TryGet(123456, 170, out _), "a record is not matched to a different chronometer template");
            Check(loaded.Load(null) && loaded.Count == 0 && loaded.Writable, "a save without alarm data loads empty");

            var broken = new AlarmStore();
            Check(!broken.Load("{not json") && !broken.Writable && broken.Count == 0, "malformed data is left unwritten");
            Check(!broken.Load("{\"Schema\":2,\"Alarms\":[]}") && !broken.Writable, "an unknown schema is left unwritten");
            broken.Reset();
            Check(broken.Writable, "reset makes the store writable again");

            Console.WriteLine(failures == 0 ? "All alarm logic checks passed." : failures + " alarm logic check(s) failed.");
            return failures == 0 ? 0 : 1;
        }
    }
}
