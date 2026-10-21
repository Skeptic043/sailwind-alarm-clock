using System;

namespace AlarmClock
{
    internal enum AlarmOutcome { None, Fire, Skip }

    // Absolute game time is GameState.day * 24 + Sun.globalTime, in hours. It only
    // moves forward during play, so alarms compare against it instead of the
    // wrapping time of day. Sleep can advance many minutes in one frame.
    internal static class AlarmTime
    {
        public const int MinutesPerDay = 24 * 60;
        public const double GraceHours = 1.0;
        public const int StepMinutes = 15;

        public static double Absolute(int day, float globalTime) => day * 24.0 + globalTime;

        public static double NextOccurrenceAfter(int minuteOfDay, double now)
        {
            double offset = Normalize(minuteOfDay) / 60.0;
            double candidate = Math.Floor(now / 24.0) * 24.0 + offset;
            while (candidate <= now) candidate += 24.0;
            return candidate;
        }

        // Late by more than the grace period means the alarm was not observed when
        // it was due (for example the mod was absent), so it is skipped rather than rung.
        public static AlarmOutcome Evaluate(int minuteOfDay, double nextDue, double now, out double newNextDue)
        {
            newNextDue = nextDue;
            if (now < nextDue) return AlarmOutcome.None;
            newNextDue = NextOccurrenceAfter(minuteOfDay, now);
            return now - nextDue > GraceHours ? AlarmOutcome.Skip : AlarmOutcome.Fire;
        }

        public static int Step(int minuteOfDay, int steps)
        {
            // Snap an off-grid time to the grid first, counting that move as one step
            // in the downward direction.
            int minute = Normalize(minuteOfDay);
            int snapped = minute / StepMinutes * StepMinutes;
            if (steps < 0 && snapped != minute) return Normalize(snapped + (steps + 1) * StepMinutes);
            return Normalize(snapped + steps * StepMinutes);
        }

        // One mouse-wheel notch reads about 0.1 but can arrive slightly under it, and
        // smooth-scrolling devices send smaller pieces. Accumulating keeps every notch
        // without letting each small piece count as a whole step.
        public static int WheelSteps(ref float accumulated, float wheel)
        {
            accumulated += wheel * 10f;
            if (Math.Abs(accumulated) < 0.5f) return 0;
            int steps = (int)Math.Round(accumulated);
            accumulated -= steps;
            return steps;
        }

        public static string Format(int minuteOfDay)
        {
            int minute = Normalize(minuteOfDay);
            return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
        }

        public static int Normalize(int minuteOfDay) => ((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
    }
}
