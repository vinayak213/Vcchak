using System;
using System.Collections;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Multi-phase boss enemy with spread shot, charge, minion summon, and laser
    /// sweep attacks. Transitions through at least 3 phases based on health
    /// thresholds, with vulnerability windows between attacks.
    /// </summary>
    public sealed class BossEnemy : EnemyBase, IAttacker
    {
        // ================================================================== //
        //  ENUMS
        // ================================================================== //
        private enum BossPhase { Entrance, Phase1, Phase2, Phase3 }

        private enum BossAttack { SpreadShot, Charge, SummonMinions, LaserSweep }

        // ================================================================== //
        //  INSPECTOR
        // ================================================================== //

        // ---- General ----------------------------------------------------- //
        [Header("Boss – General")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private float actionCooldown = 2f;
        [SerializeField] private float vulnerabilityDuration = 1.5f;

        // ---- Phase thresholds -------------------------------------------- //
        [Header("Boss – Phase Thresholds (% health)")]
        [SerializeField, Range(0f, 1f)] private float phase2Threshold = 0.65f;
        [SerializeField, Range(0f, 1f)] private float phase3Threshold = 0.30f;

        // ---- Entrance ---------------------------------------------------- //
        [Header("Boss – Entrance")]
        [SerializeField] private Vector2 entranceLandPosition;
        [SerializeField] private float entranceFallSpeed = 6f;
        [SerializeField] private float entranceShakeDuration = 0.3f;

        // ---- Spread Shot ------------------------------------------------- //
        [Header("Boss – Spread Shot")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private int spreadBulletCount = 7;
        [SerializeField] private float spreadAngle = 120f;
        [SerializeField] private float spreadBulletSpeed = 8f;
        [SerializeField] private int spreadDamage = 1;
        [SerializeField] private int spreadWaves = 2;
        [SerializeField] private float spreadWaveDelay = 0.3f;

        // ---- Charge ------------------------------------------------------ //
        [Header("Boss – Charge Attack")]
        [SerializeField] private float chargeSpeed = 14f;
        [SerializeField] private float chargeWindUp = 0.5f;
        [SerializeField] private float chargeDuration = 0.8f;
        [SerializeField] private int chargeDamage = 2;
        [SerializeField] private LayerMask wallMask;

        // ---- Summon Minions ---------------------------------------------- //
        [Header("Boss – Summon Minions")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private Transform[] minionSpawnPoints;
        [SerializeField] private int minionsPerSummon = 3;
        [SerializeField] private float minionSpawnDelay = 0.25f;

        // ---- Laser Sweep ------------------------------------------------- //
        [Header("Boss – Laser Sweep")]
        [SerializeField] private LineRenderer laserLineRenderer;
        [SerializeField] private float laserWindUp = 1f;
        [SerializeField] private float laserSweepArc = 160f;
        [SerializeField] private float laserSweepDuration = 2f;
        [SerializeField] private float laserRange = 15f;
        [SerializeField] private int laserDamage = 2;
        [SerializeField] private float laserDamageInterval = 0.25f;
        [SerializeField] private float laserWidth = 0.15f;
        [SerializeField] private LayerMask laserHitMask;

        // ---- Health bar -------------------------------------------------- //
        [Header("Boss – UI")]
        [SerializeField] private GameObject healthBarUI;

        // ---- Audio ------------------------------------------------------- //
        [Header("Boss – Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip entranceSfx;
        [SerializeField] private AudioClip spreadShotSfx;
        [SerializeField] private AudioClip chargeSfx;
        [SerializeField] private AudioClip summonSfx;
        [SerializeField] private AudioClip laserSfx;
        [SerializeField] private AudioClip phaseTransitionSfx;

        // ================================================================== //
        //  RUNTIME STATE
        // ================================================================== //
        private BossPhase _currentPhase;
        private Rigidbody2D _rb;
        private Coroutine _actionRoutine;
        private bool _isVulnerable;
        private bool _isActing;
        private float _lastActionTime;
        private bool _entranceDone;
        private int _attackIndex;

        // ================================================================== //
        //  UNITY LIFECYCLE
        // ================================================================== //
        protected override void Awake()
        {
            base.Awake();
            _rb = GetComponent<Rigidbody2D>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _currentPhase = BossPhase.Entrance;
            _isVulnerable = false;
            _isActing = false;
            _entranceDone = false;
            _lastActionTime = Time.time;
            _attackIndex = 0;

            SetLaserActive(false);

            if (healthBarUI != null)
                healthBarUI.SetActive(false);

            OnDamaged += HandleDamaged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            OnDamaged -= HandleDamaged;

            if (_actionRoutine != null)
            {
                StopCoroutine(_actionRoutine);
                _actionRoutine = null;
            }
        }

        private void Start()
        {
            if (playerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTarget = player.transform;
            }

            _actionRoutine = StartCoroutine(EntranceRoutine());
        }

        private void Update()
        {
            if (IsDead || !_entranceDone) return;
            if (_isActing) return;

            if (Time.time - _lastActionTime >= actionCooldown)
            {
                _actionRoutine = StartCoroutine(PerformNextAction());
            }
        }

        // ================================================================== //
        //  ENTRANCE
        // ================================================================== //

        private IEnumerator EntranceRoutine()
        {
            // Fall from above.
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.down * entranceFallSpeed;

            while ((Vector2)transform.position != entranceLandPosition)
            {
                if (transform.position.y <= entranceLandPosition.y)
                {
                    transform.position = (Vector3)entranceLandPosition;
                    _rb.linearVelocity = Vector2.zero;
                    break;
                }
                yield return null;
            }

            // Camera shake placeholder — trigger via event.
            PlaySound(entranceSfx);
            yield return new WaitForSeconds(entranceShakeDuration);

            // Show health bar.
            if (healthBarUI != null)
                healthBarUI.SetActive(true);

            _currentPhase = BossPhase.Phase1;
            _entranceDone = true;
            _lastActionTime = Time.time;
        }

        // ================================================================== //
        //  PHASE EVALUATION
        // ================================================================== //

        private void HandleDamaged(int damageDealt, int remainingHealth)
        {
            float healthPercent = (float)remainingHealth / data.MaxHealth;

            if (_currentPhase == BossPhase.Phase1 && healthPercent <= phase2Threshold)
            {
                TransitionToPhase(BossPhase.Phase2);
            }
            else if (_currentPhase == BossPhase.Phase2 && healthPercent <= phase3Threshold)
            {
                TransitionToPhase(BossPhase.Phase3);
            }
        }

        private void TransitionToPhase(BossPhase newPhase)
        {
            if (_actionRoutine != null)
            {
                StopCoroutine(_actionRoutine);
                _actionRoutine = null;
            }

            _isActing = false;
            SetLaserActive(false);

            _currentPhase = newPhase;
            _attackIndex = 0;
            PlaySound(phaseTransitionSfx);

            // Brief invulnerability flash for dramatic effect.
            _actionRoutine = StartCoroutine(PhaseTransitionRoutine());
        }

        private IEnumerator PhaseTransitionRoutine()
        {
            _isActing = true;

            // Flash rapidly to signal the phase shift.
            Color orig = SpriteRenderer.color;
            for (int i = 0; i < 6; i++)
            {
                SpriteRenderer.color = Color.red;
                yield return new WaitForSeconds(0.08f);
                SpriteRenderer.color = orig;
                yield return new WaitForSeconds(0.08f);
            }

            _isActing = false;
            _lastActionTime = Time.time;
        }

        // ================================================================== //
        //  ACTION SEQUENCER
        // ================================================================== //

        public void PerformAttack()
        {
            if (IsDead || _isActing || !_entranceDone) return;
            _actionRoutine = StartCoroutine(PerformNextAction());
        }

        private IEnumerator PerformNextAction()
        {
            _isActing = true;
            BossAttack attack = PickAttack();

            FacePlayer();

            switch (attack)
            {
                case BossAttack.SpreadShot:
                    yield return SpreadShotRoutine();
                    break;
                case BossAttack.Charge:
                    yield return ChargeRoutine();
                    break;
                case BossAttack.SummonMinions:
                    yield return SummonMinionsRoutine();
                    break;
                case BossAttack.LaserSweep:
                    yield return LaserSweepRoutine();
                    break;
            }

            // Vulnerability window.
            _isVulnerable = true;
            yield return new WaitForSeconds(vulnerabilityDuration);
            _isVulnerable = false;

            _isActing = false;
            _lastActionTime = Time.time;
            _actionRoutine = null;
        }

        private BossAttack PickAttack()
        {
            BossAttack[] pool;

            switch (_currentPhase)
            {
                case BossPhase.Phase1:
                    pool = new[] { BossAttack.SpreadShot, BossAttack.Charge };
                    break;
                case BossPhase.Phase2:
                    pool = new[] { BossAttack.SpreadShot, BossAttack.Charge, BossAttack.SummonMinions };
                    break;
                case BossPhase.Phase3:
                default:
                    pool = new[]
                    {
                        BossAttack.SpreadShot, BossAttack.Charge,
                        BossAttack.SummonMinions, BossAttack.LaserSweep
                    };
                    break;
            }

            BossAttack chosen = pool[_attackIndex % pool.Length];
            _attackIndex++;
            return chosen;
        }

        // ================================================================== //
        //  ATTACKS
        // ================================================================== //

        // ---- Spread Shot ------------------------------------------------- //
        private IEnumerator SpreadShotRoutine()
        {
            PlaySound(spreadShotSfx);

            for (int wave = 0; wave < spreadWaves; wave++)
            {
                FireSpread();

                if (wave < spreadWaves - 1)
                    yield return new WaitForSeconds(spreadWaveDelay);
            }

            yield return new WaitForSeconds(0.2f);
        }

        private void FireSpread()
        {
            if (bulletPrefab == null || ObjectPool.Instance == null) return;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 baseDir = playerTarget != null
                ? ((Vector2)playerTarget.position - (Vector2)spawnPos).normalized
                : FacingDirection();

            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float halfArc = spreadAngle * 0.5f;
            float step = spreadBulletCount > 1 ? spreadAngle / (spreadBulletCount - 1) : 0f;

            for (int i = 0; i < spreadBulletCount; i++)
            {
                float angle = baseAngle - halfArc + step * i;
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

                GameObject obj = ObjectPool.Instance.Get(bulletPrefab, spawnPos, Quaternion.identity);
                EnemyBullet bullet = obj.GetComponent<EnemyBullet>();
                if (bullet != null)
                {
                    bullet.Initialize(dir, spreadBulletSpeed, spreadDamage, bulletPrefab);
                }
            }
        }

        // ---- Charge ------------------------------------------------------ //
        private IEnumerator ChargeRoutine()
        {
            PlaySound(chargeSfx);

            // Wind-up telegraph.
            Color orig = SpriteRenderer.color;
            SpriteRenderer.color = Color.yellow;
            yield return new WaitForSeconds(chargeWindUp);
            SpriteRenderer.color = orig;

            // Charge toward player's current position.
            Vector2 chargeDir = playerTarget != null
                ? ((Vector2)playerTarget.position - (Vector2)transform.position).normalized
                : FacingDirection();

            float elapsed = 0f;
            while (elapsed < chargeDuration)
            {
                _rb.linearVelocity = chargeDir * chargeSpeed;

                // Hit a wall -> stop early.
                RaycastHit2D hit = Physics2D.Raycast(transform.position, chargeDir, 0.6f, wallMask);
                if (hit.collider != null)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            _rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.15f);
        }

        // ---- Summon Minions ---------------------------------------------- //
        private IEnumerator SummonMinionsRoutine()
        {
            PlaySound(summonSfx);

            int spawnCount = Mathf.Min(minionsPerSummon,
                minionSpawnPoints != null ? minionSpawnPoints.Length : 0);

            for (int i = 0; i < spawnCount; i++)
            {
                if (minionPrefab == null) continue;

                Vector3 pos = minionSpawnPoints[i].position;

                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Get(minionPrefab, pos, Quaternion.identity);
                }
                else
                {
                    Instantiate(minionPrefab, pos, Quaternion.identity);
                }

                yield return new WaitForSeconds(minionSpawnDelay);
            }

            yield return new WaitForSeconds(0.3f);
        }

        // ---- Laser Sweep ------------------------------------------------- //
        private IEnumerator LaserSweepRoutine()
        {
            PlaySound(laserSfx);
            SetLaserActive(true);

            // Wind-up: narrow beam, charge glow.
            if (laserLineRenderer != null)
            {
                laserLineRenderer.startWidth = 0.02f;
                laserLineRenderer.endWidth = 0.02f;
            }

            float windUpTimer = 0f;
            while (windUpTimer < laserWindUp)
            {
                UpdateLaserAim(FacingAngle());
                windUpTimer += Time.deltaTime;
                yield return null;
            }

            // Full-width sweep.
            if (laserLineRenderer != null)
            {
                laserLineRenderer.startWidth = laserWidth;
                laserLineRenderer.endWidth = laserWidth;
            }

            float sweepTimer = 0f;
            float startAngle = FacingAngle() + laserSweepArc * 0.5f;
            float lastDamageTime = 0f;

            while (sweepTimer < laserSweepDuration)
            {
                float t = sweepTimer / laserSweepDuration;
                float currentAngle = startAngle - laserSweepArc * t;

                UpdateLaserAim(currentAngle);

                // Damage anything the laser hits.
                if (Time.time - lastDamageTime >= laserDamageInterval)
                {
                    LaserDamageCheck(currentAngle);
                    lastDamageTime = Time.time;
                }

                sweepTimer += Time.deltaTime;
                yield return null;
            }

            SetLaserActive(false);
            yield return new WaitForSeconds(0.2f);
        }

        private void UpdateLaserAim(float angleDeg)
        {
            if (laserLineRenderer == null) return;

            Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 dir = new Vector2(
                Mathf.Cos(angleDeg * Mathf.Deg2Rad),
                Mathf.Sin(angleDeg * Mathf.Deg2Rad)
            );

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, laserRange, laserHitMask);
            Vector2 endPoint = hit.collider != null ? hit.point : origin + dir * laserRange;

            laserLineRenderer.SetPosition(0, origin);
            laserLineRenderer.SetPosition(1, endPoint);
        }

        private void LaserDamageCheck(float angleDeg)
        {
            Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 dir = new Vector2(
                Mathf.Cos(angleDeg * Mathf.Deg2Rad),
                Mathf.Sin(angleDeg * Mathf.Deg2Rad)
            );

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, laserRange, laserHitMask);
            if (hit.collider != null)
            {
                IDamageable target = hit.collider.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(laserDamage, hit.point);
                }
            }
        }

        private void SetLaserActive(bool active)
        {
            if (laserLineRenderer != null)
                laserLineRenderer.enabled = active;
        }

        // ================================================================== //
        //  HELPERS
        // ================================================================== //

        private void FacePlayer()
        {
            if (playerTarget == null) return;
            float dir = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(dir) > 0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dir);
                transform.localScale = scale;
            }
        }

        private Vector2 FacingDirection()
        {
            return new Vector2(Mathf.Sign(transform.localScale.x), 0f);
        }

        private float FacingAngle()
        {
            return transform.localScale.x >= 0f ? 0f : 180f;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip);
        }

        /// <summary>Whether the boss is currently in a vulnerability window.</summary>
        public bool IsVulnerable => _isVulnerable;

        /// <summary>Current phase index (1-3).</summary>
        public int CurrentPhaseNumber
        {
            get
            {
                switch (_currentPhase)
                {
                    case BossPhase.Phase1: return 1;
                    case BossPhase.Phase2: return 2;
                    case BossPhase.Phase3: return 3;
                    default: return 0;
                }
            }
        }

        // ================================================================== //
        //  CONTACT DAMAGE
        // ================================================================== //

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (IsDead) return;

            // During charge, deal charge-specific damage.
            int dmg = _isActing ? chargeDamage : data.ContactDamage;

            IDamageable target = collision.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(dmg, collision.contacts[0].point);
            }
        }

        // ================================================================== //
        //  DEATH
        // ================================================================== //

        protected override void OnDeathInternal()
        {
            if (_actionRoutine != null)
            {
                StopCoroutine(_actionRoutine);
                _actionRoutine = null;
            }

            StopAllCoroutines();
            SetLaserActive(false);
            _rb.linearVelocity = Vector2.zero;

            if (healthBarUI != null)
                healthBarUI.SetActive(false);

            // Award score.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(data.ScoreValue);
            }
        }
    }
}
