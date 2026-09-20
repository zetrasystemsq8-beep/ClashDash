using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    public enum GameState
    {
        Countdown,
        Running,
        Finished
    }

    /// <summary>
    /// Runs one arena: countdown, timer, checkpoints, mistakes/penalties, combo, XP + coins, results.
    /// The HUD fields are a lightweight built-in display; a full HUD can replace them via the events.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        private const string KeyTotalXp = "CD_TOTAL_XP";
        private const string KeyTotalCoins = "CD_TOTAL_COINS";
        private const string KeyBestPrefix = "CD_BEST_";

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static GameManager Instance { get; private set; }

        [Header("Arena")]
        [SerializeField] private string arenaId = "ARENA_01";
        [SerializeField] private string arenaTitle = "ARENA 01 - THE TEST";
        [SerializeField] private float parTime = 95f;
        [SerializeField] private float countdownSeconds = 3f;

        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Checkpoint[] checkpoints;

        [Header("Basic HUD")]
        [SerializeField] private Text timerText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text bannerText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;

        [Header("Scoring")]
        [SerializeField] private int baseXp = 200;
        [SerializeField] private int xpPerCheckpoint = 50;
        [SerializeField] private float xpPerSecondUnderPar = 8f;
        [SerializeField] private float comboXpStep = 0.1f;
        [SerializeField] private int coinsPerCheckpoint = 10;
        [SerializeField] private int cleanRunCoins = 50;
        [SerializeField] private float defaultMistakePenalty = 2f;
        [SerializeField] private float fallPenalty = 3f;

        [Header("Events")]
        public UnityEvent onCountdownStarted = new UnityEvent();
        public UnityEvent onRunStarted = new UnityEvent();
        public UnityEvent onCheckpointReached = new UnityEvent();
        public UnityEvent onMistake = new UnityEvent();
        public UnityEvent onRespawned = new UnityEvent();
        public UnityEvent onFinished = new UnityEvent();

        public GameState State { get; private set; } = GameState.Countdown;
        public float Elapsed { get; private set; }
        public float PenaltySeconds { get; private set; }
        public float TotalTime => Elapsed + PenaltySeconds;
        public int Combo { get; private set; }
        public int Mistakes { get; private set; }
        public int CheckpointsReached { get; private set; }
        public int CoinsEarned { get; private set; }
        public Transform ActiveRespawn => activeRespawn != null ? activeRespawn : startPoint;

        private Transform activeRespawn;
        private int activeIndex;
        private int bestCombo;
        private int mistakesSinceCheckpoint;
        private float countdownTimer;
        private int lastCountdownShown = -1;
        private float bannerHideTime;
        private float nextHudTime;
        private bool statsDirty = true;
        private readonly StringBuilder sb = new StringBuilder(160);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            activeRespawn = startPoint;
            if (resultPanel != null) resultPanel.SetActive(false);

            if (player == null)
            {
                Debug.LogError("[CLASHDASH] GameManager has no PlayerController assigned.");
                return;
            }

            State = GameState.Countdown;
            countdownTimer = Mathf.Max(0.1f, countdownSeconds);
            player.SetInputLocked(true);
            onCountdownStarted.Invoke();
            RefreshHud(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (State == GameState.Countdown) TickCountdown(dt);
            else if (State == GameState.Running) Elapsed += dt;

            if (bannerHideTime > 0f && Time.time >= bannerHideTime)
            {
                bannerHideTime = 0f;
                if (bannerText != null) bannerText.text = string.Empty;
            }

            RefreshHud(false);
        }

        private void TickCountdown(float dt)
        {
            countdownTimer -= dt;
            if (countdownTimer <= 0f)
            {
                BeginRun();
                return;
            }

            int shown = Mathf.CeilToInt(countdownTimer);
            if (shown != lastCountdownShown)
            {
                lastCountdownShown = shown;
                SetBanner(shown.ToString(Culture), 0f);
            }
        }

        private void BeginRun()
        {
            State = GameState.Running;
            if (player != null) player.SetInputLocked(false);
            SetBanner("GO", 0.9f);
            statsDirty = true;
            onRunStarted.Invoke();
        }

        // ---- Checkpoints / mistakes ----

        public bool TryActivateCheckpoint(Checkpoint checkpoint)
        {
            if (State != GameState.Running || checkpoint == null) return false;
            if (checkpoint.Index <= activeIndex) return false;

            activeIndex = checkpoint.Index;
            activeRespawn = checkpoint.RespawnPoint;
            CheckpointsReached++;
            CoinsEarned += coinsPerCheckpoint;

            if (mistakesSinceCheckpoint == 0)
            {
                Combo++;
                if (Combo > bestCombo) bestCombo = Combo;
            }
            mistakesSinceCheckpoint = 0;

            statsDirty = true;
            SetBanner("CHECKPOINT", 1.4f);
            onCheckpointReached.Invoke();
            return true;
        }

        public void RegisterMistake(float penaltySeconds, string reason)
        {
            if (State != GameState.Running) return;

            float penalty = penaltySeconds > 0f ? penaltySeconds : defaultMistakePenalty;
            PenaltySeconds += penalty;
            Mistakes++;
            mistakesSinceCheckpoint++;
            Combo = 0;

            statsDirty = true;
            SetBanner(reason + "  +" + penalty.ToString("0.0", Culture) + "s", 1.2f);
            onMistake.Invoke();
        }

        public void HandlePlayerFell()
        {
            if (State == GameState.Running) RegisterMistake(fallPenalty, "FELL");
            RespawnPlayer();
        }

        public void RespawnPlayer()
        {
            Transform point = ActiveRespawn;
            if (player == null || point == null) return;

            player.TeleportTo(point.position, point.rotation);
            onRespawned.Invoke();
        }

        // ---- Finish ----

        public void CompleteArena()
        {
            if (State != GameState.Running) return;

            State = GameState.Finished;
            if (player != null) player.SetInputLocked(true);

            float total = TotalTime;
            float underPar = Mathf.Max(0f, parTime - total);
            float multiplier = 1f + bestCombo * comboXpStep;

            int xp = Mathf.RoundToInt((baseXp + CheckpointsReached * xpPerCheckpoint + underPar * xpPerSecondUnderPar) * multiplier);

            string rank;
            int rankCoins;
            if (Mistakes <= 1 && total <= parTime * 0.85f) { rank = "S"; rankCoins = 100; }
            else if (total <= parTime) { rank = "A"; rankCoins = 60; }
            else if (total <= parTime * 1.4f) { rank = "B"; rankCoins = 30; }
            else { rank = "C"; rankCoins = 10; }

            int coins = CoinsEarned + rankCoins + (Mistakes == 0 ? cleanRunCoins : 0);
            CoinsEarned = coins;

            string bestKey = KeyBestPrefix + arenaId;
            float previousBest = PlayerPrefs.GetFloat(bestKey, 0f);
            bool newBest = previousBest <= 0f || total < previousBest;
            if (newBest) PlayerPrefs.SetFloat(bestKey, total);
            PlayerPrefs.SetInt(KeyTotalXp, PlayerPrefs.GetInt(KeyTotalXp, 0) + xp);
            PlayerPrefs.SetInt(KeyTotalCoins, PlayerPrefs.GetInt(KeyTotalCoins, 0) + coins);
            PlayerPrefs.Save();

            sb.Length = 0;
            sb.Append(arenaTitle).Append('\n').Append('\n');
            sb.Append("RANK  ").Append(rank).Append('\n').Append('\n');
            sb.Append("TIME  ").Append(FormatTime(total)).Append('\n');
            sb.Append("PENALTY  +").Append(PenaltySeconds.ToString("0.0", Culture)).Append("s   MISTAKES  ").Append(Mistakes).Append('\n');
            sb.Append("BEST COMBO  x").Append(bestCombo).Append('\n').Append('\n');
            sb.Append("XP  +").Append(xp).Append("     COINS  +").Append(coins).Append('\n');
            if (newBest) sb.Append('\n').Append("NEW BEST TIME");

            if (resultText != null) resultText.text = sb.ToString();
            if (resultPanel != null) resultPanel.SetActive(true);
            SetBanner("FINISH", 0f);
            statsDirty = true;
            RefreshHud(true);
            onFinished.Invoke();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        // ---- HUD ----

        private void SetBanner(string text, float duration)
        {
            if (bannerText == null) return;
            bannerText.text = text;
            bannerHideTime = duration > 0f ? Time.time + duration : 0f;
        }

        private void RefreshHud(bool force)
        {
            if (!force && Time.unscaledTime < nextHudTime) return;
            nextHudTime = Time.unscaledTime + 0.05f;

            if (timerText != null && (State == GameState.Running || force))
            {
                timerText.text = FormatTime(TotalTime);
            }

            if (statsText != null && (statsDirty || force))
            {
                statsDirty = false;
                int total = checkpoints != null ? checkpoints.Length : 0;
                sb.Length = 0;
                sb.Append("COINS  ").Append(CoinsEarned).Append('\n');
                sb.Append("COMBO  x").Append(Combo).Append('\n');
                sb.Append("CHECKPOINT  ").Append(CheckpointsReached).Append('/').Append(total).Append('\n');
                sb.Append("MISTAKES  ").Append(Mistakes);
                statsText.text = sb.ToString();
            }
        }

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return string.Format(Culture, "{0:00}:{1:00.00}", minutes, rest);
        }

        public void Bind(PlayerController playerController, Transform spawn, Checkpoint[] arenaCheckpoints,
                         Text timer, Text stats, Text banner, GameObject panel, Text result, float par)
        {
            player = playerController;
            startPoint = spawn;
            checkpoints = arenaCheckpoints;
            timerText = timer;
            statsText = stats;
            bannerText = banner;
            resultPanel = panel;
            resultText = result;
            parTime = par;
        }
    }
}
