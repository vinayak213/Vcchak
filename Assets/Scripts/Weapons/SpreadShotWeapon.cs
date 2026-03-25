using UnityEngine;

namespace RunAndGun
{
    public class SpreadShotWeapon : WeaponBase
    {
        [Header("Spread Shot Settings")]
        [SerializeField] private float perBulletRandomSpread = 2f;
        [SerializeField] private float knockbackForce = 5f;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void Fire(Vector2 direction)
        {
            base.Fire(direction);

            ApplyKnockback(direction);
        }

        protected override void SpawnBullets(Vector2 direction)
        {
            int count = weaponData.BulletsPerShot;
            float totalSpread = weaponData.SpreadAngle;

            count = Mathf.Clamp(count, 3, 5);

            float startAngle = -totalSpread * 0.5f;
            float step = totalSpread / (count - 1);

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                float randomOffset = Random.Range(-perBulletRandomSpread, perBulletRandomSpread);
                SpawnSingleBullet(direction, angle + randomOffset);
            }
        }

        private void ApplyKnockback(Vector2 fireDirection)
        {
            if (knockbackForce <= 0f) return;

            Rigidbody2D playerRb = GetComponentInParent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.AddForce(-fireDirection.normalized * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }
}
