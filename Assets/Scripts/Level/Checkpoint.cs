using UnityEngine;
using UnityEngine.Events;

namespace RunAndGun
{
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private SpriteRenderer flagRenderer;
        [SerializeField] private Sprite inactiveSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private GameObject activeLightEffect;

        [Header("Spawn Point")]
        [SerializeField] private Transform respawnPoint;

        [Header("Events")]
        [SerializeField] private UnityEvent onActivated;

        [Header("Audio")]
        [SerializeField] private AudioClip activateSound;

        private bool isActive;

        private static Checkpoint currentCheckpoint;

        public static Checkpoint Current => currentCheckpoint;
        public bool IsActive => isActive;

        public Vector3 RespawnPosition =>
            respawnPoint != null ? respawnPoint.position : transform.position;

        private void Awake()
        {
            SetVisualState(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isActive) return;
            if (!other.CompareTag("Player")) return;

            Activate();
        }

        public void Activate()
        {
            // Deactivate previous checkpoint
            if (currentCheckpoint != null && currentCheckpoint != this)
            {
                currentCheckpoint.Deactivate();
            }

            currentCheckpoint = this;
            isActive = true;

            SetVisualState(true);
            PlayActivateSound();
            onActivated?.Invoke();
        }

        private void Deactivate()
        {
            isActive = false;
            SetVisualState(false);
        }

        private void SetVisualState(bool active)
        {
            if (flagRenderer != null)
            {
                flagRenderer.sprite = active ? activeSprite : inactiveSprite;
                flagRenderer.color = active ? activeColor : inactiveColor;
            }

            if (activeLightEffect != null)
            {
                activeLightEffect.SetActive(active);
            }
        }

        private void PlayActivateSound()
        {
            if (activateSound != null)
            {
                AudioSource.PlayClipAtPoint(activateSound, transform.position);
            }
        }

        /// <summary>
        /// Returns the respawn position for the currently active checkpoint.
        /// Falls back to Vector3.zero if no checkpoint is active.
        /// </summary>
        public static Vector3 GetRespawnPosition()
        {
            return currentCheckpoint != null ? currentCheckpoint.RespawnPosition : Vector3.zero;
        }

        /// <summary>
        /// Clears all checkpoint state. Call when loading a new level.
        /// </summary>
        public static void ResetAll()
        {
            currentCheckpoint = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isActive ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            Vector3 spawn = respawnPoint != null ? respawnPoint.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawn, 0.2f);
            Gizmos.DrawLine(transform.position, spawn);
        }
#endif
    }
}
