using UnityEngine;

namespace RunAndGun
{
    public enum PlatformMode
    {
        Loop,
        PingPong,
        OneWay
    }

    public enum PlatformBehavior
    {
        Standard,
        Falling,
        OneWayPassthrough
    }

    public class MovingPlatform : MonoBehaviour
    {
        [Header("Waypoints")]
        [SerializeField] private Transform[] waypoints;

        [Header("Movement")]
        [SerializeField] private float speed = 3f;
        [SerializeField] private float waitTime = 0.5f;
        [SerializeField] private PlatformMode mode = PlatformMode.PingPong;

        [Header("Behavior")]
        [SerializeField] private PlatformBehavior behavior = PlatformBehavior.Standard;

        [Header("Falling Platform")]
        [SerializeField] private float fallDelay = 0.5f;
        [SerializeField] private float fallSpeed = 8f;
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private float shakeIntensity = 0.03f;

        [Header("One-Way Platform")]
        [SerializeField] private PlatformEffector2D platformEffector;

        private int currentWaypointIndex;
        private int direction = 1;
        private float waitTimer;
        private bool isWaiting;
        private Vector3 lastPosition;

        // Falling state
        private bool isFalling;
        private bool isFallingTriggered;
        private float fallTimer;
        private Vector3 originalPosition;
        private Collider2D platformCollider;
        private SpriteRenderer spriteRenderer;

        public Vector3 Velocity { get; private set; }

        private void Awake()
        {
            platformCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            lastPosition = transform.position;
            originalPosition = transform.position;
            currentWaypointIndex = 0;
        }

        private void FixedUpdate()
        {
            if (behavior == PlatformBehavior.Falling)
            {
                UpdateFallingPlatform();
                return;
            }

            if (waypoints == null || waypoints.Length < 2) return;

            UpdateMovement();

            Velocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
            lastPosition = transform.position;
        }

        private void UpdateMovement()
        {
            if (isWaiting)
            {
                waitTimer -= Time.fixedDeltaTime;
                if (waitTimer <= 0f)
                {
                    isWaiting = false;
                    AdvanceWaypoint();
                }
                return;
            }

            Transform target = waypoints[currentWaypointIndex];
            if (target == null) return;

            Vector3 targetPos = target.position;
            float step = speed * Time.fixedDeltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

            if (Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                transform.position = targetPos;
                isWaiting = true;
                waitTimer = waitTime;
            }
        }

        private void AdvanceWaypoint()
        {
            switch (mode)
            {
                case PlatformMode.Loop:
                    currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                    break;

                case PlatformMode.PingPong:
                    currentWaypointIndex += direction;
                    if (currentWaypointIndex >= waypoints.Length - 1 || currentWaypointIndex <= 0)
                    {
                        direction = -direction;
                    }
                    break;

                case PlatformMode.OneWay:
                    if (currentWaypointIndex < waypoints.Length - 1)
                    {
                        currentWaypointIndex++;
                    }
                    break;
            }
        }

        private void UpdateFallingPlatform()
        {
            Velocity = Vector3.zero;

            if (isFalling)
            {
                transform.position += Vector3.down * (fallSpeed * Time.fixedDeltaTime);
                Velocity = Vector3.down * fallSpeed;
                lastPosition = transform.position;
                return;
            }

            if (isFallingTriggered)
            {
                fallTimer -= Time.fixedDeltaTime;

                // Shake while waiting to fall
                if (spriteRenderer != null)
                {
                    float shakeX = (Mathf.PerlinNoise(Time.time * 50f, 0f) * 2f - 1f) * shakeIntensity;
                    Vector3 pos = originalPosition;
                    pos.x += shakeX;
                    transform.position = pos;
                }

                if (fallTimer <= 0f)
                {
                    isFalling = true;
                    Invoke(nameof(Respawn), respawnDelay);
                }
            }
        }

        private void Respawn()
        {
            transform.position = originalPosition;
            isFalling = false;
            isFallingTriggered = false;

            if (platformCollider != null)
            {
                platformCollider.enabled = true;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.collider.CompareTag("Player")) return;

            // Parent player to platform
            collision.transform.SetParent(transform);

            // Trigger falling behavior
            if (behavior == PlatformBehavior.Falling && !isFallingTriggered)
            {
                isFallingTriggered = true;
                fallTimer = fallDelay;
                originalPosition = transform.position;
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!collision.collider.CompareTag("Player")) return;

            collision.transform.SetParent(null);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                Gizmos.DrawWireSphere(waypoints[i].position, 0.15f);

                if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }

            // Draw loop connection
            if (mode == PlatformMode.Loop && waypoints.Length > 1
                && waypoints[0] != null && waypoints[^1] != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(waypoints[^1].position, waypoints[0].position);
            }
        }
#endif
    }
}
