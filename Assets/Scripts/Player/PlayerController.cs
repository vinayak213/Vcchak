// ---------------------------------------------------------------------------
// PlayerController.cs — Main player controller for a 2D Contra-style
// action platformer.  Handles horizontal movement, variable-height jumping
// with double-jump, crouching, 8-directional aiming, coyote time, jump
// buffering, and a simple finite state machine.  All physics work is done
// in FixedUpdate; input is sampled in Update so button presses are never
// missed between physics ticks.
// ---------------------------------------------------------------------------

using System;
using UnityEngine;

namespace RunAndGun
{
    // ---------------------------------------------------------------------- //
    //  Player state machine
    // ---------------------------------------------------------------------- //

    /// <summary>All mutually exclusive locomotion states.</summary>
    public enum PlayerState
    {
        Idle,
        Running,
        Jumping,
        Falling,
        Crouching
    }

    // ---------------------------------------------------------------------- //
    //  Controller
    // ---------------------------------------------------------------------- //

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        // ================================================================== //
        //  Inspector — Movement
        // ================================================================== //

        [Header("Horizontal Movement")]
        [Tooltip("Maximum horizontal speed in units/sec.")]
        [SerializeField] private float moveSpeed = 8f;

        [Tooltip("Time (seconds) to reach full speed from rest.")]
        [SerializeField] private float accelerationTime = 0.08f;

        [Tooltip("Time (seconds) to decelerate to zero.")]
        [SerializeField] private float decelerationTime = 0.05f;

        // ================================================================== //
        //  Inspector — Jumping
        // ================================================================== //

        [Header("Jumping")]
        [Tooltip("Upward velocity applied on jump.")]
        [SerializeField] private float jumpForce = 16f;

        [Tooltip("Gravity multiplier when the player releases jump early (variable height).")]
        [SerializeField] private float lowJumpMultiplier = 4f;

        [Tooltip("Gravity multiplier while falling for a snappier arc.")]
        [SerializeField] private float fallMultiplier = 2.5f;

        [Tooltip("Allow one extra jump in the air.")]
        [SerializeField] private bool enableDoubleJump = true;

        [Tooltip("Seconds after leaving a ledge during which a jump is still allowed.")]
        [SerializeField] private float coyoteTime = 0.12f;

        [Tooltip("Seconds before landing during which a jump input is remembered.")]
        [SerializeField] private float jumpBufferTime = 0.15f;

        // ================================================================== //
        //  Inspector — Crouching
        // ================================================================== //

        [Header("Crouching")]
        [Tooltip("Speed multiplier while crouching (0-1).")]
        [SerializeField, Range(0f, 1f)] private float crouchSpeedMultiplier = 0.35f;

        [Tooltip("Collider height multiplier while crouching (0-1).")]
        [SerializeField, Range(0.2f, 1f)] private float crouchColliderScale = 0.5f;

        // ================================================================== //
        //  Inspector — Ground Check
        // ================================================================== //

        [Header("Ground Check")]
        [Tooltip("Origin offset for the ground check rays (relative to collider bottom).")]
        [SerializeField] private Vector2 groundCheckOffset = new(0f, -0.05f);

        [Tooltip("Width of the ground check box (fraction of collider width).")]
        [SerializeField, Range(0.5f, 1f)] private float groundCheckWidth = 0.9f;

        [Tooltip("Depth of the ground check box.")]
        [SerializeField] private float groundCheckDepth = 0.1f;

        [Tooltip("Layers treated as solid ground.")]
        [SerializeField] private LayerMask groundLayers;

        // ================================================================== //
        //  Events (consumed by combat, animation, UI, etc.)
        // ================================================================== //

        /// <summary>Fired whenever the locomotion state changes.</summary>
        public event Action<PlayerState> OnStateChanged;

        /// <summary>Fires every FixedUpdate with the current normalised aim direction.</summary>
        public event Action<Vector2> OnAimDirectionChanged;

        // ================================================================== //
        //  Public read-only properties
        // ================================================================== //

        /// <summary>Current locomotion state.</summary>
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        /// <summary>True when the player is touching ground.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>True while crouching.</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>True when the sprite faces right (+X).</summary>
        public bool IsFacingRight { get; private set; } = true;

        /// <summary>Current horizontal velocity (signed).</summary>
        public float HorizontalVelocity => _rb.linearVelocity.x;

        /// <summary>Current vertical velocity (signed).</summary>
        public float VerticalVelocity => _rb.linearVelocity.y;

        /// <summary>Current 8-direction aim vector (normalised).</summary>
        public Vector2 AimDirection { get; private set; } = Vector2.right;

        /// <summary>The underlying Rigidbody2D (for external queries).</summary>
        public Rigidbody2D Rigidbody => _rb;

        // ================================================================== //
        //  Cached references
        // ================================================================== //

        private Rigidbody2D _rb;
        private CapsuleCollider2D _collider;
        private InputManager _input;

        // ================================================================== //
        //  Internal state
        // ================================================================== //

        // -- Collider defaults (saved on Awake for crouch toggling) --
        private float _standingColliderHeight;
        private float _standingColliderOffsetY;

        // -- Jump bookkeeping --
        private int _jumpsRemaining;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _isJumpCut;           // True when the player released jump early

        // -- Smoothed movement --
        private float _currentVelX;        // Tracks velocity for SmoothDamp

        // -- Input snapshot (sampled in Update, consumed in FixedUpdate) --
        private float _inputH;
        private float _inputV;
        private bool _jumpPressed;
        private bool _jumpHeld;
        private bool _crouchHeld;

        // -- Physics helpers --
        private float _defaultGravityScale;

        // -- Contact-based ground detection --
        private ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private float _debugTimer;

        // ================================================================== //
        //  Unity lifecycle
        // ================================================================== //

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();

            // Store standing collider dimensions.
            _standingColliderHeight = _collider.size.y;
            _standingColliderOffsetY = _collider.offset.y;

            _defaultGravityScale = _rb.gravityScale;

            // Prevent unwanted rotation in 2D.
            _rb.freezeRotation = true;
        }

        private void Start()
        {
            _input = InputManager.Instance;
            _jumpsRemaining = enableDoubleJump ? 2 : 1;

            // Safety: if groundLayers was never assigned in the Inspector,
            // default to the "Default" layer so the player doesn't fall forever.
            if (groundLayers.value == 0)
            {
                groundLayers = 1 << 0;
                Debug.LogWarning("[PlayerController] groundLayers was Nothing — defaulted to Default layer.");
            }

            Debug.Log($"[PlayerController] Start: groundLayers={groundLayers.value}, pos={transform.position}, " +
                      $"collider.size={_collider.size}, rb.bodyType={_rb.bodyType}, rb.simulated={_rb.simulated}");

            // Log all ground colliders in the scene
            var allCols = FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
            foreach (var c in allCols)
            {
                if (c.gameObject.name.StartsWith("Ground_") || c.gameObject.name == "Platform")
                {
                    Debug.Log($"[PlayerController] Found ground: {c.gameObject.name} pos={c.transform.position} " +
                              $"size={c.size} isTrigger={c.isTrigger} layer={c.gameObject.layer}");
                }
            }
        }

        /// <summary>
        /// Input is sampled in Update so that single-frame presses (GetButtonDown)
        /// are never lost between FixedUpdate ticks.
        /// </summary>
        private void Update()
        {
            SampleInput();
            TickTimers();
        }

        /// <summary>
        /// All physics-related logic runs in FixedUpdate for deterministic behaviour.
        /// </summary>
        private void FixedUpdate()
        {
            CheckGround();
            HandleHorizontalMovement();
            HandleJump();
            HandleCrouch();
            ApplyVariableGravity();
            UpdateAimDirection();
            ResolveState();
        }

        // ================================================================== //
        //  Input sampling
        // ================================================================== //

        private void SampleInput()
        {
            _inputH = _input.Horizontal;
            _inputV = _input.Vertical;

            // Buffer the jump press so it persists until consumed or expired.
            if (_input.JumpDown)
            {
                _jumpBufferTimer = jumpBufferTime;
            }

            _jumpHeld = _input.JumpHeld;
            _crouchHeld = _input.CrouchHeld;
        }

        /// <summary>Tick frame-based timers (coyote, jump buffer).</summary>
        private void TickTimers()
        {
            if (_jumpBufferTimer > 0f)
                _jumpBufferTimer -= Time.deltaTime;

            if (!IsGrounded)
                _coyoteTimer -= Time.deltaTime;
        }

        // ================================================================== //
        //  Ground detection
        // ================================================================== //

        private void CheckGround()
        {
            bool wasGrounded = IsGrounded;

            // Primary: contact-based detection (most reliable)
            IsGrounded = false;
            int contactCount = _rb.GetContacts(_contacts);
            for (int i = 0; i < contactCount; i++)
            {
                // Contact normal pointing up means we're standing on something
                if (_contacts[i].normal.y > 0.5f)
                {
                    IsGrounded = true;
                    break;
                }
            }

            // Fallback: OverlapBox check
            if (!IsGrounded)
            {
                float halfHeight = _collider.size.y * 0.5f;
                Vector2 origin = (Vector2)transform.position
                                 + _collider.offset
                                 + new Vector2(0f, -halfHeight)
                                 + groundCheckOffset;
                Vector2 size = new(_collider.size.x * groundCheckWidth, groundCheckDepth);
                IsGrounded = Physics2D.OverlapBox(origin, size, 0f, groundLayers) != null;
            }

            // Debug: periodic logging
            _debugTimer += Time.fixedDeltaTime;
            if (_debugTimer >= 1f)
            {
                _debugTimer = 0f;
                Debug.Log($"[PlayerController] pos.y={transform.position.y:F1} vel.y={_rb.linearVelocity.y:F1} " +
                          $"grounded={IsGrounded} contacts={contactCount} gravity={_rb.gravityScale}");
            }

            // Safety: if the player falls below -20, teleport back to start
            if (transform.position.y < -20f)
            {
                Debug.LogWarning("[PlayerController] Player fell below kill plane — teleporting to (-5, 2)");
                _rb.linearVelocity = Vector2.zero;
                _rb.position = new Vector2(-5f, 2f);
                _rb.gravityScale = _defaultGravityScale;
            }

            if (IsGrounded)
            {
                _coyoteTimer = coyoteTime;
                _jumpsRemaining = enableDoubleJump ? 2 : 1;
                _isJumpCut = false;
            }
            else if (wasGrounded)
            {
                if (_jumpsRemaining > 0 && _rb.linearVelocity.y <= 0.01f)
                {
                    _jumpsRemaining--;
                }
            }
        }

        // ================================================================== //
        //  Horizontal movement (smooth acceleration / deceleration)
        // ================================================================== //

        private void HandleHorizontalMovement()
        {
            float targetSpeed = _inputH * moveSpeed;

            // Slow down while crouching.
            if (IsCrouching)
                targetSpeed *= crouchSpeedMultiplier;

            // Choose smoothing time based on whether we are accelerating or stopping.
            float smoothTime = Mathf.Abs(_inputH) > 0.01f ? accelerationTime : decelerationTime;
            float newVelX = Mathf.SmoothDamp(_rb.linearVelocity.x, targetSpeed, ref _currentVelX, smoothTime);

            _rb.linearVelocity = new Vector2(newVelX, _rb.linearVelocity.y);

            // Flip sprite.
            if (_inputH > 0.01f && !IsFacingRight) Flip();
            else if (_inputH < -0.01f && IsFacingRight) Flip();
        }

        // ================================================================== //
        //  Jumping (variable height, double jump, coyote, buffer)
        // ================================================================== //

        private void HandleJump()
        {
            // Consume buffered jump if we can jump.
            bool canCoyote = _coyoteTimer > 0f;
            bool wantsJump = _jumpBufferTimer > 0f;

            if (wantsJump && (_jumpsRemaining > 0 || canCoyote))
            {
                ExecuteJump();
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }

            // Detect early release for variable-height jump.
            if (!_jumpHeld && _rb.linearVelocity.y > 0f && !_isJumpCut)
            {
                _isJumpCut = true;
            }
        }

        private void ExecuteJump()
        {
            _jumpsRemaining--;
            _isJumpCut = false;

            // Reset vertical velocity so double-jumps feel consistent.
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        // ================================================================== //
        //  Variable gravity (snappy fall, short-hop support)
        // ================================================================== //

        private void ApplyVariableGravity()
        {
            if (_rb.linearVelocity.y < 0f)
            {
                // Falling — increase gravity for a snappier arc.
                _rb.gravityScale = _defaultGravityScale * fallMultiplier;
            }
            else if (_rb.linearVelocity.y > 0f && _isJumpCut)
            {
                // Rising but the player let go of jump — cut the ascent.
                _rb.gravityScale = _defaultGravityScale * lowJumpMultiplier;
            }
            else
            {
                _rb.gravityScale = _defaultGravityScale;
            }
        }

        // ================================================================== //
        //  Crouching
        // ================================================================== //

        private void HandleCrouch()
        {
            bool wantsCrouch = _crouchHeld && IsGrounded;

            if (wantsCrouch && !IsCrouching)
            {
                EnterCrouch();
            }
            else if (!wantsCrouch && IsCrouching)
            {
                // Only stand up if there is room above.
                if (!IsBlockedAbove())
                    ExitCrouch();
            }
        }

        private void EnterCrouch()
        {
            IsCrouching = true;

            // Shrink the collider and shift its offset downward so the feet
            // stay on the ground.
            float newHeight = _standingColliderHeight * crouchColliderScale;
            float heightDiff = _standingColliderHeight - newHeight;
            _collider.size = new Vector2(_collider.size.x, newHeight);
            _collider.offset = new Vector2(_collider.offset.x, _standingColliderOffsetY - heightDiff * 0.5f);
        }

        private void ExitCrouch()
        {
            IsCrouching = false;
            _collider.size = new Vector2(_collider.size.x, _standingColliderHeight);
            _collider.offset = new Vector2(_collider.offset.x, _standingColliderOffsetY);
        }

        /// <summary>
        /// Quick overlap check above the player to prevent standing up into a
        /// ceiling while crouched.
        /// </summary>
        private bool IsBlockedAbove()
        {
            float currentHalfHeight = _collider.size.y * 0.5f;
            float standHalfHeight = _standingColliderHeight * 0.5f;
            float extraNeeded = standHalfHeight - currentHalfHeight;

            Vector2 origin = (Vector2)transform.position
                             + _collider.offset
                             + new Vector2(0f, currentHalfHeight + extraNeeded * 0.5f);
            Vector2 size = new(_collider.size.x * 0.9f, extraNeeded);

            return Physics2D.OverlapBox(origin, size, 0f, groundLayers) != null;
        }

        // ================================================================== //
        //  8-directional aiming
        // ================================================================== //

        /// <summary>
        /// Snap the raw input to one of eight cardinal / diagonal directions.
        /// When no directional input is given, default to facing direction.
        /// </summary>
        private void UpdateAimDirection()
        {
            Vector2 raw = new(_inputH, _inputV);

            if (raw.sqrMagnitude < 0.1f)
            {
                // No input — aim in the direction the sprite faces.
                AimDirection = IsFacingRight ? Vector2.right : Vector2.left;
            }
            else
            {
                // Snap to nearest 45-degree increment.
                float angle = Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg;
                float snapped = Mathf.Round(angle / 45f) * 45f;
                float rad = snapped * Mathf.Deg2Rad;
                AimDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
            }

            OnAimDirectionChanged?.Invoke(AimDirection);
        }

        // ================================================================== //
        //  State machine
        // ================================================================== //

        private void ResolveState()
        {
            PlayerState newState;

            if (IsCrouching)
            {
                newState = PlayerState.Crouching;
            }
            else if (!IsGrounded && _rb.linearVelocity.y > 0.1f)
            {
                newState = PlayerState.Jumping;
            }
            else if (!IsGrounded && _rb.linearVelocity.y <= 0.1f)
            {
                newState = PlayerState.Falling;
            }
            else if (Mathf.Abs(_rb.linearVelocity.x) > 0.1f)
            {
                newState = PlayerState.Running;
            }
            else
            {
                newState = PlayerState.Idle;
            }

            if (newState != CurrentState)
            {
                CurrentState = newState;
                OnStateChanged?.Invoke(CurrentState);
            }
        }

        // ================================================================== //
        //  Sprite flip
        // ================================================================== //

        private void Flip()
        {
            IsFacingRight = !IsFacingRight;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (IsFacingRight ? 1f : -1f);
            transform.localScale = scale;
        }

        // ================================================================== //
        //  Public helpers (called by external systems)
        // ================================================================== //

        /// <summary>
        /// Apply an external force (knockback, explosion, etc.).
        /// </summary>
        public void ApplyForce(Vector2 force)
        {
            _rb.AddForce(force, ForceMode2D.Impulse);
        }

        /// <summary>
        /// Teleport the player to a position (checkpoints, respawn).
        /// </summary>
        public void Teleport(Vector2 position)
        {
            _rb.position = position;
            _rb.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// Freeze or unfreeze all player movement (cutscenes, death).
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (!enabled)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = 0f;
            }
            else
            {
                _rb.gravityScale = _defaultGravityScale;
            }
        }

        // ================================================================== //
        //  Gizmos (editor only)
        // ================================================================== //

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            CapsuleCollider2D col = GetComponent<CapsuleCollider2D>();
            if (col == null) return;

            float halfHeight = col.size.y * 0.5f;
            Vector2 origin = (Vector2)transform.position
                             + col.offset
                             + new Vector2(0f, -halfHeight)
                             + groundCheckOffset;
            Vector2 size = new(col.size.x * groundCheckWidth, groundCheckDepth);

            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(origin, size);

            // Draw aim direction.
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, AimDirection * 1.5f);
        }
#endif
    }
}
