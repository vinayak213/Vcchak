using System.Collections;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Ground-based soldier enemy. Patrols between waypoints, shoots at the
    /// player when in range, occasionally takes cover, and charges when the
    /// player is very close.
    /// </summary>
    [RequireComponent(typeof(EnemyAI))]
    public sealed class GroundEnemy : EnemyBase, IAttacker
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //
        [Header("Ground Enemy – Shooting")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject bulletPrefab;

        [Header("Ground Enemy – Cover")]
        [Tooltip("Probability (0-1) that the enemy ducks into cover after attacking.")]
        [SerializeField, Range(0f, 1f)] private float coverChance = 0.25f;
        [SerializeField] private float coverDuration = 1.2f;
        [SerializeField] private Vector2 coverCrouchOffset = new Vector2(0f, -0.3f);

        [Header("Ground Enemy – Charge")]
        [Tooltip("Distance at which the enemy charges the player instead of shooting.")]
        [SerializeField] private float chargeDistance = 2.5f;
        [SerializeField] private float chargeSpeedMultiplier = 1.6f;

        [Header("Ground Enemy – Audio")]
        [SerializeField] private AudioClip fireSound;
        [SerializeField] private AudioSource audioSource;

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //
        private EnemyAI _ai;
        private bool _isTakingCover;
        private Vector3 _originalLocalPosition;
        private Coroutine _coverRoutine;

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
            _isTakingCover = false;
            _originalLocalPosition = Vector3.zero;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_coverRoutine != null)
            {
                StopCoroutine(_coverRoutine);
                _coverRoutine = null;
            }
        }

        private void FixedUpdate()
        {
            if (IsDead || _ai.PlayerTarget == null) return;

            float dist = _ai.DistanceToPlayer();

            // If the player is very close and we have line of sight, charge.
            if (dist <= chargeDistance && _ai.HasLineOfSight() && !_isTakingCover)
            {
                ChargeTowardPlayer();
            }
        }

        // ------------------------------------------------------------------ //
        //  IAttacker
        // ------------------------------------------------------------------ //

        public void PerformAttack()
        {
            if (IsDead) return;

            if (_isTakingCover) return;

            FireBullet();

            // Occasionally duck into cover after shooting.
            if (Random.value <= coverChance)
            {
                _coverRoutine = StartCoroutine(TakeCoverRoutine());
            }
        }

        // ------------------------------------------------------------------ //
        //  Combat helpers
        // ------------------------------------------------------------------ //

        private void FireBullet()
        {
            if (bulletPrefab == null || ObjectPool.Instance == null) return;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 direction = _ai.DirectionToPlayer();

            GameObject obj = ObjectPool.Instance.Get(bulletPrefab, spawnPos, Quaternion.identity);
            EnemyBullet bullet = obj.GetComponent<EnemyBullet>();

            if (bullet != null)
            {
                bullet.Initialize(direction, data.ProjectileSpeed, data.ProjectileDamage, bulletPrefab);
            }

            PlayFireSound();
        }

        private void PlayFireSound()
        {
            if (fireSound == null || audioSource == null) return;
            audioSource.PlayOneShot(fireSound);
        }

        private void ChargeTowardPlayer()
        {
            _ai.FacePlayer();
            Vector2 dir = _ai.DirectionToPlayer();
            float speed = data.MoveSpeed * chargeSpeedMultiplier;
            _ai.Rb.linearVelocity = new Vector2(dir.x * speed, _ai.Rb.linearVelocity.y);
        }

        // ------------------------------------------------------------------ //
        //  Cover behaviour
        // ------------------------------------------------------------------ //

        private IEnumerator TakeCoverRoutine()
        {
            _isTakingCover = true;

            // Simulate crouching by shifting the sprite down.
            _originalLocalPosition = SpriteRenderer.transform.localPosition;
            SpriteRenderer.transform.localPosition = _originalLocalPosition + (Vector3)coverCrouchOffset;

            // Reduce the collider height while crouching (optional visual cue).
            yield return new WaitForSeconds(coverDuration);

            SpriteRenderer.transform.localPosition = _originalLocalPosition;
            _isTakingCover = false;
            _coverRoutine = null;
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
        //  Death override
        // ------------------------------------------------------------------ //

        protected override void OnDeathInternal()
        {
            if (_coverRoutine != null)
            {
                StopCoroutine(_coverRoutine);
                _coverRoutine = null;
            }
            _isTakingCover = false;
        }
    }
}
