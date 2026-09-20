using System.Collections.Generic;
using UnityEngine;

namespace Zetra.ClashDash
{
    public enum Sfx
    {
        Jump,
        Land,
        Hit,
        Checkpoint,
        Beep,
        Go,
        Finish,
        Fall,
        Click
    }

    /// <summary>
    /// Synthesises all CLASHDASH sound effects and the ambient bed in code. Clips are built once and cached,
    /// so real audio assets can replace them later without touching gameplay code.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 44100;

        private enum Wave
        {
            Sine,
            Square,
            Saw
        }

        private static readonly Dictionary<Sfx, AudioClip> Cache = new Dictionary<Sfx, AudioClip>();
        private static readonly System.Random Rng = new System.Random(4242);
        private static AudioClip ambient;

        public static AudioClip Get(Sfx id)
        {
            AudioClip clip;
            if (Cache.TryGetValue(id, out clip) && clip != null) return clip;

            clip = Build(id);
            Cache[id] = clip;
            return clip;
        }

        /// <summary>Seamless 8-second looping pad (every layer completes whole cycles per loop).</summary>
        public static AudioClip GetAmbientLoop()
        {
            if (ambient != null) return ambient;

            const int rate = 16000;
            const double duration = 8.0;
            int count = (int)(rate * duration);
            float[] data = new float[count];

            int[] cycles = { 440, 660, 880, 1320, 1760, 2640 };       // 55, 82.5, 110, 165, 220, 330 Hz
            float[] amp = { 0.30f, 0.20f, 0.20f, 0.12f, 0.10f, 0.06f };
            int[] wobble = { 1, 2, 1, 3, 2, 4 };                       // amplitude wobble cycles per loop

            for (int i = 0; i < count; i++)
            {
                double t = i / (double)rate;
                double sum = 0.0;
                for (int k = 0; k < cycles.Length; k++)
                {
                    double freq = cycles[k] / duration;
                    double lfo = 0.65 + 0.35 * System.Math.Sin(2.0 * System.Math.PI * wobble[k] / duration * t + k);
                    sum += amp[k] * lfo * System.Math.Sin(2.0 * System.Math.PI * freq * t + k * 0.7);
                }
                data[i] = (float)(sum * 0.9);
            }

            ambient = MakeClip("CD_Ambient", data, rate);
            return ambient;
        }

        // ------------------------------------------------------------------ effect recipes

        private static AudioClip Build(Sfx id)
        {
            float[] b;
            switch (id)
            {
                case Sfx.Jump:
                    b = new float[Samples(0.16f)];
                    Sweep(b, 0f, 0.16f, 360f, 780f, 0.55f, Wave.Sine, 5f);
                    Sweep(b, 0f, 0.16f, 720f, 1560f, 0.15f, Wave.Sine, 6f);
                    break;

                case Sfx.Land:
                    b = new float[Samples(0.16f)];
                    Sweep(b, 0f, 0.16f, 140f, 45f, 0.8f, Wave.Sine, 4f);
                    Noise(b, 0f, 0.06f, 0.25f, 6f);
                    break;

                case Sfx.Hit:
                    b = new float[Samples(0.34f)];
                    Sweep(b, 0f, 0.34f, 240f, 60f, 0.55f, Wave.Square, 3.5f);
                    Noise(b, 0f, 0.2f, 0.5f, 4f);
                    break;

                case Sfx.Checkpoint:
                    b = new float[Samples(0.9f)];
                    Note(b, 0.00f, 523.25f, 0.35f, 0.5f, 5f);
                    Note(b, 0.09f, 659.25f, 0.35f, 0.5f, 5f);
                    Note(b, 0.18f, 783.99f, 0.35f, 0.5f, 5f);
                    Note(b, 0.27f, 1046.5f, 0.4f, 0.55f, 5f);
                    break;

                case Sfx.Beep:
                    b = new float[Samples(0.16f)];
                    Sweep(b, 0f, 0.16f, 880f, 880f, 0.5f, Wave.Sine, 2.5f);
                    Sweep(b, 0f, 0.16f, 1760f, 1760f, 0.12f, Wave.Sine, 3f);
                    break;

                case Sfx.Go:
                    b = new float[Samples(0.5f)];
                    Sweep(b, 0f, 0.5f, 1320f, 1320f, 0.5f, Wave.Sine, 3f);
                    Sweep(b, 0f, 0.5f, 1760f, 1760f, 0.25f, Wave.Sine, 3.5f);
                    Sweep(b, 0f, 0.12f, 660f, 1320f, 0.3f, Wave.Sine, 2f);
                    break;

                case Sfx.Finish:
                    b = new float[Samples(2.3f)];
                    Note(b, 0.00f, 523.25f, 0.3f, 0.5f, 4f);
                    Note(b, 0.12f, 659.25f, 0.3f, 0.5f, 4f);
                    Note(b, 0.24f, 783.99f, 0.3f, 0.5f, 4f);
                    Note(b, 0.36f, 1046.5f, 0.3f, 0.5f, 4f);
                    Note(b, 0.48f, 1318.5f, 0.3f, 0.6f, 4f);
                    Note(b, 0.68f, 1046.5f, 0.28f, 1.5f, 2.5f);
                    Note(b, 0.68f, 1318.5f, 0.24f, 1.5f, 2.5f);
                    Note(b, 0.68f, 1568f, 0.24f, 1.5f, 2.5f);
                    break;

                case Sfx.Fall:
                    b = new float[Samples(0.55f)];
                    Sweep(b, 0f, 0.55f, 700f, 70f, 0.5f, Wave.Saw, 2.5f);
                    Noise(b, 0f, 0.25f, 0.2f, 4f);
                    break;

                default: // Click
                    b = new float[Samples(0.06f)];
                    Sweep(b, 0f, 0.06f, 1400f, 900f, 0.4f, Wave.Sine, 6f);
                    break;
            }

            return MakeClip("CD_" + id, b, Rate);
        }

        // ------------------------------------------------------------------ synthesis helpers

        private static int Samples(float seconds)
        {
            return Mathf.CeilToInt(seconds * Rate);
        }

        private static float Osc(Wave wave, double phase)
        {
            switch (wave)
            {
                case Wave.Square:
                    return System.Math.Sin(phase) >= 0.0 ? 0.6f : -0.6f;
                case Wave.Saw:
                {
                    double p = phase / (2.0 * System.Math.PI);
                    p -= System.Math.Floor(p);
                    return (float)(2.0 * p - 1.0);
                }
                default:
                    return (float)System.Math.Sin(phase);
            }
        }

        private static void Sweep(float[] buf, float start, float duration, float f0, float f1, float volume, Wave wave, float decay)
        {
            int first = Mathf.Max(0, Mathf.RoundToInt(start * Rate));
            int count = Mathf.RoundToInt(duration * Rate);
            double phase = 0.0;

            for (int i = 0; i < count; i++)
            {
                int index = first + i;
                if (index >= buf.Length) break;

                float u = i / (float)count;
                float freq = f0 * Mathf.Pow(f1 / f0, u);
                phase += 2.0 * System.Math.PI * freq / Rate;
                phase %= 2.0 * System.Math.PI;

                float env = Mathf.Exp(-decay * u) * Mathf.Min(1f, i / 120f) * Mathf.Clamp01((count - i) / 200f);
                buf[index] += Osc(wave, phase) * volume * env;
            }
        }

        private static void Note(float[] buf, float start, float freq, float volume, float duration, float decay)
        {
            Sweep(buf, start, duration, freq, freq, volume, Wave.Sine, decay);
            Sweep(buf, start, duration * 0.7f, freq * 2f, freq * 2f, volume * 0.3f, Wave.Sine, decay + 1f);
        }

        private static void Noise(float[] buf, float start, float duration, float volume, float decay)
        {
            int first = Mathf.Max(0, Mathf.RoundToInt(start * Rate));
            int count = Mathf.RoundToInt(duration * Rate);
            float lowpass = 0f;

            for (int i = 0; i < count; i++)
            {
                int index = first + i;
                if (index >= buf.Length) break;

                float u = i / (float)count;
                float white = (float)(Rng.NextDouble() * 2.0 - 1.0);
                lowpass += (white - lowpass) * 0.35f;
                float env = Mathf.Exp(-decay * u) * Mathf.Min(1f, i / 60f) * Mathf.Clamp01((count - i) / 100f);
                buf[index] += lowpass * volume * env;
            }
        }

        private static AudioClip MakeClip(string name, float[] data, int rate)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > peak) peak = a;
            }
            if (peak > 0.95f)
            {
                float scale = 0.95f / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= scale;
            }

            AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}
