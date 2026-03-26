using UnityEngine;

namespace RunAndGun
{
    public class ExplosiveBullet : Bullet
    {
        [Header("Explosion Defaults")]
        [SerializeField] private float defaultExplosionRadius = 3f;
        [SerializeField] private float defaultExplosionDamage = 50f;
        [SerializeField] private float defaultDamageMinPercent = 0.2f;
        [SerializeField] private GameObject defaultExplosionEffect;
        [SerializeField] private LayerMask explosionHitLayers;
        [SerializeField] private float screenShakeIntensity = 0.4f;
        [SerializeField] private float screenShakeDuration = 0.2f;

        private float explosionRadius;
        private float explosionDamage;
        private float damageMinPercent;
        private GameObject explosionEffectPrefab;

        protected override void Awake()
        {
            base.Awake();
            ResetToDefaults();
        }

        private void OnEnable()
        {
            ResetToDefaults();
        }

        private void ResetToDefaults()
        {
            explosionRadius = defaultExplosionRadius;
            explosionDamage = defaultExplosionDamage;
            damageMinPercent = defaultDamageMinPercent;
            explosionEffectPrefab = defaultExplosionEffect;
        }

        public void SetExplosionParameters(float radius, float damage, float minPercent, GameObject effectPrefab)
        {
            explosionRadius = radius;
            explosionDamage = damage;
            damageMinPercent = minPercent;

            if (effectPrefab != null)
            {
                explosionEffectPrefab = effectPrefab;
            }
        }

        // Bypass single-target damage from Bullet; the explosion handles all damage.
        protected override void ApplyDamage(Collider2D target) { }

        protected override void OnHit(Collider2D target)
        {
            Explode();
        }

        private void Explode()
        {
            Vector2 center = transform.position;

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius, explosionHitLayers);

            foreach (Collider2D hit in hits)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) continue;

                Vector2 closestPoint = hit.ClosestPoint(center);
                float distance = Vector2.Distance(center, closestPoint);
                float normalizedDistance = Mathf.Clamp01(distance / explosionRadius);

                // Linear falloff from full damage at center to damageMinPercent at edge
                float falloff = Mathf.Lerp(1f, damageMinPercent, normalizedDistance);
                float finalDamage = explosionDamage * falloff;

                damageable.TakeDamage(finalDamage, closestPoint);
            }

            SpawnExplosionEffect(center);
            TriggerScreenShake();
        }

        private void SpawnExplosionEffect(Vector2 position)
        {
            if (explosionEffectPrefab == null || ObjectPool.Instance == null) return;

            GameObject effect = ObjectPool.Instance.Get(explosionEffectPrefab, position, Quaternion.identity);

            // Auto-return explosion effect after particle system finishes
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ExplosionEffectReturn returnComp = effect.GetComponent<ExplosionEffectReturn>();
                if (returnComp == null)
                {
                    returnComp = effect.AddComponent<ExplosionEffectReturn>();
                }
                returnComp.Begin(explosionEffectPrefab, ps.main.duration + ps.main.startLifetime.constantMax);
            }
        }

        private void TriggerScreenShake()
        {
            if (Camera.main == null) return;

            ScreenShake shake = Camera.main.GetComponent<ScreenShake>();
            if (shake != null)
            {
                shake.Shake(screenShakeIntensity, screenShakeDuration);
            }
        }
    }

    public class ExplosionEffectReturn : MonoBehaviour
    {
        private float timer;
        private float duration;
        private GameObject prefabRef;
        private bool active;

        public void Begin(GameObject prefab, float dur)
        {
            prefabRef = prefab;
            duration = dur;
            timer = 0f;
            active = true;
        }

        private void Update()
        {
            if (!active) return;

            timer += Time.deltaTime;
            if (timer >= duration)
            {
                active = false;
                if (ObjectPool.Instance != null && prefabRef != null)
                {
                    ObjectPool.Instance.Return(prefabRef, gameObject);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }

}
