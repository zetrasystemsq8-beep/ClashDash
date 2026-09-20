using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Zetra.ClashDash
{
    /// <summary>
    /// Third-person character controller: camera-relative movement, responsive jump (coyote time,
    /// jump buffer, variable height), moving-platform riding, hit knockback and checkpoint respawn.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MobileJoystick joystick;
        [SerializeField] private MobileJumpButton jumpButton;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform visualRoot;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 9f;
        [SerializeField] private float groundAcceleration = 60f;
        [SerializeField] private float groundDeceleration = 70f;
        [SerializeField] private float airAcceleration = 30f;
        [SerializeField] private float turnSharpness = 16f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 2.4f;
        [SerializeField] private float gravity = 32f;
        [SerializeField] private float fallGravityMultiplier = 1.35f;
        [SerializeField] private float maxFallSpeed = 45f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.14f;
        [SerializeField, Range(0.1f, 1f)] private float jumpCutMultiplier = 0.55f;

        [Header("Hit reactions")]
        [SerializeField] private float stunSeconds = 0.35f;
        [SerializeField] private float hitInvulnerability = 0.9f;
        [SerializeField] private float respawnInvulnerability = 1.2f;
        [SerializeField] private float knockbackDecay = 26f;

        [Header("Visual feel")]
        [SerializeField] private float leanAmount = 9f;
        [SerializeField] private float maxRoll = 12f;
        [SerializeField] private float rollFromTurn = 0.035f;
        [SerializeField] private float tiltSharpness = 10f;

        [Header("Safety")]
        [SerializeField] private float killY = -25f;

        [Header("Events (VFX / audio hooks)")]
        public UnityEvent onJump = new UnityEvent();
        public UnityEvent onLand = new UnityEvent();
        public UnityEvent onHit = new UnityEvent();
        public UnityEvent onRespawn = new UnityEvent();

        public float MoveSpeed => moveSpeed;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector3 PlanarVelocity => horizontalVelocity;
        public float PlanarSpeed01 => Mathf.Clamp01(new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude / Mathf.Max(0.01f, moveSpeed));
        public bool InputLocked => inputLocked;

        private CharacterController controller;
        private Renderer[] renderers;
        private TrailRenderer[] trails;

        private Vector3 horizontalVelocity;
        private Vector3 knockVelocity;
        private float verticalVelocity;

        private float coyoteTimer;
        private float jumpBufferTimer;
        private float stunTimer;
        private float invulnTimer;
        private bool inputLocked;
        private bool wasGrounded;
        private bool jumpCutApplied;
        private bool renderersVisible = true;

        private float yaw;
        private float lean;
        private float roll;
        private float squash;
        private Vector3 visualBaseScale = Vector3.one;
        private Vector3 spawnPosition;

        private ArenaMover platform;
        private Collider lastPlatformCollider;
        private ArenaMover lastPlatformMover;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            renderers = GetComponentsInChildren<Renderer>(true);
            trails = GetComponentsInChildren<TrailRenderer>(true);
            if (visualRoot != null) visualBaseScale = visualRoot.localScale;
            yaw = transform.eulerAngles.y;
        }

        private void Start()
        {
            spawnPosition = transform.position;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            wasGrounded = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !controller.enabled) return;

            invulnTimer = Mathf.Max(0f, invulnTimer - dt);
            stunTimer = Mathf.Max(0f, stunTimer - dt);

            // ---- Input ----
            Vector2 move = Vector2.zero;
            bool jumpDown = false;
            bool jumpHeld = false;

            if (!inputLocked && stunTimer <= 0f)
            {
                move = ReadMoveInput();
                jumpDown = ReadJumpDown();
                jumpHeld = ReadJumpHeld();
            }
            else if (jumpButton != null)
            {
                jumpButton.ConsumePress();
            }

            // ---- Ground state ----
            bool grounded = controller.isGrounded;
            if (grounded && !wasGrounded) HandleLanding();
            wasGrounded = grounded;

            coyoteTimer = grounded ? coyoteTime : coyoteTimer - dt;
            jumpBufferTimer = jumpDown ? jumpBufferTime : jumpBufferTimer - dt;

            // ---- Horizontal ----
            Vector3 desired = ComputeDesiredVelocity(move);
            float accel = grounded ? groundAcceleration : airAcceleration;
            if (grounded && desired.sqrMagnitude < 0.01f) accel = groundDeceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desired, accel * dt);
            knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, knockbackDecay * dt);

            // ---- Vertical ----
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                jumpCutApplied = false;
                squash = -0.14f;
                platform = null;
                onJump.Invoke();
            }

            if (!jumpHeld && !jumpCutApplied && verticalVelocity > 0f)
            {
                verticalVelocity *= jumpCutMultiplier;
                jumpCutApplied = true;
            }

            float g = verticalVelocity < 0f ? gravity * fallGravityMultiplier : gravity;
            verticalVelocity = Mathf.Max(-maxFallSpeed, verticalVelocity - g * dt);

            // ---- Move ----
            Vector3 platformDelta = (grounded && platform != null) ? platform.LastDelta : Vector3.zero;
            Vector3 motion = (horizontalVelocity + knockVelocity + Vector3.up * verticalVelocity) * dt + platformDelta;
            controller.Move(motion);
            if (!controller.isGrounded) platform = null;

            // ---- Facing + visuals ----
            float yawDelta = 0f;
            if (horizontalVelocity.sqrMagnitude > 0.6f)
            {
                float target = Mathf.Atan2(horizontalVelocity.x, horizontalVelocity.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-turnSharpness * dt));
                yawDelta = Mathf.DeltaAngle(yaw, newYaw);
                yaw = newYaw;
            }
            UpdateVisual(dt, yawDelta);

            // ---- Fell out of the arena ----
            if (transform.position.y < killY)
            {
                if (GameManager.Instance != null) GameManager.Instance.HandlePlayerFell();
                else TeleportTo(spawnPosition, Quaternion.identity);
            }
        }

        private Vector3 ComputeDesiredVelocity(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            return (right * move.x + forward * move.y) * moveSpeed;
        }

        private void HandleLanding()
        {
            float impact = -verticalVelocity;
            if (impact > 4f)
            {
                squash = 0.08f + Mathf.Clamp01(impact / 25f) * 0.2f;
                onLand.Invoke();
            }
        }

        private void UpdateVisual(float dt, float yawDelta)
        {
            // Blink while invulnerable.
            bool visible = invulnTimer <= 0f || (((int)(Time.time * 14f)) & 1) == 0;
            if (visible != renderersVisible)
            {
                renderersVisible = visible;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) renderers[i].enabled = visible;
                }
            }

            if (visualRoot == null) return;

            float k = 1f - Mathf.Exp(-tiltSharpness * dt);
            float targetLean = PlanarSpeed01 * leanAmount;
            float targetRoll = Mathf.Clamp(-(yawDelta / dt) * rollFromTurn, -maxRoll, maxRoll);
            lean = Mathf.Lerp(lean, targetLean, k);
            roll = Mathf.Lerp(roll, targetRoll, k);
            squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-12f * dt));

            visualRoot.rotation = Quaternion.Euler(lean, yaw, roll);
            visualRoot.localScale = new Vector3(
                visualBaseScale.x * (1f + squash),
                visualBaseScale.y * (1f - squash),
                visualBaseScale.z * (1f + squash));
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.5f) return;

            if (hit.collider != lastPlatformCollider)
            {
                lastPlatformCollider = hit.collider;
                lastPlatformMover = hit.collider.GetComponentInParent<ArenaMover>();
            }

            platform = (lastPlatformMover != null && lastPlatformMover.Mode != ArenaMoveMode.Rotate) ? lastPlatformMover : null;
        }

        // ---- Public API ----

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            if (locked && jumpButton != null) jumpButton.ConsumePress();
        }

        /// <summary>Returns false if the player is currently invulnerable.</summary>
        public bool TakeHit(Vector3 awayDirection, float force, float launch)
        {
            if (invulnTimer > 0f) return false;

            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude < 0.0001f) awayDirection = -transform.forward;
            awayDirection.Normalize();

            knockVelocity = awayDirection * force;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = launch;
            stunTimer = stunSeconds;
            invulnTimer = hitInvulnerability;
            platform = null;
            jumpCutApplied = true;
            onHit.Invoke();
            return true;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;

            horizontalVelocity = Vector3.zero;
            knockVelocity = Vector3.zero;
            verticalVelocity = 0f;
            yaw = rotation.eulerAngles.y;
            platform = null;
            stunTimer = 0f;
            invulnTimer = respawnInvulnerability;
            wasGrounded = true;

            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null) trails[i].Clear();
            }

            onRespawn.Invoke();
        }

        public void Bind(MobileJoystick stick, MobileJumpButton jump, Transform cameraTarget, Transform visual)
        {
            joystick = stick;
            jumpButton = jump;
            cameraTransform = cameraTarget;
            visualRoot = visual;
        }

        // ---- Input ----

        private Vector2 ReadMoveInput()
        {
            Vector2 v = joystick != null ? joystick.Value : Vector2.zero;
            Vector2 keys = DesktopInput.Move();
            if (keys.sqrMagnitude > v.sqrMagnitude) v = keys;
            return Vector2.ClampMagnitude(v, 1f);
        }

        private bool ReadJumpDown()
        {
            bool pressed = jumpButton != null && jumpButton.ConsumePress();
            return pressed || DesktopInput.JumpDown();
        }

        private bool ReadJumpHeld()
        {
            return (jumpButton != null && jumpButton.IsHeld) || DesktopInput.JumpHeld();
        }
    }

    /// <summary>Keyboard fallback for testing in the Editor / on desktop. Works with either input backend.</summary>
    internal static class DesktopInput
    {
        public static Vector2 Move()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            float x = ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1f : 0f)
                    - ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1f : 0f);
            float y = ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f)
                    - ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f);
            return new Vector2(x, y);
#elif ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                    - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);
            float y = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                    - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);
            return new Vector2(x, y);
#else
            return Vector2.zero;
#endif
        }

        public static bool JumpDown()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#elif ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool JumpHeld()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Space);
#elif ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.spaceKey.isPressed;
#else
            return false;
#endif
        }
    }
}
