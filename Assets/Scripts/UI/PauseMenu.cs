using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Pause button (top-right), pause panel with resume / restart / menu / sound, Android back-button support,
    /// auto-pause when the app is backgrounded, and a MENU button after finishing. Built entirely in code and
    /// attached automatically by ClashDashBootstrap.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private GameManager gm;
        private GameObject panel;
        private Button pauseButton;
        private Button finishMenuButton;
        private Button menuButton;
        private Text soundLabel;
        private Text profileLabel;
        private RectTransform xpFill;
        private UnityAction onFinished;
        private bool paused;

        public void Bind(GameManager manager)
        {
            gm = manager;
            ClashUi.EnsureEventSystem();

            Canvas canvas = ClashUi.CreateCanvas("PauseUI", 20, transform);
            Transform root = canvas.transform;
            bool canGoToMenu = Application.CanStreamedLevelBeLoaded(MainMenuController.MenuSceneName);

            // Pause button (top-right)
            pauseButton = ClashUi.MakeButton(root, "PauseButton", "II", new Vector2(1f, 1f), new Vector2(-40f, -30f),
                                             new Vector2(120f, 120f), new Color(0.4f, 0.9f, 1f, 0.55f), () => SetPaused(true), 56);

            // Menu button that appears next to the results panel's RESTART button
            finishMenuButton = ClashUi.MakeButton(root, "FinishMenuButton", "MENU", new Vector2(0.5f, 0f), new Vector2(520f, 90f),
                                                  new Vector2(380f, 120f), new Color(1f, 0.86f, 0.5f, 0.95f), GoToMenu, 52);
            finishMenuButton.gameObject.SetActive(false);

            // Pause panel
            RectTransform panelRt = ClashUi.Stretch("PausePanel", root);
            ClashUi.Panel(panelRt, new Color(0f, 0.02f, 0.06f, 0.8f), false).raycastTarget = true;
            panel = panelRt.gameObject;

            RectTransform card = ClashUi.Rect("Card", panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                              new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 900f));
            ClashUi.Panel(card, new Color(0.08f, 0.14f, 0.28f, 0.96f), true);

            Vector2 mid = new Vector2(0.5f, 0.5f);
            ClashUi.Label(ClashUi.Rect("Title", card, mid, mid, mid, new Vector2(0f, 360f), new Vector2(800f, 120f)),
                          "PAUSED", 92, TextAnchor.MiddleCenter, Color.white, true);

            profileLabel = ClashUi.Label(ClashUi.Rect("Profile", card, mid, mid, mid, new Vector2(0f, 270f), new Vector2(800f, 60f)),
                                         string.Empty, 38, TextAnchor.MiddleCenter, ClashUi.Cyan, false);

            RectTransform barBg = ClashUi.Rect("XpBar", card, mid, mid, mid, new Vector2(0f, 210f), new Vector2(640f, 24f));
            ClashUi.Panel(barBg, new Color(0f, 0f, 0f, 0.5f), true);
            xpFill = ClashUi.Rect("Fill", barBg, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            ClashUi.Panel(xpFill, ClashUi.Gold, true);

            ClashUi.MakeButton(card.transform, "Resume", "RESUME", mid, new Vector2(0f, 90f), new Vector2(620f, 110f),
                               new Color(0.4f, 0.95f, 0.8f, 1f), () => SetPaused(false), 54);
            ClashUi.MakeButton(card.transform, "Restart", "RESTART", mid, new Vector2(0f, -40f), new Vector2(620f, 110f),
                               new Color(0.45f, 0.9f, 1f, 1f), Restart, 54);

            menuButton = ClashUi.MakeButton(card.transform, "Menu", "MAIN MENU", mid, new Vector2(0f, -170f), new Vector2(620f, 110f),
                                            new Color(1f, 0.86f, 0.5f, 1f), GoToMenu, 54);
            menuButton.gameObject.SetActive(canGoToMenu);

            Button sound = ClashUi.MakeButton(card.transform, "Sound", "SOUND: ON", mid, new Vector2(0f, -300f), new Vector2(420f, 90f),
                                              new Color(0.75f, 0.8f, 1f, 1f), ToggleSound, 42);
            soundLabel = sound.GetComponentInChildren<Text>();
            RefreshSoundLabel();

            panel.SetActive(false);

            if (gm != null)
            {
                onFinished = OnFinished;
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void Update()
        {
            if (gm == null || gm.State == GameState.Finished) return;
            if (BackPressed()) SetPaused(!paused);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && gm != null && gm.State != GameState.Finished && panel != null) SetPaused(true);
        }

        private void SetPaused(bool value)
        {
            if (panel == null) return;
            if (value && gm != null && gm.State == GameState.Finished) return;

            paused = value;
            panel.SetActive(value);
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;

            if (value) RefreshProfile();
        }

        private void RefreshProfile()
        {
            int xp = ProgressStore.TotalXp;
            if (profileLabel != null)
            {
                profileLabel.text = "LEVEL " + ProgressStore.LevelFromXp(xp) + "     COINS " + ProgressStore.TotalCoins;
            }
            if (xpFill != null)
            {
                xpFill.anchorMax = new Vector2(Mathf.Max(0.02f, ProgressStore.LevelProgress(xp)), 1f);
            }
        }

        private void RefreshSoundLabel()
        {
            if (soundLabel != null) soundLabel.text = ProgressStore.SoundEnabled ? "SOUND: ON" : "SOUND: OFF";
        }

        private void ToggleSound()
        {
            ProgressStore.SoundEnabled = !ProgressStore.SoundEnabled;
            RefreshSoundLabel();
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (gm != null) gm.Restart();
        }

        private void GoToMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(MainMenuController.MenuSceneName);
        }

        private void OnFinished()
        {
            if (pauseButton != null) pauseButton.gameObject.SetActive(false);
            if (finishMenuButton != null)
            {
                finishMenuButton.gameObject.SetActive(Application.CanStreamedLevelBeLoaded(MainMenuController.MenuSceneName));
            }
        }

        private static bool BackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (gm != null && onFinished != null) gm.onFinished.RemoveListener(onFinished);
        }
    }
}
