using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zetra.ClashDash
{
    /// <summary>Adds ZetraPostFx to the main camera of every scene automatically.</summary>
    public static class ZetraPostFxInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<ZetraPostFx>() != null) return;
            cam.gameObject.AddComponent<ZetraPostFx>();
        }
    }

    /// <summary>
    /// Full-screen look: neon bloom (the glowing strips, portals and hazards actually glow), filmic tone mapping,
    /// a touch of saturation/contrast, and a soft vignette. Built-in render pipeline only (does nothing under URP).
    /// If the device runs slowly for several seconds the effect switches itself off.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class ZetraPostFx : MonoBehaviour
    {
        [Header("Glow")]
        [SerializeField, Range(0.3f, 2f)] private float threshold = 0.85f;
        [SerializeField, Range(0f, 3f)] private float bloomIntensity = 0.9f;
        [SerializeField, Range(0.5f, 3f)] private float blurSize = 1.6f;

        [Header("Grade")]
        [SerializeField, Range(0.5f, 2f)] private float exposure = 1.15f;
        [SerializeField, Range(0.8f, 1.5f)] private float contrast = 1.08f;
        [SerializeField, Range(0.5f, 1.8f)] private float saturation = 1.15f;
        [SerializeField, Range(0f, 1.5f)] private float vignette = 0.45f;
        [SerializeField] private Color tint = new Color(1f, 0.985f, 1f, 1f);

        [Header("Safety")]
        [SerializeField] private bool autoDisableWhenSlow = true;

        private Material material;
        private float smoothedFrameTime = 1f / 60f;
        private float slowTimer;

        private void OnEnable()
        {
            Shader shader = Resources.Load<Shader>("Shaders/CD_PostFx");
            if (shader == null || !shader.isSupported)
            {
                enabled = false;
                return;
            }

            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;

            Camera cam = GetComponent<Camera>();
            cam.allowHDR = true;
        }

        private void OnDisable()
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }

        private void Update()
        {
            if (!autoDisableWhenSlow || Application.isEditor) return;

            smoothedFrameTime = Mathf.Lerp(smoothedFrameTime, Time.unscaledDeltaTime, 0.05f);
            if (Time.timeSinceLevelLoad < 3f) return;

            slowTimer = smoothedFrameTime > 1f / 38f ? slowTimer + Time.unscaledDeltaTime : 0f;
            if (slowTimer > 4f) enabled = false;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int width = Mathf.Max(8, source.width / 2);
            int height = Mathf.Max(8, source.height / 2);

            RenderTexture half = RenderTexture.GetTemporary(width, height, 0, source.format);
            RenderTexture quarterA = RenderTexture.GetTemporary(width / 2, height / 2, 0, source.format);
            RenderTexture quarterB = RenderTexture.GetTemporary(width / 2, height / 2, 0, source.format);
            half.filterMode = FilterMode.Bilinear;
            quarterA.filterMode = FilterMode.Bilinear;
            quarterB.filterMode = FilterMode.Bilinear;

            // Bright pass at half resolution, then blur at quarter resolution (two widths for a soft, wide glow).
            material.SetFloat("_Threshold", threshold);
            Graphics.Blit(source, half, material, 0);
            Graphics.Blit(half, quarterA);

            material.SetFloat("_BlurSize", blurSize);
            Graphics.Blit(quarterA, quarterB, material, 1);
            Graphics.Blit(quarterB, quarterA, material, 2);

            material.SetFloat("_BlurSize", blurSize * 2.2f);
            Graphics.Blit(quarterA, quarterB, material, 1);
            Graphics.Blit(quarterB, quarterA, material, 2);

            // Composite
            material.SetTexture("_BloomTex", quarterA);
            material.SetFloat("_BloomIntensity", bloomIntensity);
            material.SetFloat("_Exposure", exposure);
            material.SetFloat("_Contrast", contrast);
            material.SetFloat("_Saturation", saturation);
            material.SetFloat("_Vignette", vignette);
            material.SetColor("_Tint", tint);
            Graphics.Blit(source, destination, material, 3);

            RenderTexture.ReleaseTemporary(half);
            RenderTexture.ReleaseTemporary(quarterA);
            RenderTexture.ReleaseTemporary(quarterB);
        }
    }
}
