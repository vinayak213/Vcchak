using UnityEngine;

namespace RunAndGun
{
    public class ExplosiveWeapon : WeaponBase
    {
        [Header("Explosive Settings")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float explosionDamage = 50f;
        [SerializeField] private float damageMinPercent = 0.2f;
        [SerializeField] private GameObject explosionEffectPrefab;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void SpawnSingleBullet(Vector2 baseDirection, float angleOffset)
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

            ExplosiveBullet explosiveBullet = bulletObj.GetComponent<ExplosiveBullet>();
            if (explosiveBullet != null)
            {
                explosiveBullet.Initialize(finalDirection, weaponData.BulletSpeed, weaponData.Damage, weaponData.BulletPrefab);
                explosiveBullet.SetExplosionParameters(explosionRadius, explosionDamage, damageMinPercent, explosionEffectPrefab);
            }
            else
            {
                Bullet bullet = bulletObj.GetComponent<Bullet>();
                if (bullet != null)
                {
                    bullet.Initialize(finalDirection, weaponData.BulletSpeed, weaponData.Damage, weaponData.BulletPrefab);
                }
            }
        }
    }
}
