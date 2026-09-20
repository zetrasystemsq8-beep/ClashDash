using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Cinematic intro: during the countdown the camera sweeps from the finish gate back to the gameplay view,
    /// blending seamlessly into the follow camera. Runs after ThirdPersonCamera so it can override its pose.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class IntroFlyover : MonoBehaviour
    {
        [SerializeField] private float duration = 3f;

        private GameManager gm;
        private Transform cam;
        private Vector3 finishPosition;
        private float elapsed;
        private bool done;

        public void Bind(GameManager manager, Transform cameraTransform, Transform finishGate)
        {
            gm = manager;
            cam = cameraTransform;
            finishPosition = finishGate != null ? finishGate.position : Vector3.zero;
            done = cam == null || finishGate == null;
        }

        private void LateUpdate()
        {
            if (done || gm == null || cam == null) return;

            if (gm.State != GameState.Countdown)
            {
                done = true;
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            float e = t * t * (3f - 2f * t);

            // The follow camera has already positioned the camera this frame: that pose is the destination.
            Vector3 gameplayPos = cam.position;
            Quaternion gameplayRot = cam.rotation;

            Vector3 start = finishPosition + new Vector3(14f, 20f, -30f);
            Vector3 mid = Vector3.Lerp(start, gameplayPos, 0.5f) + Vector3.up * 25f;
            float inv = 1f - e;
            Vector3 pos = inv * inv * start + 2f * inv * e * mid + e * e * gameplayPos;

            Vector3 finishLook = finishPosition + Vector3.up * 6f;
            Vector3 playerLook = gameplayPos + gameplayRot * Vector3.forward * 10f;
            Vector3 look = Vector3.Lerp(finishLook, playerLook, e);

            Vector3 dir = look - pos;
            Quaternion lookRot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : gameplayRot;

            cam.SetPositionAndRotation(pos, Quaternion.Slerp(lookRot, gameplayRot, e * e));

            if (t >= 1f) done = true;
        }
    }
}
