using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// After the arena is completed the camera eases into a slow orbit around the player.
    /// Runs after ThirdPersonCamera so it can override its pose.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FinishOrbitCamera : MonoBehaviour
    {
        [SerializeField] private float radius = 6.5f;
        [SerializeField] private float height = 3.2f;
        [SerializeField] private float degreesPerSecond = 22f;
        [SerializeField] private float blendTime = 1.4f;

        private GameManager gm;
        private Transform cam;
        private Transform target;
        private UnityAction onFinished;
        private bool active;
        private float elapsed;
        private float angle;

        public void Bind(GameManager manager, Transform cameraTransform, Transform followTarget)
        {
            gm = manager;
            cam = cameraTransform;
            target = followTarget;

            if (gm != null)
            {
                onFinished = BeginOrbit;
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void BeginOrbit()
        {
            if (cam == null || target == null) return;

            Vector3 offset = cam.position - target.position;
            angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            elapsed = 0f;
            active = true;
        }

        private void LateUpdate()
        {
            if (!active || cam == null || target == null) return;

            elapsed += Time.unscaledDeltaTime;
            angle += degreesPerSecond * Time.unscaledDeltaTime;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, blendTime)));

            float rad = angle * Mathf.Deg2Rad;
            Vector3 orbitPos = target.position + new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
            Vector3 pos = Vector3.Lerp(cam.position, orbitPos, blend);

            Vector3 look = target.position + Vector3.up * 1.4f;
            Vector3 dir = look - pos;
            Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : cam.rotation;

            cam.SetPositionAndRotation(pos, Quaternion.Slerp(cam.rotation, rot, blend));
        }

        private void OnDestroy()
        {
            if (gm != null && onFinished != null) gm.onFinished.RemoveListener(onFinished);
        }
    }
}
