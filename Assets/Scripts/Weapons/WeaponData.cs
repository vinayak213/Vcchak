using UnityEngine;

namespace RunAndGun
{
    public enum WeaponType
    {
        RapidFire,
        SpreadShot,
        Laser,
        Explosive
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "RunAndGun/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string weaponName = "Unnamed Weapon";
        [SerializeField] private Sprite icon;
        [SerializeField] private WeaponType weaponType = WeaponType.RapidFire;

        [Header("Stats")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float fireRate = 5f;
        [SerializeField] private float bulletSpeed = 20f;

        [Header("Ammo")]
        [Tooltip("-1 for infinite ammo")]
        [SerializeField] private int ammoCapacity = -1;

        [Header("Spread")]
        [SerializeField] private float spreadAngle;
        [SerializeField] private int bulletsPerShot = 1;

        [Header("Prefabs")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject muzzleFlashPrefab;

        [Header("Audio")]
        [SerializeField] private AudioClip fireSoundEffect;

        public string WeaponName => weaponName;
        public Sprite Icon => icon;
        public WeaponType WeaponType => weaponType;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float BulletSpeed => bulletSpeed;
        public int AmmoCapacity => ammoCapacity;
        public float SpreadAngle => spreadAngle;
        public int BulletsPerShot => bulletsPerShot;
        public GameObject BulletPrefab => bulletPrefab;
        public GameObject MuzzleFlashPrefab => muzzleFlashPrefab;
        public AudioClip FireSoundEffect => fireSoundEffect;

        public bool HasInfiniteAmmo => ammoCapacity < 0;
    }
}
