using System;
using System.ComponentModel;

namespace AlarmClock
{
    // Configuration Manager shows the descriptions. The config file stores the names.
    internal enum AlarmSound
    {
        [Description("Original Sailwind bell")] OriginalBell,
        [Description("Warm mechanical clock")] WarmClock,
        [Description("Bright twin-bell clock")] BrightClock,
    }

    internal static class AlarmSounds
    {
        public const int NeutralTone = 50;
        // The tone slider moves every sound up to four semitones from its natural
        // pitch, so the same setting changes each sound by a similar amount.
        private const float SemitoneSpan = 4f;

        // The recorded clips are named <stem>-loop.wav and <stem>-tail.wav. The original bell has none.
        public static string FileStem(AlarmSound sound) =>
            sound == AlarmSound.WarmClock ? "warm-clock" : sound == AlarmSound.BrightClock ? "bright-clock" : null;

        public static float Pitch(AlarmSound sound, int tone) =>
            NaturalPitch(sound) * (float)Math.Pow(2, (tone - NeutralTone) / (double)NeutralTone * SemitoneSpan / 12);

        // The native ding is a large bell. At 2.5 times its pitch its partials sit
        // where measured small alarm bells have theirs (see the timepieces research
        // page), so that is where it belongs among the recorded alarm clocks.
        private static float NaturalPitch(AlarmSound sound) => sound == AlarmSound.OriginalBell ? 2.5f : 1f;
    }
}
