using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>Adds MenuArenaCards to the main menu automatically whenever it loads.</summary>
    public static class MenuArenaCardsInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MainMenuController menu = ClashDashBootstrap.FindOne<MainMenuController>();
            if (menu == null || menu.GetComponent<MenuArenaCards>() != null) return;
            menu.gameObject.AddComponent<MenuArenaCards>();
        }
    }

    /// <summary>
    /// Replaces the menu's "coming soon" placeholders with real arena cards driven by ArenaCatalog
    /// (locked / unlocked, best time, load on tap) and adds a HAPTICS toggle.
    /// </summary>
    public class MenuArenaCards : MonoBehaviour
    {
        private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        private Text hapticsLabel;
        private bool loading;

        private IEnumerator Start()
        {
            // Wait for MainMenuController to build its UI.
            yield return null;
            yield return null;
            Build();
        }

        private void Build()
        {
            GameObject canvasGo = GameObject.Find("MainMenuUI");
            if (canvasGo == null) return;

            Transform root = canvasGo.transform;

            // Hide the static placeholders created by MainMenuController.
            for (int n = 2; n <= 9; n++)
            {
                Transform placeholder = root.Find("Arena" + n);
                if (placeholder != null) placeholder.gameObject.SetActive(false);
            }

            for (int i = 0; i < ArenaCatalog.All.Length; i++)
            {
                ArenaInfo info = ArenaCatalog.All[i];
                if (info.Number < 2) continue;
                BuildCard(root, info);
            }

            Button haptics = ClashUi.MakeButton(root, "Haptics", string.Empty, new Vector2(1f, 0f), new Vector2(-40f, 160f),
                                                new Vector2(360f, 100f), new Color(0.75f, 0.8f, 1f, 1f), ToggleHaptics, 40);
            hapticsLabel = haptics.GetComponentInChildren<Text>();
            RefreshHapticsLabel();
        }

        private void BuildCard(Transform root, ArenaInfo info)
        {
            int slot = info.Number - 2;
            Vector2 position = new Vector2((slot % 2 == 0) ? -260f : 260f, -260f - (slot / 2) * 150f);

            bool unlocked = ProgressStore.IsArenaUnlocked(info.Number);
            bool inBuild = info.IsInBuild;
            bool playable = unlocked && inBuild;

            Color color = playable ? new Color(0.4f, 0.95f, 1f, 1f) : new Color(0.55f, 0.6f, 0.75f, 0.55f);
            string sceneName = info.SceneName;
            Button card = ClashUi.MakeButton(root, "ArenaCard" + info.Number, string.Empty, Mid, position,
                                             new Vector2(470f, 130f), color, () => Enter(sceneName), 30);
            card.interactable = playable;

            string detail;
            if (!unlocked) detail = "COMPLETE ARENA " + (info.Number - 1).ToString("00") + " TO UNLOCK";
            else if (!inBuild) detail = "NOT IN BUILD SETTINGS";
            else
            {
                float best = ProgressStore.BestTime(info.Id);
                detail = best > 0f ? "BEST  " + ProgressStore.FormatTime(best) : "TAP TO PLAY";
            }

            Text label = card.GetComponentInChildren<Text>();
            label.text = info.Title + "\n" + detail;
            label.fontSize = 30;
            label.color = playable ? ClashUi.Ink : new Color(1f, 1f, 1f, 0.75f);
        }

        private void Enter(string sceneName)
        {
            if (loading) return;
            loading = true;
            SceneManager.LoadSceneAsync(sceneName);
        }

        private void ToggleHaptics()
        {
            HapticsFx.Enabled = !HapticsFx.Enabled;
            RefreshHapticsLabel();
        }

        private void RefreshHapticsLabel()
        {
            if (hapticsLabel != null) hapticsLabel.text = HapticsFx.Enabled ? "HAPTICS: ON" : "HAPTICS: OFF";
        }
    }
}
