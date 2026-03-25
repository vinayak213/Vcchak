using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Standalone screen shake utility using Perlin noise and a trauma system.
    /// Can be used independently of CameraController for non-camera shake effects,
    /// or as a global shake manager that delegates to CameraController.
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        public static ScreenShake Instance { get; private set; }

        [Header("Shake Settings")]
        [SerializeField] private float maxHorizontalShake = 0.5f;
        [SerializeField] private float maxVerticalShake = 0.5f;
        [SerializeField] private float maxRotationalShake = 5f;

        [Header("Noise")]
        [SerializeField] private float noiseFrequency = 25f;

        [Header("Trauma Decay")]
        [SerializeField] private float traumaDecayRate = 1.3f;
        [SerializeField] private float traumaExponent = 2f;

        private float trauma;
        private float seed;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private bool initialized;

        /// <summary>
        /// Current trauma level (0 to 1).
        /// </summary>
        public float Trauma => trauma;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            seed = Random.Range(0f, 10000f);
        }

        private void OnEnable()
        {
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            initialized = true;
        }

        private void OnDisable()
        {
            if (initialized)
            {
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
            }
        }

        private void Update()
        {
            if (trauma <= 0f)
            {
                if (initialized)
                {
                    transform.localPosition = originalLocalPosition;
                    transform.localRotation = originalLocalRotation;
                }
                return;
            }

            float shake = Mathf.Pow(trauma, traumaExponent);
            float time = Time.time + seed;

            // Perlin noise offsets (centered around 0 by subtracting 0.5 and multiplying by 2)
            float noiseX = (Mathf.PerlinNoise(time * noiseFrequency, seed) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(seed, time * noiseFrequency) - 0.5f) * 2f;
            float noiseRot = (Mathf.PerlinNoise(time * noiseFrequency, time * noiseFrequency + seed) - 0.5f) * 2f;

            Vector3 shakeOffset = new Vector3(
                noiseX * maxHorizontalShake * shake,
                noiseY * maxVerticalShake * shake,
                0f
            );

            float shakeRotation = noiseRot * maxRotationalShake * shake;

            transform.localPosition = originalLocalPosition + shakeOffset;
            transform.localRotation = originalLocalRotation * Quaternion.Euler(0f, 0f, shakeRotation);

            // Decay trauma
            trauma = Mathf.Max(0f, trauma - traumaDecayRate * Time.deltaTime);
        }

        /// <summary>
        /// Add trauma to the shake system. Values are clamped to [0, 1].
        /// </summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>
        /// Set trauma directly. Values are clamped to [0, 1].
        /// </summary>
        public void SetTrauma(float value)
        {
            trauma = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Convenience method: trigger a shake with a specific intensity.
        /// Intensity maps directly to trauma added.
        /// </summary>
        public void Shake(float intensity)
        {
            AddTrauma(intensity);
        }

        /// <summary>
        /// Trigger a shake and also forward it to the CameraController if available.
        /// </summary>
        public void ShakeCamera(float intensity, float duration)
        {
            AddTrauma(intensity);

            CameraController cam = FindAnyObjectByType<CameraController>();
            if (cam != null)
            {
                cam.Shake(intensity, duration);
            }
        }

        /// <summary>
        /// Immediately stop all shake.
        /// </summary>
        public void StopShake()
        {
            trauma = 0f;
            if (initialized)
            {
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
