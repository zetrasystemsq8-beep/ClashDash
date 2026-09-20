using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Smooth third-person follow camera with speed-reactive FOV, banking roll, look-ahead,
    /// light obstacle avoidance and shake. Runs in LateUpdate.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerController player;

        [Header("Framing")]
        [SerializeField] private float yaw;
        [SerializeField] private float distance = 8.5f;
        [SerializeField] private float height = 4.2f;
        [SerializeField] private float lookHeight = 1.6f;
        [SerializeField] private float lookAheadSeconds = 0.22f;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothTime = 0.16f;
        [SerializeField] private float verticalSmoothTime = 0.32f;
        [SerializeField] private float rotationSharpness = 9f;

        [Header("Speed effects")]
        [SerializeField] private float baseFov = 62f;
        [SerializeField] private float maxFovBoost = 12f;
        [SerializeField] private float fovSharpness = 5f;
        [SerializeField] private float maxRoll = 3.5f;
        [SerializeField] private float rollSharpness = 6f;

        [Header("Obstacle avoidance")]
        [SerializeField] private bool avoidObstacles = true;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.35f;
        [SerializeField] private float minDistance = 2.5f;

        private Camera cam;
        private Vector3 smoothedPosition;
        private Vector3 positionVelocity;
        private Quaternion smoothedRotation = Quaternion.identity;
        private float focusY;
        private float focusYVelocity;
        private float roll;

        private float shakeTime;
        private float shakeDuration = 0.01f;
        private float shakeMagnitude;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Solve(Time.deltaTime, false);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            if (cam == null) cam = GetComponent<Camera>();
            Solve(0f, true);
        }

        private void Solve(float dt, bool snap)
        {
            Vector3 targetPos = target.position;
            Vector3 planarVelocity = player != null ? player.PlanarVelocity : Vector3.zero;
            float speed01 = player != null ? player.PlanarSpeed01 : 0f;

            // Vertical follow is slower so jumps don't whip the camera around.
            if (snap)
            {
                focusY = targetPos.y;
                focusYVelocity = 0f;
            }
            else
            {
                focusY = Mathf.SmoothDamp(focusY, targetPos.y, ref focusYVelocity, verticalSmoothTime);
            }

            Vector3 focus = new Vector3(targetPos.x, focusY, targetPos.z);
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 lookOrigin = focus + Vector3.up * lookHeight;
            Vector3 desired = focus + yawRotation * new Vector3(0f, height, -distance);

            if (avoidObstacles)
            {
                Vector3 toCamera = desired - lookOrigin;
                float length = toCamera.magnitude;
                if (length > 0.01f)
                {
                    Vector3 dir = toCamera / length;
                    if (Physics.SphereCast(lookOrigin, collisionRadius, dir, out RaycastHit hit, length, collisionMask, QueryTriggerInteraction.Ignore))
                    {
                        desired = lookOrigin + dir * Mathf.Max(minDistance, hit.distance);
                    }
                }
            }

            if (snap)
            {
                smoothedPosition = desired;
                positionVelocity = Vector3.zero;
            }
            else
            {
                smoothedPosition = Vector3.SmoothDamp(smoothedPosition, desired, ref positionVelocity, positionSmoothTime);
            }

            Vector3 lookPoint = lookOrigin + planarVelocity * lookAheadSeconds;
            Vector3 lookDir = lookPoint - smoothedPosition;
            Quaternion desiredRotation = lookDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(lookDir) : smoothedRotation;

            smoothedRotation = snap
                ? desiredRotation
                : Quaternion.Slerp(smoothedRotation, desiredRotation, 1f - Mathf.Exp(-rotationSharpness * dt));

            // Banking roll from sideways speed.
            float lateral01 = 0f;
            if (player != null)
            {
                Vector3 right = smoothedRotation * Vector3.right;
                lateral01 = Mathf.Clamp(Vector3.Dot(planarVelocity, right) / Mathf.Max(1f, player.MoveSpeed), -1f, 1f);
            }
            float targetRoll = -lateral01 * maxRoll;
            roll = snap ? 0f : Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-rollSharpness * dt));

            Quaternion finalRotation = smoothedRotation * Quaternion.Euler(0f, 0f, roll);
            Vector3 finalPosition = smoothedPosition;

            // Shake.
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                float fade = Mathf.Clamp01(shakeTime / shakeDuration);
                float t = Time.unscaledTime * 38f;
                float sx = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f;
                float sy = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f;
                finalPosition += finalRotation * new Vector3(sx, sy, 0f) * (shakeMagnitude * fade);
            }

            transform.SetPositionAndRotation(finalPosition, finalRotation);

            if (cam != null)
            {
                float targetFov = baseFov + speed01 * maxFovBoost;
                cam.fieldOfView = snap ? targetFov : Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSharpness * dt));
            }
        }

        // ---- Public API ----

        public void Shake(float magnitude, float duration)
        {
            shakeMagnitude = magnitude;
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeTime = shakeDuration;
        }

        public void ShakeLight()
        {
            Shake(0.12f, 0.2f);
        }

        public void ShakeHeavy()
        {
            Shake(0.4f, 0.45f);
        }

        public void SetYaw(float degrees)
        {
            yaw = degrees;
        }

        public void Bind(Transform followTarget, PlayerController playerController, float yawDegrees)
        {
            target = followTarget;
            player = playerController;
            yaw = yawDegrees;
        }
    }
}
