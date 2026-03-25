using System.Collections;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Stationary turret enemy. Rotates to track the player, fires in bursts,
    /// exposes a weak point on its back, and periodically activates a shield.
    /// </summary>
    public sealed class TurretEnemy : EnemyBase, IAttacker
    {
        // ------------------------------------------------------------------ //
        //  Inspector – Tracking
        // ------------------------------------------------------------------ //
        [Header("Turret – Tracking")]
        [SerializeField] private Transform barrel;
        [SerializeField] private float rotationSpeed = 180f;
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private LayerMask obstacleMask;

        // ------------------------------------------------------------------ //
        //  Inspector – Firing
        // ------------------------------------------------------------------ //
        [Header("Turret – Firing")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float burstInterval = 0.12f;
        [SerializeField] private float burstCooldown = 1.8f;
        [SerializeField] private AudioClip fireSound;
        [SerializeField] private AudioSource audioSource;

        // ------------------------------------------------------------------ //
        //  Inspector – Weak Point
        // ------------------------------------------------------------------ //
        [Header("Turret – Weak Point")]
        [Tooltip("Collider on the back of the turret. Hits here deal bonus damage.")]
        [SerializeField] private Collider2D weakPointCollider;
        [SerializeField] private int weakPointDamageMultiplier = 3;

        // ------------------------------------------------------------------ //
        //  Inspector – Shield
        // ------------------------------------------------------------------ //
        [Header("Turret – Shield Phase")]
        [SerializeField] private GameObject shieldVisual;
        [SerializeField] private float shieldDuration = 3f;
        [SerializeField] private float shieldCooldown = 8f;
        [Tooltip("Health percentage threshold that triggers the first shield.")]
        [SerializeField, Range(0f, 1f)] private float shieldHealthThreshold = 0.6f;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //
        private Transform _playerTarget;
        private float _lastBurstTime;
        private bool _isFiring;
        private bool _shieldActive;
        private float _lastShieldTime;
        private bool _shieldTriggeredOnce;
        private Coroutine _burstRoutine;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //
        protected override void Awake()
        {
            base.Awake();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _isFiring = false;
            _shieldActive = false;
            _shieldTriggeredOnce = false;
            _lastBurstTime = -burstCooldown;
            _lastShieldTime = -shieldCooldown;

            SetShieldActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_burstRoutine != null)
            {
                StopCoroutine(_burstRoutine);
                _burstRoutine = null;
            }
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTarget = player.transform;
        }

        private void Update()
        {
            if (IsDead) return;

            TrackPlayer();
            EvaluateShield();
            EvaluateAttack();
        }

        // ------------------------------------------------------------------ //
        //  Tracking
        // ------------------------------------------------------------------ //

        private void TrackPlayer()
        {
            if (_playerTarget == null || barrel == null) return;

            float dist = Vector2.Distance(transform.position, _playerTarget.position);
            if (dist > detectionRange) return;

            Vector2 dir = (Vector2)_playerTarget.position - (Vector2)barrel.position;
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float currentAngle = barrel.eulerAngles.z;

            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
            barrel.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        private bool HasLineOfSight()
        {
            if (_playerTarget == null) return false;

            Vector2 origin = barrel != null ? (Vector2)barrel.position : (Vector2)transform.position;
            Vector2 target = (Vector2)_playerTarget.position;
            Vector2 dir = target - origin;
            float dist = dir.magnitude;

            RaycastHit2D hit = Physics2D.Raycast(origin, dir.normalized, dist, obstacleMask);
            return hit.collider == null;
        }

        // ------------------------------------------------------------------ //
        //  Attack
        // ------------------------------------------------------------------ //

        private void EvaluateAttack()
        {
            if (_isFiring || _shieldActive) return;
            if (_playerTarget == null) return;
            if (Time.time - _lastBurstTime < burstCooldown) return;

            float dist = Vector2.Distance(transform.position, _playerTarget.position);
            if (dist > detectionRange) return;
            if (!HasLineOfSight()) return;

            _burstRoutine = StartCoroutine(BurstFireRoutine());
        }

        public void PerformAttack()
        {
            if (IsDead || _shieldActive || _isFiring) return;
            _burstRoutine = StartCoroutine(BurstFireRoutine());
        }

        private IEnumerator BurstFireRoutine()
        {
            _isFiring = true;

            for (int i = 0; i < burstCount; i++)
            {
                FireSingleShot();
                if (i < burstCount - 1)
                    yield return new WaitForSeconds(burstInterval);
            }

            _lastBurstTime = Time.time;
            _isFiring = false;
            _burstRoutine = null;
        }

        private void FireSingleShot()
        {
            if (bulletPrefab == null || ObjectPool.Instance == null) return;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = barrel != null
                ? (Vector2)barrel.right
                : (_playerTarget != null
                    ? ((Vector2)_playerTarget.position - (Vector2)transform.position).normalized
                    : Vector2.right);

            GameObject obj = ObjectPool.Instance.Get(bulletPrefab, spawnPos, Quaternion.identity);
            EnemyBullet bullet = obj.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                bullet.Initialize(direction, data.ProjectileSpeed, data.ProjectileDamage, bulletPrefab);
            }

            if (fireSound != null && audioSource != null)
                audioSource.PlayOneShot(fireSound);
        }

        // ------------------------------------------------------------------ //
        //  Weak point
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Call this from an external hit-detection script when the weak point
        /// collider is struck. Applies multiplied damage.
        /// </summary>
        public void HitWeakPoint(int baseDamage)
        {
            if (IsDead || _shieldActive) return;
            TakeDamage(baseDamage * weakPointDamageMultiplier);
        }

        /// <summary>
        /// Returns true if the given collider is the weak-point collider.
        /// Useful in external scripts to decide how to handle the hit.
        /// </summary>
        public bool IsWeakPoint(Collider2D col)
        {
            return weakPointCollider != null && col == weakPointCollider;
        }

        // ------------------------------------------------------------------ //
        //  Shield
        // ------------------------------------------------------------------ //

        private void EvaluateShield()
        {
            if (_shieldActive) return;

            // First shield triggers at a health threshold.
            if (!_shieldTriggeredOnce)
            {
                float healthPercent = (float)CurrentHealth / data.MaxHealth;
                if (healthPercent <= shieldHealthThreshold)
                {
                    _shieldTriggeredOnce = true;
                    StartCoroutine(ShieldRoutine());
                    return;
                }
            }

            // Subsequent shields on cooldown.
            if (_shieldTriggeredOnce && Time.time - _lastShieldTime >= shieldCooldown)
            {
                StartCoroutine(ShieldRoutine());
            }
        }

        private IEnumerator ShieldRoutine()
        {
            SetShieldActive(true);
            yield return new WaitForSeconds(shieldDuration);
            SetShieldActive(false);
            _lastShieldTime = Time.time;
        }

        private void SetShieldActive(bool active)
        {
            _shieldActive = active;
            if (shieldVisual != null)
                shieldVisual.SetActive(active);
        }

        /// <summary>
        /// Override damage to block while shield is up.
        /// The base class TakeDamage is not virtual, so we intercept via
        /// the OnDamaged event approach: the turret simply won't take damage
        /// while shielded because we guard externally. However, for a
        /// cleaner approach we expose a wrapper.
        /// </summary>
        public bool TryTakeDamage(int amount)
        {
            if (_shieldActive) return false;
            return TakeDamage(amount);
        }

        // ------------------------------------------------------------------ //
        //  Death
        // ------------------------------------------------------------------ //

        protected override void OnDeathInternal()
        {
            if (_burstRoutine != null)
            {
                StopCoroutine(_burstRoutine);
                _burstRoutine = null;
            }

            StopAllCoroutines();
            SetShieldActive(false);
        }
    }
}
