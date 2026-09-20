using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Headless build used by GitHub Actions (GameCI): generates every CLASHDASH scene, applies Android
    /// settings and builds an APK. Entry point: Zetra.ClashDash.EditorTools.ClashDashCi.BuildAndroid
    /// </summary>
    public static class ClashDashCi
    {
        public static void BuildAndroid()
        {
            try
            {
                Debug.Log("[CLASHDASH CI] Building scenes...");
                ArenaOneBuilder.BuildArenaOne();
                Arena02Builder.Build();
                Arena03Builder.Build();
                MainMenuSceneBuilder.Build();

                ApplyAndroidSettings();

                List<string> scenes = new List<string>();
                string[] wanted =
                {
                    "Assets/Scenes/ClashDash_MainMenu.unity",
                    "Assets/Scenes/Arena01_TheTest.unity",
                    "Assets/Scenes/Arena02_TheCrucible.unity",
                    "Assets/Scenes/Arena03_TheTribunal.unity"
                };
                for (int i = 0; i < wanted.Length; i++)
                {
                    if (File.Exists(wanted[i])) scenes.Add(wanted[i]);
                    else Debug.LogWarning("[CLASHDASH CI] Missing scene: " + wanted[i]);
                }

                if (scenes.Count == 0)
                {
                    Debug.LogError("[CLASHDASH CI] No scenes were generated - see the errors above.");
                    EditorApplication.Exit(1);
                    return;
                }

                // Make sure Build Settings match what we are about to build.
                List<EditorBuildSettingsScene> settingsScenes = new List<EditorBuildSettingsScene>();
                for (int i = 0; i < scenes.Count; i++) settingsScenes.Add(new EditorBuildSettingsScene(scenes[i], true));
                EditorBuildSettings.scenes = settingsScenes.ToArray();

                string output = ResolveOutputPath();
                Debug.Log("[CLASHDASH CI] Building APK: " + output + " (" + scenes.Count + " scenes)");

                EditorUserBuildSettings.buildAppBundle = false;

                BuildPlayerOptions options = new BuildPlayerOptions();
                options.scenes = scenes.ToArray();
                options.locationPathName = output;
                options.target = BuildTarget.Android;
                options.options = BuildOptions.None;

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError("[CLASHDASH CI] Build " + report.summary.result + " with " + report.summary.totalErrors + " error(s).");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log("[CLASHDASH CI] APK built OK: " + output + " (" + (report.summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void PrepareCloudBuild()
        {
            Debug.Log("[CLASHDASH CI] Preparing scenes for cloud build...");
            ArenaOneBuilder.BuildArenaOne();
            Arena02Builder.Build();
            Arena03Builder.Build();
            MainMenuSceneBuilder.Build();

            ApplyAndroidSettings();

            List<string> scenes = new List<string>();
            string[] wanted =
            {
                "Assets/Scenes/ClashDash_MainMenu.unity",
                "Assets/Scenes/Arena01_TheTest.unity",
                "Assets/Scenes/Arena02_TheCrucible.unity",
                "Assets/Scenes/Arena03_TheTribunal.unity"
            };
            for (int i = 0; i < wanted.Length; i++)
            {
                if (File.Exists(wanted[i])) scenes.Add(wanted[i]);
                else Debug.LogWarning("[CLASHDASH CI] Missing scene: " + wanted[i]);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("[CLASHDASH CI] No scenes were generated - see the errors above.");
                return;
            }

            List<EditorBuildSettingsScene> settingsScenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < scenes.Count; i++) settingsScenes.Add(new EditorBuildSettingsScene(scenes[i], true));
            EditorBuildSettings.scenes = settingsScenes.ToArray();

            Debug.Log("[CLASHDASH CI] Scenes ready: " + scenes.Count);
        }

        private static string ResolveOutputPath()
        {
            string path = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-customBuildPath") path = args[i + 1];
            }

            if (string.IsNullOrEmpty(path)) path = "build/Android/CLASHDASH";
            if (!path.EndsWith(".apk")) path += ".apk";

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return path;
        }

        private static void ApplyAndroidSettings()
        {
            PlayerSettings.companyName = "ZETRA";
            PlayerSettings.productName = "CLASHDASH";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.zetra.clashdash");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.zetra.clashdash");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
#endif

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });

            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.antiAliasing = 2;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
        }
    }
}
