using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Stylised humanoid runner (head, visor, torso, backpack, arms, legs) built from primitives, with a procedural
    /// animation: run cycle scaled by speed, airborne tuck, hit flail and victory pose. It can follow a
    /// PlayerController automatically or be driven by SetMotion (used by rival racers).
    /// </summary>
    public class RunnerAvatar : MonoBehaviour
    {
        private const float HipHeight = 0.95f;

        private Transform hips, torso, head;
        private Transform thighL, thighR, shinL, shinR, armL, armR, foreL, foreR;

        private PlayerController source;
        private float motionSpeed;
        private bool motionGrounded = true;

        private float phase;
        private float air;
        private float hitTimer;
        private bool victory;

        /// <summary>Creates an avatar under parent. Pass a PlayerController to follow it automatically.</summary>
        public static RunnerAvatar Create(Transform parent, string name, Material bodyMaterial, Material glowMaterial, PlayerController follow)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RunnerAvatar avatar = go.AddComponent<RunnerAvatar>();
            avatar.source = follow;
            avatar.Build(bodyMaterial, glowMaterial);

            if (follow != null)
            {
                follow.onHit.AddListener(avatar.Hit);
            }
            return avatar;
        }

        /// <summary>Holographic material used for rival racers (falls back to a tinted copy of the glow material).</summary>
        public static Material MakeGhostMaterial(Color tint, Material fallback)
        {
            Shader shader = Resources.Load<Shader>("Shaders/CD_EnergyField");
            if (shader != null)
            {
                Material m = new Material(shader);
                m.SetColor("_Color", tint);
                m.SetColor("_Color2", Color.Lerp(tint, Color.white, 0.6f));
                m.SetFloat("_Intensity", 2.2f);
                m.SetFloat("_Alpha", 0.75f);
                m.SetFloat("_FresnelPower", 1.6f);
                return m;
            }

            if (fallback == null) return null;
            Material copy = new Material(fallback);
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", tint);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", tint);
            if (copy.HasProperty("_EmissionColor")) copy.SetColor("_EmissionColor", tint * 2f);
            return copy;
        }

        // ------------------------------------------------------------------ build

        private void Build(Material body, Material glow)
        {
            hips = Pivot("Hips", transform, new Vector3(0f, HipHeight, 0f));
            torso = Pivot("Torso", hips, Vector3.zero);

            Part(PrimitiveType.Cube, "Chest", torso, new Vector3(0f, 0.36f, 0f), new Vector3(0.56f, 0.72f, 0.34f), body);
            Part(PrimitiveType.Cube, "ChestGlow", torso, new Vector3(0f, 0.5f, 0.175f), new Vector3(0.3f, 0.08f, 0.02f), glow);
            Part(PrimitiveType.Cube, "Pack", torso, new Vector3(0f, 0.4f, -0.26f), new Vector3(0.38f, 0.5f, 0.16f), body);
            Part(PrimitiveType.Cube, "PackGlow", torso, new Vector3(0f, 0.4f, -0.345f), new Vector3(0.28f, 0.06f, 0.02f), glow);

            head = Pivot("Head", torso, new Vector3(0f, 0.78f, 0f));
            Part(PrimitiveType.Sphere, "Helmet", head, new Vector3(0f, 0.2f, 0f), Vector3.one * 0.4f, body);
            Part(PrimitiveType.Cube, "Visor", head, new Vector3(0f, 0.22f, 0.15f), new Vector3(0.32f, 0.12f, 0.2f), glow);

            BuildLeg(-1f, out thighL, out shinL, body, glow);
            BuildLeg(1f, out thighR, out shinR, body, glow);
            BuildArm(-1f, out armL, out foreL, body, glow);
            BuildArm(1f, out armR, out foreR, body, glow);
        }

        private void BuildLeg(float side, out Transform thigh, out Transform shin, Material body, Material glow)
        {
            thigh = Pivot("Thigh", hips, new Vector3(0.16f * side, 0f, 0f));
            Part(PrimitiveType.Cube, "ThighMesh", thigh, new Vector3(0f, -0.23f, 0f), new Vector3(0.2f, 0.46f, 0.2f), body);
            shin = Pivot("Shin", thigh, new Vector3(0f, -0.46f, 0f));
            Part(PrimitiveType.Cube, "ShinMesh", shin, new Vector3(0f, -0.23f, 0f), new Vector3(0.18f, 0.46f, 0.18f), body);
            Part(PrimitiveType.Cube, "Foot", shin, new Vector3(0f, -0.5f, 0.07f), new Vector3(0.2f, 0.1f, 0.34f), glow);
        }

        private void BuildArm(float side, out Transform arm, out Transform fore, Material body, Material glow)
        {
            arm = Pivot("Arm", torso, new Vector3(0.37f * side, 0.62f, 0f));
            Part(PrimitiveType.Cube, "UpperArm", arm, new Vector3(0f, -0.19f, 0f), new Vector3(0.15f, 0.38f, 0.15f), body);
            fore = Pivot("Forearm", arm, new Vector3(0f, -0.38f, 0f));
            Part(PrimitiveType.Cube, "ForearmMesh", fore, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.36f, 0.13f), body);
            Part(PrimitiveType.Sphere, "Hand", fore, new Vector3(0f, -0.38f, 0f), Vector3.one * 0.14f, glow);
        }

        private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static void Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider); // no physics: the avatar is visual only

            Renderer renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ control

        public void SetMotion(float speed01, bool grounded)
        {
            motionSpeed = Mathf.Clamp01(speed01);
            motionGrounded = grounded;
        }

        public void SetVictory(bool value)
        {
            victory = value;
        }

        public void Hit()
        {
            hitTimer = 0.5f;
        }

        // ------------------------------------------------------------------ animation

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || hips == null) return;

            if (source != null)
            {
                motionSpeed = source.PlanarSpeed01;
                motionGrounded = source.IsGrounded;
            }

            air = Mathf.MoveTowards(air, motionGrounded ? 0f : 1f, dt * 9f);
            float run = motionSpeed * (1f - air);
            phase += dt * Mathf.Lerp(6f, 15f, motionSpeed) * (1f - air);

            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);

            // Legs
            float thighA = -s * 50f * run;
            float thighB = s * 50f * run;
            float kneeA = Mathf.Max(0f, c) * 75f * run + 8f * run;
            float kneeB = Mathf.Max(0f, -c) * 75f * run + 8f * run;

            // Arms
            float armA = s * 45f * run;
            float armB = -s * 45f * run;
            float elbow = -45f * run - 10f;

            // Airborne tuck
            thighA = Mathf.Lerp(thighA, -50f, air);
            thighB = Mathf.Lerp(thighB, 15f, air);
            kneeA = Mathf.Lerp(kneeA, 80f, air);
            kneeB = Mathf.Lerp(kneeB, 30f, air);
            armA = Mathf.Lerp(armA, -110f, air);
            armB = Mathf.Lerp(armB, -110f, air);

            float lean = 8f * run;

            // Hit flail
            if (hitTimer > 0f)
            {
                hitTimer -= dt;
                float f = Mathf.Clamp01(hitTimer / 0.5f);
                float flail = Mathf.Sin(Time.time * 35f) * 70f;
                armA = Mathf.Lerp(armA, -60f + flail, f);
                armB = Mathf.Lerp(armB, -60f - flail, f);
                lean = Mathf.Lerp(lean, -22f, f);
            }

            // Victory pose
            if (victory)
            {
                float wave = Mathf.Sin(Time.time * 6f) * 15f;
                armA = -165f + wave;
                armB = -165f - wave;
                elbow = -10f;
                lean = -4f;
            }

            thighL.localRotation = Quaternion.Euler(thighA, 0f, 0f);
            thighR.localRotation = Quaternion.Euler(thighB, 0f, 0f);
            shinL.localRotation = Quaternion.Euler(kneeA, 0f, 0f);
            shinR.localRotation = Quaternion.Euler(kneeB, 0f, 0f);
            armL.localRotation = Quaternion.Euler(armA, 0f, 0f);
            armR.localRotation = Quaternion.Euler(armB, 0f, 0f);
            foreL.localRotation = Quaternion.Euler(elbow, 0f, 0f);
            foreR.localRotation = Quaternion.Euler(elbow, 0f, 0f);

            torso.localRotation = Quaternion.Euler(lean, 0f, 0f);
            head.localRotation = Quaternion.Euler(-lean * 0.6f, 0f, 0f);
            hips.localPosition = new Vector3(0f, HipHeight + Mathf.Abs(s) * 0.06f * run + Mathf.Sin(Time.time * 2f) * 0.006f, 0f);
        }

        private void OnDestroy()
        {
            if (source != null) source.onHit.RemoveListener(Hit);
        }
    }
}
