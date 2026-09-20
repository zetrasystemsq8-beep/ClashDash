using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Extra HUD layer: track progress bar with checkpoint ticks, coin counter, animated rank stamp and best-time
    /// comparison on finish, and a NEXT ARENA button. Attached automatically by ClashDashExtrasBootstrap.
    /// </summary>
    public class RunHud : MonoBehaviour
    {
        private const float BarWidth = 760f;

        private GameManager gm;
        private PlayerController player;
        private ArenaInfo arena;
        private float startZ;
        private float finishZ;
        private float previousBest;

        private RectTransform marker;
        private GameObject coinGroup;
        private Text coinText;

        private RectTransform stampRt;
        private Text stampText;
        private Text stampDetail;
        private Text coinSummary;
        private Button nextButton;

        private UnityAction onFinished;
        private System.Action onCoinsChanged;
        private float stampTime = -1f;
        private Color stampColor = Color.white;

        public void Bind(GameManager manager, PlayerController playerController, Transform finishGate, ArenaInfo arenaInfo, int coinsInArena)
        {
            gm = manager;
            player = playerController;
            arena = arenaInfo;
            previousBest = arena != null ? ProgressStore.BestTime(arena.Id) : 0f;

            startZ = player != null ? player.transform.position.z : 0f;
            finishZ = finishGate != null ? finishGate.position.z : startZ + 100f;

            Canvas canvas = ClashUi.CreateCanvas("RunHudUI", 15, transform);
            Transform root = canvas.transform;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();

            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 topRight = new Vector2(1f, 1f);
            Vector2 mid = new Vector2(0.5f, 0.5f);

            // ---- Progress bar ----
            RectTransform bar = ClashUi.Rect("ProgressBar", root, top, top, top, new Vector2(0f, -176f), new Vector2(BarWidth, 14f));
            ClashUi.Panel(bar, new Color(0f, 0f, 0f, 0.45f), true);

            Checkpoint[] checkpoints = FindCheckpoints();
            for (int i = 0; i < checkpoints.Length; i++)
            {
                float p = Mathf.Clamp01(Mathf.InverseLerp(startZ, finishZ, checkpoints[i].transform.position.z));
                RectTransform tick = ClashUi.Rect("Tick", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), mid,
                                                  new Vector2(p * BarWidth, 0f), new Vector2(6f, 26f));
                ClashUi.Panel(tick, new Color(0.45f, 1f, 0.8f, 0.9f), true);
            }

            RectTransform flag = ClashUi.Rect("Finish", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), mid, Vector2.zero, new Vector2(16f, 32f));
            ClashUi.Panel(flag, ClashUi.Gold, true);

            marker = ClashUi.Rect("Marker", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), mid, Vector2.zero, new Vector2(30f, 30f));
            Image markerImage = ClashUi.Panel(marker, Color.white, false);
            markerImage.sprite = ClashUi.Circle;

            // ---- Coin counter ----
            RectTransform coins = ClashUi.Rect("Coins", root, topRight, topRight, topRight, new Vector2(-40f, -170f), new Vector2(300f, 70f));
            coinGroup = coins.gameObject;
            RectTransform icon = ClashUi.Rect("Icon", coins, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(48f, 48f));
            Image iconImage = ClashUi.Panel(icon, ClashUi.Gold, false);
            iconImage.sprite = ClashUi.Circle;
            RectTransform label = ClashUi.Rect("Count", coins, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(230f, 70f));
            coinText = ClashUi.Label(label, "0", 52, TextAnchor.MiddleRight, ClashUi.Gold, true);
            coinGroup.SetActive(coinsInArena > 0);

            onCoinsChanged = RefreshCoins;
            RunStats.Changed += onCoinsChanged;
            RefreshCoins();

            // ---- Finish stamp ----
            stampRt = ClashUi.Rect("RankStamp", root, mid, mid, mid, new Vector2(700f, 190f), new Vector2(300f, 300f));
            stampText = ClashUi.Label(stampRt, string.Empty, 240, TextAnchor.MiddleCenter, Color.white, true);
            stampRt.gameObject.SetActive(false);

            RectTransform detailRt = ClashUi.Rect("StampDetail", root, mid, mid, mid, new Vector2(700f, 20f), new Vector2(520f, 120f));
            stampDetail = ClashUi.Label(detailRt, string.Empty, 38, TextAnchor.UpperCenter, Color.white, true);
            detailRt.gameObject.SetActive(false);

            RectTransform coinSumRt = ClashUi.Rect("CoinSummary", root, mid, mid, mid, new Vector2(700f, -80f), new Vector2(520f, 60f));
            coinSummary = ClashUi.Label(coinSumRt, string.Empty, 36, TextAnchor.MiddleCenter, ClashUi.Gold, true);
            coinSumRt.gameObject.SetActive(false);

            // ---- Next arena ----
            ArenaInfo next = ArenaCatalog.Next(arena);
            if (next != null && next.IsInBuild)
            {
                string sceneName = next.SceneName;
                nextButton = ClashUi.MakeButton(root, "NextArena", "NEXT ARENA", new Vector2(0.5f, 0f), new Vector2(-520f, 90f),
                                                new Vector2(420f, 120f), new Color(0.4f, 0.95f, 0.8f, 1f), () => LoadArena(sceneName), 50);
                nextButton.gameObject.SetActive(false);
            }

            if (raycaster != null && nextButton == null) Destroy(raycaster); // nothing to click on this layer

            if (gm != null)
            {
                onFinished = OnFinished;
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void Update()
        {
            if (marker != null && player != null && gm != null && gm.State != GameState.Finished)
            {
                float p = Mathf.Clamp01(Mathf.InverseLerp(startZ, finishZ, player.transform.position.z));
                marker.anchoredPosition = new Vector2(p * BarWidth, 0f);
            }

            if (stampTime >= 0f && stampRt != null)
            {
                stampTime += Time.unscaledDeltaTime;
                float t = stampTime;

                float scale = 1f + 2.2f * Mathf.Exp(-7f * t) * Mathf.Cos(10f * t);
                stampRt.localScale = Vector3.one * Mathf.Max(0.1f, scale);
                stampRt.localRotation = Quaternion.Euler(0f, 0f, -8f + Mathf.Exp(-5f * t) * 20f * Mathf.Sin(9f * t));

                Color c = stampColor;
                c.a = Mathf.Clamp01(t * 8f);
                stampText.color = c;
            }
        }

        private void OnFinished()
        {
            if (gm == null) return;

            string rank = arena != null ? ArenaCatalog.ComputeRank(gm.TotalTime, gm.Mistakes, arena.ParTime) : "C";
            stampColor = RankColor(rank);
            stampText.text = rank;
            stampRt.gameObject.SetActive(true);
            stampTime = 0f;

            string detail;
            float total = gm.TotalTime;
            if (previousBest <= 0f)
            {
                detail = "FIRST CLEAR";
            }
            else if (total < previousBest)
            {
                detail = "NEW BEST TIME\n-" + (previousBest - total).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s";
            }
            else
            {
                detail = "+" + (total - previousBest).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s vs BEST";
            }
            stampDetail.text = detail;
            stampDetail.gameObject.SetActive(true);

            int banked = RunStats.Bank();
            if (banked > 0)
            {
                coinSummary.text = "+" + banked + " COINS COLLECTED";
                coinSummary.gameObject.SetActive(true);
            }

            if (nextButton != null) nextButton.gameObject.SetActive(true);
        }

        private void RefreshCoins()
        {
            if (coinText != null) coinText.text = RunStats.CoinsCollected.ToString();
        }

        private static Color RankColor(string rank)
        {
            switch (rank)
            {
                case "S": return new Color(1f, 0.85f, 0.35f, 1f);
                case "A": return new Color(0.45f, 0.95f, 1f, 1f);
                case "B": return new Color(0.5f, 1f, 0.6f, 1f);
                default: return new Color(0.8f, 0.85f, 0.95f, 1f);
            }
        }

        private static void LoadArena(string sceneName)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(sceneName);
        }

        private static Checkpoint[] FindCheckpoints()
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<Checkpoint>();
#endif
        }

        private void OnDestroy()
        {
            RunStats.Changed -= onCoinsChanged;
            if (gm != null && onFinished != null) gm.onFinished.RemoveListener(onFinished);
        }
    }
}
