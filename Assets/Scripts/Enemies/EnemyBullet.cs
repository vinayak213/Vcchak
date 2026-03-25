using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Pooled enemy projectile. Damages the player on contact and auto-returns
    /// to the pool after its lifetime expires.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class EnemyBullet : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //
        [Header("Bullet Settings")]
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private LayerMask hitLayers;
        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Visual")]
        [SerializeField] private Color bulletColor = new Color(1f, 0.3f, 0.2f, 1f);

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //
        private Rigidbody2D _rb;
        private int _damage;
        private float _spawnTime;
        private GameObject _sourcePrefab;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (trailRenderer == null)
                trailRenderer = GetComponent<TrailRenderer>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
                spriteRenderer.color = bulletColor;
        }

        private void OnEnable()
        {
            _spawnTime = Time.time;

            if (trailRenderer != null)
                trailRenderer.Clear();
        }

        private void Update()
        {
            if (Time.time - _spawnTime >= lifetime)
            {
                ReturnToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsInHitLayer(other.gameObject)) return;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(_damage, transform.position);
            }

            ReturnToPool();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsInHitLayer(collision.gameObject)) return;

            IDamageable damageable = collision.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(_damage, transform.position);
            }

            ReturnToPool();
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Set up the bullet before it flies. Called by the enemy that fires it.
        /// </summary>
        public void Initialize(Vector2 direction, float speed, int damage, GameObject prefab)
        {
            direction.Normalize();
            _damage = damage;
            _sourcePrefab = prefab;
            _spawnTime = Time.time;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _rb.linearVelocity = direction * speed;

            if (trailRenderer != null)
                trailRenderer.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Internal
        // ------------------------------------------------------------------ //

        private bool IsInHitLayer(GameObject obj)
        {
            return (hitLayers.value & (1 << obj.layer)) != 0;
        }

        private void ReturnToPool()
        {
            _rb.linearVelocity = Vector2.zero;

            if (trailRenderer != null)
                trailRenderer.Clear();

            if (ObjectPool.Instance != null && _sourcePrefab != null)
            {
                ObjectPool.Instance.Return(_sourcePrefab, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
