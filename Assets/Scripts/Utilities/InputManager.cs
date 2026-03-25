// ---------------------------------------------------------------------------
// InputManager.cs — Unified input abstraction for keyboard and mobile touch.
// Provides a static access pattern so any system can query input state without
// holding a direct reference.  Mobile virtual-joystick and button widgets feed
// their values into this manager via public setters.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Centralised input hub.  Reads Unity's legacy Input axes for
    /// keyboard / gamepad, and exposes setter methods that mobile UI
    /// widgets (virtual joystick, on-screen buttons) call to inject
    /// their values.  Consumers read the merged result through the
    /// static <see cref="Instance"/> singleton.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Singleton
        // ------------------------------------------------------------------ //

        private static InputManager _instance;

        /// <summary>Global access point.  Guaranteed non-null after Awake.</summary>
        public static InputManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Fallback: create a runtime instance if one was not placed
                    // in the scene.  This keeps test scenes working without a
                    // prefab dependency.
                    var go = new GameObject("[InputManager]");
                    _instance = go.AddComponent<InputManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Mobile Settings")]
        [SerializeField] private bool _forceMobileInput;

        [Tooltip("Dead-zone radius for the virtual joystick (0-1).")]
        [SerializeField, Range(0f, 0.5f)] private float _joystickDeadZone = 0.15f;

        // ------------------------------------------------------------------ //
        //  Internal state — mobile overlay values
        // ------------------------------------------------------------------ //

        private Vector2 _mobileJoystick;   // Set by virtual joystick widget
        private bool _mobileJumpDown;
        private bool _mobileJumpHeld;
        private bool _mobileShootDown;
        private bool _mobileShootHeld;
        private bool _mobileWeaponSwitch;
        private bool _mobilePause;
        private bool _mobileCrouch;

        // ------------------------------------------------------------------ //
        //  Public — merged results (keyboard OR mobile, mobile wins if forced)
        // ------------------------------------------------------------------ //

        /// <summary>Horizontal axis in [-1, 1].</summary>
        public float Horizontal { get; private set; }

        /// <summary>Vertical axis in [-1, 1].</summary>
        public float Vertical { get; private set; }

        /// <summary>Normalised movement vector (magnitude clamped to 1).</summary>
        public Vector2 MoveAxis { get; private set; }

        // --- Button states ------------------------------------------------ //

        /// <summary>True the frame Jump was first pressed.</summary>
        public bool JumpDown { get; private set; }

        /// <summary>True while Jump is held.</summary>
        public bool JumpHeld { get; private set; }

        /// <summary>True the frame Jump was released.</summary>
        public bool JumpUp { get; private set; }

        /// <summary>True the frame Shoot was first pressed.</summary>
        public bool ShootDown { get; private set; }

        /// <summary>True while Shoot is held (auto-fire friendly).</summary>
        public bool ShootHeld { get; private set; }

        /// <summary>True the frame weapon-switch was pressed.</summary>
        public bool WeaponSwitchDown { get; private set; }

        /// <summary>True the frame Pause was pressed.</summary>
        public bool PauseDown { get; private set; }

        /// <summary>True while Crouch is held.</summary>
        public bool CrouchHeld { get; private set; }

        /// <summary>Whether we are currently using mobile (touch) input.</summary>
        public bool IsMobile => _forceMobileInput || Application.isMobilePlatform;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            // Enforce singleton — destroy duplicates.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Input is sampled in Update (not FixedUpdate) so that single-frame
        /// button presses are never missed between physics ticks.
        /// </summary>
        private void Update()
        {
            if (IsMobile)
                ReadMobileInput();
            else
                ReadDesktopInput();
        }

        private void LateUpdate()
        {
            // Clear one-shot mobile flags so they only last one frame.
            _mobileJumpDown    = false;
            _mobileShootDown   = false;
            _mobileWeaponSwitch = false;
            _mobilePause       = false;
        }

        // ------------------------------------------------------------------ //
        //  Desktop / keyboard + gamepad
        // ------------------------------------------------------------------ //

        private void ReadDesktopInput()
        {
            Horizontal = Input.GetAxisRaw("Horizontal");
            Vertical   = Input.GetAxisRaw("Vertical");

            MoveAxis = Vector2.ClampMagnitude(new Vector2(Horizontal, Vertical), 1f);

            JumpDown  = Input.GetButtonDown("Jump");
            JumpHeld  = Input.GetButton("Jump");
            JumpUp    = Input.GetButtonUp("Jump");

            // "Fire1" is left-ctrl / left-mouse by default.
            ShootDown = Input.GetButtonDown("Fire1");
            ShootHeld = Input.GetButton("Fire1");

            WeaponSwitchDown = Input.GetKeyDown(KeyCode.Q);
            PauseDown        = Input.GetKeyDown(KeyCode.Escape);

            // Crouch: down arrow or S key (already covered by negative Vertical),
            // but we expose an explicit flag for clarity.
            CrouchHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        }

        // ------------------------------------------------------------------ //
        //  Mobile — reads values pushed by UI widgets
        // ------------------------------------------------------------------ //

        private void ReadMobileInput()
        {
            // Apply dead-zone to joystick.
            Vector2 raw = _mobileJoystick;
            if (raw.magnitude < _joystickDeadZone)
                raw = Vector2.zero;

            Horizontal = Mathf.Clamp(raw.x, -1f, 1f);
            Vertical   = Mathf.Clamp(raw.y, -1f, 1f);
            MoveAxis   = Vector2.ClampMagnitude(new Vector2(Horizontal, Vertical), 1f);

            JumpDown  = _mobileJumpDown;
            JumpHeld  = _mobileJumpHeld;
            JumpUp    = false; // Mobile buttons don't reliably provide "up" events.

            ShootDown = _mobileShootDown;
            ShootHeld = _mobileShootHeld;

            WeaponSwitchDown = _mobileWeaponSwitch;
            PauseDown        = _mobilePause;
            CrouchHeld       = _mobileCrouch;
        }

        // ------------------------------------------------------------------ //
        //  Public setters — called by mobile UI widgets
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called every frame by the virtual joystick widget with its current
        /// direction (components in -1..1).
        /// </summary>
        public void SetMobileJoystick(Vector2 value)
        {
            _mobileJoystick = value;
        }

        /// <summary>Set by the on-screen jump button (pointer-down).</summary>
        public void SetMobileJumpDown()  { _mobileJumpDown = true; _mobileJumpHeld = true; }

        /// <summary>Set by the on-screen jump button (pointer-up).</summary>
        public void SetMobileJumpUp()    { _mobileJumpHeld = false; }

        /// <summary>Set by the on-screen shoot button (pointer-down).</summary>
        public void SetMobileShootDown() { _mobileShootDown = true; _mobileShootHeld = true; }

        /// <summary>Set by the on-screen shoot button (pointer-up).</summary>
        public void SetMobileShootUp()   { _mobileShootHeld = false; }

        /// <summary>Weapon-switch button tap.</summary>
        public void SetMobileWeaponSwitch() { _mobileWeaponSwitch = true; }

        /// <summary>Pause button tap.</summary>
        public void SetMobilePause() { _mobilePause = true; }

        /// <summary>Crouch button (hold).</summary>
        public void SetMobileCrouch(bool held) { _mobileCrouch = held; }
    }
}
