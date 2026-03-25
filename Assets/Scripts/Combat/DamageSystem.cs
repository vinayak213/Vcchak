using UnityEngine;

namespace RunAndGun
{
    public enum DamageType
    {
        Normal,
        Fire,
        Explosive,
        Electric
    }

    [System.Serializable]
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public GameObject source;
        public Vector2 hitPoint;
        public bool isCritical;
    }

    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }

        void TakeDamage(DamageInfo info);

        /// <summary>
        /// Legacy overload for backward compatibility with simple damage calls.
        /// </summary>
        void TakeDamage(float amount, Vector2 hitPoint);
    }

    /// <summary>
    /// Centralized damage processing utility. Handles critical hits, damage multipliers,
    /// and spawning damage number popups.
    /// </summary>
    public static class DamageProcessor
    {
        private static float criticalHitMultiplier = 2f;
        private static float criticalHitChance = 0.1f;

        private static GameObject damageNumberPrefab;

        /// <summary>
        /// Configure global critical hit parameters.
        /// </summary>
        public static void SetCriticalHitParams(float chance, float multiplier)
        {
            criticalHitChance = Mathf.Clamp01(chance);
            criticalHitMultiplier = Mathf.Max(1f, multiplier);
        }

        /// <summary>
        /// Set the prefab used for damage number popups.
        /// The prefab must have a DamageNumber component.
        /// </summary>
        public static void SetDamageNumberPrefab(GameObject prefab)
        {
            damageNumberPrefab = prefab;
        }

        /// <summary>
        /// Build a DamageInfo with optional critical hit roll.
        /// </summary>
        public static DamageInfo BuildDamage(float baseDamage, DamageType type, GameObject source,
            Vector2 hitPoint, bool allowCritical = true)
        {
            bool isCritical = allowCritical && Random.value < criticalHitChance;
            float finalDamage = isCritical ? baseDamage * criticalHitMultiplier : baseDamage;

            return new DamageInfo
            {
                amount = finalDamage,
                type = type,
                source = source,
                hitPoint = hitPoint,
                isCritical = isCritical
            };
        }

        /// <summary>
        /// Apply damage to a target and spawn a damage number popup.
        /// </summary>
        public static void ApplyDamage(IDamageable target, DamageInfo info)
        {
            if (target == null || !target.IsAlive) return;

            target.TakeDamage(info);
            SpawnDamageNumber(info);
        }

        /// <summary>
        /// Try to find an IDamageable on the given collider or its parent and apply damage.
        /// Returns true if damage was applied.
        /// </summary>
        public static bool TryApplyDamage(Collider2D collider, DamageInfo info)
        {
            if (collider == null) return false;

            IDamageable damageable = collider.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null || !damageable.IsAlive) return false;

            damageable.TakeDamage(info);
            SpawnDamageNumber(info);
            return true;
        }

        /// <summary>
        /// Apply area damage to all IDamageable targets within a radius.
        /// </summary>
        public static int ApplyAreaDamage(Vector2 center, float radius, float baseDamage,
            DamageType type, GameObject source, LayerMask affectedLayers, bool allowCritical = true)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, affectedLayers);
            int hitCount = 0;

            foreach (Collider2D hit in hits)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = hit.GetComponentInParent<IDamageable>();
                }

                if (damageable == null || !damageable.IsAlive) continue;

                // Damage falloff based on distance from center
                float distance = Vector2.Distance(center, hit.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / radius);
                float scaledDamage = baseDamage * falloff;

                DamageInfo info = BuildDamage(scaledDamage, type, source,
                    hit.ClosestPoint(center), allowCritical);

                damageable.TakeDamage(info);
                SpawnDamageNumber(info);
                hitCount++;
            }

            return hitCount;
        }

        /// <summary>
        /// Get a damage type multiplier for elemental interactions.
        /// Override this logic for custom weakness/resistance systems.
        /// </summary>
        public static float GetTypeMultiplier(DamageType attackType, DamageType resistanceType)
        {
            // Fire vs Electric = bonus, Explosive = neutral to all, etc.
            if (attackType == resistanceType) return 0.5f; // Resistance
            if (attackType == DamageType.Explosive) return 1.25f; // Explosive is strong vs all

            return 1f;
        }

        private static void SpawnDamageNumber(DamageInfo info)
        {
            if (damageNumberPrefab == null) return;

            Vector3 spawnPos = new Vector3(info.hitPoint.x, info.hitPoint.y, 0f);
            spawnPos += new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, 0f);

            GameObject numberObj;
            if (ObjectPool.Instance != null)
            {
                numberObj = ObjectPool.Instance.Get(damageNumberPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                numberObj = Object.Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
            }

            DamageNumber damageNumber = numberObj.GetComponent<DamageNumber>();
            if (damageNumber != null)
            {
                damageNumber.Show(info.amount, info.isCritical, info.type);
            }
        }
    }

    /// <summary>
    /// Attach to a UI/World Space text object to display floating damage numbers.
    /// Works with TextMesh or can be extended for TextMeshPro.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float fadeStartTime = 0.4f;
        [SerializeField] private float scaleUpOnCritical = 1.5f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalColor = Color.yellow;
        [SerializeField] private Color fireColor = new Color(1f, 0.4f, 0f);
        [SerializeField] private Color explosiveColor = new Color(1f, 0.6f, 0f);
        [SerializeField] private Color electricColor = new Color(0.3f, 0.8f, 1f);

        private TextMesh textMesh;
        private float timer;
        private Color currentColor;
        private Vector3 baseScale;

        private void Awake()
        {
            textMesh = GetComponent<TextMesh>();
            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMesh>();
            }

            baseScale = transform.localScale;
        }

        public void Show(float damage, bool isCritical, DamageType type)
        {
            timer = 0f;

            if (textMesh != null)
            {
                textMesh.text = Mathf.RoundToInt(damage).ToString();
            }

            currentColor = type switch
            {
                DamageType.Fire => fireColor,
                DamageType.Explosive => explosiveColor,
                DamageType.Electric => electricColor,
                _ => isCritical ? criticalColor : normalColor
            };

            if (textMesh != null)
            {
                textMesh.color = currentColor;
            }

            transform.localScale = isCritical ? baseScale * scaleUpOnCritical : baseScale;
        }

        private void Update()
        {
            timer += Time.deltaTime;

            // Float upward
            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

            // Fade out
            if (timer >= fadeStartTime && textMesh != null)
            {
                float fadeProgress = (timer - fadeStartTime) / (lifetime - fadeStartTime);
                Color c = currentColor;
                c.a = Mathf.Lerp(1f, 0f, fadeProgress);
                textMesh.color = c;
            }

            if (timer >= lifetime)
            {
                PoolMember poolMember = GetComponent<PoolMember>();
                if (poolMember != null)
                {
                    poolMember.ReturnToPool();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnEnable()
        {
            timer = 0f;
        }
    }
}
