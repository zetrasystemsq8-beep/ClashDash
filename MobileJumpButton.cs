using UnityEngine;
using UnityEngine.EventSystems;

namespace Zetra.ClashDash
{
    /// <summary>
    /// On-screen jump button. Reacts on press (not release) and latches the press until consumed.
    /// </summary>
    public class MobileJumpButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private float pressedScale = 0.9f;

        public bool IsHeld { get; private set; }

        private bool pressLatched;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            if (visual == null) visual = transform as RectTransform;
            if (visual != null) baseScale = visual.localScale;
        }

        private void OnDisable()
        {
            IsHeld = false;
            pressLatched = false;
            if (visual != null) visual.localScale = baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            pressLatched = true;
            if (visual != null) visual.localScale = baseScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
            if (visual != null) visual.localScale = baseScale;
        }

        /// <summary>True once per press.</summary>
        public bool ConsumePress()
        {
            bool pressed = pressLatched;
            pressLatched = false;
            return pressed;
        }
    }
}
