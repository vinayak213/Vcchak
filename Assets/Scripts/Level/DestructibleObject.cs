using UnityEngine;
using UnityEngine.Events;

namespace RunAndGun
{
    [RequireComponent(typeof(Collider2D))]
    public class DestructibleObject : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 30f;

        [Header("Visual Damage Stages")]
        [Tooltip("Sprites for progressive damage. Index 0 = full health, last = nearly broken.")]
        [SerializeField] private Sprite[] damageStageSprites;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Destruction Effects")]
        [SerializeField] private GameObject destroyParticlePrefab;
        [SerializeField] private AudioClip destroySound;
        [SerializeField] private AudioClip hitSound;

        [Header("Drops")]
        [SerializeField] private DropEntry[] drops;

        [Header("Events")]
        [SerializeField] private UnityEvent onDestroyed;

        private float currentHealth;
        private bool isDestroyed;

        [System.Serializable]
        public struct DropEntry
        {
            public GameObject prefab;
            [Range(0f, 1f)] public float chance;
            public int minCount;
            public int maxCount;
        }

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => !isDestroyed;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            if (isDestroyed) return;

            currentHealth -= info.amount;

            PlayHitEffect();
            UpdateDamageVisual();

            if (currentHealth <= 0f)
            {
                Destroy();
            }
        }

        public void TakeDamage(float amount, Vector2 hitPoint)
        {
            TakeDamage(new DamageInfo
            {
                amount = amount,
                type = DamageType.Normal,
                source = null,
                hitPoint = hitPoint,
                isCritical = false
            });
        }

        private void UpdateDamageVisual()
        {
            if (damageStageSprites == null || damageStageSprites.Length == 0) return;
            if (spriteRenderer == null) return;

            float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
            // Map health percentage to sprite index: full health = 0, near death = last index
            int index = Mathf.FloorToInt((1f - healthPercent) * damageStageSprites.Length);
            index = Mathf.Clamp(index, 0, damageStageSprites.Length - 1);

            if (damageStageSprites[index] != null)
            {
                spriteRenderer.sprite = damageStageSprites[index];
            }
        }

        private void PlayHitEffect()
        {
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position);
            }

            // Flash white briefly
            if (spriteRenderer != null)
            {
                StartCoroutine(FlashRoutine());
            }
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            if (spriteRenderer == null) yield break;

            Color original = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = original;
            }
        }

        private void Destroy()
        {
            if (isDestroyed) return;
            isDestroyed = true;

            // Spawn particles
            if (destroyParticlePrefab != null)
            {
                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Get(destroyParticlePrefab, transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(destroyParticlePrefab, transform.position, Quaternion.identity);
                }
            }

            // Play sound
            if (destroySound != null)
            {
                AudioSource.PlayClipAtPoint(destroySound, transform.position);
            }

            // Drop items
            SpawnDrops();

            onDestroyed?.Invoke();

            Destroy(gameObject);
        }

        private void SpawnDrops()
        {
            if (drops == null) return;

            foreach (DropEntry drop in drops)
            {
                if (drop.prefab == null) continue;
                if (Random.value > drop.chance) continue;

                int count = Random.Range(drop.minCount, drop.maxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    Vector2 scatter = Random.insideUnitCircle * 0.5f;
                    Vector3 spawnPos = transform.position + new Vector3(scatter.x, scatter.y, 0f);

                    GameObject spawned;
                    if (ObjectPool.Instance != null)
                    {
                        spawned = ObjectPool.Instance.Get(drop.prefab, spawnPos, Quaternion.identity);
                    }
                    else
                    {
                        spawned = Instantiate(drop.prefab, spawnPos, Quaternion.identity);
                    }

                    // Give a small upward impulse
                    Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        Vector2 force = new(Random.Range(-2f, 2f), Random.Range(2f, 5f));
                        rb.linearVelocity = force;
                    }
                }
            }
        }
    }
}
