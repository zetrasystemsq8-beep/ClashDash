using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>
    /// CLASHDASH main menu. Put this on an empty GameObject in the menu scene; the whole UI is built in code.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public const string MenuSceneName = "ClashDash_MainMenu";

        [SerializeField] private string arenaSceneName = "Arena01_TheTest";

        private RectTransform title;
        private RectTransform enterButtonRt;
        private Button enterButton;
        private Text enterLabel;
        private Text statusText;
        private Text soundLabel;
        private AudioSource uiAudio;
        private AudioSource ambient;
        private bool loading;

        private void Start()
        {
            ClashUi.EnsureEventSystem();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = ProgressStore.SoundEnabled ? 1f : 0f;

            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;

            ambient = gameObject.AddComponent<AudioSource>();
            ambient.playOnAwake = false;
            ambient.spatialBlend = 0f;
            ambient.loop = true;
            ambient.volume = 0.2f;
            ambient.clip = ProceduralAudio.GetAmbientLoop();
            ambient.Play();

            BuildUi();
        }

        public void SetArenaScene(string sceneName)
        {
            arenaSceneName = sceneName;
        }

        private void BuildUi()
        {
            Canvas canvas = ClashUi.CreateCanvas("MainMenuUI", 10, transform);
            Transform root = canvas.transform;

            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 bottomLeft = new Vector2(0f, 0f);
            Vector2 bottomRight = new Vector2(1f, 0f);
            Vector2 bottomMid = new Vector2(0.5f, 0f);

            // Title block
            title = ClashUi.Rect("Title", root, top, top, top, new Vector2(0f, -70f), new Vector2(1500f, 200f));
            ClashUi.Label(title, "CLASHDASH", 176, TextAnchor.MiddleCenter, Color.white, true);
            ClashUi.Label(ClashUi.Rect("Subtitle", root, top, top, top, new Vector2(0f, -270f), new Vector2(1200f, 60f)),
                          "ZETRA   -   SEASON ZERO", 42, TextAnchor.MiddleCenter, ClashUi.Cyan, true);

            // Primary action
            bool sceneAvailable = Application.CanStreamedLevelBeLoaded(arenaSceneName);
            enterButton = ClashUi.MakeButton(root, "EnterArena", "ENTER ARENA 01 - THE TEST", mid, new Vector2(0f, 10f),
                                             new Vector2(1000f, 170f), new Color(0.4f, 0.95f, 1f, 1f), EnterArena, 62);
            enterButtonRt = enterButton.GetComponent<RectTransform>();
            enterLabel = enterButton.GetComponentInChildren<Text>();
            if (!sceneAvailable)
            {
                enterButton.interactable = false;
                enterLabel.text = "ARENA SCENE NOT IN BUILD SETTINGS";
                enterLabel.fontSize = 44;
            }

            // Best time / status line
            statusText = ClashUi.Label(ClashUi.Rect("Status", root, mid, mid, mid, new Vector2(0f, -110f), new Vector2(1000f, 60f)),
                                       BuildStatusLine(), 38, TextAnchor.MiddleCenter, ClashUi.Gold, true);

            // Future arenas
            BuildArenaCard(root, mid, new Vector2(-260f, -260f), 2);
            BuildArenaCard(root, mid, new Vector2(260f, -260f), 3);

            // Profile card (bottom-left)
            RectTransform profile = ClashUi.Rect("Profile", root, bottomLeft, bottomLeft, bottomLeft, new Vector2(40f, 40f), new Vector2(620f, 190f));
            ClashUi.Panel(profile, new Color(0.08f, 0.14f, 0.28f, 0.85f), true);

            int xp = ProgressStore.TotalXp;
            int level = ProgressStore.LevelFromXp(xp);
            Vector2 tl = new Vector2(0f, 1f);
            ClashUi.Label(ClashUi.Rect("Level", profile, tl, tl, tl, new Vector2(30f, -18f), new Vector2(560f, 70f)),
                          "LEVEL " + level, 56, TextAnchor.MiddleLeft, Color.white, false);

            RectTransform barBg = ClashUi.Rect("XpBar", profile, tl, tl, tl, new Vector2(30f, -100f), new Vector2(560f, 20f));
            ClashUi.Panel(barBg, new Color(0f, 0f, 0f, 0.5f), true);
            RectTransform fill = ClashUi.Rect("Fill", barBg, Vector2.zero, new Vector2(Mathf.Max(0.02f, ProgressStore.LevelProgress(xp)), 1f),
                                              new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            ClashUi.Panel(fill, ClashUi.Gold, true);

            ClashUi.Label(ClashUi.Rect("Stats", profile, tl, tl, tl, new Vector2(30f, -128f), new Vector2(560f, 50f)),
                          "XP " + xp + " / " + ProgressStore.XpForLevel(level + 1) + "    COINS " + ProgressStore.TotalCoins + "    RUNS " + ProgressStore.RunsCompleted,
                          30, TextAnchor.MiddleLeft, ClashUi.Cyan, false);

            // Sound toggle (bottom-right)
            Button sound = ClashUi.MakeButton(root, "Sound", "SOUND: ON", bottomRight, new Vector2(-40f, 40f), new Vector2(360f, 100f),
                                              new Color(0.75f, 0.8f, 1f, 1f), ToggleSound, 40);
            sound.GetComponent<RectTransform>().pivot = bottomRight;
            soundLabel = sound.GetComponentInChildren<Text>();
            RefreshSoundLabel();

            // Signature
            ClashUi.Label(ClashUi.Rect("Signature", root, bottomMid, bottomMid, bottomMid, new Vector2(0f, 24f), new Vector2(600f, 44f)),
                          "CONNECT   -   v" + Application.version, 30, TextAnchor.MiddleCenter, new Color(0.6f, 0.8f, 1f, 0.8f), false);
        }

        private void BuildArenaCard(Transform root, Vector2 anchor, Vector2 position, int arenaNumber)
        {
            bool unlocked = ProgressStore.IsArenaUnlocked(arenaNumber);
            RectTransform card = ClashUi.Rect("Arena" + arenaNumber, root, anchor, anchor, anchor, position, new Vector2(470f, 130f));
            ClashUi.Panel(card, new Color(0.08f, 0.14f, 0.28f, unlocked ? 0.85f : 0.6f), true);

            string headline = "ARENA " + arenaNumber.ToString("00");
            string detail = unlocked ? "COMING SOON" : "COMPLETE ARENA " + (arenaNumber - 1).ToString("00") + " TO UNLOCK";

            ClashUi.Label(ClashUi.Rect("Head", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(440f, 56f)),
                          headline, 44, TextAnchor.MiddleCenter, unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f), false);
            ClashUi.Label(ClashUi.Rect("Detail", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(440f, 44f)),
                          detail, 26, TextAnchor.MiddleCenter, ClashUi.Cyan, false);
        }

        private string BuildStatusLine()
        {
            float best = ProgressStore.BestTime(ProgressStore.Arena01);
            return best > 0f ? "BEST TIME   " + ProgressStore.FormatTime(best) : "NOT COMPLETED YET";
        }

        private void Update()
        {
            float t = Time.unscaledTime;

            if (enterButtonRt != null && enterButton != null && enterButton.interactable && !loading)
            {
                float s = 1f + Mathf.Sin(t * 2.4f) * 0.02f;
                enterButtonRt.localScale = new Vector3(s, s, 1f);
            }

            if (title != null)
            {
                title.localScale = Vector3.one * (1f + Mathf.Sin(t * 1.2f) * 0.012f);
            }
        }

        private void EnterArena()
        {
            if (loading) return;
            loading = true;
            Click();

            if (enterLabel != null) enterLabel.text = "LOADING...";
            if (enterButton != null) enterButton.interactable = false;
            SceneManager.LoadSceneAsync(arenaSceneName);
        }

        private void ToggleSound()
        {
            ProgressStore.SoundEnabled = !ProgressStore.SoundEnabled;
            RefreshSoundLabel();
            Click();
        }

        private void RefreshSoundLabel()
        {
            if (soundLabel != null) soundLabel.text = ProgressStore.SoundEnabled ? "SOUND: ON" : "SOUND: OFF";
        }

        private void Click()
        {
            if (uiAudio != null) uiAudio.PlayOneShot(ProceduralAudio.Get(Sfx.Click), 0.8f);
        }
    }
}
