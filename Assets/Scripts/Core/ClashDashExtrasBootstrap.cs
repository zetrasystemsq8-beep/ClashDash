using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Runs automatically (no scene setup needed). Whenever an arena scene loads, attaches everything that makes
    /// the run feel complete: run HUD, intro flyover, finish orbit camera, finish portal, holo-sign flicker,
    /// atmosphere, ambient streaks, adaptive music, performance guard, haptics, debug tools, and correct
    /// per-arena progression.
    /// </summary>
    public static class ClashDashExtrasBootstrap
    {
        // Ring centre above the FinishGate object's origin (matches the gate built by the arena builders).
        private static readonly Vector3 PortalOffset = new Vector3(0f, 6.5f, 0f);

        private static GameManager attachedTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            attachedTo = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm == attachedTo) return;

            PlayerController player = ClashDashBootstrap.FindOne<PlayerController>();
            if (player == null) return;

            attachedTo = gm;

            RunStats.ResetRun();

            ArenaInfo arena = ArenaCatalog.Current();
            FinishGate gate = ClashDashBootstrap.FindOne<FinishGate>();
            Transform gateTransform = gate != null ? gate.transform : null;
            Camera cam = Camera.main;

            GameObject root = new GameObject("ClashDashExtras");

            // World feel
            root.AddComponent<WorldAtmosphere>().Bind(gm, player);
            root.AddComponent<ArenaMusic>().Bind(gm, player);

            Vector3 start = player.transform.position;
            Vector3 finish = gateTransform != null ? gateTransform.position : start + new Vector3(0f, 0f, 300f);
            root.AddComponent<AmbientStreaks>().Bind(start, finish);
            root.AddComponent<PerformanceGuard>();
            root.AddComponent<HapticsFx>().Bind(player);

            // HUD layer
            int coins = CountCoins();
            root.AddComponent<RunHud>().Bind(gm, player, gateTransform, arena, coins);

            // Camera cinematics
            if (cam != null)
            {
                IntroFlyover flyover = cam.gameObject.AddComponent<IntroFlyover>();
                flyover.Bind(gm, cam.transform, gateTransform);

                FinishOrbitCamera orbit = cam.gameObject.AddComponent<FinishOrbitCamera>();
                orbit.Bind(gm, cam.transform, player.transform);
            }

            // Finish portal inside the gate ring
            if (gateTransform != null)
            {
                GameObject anchor = new GameObject("PortalAnchor");
                anchor.transform.SetParent(gateTransform, false);
                anchor.transform.localPosition = PortalOffset;
                anchor.AddComponent<FinishPortal>();
            }

            AttachHoloFlicker();

            // Developer tools (Editor and development builds only)
            if (Debug.isDebugBuild)
            {
                root.AddComponent<DebugOverlay>().Bind(gm, player, gateTransform);
            }

            // Progression: the arena that was actually played is the one that gets completed.
            string arenaId = arena != null ? arena.Id : ProgressStore.Arena01;
            gm.onFinished.AddListener(() => ProgressStore.MarkArenaCompleted(arenaId));
            root.AddComponent<ProgressGuard>().Bind(gm, arena);
        }

        private static int CountCoins()
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<CoinPickup>().Length;
#endif
        }

        private static void AttachHoloFlicker()
        {
#if UNITY_2022_2_OR_NEWER
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
#endif
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                if (canvas.GetComponent<HoloFlicker>() != null) continue;
                canvas.gameObject.AddComponent<HoloFlicker>();
            }
        }
    }
}
