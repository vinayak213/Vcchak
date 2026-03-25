using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Collectible coin with float animation, magnetic attraction to the player,
    /// score value, collection effect, and object pool support.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Coin : MonoBehaviour
    {
        [Header("Value")]
        [SerializeField] private int scoreValue = 100;

        [Header("Float Animation")]
        [SerializeField] private float floatAmplitude = 0.15f;
        [SerializeField] private float floatFrequency = 2f;
        [SerializeField] private float rotateSpeed = 90f;

        [Header("Magnetic Attraction")]
        [SerializeField] private bool enableMagnet = true;
        [SerializeField] private float magnetRange = 3f;
        [SerializeField] private float magnetSpeed = 8f;
        [SerializeField] private float magnetAcceleration = 15f;

        [Header("Collection")]
        [SerializeField] private GameObject collectEffectPrefab;
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private float collectSoundVolume = 0.8f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Vector3 startPosition;
        private float animationOffset;
        private Transform playerTransform;
        private bool isBeingAttracted;
        private float currentMagnetSpeed;
        private bool collected;
        private Collider2D coinCollider;

        private void Awake()
        {
            coinCollider = GetComponent<Collider2D>();
            coinCollider.isTrigger = true;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            startPosition = transform.position;
            animationOffset = Random.Range(0f, Mathf.PI * 2f);
            isBeingAttracted = false;
            currentMagnetSpeed = 0f;
            collected = false;

            if (coinCollider != null) coinCollider.enabled = true;
            if (spriteRenderer != null) spriteRenderer.enabled = true;

            playerTransform = null;
        }

        private void Update()
        {
            if (collected) return;

            if (isBeingAttracted && playerTransform != null)
            {
                UpdateMagnetMovement();
            }
            else
            {
                UpdateFloatAnimation();
                CheckMagnetRange();
            }
        }

        private void UpdateFloatAnimation()
        {
            // Vertical bob
            float yOffset = Mathf.Sin(Time.time * floatFrequency + animationOffset) * floatAmplitude;
            Vector3 pos = startPosition;
            pos.y += yOffset;
            transform.position = pos;

            // Rotation (simulates 3D coin spin via scale)
            if (spriteRenderer != null)
            {
                float scaleX = Mathf.Cos(Time.time * rotateSpeed * Mathf.Deg2Rad + animationOffset);
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scaleX) * Mathf.Sign(transform.localScale.x > 0 ? 1f : -1f);
                // Prevent zero scale
                if (Mathf.Abs(scale.x) < 0.05f) scale.x = 0.05f * Mathf.Sign(scale.x);
                transform.localScale = scale;
            }
        }

        private void CheckMagnetRange()
        {
            if (!enableMagnet) return;

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else
                {
                    return;
                }
            }

            float distance = Vector2.Distance(transform.position, playerTransform.position);
            if (distance <= magnetRange)
            {
                isBeingAttracted = true;
                currentMagnetSpeed = magnetSpeed * 0.3f;
            }
        }

        private void UpdateMagnetMovement()
        {
            if (playerTransform == null)
            {
                isBeingAttracted = false;
                return;
            }

            currentMagnetSpeed += magnetAcceleration * Time.deltaTime;
            float maxSpeed = magnetSpeed * 3f;
            currentMagnetSpeed = Mathf.Min(currentMagnetSpeed, maxSpeed);

            Vector3 direction = (playerTransform.position - transform.position).normalized;
            transform.position += direction * (currentMagnetSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;
            if (!other.CompareTag("Player")) return;

            Collect();
        }

        private void Collect()
        {
            collected = true;
            coinCollider.enabled = false;

            // Add score
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreValue);
            }

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

            // Return to pool or destroy
            ReturnOrDestroy();
        }

        private void ReturnOrDestroy()
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (enableMagnet)
            {
                Gizmos.color = new Color(1f, 0.84f, 0f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, magnetRange);
            }
        }
#endif
    }
}
