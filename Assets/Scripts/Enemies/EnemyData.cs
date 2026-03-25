using System;
using UnityEngine;

namespace RunAndGun
{
    [Serializable]
    public struct DropEntry
    {
        [Tooltip("Prefab to spawn on death.")]
        public GameObject Prefab;

        [Range(0f, 1f)]
        [Tooltip("Probability this item drops (0-1).")]
        public float Probability;

        [Min(1)]
        [Tooltip("How many to drop when the roll succeeds.")]
        public int Quantity;
    }

    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "RunAndGun/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [Header("Vitals")]
        [Min(1)]
        [SerializeField] private int maxHealth = 3;
        [Min(0f)]
        [SerializeField] private float moveSpeed = 3f;
        [Min(0)]
        [SerializeField] private int contactDamage = 1;

        [Header("Detection")]
        [Min(0f)]
        [SerializeField] private float detectionRange = 8f;
        [Min(0f)]
        [SerializeField] private float attackRange = 5f;
        [Min(0f)]
        [SerializeField] private float retreatRange = 1.5f;
        [SerializeField] private LayerMask lineOfSightMask;

        [Header("Combat")]
        [Min(0f)]
        [SerializeField] private float attackCooldown = 1.2f;
        [Min(0)]
        [SerializeField] private int projectileDamage = 1;
        [Min(0f)]
        [SerializeField] private float projectileSpeed = 8f;

        [Header("Scoring")]
        [Min(0)]
        [SerializeField] private int scoreValue = 100;

        [Header("Drops")]
        [SerializeField] private DropEntry[] dropTable = Array.Empty<DropEntry>();

        // --- public read-only accessors ---
        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public int ContactDamage => contactDamage;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public float RetreatRange => retreatRange;
        public LayerMask LineOfSightMask => lineOfSightMask;
        public float AttackCooldown => attackCooldown;
        public int ProjectileDamage => projectileDamage;
        public float ProjectileSpeed => projectileSpeed;
        public int ScoreValue => scoreValue;
        public DropEntry[] DropTable => dropTable;
    }
}
