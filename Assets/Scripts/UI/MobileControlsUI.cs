using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RunAndGun
{
    /// <summary>
    /// Mobile touch controls overlay: virtual joystick, jump, shoot (hold for
    /// auto-fire), weapon switch, crouch, and pause buttons. Automatically
    /// hidden on non-mobile platforms unless forced.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Joystick")]
        [SerializeField] private VirtualJoystick joystick;

        [Header("Action Buttons")]
        [SerializeField] private MobileTouchButton jumpButton;
        [SerializeField] private MobileTouchButton shootButton;
        [SerializeField] private Button weaponSwitchButton;
        [SerializeField] private MobileTouchButton crouchButton;
        [SerializeField] private Button pauseButton;

        [Header("Visibility")]
        [SerializeField] private bool forceShowOnDesktop;

        [Header("Auto-Fire")]
        [Tooltip("Seconds between auto-fire shots while the shoot button is held.")]
        [SerializeField] private float autoFireInterval = 0.15f;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private float autoFireTimer;
        private bool shootHeldLastFrame;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (weaponSwitchButton != null)
                weaponSwitchButton.onClick.AddListener(OnWeaponSwitch);

            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPause);
        }

        private void Start()
        {
            bool shouldShow = forceShowOnDesktop || Application.isMobilePlatform;

            if (InputManager.Instance != null)
                shouldShow = forceShowOnDesktop || InputManager.Instance.IsMobile;

            gameObject.SetActive(shouldShow);
        }

        private void Update()
        {
            ProcessJumpButton();
            ProcessShootButton();
            ProcessCrouchButton();
        }

        private void OnDestroy()
        {
            if (weaponSwitchButton != null)
                weaponSwitchButton.onClick.RemoveListener(OnWeaponSwitch);

            if (pauseButton != null)
                pauseButton.onClick.RemoveListener(OnPause);
        }

        // ------------------------------------------------------------------ //
        //  Button processing (per-frame for held states)
        // ------------------------------------------------------------------ //

        private void ProcessJumpButton()
        {
            if (jumpButton == null || InputManager.Instance == null) return;

            if (jumpButton.IsPressed && !jumpButton.WasPressedLastFrame)
            {
                InputManager.Instance.SetMobileJumpDown();
            }
            else if (!jumpButton.IsPressed && jumpButton.WasPressedLastFrame)
            {
                InputManager.Instance.SetMobileJumpUp();
            }

            jumpButton.WasPressedLastFrame = jumpButton.IsPressed;
        }

        private void ProcessShootButton()
        {
            if (shootButton == null || InputManager.Instance == null) return;

            bool held = shootButton.IsPressed;

            // First press
            if (held && !shootHeldLastFrame)
            {
                InputManager.Instance.SetMobileShootDown();
                autoFireTimer = autoFireInterval;
            }

            // Release
            if (!held && shootHeldLastFrame)
            {
                InputManager.Instance.SetMobileShootUp();
            }

            // Auto-fire while held
            if (held)
            {
                autoFireTimer -= Time.deltaTime;
                if (autoFireTimer <= 0f)
                {
                    InputManager.Instance.SetMobileShootDown();
                    autoFireTimer = autoFireInterval;
                }
            }

            shootHeldLastFrame = held;
        }

        private void ProcessCrouchButton()
        {
            if (crouchButton == null || InputManager.Instance == null) return;
            InputManager.Instance.SetMobileCrouch(crouchButton.IsPressed);
        }

        // ------------------------------------------------------------------ //
        //  One-shot button callbacks
        // ------------------------------------------------------------------ //

        private void OnWeaponSwitch()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.SetMobileWeaponSwitch();
        }

        private void OnPause()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.SetMobilePause();
        }
    }

    /// <summary>
    /// Simple pointer-down / pointer-up tracker placed on a UI Button or Image.
    /// Exposes <see cref="IsPressed"/> for continuous held-state queries.
    /// </summary>
    public class MobileTouchButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>True while the finger/pointer is down on this element.</summary>
        public bool IsPressed { get; private set; }

        /// <summary>
        /// Used by <see cref="MobileControlsUI"/> to detect press/release edges.
        /// </summary>
        public bool WasPressedLastFrame { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
        }

        private void OnDisable()
        {
            IsPressed = false;
            WasPressedLastFrame = false;
        }
    }
}
