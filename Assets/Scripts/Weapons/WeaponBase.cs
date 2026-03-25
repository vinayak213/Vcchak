using UnityEngine;

namespace RunAndGun
{
    public class WeaponBase : MonoBehaviour
    {
        [SerializeField] protected WeaponData weaponData;
        [SerializeField] protected Transform firePoint;
        [SerializeField] protected AudioSource audioSource;

        protected int currentAmmo;
        protected float nextFireTime;
        protected bool isEquipped;

        public WeaponData Data => weaponData;
        public int CurrentAmmo => currentAmmo;
        public bool HasAmmo => weaponData.HasInfiniteAmmo || currentAmmo > 0;

        public event System.Action<int> OnAmmoChanged;
        public event System.Action OnWeaponEmpty;

        protected virtual void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        protected virtual void OnEnable()
        {
            ResetAmmo();
            isEquipped = true;
        }

        protected virtual void OnDisable()
        {
            isEquipped = false;
        }

        public void ResetAmmo()
        {
            currentAmmo = weaponData.AmmoCapacity;
            OnAmmoChanged?.Invoke(currentAmmo);
        }

        public void AddAmmo(int amount)
        {
            if (weaponData.HasInfiniteAmmo) return;

            currentAmmo = Mathf.Min(currentAmmo + amount, weaponData.AmmoCapacity);
            OnAmmoChanged?.Invoke(currentAmmo);
        }

        public bool TryFire(Vector2 direction)
        {
            if (Time.time < nextFireTime) return false;
            if (!HasAmmo) return false;

            Fire(direction);
            return true;
        }

        public virtual void Fire(Vector2 direction)
        {
            if (direction == Vector2.zero)
            {
                direction = Vector2.right;
            }

            direction.Normalize();

            nextFireTime = Time.time + (1f / weaponData.FireRate);

            ConsumeAmmo();
            SpawnBullets(direction);
            PlayFireSound();
            SpawnMuzzleFlash();
        }

        protected virtual void SpawnBullets(Vector2 direction)
        {
            int count = weaponData.BulletsPerShot;
            float totalSpread = weaponData.SpreadAngle;

            if (count <= 1)
            {
                SpawnSingleBullet(direction, 0f);
                return;
            }

            float startAngle = -totalSpread * 0.5f;
            float step = totalSpread / (count - 1);

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                SpawnSingleBullet(direction, angle);
            }
        }

        protected virtual void SpawnSingleBullet(Vector2 baseDirection, float angleOffset)
        {
            if (weaponData.BulletPrefab == null || ObjectPool.Instance == null) return;

            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            float finalAngle = baseAngle + angleOffset;
            Quaternion rotation = Quaternion.Euler(0f, 0f, finalAngle);

            Vector2 finalDirection = new Vector2(
                Mathf.Cos(finalAngle * Mathf.Deg2Rad),
                Mathf.Sin(finalAngle * Mathf.Deg2Rad)
            );

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            GameObject bulletObj = ObjectPool.Instance.Get(weaponData.BulletPrefab, spawnPos, rotation);

            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Initialize(finalDirection, weaponData.BulletSpeed, weaponData.Damage, weaponData.BulletPrefab);
            }
        }

        protected virtual void ConsumeAmmo()
        {
            if (weaponData.HasInfiniteAmmo) return;

            currentAmmo--;
            OnAmmoChanged?.Invoke(currentAmmo);

            if (currentAmmo <= 0)
            {
                OnWeaponEmpty?.Invoke();
            }
        }

        protected virtual void PlayFireSound()
        {
            if (weaponData.FireSoundEffect == null || audioSource == null) return;

            audioSource.PlayOneShot(weaponData.FireSoundEffect);
        }

        protected virtual void SpawnMuzzleFlash()
        {
            if (weaponData.MuzzleFlashPrefab == null || ObjectPool.Instance == null) return;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;
            GameObject flash = ObjectPool.Instance.Get(weaponData.MuzzleFlashPrefab, spawnPos, spawnRot);

            if (firePoint != null)
            {
                flash.transform.SetParent(firePoint);
            }

            MuzzleFlashReturn returnComp = flash.GetComponent<MuzzleFlashReturn>();
            if (returnComp == null)
            {
                returnComp = flash.AddComponent<MuzzleFlashReturn>();
            }
            returnComp.Begin(weaponData.MuzzleFlashPrefab, 0.1f);
        }
    }

    public class MuzzleFlashReturn : MonoBehaviour
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
                transform.SetParent(null);
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
