using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Menu: ZETRA > CLASHDASH > Validate Project
    /// Opens every arena scene and the main menu (one after another), checks that everything a run needs is
    /// present and consistent, then reopens the scene you started from. Read-only: nothing is modified or saved.
    /// </summary>
    public static class ProjectValidator
    {
        private const string MenuScene = "ClashDash_MainMenu";

        [MenuItem("ZETRA/CLASHDASH/Validate Project")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("CLASHDASH", "Stop Play mode before validating.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            StringBuilder report = new StringBuilder();
            int problems = 0;

            // ---- Project-level ----
            report.AppendLine("PROJECT");
            problems += Check(report, Resources.Load<Shader>("Shaders/CD_EnergyField") != null, "Energy field shader is in Resources/Shaders");
            problems += Check(report, Shader.Find("CLASHDASH/EnergyField") != null, "Energy field shader compiled");
            problems += Check(report, Shader.Find("CLASHDASH/BoostChevrons") != null, "Boost chevron shader compiled");

            // ---- Arena scenes ----
            for (int i = 0; i < ArenaCatalog.All.Length; i++)
            {
                ArenaInfo info = ArenaCatalog.All[i];
                string path = ScenePath(info.SceneName);
                report.AppendLine();
                report.AppendLine(info.Title);

                bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                if (!exists)
                {
                    report.AppendLine("  --    scene not built yet (" + path + ")");
                    continue;
                }

                problems += Check(report, InBuildSettings(path), "Scene is enabled in Build Settings");
                problems += ValidateArena(path, info, report);
            }

            // ---- Main menu ----
            report.AppendLine();
            report.AppendLine("MAIN MENU");
            string menuPath = ScenePath(MenuScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(menuPath) == null)
            {
                report.AppendLine("  --    scene not built yet (" + menuPath + ")");
            }
            else
            {
                problems += Check(report, InBuildSettings(menuPath), "Scene is enabled in Build Settings");
                EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
                problems += Check(report, FindAll<MainMenuController>().Length == 1, "Exactly one MainMenuController");
                problems += Check(report, FindAll<Camera>().Length >= 1, "Has a camera");
            }

            // Build order
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool menuFirst = scenes.Length > 0 && scenes[0].path == menuPath;
            report.AppendLine();
            report.AppendLine("BUILD SETTINGS");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(menuPath) != null)
            {
                problems += Check(report, menuFirst, "Main menu is the first scene");
            }

            // Restore the scene the user had open.
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            string text = report.ToString();
            Debug.Log("[CLASHDASH] Validation report:\n" + text);
            EditorUtility.DisplayDialog("CLASHDASH - validation",
                problems == 0 ? "All checks passed. See the Console for the full report."
                              : problems + " problem(s) found. See the Console for the full report.",
                "OK");
        }

        // ------------------------------------------------------------------ per-arena checks

        private static int ValidateArena(string path, ArenaInfo info, StringBuilder report)
        {
            int problems = 0;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            GameManager[] managers = FindAll<GameManager>();
            problems += Check(report, managers.Length == 1, "Exactly one GameManager");

            if (managers.Length == 1)
            {
                SerializedObject so = new SerializedObject(managers[0]);
                SerializedProperty id = so.FindProperty("arenaId");
                problems += Check(report, id != null && id.stringValue == info.Id, "GameManager arenaId is " + info.Id);
            }

            PlayerController[] players = FindAll<PlayerController>();
            problems += Check(report, players.Length == 1, "Exactly one PlayerController");
            if (players.Length == 1)
            {
                problems += Check(report, players[0].CompareTag("Player"), "Player is tagged 'Player'");
                problems += Check(report, players[0].GetComponent<CharacterController>() != null, "Player has a CharacterController");
            }

            Checkpoint[] checkpoints = FindAll<Checkpoint>();
            problems += Check(report, checkpoints.Length >= 3, "At least 3 checkpoints (" + checkpoints.Length + ")");

            List<Checkpoint> ordered = new List<Checkpoint>(checkpoints);
            ordered.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            bool increasing = true;
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Index <= ordered[i - 1].Index) increasing = false;
            }
            problems += Check(report, increasing, "Checkpoint indexes increase along the course");

            FinishGate[] gates = FindAll<FinishGate>();
            problems += Check(report, gates.Length == 1, "Exactly one FinishGate");
            if (gates.Length == 1 && ordered.Count > 0)
            {
                problems += Check(report, gates[0].transform.position.z > ordered[ordered.Count - 1].transform.position.z,
                                  "Finish gate is beyond the last checkpoint");
            }

            problems += Check(report, FindAll<EventSystem>().Length == 1, "Exactly one EventSystem");

            Camera main = null;
            Camera[] cameras = FindAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].CompareTag("MainCamera")) main = cameras[i];
            }
            problems += Check(report, main != null && main.GetComponent<ThirdPersonCamera>() != null, "MainCamera has ThirdPersonCamera");

            Hazard[] hazards = FindAll<Hazard>();
            int brokenHazards = 0;
            for (int i = 0; i < hazards.Length; i++)
            {
                Collider[] colliders = hazards[i].GetComponents<Collider>();
                bool hasTrigger = false;
                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c].isTrigger) hasTrigger = true;
                }
                if (!hasTrigger) brokenHazards++;
            }
            problems += Check(report, brokenHazards == 0, hazards.Length + " hazards, all have a trigger collider");

            int missing = 0;
            GameObject[] all = ToObjects(FindAll<Transform>());
            for (int i = 0; i < all.Length; i++)
            {
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(all[i]);
            }
            problems += Check(report, missing == 0, "No missing scripts (" + missing + ")");

            report.AppendLine("  info  " + FindAll<CoinPickup>().Length + " coins, " + FindAll<FallingTile>().Length + " falling tiles, "
                              + FindAll<BoostPad>().Length + " boost pads, " + FindAll<PulseGate>().Length + " laser gates, "
                              + FindAll<Pendulum>().Length + " pendulums");
            return problems;
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject[] ToObjects(Transform[] transforms)
        {
            GameObject[] result = new GameObject[transforms.Length];
            for (int i = 0; i < transforms.Length; i++) result[i] = transforms[i].gameObject;
            return result;
        }

        private static T[] FindAll<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(true);
#endif
        }

        private static string ScenePath(string sceneName)
        {
            return "Assets/Scenes/" + sceneName + ".unity";
        }

        private static bool InBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == path && scenes[i].enabled) return true;
            }
            return false;
        }

        private static int Check(StringBuilder report, bool ok, string label)
        {
            report.AppendLine((ok ? "  OK    " : "  FIX   ") + label);
            return ok ? 0 : 1;
        }
    }
}
