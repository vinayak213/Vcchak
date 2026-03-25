using UnityEngine;

namespace RunAndGun
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private LayerMask hitLayers;
        [SerializeField] private TrailRenderer trailRenderer;

        protected Vector2 moveDirection;
        protected float speed;
        protected float damage;
        protected GameObject sourcePrefab;
        protected Rigidbody2D rb;

        private float spawnTime;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }
        }

        public virtual void Initialize(Vector2 direction, float bulletSpeed, float bulletDamage, GameObject prefab)
        {
            moveDirection = direction.normalized;
            speed = bulletSpeed;
            damage = bulletDamage;
            sourcePrefab = prefab;
            spawnTime = Time.time;

            rb.linearVelocity = moveDirection * speed;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
        }

        protected virtual void Update()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                ReturnToPool();
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsInHitLayer(other.gameObject)) return;

            ApplyDamage(other);
            OnHit(other);
            ReturnToPool();
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsInHitLayer(collision.gameObject)) return;

            Collider2D other = collision.collider;
            ApplyDamage(other);
            OnHit(other);
            ReturnToPool();
        }

        protected virtual void ApplyDamage(Collider2D target)
        {
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, transform.position);
            }
        }

        protected virtual void OnHit(Collider2D target)
        {
            // Override in subclasses for hit effects
        }

        private bool IsInHitLayer(GameObject obj)
        {
            return (hitLayers.value & (1 << obj.layer)) != 0;
        }

        protected void ReturnToPool()
        {
            rb.linearVelocity = Vector2.zero;

            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }

            if (ObjectPool.Instance != null && sourcePrefab != null)
            {
                ObjectPool.Instance.Return(sourcePrefab, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    // IDamageable interface has been moved to Combat/DamageSystem.cs
}
