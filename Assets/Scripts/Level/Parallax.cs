using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Parallax scrolling for a single background layer.
    /// Attach to each background layer GameObject. Supports infinite tiling,
    /// camera-based parallax, auto-scrolling, and vertical parallax.
    /// </summary>
    public class Parallax : MonoBehaviour
    {
        [Header("Parallax Factor")]
        [Tooltip("0 = no movement (fixed background), 1 = moves with camera (foreground).")]
        [SerializeField] [Range(0f, 1f)] private float horizontalParallaxFactor = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float verticalParallaxFactor = 0.2f;

        [Header("Auto Scroll")]
        [SerializeField] private bool autoScroll;
        [SerializeField] private Vector2 autoScrollSpeed = new Vector2(-0.5f, 0f);

        [Header("Infinite Tiling")]
        [SerializeField] private bool infiniteHorizontal = true;
        [SerializeField] private bool infiniteVertical;

        private Transform cameraTransform;
        private Vector3 previousCameraPosition;
        private float textureUnitSizeX;
        private float textureUnitSizeY;
        private SpriteRenderer spriteRenderer;

        private void Start()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
                previousCameraPosition = cameraTransform.position;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Sprite sprite = spriteRenderer.sprite;
                Texture2D texture = sprite.texture;

                textureUnitSizeX = texture.width / sprite.pixelsPerUnit * transform.lossyScale.x;
                textureUnitSizeY = texture.height / sprite.pixelsPerUnit * transform.lossyScale.y;
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;

            Vector3 cameraDelta = cameraTransform.position - previousCameraPosition;
            previousCameraPosition = cameraTransform.position;

            // Parallax: the layer moves WITH the camera but at a reduced rate.
            // Factor 0 = layer stays fixed (distant background).
            // Factor 1 = layer moves exactly with the camera (foreground).
            Vector3 pos = transform.position;
            pos.x += cameraDelta.x * horizontalParallaxFactor;
            pos.y += cameraDelta.y * verticalParallaxFactor;

            // Auto-scroll adds continuous movement independent of camera
            if (autoScroll)
            {
                pos.x += autoScrollSpeed.x * Time.deltaTime;
                pos.y += autoScrollSpeed.y * Time.deltaTime;
            }

            transform.position = pos;

            // Infinite tiling: reposition when the camera moves past the texture bounds
            if (infiniteHorizontal && textureUnitSizeX > 0f)
            {
                float diffX = cameraTransform.position.x - transform.position.x;
                if (Mathf.Abs(diffX) >= textureUnitSizeX)
                {
                    float offsetX = diffX % textureUnitSizeX;
                    Vector3 p = transform.position;
                    p.x = cameraTransform.position.x - offsetX;
                    transform.position = p;
                }
            }

            if (infiniteVertical && textureUnitSizeY > 0f)
            {
                float diffY = cameraTransform.position.y - transform.position.y;
                if (Mathf.Abs(diffY) >= textureUnitSizeY)
                {
                    float offsetY = diffY % textureUnitSizeY;
                    Vector3 p = transform.position;
                    p.y = cameraTransform.position.y - offsetY;
                    transform.position = p;
                }
            }
        }

        /// <summary>
        /// Set the parallax factors at runtime.
        /// </summary>
        public void SetParallaxFactor(float horizontal, float vertical)
        {
            horizontalParallaxFactor = Mathf.Clamp01(horizontal);
            verticalParallaxFactor = Mathf.Clamp01(vertical);
        }

        /// <summary>
        /// Set auto-scroll speed at runtime.
        /// </summary>
        public void SetAutoScrollSpeed(Vector2 speed)
        {
            autoScrollSpeed = speed;
            autoScroll = speed.sqrMagnitude > 0.001f;
        }
    }
}
