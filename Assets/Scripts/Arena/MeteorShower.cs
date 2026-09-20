using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Falling-hazard zone. Meteors telegraph their landing spot with a growing warning disc, then drop and hit
    /// anything (the player) inside the impact radius. Objects are created once and reused.
    /// </summary>
    public class MeteorShower : MonoBehaviour
    {
        [Header("Zone (world space)")]
        [SerializeField] private Vector3 zoneCenter;
        [SerializeField] private Vector2 zoneSize = new Vector2(14f, 40f);

        [Header("Meteors")]
        [SerializeField] private int meteorCount = 6;
        [SerializeField] private float impactRadius = 1.6f;
        [SerializeField] private float dropHeight = 32f;
        [SerializeField] private float warnTime = 1.1f;
        [SerializeField] private float fallTime = 0.35f;
        [SerializeField] private float impactTime = 0.35f;
        [SerializeField] private float minPause = 0.3f;
        [SerializeField] private float maxPause = 1.6f;

        [Header("Look")]
        [SerializeField] private Material meteorMaterial;
        [SerializeField] private Material warnMaterial;

        [Header("Hazard")]
        [SerializeField] private float penaltySeconds = 2f;
        [SerializeField] private float knockback = 11f;

        private enum Phase
        {
            Waiting,
            Warning,
            Falling,
            Impact
        }

        private sealed class Meteor
        {
            public Transform warn;
            public Transform rock;
            public Collider rockCollider;
            public Phase phase;
            public float timer;
            public Vector3 target;
        }

        private Meteor[] meteors;

        private void Start()
        {
            int count = Mathf.Max(1, meteorCount);
            meteors = new Meteor[count];

            for (int i = 0; i < count; i++)
            {
                Meteor m = new Meteor();

                GameObject warn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                warn.name = "MeteorWarning";
                warn.transform.SetParent(transform, false);
                Destroy(warn.GetComponent<Collider>());
                Renderer wr = warn.GetComponent<Renderer>();
                if (warnMaterial != null) wr.sharedMaterial = warnMaterial;
                wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                warn.SetActive(false);
                m.warn = warn.transform;

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "Meteor";
                rock.transform.SetParent(transform, false);
                rock.transform.localScale = Vector3.one * (impactRadius * 1.4f);
                Renderer rr = rock.GetComponent<Renderer>();
                if (meteorMaterial != null) rr.sharedMaterial = meteorMaterial;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                SphereCollider trigger = rock.GetComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.5f * (impactRadius * 2f) / (impactRadius * 1.4f);

                Rigidbody body = rock.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                Hazard hazard = rock.AddComponent<Hazard>();
                hazard.Configure(HazardMode.Penalty, penaltySeconds, knockback, 6f, false, "METEOR");

                rock.SetActive(false);
                m.rock = rock.transform;
                m.rockCollider = trigger;

                m.phase = Phase.Waiting;
                m.timer = i * (warnTime + maxPause) / count;
                meteors[i] = m;
            }
        }

        private void Update()
        {
            if (meteors == null) return;

            GameManager manager = GameManager.Instance;
            bool active = manager == null || manager.State == GameState.Running;
            float dt = Time.deltaTime;

            for (int i = 0; i < meteors.Length; i++)
            {
                Meteor m = meteors[i];

                // Only run while the arena is live; otherwise clear everything.
                if (!active)
                {
                    if (m.phase != Phase.Waiting) Reset(m);
                    continue;
                }

                m.timer -= dt;

                switch (m.phase)
                {
                    case Phase.Waiting:
                        if (m.timer <= 0f) BeginWarning(m);
                        break;

                    case Phase.Warning:
                    {
                        float u = 1f - Mathf.Clamp01(m.timer / warnTime);
                        float d = Mathf.Lerp(0.4f, impactRadius * 2.6f, u);
                        m.warn.localScale = new Vector3(d, 0.02f, d);
                        if (m.timer <= 0f) BeginFall(m);
                        break;
                    }

                    case Phase.Falling:
                    {
                        float u = 1f - Mathf.Clamp01(m.timer / fallTime);
                        float y = Mathf.Lerp(m.target.y + dropHeight, m.target.y + impactRadius * 0.6f, u * u);
                        m.rock.position = new Vector3(m.target.x, y, m.target.z);
                        if (m.timer <= 0f)
                        {
                            m.phase = Phase.Impact;
                            m.timer = impactTime;
                        }
                        break;
                    }

                    case Phase.Impact:
                        if (m.timer <= 0f)
                        {
                            Reset(m);
                            m.timer = Random.Range(minPause, maxPause);
                        }
                        break;
                }
            }
        }

        private void BeginWarning(Meteor m)
        {
            float x = zoneCenter.x + Random.Range(-zoneSize.x * 0.5f, zoneSize.x * 0.5f);
            float z = zoneCenter.z + Random.Range(-zoneSize.y * 0.5f, zoneSize.y * 0.5f);
            m.target = new Vector3(x, zoneCenter.y, z);

            m.warn.position = new Vector3(x, zoneCenter.y + 0.06f, z);
            m.warn.localScale = new Vector3(0.4f, 0.02f, 0.4f);
            m.warn.gameObject.SetActive(true);

            m.phase = Phase.Warning;
            m.timer = warnTime;
        }

        private void BeginFall(Meteor m)
        {
            m.rock.position = new Vector3(m.target.x, m.target.y + dropHeight, m.target.z);
            m.rock.gameObject.SetActive(true);
            m.rockCollider.enabled = true;

            m.phase = Phase.Falling;
            m.timer = fallTime;
        }

        private void Reset(Meteor m)
        {
            m.warn.gameObject.SetActive(false);
            m.rockCollider.enabled = false;
            m.rock.gameObject.SetActive(false);
            m.phase = Phase.Waiting;
            m.timer = 0.2f;
        }

        public void Bind(Vector3 center, Vector2 size, int count, Material meteorMat, Material warnMat)
        {
            zoneCenter = center;
            zoneSize = size;
            meteorCount = count;
            meteorMaterial = meteorMat;
            warnMaterial = warnMat;
        }
    }
}
