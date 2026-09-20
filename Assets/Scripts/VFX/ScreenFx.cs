using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Screen-space feedback: colour flashes, vignette pulse, warp-speed streaks around the screen edge and
    /// a toast line for messages such as XP gained and level ups. Attached automatically by ClashDashBootstrap.
    /// </summary>
    public class ScreenFx : MonoBehaviour
    {
        private static readonly Color BaseVignetteColor = new Color(0.4f, 0.8f, 1f, 1f);
        private const float BaseVignetteAlpha = 0.16f;

        private Image flash;
        private Image vignette;
        private Text toast;
        private ParticleSystem streaks;
        private Material streakMaterial;

        private GameManager gm;
        private PlayerController player;

        private Color flashColor;
        private float flashAlpha;
        private Color pulseColor = BaseVignetteColor;
        private float pulseAlpha;
        private float toastTimer;

        private UnityAction onHit;
        private UnityAction onRespawn;
        private UnityAction onCheckpoint;
        private UnityAction onRunStarted;
        private UnityAction onFinished;

        public void Bind(GameManager manager, PlayerController playerController, Camera cam)
        {
            gm = manager;
            player = playerController;

            Canvas canvas = ClashUi.CreateCanvas("ScreenFx", 5, transform);
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster); // effects must never block touches

            // Vignette
            RectTransform vignetteRt = ClashUi.Stretch("Vignette", canvas.transform);
            vignette = vignetteRt.gameObject.AddComponent<Image>();
            Texture2D vt = GlowTexture.Vignette();
            vignette.sprite = Sprite.Create(vt, new Rect(0f, 0f, vt.width, vt.height), new Vector2(0.5f, 0.5f), 100f);
            vignette.raycastTarget = false;
            vignette.color = new Color(BaseVignetteColor.r, BaseVignetteColor.g, BaseVignetteColor.b, BaseVignetteAlpha);

            // Flash
            RectTransform flashRt = ClashUi.Stretch("Flash", canvas.transform);
            flash = flashRt.gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.color = new Color(1f, 1f, 1f, 0f);

            // Toast
            RectTransform toastRt = ClashUi.Rect("Toast", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                 new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1500f, 90f));
            toast = ClashUi.Label(toastRt, string.Empty, 54, TextAnchor.MiddleCenter, ClashUi.Gold, true);

            CreateStreaks(cam);

            onHit = () => { Flash(new Color(1f, 0.15f, 0.4f), 0.5f); Pulse(new Color(1f, 0.2f, 0.4f), 0.55f); };
            onRespawn = () => Flash(new Color(0.4f, 0.9f, 1f), 0.35f);
            onCheckpoint = () => { Flash(new Color(0.4f, 1f, 0.75f), 0.22f); Pulse(new Color(0.4f, 1f, 0.75f), 0.3f); };
            onRunStarted = () => Flash(Color.white, 0.35f);
            onFinished = () => { Flash(new Color(1f, 0.85f, 0.4f), 0.5f); Pulse(new Color(1f, 0.85f, 0.4f), 0.4f); };

            if (player != null)
            {
                player.onHit.AddListener(onHit);
                player.onRespawn.AddListener(onRespawn);
            }

            if (gm != null)
            {
                gm.onCheckpointReached.AddListener(onCheckpoint);
                gm.onRunStarted.AddListener(onRunStarted);
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void CreateStreaks(Camera cam)
        {
            if (cam == null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            streakMaterial = new Material(shader);
            streakMaterial.mainTexture = GlowTexture.Glow();

            GameObject go = new GameObject("SpeedStreaks");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 14f);
            go.transform.localRotation = Quaternion.identity;

            streaks = go.AddComponent<ParticleSystem>();
            streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = streaks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(0.8f, 0.95f, 1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = streaks.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 9f;
            shape.radiusThickness = 0.35f;

            ParticleSystem.VelocityOverLifetimeModule velocity = streaks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-55f);

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.04f;
            pr.lengthScale = 2f;
            pr.sharedMaterial = streakMaterial;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;

            streaks.Play();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.4f);
                Color c = flashColor;
                c.a = flashAlpha;
                flash.color = c;
            }

            if (pulseAlpha > 0f)
            {
                pulseAlpha = Mathf.MoveTowards(pulseAlpha, 0f, dt * 1.6f);
            }
            if (vignette != null)
            {
                float k = pulseAlpha > 0f ? Mathf.Clamp01(pulseAlpha / 0.6f) : 0f;
                Color c = Color.Lerp(BaseVignetteColor, pulseColor, k);
                c.a = BaseVignetteAlpha + pulseAlpha;
                vignette.color = c;
            }

            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                Color c = ClashUi.Gold;
                c.a = Mathf.Clamp01(toastTimer / 0.6f);
                toast.color = c;
                if (toastTimer <= 0f) toast.text = string.Empty;
            }

            if (streaks != null && player != null)
            {
                float amount = Mathf.InverseLerp(0.55f, 1f, player.PlanarSpeed01);
                ParticleSystem.EmissionModule emission = streaks.emission;
                emission.rateOverTime = amount * 60f;
            }
        }

        // ---- Public API ----

        public void ShowToast(string text, float seconds)
        {
            if (toast == null) return;
            toast.text = text;
            toast.color = ClashUi.Gold;
            toastTimer = Mathf.Max(0.1f, seconds);
        }

        private void Flash(Color color, float alpha)
        {
            flashColor = color;
            flashAlpha = Mathf.Max(flashAlpha, alpha);
        }

        private void Pulse(Color color, float alpha)
        {
            pulseColor = color;
            pulseAlpha = Mathf.Max(pulseAlpha, alpha);
        }

        private void OnDestroy()
        {
            if (player != null && onHit != null)
            {
                player.onHit.RemoveListener(onHit);
                player.onRespawn.RemoveListener(onRespawn);
            }

            if (gm != null && onCheckpoint != null)
            {
                gm.onCheckpointReached.RemoveListener(onCheckpoint);
                gm.onRunStarted.RemoveListener(onRunStarted);
                gm.onFinished.RemoveListener(onFinished);
            }

            if (streakMaterial != null) Destroy(streakMaterial);
        }
    }
}
