using UnityEngine;

namespace RunAndGun
{
    public enum HazardType
    {
        Spikes,
        Fire,
        Acid,
        Electric
    }

    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        [Header("Hazard Settings")]
        [SerializeField] private HazardType hazardType = HazardType.Spikes;
        [SerializeField] private float damage = 20f;
        [SerializeField] private DamageType damageType = DamageType.Normal;
        [SerializeField] private float damageCooldown = 1f;

        [Header("Periodic Activation")]
        [SerializeField] private bool isPeriodic;
        [SerializeField] private float activeTime = 2f;
        [SerializeField] private float inactiveTime = 2f;
        [SerializeField] private float startDelay;

        [Header("Visual Warning")]
        [SerializeField] private float warningTime = 0.5f;
        [SerializeField] private SpriteRenderer warningIndicator;
        [SerializeField] private Color warningColor = new(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private SpriteRenderer hazardRenderer;
        [SerializeField] private Animator hazardAnimator;

        [Header("Audio")]
        [SerializeField] private AudioClip activateSound;
        [SerializeField] private AudioSource audioSource;

        private Collider2D hazardCollider;
        private float cycleTimer;
        private bool isCurrentlyActive = true;
        private bool isWarning;
        private float lastDamageTime = float.NegativeInfinity;

        private static readonly int AnimActive = Animator.StringToHash("Active");
        private static readonly int AnimWarning = Animator.StringToHash("Warning");

        private void Awake()
        {
            hazardCollider = GetComponent<Collider2D>();
            hazardCollider.isTrigger = true;

            // Auto-assign damage type from hazard type if left as Normal
            if (damageType == DamageType.Normal)
            {
                damageType = hazardType switch
                {
                    HazardType.Fire => DamageType.Fire,
                    HazardType.Electric => DamageType.Electric,
                    HazardType.Acid => DamageType.Fire,
                    _ => DamageType.Normal
                };
            }
        }

        private void Start()
        {
            if (isPeriodic)
            {
                cycleTimer = -startDelay;
                SetHazardActive(false);
            }
            else
            {
                SetHazardActive(true);
            }
        }

        private void Update()
        {
            if (!isPeriodic) return;

            cycleTimer += Time.deltaTime;

            if (!isCurrentlyActive)
            {
                float warningStart = inactiveTime - warningTime;

                if (cycleTimer >= warningStart && !isWarning)
                {
                    ShowWarning(true);
                }

                if (cycleTimer >= inactiveTime)
                {
                    SetHazardActive(true);
                    ShowWarning(false);
                    cycleTimer = 0f;
                }
            }
            else
            {
                if (cycleTimer >= activeTime)
                {
                    SetHazardActive(false);
                    cycleTimer = 0f;
                }
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isCurrentlyActive) return;
            if (!other.CompareTag("Player")) return;
            if (Time.time - lastDamageTime < damageCooldown) return;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                DamageInfo info = new()
                {
                    amount = damage,
                    type = damageType,
                    source = gameObject,
                    hitPoint = other.ClosestPoint(transform.position),
                    isCritical = false
                };

                damageable.TakeDamage(info);
                lastDamageTime = Time.time;
            }
        }

        private void SetHazardActive(bool active)
        {
            isCurrentlyActive = active;
            hazardCollider.enabled = active;

            if (hazardRenderer != null)
            {
                hazardRenderer.enabled = active;
            }

            if (hazardAnimator != null)
            {
                hazardAnimator.SetBool(AnimActive, active);
            }

            if (active && activateSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(activateSound);
            }
        }

        private void ShowWarning(bool show)
        {
            isWarning = show;

            if (warningIndicator != null)
            {
                warningIndicator.enabled = show;
                if (show)
                {
                    warningIndicator.color = warningColor;
                }
            }

            if (hazardAnimator != null)
            {
                hazardAnimator.SetBool(AnimWarning, show);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color c = hazardType switch
            {
                HazardType.Spikes => Color.gray,
                HazardType.Fire => Color.red,
                HazardType.Acid => Color.green,
                HazardType.Electric => Color.yellow,
                _ => Color.white
            };
            c.a = 0.4f;
            Gizmos.color = c;

            Collider2D col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
#endif
    }
}
