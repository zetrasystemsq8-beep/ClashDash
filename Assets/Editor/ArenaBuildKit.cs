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
    /// <summary>Colours and lighting for one arena. Materials are saved as assets with the theme's prefix.</summary>
    public sealed class ArenaTheme
    {
        public string Prefix = "CDX_";

        public Color SkyTint = new Color(0.45f, 0.62f, 1f);
        public Color SkyGround = new Color(0.85f, 0.9f, 1f);
        public float Exposure = 1.3f;

        public Color FogColor = new Color(0.72f, 0.82f, 1f);
        public float FogDensity = 0.0024f;

        public Color SunColor = new Color(1f, 0.95f, 0.86f);
        public float SunIntensity = 1.2f;
        public Vector3 SunEuler = new Vector3(48f, -32f, 0f);

        public Color AmbientSky = new Color(0.62f, 0.76f, 1f);
        public Color AmbientEquator = new Color(0.72f, 0.72f, 0.9f);
        public Color AmbientGround = new Color(0.4f, 0.34f, 0.58f);

        public Color Deck = new Color(0.86f, 0.9f, 0.98f);
        public Color DeckDark = new Color(0.3f, 0.34f, 0.5f);
        public Color Tower = new Color(0.92f, 0.95f, 1f);
        public Color Cloud = new Color(0.97f, 0.98f, 1f);

        /// <summary>Safe / path accent (edge strips, checkpoints, boost pads).</summary>
        public Color Safe = new Color(0.1f, 0.8f, 1f);
        public Color Secondary = new Color(0.6f, 0.4f, 1f);
        public Color Gold = new Color(1f, 0.8f, 0.35f);
        /// <summary>Danger accent (hazards, lasers, meteors).</summary>
        public Color Hazard = new Color(1f, 0.15f, 0.55f);
    }

    /// <summary>
    /// Shared building blocks for the procedural arenas (ARENA 02 and later). The generic helpers below the
    /// "shared helpers" marker mirror the ones inside ArenaOneBuilder (which keeps them private), so new arenas can
    /// be built without touching that file. Material fields keep their historical names: glowCyan = safe accent,
    /// glowViolet = secondary accent, glowMagenta = hazard accent.
    /// </summary>
    public static class ArenaBuildKit
    {
        public const string GenRoot = "Assets/ClashDash/Generated";
        public const float PathWidth = 10f;

        public static Material deck, deckDark, tower, glowCyan, glowGold, glowMagenta, glowViolet, glowWhite;
        public static Material cloudMat, hazardBody, playerBody, trailMat, particleMat, energyMat, chevronMat;
        public static System.Random rng;
        public static Font uiFont;
        public static string CurrentTitle = "ARENA";

        public static readonly Color CyanText = new Color(0.55f, 0.95f, 1f, 1f);
        public static readonly Color GoldText = new Color(1f, 0.86f, 0.5f, 1f);
        public static readonly Color VioletText = new Color(0.78f, 0.68f, 1f, 1f);
        public static readonly Color MagentaText = new Color(1f, 0.5f, 0.78f, 1f);

        // ------------------------------------------------------------------ theme / palette

        public static void Begin(ArenaTheme t, int seed)
        {
            rng = new System.Random(seed);
            uiFont = LoadUiFont();

            string p = t.Prefix;
            deck = Mat(p + "Deck", t.Deck, Color.black, 0.65f, 0.1f);
            deckDark = Mat(p + "DeckDark", t.DeckDark, Color.black, 0.5f, 0.3f);
            tower = Mat(p + "Tower", t.Tower, new Color(0.05f, 0.08f, 0.14f), 0.7f, 0.2f);
            glowCyan = Mat(p + "GlowSafe", t.Safe, Emit(t.Safe, 2.4f), 0.4f, 0f);
            glowViolet = Mat(p + "GlowSecondary", t.Secondary, Emit(t.Secondary, 2.4f), 0.4f, 0f);
            glowGold = Mat(p + "GlowGold", t.Gold, Emit(t.Gold, 2.6f), 0.4f, 0f);
            glowMagenta = Mat(p + "GlowHot", t.Hazard, Emit(t.Hazard, 3f), 0.4f, 0f);
            glowWhite = Mat(p + "GlowWhite", Color.white, new Color(2.5f, 2.5f, 3f), 0.4f, 0f);
            cloudMat = Mat(p + "Cloud", t.Cloud, new Color(0.35f, 0.4f, 0.55f), 0.1f, 0f);
            hazardBody = Mat(p + "Hazard", Color.Lerp(t.Hazard, Color.black, 0.75f), Emit(t.Hazard, 0.6f), 0.5f, 0.2f);
            playerBody = Mat(p + "Player", new Color(0.95f, 0.97f, 1f), new Color(0.05f, 0.08f, 0.12f), 0.75f, 0.3f);

            Texture2D glow = SoftGlowTexture();
            particleMat = SpriteMat("CD_Particle", glow);
            trailMat = SpriteMat("CD_Trail", glow);

            energyMat = ShaderMat(p + "Energy", "CLASHDASH/EnergyField", glowMagenta, m =>
            {
                m.SetColor("_Color", t.Hazard);
                m.SetColor("_Color2", t.Gold);
                m.SetFloat("_Intensity", 2.4f);
                m.SetFloat("_Alpha", 0.6f);
            });

            chevronMat = ShaderMat(p + "Chevrons", "CLASHDASH/BoostChevrons", glowCyan, m =>
            {
                m.SetColor("_Color", Color.Lerp(t.Safe, Color.white, 0.25f));
                m.SetColor("_BaseColor", Color.Lerp(t.Safe, Color.black, 0.9f));
                m.SetFloat("_Intensity", 2.6f);
            });
        }

        private static Color Emit(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        private static Material ShaderMat(string name, string shaderName, Material fallback, System.Action<Material> setup)
        {
            string path = GenRoot + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null) return fallback;

            Material m = new Material(shader);
            m.name = name;
            setup(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ lighting / environment

        public static void BuildThemedLighting(ArenaTheme t)
        {
            GameObject sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = t.SunColor;
            sun.intensity = t.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(t.SunEuler);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = t.AmbientSky;
            RenderSettings.ambientEquatorColor = t.AmbientEquator;
            RenderSettings.ambientGroundColor = t.AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = t.FogColor;
            RenderSettings.fogDensity = t.FogDensity;

            string skyPath = GenRoot + "/Materials/" + t.Prefix + "Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    sky = new Material(skyShader);
                    sky.name = t.Prefix + "Sky";
                    if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", t.SkyTint);
                    if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", t.SkyGround);
                    if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", t.Exposure);
                    if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.9f);
                    if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.06f);
                    AssetDatabase.CreateAsset(sky, skyPath);
                }
            }
            if (sky != null) RenderSettings.skybox = sky;
        }

        public static void BuildThemedEnvironment(Transform root, Vector3 start, Vector3 end)
        {
            float length = Mathf.Max(100f, end.z - start.z);

            // Towers flanking the course
            Transform towers = Group("Towers", root).transform;
            int rows = Mathf.Max(4, Mathf.RoundToInt((length + 200f) / 80f));
            for (int i = 0; i < rows; i++)
            {
                float z = start.z - 60f + i * (length + 200f) / (rows - 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * Rand(100f, 150f);
                    float h = Mathf.Round(Rand(150f, 300f));
                    float w = Mathf.Round(Rand(16f, 30f));
                    Tower(towers, "Tower_" + i + (side < 0 ? "L" : "R"), new Vector3(x, h * 0.5f - 60f, z + Rand(-15f, 15f)), w, h, true);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                Tower(towers, "MegaTower_" + i, new Vector3(-200f + i * 100f, 140f, end.z + 270f + Rand(-30f, 30f)), 60f, 400f, false);
            }

            // Sky bridges
            Transform bridges = Group("SkyBridges", root).transform;
            Bridge(bridges, "Bridge_A", new Vector3(0f, 110f, start.z + length * 0.08f), 330f);
            Bridge(bridges, "Bridge_B", new Vector3(0f, 150f, start.z + length * 0.5f), 330f);
            Bridge(bridges, "Bridge_C", new Vector3(0f, 90f, start.z + length * 0.9f), 330f);

            // Floating islands
            Transform islands = Group("Islands", root).transform;
            for (int i = 0; i < 16; i++)
            {
                int side = (i % 2 == 0) ? -1 : 1;
                float x = side * Rand(34f, 75f);
                float z = start.z - 20f + i * (length + 40f) / 16f + Rand(-8f, 8f);
                float y = Rand(-6f, 32f);
                Island(islands, "Island_" + i, new Vector3(x, y, z), Rand(5f, 13f), i);
            }

            // Dimensional gate rings the course runs through
            Transform rings = Group("DimensionalRings", root).transform;
            float[] fractions = { 0.2f, 0.5f, 0.8f };
            for (int i = 0; i < fractions.Length; i++)
            {
                Ring(rings, "GateRing_" + i, new Vector3(0f, 25f, start.z + length * fractions[i]), Vector3.zero, 70f, 1.6f,
                     glowCyan, glowWhite, (i % 2 == 0) ? 4f : -5f, 24);
            }

            // Core at the end of the view line
            Transform core = Group("ZetraCore", root).transform;
            core.position = new Vector3(0f, 75f, end.z + 55f);
            Prim(PrimitiveType.Sphere, "Orb", core, Vector3.zero, Vector3.one * 20f, glowGold, false, false);
            Ring(core, "CoreRing_A", core.position, new Vector3(0f, 0f, 0f), 34f, 1.2f, glowCyan, glowWhite, 8f, 16);
            Ring(core, "CoreRing_B", core.position, new Vector3(60f, 0f, 0f), 42f, 1.2f, glowViolet, glowWhite, -12f, 20);
            Ring(core, "CoreRing_C", core.position, new Vector3(0f, 60f, 30f), 52f, 1.4f, glowGold, glowWhite, 5f, 24);

            // Cloud sea below
            Transform clouds = Group("CloudSea", root).transform;
            for (int i = 0; i < 50; i++)
            {
                Vector3 pos = new Vector3(Rand(-260f, 260f), Rand(-95f, -55f), Rand(start.z - 120f, end.z + 230f));
                Vector3 scale = new Vector3(Rand(40f, 90f), Rand(8f, 16f), Rand(40f, 90f));
                Prim(PrimitiveType.Sphere, "Cloud", clouds, pos, scale, cloudMat, false, false);
            }

            // Drifting motes, stretched along the course
            BuildParticles(root);
            Transform motes = root.Find("AtmosphereMotes");
            if (motes != null)
            {
                motes.position = new Vector3(0f, 30f, start.z + length * 0.5f);
                ParticleSystem ps = motes.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ParticleSystem.ShapeModule shape = ps.shape;
                    shape.scale = new Vector3(240f, 80f, length + 200f);
                }
            }
        }

        // ------------------------------------------------------------------ new obstacle builders

        /// <summary>Hanging hammer that swings across the track. floorY is used to size the support pillars.</summary>
        public static Pendulum BuildPendulum(Transform parent, string name, Vector3 pivot, float armLength, float amplitude,
                                             float period, float phase, float frameHalfWidth, float floorY)
        {
            // Fixed support frame (does not swing)
            Transform frame = Group(name + "_Frame", parent).transform;
            frame.position = pivot;
            float pillarHeight = pivot.y + 1f - floorY;
            float pillarCenter = (floorY + pivot.y + 1f) * 0.5f - pivot.y;
            Prim(PrimitiveType.Cylinder, "PillarL", frame, new Vector3(-frameHalfWidth, pillarCenter, 0f), new Vector3(0.9f, pillarHeight * 0.5f, 0.9f), deckDark, true);
            Prim(PrimitiveType.Cylinder, "PillarR", frame, new Vector3(frameHalfWidth, pillarCenter, 0f), new Vector3(0.9f, pillarHeight * 0.5f, 0.9f), deckDark, true);
            Prim(PrimitiveType.Cube, "Beam", frame, new Vector3(0f, 0.6f, 0f), new Vector3(frameHalfWidth * 2f + 1.2f, 0.7f, 1f), deckDark, false);
            Prim(PrimitiveType.Cube, "BeamGlow", frame, new Vector3(0f, 0.2f, 0f), new Vector3(frameHalfWidth * 2f, 0.1f, 1.05f), glowGold, false, false);

            // Swinging part
            GameObject root = Group(name, parent);
            root.transform.position = pivot;
            Transform t = root.transform;

            Prim(PrimitiveType.Sphere, "Hub", t, Vector3.zero, Vector3.one * 0.7f, glowGold, false, false);
            Prim(PrimitiveType.Cube, "Arm", t, new Vector3(0f, -armLength * 0.5f, 0f), new Vector3(0.28f, armLength, 0.28f), deckDark, false);

            GameObject head = Group("Head", t);
            head.transform.localPosition = new Vector3(0f, -armLength, 0f);
            Prim(PrimitiveType.Cube, "Body", head.transform, Vector3.zero, new Vector3(2.4f, 2.4f, 2.4f), hazardBody, true);
            Prim(PrimitiveType.Cube, "Core", head.transform, Vector3.zero, new Vector3(2f, 2f, 2.55f), glowMagenta, false, false);

            BoxCollider trigger = head.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.9f, 2.9f, 2.9f);
            head.AddComponent<Hazard>().Configure(HazardMode.Penalty, 2f, 13f, 6f, false, "PENDULUM");

            Pendulum pendulum = root.AddComponent<Pendulum>();
            pendulum.Bind(head.transform, amplitude, period, phase);
            return pendulum;
        }

        /// <summary>Energy wall that switches on and off; the hazard is only active while the beam is visible.</summary>
        public static PulseGate BuildPulseGate(Transform parent, string name, Vector3 baseCenter, float width, float height,
                                               float onTime, float offTime, float phase)
        {
            GameObject root = Group(name, parent);
            root.transform.position = baseCenter;
            Transform t = root.transform;

            Prim(PrimitiveType.Cylinder, "PostL", t, new Vector3(-width * 0.5f - 0.3f, height * 0.5f, 0f), new Vector3(0.6f, height * 0.5f, 0.6f), deckDark, true);
            Prim(PrimitiveType.Cylinder, "PostR", t, new Vector3(width * 0.5f + 0.3f, height * 0.5f, 0f), new Vector3(0.6f, height * 0.5f, 0.6f), deckDark, true);
            Prim(PrimitiveType.Cube, "Lintel", t, new Vector3(0f, height + 0.2f, 0f), new Vector3(width + 1.6f, 0.5f, 0.7f), deckDark, false);
            Prim(PrimitiveType.Cube, "EmitterL", t, new Vector3(-width * 0.5f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), glowMagenta, false, false);
            Prim(PrimitiveType.Cube, "EmitterR", t, new Vector3(width * 0.5f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), glowMagenta, false, false);

            GameObject field = Prim(PrimitiveType.Cube, "Field", t, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.12f), energyMat, false, false);
            GameObject beam = Prim(PrimitiveType.Cube, "TopBeam", t, new Vector3(0f, height, 0f), new Vector3(width, 0.12f, 0.2f), glowMagenta, false, false);

            GameObject hazard = Group("Hazard", t);
            hazard.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            BoxCollider trigger = hazard.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(width, height, 0.9f);
            AddTriggerBody(hazard);
            hazard.AddComponent<Hazard>().Configure(HazardMode.Penalty, 2f, 12f, 4f, true, "LASER");

            PulseGate gate = root.AddComponent<PulseGate>();
            Renderer[] beams = { field.GetComponent<Renderer>(), beam.GetComponent<Renderer>() };
            gate.Bind(hazard, beams, onTime, offTime, 0.6f, phase);
            return gate;
        }

        /// <summary>Platform that crumbles shortly after the player steps on it, then returns.</summary>
        public static FallingTile BuildFallingTile(Transform parent, string name, Vector3 topCenter, Vector2 size)
        {
            GameObject root = Group(name, parent);
            root.transform.position = new Vector3(topCenter.x, topCenter.y - 0.5f, topCenter.z);
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Slab", t, Vector3.zero, new Vector3(size.x, 1f, size.y), deck, true);
            Prim(PrimitiveType.Cube, "Hull", t, new Vector3(0f, -1f, 0f), new Vector3(size.x * 0.6f, 1.2f, size.y * 0.6f), deckDark, false);
            Prim(PrimitiveType.Cube, "TrimL", t, new Vector3(-(size.x * 0.5f - 0.12f), 0.53f, 0f), new Vector3(0.24f, 0.06f, size.y - 0.2f), glowGold, false, false);
            Prim(PrimitiveType.Cube, "TrimR", t, new Vector3(size.x * 0.5f - 0.12f, 0.53f, 0f), new Vector3(0.24f, 0.06f, size.y - 0.2f), glowGold, false, false);
            Prim(PrimitiveType.Cube, "TrimF", t, new Vector3(0f, 0.53f, size.y * 0.5f - 0.12f), new Vector3(size.x - 0.2f, 0.06f, 0.24f), glowGold, false, false);
            Prim(PrimitiveType.Cube, "TrimB", t, new Vector3(0f, 0.53f, -(size.y * 0.5f - 0.12f)), new Vector3(size.x - 0.2f, 0.06f, 0.24f), glowGold, false, false);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.85f, 0f);
            trigger.size = new Vector3(size.x * 0.9f, 0.7f, size.y * 0.9f);

            return root.AddComponent<FallingTile>();
        }

        /// <summary>Speed pad that pushes the player along +Z. topCenter is the floor position under the pad.</summary>
        public static BoostPad BuildBoostPad(Transform parent, string name, Vector3 topCenter, float width, float length,
                                             float speed, float duration)
        {
            GameObject root = Group(name, parent);
            root.transform.position = topCenter;
            Transform t = root.transform;

            GameObject quad = Prim(PrimitiveType.Quad, "Chevrons", t, new Vector3(0f, 0.04f, 0f), new Vector3(width, length, 1f), chevronMat, false, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "EdgeL", t, new Vector3(-width * 0.5f, 0.05f, 0f), new Vector3(0.18f, 0.06f, length), glowGold, false, false);
            Prim(PrimitiveType.Cube, "EdgeR", t, new Vector3(width * 0.5f, 0.05f, 0f), new Vector3(0.18f, 0.06f, length), glowGold, false, false);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.1f, 0f);
            trigger.size = new Vector3(width, 2.2f, length);

            BoostPad pad = root.AddComponent<BoostPad>();
            pad.Configure(speed, duration);
            return pad;
        }

        public static void BuildCoin(Transform parent, Vector3 position)
        {
            GameObject root = Group("Coin", parent);
            root.transform.position = position;

            Transform spin = Group("Spin", root.transform).transform;
            GameObject disc = Prim(PrimitiveType.Cylinder, "Disc", spin, Vector3.zero, new Vector3(0.75f, 0.05f, 0.75f), glowGold, false, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;

            root.AddComponent<CoinPickup>().Bind(spin, 5);
        }

        public static void BuildCoinLine(Transform parent, Vector3 from, Vector3 to, int count)
        {
            Transform group = Group("Coins", parent).transform;
            for (int i = 0; i < count; i++)
            {
                float u = count <= 1 ? 0f : i / (float)(count - 1);
                BuildCoin(group, Vector3.Lerp(from, to, u));
            }
        }

        /// <summary>Falling-hazard zone. center is the world position at floor level.</summary>
        public static MeteorShower BuildMeteorZone(Transform parent, string name, Vector3 center, Vector2 size, int count)
        {
            GameObject go = Group(name, parent);
            MeteorShower shower = go.AddComponent<MeteorShower>();
            shower.Bind(center, size, count, hazardBody, glowMagenta);
            return shower;
        }

        // ------------------------------------------------------------------ scene assembly

        public static void MarkStaticSafe(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform tr = all[i];
                if (tr.GetComponentInParent<ArenaMover>() != null) continue;
                if (tr.GetComponentInParent<Checkpoint>() != null) continue;
                if (tr.GetComponentInParent<FallingTile>() != null) continue;
                if (tr.GetComponentInParent<Pendulum>() != null) continue;
                if (tr.GetComponentInParent<PulseGate>() != null) continue;
                if (tr.GetComponentInParent<CoinPickup>() != null) continue;
                if (tr.GetComponentInParent<MeteorShower>() != null) continue;
                if (tr.GetComponentInParent<BoostPad>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>
        /// Builds, saves and registers a complete arena scene. Returns the scene path (or null if cancelled).
        /// An existing scene with the same name is never overwritten.
        /// </summary>
        public static string Assemble(ArenaInfo info, ArenaTheme theme,
                                      System.Func<Transform, List<Checkpoint>, Transform> buildCourse,
                                      System.Action<Transform> buildSigns,
                                      Vector3 courseStart, Vector3 courseEnd, int seed)
        {
            string scenePath = "Assets/Scenes/" + info.SceneName + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                AddToBuildSettings(scenePath);
                Debug.Log("[CLASHDASH] " + info.Title + " already exists at " + scenePath + " - left untouched.");
                return scenePath;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("CLASHDASH", "Stop Play mode before building the arena.", "OK");
                return null;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;

            EnsureFolder(GenRoot + "/Materials");
            EnsureFolder(GenRoot + "/Meshes");
            EnsureFolder(GenRoot + "/Textures");
            EnsureFolder("Assets/Scenes");

            Begin(theme, seed);
            CurrentTitle = info.Title;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildThemedLighting(theme);

            Transform arenaRoot = Group("Arena").transform;
            Transform envRoot = Group("Environment").transform;
            Transform signRoot = Group("Signage").transform;

            List<Checkpoint> checkpoints = new List<Checkpoint>();
            Transform startPoint = buildCourse(arenaRoot, checkpoints);
            BuildThemedEnvironment(envRoot, courseStart, courseEnd);
            buildSigns(signRoot);

            MarkStaticSafe(arenaRoot);
            MarkStaticSafe(envRoot);

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
            manager.Bind(player, startPoint, checkpoints.ToArray(), timerText, statsText, bannerText, resultPanel, resultText, info.ParTime);
            UnityEventTools.AddPersistentListener(restartButton.onClick, manager.Restart);

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty idProp = so.FindProperty("arenaId");
            if (idProp != null) idProp.stringValue = info.Id;
            SerializedProperty titleProp = so.FindProperty("arenaTitle");
            if (titleProp != null) titleProp.stringValue = info.Title;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = gmGo;
            Debug.Log("[CLASHDASH] " + info.Title + " built and saved to " + scenePath + ". Press Play.");
            return scenePath;
        }

        // ------------------------------------------------------------------ shared helpers (mirrors of ArenaOneBuilder's private helpers)

        public static void BuildFinishGate(Transform root, Vector3 basePos)
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

        public static void Deck(Transform parent, string name, float cx, float cz, float width, float length, float topY)
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

        public static GameObject Platform(Transform parent, string name, Vector3 topCenter, Vector2 size, Vector3 travel, float period, float phase)
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

        public static GameObject SpinBar(Transform parent, string name, Vector3 center, float length, float dps, float thickness)
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

        public static GameObject SlideWall(Transform parent, string name, Vector3 center, Vector3 size, Vector3 travel, float period, float phase)
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

        public static GameObject SlamBlock(Transform parent, string name, Vector3 center, Vector3 size, float drop, float phase)
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

        public static Checkpoint MakeCheckpoint(Transform parent, int index, Vector3 pos, float width)
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

        public static GameObject Sign(Transform parent, string name, string text, Vector3 localPos, Vector3 euler,
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

        public static void Tower(Transform parent, string name, Vector3 center, float w, float h, bool halo)
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

        public static void Bridge(Transform parent, string name, Vector3 center, float length)
        {
            Transform t = Group(name, parent).transform;
            t.position = center;
            Prim(PrimitiveType.Cube, "Span", t, Vector3.zero, new Vector3(length, 2.5f, 7f), tower);
            Prim(PrimitiveType.Cube, "GlowL", t, new Vector3(0f, 1.3f, -3.2f), new Vector3(length, 0.15f, 0.25f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "GlowR", t, new Vector3(0f, 1.3f, 3.2f), new Vector3(length, 0.15f, 0.25f), glowCyan, false, false);
            Prim(PrimitiveType.Cube, "Under", t, new Vector3(0f, -1.28f, 0f), new Vector3(length * 0.98f, 0.1f, 3f), glowViolet, false, false);
        }

        public static void Island(Transform parent, string name, Vector3 pos, float radius, int seed)
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

        public static void BuildParticles(Transform root)
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

        public static GameObject Ring(Transform parent, string name, Vector3 worldCenter, Vector3 euler, float radius, float tube,
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

        public static PlayerController BuildPlayer(Vector3 spawn, out Transform visualRoot)
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

        public static void BuildHud(out MobileJoystick joystick, out MobileJumpButton jump, out Text timer, out Text stats,
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
            UiText(labelRt, CurrentTitle, 28, TextAnchor.MiddleCenter, CyanText, true);

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

        public static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
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

        public static Text UiText(RectTransform rt, string content, int size, TextAnchor alignment, Color color, bool outline)
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

        public static GameObject Group(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
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

        public static Mesh Torus(float radius, float tube)
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

        public static Mesh RockMesh(int variant)
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

        public static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int s = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            tris.Add(s);
            tris.Add(s + 1);
            tris.Add(s + 2);
        }

        public static Material Mat(string name, Color color, Color emission, float smoothness, float metallic)
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

        public static Material SpriteMat(string name, Texture2D texture)
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

        public static Shader LitShader()
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

        public static Texture2D SoftGlowTexture()
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

        public static float Rand(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        public static Font LoadUiFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void AddTriggerBody(GameObject go)
        {
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public static void AddToBuildSettings(string scenePath)
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
