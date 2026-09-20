using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Adds a decaying extra push to the player's CharacterController (used by boost pads).
    /// Added to the player automatically the first time a BoostPad is touched.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class PlayerBoost : MonoBehaviour
    {
        private CharacterController controller;
        private PlayerController player;
        private Vector3 direction = Vector3.forward;
        private float speed;
        private float duration = 1f;
        private float timer;

        public bool IsBoosting => timer > 0f;
        public float Strength01 => duration > 0f ? Mathf.Clamp01(timer / duration) : 0f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            player = GetComponent<PlayerController>();

            if (player != null)
            {
                player.onHit.AddListener(Cancel);
                player.onRespawn.AddListener(Cancel);
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.onHit.RemoveListener(Cancel);
                player.onRespawn.RemoveListener(Cancel);
            }
        }

        public void Boost(Vector3 boostDirection, float boostSpeed, float boostDuration)
        {
            boostDirection.y = 0f;
            if (boostDirection.sqrMagnitude < 0.001f) boostDirection = transform.forward;

            direction = boostDirection.normalized;
            speed = boostSpeed;
            duration = Mathf.Max(0.05f, boostDuration);
            timer = duration;
        }

        public void Cancel()
        {
            timer = 0f;
        }

        private void Update()
        {
            if (timer <= 0f || controller == null || !controller.enabled) return;

            if (player != null && player.InputLocked)
            {
                timer = 0f;
                return;
            }

            float dt = Time.deltaTime;
            timer -= dt;

            Vector3 motion = direction * (speed * Strength01 * dt);

            // Keep the CharacterController's ground detection alive while boosting on the floor.
            if (player != null && player.IsGrounded) motion += Vector3.down * 0.06f;

            controller.Move(motion);
        }
    }
}
