// ---------------------------------------------------------------------------
// PlayerCombat.cs — Combat integration for the player.  Manages the active
// weapon, handles shooting input (including auto-fire), calculates 8-direction
// aim, and supports weapon switching across an inventory of WeaponBase slots.
// Fires events that the UI and animation systems consume.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    public class PlayerCombat : MonoBehaviour
    {
        // ================================================================== //
        //  Inspector
        // ================================================================== //

        [Header("Weapon Slots")]
        [Tooltip("Pre-assigned weapon components (children of the player). " +
                 "Index 0 is the default weapon.")]
        [SerializeField] private List<WeaponBase> weaponSlots = new();

        [Header("Fire Point")]
        [Tooltip("Transform used as the bullet spawn origin.  If null, the " +
                 "first active weapon's own fire point is used.")]
        [SerializeField] private Transform firePointOverride;

        [Header("Auto-Fire")]
        [Tooltip("When true, holding the shoot button continuously fires.")]
        [SerializeField] private bool enableAutoFire = true;

        // ================================================================== //
        //  Events
        // ================================================================== //

        /// <summary>Raised when a weapon fires.  Arg: the weapon that fired.</summary>
        public event Action<WeaponBase> OnWeaponFired;

        /// <summary>Raised when the active weapon changes.  Args: new weapon, slot index.</summary>
        public event Action<WeaponBase, int> OnWeaponSwitched;

        /// <summary>Raised when ammo changes on the current weapon.  Arg: current ammo.</summary>
        public event Action<int> OnAmmoChanged;

        // ================================================================== //
        //  Public properties
        // ================================================================== //

        /// <summary>Currently equipped weapon (may be null if inventory is empty).</summary>
        public WeaponBase CurrentWeapon { get; private set; }

        /// <summary>Index into <see cref="weaponSlots"/>.</summary>
        public int CurrentWeaponIndex { get; private set; }

        /// <summary>
        /// True while the player is actively shooting (used by animation
        /// controller for the IsShooting flag).
        /// </summary>
        public bool IsShooting { get; private set; }

        /// <summary>Current 8-direction aim vector.</summary>
        public Vector2 AimDirection { get; private set; } = Vector2.right;

        // ================================================================== //
        //  Cached references
        // ================================================================== //

        private PlayerController _controller;
        private InputManager _input;

        // -- Shooting cooldown visual flag --
        private float _shootFlagTimer;
        private const float ShootFlagDuration = 0.15f; // brief window to hold IsShooting

        // ================================================================== //
        //  Unity lifecycle
        // ================================================================== //

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

        private void Start()
        {
            _input = InputManager.Instance;

            // Activate the first weapon and deactivate the rest.
            InitialiseWeaponSlots();
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnAimDirectionChanged += HandleAimChanged;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnAimDirectionChanged -= HandleAimChanged;

            // Unsubscribe from current weapon's ammo event.
            UnsubscribeWeaponEvents(CurrentWeapon);
        }

        private void Update()
        {
            HandleShootInput();
            HandleWeaponSwitchInput();
            UpdateShootingFlag();
        }

        // ================================================================== //
        //  Weapon slot management
        // ================================================================== //

        private void InitialiseWeaponSlots()
        {
            if (weaponSlots == null || weaponSlots.Count == 0)
            {
                // Try to discover weapons as children at runtime.
                GetComponentsInChildren(true, weaponSlots);
            }

            // Deactivate all slots, then activate the first.
            for (int i = 0; i < weaponSlots.Count; i++)
            {
                if (weaponSlots[i] != null)
                    weaponSlots[i].gameObject.SetActive(false);
            }

            if (weaponSlots.Count > 0)
            {
                EquipWeapon(0);
            }
        }

        /// <summary>
        /// Equip the weapon at the given slot index.
        /// </summary>
        public void EquipWeapon(int slotIndex)
        {
            if (weaponSlots == null || slotIndex < 0 || slotIndex >= weaponSlots.Count) return;

            // Deactivate current weapon.
            if (CurrentWeapon != null)
            {
                UnsubscribeWeaponEvents(CurrentWeapon);
                CurrentWeapon.gameObject.SetActive(false);
            }

            CurrentWeaponIndex = slotIndex;
            CurrentWeapon = weaponSlots[slotIndex];

            if (CurrentWeapon != null)
            {
                CurrentWeapon.gameObject.SetActive(true);
                SubscribeWeaponEvents(CurrentWeapon);
            }

            OnWeaponSwitched?.Invoke(CurrentWeapon, CurrentWeaponIndex);
        }

        /// <summary>
        /// Add a new weapon to the inventory and optionally equip it.
        /// Returns the slot index assigned.
        /// </summary>
        public int AddWeapon(WeaponBase weapon, bool equipImmediately = true)
        {
            if (weapon == null) return -1;

            // Check if we already have this weapon type — if so, just add ammo.
            for (int i = 0; i < weaponSlots.Count; i++)
            {
                if (weaponSlots[i] != null &&
                    weaponSlots[i].Data != null &&
                    weapon.Data != null &&
                    weaponSlots[i].Data.WeaponType == weapon.Data.WeaponType)
                {
                    weaponSlots[i].AddAmmo(weapon.Data.AmmoCapacity);
                    if (equipImmediately)
                        EquipWeapon(i);
                    return i;
                }
            }

            // New weapon — parent it under this object.
            weapon.transform.SetParent(transform);
            weapon.transform.localPosition = Vector3.zero;
            weapon.gameObject.SetActive(false);

            weaponSlots.Add(weapon);
            int index = weaponSlots.Count - 1;

            if (equipImmediately)
                EquipWeapon(index);

            return index;
        }

        /// <summary>
        /// Remove a weapon from the inventory.  If it was active, switch to
        /// the default weapon (slot 0).
        /// </summary>
        public void RemoveWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= weaponSlots.Count) return;

            WeaponBase removed = weaponSlots[slotIndex];
            weaponSlots.RemoveAt(slotIndex);

            if (removed != null)
            {
                UnsubscribeWeaponEvents(removed);
                removed.gameObject.SetActive(false);
            }

            // Re-equip if we removed the active weapon.
            if (CurrentWeaponIndex == slotIndex || CurrentWeaponIndex >= weaponSlots.Count)
            {
                EquipWeapon(weaponSlots.Count > 0 ? 0 : -1);
            }
        }

        // ================================================================== //
        //  Shooting
        // ================================================================== //

        private void HandleShootInput()
        {
            if (CurrentWeapon == null) return;

            bool wantsToShoot = enableAutoFire ? _input.ShootHeld : _input.ShootDown;

            if (wantsToShoot)
            {
                bool fired = CurrentWeapon.TryFire(AimDirection);
                if (fired)
                {
                    _shootFlagTimer = ShootFlagDuration;
                    OnWeaponFired?.Invoke(CurrentWeapon);
                }
            }
        }

        /// <summary>
        /// Keeps <see cref="IsShooting"/> true for a brief window after firing
        /// so the animation controller can blend the shoot pose.
        /// </summary>
        private void UpdateShootingFlag()
        {
            if (_shootFlagTimer > 0f)
            {
                _shootFlagTimer -= Time.deltaTime;
                IsShooting = true;
            }
            else
            {
                IsShooting = _input.ShootHeld && enableAutoFire;
            }
        }

        // ================================================================== //
        //  Weapon switching
        // ================================================================== //

        private void HandleWeaponSwitchInput()
        {
            if (_input.WeaponSwitchDown && weaponSlots.Count > 1)
            {
                int next = (CurrentWeaponIndex + 1) % weaponSlots.Count;
                EquipWeapon(next);
            }
        }

        /// <summary>Cycle to the next weapon in the inventory.</summary>
        public void CycleWeaponForward()
        {
            if (weaponSlots.Count <= 1) return;
            EquipWeapon((CurrentWeaponIndex + 1) % weaponSlots.Count);
        }

        /// <summary>Cycle to the previous weapon in the inventory.</summary>
        public void CycleWeaponBackward()
        {
            if (weaponSlots.Count <= 1) return;
            int prev = (CurrentWeaponIndex - 1 + weaponSlots.Count) % weaponSlots.Count;
            EquipWeapon(prev);
        }

        // ================================================================== //
        //  Aim direction
        // ================================================================== //

        private void HandleAimChanged(Vector2 direction)
        {
            AimDirection = direction;
        }

        /// <summary>
        /// Utility: returns the current aim direction as an angle in degrees
        /// (0 = right, 90 = up, counter-clockwise).
        /// </summary>
        public float GetAimAngleDegrees()
        {
            return Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
        }

        // ================================================================== //
        //  Weapon event wiring
        // ================================================================== //

        private void SubscribeWeaponEvents(WeaponBase weapon)
        {
            if (weapon == null) return;
            weapon.OnAmmoChanged += HandleAmmoChanged;
        }

        private void UnsubscribeWeaponEvents(WeaponBase weapon)
        {
            if (weapon == null) return;
            weapon.OnAmmoChanged -= HandleAmmoChanged;
        }

        private void HandleAmmoChanged(int ammo)
        {
            OnAmmoChanged?.Invoke(ammo);
        }
    }
}
