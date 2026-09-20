using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>Runtime-generated soft textures (no image assets needed).</summary>
    internal static class GlowTexture
    {
        private static Texture2D glow;
        private static Texture2D vignette;

        public static Texture2D Glow()
        {
            if (glow != null) return glow;

            const int n = 64;
            glow = new Texture2D(n, n, TextureFormat.RGBA32, false);
            glow.hideFlags = HideFlags.HideAndDontSave;
            glow.wrapMode = TextureWrapMode.Clamp;
            glow.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            glow.SetPixels32(pixels);
            glow.Apply(false, false);
            return glow;
        }

        public static Texture2D Vignette()
        {
            if (vignette != null) return vignette;

            const int n = 128;
            vignette = new Texture2D(n, n, TextureFormat.RGBA32, false);
            vignette.hideFlags = HideFlags.HideAndDontSave;
            vignette.wrapMode = TextureWrapMode.Clamp;
            vignette.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0.5f, 1.25f, d);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            vignette.SetPixels32(pixels);
            vignette.Apply(false, false);
            return vignette;
        }
    }

    /// <summary>
    /// Emits pooled particle bursts in response to gameplay events. Attached automatically by ClashDashBootstrap.
    /// Systems are created once; bursts use ParticleSystem.Emit so nothing is allocated during play.
    /// </summary>
    public class ArenaVfx : MonoBehaviour
    {
        private enum Kind
        {
            Puff,
            Dust,
            Hit,
            Checkpoint,
            Finish,
            Respawn
        }

        private ParticleSystem[] systems;
        private Material material;
        private GameManager gm;
        private PlayerController player;

        private UnityAction onJump;
        private UnityAction onLand;
        private UnityAction onHit;
        private UnityAction onRespawn;
        private UnityAction onCheckpoint;
        private UnityAction onFinished;

        public void Bind(GameManager manager, PlayerController playerController)
        {
            gm = manager;
            player = playerController;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                enabled = false;
                return;
            }

            material = new Material(shader);
            material.mainTexture = GlowTexture.Glow();

            systems = new ParticleSystem[6];
            systems[(int)Kind.Puff] = Make("Puff", new Color(0.7f, 0.95f, 1f, 0.8f), new Color(1f, 1f, 1f, 0.6f), 0.25f, 0.5f, 1f, 2.2f, 0.3f, 0.5f, -0.05f, 40, true);
            systems[(int)Kind.Dust] = Make("Dust", new Color(0.8f, 0.9f, 1f, 0.7f), new Color(1f, 1f, 1f, 0.5f), 0.35f, 0.8f, 2f, 4f, 0.4f, 0.7f, -0.05f, 60, true);
            systems[(int)Kind.Hit] = Make("Hit", new Color(1f, 0.2f, 0.6f, 1f), new Color(1f, 0.65f, 0.2f, 1f), 0.18f, 0.45f, 5f, 10f, 0.45f, 0.9f, 0.6f, 120, false);
            systems[(int)Kind.Checkpoint] = Make("Checkpoint", new Color(0.4f, 1f, 0.8f, 1f), new Color(0.5f, 0.9f, 1f, 1f), 0.2f, 0.5f, 3f, 7f, 0.7f, 1.2f, -0.25f, 150, false);
            systems[(int)Kind.Finish] = Make("Finish", new Color(1f, 0.85f, 0.4f, 1f), new Color(0.5f, 0.95f, 1f, 1f), 0.25f, 0.7f, 7f, 16f, 1.2f, 2.2f, 0.3f, 400, false);
            systems[(int)Kind.Respawn] = Make("Respawn", new Color(0.5f, 0.95f, 1f, 1f), new Color(1f, 1f, 1f, 1f), 0.2f, 0.5f, 2f, 5f, 0.4f, 0.7f, -0.1f, 80, false);

            onJump = () => Burst(Kind.Puff, FeetPosition(), 8);
            onLand = () => Burst(Kind.Dust, FeetPosition(), 14);
            onHit = () => Burst(Kind.Hit, BodyPosition(), 40);
            onRespawn = () => Burst(Kind.Respawn, BodyPosition(), 30);
            onCheckpoint = () => Burst(Kind.Checkpoint, BodyPosition(), 60);
            onFinished = () => Burst(Kind.Finish, BodyPosition() + Vector3.up * 0.5f, 250);

            if (player != null)
            {
                player.onJump.AddListener(onJump);
                player.onLand.AddListener(onLand);
                player.onHit.AddListener(onHit);
                player.onRespawn.AddListener(onRespawn);
            }

            if (gm != null)
            {
                gm.onCheckpointReached.AddListener(onCheckpoint);
                gm.onFinished.AddListener(onFinished);
            }
        }

        private Vector3 FeetPosition()
        {
            return player != null ? player.transform.position + Vector3.up * 0.1f : Vector3.zero;
        }

        private Vector3 BodyPosition()
        {
            return player != null ? player.transform.position + Vector3.up * 1f : Vector3.zero;
        }

        private void Burst(Kind kind, Vector3 position, int count)
        {
            if (systems == null) return;
            ParticleSystem ps = systems[(int)kind];
            if (ps == null) return;

            ps.transform.position = position;
            ps.Emit(count);
        }

        private ParticleSystem Make(string label, Color colorA, Color colorB, float sizeMin, float sizeMax,
                                    float speedMin, float speedMax, float lifeMin, float lifeMax,
                                    float gravity, int maxParticles, bool ring)
        {
            GameObject go = new GameObject("VFX_" + label);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false; // bursts are emitted from script

            ParticleSystem.ShapeModule shape = ps.shape;
            if (ring)
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                shape.radiusThickness = 0f;
                shape.rotation = new Vector3(90f, 0f, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.25f;
            }

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = material;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;

            return ps;
        }

        private void OnDestroy()
        {
            if (player != null && onJump != null)
            {
                player.onJump.RemoveListener(onJump);
                player.onLand.RemoveListener(onLand);
                player.onHit.RemoveListener(onHit);
                player.onRespawn.RemoveListener(onRespawn);
            }

            if (gm != null && onCheckpoint != null)
            {
                gm.onCheckpointReached.RemoveListener(onCheckpoint);
                gm.onFinished.RemoveListener(onFinished);
            }

            if (material != null) Destroy(material);
        }
    }
}
