using UnityEngine;

namespace Zetra.ClashDash
{
    public enum ArenaMoveMode
    {
        Rotate,
        PingPong,
        Slam
    }

    /// <summary>
    /// Drives rotating bars, sliding walls, floating platforms, crushers and ambient machinery.
    /// Runs before the player so LastDelta can carry the player on moving platforms.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class ArenaMover : MonoBehaviour
    {
        [SerializeField] private ArenaMoveMode mode = ArenaMoveMode.PingPong;

        [Header("Rotate")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 90f;

        [Header("PingPong / Slam")]
        [SerializeField] private Vector3 travel = new Vector3(6f, 0f, 0f);
        [SerializeField] private float period = 4f;
        [SerializeField, Range(0f, 1f)] private float phase;

        [Header("Slam timing (seconds)")]
        [SerializeField] private float pauseTime = 1.2f;
        [SerializeField] private float dropTime = 0.3f;
        [SerializeField] private float restTime = 0.4f;
        [SerializeField] private float returnTime = 1.2f;
        [SerializeField] private float telegraphTime = 0.45f;

        [Header("Physics")]
        [SerializeField] private bool kinematicBody = true;

        public ArenaMoveMode Mode => mode;

        /// <summary>World-space movement applied during the most recent frame.</summary>
        public Vector3 LastDelta { get; private set; }

        private Vector3 origin;

        private void Awake()
        {
            origin = transform.position;

            if (kinematicBody)
            {
                Rigidbody body;
                if (!TryGetComponent(out body))
                {
                    body = gameObject.AddComponent<Rigidbody>();
                }
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void Update()
        {
            switch (mode)
            {
                case ArenaMoveMode.Rotate:
                    transform.Rotate(rotationAxis, degreesPerSecond * Time.deltaTime, Space.Self);
                    LastDelta = Vector3.zero;
                    break;

                case ArenaMoveMode.PingPong:
                {
                    float t = Time.time / Mathf.Max(0.01f, period) + phase;
                    float k = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
                    ApplyPosition(origin + travel * k);
                    break;
                }

                case ArenaMoveMode.Slam:
                {
                    float cycle = pauseTime + dropTime + restTime + returnTime;
                    float t = Mathf.Repeat(Time.time + phase * cycle, cycle);
                    float d;
                    float shake = 0f;

                    if (t < pauseTime)
                    {
                        d = 0f;
                        float warnStart = pauseTime - telegraphTime;
                        if (t > warnStart)
                        {
                            shake = Mathf.Sin(Time.time * 90f) * 0.07f;
                        }
                    }
                    else if (t < pauseTime + dropTime)
                    {
                        float u = (t - pauseTime) / Mathf.Max(0.01f, dropTime);
                        d = u * u;
                    }
                    else if (t < pauseTime + dropTime + restTime)
                    {
                        d = 1f;
                    }
                    else
                    {
                        float u = (t - pauseTime - dropTime - restTime) / Mathf.Max(0.01f, returnTime);
                        d = 1f - Mathf.SmoothStep(0f, 1f, u);
                    }

                    ApplyPosition(origin + travel * d + new Vector3(0f, shake, 0f));
                    break;
                }
            }
        }

        private void ApplyPosition(Vector3 target)
        {
            LastDelta = target - transform.position;
            transform.position = target;
        }

        // ---- Authoring helpers (used by the Editor builder) ----

        public void ConfigureRotate(Vector3 axis, float dps, bool physicsBody = true)
        {
            mode = ArenaMoveMode.Rotate;
            rotationAxis = axis;
            degreesPerSecond = dps;
            kinematicBody = physicsBody;
        }

        public void ConfigurePingPong(Vector3 travelVector, float periodSeconds, float phase01, bool physicsBody = true)
        {
            mode = ArenaMoveMode.PingPong;
            travel = travelVector;
            period = periodSeconds;
            phase = Mathf.Repeat(phase01, 1f);
            kinematicBody = physicsBody;
        }

        public void ConfigureSlam(Vector3 travelVector, float pause, float drop, float rest, float ret, float phase01)
        {
            mode = ArenaMoveMode.Slam;
            travel = travelVector;
            pauseTime = pause;
            dropTime = drop;
            restTime = rest;
            returnTime = ret;
            phase = Mathf.Repeat(phase01, 1f);
            kinematicBody = true;
        }
    }
}
