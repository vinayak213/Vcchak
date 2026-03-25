using System;
using System.Collections;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Base class for every enemy. Handles health, damage flash, death drops,
    /// scoring, and automatic pool return.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class EnemyBase : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //
        [Header("Enemy Config")]
        [SerializeField] protected EnemyData data;

        [Header("VFX")]
        [SerializeField] private GameObject deathEffectPrefab;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private int flashCount = 2;

        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //
        /// <summary>Raised after taking damage. Args: damage dealt, remaining health.</summary>
        public event Action<int, int> OnDamaged;
        /// <summary>Raised once on death. Arg: this enemy instance.</summary>
        public event Action<EnemyBase> OnDeath;

        // ------------------------------------------------------------------ //
        //  Runtime state
        // ------------------------------------------------------------------ //
        public int CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        public EnemyData Data => data;

        protected SpriteRenderer SpriteRenderer { get; private set; }
        private Color _originalColor;
        private Coroutine _flashRoutine;
        private bool _isFlashing;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //
        protected virtual void Awake()
        {
            SpriteRenderer = GetComponent<SpriteRenderer>();
        }

        protected virtual void OnEnable()
        {
            ResetEnemy();
        }

        protected virtual void OnDisable()
        {
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }
            SpriteRenderer.color = _originalColor;
            _isFlashing = false;
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Initialise / re-initialise the enemy (called from pool or on enable).
        /// </summary>
        public void ResetEnemy()
        {
            _originalColor = SpriteRenderer.color;
            CurrentHealth = data.MaxHealth;
            IsDead = false;
        }

        /// <summary>
        /// Apply damage. Returns true if the hit killed the enemy.
        /// </summary>
        public bool TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return false;

            CurrentHealth = Mathf.Max(CurrentHealth - amount, 0);
            OnDamaged?.Invoke(amount, CurrentHealth);

            if (CurrentHealth <= 0)
            {
                Die();
                return true;
            }

            if (!_isFlashing)
            {
                _flashRoutine = StartCoroutine(DamageFlashRoutine());
            }

            return false;
        }

        /// <summary>
        /// Immediately kill this enemy regardless of remaining health.
        /// </summary>
        public void ForceKill()
        {
            if (IsDead) return;
            CurrentHealth = 0;
            Die();
        }

        // ------------------------------------------------------------------ //
        //  Internal
        // ------------------------------------------------------------------ //

        private void Die()
        {
            IsDead = true;

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }
            SpriteRenderer.color = _originalColor;

            SpawnDeathEffect();
            DropItems();

            OnDeath?.Invoke(this);
            OnDeathInternal();

            // Auto-return to pool after a short delay so particles can play.
            StartCoroutine(ReturnToPoolDelayed(0.05f));
        }

        /// <summary>
        /// Override in subclasses for additional death logic (stop AI, play sound, etc.).
        /// </summary>
        protected virtual void OnDeathInternal() { }

        private void SpawnDeathEffect()
        {
            if (deathEffectPrefab != null)
            {
                GameObject fx = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
                Destroy(fx, 3f);
            }
        }

        private void DropItems()
        {
            if (data.DropTable == null) return;

            foreach (DropEntry entry in data.DropTable)
            {
                if (entry.Prefab == null) continue;
                if (UnityEngine.Random.value > entry.Probability) continue;

                for (int i = 0; i < entry.Quantity; i++)
                {
                    Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.4f;
                    Vector3 pos = transform.position + (Vector3)jitter;
                    Instantiate(entry.Prefab, pos, Quaternion.identity);
                }
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            _isFlashing = true;
            for (int i = 0; i < flashCount; i++)
            {
                SpriteRenderer.color = flashColor;
                yield return new WaitForSeconds(flashDuration);
                SpriteRenderer.color = _originalColor;
                yield return new WaitForSeconds(flashDuration);
            }
            _isFlashing = false;
            _flashRoutine = null;
        }

        private IEnumerator ReturnToPoolDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        /// <summary>
        /// Override if using a custom pool. Default just deactivates the GameObject.
        /// </summary>
        protected virtual void ReturnToPool()
        {
            gameObject.SetActive(false);
        }
    }
}
