using UnityEngine;

namespace RunAndGun
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserWeapon : WeaponBase
    {
        [Header("Laser Settings")]
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float damagePerSecond = 40f;
        [SerializeField] private float heatBuildRate = 25f;
        [SerializeField] private float heatDissipateRate = 15f;
        [SerializeField] private float maxHeat = 100f;
        [SerializeField] private float overheatCooldownDuration = 2f;
        [SerializeField] private LayerMask hitLayers;
        [SerializeField] private float laserWidth = 0.1f;
        [SerializeField] private Color laserColor = Color.red;

        private LineRenderer lineRenderer;
        private bool isFiring;
        private float currentHeat;
        private bool isOverheated;
        private float overheatTimer;

        public float CurrentHeat => currentHeat;
        public float MaxHeat => maxHeat;
        public bool IsOverheated => isOverheated;
        public float HeatPercent => currentHeat / maxHeat;

        public event System.Action OnOverheat;
        public event System.Action OnCooldownComplete;

        protected override void Awake()
        {
            base.Awake();

            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth * 0.5f;
            lineRenderer.startColor = laserColor;
            lineRenderer.endColor = laserColor;
            lineRenderer.enabled = false;
        }

        private void Update()
        {
            if (isOverheated)
            {
                HandleOverheatCooldown();
                return;
            }

            if (!isFiring)
            {
                DissipateHeat();
                lineRenderer.enabled = false;
            }

            isFiring = false;
        }

        public void FireContinuous(Vector2 direction)
        {
            if (isOverheated) return;
            if (direction == Vector2.zero) direction = Vector2.right;

            direction.Normalize();
            isFiring = true;

            PlayContinuousSound();
            UpdateLaser(direction);
            BuildHeat();
        }

        // Override Fire to work as a single-frame trigger; prefer FireContinuous for hold-to-fire.
        public override void Fire(Vector2 direction)
        {
            FireContinuous(direction);
        }

        // Laser does not spawn bullets.
        protected override void SpawnBullets(Vector2 direction) { }

        private void UpdateLaser(Vector2 direction)
        {
            lineRenderer.enabled = true;

            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxDistance, hitLayers);

            Vector3 endPoint;
            if (hit.collider != null)
            {
                endPoint = hit.point;
                ApplyLaserDamage(hit.collider);
            }
            else
            {
                endPoint = origin + (Vector3)(direction * maxDistance);
            }

            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        private void ApplyLaserDamage(Collider2D target)
        {
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damagePerSecond * Time.deltaTime, target.ClosestPoint(transform.position));
            }
        }

        private void BuildHeat()
        {
            currentHeat += heatBuildRate * Time.deltaTime;

            if (currentHeat >= maxHeat)
            {
                currentHeat = maxHeat;
                isOverheated = true;
                overheatTimer = overheatCooldownDuration;
                lineRenderer.enabled = false;
                isFiring = false;
                StopFiringSound();
                OnOverheat?.Invoke();
            }
        }

        private void DissipateHeat()
        {
            if (currentHeat > 0f)
            {
                currentHeat = Mathf.Max(0f, currentHeat - heatDissipateRate * Time.deltaTime);
            }
        }

        private void HandleOverheatCooldown()
        {
            lineRenderer.enabled = false;

            overheatTimer -= Time.deltaTime;
            currentHeat = Mathf.Lerp(0f, maxHeat, overheatTimer / overheatCooldownDuration);

            if (overheatTimer <= 0f)
            {
                isOverheated = false;
                currentHeat = 0f;
                OnCooldownComplete?.Invoke();
            }
        }

        private void PlayContinuousSound()
        {
            if (audioSource == null || weaponData.FireSoundEffect == null) return;

            if (!audioSource.isPlaying)
            {
                audioSource.clip = weaponData.FireSoundEffect;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void StopFiringSound()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            lineRenderer.enabled = false;
            StopFiringSound();
            isFiring = false;
        }
    }
}
