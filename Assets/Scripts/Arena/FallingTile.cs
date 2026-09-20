using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Platform that shakes when the player steps on it, then falls away and returns after a delay.
    /// Needs: a trigger collider on this object (slightly above the top surface) and solid colliders on children.
    /// </summary>
    public class FallingTile : MonoBehaviour
    {
        [SerializeField] private float crumbleDelay = 0.55f;
        [SerializeField] private float fallTime = 1.4f;
        [SerializeField] private float respawnDelay = 3.5f;
        [SerializeField] private float shakeAmount = 0.06f;

        private enum TileState
        {
            Idle,
            Shaking,
            Falling,
            Hidden,
            Returning
        }

        private const float ReturnTime = 0.35f;

        private TileState state;
        private float timer;
        private float fallSpeed;
        private Vector3 origin;
        private Vector3 baseScale;
        private Collider[] solids;
        private Renderer[] renderers;

        private void Awake()
        {
            origin = transform.position;
            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<Renderer>(true);

            Collider[] all = GetComponentsInChildren<Collider>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].isTrigger) count++;
            }
            solids = new Collider[count];
            int index = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].isTrigger) solids[index++] = all[i];
            }

            Rigidbody body;
            if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state != TileState.Idle) return;
            if (other.GetComponent<PlayerController>() == null) return;

            state = TileState.Shaking;
            timer = crumbleDelay;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (state)
            {
                case TileState.Shaking:
                    timer -= dt;
                    transform.position = origin + new Vector3(Mathf.Sin(Time.time * 70f), 0f, Mathf.Cos(Time.time * 63f)) * shakeAmount;
                    if (timer <= 0f)
                    {
                        state = TileState.Falling;
                        timer = fallTime;
                        fallSpeed = 0f;
                        SetSolids(false);
                    }
                    break;

                case TileState.Falling:
                    fallSpeed += 30f * dt;
                    transform.position += Vector3.down * (fallSpeed * dt);
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        state = TileState.Hidden;
                        timer = respawnDelay;
                        SetVisible(false);
                    }
                    break;

                case TileState.Hidden:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        transform.position = origin;
                        transform.localScale = baseScale * 0.01f;
                        SetVisible(true);
                        state = TileState.Returning;
                        timer = ReturnTime;
                    }
                    break;

                case TileState.Returning:
                    timer -= dt;
                    float u = 1f - Mathf.Clamp01(timer / ReturnTime);
                    transform.localScale = baseScale * Mathf.Max(0.01f, Mathf.SmoothStep(0f, 1f, u));
                    if (timer <= 0f)
                    {
                        transform.localScale = baseScale;
                        SetSolids(true);
                        state = TileState.Idle;
                    }
                    break;
            }
        }

        private void SetSolids(bool enabledState)
        {
            for (int i = 0; i < solids.Length; i++)
            {
                if (solids[i] != null) solids[i].enabled = enabledState;
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = visible;
            }
        }

        public void Configure(float delay, float respawn)
        {
            crumbleDelay = delay;
            respawnDelay = respawn;
        }
    }
}
