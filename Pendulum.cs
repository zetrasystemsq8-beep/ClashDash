using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Swings this object around its local Z axis (a hammer hanging from a pivot). The moving part that hurts the
    /// player is the "head": a child with a trigger collider and a Hazard component.
    /// </summary>
    public class Pendulum : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private float amplitude = 55f;
        [SerializeField] private float period = 3.2f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private Quaternion baseRotation;

        private void Awake()
        {
            baseRotation = transform.localRotation;

            if (head != null)
            {
                // A kinematic body on the moving head keeps trigger events reliable.
                Rigidbody body;
                if (!head.TryGetComponent(out body)) body = head.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void Update()
        {
            float t = Time.time / Mathf.Max(0.1f, period) + phase;
            float angle = Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
        }

        public void Bind(Transform headTransform, float amplitudeDegrees, float periodSeconds, float phase01)
        {
            head = headTransform;
            amplitude = amplitudeDegrees;
            period = periodSeconds;
            phase = Mathf.Repeat(phase01, 1f);
        }
    }
}
