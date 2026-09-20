using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Menu: ZETRA > CLASHDASH > Build EVERYTHING
    /// Builds every scene that does not exist yet (ARENA 01, 02, 03 and the main menu), orders Build Settings
    /// (menu first, then arenas in order), applies the Android setup and finishes with a validation report.
    /// Existing scenes and assets are never overwritten.
    /// </summary>
    public static class BuildEverything
    {
        private const string MenuScene = "ClashDash_MainMenu";

        [MenuItem("ZETRA/CLASHDASH/Build EVERYTHING (arenas, menu, Android, validation)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("CLASHDASH", "Stop Play mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!SceneExists("Arena01_TheTest")) ArenaOneBuilder.BuildArenaOne();
            if (!SceneExists(ArenaCatalog.Get(2).SceneName)) Arena02Builder.Build();
            if (!SceneExists(ArenaCatalog.Get(3).SceneName)) Arena03Builder.Build();
            if (!SceneExists(MenuScene)) MainMenuSceneBuilder.Build();

            OrderBuildSettings();
            AndroidSetup.Apply();
            ProjectValidator.Run();
        }

        private static bool SceneExists(string sceneName)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath(sceneName)) != null;
        }

        private static string ScenePath(string sceneName)
        {
            return "Assets/Scenes/" + sceneName + ".unity";
        }

        /// <summary>Menu first, then ARENA 01/02/03; any other scenes keep their place after those.</summary>
        private static void OrderBuildSettings()
        {
            List<string> wanted = new List<string> { MenuScene, "Arena01_TheTest" };
            for (int i = 2; i <= 9; i++)
            {
                ArenaInfo info = ArenaCatalog.Get(i);
                if (info != null) wanted.Add(info.SceneName);
            }

            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
            HashSet<string> added = new HashSet<string>();

            for (int i = 0; i < wanted.Count; i++)
            {
                string path = ScenePath(wanted[i]);
                if (!SceneExists(wanted[i])) continue;
                list.Add(new EditorBuildSettingsScene(path, true));
                added.Add(path);
            }

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (!added.Contains(existing[i].path)) list.Add(existing[i]);
            }

            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
