using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Generic AI controller that drives any <see cref="EnemyBase"/> through a
    /// finite state machine with Patrol / Chase / Attack / Retreat / Death states.
    /// Attach alongside a concrete enemy component (GroundEnemy, FlyingEnemy, etc.).
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyAI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //
        [Header("Player Reference")]
        [Tooltip("Leave null to auto-find by tag 'Player' at Start.")]
        [SerializeField] private Transform playerTarget;

        [Header("Patrol")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolWaitTime = 1f;

        [Header("Line of Sight")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private Vector2 eyeOffset = new Vector2(0f, 0.3f);

        // ------------------------------------------------------------------ //
        //  Runtime references
        // ------------------------------------------------------------------ //
        public EnemyBase Enemy { get; private set; }
        public Rigidbody2D Rb { get; private set; }
        public EnemyData Data => Enemy.Data;
        public Transform PlayerTarget => playerTarget;
        public Transform[] PatrolPoints => patrolPoints;
        public float PatrolWaitTime => patrolWaitTime;

        public StateMachine<EnemyAI> Machine { get; private set; }

        // Shared combat bookkeeping
        public float LastAttackTime { get; set; }
        public int CurrentPatrolIndex { get; set; }

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //
        private void Awake()
        {
            Enemy = GetComponent<EnemyBase>();
            Rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            InitMachine();
            Enemy.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            Enemy.OnDeath -= HandleDeath;
        }

        private void Start()
        {
            if (playerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTarget = player.transform;
            }
        }

        private void Update()
        {
            if (Enemy.IsDead) return;
            Machine.Tick();
        }

        // ------------------------------------------------------------------ //
        //  Helpers available to states
        // ------------------------------------------------------------------ //

        public float DistanceToPlayer()
        {
            if (playerTarget == null) return float.MaxValue;
            return Vector2.Distance(transform.position, playerTarget.position);
        }

        public Vector2 DirectionToPlayer()
        {
            if (playerTarget == null) return Vector2.zero;
            return ((Vector2)playerTarget.position - (Vector2)transform.position).normalized;
        }

        public bool HasLineOfSight()
        {
            if (playerTarget == null) return false;

            Vector2 origin = (Vector2)transform.position + eyeOffset;
            Vector2 target = (Vector2)playerTarget.position;
            Vector2 dir = target - origin;
            float dist = dir.magnitude;

            RaycastHit2D hit = Physics2D.Raycast(origin, dir.normalized, dist, obstacleMask);
            return hit.collider == null;
        }

        public bool PlayerInDetectionRange()
        {
            return DistanceToPlayer() <= Data.DetectionRange;
        }

        public bool PlayerInAttackRange()
        {
            return DistanceToPlayer() <= Data.AttackRange;
        }

        public bool PlayerTooClose()
        {
            return DistanceToPlayer() <= Data.RetreatRange;
        }

        public bool CanAttack()
        {
            return Time.time >= LastAttackTime + Data.AttackCooldown;
        }

        public void FacePlayer()
        {
            if (playerTarget == null) return;
            float dir = playerTarget.position.x - transform.position.x;
            if (Mathf.Abs(dir) > 0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dir);
                transform.localScale = scale;
            }
        }

        // ------------------------------------------------------------------ //
        //  Machine bootstrap
        // ------------------------------------------------------------------ //
        private void InitMachine()
        {
            Machine = new StateMachine<EnemyAI>(this);
            Machine.RegisterState(new PatrolState());
            Machine.RegisterState(new ChaseState());
            Machine.RegisterState(new AttackState());
            Machine.RegisterState(new RetreatState());
            Machine.RegisterState(new DeathState());

            Machine.ChangeState<PatrolState>();
        }

        private void HandleDeath(EnemyBase _)
        {
            Machine.ChangeState<DeathState>();
        }

        // ================================================================== //
        //  STATES
        // ================================================================== //

        // ---- Patrol ------------------------------------------------------- //
        public sealed class PatrolState : State<EnemyAI>
        {
            private float _waitTimer;
            private bool _waiting;

            public override void Enter()
            {
                _waiting = false;
                _waitTimer = 0f;
            }

            public override void Execute()
            {
                // Transition: detect player
                if (Context.PlayerInDetectionRange() && Context.HasLineOfSight())
                {
                    Machine.ChangeState<ChaseState>();
                    return;
                }

                if (Context.PatrolPoints == null || Context.PatrolPoints.Length == 0)
                {
                    Context.Rb.linearVelocity = new Vector2(0f, Context.Rb.linearVelocity.y);
                    return;
                }

                if (_waiting)
                {
                    _waitTimer -= Time.deltaTime;
                    Context.Rb.linearVelocity = new Vector2(0f, Context.Rb.linearVelocity.y);
                    if (_waitTimer <= 0f) _waiting = false;
                    return;
                }

                Transform wp = Context.PatrolPoints[Context.CurrentPatrolIndex];
                Vector2 dir = ((Vector2)wp.position - (Vector2)Context.transform.position);
                float dist = dir.magnitude;

                if (dist < 0.3f)
                {
                    Context.CurrentPatrolIndex = (Context.CurrentPatrolIndex + 1) % Context.PatrolPoints.Length;
                    _waiting = true;
                    _waitTimer = Context.PatrolWaitTime;
                    return;
                }

                Vector2 move = dir.normalized * Context.Data.MoveSpeed;
                Context.Rb.linearVelocity = new Vector2(move.x, Context.Rb.linearVelocity.y);

                // Face movement direction
                Vector3 scale = Context.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(move.x);
                Context.transform.localScale = scale;
            }

            public override void Exit()
            {
                Context.Rb.linearVelocity = new Vector2(0f, Context.Rb.linearVelocity.y);
            }
        }

        // ---- Chase -------------------------------------------------------- //
        public sealed class ChaseState : State<EnemyAI>
        {
            public override void Execute()
            {
                if (Context.PlayerTarget == null)
                {
                    Machine.ChangeState<PatrolState>();
                    return;
                }

                // Lost sight for too long -> return to patrol
                if (!Context.PlayerInDetectionRange())
                {
                    Machine.ChangeState<PatrolState>();
                    return;
                }

                // Too close -> retreat
                if (Context.PlayerTooClose())
                {
                    Machine.ChangeState<RetreatState>();
                    return;
                }

                // In attack range with LoS -> attack
                if (Context.PlayerInAttackRange() && Context.HasLineOfSight() && Context.CanAttack())
                {
                    Machine.ChangeState<AttackState>();
                    return;
                }

                // Move toward player
                Context.FacePlayer();
                Vector2 dir = Context.DirectionToPlayer();
                Context.Rb.linearVelocity = new Vector2(dir.x * Context.Data.MoveSpeed, Context.Rb.linearVelocity.y);
            }

            public override void Exit()
            {
                Context.Rb.linearVelocity = new Vector2(0f, Context.Rb.linearVelocity.y);
            }
        }

        // ---- Attack ------------------------------------------------------- //
        public sealed class AttackState : State<EnemyAI>
        {
            private bool _hasAttacked;

            public override void Enter()
            {
                _hasAttacked = false;
                Context.Rb.linearVelocity = new Vector2(0f, Context.Rb.linearVelocity.y);
                Context.FacePlayer();
            }

            public override void Execute()
            {
                if (!_hasAttacked)
                {
                    _hasAttacked = true;
                    Context.LastAttackTime = Time.time;

                    // Delegate actual firing to the enemy subclass.
                    IAttacker attacker = Context.Enemy as IAttacker;
                    attacker?.PerformAttack();
                }

                // After a short wind-down return to an appropriate state.
                if (Machine.TimeInState > 0.3f)
                {
                    if (Context.PlayerTooClose())
                        Machine.ChangeState<RetreatState>();
                    else if (Context.PlayerInDetectionRange())
                        Machine.ChangeState<ChaseState>();
                    else
                        Machine.ChangeState<PatrolState>();
                }
            }
        }

        // ---- Retreat ------------------------------------------------------ //
        public sealed class RetreatState : State<EnemyAI>
        {
            public override void Execute()
            {
                if (Context.PlayerTarget == null || !Context.PlayerTooClose())
                {
                    Machine.ChangeState<ChaseState>();
                    return;
                }

                Context.FacePlayer();
                Vector2 away = -Context.DirectionToPlayer();
                Context.Rb.linearVelocity = new Vector2(away.x * Context.Data.MoveSpeed, Context.Rb.linearVelocity.y);
            }

            public override void Exit()
            {
                Context.Rb.linearVelocity = new Vector2(0f, Context.Rb.linearVelocity.y);
            }
        }

        // ---- Death -------------------------------------------------------- //
        public sealed class DeathState : State<EnemyAI>
        {
            public override void Enter()
            {
                Context.Rb.linearVelocity = Vector2.zero;
                Context.Rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }

    // ================================================================== //
    //  IAttacker interface
    // ================================================================== //

    /// <summary>
    /// Implement on concrete enemy classes to fire projectiles / melee.
    /// Called by <see cref="EnemyAI.AttackState"/>.
    /// </summary>
    public interface IAttacker
    {
        void PerformAttack();
    }
}
