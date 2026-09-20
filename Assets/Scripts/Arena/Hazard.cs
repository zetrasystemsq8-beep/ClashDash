using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    public enum HazardMode
    {
        /// <summary>Adds a time penalty, breaks combo and knocks the player back.</summary>
        Penalty,
        /// <summary>Sends the player back to the last checkpoint (void zones).</summary>
        Respawn
    }

    /// <summary>
    /// Put on an object that has a trigger collider (a solid collider can sit alongside it).
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        [SerializeField] private HazardMode mode = HazardMode.Penalty;
        [SerializeField] private float penaltySeconds = 2f;
        [SerializeField] private float knockbackForce = 12f;
        [SerializeField] private float launchUp = 6f;
        [Tooltip("Knock the player back toward the start of the arena (-Z) instead of away from this object's centre.")]
        [SerializeField] private bool pushBackAlongCorridor;
        [SerializeField] private string label = "HIT";

        public UnityEvent onHitPlayer = new UnityEvent();

        private Collider cachedCollider;
        private PlayerController cachedPlayer;

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (other != cachedCollider)
            {
                cachedCollider = other;
                cachedPlayer = other.GetComponent<PlayerController>();
            }

            if (cachedPlayer == null) return;

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.State != GameState.Running) return;

            if (mode == HazardMode.Respawn)
            {
                if (manager != null) manager.HandlePlayerFell();
                return;
            }

            Vector3 away = pushBackAlongCorridor
                ? Vector3.back
                : cachedPlayer.transform.position - transform.position;

            if (cachedPlayer.TakeHit(away, knockbackForce, launchUp))
            {
                if (manager != null) manager.RegisterMistake(penaltySeconds, label);
                onHitPlayer.Invoke();
            }
        }

        public void Configure(HazardMode hazardMode, float penalty, float knockback, float launch, bool pushBack, string hitLabel)
        {
            mode = hazardMode;
            penaltySeconds = penalty;
            knockbackForce = knockback;
            launchUp = launch;
            pushBackAlongCorridor = pushBack;
            label = hitLabel;
        }
    }
}
