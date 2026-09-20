using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Adaptive procedural music: three synchronised looping layers (drums, bass, arpeggio) at 120 BPM in A minor.
    /// Layers fade in and out with the run state, checkpoints reached and player speed.
    /// Attached automatically by ClashDashExtrasBootstrap.
    /// </summary>
    public class ArenaMusic : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float Bpm = 120f;
        private const int Bars = 4;

        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;

        private static AudioClip drumsClip;
        private static AudioClip bassClip;
        private static AudioClip leadClip;

        private AudioSource drums;
        private AudioSource bass;
        private AudioSource lead;
        private GameManager gm;
        private PlayerController player;
        private UnityAction onFinished;

        public void Bind(GameManager manager, PlayerController playerController)
        {
            gm = manager;
            player = playerController;

            if (drumsClip == null) BuildClips();

            drums = CreateSource(drumsClip);
            bass = CreateSource(bassClip);
            lead = CreateSource(leadClip);

            double start = AudioSettings.dspTime + 0.25;
            drums.PlayScheduled(start);
            bass.PlayScheduled(start);
            lead.PlayScheduled(start);

            if (gm != null)
            {
                onFinished = () => { };
                gm.onFinished.AddListener(onFinished);
            }
        }

        private AudioSource CreateSource(AudioClip clip)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void Update()
        {
            if (gm == null || drums == null) return;

            float drumsTarget = 0f;
            float bassTarget = 0f;
            float leadTarget = 0f;

            switch (gm.State)
            {
                case GameState.Countdown:
                    bassTarget = 0.25f;
                    break;

                case GameState.Running:
                    {
                        float speed = player != null ? player.PlanarSpeed01 : 0f;
                        drumsTarget = 0.55f + 0.25f * speed;
                        bassTarget = 0.55f;
                        leadTarget = gm.CheckpointsReached > 0 ? 0.55f : 0.2f;
                        break;
                    }

                case GameState.Finished:
                    bassTarget = 0.25f;
                    leadTarget = 0.5f;
                    break;
            }

            float step = Time.unscaledDeltaTime * 0.8f;
            drums.volume = Mathf.MoveTowards(drums.volume, drumsTarget * musicVolume, step * musicVolume);
            bass.volume = Mathf.MoveTowards(bass.volume, bassTarget * musicVolume, step * musicVolume);
            lead.volume = Mathf.MoveTowards(lead.volume, leadTarget * musicVolume, step * musicVolume);
        }

        private void OnDestroy()
        {
            if (gm != null && onFinished != null) gm.onFinished.RemoveListener(onFinished);
        }

        // ------------------------------------------------------------------ synthesis

        private static void BuildClips()
        {
            float beat = 60f / Bpm;
            float length = Bars * 4 * beat;                 // 8 seconds
            int count = Mathf.RoundToInt(length * Rate);

            float[] drumData = new float[count];
            float[] bassData = new float[count];
            float[] leadData = new float[count];

            // Chord roots (A minor: Am, F, G, Em) and arpeggio notes per bar.
            float[] roots = { 55f, 43.65f, 49f, 41.2f };
            float[][] arps =
            {
                new[] { 220f, 261.63f, 329.63f, 440f },
                new[] { 174.61f, 220f, 261.63f, 349.23f },
                new[] { 196f, 246.94f, 293.66f, 392f },
                new[] { 164.81f, 196f, 246.94f, 329.63f }
            };
            int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 2, 1, 3, 2 };

            System.Random rng = new System.Random(2024);

            for (int bar = 0; bar < Bars; bar++)
            {
                float barStart = bar * 4 * beat;

                for (int b = 0; b < 4; b++)
                {
                    float t = barStart + b * beat;

                    Kick(drumData, t);
                    if (b == 1 || b == 3) Snare(drumData, t, rng);
                    Hat(drumData, t + beat * 0.5f, rng);

                    Tone(bassData, t, beat * 0.9f, roots[bar], 0.55f, 3f, false);
                    Tone(bassData, t + beat * 0.5f, beat * 0.4f, roots[bar] * 2f, 0.22f, 5f, false);
                }

                for (int s = 0; s < 16; s++)
                {
                    float t = barStart + s * beat * 0.25f;
                    Tone(leadData, t, beat * 0.45f, arps[bar][pattern[s]], 0.35f, 7f, true);
                }
            }

            drumsClip = Finish("CD_Music_Drums", drumData);
            bassClip = Finish("CD_Music_Bass", bassData);
            leadClip = Finish("CD_Music_Lead", leadData);
        }

        private static void Kick(float[] buf, float start)
        {
            int first = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(0.2f * Rate);
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                int idx = first + i;
                if (idx >= buf.Length) break;
                float u = i / (float)count;
                double freq = 150.0 * System.Math.Pow(45.0 / 150.0, u);
                phase += 2.0 * System.Math.PI * freq / Rate;
                float env = Mathf.Exp(-5f * u) * Mathf.Clamp01((count - i) / 100f);
                buf[idx] += (float)System.Math.Sin(phase) * 0.9f * env;
            }
        }

        private static void Snare(float[] buf, float start, System.Random rng)
        {
            int first = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(0.14f * Rate);
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                int idx = first + i;
                if (idx >= buf.Length) break;
                float u = i / (float)count;
                float env = Mathf.Exp(-6f * u) * Mathf.Clamp01((count - i) / 100f);
                phase += 2.0 * System.Math.PI * 190.0 / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                buf[idx] += (noise * 0.45f + (float)System.Math.Sin(phase) * 0.25f) * env;
            }
        }

        private static void Hat(float[] buf, float start, System.Random rng)
        {
            int first = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(0.05f * Rate);
            float previous = 0f;
            for (int i = 0; i < count; i++)
            {
                int idx = first + i;
                if (idx >= buf.Length) break;
                float u = i / (float)count;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float high = white - previous;
                previous = white;
                buf[idx] += high * 0.18f * Mathf.Exp(-8f * u) * Mathf.Clamp01((count - i) / 40f);
            }
        }

        /// <summary>Simple plucked/bass tone. Duration is clamped to the buffer so the loop never clicks.</summary>
        private static void Tone(float[] buf, float start, float duration, float freq, float volume, float decay, bool bright)
        {
            int first = Mathf.RoundToInt(start * Rate);
            int count = Mathf.Min(Mathf.RoundToInt(duration * Rate), buf.Length - first);
            if (count <= 0) return;

            double w = 2.0 * System.Math.PI * freq / Rate;
            for (int i = 0; i < count; i++)
            {
                int idx = first + i;
                double p = w * i;
                float u = i / (float)count;
                float env = Mathf.Exp(-decay * u) * Mathf.Min(1f, i / 60f) * Mathf.Clamp01((count - i) / 120f);

                float s = (float)System.Math.Sin(p);
                if (bright) s += (float)(System.Math.Sin(p * 2.0) * 0.4 + System.Math.Sin(p * 3.0) * 0.18);
                else s += (float)(System.Math.Sin(p * 2.0) * 0.25);

                buf[idx] += s * volume * env;
            }
        }

        private static AudioClip Finish(string name, float[] data)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > peak) peak = a;
            }
            if (peak > 0.001f)
            {
                float scale = 0.9f / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= scale;
            }

            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}
