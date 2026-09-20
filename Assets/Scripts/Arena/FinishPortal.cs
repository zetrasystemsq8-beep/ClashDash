using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Animated energy portal drawn inside the finish gate ring (uses the CLASHDASH/EnergyField shader).
    /// Attached automatically to FinishGate objects by ClashDashExtrasBootstrap.
    /// </summary>
    public class FinishPortal : MonoBehaviour
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] private float radius = 5.2f;
        [SerializeField] private float baseIntensity = 1.4f;
        [SerializeField] private float baseAlpha = 0.35f;

        private Material material;
        private Transform disc;
        private GameManager gm;
        private UnityAction onFinished;
        private float burst;

        private void Awake()
        {
            Shader shader = Resources.Load<Shader>("Shaders/CD_EnergyField");
            if (shader == null)
            {
                enabled = false;
                return;
            }

            material = new Material(shader);
            material.SetColor("_Color", new Color(0.25f, 0.9f, 1f, 1f));
            material.SetColor("_Color2", new Color(1f, 0.8f, 0.4f, 1f));
            material.SetFloat(IntensityId, baseIntensity);
            material.SetFloat(AlphaId, baseAlpha);

            GameObject go = new GameObject("PortalDisc");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * radius;
            disc = go.transform;

            go.AddComponent<MeshFilter>().sharedMesh = BuildDiscMesh(48);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void Start()
        {
            gm = GameManager.Instance;
            if (gm != null)
            {
                onFinished = () => burst = 1f;
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void Update()
        {
            if (disc == null) return;

            float t = Time.time;
            burst = Mathf.MoveTowards(burst, 0f, Time.deltaTime * 0.6f);

            float breathe = 1f + Mathf.Sin(t * 1.8f) * 0.03f;
            disc.localScale = Vector3.one * (radius * breathe * (1f + burst * 0.5f));
            disc.localRotation = Quaternion.Euler(0f, 0f, t * 12f);

            material.SetFloat(IntensityId, baseIntensity + burst * 3f);
            material.SetFloat(AlphaId, Mathf.Clamp01(baseAlpha + burst * 0.5f));
        }

        private void OnDestroy()
        {
            if (gm != null && onFinished != null) gm.onFinished.RemoveListener(onFinished);
            if (material != null) Destroy(material);
        }

        private static Mesh BuildDiscMesh(int segments)
        {
            Vector3[] verts = new Vector3[segments + 2];
            Vector3[] normals = new Vector3[verts.Length];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            normals[0] = Vector3.back;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a);
                float y = Mathf.Sin(a);
                verts[i + 1] = new Vector3(x, y, 0f);
                normals[i + 1] = Vector3.back;
                uvs[i + 1] = new Vector2(x * 0.5f + 0.5f, y * 0.5f + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = i + 1;
            }

            Mesh mesh = new Mesh();
            mesh.name = "CD_PortalDisc";
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
