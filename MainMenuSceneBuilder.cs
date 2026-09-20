using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Menu: ZETRA > CLASHDASH > Build Main Menu Scene
    /// Builds an animated futuristic backdrop and a MainMenuController (which builds its own UI at runtime),
    /// saves it as Assets/Scenes/ClashDash_MainMenu.unity and puts it first in Build Settings.
    /// If that scene already exists it is left untouched.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        private const string GenRoot = "Assets/ClashDash/Generated";
        private const string ScenePath = "Assets/Scenes/ClashDash_MainMenu.unity";

        private static Material deck, tower, glowCyan, glowGold, glowViolet, cloud;
        private static System.Random rng;

        [MenuItem("ZETRA/CLASHDASH/Build Main Menu Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("CLASHDASH", "Stop Play mode before building the menu scene.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EnsureBuildSettings(ScenePath, null);
                EditorUtility.DisplayDialog("CLASHDASH",
                    "The main menu scene already exists, so it was left untouched. It is now first in Build Settings.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(GenRoot + "/Materials");
            EnsureFolder("Assets/Scenes");

            rng = new System.Random(99);
            InitPalette();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildBackdrop();
            BuildMotes();

            // Camera orbiting slowly around the scene centre
            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.position = new Vector3(0f, 6f, 0f);
            pivot.AddComponent<ArenaMover>().ConfigureRotate(Vector3.up, 4f, false);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 4f, -34f);
            camGo.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1500f;
            camGo.AddComponent<AudioListener>();

            // Menu controller (UI is built at runtime)
            GameObject menuGo = new GameObject("MainMenu");
            MainMenuController menu = menuGo.AddComponent<MainMenuController>();

            string arenaScenePath = null;
            string[] guids = AssetDatabase.FindAssets("Arena01_TheTest t:SceneAsset");
            if (guids.Length > 0)
            {
                arenaScenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                menu.SetArenaScene(Path.GetFileNameWithoutExtension(arenaScenePath));
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildSettings(ScenePath, arenaScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = menuGo;
            Debug.Log("[CLASHDASH] Main menu scene built: " + ScenePath +
                      (arenaScenePath == null ? "  (Arena scene not found yet - run 'Build Arena 01' then set it on the MainMenu object.)" : ""));
        }

        // ------------------------------------------------------------------ scene content

        private static void BuildLighting()
        {
            GameObject sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.72f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.58f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 1f);
            RenderSettings.fogDensity = 0.0032f;

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

        private static void BuildBackdrop()
        {
            Transform root = new GameObject("Backdrop").transform;

            // Central orb + concentric rings of glowing blocks
            Prim(PrimitiveType.Sphere, "Orb", root, new Vector3(0f, 6f, 0f), Vector3.one * 8f, glowGold);
            Vector3 c = new Vector3(0f, 6f, 0f);
            RingOfBlocks(root, "Ring_A", c, new Vector3(0f, 0f, 0f), 14f, 28, glowCyan, 14f);
            RingOfBlocks(root, "Ring_B", c, new Vector3(60f, 0f, 0f), 20f, 34, glowViolet, -10f);
            RingOfBlocks(root, "Ring_C", c, new Vector3(0f, 60f, 30f), 27f, 40, glowGold, 7f);

            // Floating slabs bobbing around the centre
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f + Rand(-0.2f, 0.2f);
                float radius = Rand(34f, 60f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Rand(-4f, 22f), Mathf.Sin(angle) * radius);
                Vector3 size = new Vector3(Rand(6f, 12f), 1f, Rand(6f, 12f));

                Transform slab = new GameObject("Slab_" + i).transform;
                slab.SetParent(root, false);
                slab.position = pos;
                Prim(PrimitiveType.Cube, "Body", slab, Vector3.zero, size, deck);
                Prim(PrimitiveType.Cube, "Trim", slab, new Vector3(0f, 0.53f, size.z * 0.5f - 0.15f), new Vector3(size.x - 0.2f, 0.06f, 0.24f), glowCyan);
                Prim(PrimitiveType.Cube, "Under", slab, new Vector3(0f, -0.52f, 0f), new Vector3(size.x * 0.9f, 0.05f, size.z * 0.9f), glowViolet);
                slab.gameObject.AddComponent<ArenaMover>().ConfigurePingPong(new Vector3(0f, Rand(1f, 2.5f), 0f), Rand(5f, 9f), Rand(0f, 1f), false);
            }

            // Distant towers
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f + 0.3f;
                float radius = Rand(110f, 150f);
                float h = Rand(160f, 280f);
                float w = Rand(16f, 28f);

                Transform t = new GameObject("Tower_" + i).transform;
                t.SetParent(root, false);
                t.position = new Vector3(Mathf.Cos(angle) * radius, h * 0.5f - 60f, Mathf.Sin(angle) * radius);
                Prim(PrimitiveType.Cylinder, "Shaft", t, Vector3.zero, new Vector3(w, h * 0.5f, w), tower);
                for (int b = 1; b <= 4; b++)
                {
                    float y = -h * 0.5f + b * h / 5f;
                    Prim(PrimitiveType.Cylinder, "Band", t, new Vector3(0f, y, 0f), new Vector3(w * 1.1f, 0.5f, w * 1.1f), b % 2 == 0 ? glowViolet : glowCyan);
                }
                Prim(PrimitiveType.Sphere, "Crown", t, new Vector3(0f, h * 0.5f + w * 0.2f, 0f), Vector3.one * w * 0.55f, glowGold);
            }

            // Cloud sea
            for (int i = 0; i < 30; i++)
            {
                Vector3 pos = new Vector3(Rand(-220f, 220f), Rand(-70f, -40f), Rand(-220f, 220f));
                Vector3 scale = new Vector3(Rand(40f, 90f), Rand(8f, 16f), Rand(40f, 90f));
                Prim(PrimitiveType.Sphere, "Cloud", root, pos, scale, cloud);
            }
        }

        private static void RingOfBlocks(Transform parent, string name, Vector3 center, Vector3 euler, float radius, int count,
                                         Material mat, float dps)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            ring.transform.position = center;
            ring.transform.rotation = Quaternion.Euler(euler);

            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                float len = (i % 3 == 0) ? 2.6f : 1.6f;
                GameObject block = Prim(PrimitiveType.Cube, "Block", ring.transform, p, new Vector3(len, 0.5f, 0.5f), mat);
                block.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }

            ring.AddComponent<ArenaMover>().ConfigureRotate(Vector3.forward, dps, false);
        }

        private static void BuildMotes()
        {
            Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(GenRoot + "/Materials/CD_Particle.mat");
            if (particleMat == null) return; // created by the Arena builder; motes are optional here

            GameObject go = new GameObject("AtmosphereMotes");
            go.transform.position = new Vector3(0f, 15f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 12f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.9f, 1f, 0.6f), new Color(1f, 0.95f, 0.8f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 20f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(140f, 70f, 140f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = particleMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;

            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }

        private static void InitPalette()
        {
            deck = Mat("CD_Deck", new Color(0.86f, 0.9f, 0.98f), Color.black, 0.65f, 0.1f);
            tower = Mat("CD_Tower", new Color(0.92f, 0.95f, 1f), new Color(0.05f, 0.08f, 0.14f), 0.7f, 0.2f);
            glowCyan = Mat("CD_GlowCyan", new Color(0.1f, 0.8f, 1f), new Color(0.2f, 1.8f, 2.6f), 0.4f, 0f);
            glowGold = Mat("CD_GlowGold", new Color(1f, 0.8f, 0.35f), new Color(3f, 2.1f, 0.6f), 0.4f, 0f);
            glowViolet = Mat("CD_GlowViolet", new Color(0.6f, 0.4f, 1f), new Color(1.2f, 0.7f, 3f), 0.4f, 0f);
            cloud = Mat("CD_Cloud", new Color(0.97f, 0.98f, 1f), new Color(0.35f, 0.4f, 0.55f), 0.1f, 0f);
        }

        /// <summary>Reuses the arena builder's materials when they exist; otherwise creates them with the same names.</summary>
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

        private static float Rand(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Menu scene first, arena scene (if found) right after; other entries keep their order.</summary>
        private static void EnsureBuildSettings(string menuPath, string arenaPath)
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == menuPath);
            list.Insert(0, new EditorBuildSettingsScene(menuPath, true));

            if (!string.IsNullOrEmpty(arenaPath))
            {
                bool present = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].path == arenaPath) present = true;
                }
                if (!present) list.Add(new EditorBuildSettingsScene(arenaPath, true));
            }

            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
