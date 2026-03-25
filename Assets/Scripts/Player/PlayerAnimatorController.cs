// ---------------------------------------------------------------------------
// PlayerAnimatorController.cs — Bridges the player's runtime state to
// Unity's Animator.  Reads data from PlayerController, PlayerHealth, and
// PlayerCombat and sets hashed Animator parameters every frame so the
// Animator state machine can drive sprite transitions smoothly.
//
// Expected Animator Parameters (set them up in the Animator Controller asset):
//   Float  "Speed"        — absolute horizontal speed (0 = idle)
//   Float  "VerticalVel"  — vertical velocity (positive = rising)
//   Bool   "IsGrounded"   — feet on the ground
//   Bool   "IsCrouching"  — currently crouched
//   Bool   "IsShooting"   — fire button held / just fired
//   Bool   "IsDead"       — player is dead
//   Trigger "Jump"        — set the frame a jump begins
//   Trigger "Hit"         — set the frame damage is taken
// ---------------------------------------------------------------------------

using UnityEngine;

namespace RunAndGun
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        // ================================================================== //
        //  Inspector
        // ================================================================== //

        [Header("References (auto-resolved if left empty)")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat playerCombat;

        [Header("Smoothing")]
        [Tooltip("Damping time for the Speed parameter to avoid snapping.")]
        [SerializeField] private float speedDampTime = 0.05f;

        // ================================================================== //
        //  Animator parameter hashes (cached for performance)
        // ================================================================== //

        private static readonly int AnimSpeed       = Animator.StringToHash("Speed");
        private static readonly int AnimVerticalVel = Animator.StringToHash("VerticalVel");
        private static readonly int AnimIsGrounded  = Animator.StringToHash("IsGrounded");
        private static readonly int AnimIsCrouching = Animator.StringToHash("IsCrouching");
        private static readonly int AnimIsShooting  = Animator.StringToHash("IsShooting");
        private static readonly int AnimIsDead      = Animator.StringToHash("IsDead");
        private static readonly int AnimJump        = Animator.StringToHash("Jump");
        private static readonly int AnimHit         = Animator.StringToHash("Hit");

        // ================================================================== //
        //  Cached references
        // ================================================================== //

        private Animator _animator;
        private PlayerState _previousState;

        // ================================================================== //
        //  Unity lifecycle
        // ================================================================== //

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            // Auto-resolve sibling components if not assigned in the inspector.
            if (playerController == null)
                playerController = GetComponent<PlayerController>();
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            if (playerCombat == null)
                playerCombat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            // Subscribe to events that map cleanly to one-shot triggers.
            if (playerController != null)
                playerController.OnStateChanged += HandleStateChanged;
            if (playerHealth != null)
            {
                playerHealth.OnDamaged += HandleDamaged;
                playerHealth.OnDeath   += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (playerController != null)
                playerController.OnStateChanged -= HandleStateChanged;
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandleDamaged;
                playerHealth.OnDeath   -= HandleDeath;
            }
        }

        /// <summary>
        /// Continuous parameters are updated every frame for smooth blending.
        /// </summary>
        private void LateUpdate()
        {
            if (playerController == null || _animator == null) return;

            // -- Continuous floats --
            float absSpeed = Mathf.Abs(playerController.HorizontalVelocity);
            _animator.SetFloat(AnimSpeed, absSpeed, speedDampTime, Time.deltaTime);
            _animator.SetFloat(AnimVerticalVel, playerController.VerticalVelocity);

            // -- Booleans --
            _animator.SetBool(AnimIsGrounded, playerController.IsGrounded);
            _animator.SetBool(AnimIsCrouching, playerController.IsCrouching);

            // Shooting flag: true while the fire button is held (auto-fire) or
            // during the brief window after a shot.
            bool shooting = playerCombat != null && playerCombat.IsShooting;
            _animator.SetBool(AnimIsShooting, shooting);

            // Dead flag (sticky until respawn resets it externally).
            if (playerHealth != null)
                _animator.SetBool(AnimIsDead, playerHealth.IsDead);
        }

        // ================================================================== //
        //  Event handlers → one-shot triggers
        // ================================================================== //

        private void HandleStateChanged(PlayerState newState)
        {
            // Fire the Jump trigger on the transition *into* Jumping.
            if (newState == PlayerState.Jumping && _previousState != PlayerState.Jumping)
            {
                _animator.SetTrigger(AnimJump);
            }
            _previousState = newState;
        }

        private void HandleDamaged(float damage, float remainingHealth)
        {
            _animator.SetTrigger(AnimHit);
        }

        private void HandleDeath()
        {
            _animator.SetBool(AnimIsDead, true);
        }

        // ================================================================== //
        //  Public helpers
        // ================================================================== //

        /// <summary>
        /// Reset all animator parameters (called on respawn).
        /// </summary>
        public void ResetAnimator()
        {
            _animator.SetFloat(AnimSpeed, 0f);
            _animator.SetFloat(AnimVerticalVel, 0f);
            _animator.SetBool(AnimIsGrounded, true);
            _animator.SetBool(AnimIsCrouching, false);
            _animator.SetBool(AnimIsShooting, false);
            _animator.SetBool(AnimIsDead, false);
            _animator.Rebind();
            _animator.Update(0f);
        }
    }
}
