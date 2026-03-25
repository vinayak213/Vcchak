using UnityEngine;

namespace RunAndGun
{
    public class RapidFireWeapon : WeaponBase
    {
        [Header("Rapid Fire Settings")]
        [SerializeField] private float recoilAngle = 3f;
        [SerializeField] private float recoilRecoveryRate = 15f;
        [SerializeField] private float maxRecoilAngle = 12f;

        private float currentRecoil;
        private bool isFiring;
        private float lastFireInput;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Update()
        {
            if (Time.time - lastFireInput > 0.15f)
            {
                isFiring = false;
            }

            if (!isFiring)
            {
                currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, recoilRecoveryRate * Time.deltaTime);
            }
        }

        public override void Fire(Vector2 direction)
        {
            isFiring = true;
            lastFireInput = Time.time;

            base.Fire(direction);

            currentRecoil = Mathf.Min(currentRecoil + recoilAngle, maxRecoilAngle);
        }

        protected override void SpawnSingleBullet(Vector2 baseDirection, float angleOffset)
        {
            float recoilOffset = Random.Range(-currentRecoil, currentRecoil);
            base.SpawnSingleBullet(baseDirection, angleOffset + recoilOffset);
        }
    }
}
