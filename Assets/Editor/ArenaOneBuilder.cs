using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Menu: ZETRA > CLASHDASH > Build Arena 01 - THE TEST
    /// Procedurally builds the full playable scene (course, environment, player, camera, HUD, managers).
    /// Never overwrites an existing scene or asset: existing files are reused / a unique scene name is used.
    /// </summary>
    public static class ArenaOneBuilder
    {
        private const string GenRoot = "Assets/ClashDash/Generated";
        private const string ScenePath = "Assets/Scenes/Arena01_TheTest.unity";
        private const float PathWidth = 10f;

        private static Material deck, deckDark, tower, glowCyan, glowGold, glowMagenta, glowViolet, glowWhite;
        private static Material cloudMat, hazardBody, playerBody, trailMat, particleMat;
        private static System.Random rng;
        private static Font uiFont;

        private static readonly Color CyanText = new Color(0.55f, 0.95f, 1f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.5f, 1f);
        private static readonly Color VioletText = new Color(0.78f, 0.68f, 1f, 1f);
        private static readonly Color MagentaText = new Color(1f, 0.5f, 0.78f, 1f);

        // ------------------------------------------------------------------ entry point

        [MenuItem("ZETRA/CLASHDASH/Build Arena 01 - THE TEST")]
        public static void BuildArenaOne()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("CLASHDASH", "Stop Play mode before building the arena.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(GenRoot + "/Materials");
            EnsureFolder(GenRoot + "/Meshes");
            EnsureFolder(GenRoot + "/Textures");
            EnsureFolder("Assets/Scenes");

            rng = new System.Random(20260920);
            uiFont = LoadUiFont();
            InitPalette();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();

            Transform arenaRoot = Group("Arena").transform;
            Transform envRoot = Group("Environment").transform;
            Transform signRoot = Group("Signage").transform;

            List<Checkpoint> checkpoints = new List<Checkpoint>();
            Transform startPoint = BuildCourse(arenaRoot, checkpoints);
            BuildEnvironment(envRoot);
            BuildSigns(signRoot);

            // Static batching for everything that never moves.
            MarkStatic(arenaRoot);
            MarkStatic(envRoot);

            // Player + camera + HUD + manager
            Transform visualRoot;
            PlayerController player = BuildPlayer(startPoint.position, out visualRoot);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1500f;
            camGo.AddComponent<AudioListener>();
            ThirdPersonCamera tpc = camGo.AddComponent<ThirdPersonCamera>();
            camGo.transform.position = startPoint.position + new Vector3(0f, 4.2f, -8.5f);
            tpc.Bind(player.transform, player, 0f);

            MobileJoystick joystick;
            MobileJumpButton jump;
            Text timerText, statsText, bannerText, resultText;
            GameObject resultPanel;
            Button restartButton;
            BuildHud(out joystick, out jump, out timerText, out statsText, out bannerText, out resultPanel, out resultText, out restartButton);

            player.Bind(joystick, jump, camGo.transform, visualRoot);
            UnityEventTools.AddPersistentListener(player.onHit, tpc.ShakeHeavy);
            UnityEventTools.AddPersistentListener(player.onRespawn, tpc.SnapToTarget);

            GameObject gmGo = new GameObject("GameManager");
            GameManager manager = gmGo.AddComponent<GameManager>();
            manager.Bind(player, startPoint, checkpoints.ToArray(), timerText, statsText, bannerText, resultPanel, resultText, 95f);
            UnityEventTools.AddPersistentListener(restartButton.onClick, manager.Restart);

            string path = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = gmGo;
            Debug.Log("[CLASHDASH] ARENA 01 - THE TEST built and saved to " + path + ". Press Play.");
        }

        // ------------------------------------------------------------------ lighting / sky

        private static void BuildLighting()
        {
            GameObject sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.72f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.58f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 1f);
            RenderSettings.fogDensity = 0.0024f;

            string skyPath = GenRoot + "/Materials/CD_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    sky = new Material(skyShader);
                    sky.name = "CD_Sky";
                    if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 1f));
                    if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.85f, 0.9f, 1f));
                    if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.35f);
                    if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.9f);
                    if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.06f);
                    AssetDatabase.CreateAsset(sky, skyPath);
                }
            }
            if (sky != null) RenderSettings.skybox = sky;
        }

        // ------------------------------------------------------------------ the course

        private static Transform BuildCourse(Transform root, List<Checkpoint> checkpoints)
        {
            // Start
            Deck(root, "Deck_Start", 0f, 6f, 14f, 20f, 0f);
            Transform start = Group("StartPoint", root).transform;
            start.position = new Vector3(0f, 0.1f, 2f);

            // Section 1 - SPRINT ALLEY (timing walls)
            Deck(root, "Deck_Sprint", 0f, 37f, PathWidth, 42f, 0f);
            SlideWall(root, "Wall_1", new Vector3(-1.75f, 1.5f, 26f), new Vector3(6.5f, 3f, 0.8f), new Vector3(3.5f, 0f, 0f), 3.2f, 0f);
            SlideWall(root, "Wall_2", new Vector3(-1.75f, 1.5f, 36f), new Vector3(6.5f, 3f, 0.8f), new Vector3(3.5f, 0f, 0f), 3.2f, 0.5f);
            SlideWall(root, "Wall_3", new Vector3(-1.75f, 1.5f, 46f), new Vector3(6.5f, 3f, 0.8f), new Vector3(3.5f, 0f, 0f), 2.8f, 0.25f);
            checkpoints.Add(MakeCheckpoint(root, 1, new Vector3(0f, 0f, 53f), PathWidth));

            // Section 2 - ROTOR FIELD (rotating bars)
            Deck(root, "Deck_Rotor", 0f, 81f, 16f, 46f, 0f);
            SpinBar(root, "Rotor_1", new Vector3(0f, 0.95f, 70f), 14f, 75f, 0.7f);
            SpinBar(root, "Rotor_2", new Vector3(0f, 0.95f, 84f), 14f, -95f, 0.7f);
            SpinBar(root, "Rotor_3", new Vector3(0f, 0.95f, 97f), 15f, 115f, 0.7f);
            checkpoints.Add(MakeCheckpoint(root, 2, new Vector3(0f, 0f, 101f), 16f));

            // Section 3 - GAP RUN (floating platforms)
            Platform(root, "Float_1", new Vector3(0f, 0f, 109f), new Vector2(7f, 6f), Vector3.zero, 0f, 0f);
            Platform(root, "Float_2", new Vector3(3f, 0.5f, 118f), new Vector2(6f, 6f), Vector3.zero, 0f, 0f);
            Platform(root, "Float_3", new Vector3(-6f, 1f, 127f), new Vector2(6f, 6f), new Vector3(6f, 0f, 0f), 4.5f, 0f);
            Platform(root, "Float_4", new Vector3(0f, 1.5f, 136f), new Vector2(5f, 5f), Vector3.zero, 0f, 0f);
            Platform(root, "Float_5", new Vector3(0f, 1f, 145f), new Vector2(6f, 6f), new Vector3(0f, 2.5f, 0f), 3.5f, 0.25f);
            Platform(root, "Float_6", new Vector3(0f, 1f, 156f), new Vector2(10f, 8f), Vector3.zero, 0f, 0f);
            checkpoints.Add(MakeCheckpoint(root, 3, new Vector3(0f, 1f, 155.5f), 10f));

            // Section 4 - CRUSHER HALL (timed crushers)
            Deck(root, "Deck_Crusher", 0f, 184f, PathWidth, 44f, 1f);
            float[] slamZ = { 172f, 184f, 196f };
            float[] slamPhase = { 0f, 0.33f, 0.66f };
            for (int i = 0; i < slamZ.Length; i++)
            {
                SlamBlock(root, "Slam_" + (i + 1), new Vector3(0f, 7.8f, slamZ[i]), new Vector3(10f, 1.6f, 2.6f), 6f, slamPhase[i]);
                Prim(PrimitiveType.Cylinder, "PillarL", root, new Vector3(-5.9f, 4.5f, slamZ[i]), new Vector3(0.9f, 4.5f, 0.9f), deckDark, true);
                Prim(PrimitiveType.Cylinder, "PillarR", root, new Vector3(5.9f, 4.5f, slamZ[i]), new Vector3(0.9f, 4.5f, 0.9f), deckDark, true);
                Prim(PrimitiveType.Cube, "Lintel", root, new Vector3(0f, 9.4f, slamZ[i]), new Vector3(12.8f, 0.6f, 1.4f), deckDark, false);
                Prim(PrimitiveType.Cube, "LintelGlow", root, new Vector3(0f, 9.1f, slamZ[i]), new Vector3(12.4f, 0.12f, 1.5f), glowMagenta, false, false);
            }
            checkpoints.Add(MakeCheckpoint(root, 4, new Vector3(0f, 1f, 203f), PathWidth));

            // Section 5 - PRECISION SPINE (narrow beam, sweepers, stepping pillars)
            Deck(root, "Beam", 0f, 226f, 2.4f, 40f, 1f);
            SpinBar(root, "Sweeper_1", new Vector3(0f, 1.95f, 218f), 8f, 130f, 0.6f);
            SpinBar(root, "Sweeper_2", new Vector3(0f, 1.95f, 236f), 8f, -150f, 0.6f);
            Deck(root, "Deck_SpineEnd", 0f, 245f, 8f, 6f, 1f);
            checkpoints.Add(MakeCheckpoint(root, 5, new Vector3(0f, 1f, 245f), 8f));

            Platform(root, "Step_1", new Vector3(0f, 1.5f, 251f), new Vector2(3f, 3f), Vector3.zero, 0f, 0f);
            Platform(root, "Step_2", new Vector3(2.5f, 2.5f, 257f), new Vector2(3f, 3f), Vector3.zero, 0f, 0f);
            Platform(root, "Step_3", new Vector3(-2.5f, 3.5f, 263f), new Vector2(3f, 3f), Vector3.zero, 0f, 0f);
            Platform(root, "Step_4", new Vector3(-2f, 4.5f, 269f), new Vector2(3f, 3f), new Vector3(4f, 0f, 0f), 3.8f, 0f);
            Platform(root, "Step_5", new Vector3(0f, 5.5f, 276f), new Vector2(4f, 4f), Vector3.zero, 0f, 0f);

            // Finish
            Deck(root, "Deck_Finish", 0f, 288f, 16f, 16f, 5.5f);
            BuildFinishGate(root, new Vector3(0f, 5.5f, 290f));

            return start;
        }

        private static void BuildFinishGate(Transform root, Vector3 basePos)
        {
            GameObject gate = Group("FinishGate", root);
            gate.transform.position = basePos;

            BoxCollider trigger = gate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 5f, 0f);
            trigger.size = new Vector3(16f, 10f, 2f);
            gate.AddComponent<FinishGate>();
            AddTriggerBody(gate);

            Vector3 ringCenter = basePos + new Vector3(0f, 6.5f, 0f);
            Ring(gate.transform, "Gate_Outer", ringCenter, Vector3.zero, 6.5f, 0.35f, glowGold, glowWhite, 20f, 16);
            Ring(gate.transform, "Gate_Inner", ringCenter, Vector3.zero, 5f, 0.2f, glowCyan, glowWhite, -35f, 12);
            Prim(PrimitiveType.Cylinder, "PylonL", gate.transform, new Vector3(-8f, 6f, 0f), new Vector3(0.8f, 6f, 0.8f), deckDark, false);
            Prim(PrimitiveType.Cylinder, "PylonR", gate.transform, new Vector3(8f, 6f, 0f), new Vector3(0.8f, 6f, 0.8f), deckDark, false);
            Prim(PrimitiveType.Sphere, "OrbL", gate.transform, new Vector3(-8f, 12.4f, 0f), Vector3.one * 1.4f, glowGold, false, false);
            Prim(PrimitiveType.Sphere, "OrbR", gate.transform, new Vector3(8f, 12.4f, 0f), Vector3.one * 1.4f, glowGold, false, false);
        }

        // ------------------------------------------------------------------ course pieces

        private static void Deck(Transform parent, string name, float cx, float cz, float width, float length, float topY)
        {
            const float thickness = 1f;
            Transform g = Group(name, parent).transform;
            Prim(PrimitiveType.Cube, "Slab", g, new Vector3(cx, topY - thickness * 0.5f, cz), new Vector3(width, thickness, length), deck, true);
            Prim(PrimitiveType.Cube, "Hull", g, new Vector3(cx, topY - thickness - 0.6f, cz), new Vector3(width * 0.7f, 1.2f, length * 0.8f), deckDark, false);
            Prim(PrimitiveType.Cube, "Underglow", g, new Vector3(cx, topY - thickness - 0.02f, cz), new Vector3(width * 0.9f, 0.05f, length * 0.95f), glowCyan, false, false);

            float ex = width * 0.5f - 0.2f;
            Prim(PrimitiveType.Cube, "EdgeL", g, new Vector3(cx - ex, topY + 0.03f, cz), new Vector3(0.28f, 0.06f, length - 0.3f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "EdgeR", g, new Vector3(cx + ex, topY + 0.03f, cz), new Vector3(0.28f, 0.06f, length - 0.3f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "Lane", g, new Vector3(cx, topY + 0.02f, cz), new Vector3(0.14f, 0.04f, length * 0.92f), glowViolet, false, false);
        }

        private static GameObject Platform(Transform parent, string name, Vector3 topCenter, Vector2 size, Vector3 travel, float period, float phase)
        {
            GameObject root = Group(name, parent);
            root.transform.position = new Vector3(topCenter.x, topCenter.y - 0.5f, topCenter.z);
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Slab", t, Vector3.zero, new Vector3(size.x, 1f, size.y), deck, true);
            Prim(PrimitiveType.Cube, "Hull", t, new Vector3(0f, -1f, 0f), new Vector3(size.x * 0.6f, 1.2f, size.y * 0.6f), deckDark, false);
            Prim(PrimitiveType.Cube, "TrimL", t, new Vector3(-(size.x * 0.5f - 0.12f), 0.53f, 0f), new Vector3(0.24f, 0.06f, size.y - 0.2f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "TrimR", t, new Vector3(size.x * 0.5f - 0.12f, 0.53f, 0f), new Vector3(0.24f, 0.06f, size.y - 0.2f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "TrimF", t, new Vector3(0f, 0.53f, size.y * 0.5f - 0.12f), new Vector3(size.x - 0.2f, 0.06f, 0.24f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "TrimB", t, new Vector3(0f, 0.53f, -(size.y * 0.5f - 0.12f)), new Vector3(size.x - 0.2f, 0.06f, 0.24f), glowCyan, false, false);

            if (travel != Vector3.zero)
            {
                root.AddComponent<ArenaMover>().ConfigurePingPong(travel, period, phase);
            }
            return root;
        }

        private static GameObject SpinBar(Transform parent, string name, Vector3 center, float length, float dps, float thickness)
        {
            GameObject root = Group(name, parent);
            root.transform.position = center;
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Body", t, Vector3.zero, new Vector3(length, thickness, thickness), hazardBody, true);
            Prim(PrimitiveType.Cube, "Core", t, Vector3.zero, new Vector3(length * 0.98f, thickness * 0.35f, thickness * 1.06f), glowMagenta, false, false);
            Prim(PrimitiveType.Sphere, "CapA", t, new Vector3(length * 0.5f, 0f, 0f), Vector3.one * thickness * 1.5f, glowMagenta, false, false);
            Prim(PrimitiveType.Sphere, "CapB", t, new Vector3(-length * 0.5f, 0f, 0f), Vector3.one * thickness * 1.5f, glowMagenta, false, false);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(length + 0.4f, thickness + 0.5f, thickness + 0.5f);

            root.AddComponent<ArenaMover>().ConfigureRotate(Vector3.up, dps);
            root.AddComponent<Hazard>().Configure(HazardMode.Penalty, 2f, 12f, 6f, false, "SPINNER HIT");

            // Emitter disc on the floor under the bar's pivot.
            Prim(PrimitiveType.Cylinder, "Emitter", parent, new Vector3(center.x, center.y - thickness * 0.5f - 0.58f, center.z), new Vector3(1.6f, 0.03f, 1.6f), glowMagenta, false, false);
            return root;
        }

        private static GameObject SlideWall(Transform parent, string name, Vector3 center, Vector3 size, Vector3 travel, float period, float phase)
        {
            GameObject root = Group(name, parent);
            root.transform.position = center;
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Panel", t, Vector3.zero, size, hazardBody, true);
            Prim(PrimitiveType.Cube, "Energy", t, Vector3.zero, new Vector3(size.x * 0.9f, size.y * 0.8f, size.z * 1.12f), glowMagenta, false, false);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size + new Vector3(0.3f, 0.3f, 0.5f);

            root.AddComponent<ArenaMover>().ConfigurePingPong(travel, period, phase);
            root.AddComponent<Hazard>().Configure(HazardMode.Penalty, 2f, 11f, 5f, true, "WALL HIT");
            return root;
        }

        private static GameObject SlamBlock(Transform parent, string name, Vector3 center, Vector3 size, float drop, float phase)
        {
            GameObject root = Group(name, parent);
            root.transform.position = center;
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Block", t, Vector3.zero, size, hazardBody, true);
            Prim(PrimitiveType.Cube, "Underside", t, new Vector3(0f, -size.y * 0.5f - 0.01f, 0f), new Vector3(size.x * 0.92f, 0.08f, size.z * 0.9f), glowMagenta, false, false);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size + new Vector3(0.3f, 0.4f, 0.5f);

            root.AddComponent<ArenaMover>().ConfigureSlam(new Vector3(0f, -drop, 0f), 1.2f, 0.3f, 0.5f, 1.3f, phase);
            root.AddComponent<Hazard>().Configure(HazardMode.Penalty, 2.5f, 12f, 6f, true, "CRUSHED");
            return root;
        }

        private static Checkpoint MakeCheckpoint(Transform parent, int index, Vector3 pos, float width)
        {
            GameObject root = Group("Checkpoint_" + index.ToString("00"), parent);
            root.transform.position = pos;
            Transform t = root.transform;

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 4f, 0f);
            trigger.size = new Vector3(width, 8f, 2.5f);

            float padSize = Mathf.Min(width, 5f);
            GameObject pad = Prim(PrimitiveType.Cylinder, "Pad", t, new Vector3(0f, 0.04f, 0f), new Vector3(padSize, 0.04f, padSize), glowCyan, false, false);
            GameObject beam = Prim(PrimitiveType.Cylinder, "Beam", t, new Vector3(0f, 4f, 0f), new Vector3(0.5f, 4f, 0.5f), glowCyan, false, false);

            float half = width * 0.5f + 0.3f;
            Prim(PrimitiveType.Cylinder, "PostL", t, new Vector3(-half, 3.1f, 0f), new Vector3(0.45f, 3.1f, 0.45f), deckDark, false);
            Prim(PrimitiveType.Cylinder, "PostR", t, new Vector3(half, 3.1f, 0f), new Vector3(0.45f, 3.1f, 0.45f), deckDark, false);
            GameObject bar = Prim(PrimitiveType.Cube, "Crossbar", t, new Vector3(0f, 6.2f, 0f), new Vector3(half * 2f, 0.35f, 0.35f), glowCyan, false, false);

            Transform respawn = Group("Respawn", t).transform;
            respawn.localPosition = new Vector3(0f, 0.15f, 0f);

            AddTriggerBody(root);
            Checkpoint cp = root.AddComponent<Checkpoint>();
            Renderer[] indicators = { pad.GetComponent<Renderer>(), beam.GetComponent<Renderer>(), bar.GetComponent<Renderer>() };
            cp.Bind(index, respawn, indicators, pad.transform);

            Sign(t, "Label", "CHECKPOINT " + index.ToString("00"), new Vector3(0f, 8f, 0f), Vector3.zero, 9f, CyanText, 90, true);
            return cp;
        }

        // ------------------------------------------------------------------ signage / easter eggs

        private static void BuildSigns(Transform root)
        {
            // Start
            Sign(root, "Sign_ZETRA", "ZETRA", new Vector3(0f, 22f, 44f), Vector3.zero, 22f, GoldText, 130, true);
            Sign(root, "Sign_Arena", "ARENA 01 - THE TEST", new Vector3(0f, 11f, 34f), Vector3.zero, 16f, CyanText, 90, true);
            Sign(root, "Sign_Season", "SEASON ZERO", new Vector3(0f, 7.6f, 34f), Vector3.zero, 8f, VioletText, 70, true);
            Sign(root, "Decal_CONNECT", "CONNECT", new Vector3(0f, 0.05f, 9f), new Vector3(90f, 0f, 0f), 8f, CyanText, 120, false);

            // Section titles
            Sign(root, "Sec_Sprint", "SPRINT ALLEY", new Vector3(0f, 9f, 20f), Vector3.zero, 12f, GoldText, 100, true);
            Sign(root, "Sec_Rotor", "ROTOR FIELD", new Vector3(0f, 9f, 62f), Vector3.zero, 12f, GoldText, 100, true);
            Sign(root, "Sec_Gap", "GAP RUN", new Vector3(0f, 9f, 106f), Vector3.zero, 10f, GoldText, 100, true);
            Sign(root, "Sec_Crusher", "CRUSHER HALL", new Vector3(0f, 13f, 163f), Vector3.zero, 12f, GoldText, 100, true);
            Sign(root, "Sec_Spine", "PRECISION SPINE", new Vector3(0f, 9f, 207f), Vector3.zero, 14f, GoldText, 100, true);

            // World terminals (discoveries)
            Terminal(root, "Terminal_ZetraMail", new Vector3(-6f, 0f, 10f), "ZetraMail\n3 unread", CyanText);
            Terminal(root, "Terminal_NAI", new Vector3(6f, 0f, 10f), "NAI\nONLINE", VioletText);

            // Floating holo-boards along the route
            Sign(root, "Holo_Nigergram", "Nigergram", new Vector3(-15f, 7f, 112f), Vector3.zero, 8f, VioletText, 90, true);
            Sign(root, "Holo_NaijaLearn", "NaijaLearn", new Vector3(15f, 8f, 128f), Vector3.zero, 8f, GoldText, 90, true);
            Sign(root, "Holo_Store", "ZETRA STORE", new Vector3(-16f, 9f, 146f), Vector3.zero, 9f, CyanText, 90, true);
            Sign(root, "Holo_Crucible", "CRUCIBLE", new Vector3(17f, 10f, 185f), Vector3.zero, 8f, MagentaText, 90, true);
            Sign(root, "Holo_Tribunal", "TRIBUNAL", new Vector3(-17f, 10f, 230f), Vector3.zero, 8f, VioletText, 90, true);

            // Finish
            Sign(root, "Sign_Finish", "FINISH", new Vector3(0f, 19.5f, 290f), Vector3.zero, 9f, Color.white, 130, true);
            Sign(root, "Decal_Names", "TOLUWANI / TOFUMI / FOLAKEMI / MARVELLOUS", new Vector3(0f, 5.55f, 282.5f), new Vector3(90f, 0f, 0f), 11f, GoldText, 46, false);
            Sign(root, "Decal_Signature", "CONNECT", new Vector3(0f, 5.55f, 294f), new Vector3(90f, 0f, 0f), 4f, CyanText, 90, false);
        }

        private static void Terminal(Transform parent, string name, Vector3 basePos, string text, Color color)
        {
            Transform t = Group(name, parent).transform;
            t.position = basePos;
            Prim(PrimitiveType.Cube, "Pedestal", t, new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 1.1f, 0.6f), deckDark, true);
            Prim(PrimitiveType.Cube, "Screen", t, new Vector3(0f, 1.25f, -0.05f), new Vector3(1.0f, 0.5f, 0.08f), glowCyan, false, false);
            Sign(t, "Text", text, new Vector3(0f, 1.25f, -0.12f), Vector3.zero, 1.6f, Color.white, 80, false);
        }

        private static GameObject Sign(Transform parent, string name, string text, Vector3 localPos, Vector3 euler,
                                       float worldWidth, Color color, int fontSize, bool bob)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 260f);
            rt.localScale = Vector3.one * (worldWidth / 1000f);
            rt.localPosition = localPos;
            rt.localEulerAngles = euler;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            Text label = textGo.AddComponent<Text>();
            label.font = uiFont;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = color;
            label.raycastTarget = false;

            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            if (bob)
            {
                go.AddComponent<ArenaMover>().ConfigurePingPong(new Vector3(0f, 0.5f, 0f), 5f, (float)rng.NextDouble(), false);
            }
            return go;
        }

        // ------------------------------------------------------------------ environment

        private static void BuildEnvironment(Transform root)
        {
            // Towers flanking the arena
            Transform towers = Group("Towers", root).transform;
            float[] zs = { -40f, 30f, 110f, 190f, 270f, 350f, 430f };
            for (int i = 0; i < zs.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * Rand(100f, 150f);
                    float h = Mathf.Round(Rand(150f, 300f));
                    float w = Mathf.Round(Rand(16f, 30f));
                    Tower(towers, "Tower_" + i + (side < 0 ? "L" : "R"), new Vector3(x, h * 0.5f - 60f, zs[i] + Rand(-15f, 15f)), w, h, true);
                }
            }

            // Far mega-towers behind the finish
            for (int i = 0; i < 5; i++)
            {
                float x = -200f + i * 100f;
                Tower(towers, "MegaTower_" + i, new Vector3(x, 200f - 60f, 560f + Rand(-30f, 30f)), 60f, 400f, false);
            }

            // Sky bridges
            Transform bridges = Group("SkyBridges", root).transform;
            Bridge(bridges, "Bridge_A", new Vector3(0f, 110f, 20f), 330f);
            Bridge(bridges, "Bridge_B", new Vector3(0f, 150f, 150f), 330f);
            Bridge(bridges, "Bridge_C", new Vector3(0f, 90f, 270f), 330f);

            // Floating islands
            Transform islands = Group("Islands", root).transform;
            for (int i = 0; i < 14; i++)
            {
                int side = (i % 2 == 0) ? -1 : 1;
                float x = side * Rand(34f, 75f);
                float z = -20f + i * 30f + Rand(-8f, 8f);
                float y = Rand(-6f, 32f);
                Island(islands, "Island_" + i, new Vector3(x, y, z), Rand(5f, 13f), i);
            }

            // Dimensional gate rings the arena runs through
            Transform rings = Group("DimensionalRings", root).transform;
            float[] gateZ = { 70f, 150f, 230f };
            for (int i = 0; i < gateZ.Length; i++)
            {
                Ring(rings, "GateRing_" + i, new Vector3(0f, 25f, gateZ[i]), Vector3.zero, 70f, 1.6f, glowCyan, glowWhite, (i % 2 == 0) ? 4f : -5f, 24);
            }

            BuildCore(root);
            BuildCloudSea(root);
            BuildParticles(root);
        }

        private static void Tower(Transform parent, string name, Vector3 center, float w, float h, bool halo)
        {
            Transform t = Group(name, parent).transform;
            t.position = center;

            Prim(PrimitiveType.Cylinder, "Shaft", t, Vector3.zero, new Vector3(w, h * 0.5f, w), tower);

            int bands = Mathf.Clamp(Mathf.RoundToInt(h / 30f), 3, 14);
            for (int b = 0; b < bands; b++)
            {
                float y = -h * 0.5f + (b + 1f) * h / (bands + 1f);
                Prim(PrimitiveType.Cylinder, "Band", t, new Vector3(0f, y, 0f), new Vector3(w * 1.1f, 0.5f, w * 1.1f), (b % 3 == 0) ? glowViolet : glowCyan, false, false);
            }

            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Prim(PrimitiveType.Cube, "Strip", t, new Vector3(Mathf.Cos(a) * w * 0.5f, 0f, Mathf.Sin(a) * w * 0.5f), new Vector3(0.6f, h * 0.9f, 0.6f), glowCyan, false, false);
            }

            Prim(PrimitiveType.Sphere, "Crown", t, new Vector3(0f, h * 0.5f + w * 0.2f, 0f), Vector3.one * w * 0.55f, glowGold, false, false);

            if (halo)
            {
                Ring(t, "Halo", t.position + new Vector3(0f, h * 0.32f, 0f), new Vector3(90f, 0f, 0f), w * 0.9f, 0.5f, glowCyan, glowWhite, Rand(-18f, 18f), 10);
            }
        }

        private static void Bridge(Transform parent, string name, Vector3 center, float length)
        {
            Transform t = Group(name, parent).transform;
            t.position = center;
            Prim(PrimitiveType.Cube, "Span", t, Vector3.zero, new Vector3(length, 2.5f, 7f), tower);
            Prim(PrimitiveType.Cube, "GlowL", t, new Vector3(0f, 1.3f, -3.2f), new Vector3(length, 0.15f, 0.25f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "GlowR", t, new Vector3(0f, 1.3f, 3.2f), new Vector3(length, 0.15f, 0.25f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "Under", t, new Vector3(0f, -1.28f, 0f), new Vector3(length * 0.98f, 0.1f, 3f), glowViolet, false, false);
        }

        private static void Island(Transform parent, string name, Vector3 pos, float radius, int seed)
        {
            Transform t = Group(name, parent).transform;
            t.position = pos;

            Prim(PrimitiveType.Cylinder, "Top", t, Vector3.zero, new Vector3(radius * 2f, 0.5f, radius * 2f), deck);
            Prim(PrimitiveType.Cylinder, "Rim", t, new Vector3(0f, 0.35f, 0f), new Vector3(radius * 2.04f, 0.06f, radius * 2.04f), glowCyan, false, false);

            GameObject rock = new GameObject("Rock");
            rock.transform.SetParent(t, false);
            rock.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            rock.transform.localScale = new Vector3(radius, radius * 1.4f, radius);
            rock.AddComponent<MeshFilter>().sharedMesh = RockMesh(seed % 4);
            MeshRenderer mr = rock.AddComponent<MeshRenderer>();
            mr.sharedMaterial = deckDark;

            for (int k = 0; k < 3; k++)
            {
                float px = Rand(-0.5f, 0.5f) * radius;
                float pz = Rand(-0.5f, 0.5f) * radius;
                float s = Rand(0.8f, 1.6f);
                GameObject crystal = Prim(PrimitiveType.Cube, "Crystal", t, new Vector3(px, 0.5f + 1.5f * s, pz), new Vector3(0.6f * s, 3f * s, 0.6f * s), glowViolet, false, false);
                crystal.transform.localRotation = Quaternion.Euler(0f, Rand(0f, 90f), Rand(-12f, 12f));
            }

            t.gameObject.AddComponent<ArenaMover>().ConfigurePingPong(new Vector3(0f, 1.2f, 0f), Rand(6f, 9f), Rand(0f, 1f), false);
        }

        private static void BuildCore(Transform root)
        {
            Transform t = Group("ZetraCore", root).transform;
            t.position = new Vector3(0f, 75f, 345f);
            Prim(PrimitiveType.Sphere, "Orb", t, Vector3.zero, Vector3.one * 20f, glowGold, false, false);

            Ring(t, "CoreRing_A", t.position, new Vector3(0f, 0f, 0f), 34f, 1.2f, glowCyan, glowWhite, 8f, 16);
            Ring(t, "CoreRing_B", t.position, new Vector3(60f, 0f, 0f), 42f, 1.2f, glowViolet, glowWhite, -12f, 20);
            Ring(t, "CoreRing_C", t.position, new Vector3(0f, 60f, 30f), 52f, 1.4f, glowGold, glowWhite, 5f, 24);
        }

        private static void BuildCloudSea(Transform root)
        {
            Transform t = Group("CloudSea", root).transform;
            for (int i = 0; i < 45; i++)
            {
                Vector3 pos = new Vector3(Rand(-260f, 260f), Rand(-95f, -55f), Rand(-120f, 520f));
                Vector3 scale = new Vector3(Rand(40f, 90f), Rand(8f, 16f), Rand(40f, 90f));
                Prim(PrimitiveType.Sphere, "Cloud", t, pos, scale, cloudMat, false, false);
            }
        }

        private static void BuildParticles(Transform root)
        {
            GameObject go = new GameObject("AtmosphereMotes");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 30f, 200f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.9f, 1f, 0.6f), new Color(1f, 0.95f, 0.8f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(240f, 80f, 420f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.8f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = particleMat != null ? particleMat : glowWhite;
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static GameObject Ring(Transform parent, string name, Vector3 worldCenter, Vector3 euler, float radius, float tube,
                                       Material ringMat, Material tickMat, float dps, int ticks)
        {
            GameObject root = Group(name, parent);
            root.transform.position = worldCenter;
            root.transform.rotation = Quaternion.Euler(euler);

            GameObject torus = new GameObject("Torus");
            torus.transform.SetParent(root.transform, false);
            torus.AddComponent<MeshFilter>().sharedMesh = Torus(radius, tube);
            MeshRenderer mr = torus.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ringMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            for (int k = 0; k < ticks; k++)
            {
                float a = k / (float)ticks * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                float len = (k % 3 == 0) ? tube * 5f : tube * 3f;
                GameObject tick = Prim(PrimitiveType.Cube, "Tick", root.transform, p, new Vector3(len, tube * 1.4f, tube * 1.4f), tickMat, false, false);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }

            if (Mathf.Abs(dps) > 0.01f)
            {
                root.AddComponent<ArenaMover>().ConfigureRotate(Vector3.forward, dps, false);
            }
            return root;
        }

        // ------------------------------------------------------------------ player

        private static PlayerController BuildPlayer(Vector3 spawn, out Transform visualRoot)
        {
            GameObject root = new GameObject("Player");
            root.tag = "Player";
            root.transform.position = spawn;

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1f, 0f);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.06f;
            cc.slopeLimit = 50f;

            Transform v = Group("Visual", root.transform).transform;
            visualRoot = v;

            Prim(PrimitiveType.Capsule, "Body", v, new Vector3(0f, 1.05f, 0f), new Vector3(0.8f, 0.85f, 0.8f), playerBody, false);
            Prim(PrimitiveType.Cube, "Visor", v, new Vector3(0f, 1.4f, 0.3f), new Vector3(0.52f, 0.2f, 0.22f), glowCyan, false, false);
            Prim(PrimitiveType.Sphere, "Core", v, new Vector3(0f, 1f, 0.37f), Vector3.one * 0.2f, glowGold, false, false);
            Prim(PrimitiveType.Cube, "Pack", v, new Vector3(0f, 1.1f, -0.36f), new Vector3(0.5f, 0.7f, 0.25f), deckDark, false);
            Prim(PrimitiveType.Cube, "PackGlow", v, new Vector3(0f, 1.1f, -0.5f), new Vector3(0.36f, 0.08f, 0.03f), glowCyan, false, false);
            Prim(PrimitiveType.Cylinder, "Belt", v, new Vector3(0f, 0.75f, 0f), new Vector3(0.92f, 0.025f, 0.92f), glowCyan, false, false);
            Prim(PrimitiveType.Cylinder, "BootL", v, new Vector3(-0.16f, 0.06f, 0f), new Vector3(0.34f, 0.015f, 0.34f), glowCyan, false, false);
            Prim(PrimitiveType.Cylinder, "BootR", v, new Vector3(0.16f, 0.06f, 0f), new Vector3(0.34f, 0.015f, 0.34f), glowCyan, false, false);

            GameObject trailGo = Group("Trail", v);
            trailGo.transform.localPosition = new Vector3(0f, 0.7f, -0.35f);
            TrailRenderer trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.minVertexDistance = 0.15f;
            trail.widthMultiplier = 0.45f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.3f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.6f, 0.5f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.sharedMaterial = trailMat != null ? trailMat : glowCyan;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            return root.AddComponent<PlayerController>();
        }

        // ------------------------------------------------------------------ HUD

        private static void BuildHud(out MobileJoystick joystick, out MobileJumpButton jump, out Text timer, out Text stats,
                                     out Text banner, out GameObject resultPanel, out Text resultText, out Button restartButton)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            GameObject canvasGo = new GameObject("HUD");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform c = canvasGo.transform;

            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            Vector2 topCenter = new Vector2(0.5f, 1f);
            Vector2 topLeft = new Vector2(0f, 1f);
            Vector2 center = new Vector2(0.5f, 0.5f);

            // Timer + arena label
            RectTransform timerRt = UiRect("Timer", c, topCenter, topCenter, topCenter, new Vector2(0f, -24f), new Vector2(520f, 100f));
            timer = UiText(timerRt, "00:00.00", 72, TextAnchor.MiddleCenter, Color.white, true);
            RectTransform labelRt = UiRect("ArenaLabel", c, topCenter, topCenter, topCenter, new Vector2(0f, -122f), new Vector2(700f, 40f));
            UiText(labelRt, "ARENA 01 - THE TEST", 28, TextAnchor.MiddleCenter, CyanText, true);

            // Stats
            RectTransform statsRt = UiRect("Stats", c, topLeft, topLeft, topLeft, new Vector2(36f, -24f), new Vector2(560f, 190f));
            stats = UiText(statsRt, "", 34, TextAnchor.UpperLeft, Color.white, true);

            // Banner
            RectTransform bannerRt = UiRect("Banner", c, center, center, center, new Vector2(0f, 180f), new Vector2(1400f, 220f));
            banner = UiText(bannerRt, "", 110, TextAnchor.MiddleCenter, Color.white, true);

            // Joystick (left half of screen, floating)
            RectTransform area = UiRect("JoystickArea", c, new Vector2(0f, 0f), new Vector2(0.5f, 0.65f), center, Vector2.zero, Vector2.zero);
            Image areaImage = area.gameObject.AddComponent<Image>();
            areaImage.color = new Color(1f, 1f, 1f, 0f);
            areaImage.raycastTarget = true;
            joystick = area.gameObject.AddComponent<MobileJoystick>();

            RectTransform bg = UiRect("Background", area, center, center, center, new Vector2(-240f, -120f), new Vector2(260f, 260f));
            Image bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = knob;
            bgImage.color = new Color(0.55f, 0.9f, 1f, 0.5f);
            bgImage.raycastTarget = false;
            CanvasGroup group = bg.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform handle = UiRect("Handle", bg, center, center, center, Vector2.zero, new Vector2(110f, 110f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = knob;
            handleImage.color = new Color(1f, 1f, 1f, 0.85f);
            handleImage.raycastTarget = false;
            joystick.Bind(area, bg, handle, group);

            // Jump button (bottom right)
            Vector2 bottomRight = new Vector2(1f, 0f);
            RectTransform jumpRt = UiRect("JumpButton", c, bottomRight, bottomRight, bottomRight, new Vector2(-140f, 140f), new Vector2(250f, 250f));
            Image jumpImage = jumpRt.gameObject.AddComponent<Image>();
            jumpImage.sprite = knob;
            jumpImage.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            jump = jumpRt.gameObject.AddComponent<MobileJumpButton>();
            RectTransform jumpLabel = UiRect("Label", jumpRt, center, center, center, Vector2.zero, new Vector2(240f, 80f));
            Text jumpText = UiText(jumpLabel, "JUMP", 44, TextAnchor.MiddleCenter, Color.white, true);
            jumpText.raycastTarget = false;

            // Result panel
            RectTransform panelRt = UiRect("ResultPanel", c, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            Image panelImage = panelRt.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.05f, 0.12f, 0.8f);
            resultPanel = panelRt.gameObject;

            RectTransform resultRt = UiRect("ResultText", panelRt, center, center, center, new Vector2(0f, 80f), new Vector2(1200f, 640f));
            resultText = UiText(resultRt, "", 56, TextAnchor.MiddleCenter, Color.white, true);
            resultText.raycastTarget = false;

            Vector2 bottomCenter = new Vector2(0.5f, 0f);
            RectTransform buttonRt = UiRect("RestartButton", panelRt, bottomCenter, bottomCenter, bottomCenter, new Vector2(0f, 90f), new Vector2(460f, 120f));
            Image buttonImage = buttonRt.gameObject.AddComponent<Image>();
            buttonImage.sprite = rounded;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = new Color(0.25f, 0.85f, 1f, 0.95f);
            restartButton = buttonRt.gameObject.AddComponent<Button>();
            restartButton.targetGraphic = buttonImage;
            RectTransform buttonLabel = UiRect("Label", buttonRt, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            Text buttonText = UiText(buttonLabel, "RESTART", 56, TextAnchor.MiddleCenter, new Color(0.02f, 0.08f, 0.18f, 1f), false);
            buttonText.raycastTarget = false;

            panelRt.gameObject.SetActive(false);
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static Text UiText(RectTransform rt, string content, int size, TextAnchor alignment, Color color, bool outline)
        {
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (outline)
            {
                Outline o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.02f, 0.06f, 0.16f, 0.85f);
                o.effectDistance = new Vector2(3f, -3f);
            }
            return text;
        }

        // ------------------------------------------------------------------ primitives / meshes

        private static GameObject Group(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
                                       Material mat, bool collider = false, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = scale;

            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;

            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            return go;
        }

        private static Mesh Torus(float radius, float tube)
        {
            string path = GenRoot + "/Meshes/Torus_" + Mathf.RoundToInt(radius * 10f) + "_" + Mathf.RoundToInt(tube * 100f) + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            const int seg = 64;
            const int sides = 14;
            Vector3[] verts = new Vector3[(seg + 1) * (sides + 1)];
            Vector3[] normals = new Vector3[verts.Length];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[seg * sides * 6];

            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg * Mathf.PI * 2f;
                float cu = Mathf.Cos(u);
                float su = Mathf.Sin(u);
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides * Mathf.PI * 2f;
                    float cv = Mathf.Cos(v);
                    float sv = Mathf.Sin(v);
                    int idx = i * (sides + 1) + j;
                    verts[idx] = new Vector3((radius + tube * cv) * cu, (radius + tube * cv) * su, tube * sv);
                    normals[idx] = new Vector3(cv * cu, cv * su, sv);
                    uvs[idx] = new Vector2(i / (float)seg * 8f, j / (float)sides);
                }
            }

            int n = 0;
            for (int i = 0; i < seg; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j;
                    int b = a + sides + 1;
                    int c = a + 1;
                    int d = b + 1;
                    tris[n++] = a; tris[n++] = b; tris[n++] = c;
                    tris[n++] = c; tris[n++] = b; tris[n++] = d;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = Path.GetFileNameWithoutExtension(path);
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Faceted inverted-cone rock, radius 1, ~1.5 tall, apex pointing down.</summary>
        private static Mesh RockMesh(int variant)
        {
            string path = GenRoot + "/Meshes/Rock_" + variant + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            System.Random r = new System.Random(variant * 7919 + 13);
            const int seg = 9;
            Vector3[] upper = new Vector3[seg];
            Vector3[] lower = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float ru = 1f + ((float)r.NextDouble() - 0.5f) * 0.3f;
                float rl = 0.6f + ((float)r.NextDouble() - 0.5f) * 0.25f;
                upper[i] = new Vector3(Mathf.Cos(a) * ru, -(float)r.NextDouble() * 0.12f, Mathf.Sin(a) * ru);
                lower[i] = new Vector3(Mathf.Cos(a + 0.2f) * rl, -0.65f - (float)r.NextDouble() * 0.15f, Mathf.Sin(a + 0.2f) * rl);
            }
            Vector3 apex = new Vector3(((float)r.NextDouble() - 0.5f) * 0.3f, -1.5f, ((float)r.NextDouble() - 0.5f) * 0.3f);

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            for (int i = 0; i < seg; i++)
            {
                int k = (i + 1) % seg;
                AddTri(verts, tris, upper[i], upper[k], lower[i]);
                AddTri(verts, tris, upper[k], lower[k], lower[i]);
                AddTri(verts, tris, lower[i], lower[k], apex);
            }

            Mesh mesh = new Mesh();
            mesh.name = Path.GetFileNameWithoutExtension(path);
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int s = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            tris.Add(s);
            tris.Add(s + 1);
            tris.Add(s + 2);
        }

        // ------------------------------------------------------------------ materials

        private static void InitPalette()
        {
            deck = Mat("CD_Deck", new Color(0.86f, 0.9f, 0.98f), Color.black, 0.65f, 0.1f);
            deckDark = Mat("CD_DeckDark", new Color(0.3f, 0.34f, 0.5f), Color.black, 0.5f, 0.3f);
            tower = Mat("CD_Tower", new Color(0.92f, 0.95f, 1f), new Color(0.05f, 0.08f, 0.14f), 0.7f, 0.2f);
            glowCyan = Mat("CD_GlowCyan", new Color(0.1f, 0.8f, 1f), new Color(0.2f, 1.8f, 2.6f), 0.4f, 0f);
            glowGold = Mat("CD_GlowGold", new Color(1f, 0.8f, 0.35f), new Color(3f, 2.1f, 0.6f), 0.4f, 0f);
            glowMagenta = Mat("CD_GlowMagenta", new Color(1f, 0.15f, 0.55f), new Color(3f, 0.25f, 1.2f), 0.4f, 0f);
            glowViolet = Mat("CD_GlowViolet", new Color(0.6f, 0.4f, 1f), new Color(1.2f, 0.7f, 3f), 0.4f, 0f);
            glowWhite = Mat("CD_GlowWhite", Color.white, new Color(2.5f, 2.5f, 3f), 0.4f, 0f);
            cloudMat = Mat("CD_Cloud", new Color(0.97f, 0.98f, 1f), new Color(0.35f, 0.4f, 0.55f), 0.1f, 0f);
            hazardBody = Mat("CD_Hazard", new Color(0.25f, 0.05f, 0.18f), new Color(0.6f, 0.05f, 0.25f), 0.5f, 0.2f);
            playerBody = Mat("CD_Player", new Color(0.95f, 0.97f, 1f), new Color(0.05f, 0.08f, 0.12f), 0.75f, 0.3f);

            Texture2D glow = SoftGlowTexture();
            particleMat = SpriteMat("CD_Particle", glow);
            trailMat = SpriteMat("CD_Trail", glow);
        }

        private static Material Mat(string name, Color color, Color emission, float smoothness, float metallic)
        {
            string path = GenRoot + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material m = new Material(LitShader());
            m.name = name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material SpriteMat(string name, Texture2D texture)
        {
            string path = GenRoot + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader s = Shader.Find("Sprites/Default");
            if (s == null) return null;

            Material m = new Material(s);
            m.name = name;
            if (texture != null) m.mainTexture = texture;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Shader LitShader()
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                Material dm = rp.defaultMaterial;
                if (dm != null && dm.shader != null) return dm.shader;
                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }
            Shader std = Shader.Find("Standard");
            return std != null ? std : Shader.Find("Universal Render Pipeline/Lit");
        }

        private static Texture2D SoftGlowTexture()
        {
            string path = GenRoot + "/Textures/SoftGlow.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int n = 64;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ utilities

        private static float Rand(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static Font LoadUiFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void MarkStatic(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].GetComponentInParent<ArenaMover>() != null) continue;
                if (all[i].GetComponentInParent<Checkpoint>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(all[i].gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void AddTriggerBody(GameObject go)
        {
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].path == scenePath) return;
            }
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
