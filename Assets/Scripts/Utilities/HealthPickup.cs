using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Health restore pickup item. Heals the player on contact
    /// with visual indicators and collection effects.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HealthPickup : MonoBehaviour
    {
        [Header("Healing")]
        [SerializeField] private float healAmount = 25f;
        [SerializeField] private bool healPercentage;
        [Tooltip("If healPercentage is true, healAmount is treated as a 0-100 percentage of max health.")]

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobFrequency = 1.5f;
        [SerializeField] private float glowPulseSpeed = 2f;
        [SerializeField] private Color baseColor = Color.green;
        [SerializeField] private Color glowColor = new Color(0.5f, 1f, 0.5f);

        [Header("Lifetime")]
        [SerializeField] private float despawnTime = 20f;
        [SerializeField] private float flashStartTime = 16f;

        [Header("Collection Effect")]
        [SerializeField] private GameObject collectEffectPrefab;
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private float collectSoundVolume = 0.7f;

        [Header("Behavior")]
        [SerializeField] private bool onlyPickupWhenDamaged = true;
        [SerializeField] private bool destroyOnCollect = true;
        [SerializeField] private float respawnTime = 10f;

        private Vector3 startPosition;
        private float animationOffset;
        private float spawnTime;
        private bool collected;
        private Collider2D pickupCollider;

        private void Awake()
        {
            pickupCollider = GetComponent<Collider2D>();
            pickupCollider.isTrigger = true;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            startPosition = transform.position;
            animationOffset = Random.Range(0f, Mathf.PI * 2f);
            spawnTime = Time.time;
            collected = false;

            if (pickupCollider != null) pickupCollider.enabled = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = glowColor;
            }
        }

        private void Update()
        {
            if (collected) return;

            float elapsed = Time.time - spawnTime;

            // Despawn after lifetime if lifetime is enabled
            if (despawnTime > 0f && elapsed >= despawnTime)
            {
                ReturnOrDestroy();
                return;
            }

            UpdateBobAnimation();
            UpdateGlowPulse(elapsed);
        }

        private void UpdateBobAnimation()
        {
            float yOffset = Mathf.Sin(Time.time * bobFrequency + animationOffset) * bobAmplitude;
            Vector3 pos = startPosition;
            pos.y += yOffset;
            transform.position = pos;
        }

        private void UpdateGlowPulse(float elapsed)
        {
            if (spriteRenderer == null) return;

            float t = (Mathf.Sin(Time.time * glowPulseSpeed + animationOffset) + 1f) * 0.5f;
            spriteRenderer.color = Color.Lerp(baseColor, glowColor, t);

            // Flash warning before despawn
            if (despawnTime > 0f && flashStartTime > 0f && elapsed >= flashStartTime)
            {
                float flash = Mathf.PingPong(elapsed * 6f, 1f);
                Color c = spriteRenderer.color;
                c.a = flash > 0.5f ? 1f : 0.2f;
                spriteRenderer.color = c;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;
            if (!other.CompareTag("Player")) return;

            IDamageable health = other.GetComponent<IDamageable>();
            if (health == null)
            {
                health = other.GetComponentInParent<IDamageable>();
            }

            if (health == null) return;

            // Check if player actually needs healing
            if (onlyPickupWhenDamaged && health.CurrentHealth >= health.MaxHealth)
            {
                return;
            }

            ApplyHeal(health);
            OnCollected();
        }

        private void ApplyHeal(IDamageable target)
        {
            float amount = healAmount;

            if (healPercentage)
            {
                amount = target.MaxHealth * (healAmount / 100f);
            }

            // Apply healing via negative damage
            DamageInfo healInfo = new DamageInfo
            {
                amount = -amount,
                type = DamageType.Normal,
                source = gameObject,
                hitPoint = transform.position,
                isCritical = false
            };

            target.TakeDamage(healInfo);
        }

        private void OnCollected()
        {
            collected = true;
            pickupCollider.enabled = false;

            // Spawn effect
            if (collectEffectPrefab != null)
            {
                if (ParticlePooler.Instance != null)
                {
                    ParticlePooler.Instance.Spawn(collectEffectPrefab, transform.position);
                }
                else if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Get(collectEffectPrefab, transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
                }
            }

            // Play sound
            if (collectSound != null)
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(collectSound, collectSoundVolume);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(collectSound, transform.position, collectSoundVolume);
                }
            }

            if (destroyOnCollect)
            {
                ReturnOrDestroy();
            }
            else
            {
                // Hide and respawn after delay
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                Invoke(nameof(Respawn), respawnTime);
            }
        }

        private void Respawn()
        {
            collected = false;
            pickupCollider.enabled = true;
            spawnTime = Time.time;
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            transform.position = startPosition;
        }

        private void ReturnOrDestroy()
        {
            PoolMember poolMember = GetComponent<PoolMember>();
            if (poolMember != null)
            {
                poolMember.ReturnToPool();
            }
            else if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            string label = healPercentage
                ? $"+{healAmount}%"
                : $"+{healAmount} HP";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, label);
        }
#endif
    }
}
