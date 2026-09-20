using UnityEngine;
using UnityEngine.EventSystems;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Floating on-screen joystick. Put on a transparent, raycast-enabled UI element that covers the
    /// touch zone (pivot centred). The background jumps to the first touch point.
    /// </summary>
    public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform touchArea;
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private CanvasGroup visualGroup;

        [Header("Feel")]
        [SerializeField] private float radius = 120f;
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.1f;
        [SerializeField] private bool floating = true;
        [SerializeField] private float idleAlpha = 0.35f;
        [SerializeField] private float activeAlpha = 0.95f;

        /// <summary>Normalised stick value (length 0..1).</summary>
        public Vector2 Value { get; private set; }

        public bool IsActive { get; private set; }

        private Vector2 restPosition;
        private int activePointerId = int.MinValue;

        private void Awake()
        {
            if (touchArea == null) touchArea = transform as RectTransform;
            if (background != null) restPosition = background.anchoredPosition;
            SetAlpha(idleAlpha);
        }

        private void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsActive || background == null || handle == null) return;

            IsActive = true;
            activePointerId = eventData.pointerId;

            if (floating && TryGetLocalPoint(eventData, out Vector2 local))
            {
                Rect rect = touchArea.rect;
                local.x = Mathf.Clamp(local.x, rect.xMin + radius, rect.xMax - radius);
                local.y = Mathf.Clamp(local.y, rect.yMin + radius, rect.yMax - radius);
                background.anchoredPosition = local;
            }

            SetAlpha(activeAlpha);
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActive || eventData.pointerId != activePointerId) return;
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsActive || eventData.pointerId != activePointerId) return;
            Release();
        }

        private void UpdateStick(PointerEventData eventData)
        {
            if (!TryGetLocalPoint(eventData, out Vector2 local)) return;

            Vector2 delta = Vector2.ClampMagnitude(local - background.anchoredPosition, radius);
            handle.anchoredPosition = delta;

            float magnitude = delta.magnitude / Mathf.Max(1f, radius);
            if (magnitude < deadZone)
            {
                Value = Vector2.zero;
            }
            else
            {
                Value = delta.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);
            }
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                touchArea, eventData.position, eventData.pressEventCamera, out local);
        }

        private void Release()
        {
            IsActive = false;
            activePointerId = int.MinValue;
            Value = Vector2.zero;

            if (background != null) background.anchoredPosition = restPosition;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            SetAlpha(idleAlpha);
        }

        private void SetAlpha(float alpha)
        {
            if (visualGroup != null) visualGroup.alpha = alpha;
        }

        public void Bind(RectTransform area, RectTransform stickBackground, RectTransform stickHandle, CanvasGroup group)
        {
            touchArea = area;
            background = stickBackground;
            handle = stickHandle;
            visualGroup = group;
        }
    }
}
