using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Keeps the world alive: the sun slowly drifts and warms/cools, fog breathes, and the whole atmosphere
    /// tints briefly on hits, checkpoints and finish. Attached automatically by ClashDashExtrasBootstrap.
    /// </summary>
    public class WorldAtmosphere : MonoBehaviour
    {
        [SerializeField] private float sweepDegrees = 14f;
        [SerializeField] private float sweepPeriod = 90f;
        [SerializeField] private float fogBreath = 0.08f;

        private static readonly Color WarmSun = new Color(1f, 0.82f, 0.62f, 1f);

        private Light sun;
        private Quaternion sunBaseRotation;
        private Color sunBaseColor;
        private Color fogBase;
        private Color ambientSkyBase;
        private Color ambientEquatorBase;
        private float fogDensityBase;

        private Color tintColor = Color.white;
        private float tintAmount;

        private GameManager gm;
        private PlayerController player;
        private UnityAction onHit;
        private UnityAction onCheckpoint;
        private UnityAction onFinished;

        public void Bind(GameManager manager, PlayerController playerController)
        {
            gm = manager;
            player = playerController;

            sun = RenderSettings.sun;
            if (sun != null)
            {
                sunBaseRotation = sun.transform.rotation;
                sunBaseColor = sun.color;
            }

            fogBase = RenderSettings.fogColor;
            fogDensityBase = RenderSettings.fogDensity;
            ambientSkyBase = RenderSettings.ambientSkyColor;
            ambientEquatorBase = RenderSettings.ambientEquatorColor;

            onHit = () => Tint(new Color(1f, 0.3f, 0.45f), 0.35f);
            onCheckpoint = () => Tint(new Color(0.4f, 1f, 0.8f), 0.25f);
            onFinished = () => Tint(new Color(1f, 0.85f, 0.45f), 0.5f);

            if (player != null) player.onHit.AddListener(onHit);
            if (gm != null)
            {
                gm.onCheckpointReached.AddListener(onCheckpoint);
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void Tint(Color color, float amount)
        {
            tintColor = color;
            tintAmount = Mathf.Max(tintAmount, amount);
        }

        private void Update()
        {
            float phase = Time.time * Mathf.PI * 2f / Mathf.Max(1f, sweepPeriod);

            if (sun != null)
            {
                float angle = Mathf.Sin(phase) * sweepDegrees;
                sun.transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * sunBaseRotation;
                sun.color = Color.Lerp(sunBaseColor, WarmSun, 0.5f + 0.5f * Mathf.Sin(phase * 0.7f)) ;
            }

            tintAmount = Mathf.MoveTowards(tintAmount, 0f, Time.deltaTime * 0.8f);

            RenderSettings.fogColor = Color.Lerp(fogBase, tintColor, tintAmount);
            RenderSettings.ambientSkyColor = Color.Lerp(ambientSkyBase, tintColor, tintAmount * 0.6f);
            RenderSettings.ambientEquatorColor = Color.Lerp(ambientEquatorBase, tintColor, tintAmount * 0.4f);
            RenderSettings.fogDensity = fogDensityBase * (1f + Mathf.Sin(phase * 3f) * fogBreath);
        }

        private void OnDestroy()
        {
            if (player != null && onHit != null) player.onHit.RemoveListener(onHit);
            if (gm != null && onCheckpoint != null)
            {
                gm.onCheckpointReached.RemoveListener(onCheckpoint);
                gm.onFinished.RemoveListener(onFinished);
            }
        }
    }
}
