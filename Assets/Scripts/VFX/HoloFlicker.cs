using UnityEngine;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Gives world-space holographic signs a soft shimmer with occasional glitch dips. Only updates while the
    /// sign is reasonably close to the camera. Attached automatically by ClashDashExtrasBootstrap.
    /// </summary>
    public class HoloFlicker : MonoBehaviour
    {
        [SerializeField] private float baseAlpha = 0.8f;
        [SerializeField] private float shimmer = 0.2f;
        [SerializeField] private float cullDistance = 160f;

        private Text[] texts;
        private Color[] baseColors;
        private Transform cam;
        private float seed;
        private float glitchAt;
        private float glitchUntil;

        private void Awake()
        {
            texts = GetComponentsInChildren<Text>(true);
            baseColors = new Color[texts.Length];
            for (int i = 0; i < texts.Length; i++) baseColors[i] = texts[i].color;

            seed = Random.value * 100f;
            glitchAt = Time.time + Random.Range(3f, 9f);
        }

        private void Start()
        {
            if (Camera.main != null) cam = Camera.main.transform;
        }

        private void Update()
        {
            if (texts == null || texts.Length == 0) return;

            if (cam != null)
            {
                float limit = cullDistance * cullDistance;
                if ((transform.position - cam.position).sqrMagnitude > limit) return;
            }

            float t = Time.time;
            float noise = Mathf.PerlinNoise(t * 2.5f + seed, seed * 0.37f);
            float alpha = baseAlpha + (noise - 0.5f) * 2f * shimmer;

            if (t >= glitchAt)
            {
                glitchUntil = t + 0.12f;
                glitchAt = t + Random.Range(4f, 10f);
            }
            if (t < glitchUntil) alpha *= 0.25f;

            alpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < texts.Length; i++)
            {
                Color c = baseColors[i];
                c.a = baseColors[i].a * alpha;
                texts[i].color = c;
            }
        }
    }
}
