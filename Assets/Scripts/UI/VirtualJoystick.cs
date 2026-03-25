using UnityEngine;
using UnityEngine.EventSystems;

namespace RunAndGun
{
    /// <summary>
    /// Drag-based virtual joystick for mobile touch input.
    /// Returns a normalised Vector2 direction. Implements pointer
    /// and drag event interfaces for reliable multi-touch handling.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Components")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Header("Behaviour")]
        [Tooltip("Maximum distance the handle can travel from centre (in pixels).")]
        [SerializeField] private float handleRange = 60f;

        [Tooltip("Input magnitude below this value is treated as zero.")]
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.15f;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private Canvas parentCanvas;
        private Camera canvasCamera;
        private Vector2 inputDirection;
        private int activePointerId = -1;

        /// <summary>
        /// Normalised joystick direction. Components range from -1 to 1.
        /// Magnitude is clamped to 1.
        /// </summary>
        public Vector2 Direction => inputDirection;

        /// <summary>Horizontal axis (-1 to 1).</summary>
        public float Horizontal => inputDirection.x;

        /// <summary>Vertical axis (-1 to 1).</summary>
        public float Vertical => inputDirection.y;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasCamera = parentCanvas.worldCamera;
        }

        private void OnDisable()
        {
            ResetJoystick();
        }

        // ------------------------------------------------------------------ //
        //  Event handlers
        // ------------------------------------------------------------------ //

        public void OnPointerDown(PointerEventData eventData)
        {
            // Claim this pointer
            activePointerId = eventData.pointerId;
            HandleInput(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Only respond to the pointer that started on us
            if (eventData.pointerId != activePointerId) return;
            HandleInput(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            ResetJoystick();
        }

        // ------------------------------------------------------------------ //
        //  Input processing
        // ------------------------------------------------------------------ //

        private void HandleInput(PointerEventData eventData)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, canvasCamera, out localPoint);

            // Normalise to -1..1 range based on background size
            Vector2 bgSize = background.sizeDelta;
            Vector2 normalised = new Vector2(
                localPoint.x / (bgSize.x * 0.5f),
                localPoint.y / (bgSize.y * 0.5f));

            // Clamp magnitude to 1
            if (normalised.magnitude > 1f)
                normalised = normalised.normalized;

            // Apply dead zone
            if (normalised.magnitude < deadZone)
                normalised = Vector2.zero;

            inputDirection = normalised;

            // Move the handle visual
            if (handle != null)
            {
                handle.anchoredPosition = normalised * handleRange;
            }

            // Feed into InputManager
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetMobileJoystick(inputDirection);
            }
        }

        private void ResetJoystick()
        {
            activePointerId = -1;
            inputDirection = Vector2.zero;

            if (handle != null)
                handle.anchoredPosition = Vector2.zero;

            if (InputManager.Instance != null)
                InputManager.Instance.SetMobileJoystick(Vector2.zero);
        }
    }
}
