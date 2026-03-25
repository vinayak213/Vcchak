using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Flying enemy that moves in a sine-wave or circular pattern, can dive-bomb
    /// the player, shoots downward, and avoids obstacles via raycasts.
    /// </summary>
    [RequireComponent(typeof(EnemyAI))]
    public sealed class FlyingEnemy : EnemyBase, IAttacker
    {
        // ------------------------------------------------------------------ //
        //  Inspector – Movement
        // ------------------------------------------------------------------ //
        [Header("Flying Enemy – Movement")]
        [SerializeField] private MovementPattern movementPattern = MovementPattern.SineWave;
        [SerializeField] private float waveAmplitude = 1.5f;
        [SerializeField] private float waveFrequency = 2f;
        [SerializeField] private float circleRadius = 2f;
        [SerializeField] private float baseAltitude = 4f;

        [Header("Flying Enemy – Obstacle Avoidance")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float avoidanceRayLength = 1.5f;
        [SerializeField] private float avoidanceForce = 5f;
        [SerializeField] private int avoidanceRayCount = 5;
        [SerializeField] private float avoidanceSpreadAngle = 120f;

        // ------------------------------------------------------------------ //
        //  Inspector – Dive Bomb
        // ------------------------------------------------------------------ //
        [Header("Flying Enemy – Dive Bomb")]
        [SerializeField] private float diveBombSpeed = 12f;
        [SerializeField] private float diveBombCooldown = 5f;
        [SerializeField] private float diveBombMinAltitude = 1f;
        [SerializeField] private float diveBombTriggerRange = 4f;
        [SerializeField, Range(0f, 1f)] private float diveBombChance = 0.3f;

        // ------------------------------------------------------------------ //
        //  Inspector – Shooting
        // ------------------------------------------------------------------ //
        [Header("Flying Enemy – Shooting")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private AudioClip fireSound;
        [SerializeField] private AudioSource audioSource;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //
        private EnemyAI _ai;
        private float _phaseTimer;
        private Vector2 _anchorPosition;
        private bool _isDiveBombing;
        private float _lastDiveBombTime;
        private Vector2 _diveBombTarget;

        private enum MovementPattern { SineWave, Circular }

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //
        protected override void Awake()
        {
            base.Awake();
            _ai = GetComponent<EnemyAI>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _phaseTimer = 0f;
            _anchorPosition = transform.position;
            _isDiveBombing = false;
            _lastDiveBombTime = -diveBombCooldown;
        }

        private void FixedUpdate()
        {
            if (IsDead) return;

            if (_isDiveBombing)
            {
                ExecuteDiveBomb();
                return;
            }

            Vector2 desired = ComputePatternVelocity();
            desired += ComputeAvoidance();
            desired += ComputeChaseInfluence();

            _ai.Rb.linearVelocity = Vector2.Lerp(_ai.Rb.linearVelocity, desired, 6f * Time.fixedDeltaTime);

            // Face movement direction.
            if (Mathf.Abs(_ai.Rb.linearVelocity.x) > 0.1f)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(_ai.Rb.linearVelocity.x);
                transform.localScale = scale;
            }

            TryInitiateDiveBomb();

            _phaseTimer += Time.fixedDeltaTime;
        }

        // ------------------------------------------------------------------ //
        //  Movement patterns
        // ------------------------------------------------------------------ //

        private Vector2 ComputePatternVelocity()
        {
            switch (movementPattern)
            {
                case MovementPattern.SineWave:
                    return ComputeSineWave();
                case MovementPattern.Circular:
                    return ComputeCircular();
                default:
                    return Vector2.zero;
            }
        }

        private Vector2 ComputeSineWave()
        {
            // Move horizontally at move speed; oscillate vertically.
            float facing = Mathf.Sign(transform.localScale.x);
            float vx = data.MoveSpeed * facing;
            float vy = Mathf.Cos(_phaseTimer * waveFrequency) * waveAmplitude * waveFrequency;

            // Gently correct altitude toward baseAltitude.
            float altError = (_anchorPosition.y + baseAltitude) - transform.position.y;
            vy += altError * 0.5f;

            return new Vector2(vx, vy);
        }

        private Vector2 ComputeCircular()
        {
            float angle = _phaseTimer * waveFrequency;
            Vector2 center = _anchorPosition + Vector2.up * baseAltitude;
            Vector2 targetPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circleRadius;
            Vector2 delta = targetPos - (Vector2)transform.position;
            return delta.normalized * data.MoveSpeed;
        }

        private Vector2 ComputeChaseInfluence()
        {
            if (_ai.PlayerTarget == null) return Vector2.zero;
            if (!_ai.PlayerInDetectionRange()) return Vector2.zero;

            Vector2 toPlayer = _ai.DirectionToPlayer();
            return toPlayer * (data.MoveSpeed * 0.4f);
        }

        // ------------------------------------------------------------------ //
        //  Obstacle avoidance
        // ------------------------------------------------------------------ //

        private Vector2 ComputeAvoidance()
        {
            Vector2 result = Vector2.zero;
            Vector2 origin = transform.position;
            float halfSpread = avoidanceSpreadAngle * 0.5f;
            float baseAngle = Mathf.Atan2(_ai.Rb.linearVelocity.y, _ai.Rb.linearVelocity.x) * Mathf.Rad2Deg;

            for (int i = 0; i < avoidanceRayCount; i++)
            {
                float t = avoidanceRayCount > 1 ? (float)i / (avoidanceRayCount - 1) : 0.5f;
                float angle = baseAngle + Mathf.Lerp(-halfSpread, halfSpread, t);
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

                RaycastHit2D hit = Physics2D.Raycast(origin, dir, avoidanceRayLength, obstacleMask);
                if (hit.collider != null)
                {
                    float weight = 1f - (hit.distance / avoidanceRayLength);
                    result -= dir * (weight * avoidanceForce);
                }
            }

            return result;
        }

        // ------------------------------------------------------------------ //
        //  Dive bomb
        // ------------------------------------------------------------------ //

        private void TryInitiateDiveBomb()
        {
            if (_ai.PlayerTarget == null) return;
            if (Time.time - _lastDiveBombTime < diveBombCooldown) return;
            if (_ai.DistanceToPlayer() > diveBombTriggerRange) return;
            if (Random.value > diveBombChance * Time.fixedDeltaTime) return;

            // Only dive if we are above the player.
            if (transform.position.y <= _ai.PlayerTarget.position.y + 1f) return;

            _isDiveBombing = true;
            _diveBombTarget = _ai.PlayerTarget.position;
        }

        private void ExecuteDiveBomb()
        {
            Vector2 dir = (_diveBombTarget - (Vector2)transform.position).normalized;
            _ai.Rb.linearVelocity = dir * diveBombSpeed;

            // End dive when close to target or below minimum altitude.
            float dist = Vector2.Distance(transform.position, _diveBombTarget);
            if (dist < 0.5f || transform.position.y < _anchorPosition.y + diveBombMinAltitude)
            {
                _isDiveBombing = false;
                _lastDiveBombTime = Time.time;

                // Pull back up.
                _ai.Rb.linearVelocity = new Vector2(_ai.Rb.linearVelocity.x * 0.5f, data.MoveSpeed);
            }
        }

        // ------------------------------------------------------------------ //
        //  IAttacker – shoots downward toward the player
        // ------------------------------------------------------------------ //

        public void PerformAttack()
        {
            if (IsDead || _isDiveBombing) return;
            if (bulletPrefab == null || ObjectPool.Instance == null) return;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = _ai.PlayerTarget != null
                ? ((Vector2)_ai.PlayerTarget.position - (Vector2)spawnPos).normalized
                : Vector2.down;

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
        //  Collision damage
        // ------------------------------------------------------------------ //

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (IsDead) return;

            IDamageable target = collision.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(data.ContactDamage, collision.contacts[0].point);
            }
        }

        // ------------------------------------------------------------------ //
        //  Death
        // ------------------------------------------------------------------ //

        protected override void OnDeathInternal()
        {
            _isDiveBombing = false;
            _ai.Rb.gravityScale = 1.5f; // Tumble down on death.
        }
    }
}
