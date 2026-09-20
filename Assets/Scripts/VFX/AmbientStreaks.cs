using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Environmental motion: a handful of glowing light streaks sweep in wide spirals around the arena so the
    /// world feels alive even when the player stands still. Attached automatically by ClashDashExtrasBootstrap.
    /// </summary>
    public class AmbientStreaks : MonoBehaviour
    {
        [SerializeField] private int count = 18;

        private struct Streak
        {
            public Transform transform;
            public float radiusX;
            public float radiusZ;
            public float height;
            public float wobble;
            public float speed;
            public float phase;
        }

        private Streak[] streaks;
        private Material material;
        private Vector3 center;

        public void Bind(Vector3 arenaStart, Vector3 arenaFinish)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                enabled = false;
                return;
            }

            material = new Material(shader);
            material.mainTexture = GlowTexture.Glow();

            center = (arenaStart + arenaFinish) * 0.5f;
            float length = Mathf.Max(60f, Vector3.Distance(arenaStart, arenaFinish));

            Color[] palette =
            {
                new Color(0.4f, 0.9f, 1f, 0.9f),
                new Color(0.7f, 0.55f, 1f, 0.9f),
                new Color(1f, 0.82f, 0.45f, 0.9f)
            };

            streaks = new Streak[count];
            System.Random rng = new System.Random(1337);

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("Streak_" + i);
                go.transform.SetParent(transform, false);

                TrailRenderer trail = go.AddComponent<TrailRenderer>();
                trail.time = 1.4f;
                trail.minVertexDistance = 1f;
                trail.widthMultiplier = 0.45f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.sharedMaterial = material;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                Color c = palette[i % palette.Length];
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = gradient;

                Streak s = new Streak();
                s.transform = go.transform;
                s.radiusX = 35f + (float)rng.NextDouble() * 90f;
                s.radiusZ = length * (0.35f + (float)rng.NextDouble() * 0.35f);
                s.height = -5f + (float)rng.NextDouble() * 60f;
                s.wobble = 4f + (float)rng.NextDouble() * 14f;
                s.speed = (0.12f + (float)rng.NextDouble() * 0.16f) * (rng.NextDouble() > 0.5 ? 1f : -1f);
                s.phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                streaks[i] = s;

                go.transform.position = Evaluate(s, 0f);
            }
        }

        private Vector3 Evaluate(Streak s, float time)
        {
            float a = s.phase + time * s.speed;
            return center + new Vector3(Mathf.Cos(a) * s.radiusX,
                                        s.height + Mathf.Sin(a * 2f) * s.wobble,
                                        Mathf.Sin(a) * s.radiusZ);
        }

        private void Update()
        {
            if (streaks == null) return;

            float t = Time.time;
            for (int i = 0; i < streaks.Length; i++)
            {
                streaks[i].transform.position = Evaluate(streaks[i], t);
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
