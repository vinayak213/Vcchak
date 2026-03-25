// ---------------------------------------------------------------------------
// PlayerHealth.cs — Health system for the player character.  Supports
// configurable max HP, invincibility frames with a red-flash visual cue,
// shield/armor absorption, healing pickups, and death handling with events.
// Implements IDamageable (defined in RunAndGun.Bullet) so hazards and
// enemy bullets can damage the player through a common interface.
// ---------------------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;

namespace RunAndGun
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        // ================================================================== //
        //  Inspector
        // ================================================================== //

        [Header("Health")]
        [Tooltip("Maximum hit points.")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Invincibility")]
        [Tooltip("Duration of invincibility after taking damage (seconds).")]
        [SerializeField] private float invincibilityDuration = 1.5f;

        [Tooltip("Flash interval during invincibility (seconds).")]
        [SerializeField] private float flashInterval = 0.1f;

        [Tooltip("Color to flash when hit.")]
        [SerializeField] private Color flashColor = Color.red;

        [Header("Shield / Armor")]
        [Tooltip("Extra hit points that absorb damage before health is reduced.")]
        [SerializeField] private float maxShield = 0f;

        [Tooltip("Fraction of damage absorbed by armor (0 = none, 1 = full).")]
        [SerializeField, Range(0f, 1f)] private float armorAbsorption = 0f;

        // ================================================================== //
        //  Events
        // ================================================================== //

        /// <summary>Raised when the player takes damage.  Args: damage dealt, remaining health.</summary>
        public event Action<float, float> OnDamaged;

        /// <summary>Raised when the player is healed.  Args: amount healed, new health.</summary>
        public event Action<float, float> OnHealed;

        /// <summary>Raised once when health reaches zero.</summary>
        public event Action OnDeath;

        /// <summary>Raised when shield value changes.  Args: current shield, max shield.</summary>
        public event Action<float, float> OnShieldChanged;

        // ================================================================== //
        //  Public properties
        // ================================================================== //

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float CurrentShield { get; private set; }
        public float MaxShield => maxShield;
        public bool IsDead { get; private set; }
        public bool IsAlive => !IsDead;
        public bool IsInvincible { get; private set; }

        /// <summary>Health as a 0-1 ratio for UI bars.</summary>
        public float HealthRatio => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

        /// <summary>Shield as a 0-1 ratio for UI bars.</summary>
        public float ShieldRatio => maxShield > 0f ? CurrentShield / maxShield : 0f;

        // ================================================================== //
        //  Cached references
        // ================================================================== //

        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private Coroutine _invincibilityRoutine;
        private PlayerController _controller;

        // ================================================================== //
        //  Unity lifecycle
        // ================================================================== //

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _controller = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            _originalColor = _spriteRenderer.color;
            CurrentHealth = maxHealth;
            CurrentShield = maxShield;
            IsDead = false;
            IsInvincible = false;
        }

        // ================================================================== //
        //  IDamageable implementation
        // ================================================================== //

        /// <summary>
        /// Called by systems that use the structured <see cref="DamageInfo"/> path.
        /// Negative amounts are treated as healing.
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (info.amount < 0f)
            {
                Heal(-info.amount);
                return;
            }

            TakeDamage(info.amount, info.hitPoint);
        }

        /// <summary>
        /// Called by bullets, hazards, or any system that deals damage through
        /// the <see cref="IDamageable"/> interface.
        /// </summary>
        public void TakeDamage(float amount, Vector2 hitPoint)
        {
            if (IsDead || IsInvincible || amount <= 0f) return;

            // ---- Armor absorption ----
            float mitigated = amount * armorAbsorption;
            float remaining = amount - mitigated;

            // ---- Shield absorption ----
            if (CurrentShield > 0f)
            {
                float shieldDamage = Mathf.Min(remaining, CurrentShield);
                CurrentShield -= shieldDamage;
                remaining -= shieldDamage;
                OnShieldChanged?.Invoke(CurrentShield, maxShield);
            }

            // ---- Apply remaining to health ----
            CurrentHealth = Mathf.Max(CurrentHealth - remaining, 0f);
            OnDamaged?.Invoke(amount, CurrentHealth);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
            else
            {
                StartInvincibility();
            }
        }

        // ================================================================== //
        //  Healing
        // ================================================================== //

        /// <summary>
        /// Restore health by the given amount, clamped to max.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
            OnHealed?.Invoke(amount, CurrentHealth);
        }

        /// <summary>Fully restore health.</summary>
        public void FullHeal()
        {
            Heal(maxHealth - CurrentHealth);
        }

        // ================================================================== //
        //  Shield
        // ================================================================== //

        /// <summary>Add shield points, clamped to maxShield.</summary>
        public void AddShield(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentShield = Mathf.Min(CurrentShield + amount, maxShield);
            OnShieldChanged?.Invoke(CurrentShield, maxShield);
        }

        /// <summary>Set a new maximum shield value (e.g. from a pickup).</summary>
        public void SetMaxShield(float newMax)
        {
            maxShield = Mathf.Max(newMax, 0f);
            CurrentShield = Mathf.Min(CurrentShield, maxShield);
            OnShieldChanged?.Invoke(CurrentShield, maxShield);
        }

        /// <summary>Set the armor absorption ratio (0-1).</summary>
        public void SetArmorAbsorption(float ratio)
        {
            armorAbsorption = Mathf.Clamp01(ratio);
        }

        // ================================================================== //
        //  Death
        // ================================================================== //

        private void Die()
        {
            IsDead = true;

            // Stop any active invincibility flash.
            StopInvincibility();

            // Freeze player movement if we have a reference.
            if (_controller != null)
            {
                _controller.SetMovementEnabled(false);
            }

            OnDeath?.Invoke();

            // Notify the GameManager so it can deduct a life / trigger game over.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoseLife();
            }
        }

        /// <summary>
        /// Respawn the player at a given position with full health.
        /// Call this from a checkpoint / respawn manager.
        /// </summary>
        public void Respawn(Vector2 position)
        {
            IsDead = false;
            CurrentHealth = maxHealth;
            CurrentShield = maxShield;
            IsInvincible = false;

            _spriteRenderer.color = _originalColor;

            if (_controller != null)
            {
                _controller.SetMovementEnabled(true);
                _controller.Teleport(position);
            }

            // Brief post-respawn invincibility so the player isn't immediately killed.
            StartInvincibility();
        }

        // ================================================================== //
        //  Invincibility frames
        // ================================================================== //

        private void StartInvincibility()
        {
            StopInvincibility();
            _invincibilityRoutine = StartCoroutine(InvincibilityRoutine());
        }

        private void StopInvincibility()
        {
            if (_invincibilityRoutine != null)
            {
                StopCoroutine(_invincibilityRoutine);
                _invincibilityRoutine = null;
            }
            _spriteRenderer.color = _originalColor;
            IsInvincible = false;
        }

        private IEnumerator InvincibilityRoutine()
        {
            IsInvincible = true;
            float elapsed = 0f;
            bool toggle = false;

            while (elapsed < invincibilityDuration)
            {
                toggle = !toggle;
                _spriteRenderer.color = toggle ? flashColor : _originalColor;
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            _spriteRenderer.color = _originalColor;
            IsInvincible = false;
            _invincibilityRoutine = null;
        }
    }
}
