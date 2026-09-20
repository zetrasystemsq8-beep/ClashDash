using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Collectible coin. Needs a trigger collider (a kinematic Rigidbody is added automatically).
    /// Coins are counted in RunStats and banked when the arena is completed.
    /// </summary>
    public class CoinPickup : MonoBehaviour
    {
        [SerializeField] private int value = 5;
        [SerializeField] private Transform visual;
        [SerializeField] private float spinDegreesPerSecond = 180f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2.5f;

        private static AudioClip chime;

        private Vector3 visualBase;
        private float phase;
        private bool collected;

        private void Awake()
        {
            if (visual != null) visualBase = visual.localPosition;
            phase = Random.value * Mathf.PI * 2f;

            Rigidbody body;
            if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void Update()
        {
            if (collected || visual == null) return;

            float t = Time.time;
            visual.localRotation = Quaternion.Euler(0f, t * spinDegreesPerSecond + phase * 57.29578f, 0f);
            visual.localPosition = visualBase + new Vector3(0f, Mathf.Sin(t * bobSpeed + phase) * bobHeight, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected) return;
            if (other.GetComponent<PlayerController>() == null) return;

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.State != GameState.Running) return;

            collected = true;
            RunStats.AddCoins(value);
            PlayChime();
            gameObject.SetActive(false);
        }

        private void PlayChime()
        {
            if (chime == null) chime = BuildChime();

            Camera cam = Camera.main;
            Vector3 position = cam != null ? cam.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(chime, position, 0.6f);
        }

        private static AudioClip BuildChime()
        {
            const int rate = 44100;
            int count = (int)(rate * 0.3f);
            float[] data = new float[count];
            double phaseA = 0.0;
            double phaseB = 0.0;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 11f) * Mathf.Min(1f, i / 80f);
                float freq = t < 0.07f ? 988f : 1319f;
                phaseA += 2.0 * System.Math.PI * freq / rate;
                phaseB += 2.0 * System.Math.PI * freq * 2.0 / rate;
                float sample = (float)(System.Math.Sin(phaseA) * 0.5 + System.Math.Sin(phaseB) * 0.15);
                data[i] = sample * env * Mathf.Clamp01((count - i) / 200f);
            }

            AudioClip clip = AudioClip.Create("CD_CoinChime", count, 1, rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        public void Bind(Transform visualRoot, int coinValue)
        {
            visual = visualRoot;
            value = coinValue;
        }
    }
}
