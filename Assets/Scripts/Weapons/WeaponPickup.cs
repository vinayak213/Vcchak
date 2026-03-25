using UnityEngine;

namespace RunAndGun
{
    [RequireComponent(typeof(Collider2D))]
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private GameObject weaponPrefab;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private float floatAmplitude = 0.25f;
        [SerializeField] private float floatFrequency = 2f;
        [SerializeField] private float glowPulseSpeed = 3f;
        [SerializeField] private float glowMinIntensity = 0.6f;
        [SerializeField] private float glowMaxIntensity = 1f;
        [SerializeField] private Color glowColor = Color.yellow;

        [Header("Despawn")]
        [Tooltip("Time in seconds before the pickup despawns. Set to -1 for no despawn.")]
        [SerializeField] private float despawnTime = 30f;
        [SerializeField] private float despawnBlinkStart = 5f;
        [SerializeField] private float blinkRate = 8f;

        [Header("Pickup")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private AudioClip pickupSound;

        private Vector3 startPosition;
        private float spawnTime;
        private SpriteRenderer spriteRenderer;
        private bool isPickedUp;
        private MaterialPropertyBlock propertyBlock;

        public WeaponData Data => weaponData;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            propertyBlock = new MaterialPropertyBlock();

            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnEnable()
        {
            startPosition = transform.position;
            spawnTime = Time.time;
            isPickedUp = false;

            if (iconRenderer != null && weaponData != null && weaponData.Icon != null)
            {
                iconRenderer.sprite = weaponData.Icon;
            }

            SetVisible(true);
        }

        private void Update()
        {
            if (isPickedUp) return;

            AnimateFloat();
            AnimateGlow();
            HandleDespawn();
        }

        private void AnimateFloat()
        {
            float offset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            transform.position = startPosition + Vector3.up * offset;
        }

        private void AnimateGlow()
        {
            if (spriteRenderer == null) return;

            float t = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, t);
            Color color = glowColor * intensity;
            color.a = 1f;

            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        private void HandleDespawn()
        {
            if (despawnTime < 0f) return;

            float elapsed = Time.time - spawnTime;
            float remaining = despawnTime - elapsed;

            if (remaining <= 0f)
            {
                Despawn();
                return;
            }

            if (remaining <= despawnBlinkStart)
            {
                bool visible = Mathf.Sin(Time.time * blinkRate * Mathf.PI) > 0f;
                SetVisible(visible);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isPickedUp) return;
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

            IWeaponReceiver receiver = other.GetComponent<IWeaponReceiver>();
            if (receiver == null)
            {
                receiver = other.GetComponentInParent<IWeaponReceiver>();
            }

            if (receiver == null) return;

            bool accepted = receiver.ReceiveWeapon(weaponData, weaponPrefab);
            if (!accepted) return;

            isPickedUp = true;
            PlayPickupSound(other.transform.position);
            Despawn();
        }

        private void PlayPickupSound(Vector3 position)
        {
            if (pickupSound == null) return;

            AudioSource.PlayClipAtPoint(pickupSound, position);
        }

        private void SetVisible(bool visible)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = visible;
            }

            if (iconRenderer != null)
            {
                iconRenderer.enabled = visible;
            }
        }

        private void Despawn()
        {
            if (ObjectPool.Instance != null)
            {
                PoolMember member = GetComponent<PoolMember>();
                if (member != null && member.SourcePrefab != null)
                {
                    ObjectPool.Instance.Return(gameObject);
                    return;
                }
            }

            Destroy(gameObject);
        }

        public void Setup(WeaponData data, GameObject prefab)
        {
            weaponData = data;
            weaponPrefab = prefab;
            spawnTime = Time.time;

            if (iconRenderer != null && data != null && data.Icon != null)
            {
                iconRenderer.sprite = data.Icon;
            }
        }
    }

    public interface IWeaponReceiver
    {
        bool ReceiveWeapon(WeaponData weaponData, GameObject weaponPrefab);
    }
}
