using UnityEngine;

namespace RunAndGun
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow")]
        [SerializeField] private Vector3 offset = new(0f, 1f, -10f);
        [SerializeField] private float horizontalDamping = 0.12f;
        [SerializeField] private float verticalDamping = 0.15f;

        [Header("Look Ahead")]
        [SerializeField] private float lookAheadDistance = 2.5f;
        [SerializeField] private float lookAheadSpeed = 3f;

        [Header("Vertical Look")]
        [SerializeField] private float verticalLookDistance = 3f;
        [SerializeField] private float verticalLookSpeed = 2f;

        [Header("Bounds")]
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 boundsMin;
        [SerializeField] private Vector2 boundsMax;

        [Header("Zoom")]
        [SerializeField] private float defaultOrthographicSize = 5f;
        [SerializeField] private float zoomTransitionSpeed = 2f;

        private Camera cam;
        private Vector3 currentVelocity;
        private float lookAheadOffset;
        private float currentVerticalLookOffset;
        private float targetVerticalLookOffset;
        private float targetOrthographicSize;

        // Screen shake
        private float shakeTrauma;
        private float shakeIntensity;
        private float shakeDecay;
        private float shakeTimeOffset;

        // Boss fight lock
        private bool isBossLocked;
        private Vector3 bossLockPosition;
        private float bossLockSize;

        private Vector3 previousTargetPosition;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            targetOrthographicSize = defaultOrthographicSize;
            cam.orthographicSize = defaultOrthographicSize;
        }

        private void Start()
        {
            if (target != null)
            {
                previousTargetPosition = target.position;
                Vector3 desired = target.position + offset;
                desired.z = offset.z;
                transform.position = desired;
            }

            shakeTimeOffset = Random.Range(0f, 1000f);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 targetPos;

            if (isBossLocked)
            {
                targetPos = bossLockPosition;
                targetPos.z = offset.z;
                targetOrthographicSize = bossLockSize;
            }
            else
            {
                targetPos = CalculateDesiredPosition();
            }

            // Smooth follow
            Vector3 smoothed;
            smoothed.x = Mathf.SmoothDamp(transform.position.x, targetPos.x, ref currentVelocity.x, horizontalDamping);
            smoothed.y = Mathf.SmoothDamp(transform.position.y, targetPos.y, ref currentVelocity.y, verticalDamping);
            smoothed.z = targetPos.z;

            // Clamp to bounds
            if (useBounds)
            {
                smoothed = ClampToBounds(smoothed);
            }

            // Apply screen shake
            smoothed += GetShakeOffset();

            transform.position = smoothed;

            // Zoom
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthographicSize,
                Time.deltaTime * zoomTransitionSpeed);

            // Decay shake trauma
            if (shakeTrauma > 0f)
            {
                shakeTrauma = Mathf.Max(0f, shakeTrauma - shakeDecay * Time.deltaTime);
            }

            previousTargetPosition = target.position;
        }

        private Vector3 CalculateDesiredPosition()
        {
            Vector3 delta = target.position - previousTargetPosition;
            float moveDir = 0f;
            if (Mathf.Abs(delta.x) > 0.001f)
            {
                moveDir = Mathf.Sign(delta.x);
            }

            float targetLookAhead = moveDir * lookAheadDistance;
            lookAheadOffset = Mathf.Lerp(lookAheadOffset, targetLookAhead, Time.deltaTime * lookAheadSpeed);

            // Vertical look
            currentVerticalLookOffset = Mathf.Lerp(currentVerticalLookOffset, targetVerticalLookOffset,
                Time.deltaTime * verticalLookSpeed);

            Vector3 desired = target.position + offset;
            desired.x += lookAheadOffset;
            desired.y += currentVerticalLookOffset;
            desired.z = offset.z;

            return desired;
        }

        private Vector3 ClampToBounds(Vector3 pos)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            pos.x = Mathf.Clamp(pos.x, boundsMin.x + halfWidth, boundsMax.x - halfWidth);
            pos.y = Mathf.Clamp(pos.y, boundsMin.y + halfHeight, boundsMax.y - halfHeight);

            return pos;
        }

        private Vector3 GetShakeOffset()
        {
            if (shakeTrauma <= 0f) return Vector3.zero;

            float shake = shakeTrauma * shakeTrauma; // quadratic falloff
            float time = Time.time + shakeTimeOffset;

            float offsetX = (Mathf.PerlinNoise(time * 25f, 0f) * 2f - 1f) * shakeIntensity * shake;
            float offsetY = (Mathf.PerlinNoise(0f, time * 25f) * 2f - 1f) * shakeIntensity * shake;

            return new Vector3(offsetX, offsetY, 0f);
        }

        // --- Public API ---

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                previousTargetPosition = target.position;
            }
        }

        /// <summary>
        /// Set vertical look offset. Pass +1 for looking up, -1 for looking down (crouch), 0 for neutral.
        /// </summary>
        public void SetVerticalLook(float direction)
        {
            targetVerticalLookOffset = Mathf.Clamp(direction, -1f, 1f) * verticalLookDistance;
        }

        public void Shake(float intensity, float duration)
        {
            shakeTrauma = 1f;
            shakeIntensity = intensity;
            shakeDecay = duration > 0f ? 1f / duration : 100f;
        }

        public void AddTrauma(float amount)
        {
            shakeTrauma = Mathf.Clamp01(shakeTrauma + amount);
        }

        public void SetBossLock(Vector3 center, float orthographicSize)
        {
            isBossLocked = true;
            bossLockPosition = center;
            bossLockSize = orthographicSize;
        }

        public void ReleaseBossLock()
        {
            isBossLocked = false;
            targetOrthographicSize = defaultOrthographicSize;
        }

        public void SetZoom(float orthographicSize)
        {
            targetOrthographicSize = orthographicSize;
        }

        public void ResetZoom()
        {
            targetOrthographicSize = defaultOrthographicSize;
        }

        public void SetBounds(Vector2 min, Vector2 max)
        {
            useBounds = true;
            boundsMin = min;
            boundsMax = max;
        }

        public void ClearBounds()
        {
            useBounds = false;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            desired.z = offset.z;
            transform.position = desired;
            currentVelocity = Vector3.zero;
            previousTargetPosition = target.position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!useBounds) return;

            Gizmos.color = Color.yellow;
            Vector3 center = new((boundsMin.x + boundsMax.x) * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f, 0f);
            Vector3 size = new(boundsMax.x - boundsMin.x, boundsMax.y - boundsMin.y, 0f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
