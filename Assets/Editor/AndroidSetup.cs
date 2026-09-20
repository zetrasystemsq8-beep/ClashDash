using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// Menu: ZETRA > CLASHDASH > Apply Android Setup / Check Android Readiness / Build Android APK (Development)
    /// Applies sensible mobile defaults for CLASHDASH (landscape, IL2CPP, ARM64, GLES3 + Vulkan, 60 FPS-friendly
    /// quality) and reports anything that still blocks an Android build.
    /// </summary>
    public static class AndroidSetup
    {
        private const string CompanyName = "ZETRA";
        private const string ProductName = "CLASHDASH";
        private const string AppId = "com.zetra.clashdash";

        [MenuItem("ZETRA/CLASHDASH/Apply Android Setup")]
        public static void Apply()
        {
            ApplySettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[CLASHDASH] Android setup applied: " + AppId + ", landscape, IL2CPP, ARM64, GLES3 + Vulkan, mobile quality defaults.");
            EditorUtility.DisplayDialog("CLASHDASH", "Android setup applied.\n\nRun 'Check Android Readiness' to verify the rest.", "OK");
        }

        [MenuItem("ZETRA/CLASHDASH/Check Android Readiness")]
        public static void Check()
        {
            StringBuilder report = new StringBuilder();
            int problems = 0;

            problems += Line(report, BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
                             "Android build support module installed", "Install the Android Build Support module (with SDK/NDK/OpenJDK) in Unity Hub");

            problems += Line(report, EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                             "Active platform is Android", "Switch Platform to Android in File > Build Profiles / Build Settings");

            int enabledScenes = 0;
            bool hasMenu = false;
            bool hasArena = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                enabledScenes++;
                if (s.path.Contains("ClashDash_MainMenu")) hasMenu = true;
                if (s.path.Contains("Arena01_TheTest")) hasArena = true;
            }
            problems += Line(report, enabledScenes > 0, "Scenes in Build Settings (" + enabledScenes + ")", "Run the scene builders from the ZETRA > CLASHDASH menu");
            problems += Line(report, hasMenu, "Main menu scene in Build Settings", "ZETRA > CLASHDASH > Build Main Menu Scene");
            problems += Line(report, hasArena, "ARENA 01 scene in Build Settings", "ZETRA > CLASHDASH > Build Arena 01 - THE TEST");

#if UNITY_2021_2_OR_NEWER
            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            bool il2cpp = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP;
#else
            string id = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            bool il2cpp = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP;
#endif
            problems += Line(report, id == AppId, "Package name is " + AppId + " (currently " + id + ")", "Run 'Apply Android Setup'");
            problems += Line(report, il2cpp, "Scripting backend is IL2CPP", "Run 'Apply Android Setup'");
            problems += Line(report, (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0,
                             "ARM64 architecture enabled (required by Google Play)", "Run 'Apply Android Setup'");
            problems += Line(report, PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation
                                     && !PlayerSettings.allowedAutorotateToPortrait
                                     && PlayerSettings.allowedAutorotateToLandscapeLeft
                                     && PlayerSettings.allowedAutorotateToLandscapeRight,
                             "Landscape-only orientation", "Run 'Apply Android Setup'");
            problems += Line(report, QualitySettings.vSyncCount == 0, "VSync off (frame rate is capped to 60 in code)", "Run 'Apply Android Setup'");

            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            report.AppendLine("  info  Render pipeline: " + (rp != null ? rp.GetType().Name : "Built-in"));
            report.AppendLine("  info  Product: " + PlayerSettings.productName + " / Company: " + PlayerSettings.companyName);
            report.AppendLine("  info  Signing: debug keystore is used for development APKs; create a release keystore in Player Settings before publishing.");

            string text = report.ToString();
            Debug.Log("[CLASHDASH] Android readiness report:\n" + text);
            EditorUtility.DisplayDialog("CLASHDASH - Android readiness",
                problems == 0 ? "Everything checks out. See the Console for the full report." : problems + " item(s) need attention. See the Console for the full report.",
                "OK");
        }

        [MenuItem("ZETRA/CLASHDASH/Build Android APK (Development)")]
        public static void BuildApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("CLASHDASH", "Android Build Support is not installed. Add it in Unity Hub (Installs > Add Modules).", "OK");
                return;
            }

            List<string> scenes = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled) scenes.Add(s.path);
            }
            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("CLASHDASH", "No scenes in Build Settings. Run the scene builders first.", "OK");
                return;
            }

            ApplySettings();

            Directory.CreateDirectory("Builds/Android");
            string file = "Builds/Android/CLASHDASH_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm") + ".apk";

            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = scenes.ToArray();
            options.locationPathName = file;
            options.target = BuildTarget.Android;
            options.options = BuildOptions.Development;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[CLASHDASH] APK built: " + file + " (" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB)");
                EditorUtility.RevealInFinder(file);
            }
            else
            {
                Debug.LogError("[CLASHDASH] Android build " + summary.result + " with " + summary.totalErrors + " error(s). See the Console.");
            }
        }

        // ------------------------------------------------------------------ implementation

        private static void ApplySettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AppId);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
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

            PlayerSettings.MTRendering = true;
            PlayerSettings.gpuSkinning = true;

            // Mobile-friendly quality defaults (URP projects also need the same limits on their pipeline asset).
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.antiAliasing = 2;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        }

        private static int Line(StringBuilder report, bool ok, string label, string fix)
        {
            if (ok)
            {
                report.AppendLine("  OK    " + label);
                return 0;
            }
            report.AppendLine("  FIX   " + label + "   ->   " + fix);
            return 1;
        }
    }
}
