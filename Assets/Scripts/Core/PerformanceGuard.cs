using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Watches the frame rate and steps quality down when a device can't hold ~60 FPS. It only ever goes down
    /// during a session, never back up, to avoid flip-flopping. Attached automatically by ClashDashExtrasBootstrap.
    /// </summary>
    public class PerformanceGuard : MonoBehaviour
    {
        [SerializeField] private float warmupSeconds = 5f;
        [SerializeField] private float windowSeconds = 3f;
        [SerializeField] private float lowFps = 48f;
        [SerializeField] private float veryLowFps = 34f;

        /// <summary>0 = full quality, 1 = reduced shadows/lights, 2 = no shadows + 75% resolution, 3 = also capped to 30 FPS.</summary>
        public static int Tier { get; private set; }
        public static float LastFps { get; private set; }

        private float accumulated;
        private int frames;
        private float warmup;
        private int nativeWidth;
        private int nativeHeight;

        private void Start()
        {
            warmup = warmupSeconds;
            nativeWidth = Screen.width;
            nativeHeight = Screen.height;
            Tier = 0;
        }

        private void Update()
        {
            // Never touch project quality settings from the Editor; this is for real devices only.
            if (Application.isEditor) return;
            if (Time.timeScale <= 0f) return;

            float dt = Time.unscaledDeltaTime;

            if (warmup > 0f)
            {
                warmup -= dt;
                return;
            }

            accumulated += dt;
            frames++;

            if (accumulated < windowSeconds) return;

            float fps = frames / accumulated;
            LastFps = fps;
            accumulated = 0f;
            frames = 0;

            if (Tier < 3 && fps < veryLowFps) SetTier(Tier + 1 < 2 ? 2 : Tier + 1);
            else if (Tier < 2 && fps < lowFps) SetTier(Tier + 1);
        }

        private void SetTier(int tier)
        {
            Tier = Mathf.Clamp(tier, 0, 3);

            if (Tier >= 1)
            {
                QualitySettings.shadowDistance = 30f;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.softParticles = false;
                QualitySettings.lodBias = 0.7f;

                AmbientStreaks streaks = GetComponent<AmbientStreaks>();
                if (streaks != null) streaks.enabled = false;
            }

            if (Tier >= 2)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                if (nativeWidth > 0 && nativeHeight > 0)
                {
                    Screen.SetResolution(Mathf.RoundToInt(nativeWidth * 0.75f), Mathf.RoundToInt(nativeHeight * 0.75f), true);
                }
            }

            if (Tier >= 3)
            {
                Application.targetFrameRate = 30;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log("[CLASHDASH] PerformanceGuard: measured " + LastFps.ToString("0") + " FPS, quality tier is now " + Tier);
            }
        }
    }
}
