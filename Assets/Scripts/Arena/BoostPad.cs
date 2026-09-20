using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Trigger pad that launches the player forward (along this object's +Z axis) with a short speed boost.
    /// </summary>
    public class BoostPad : MonoBehaviour
    {
        [SerializeField] private float boostSpeed = 14f;
        [SerializeField] private float boostDuration = 1.2f;
        [SerializeField] private float cooldown = 0.35f;

        private static AudioClip whoosh;

        private ThirdPersonCamera cameraRig;
        private float nextTime;

        private void Awake()
        {
            Rigidbody body;
            if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextTime) return;
            if (other.GetComponent<PlayerController>() == null) return;

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.State != GameState.Running) return;

            nextTime = Time.time + cooldown;

            PlayerBoost boost = other.GetComponent<PlayerBoost>();
            if (boost == null) boost = other.gameObject.AddComponent<PlayerBoost>();
            boost.Boost(transform.forward, boostSpeed, boostDuration);

            if (cameraRig == null && Camera.main != null) cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            if (cameraRig != null) cameraRig.Shake(0.16f, 0.3f);

            if (whoosh == null) whoosh = BuildWhoosh();
            Camera cam = Camera.main;
            AudioSource.PlayClipAtPoint(whoosh, cam != null ? cam.transform.position : transform.position, 0.7f);
        }

        private static AudioClip BuildWhoosh()
        {
            const int rate = 44100;
            int count = (int)(rate * 0.4f);
            float[] data = new float[count];
            System.Random rng = new System.Random(77);
            double phase = 0.0;
            float lowpass = 0f;

            for (int i = 0; i < count; i++)
            {
                float u = i / (float)count;
                float env = Mathf.Sin(u * Mathf.PI) * Mathf.Min(1f, i / 200f);
                double freq = 220.0 + 900.0 * u * u;
                phase += 2.0 * System.Math.PI * freq / rate;
                phase %= 2.0 * System.Math.PI;

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpass += (white - lowpass) * (0.05f + 0.3f * u);
                data[i] = ((float)System.Math.Sin(phase) * 0.35f + lowpass * 0.6f) * env;
            }

            AudioClip clip = AudioClip.Create("CD_BoostWhoosh", count, 1, rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        public void Configure(float speed, float duration)
        {
            boostSpeed = speed;
            boostDuration = duration;
        }
    }
}
