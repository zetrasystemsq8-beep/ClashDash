using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Trigger volume that sets the player's respawn point and lights up when reached.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private int index = 1;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private Renderer[] indicators;
        [SerializeField] private Transform pulseTarget;

        [Header("Colours")]
        [SerializeField] private Color inactiveColor = new Color(0.25f, 0.55f, 1f, 1f);
        [SerializeField] private Color inactiveEmission = new Color(0.2f, 0.5f, 1.4f, 1f);
        [SerializeField] private Color activeColor = new Color(0.35f, 1f, 0.7f, 1f);
        [SerializeField] private Color activeEmission = new Color(0.5f, 3f, 1.8f, 1f);

        public UnityEvent onActivated = new UnityEvent();

        public int Index => index;
        public bool IsActive { get; private set; }
        public Transform RespawnPoint => respawnPoint != null ? respawnPoint : transform;

        private MaterialPropertyBlock block;
        private Vector3 pulseBaseScale = Vector3.one;
        private float pulseTimer;
        private bool pulsing;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (pulseTarget != null) pulseBaseScale = pulseTarget.localScale;
            ApplyColors(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsActive) return;
            if (other.GetComponent<PlayerController>() == null) return;

            GameManager manager = GameManager.Instance;
            if (manager == null || !manager.TryActivateCheckpoint(this)) return;

            IsActive = true;
            ApplyColors(true);
            pulseTimer = 1f;
            pulsing = true;
            onActivated.Invoke();
        }

        private void Update()
        {
            if (!pulsing) return;

            if (pulseTarget == null)
            {
                pulsing = false;
                return;
            }

            pulseTimer -= Time.deltaTime;
            if (pulseTimer <= 0f)
            {
                pulseTarget.localScale = pulseBaseScale;
                pulsing = false;
                return;
            }

            float k = Mathf.Sin((1f - pulseTimer) * Mathf.PI);
            pulseTarget.localScale = pulseBaseScale * (1f + 0.4f * k);
        }

        private void ApplyColors(bool active)
        {
            if (indicators == null) return;

            Color c = active ? activeColor : inactiveColor;
            Color e = active ? activeEmission : inactiveEmission;

            for (int i = 0; i < indicators.Length; i++)
            {
                Renderer r = indicators[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                block.SetColor(EmissionId, e);
                r.SetPropertyBlock(block);
            }
        }

        public void Bind(int checkpointIndex, Transform respawn, Renderer[] indicatorRenderers, Transform pulse)
        {
            index = checkpointIndex;
            respawnPoint = respawn;
            indicators = indicatorRenderers;
            pulseTarget = pulse;
        }
    }
}
