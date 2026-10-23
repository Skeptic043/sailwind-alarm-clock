using System;

namespace AlarmClock
{
    // Reads the mono 16-bit PCM WAV files the recorded alarms ship as. PCM keeps the
    // loop length exact to the sample, which the seamless loop and tail depend on.
    internal static class Wav
    {
        public static bool TryRead(byte[] bytes, out float[] samples, out int rate, out string problem)
        {
            samples = null;
            rate = 0;
            problem = null;
            if (bytes.Length < 12 || Tag(bytes, 0) != "RIFF" || Tag(bytes, 8) != "WAVE")
            {
                problem = "not a WAV file";
                return false;
            }
            bool formatRead = false;
            for (int at = 12; at + 8 <= bytes.Length;)
            {
                string id = Tag(bytes, at);
                int size = BitConverter.ToInt32(bytes, at + 4);
                int body = at + 8;
                if (size < 0 || body + size > bytes.Length) break;
                if (id == "fmt " && size >= 16)
                {
                    int format = BitConverter.ToInt16(bytes, body);
                    int channels = BitConverter.ToInt16(bytes, body + 2);
                    int bits = BitConverter.ToInt16(bytes, body + 14);
                    if (format != 1 || channels != 1 || bits != 16)
                    {
                        problem = "not mono 16-bit PCM";
                        return false;
                    }
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    formatRead = true;
                }
                else if (id == "data" && formatRead)
                {
                    samples = new float[size / 2];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = BitConverter.ToInt16(bytes, body + i * 2) / 32768f;
                    break;
                }
                // Chunks are padded to an even size.
                at = body + size + (size & 1);
            }
            if (samples != null && samples.Length > 0) return true;
            problem = "no audio";
            return false;
        }

        private static string Tag(byte[] bytes, int at) =>
            at + 4 <= bytes.Length ? System.Text.Encoding.ASCII.GetString(bytes, at, 4) : "";
    }
}
