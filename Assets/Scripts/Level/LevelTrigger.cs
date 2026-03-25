using UnityEngine;
using UnityEngine.Events;

namespace RunAndGun
{
    public enum TriggerAction
    {
        EnemySpawn,
        CameraLock,
        Dialogue,
        BossFight,
        Custom
    }

    [RequireComponent(typeof(Collider2D))]
    public class LevelTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [SerializeField] private TriggerAction triggerAction = TriggerAction.Custom;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private float activationDelay;
        [SerializeField] private bool requirePlayerTag = true;

        [Header("Enemy Spawn")]
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnInterval = 0.3f;

        [Header("Camera Lock")]
        [SerializeField] private Transform cameraLockTarget;
        [SerializeField] private float cameraLockSize = 7f;
        [SerializeField] private bool releaseCameraOnExit;

        [Header("Dialogue")]
        [SerializeField] private string dialogueId;
        [SerializeField] private string[] dialogueLines;

        [Header("Boss Fight")]
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private Transform bossSpawnPoint;
        [SerializeField] private Transform bossArenaCenter;
        [SerializeField] private float bossArenaSize = 8f;

        [Header("Events")]
        [SerializeField] private UnityEvent onTriggerActivated;
        [SerializeField] private UnityEvent onTriggerDeactivated;

        [Header("Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);

        private bool hasTriggered;
        private bool isActive;
        private Collider2D triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (oneShot && hasTriggered) return;
            if (requirePlayerTag && !other.CompareTag("Player")) return;

            if (activationDelay > 0f)
            {
                Invoke(nameof(Activate), activationDelay);
            }
            else
            {
                Activate();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!isActive) return;
            if (requirePlayerTag && !other.CompareTag("Player")) return;

            if (releaseCameraOnExit && triggerAction == TriggerAction.CameraLock)
            {
                ReleaseCameraLock();
            }

            onTriggerDeactivated?.Invoke();
            isActive = false;
        }

        private void Activate()
        {
            hasTriggered = true;
            isActive = true;

            switch (triggerAction)
            {
                case TriggerAction.EnemySpawn:
                    StartCoroutine(SpawnEnemiesRoutine());
                    break;

                case TriggerAction.CameraLock:
                    ApplyCameraLock();
                    break;

                case TriggerAction.Dialogue:
                    TriggerDialogue();
                    break;

                case TriggerAction.BossFight:
                    StartBossFight();
                    break;

                case TriggerAction.Custom:
                    break;
            }

            onTriggerActivated?.Invoke();
        }

        private System.Collections.IEnumerator SpawnEnemiesRoutine()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0) yield break;

            Transform[] points = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints
                : new[] { transform };

            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] == null) continue;

                Transform point = points[i % points.Length];
                Vector3 spawnPos = point.position;

                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Get(enemyPrefabs[i], spawnPos, Quaternion.identity);
                }
                else
                {
                    Instantiate(enemyPrefabs[i], spawnPos, Quaternion.identity);
                }

                if (spawnInterval > 0f)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }
        }

        private void ApplyCameraLock()
        {
            CameraController cam = FindAnyObjectByType<CameraController>();
            if (cam == null) return;

            Vector3 lockPos = cameraLockTarget != null
                ? cameraLockTarget.position
                : transform.position;

            cam.SetBossLock(lockPos, cameraLockSize);
        }

        private void ReleaseCameraLock()
        {
            CameraController cam = FindAnyObjectByType<CameraController>();
            if (cam != null)
            {
                cam.ReleaseBossLock();
            }
        }

        private void TriggerDialogue()
        {
            // Fires the onTriggerActivated event which can be wired to a dialogue manager.
            // The dialogueLines array and dialogueId are available for external systems.
            Debug.Log($"[LevelTrigger] Dialogue triggered: {dialogueId}");
        }

        private void StartBossFight()
        {
            // Lock camera to arena
            CameraController cam = FindAnyObjectByType<CameraController>();
            if (cam != null && bossArenaCenter != null)
            {
                cam.SetBossLock(bossArenaCenter.position, bossArenaSize);
            }

            // Spawn boss
            if (bossPrefab != null)
            {
                Vector3 spawnPos = bossSpawnPoint != null
                    ? bossSpawnPoint.position
                    : transform.position;

                Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            }
        }

        /// <summary>
        /// Reset the trigger so it can fire again. Useful for repeatable triggers.
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            isActive = false;
        }

        /// <summary>
        /// Get the dialogue lines configured for this trigger.
        /// </summary>
        public string[] GetDialogueLines()
        {
            return dialogueLines;
        }

        /// <summary>
        /// Get the dialogue identifier for this trigger.
        /// </summary>
        public string GetDialogueId()
        {
            return dialogueId;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            Collider2D col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.DrawWireCube(box.offset, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (col is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere(
                    transform.position + (Vector3)circle.offset,
                    circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, Vector3.one);
            }

            // Draw action-specific gizmos
            Gizmos.color = Color.white;
            switch (triggerAction)
            {
                case TriggerAction.EnemySpawn:
                    if (spawnPoints != null)
                    {
                        Gizmos.color = Color.red;
                        foreach (Transform sp in spawnPoints)
                        {
                            if (sp != null)
                            {
                                Gizmos.DrawWireSphere(sp.position, 0.25f);
                                Gizmos.DrawLine(transform.position, sp.position);
                            }
                        }
                    }
                    break;

                case TriggerAction.CameraLock:
                    Gizmos.color = Color.blue;
                    Vector3 lockPos = cameraLockTarget != null
                        ? cameraLockTarget.position
                        : transform.position;
                    float halfH = cameraLockSize;
                    float halfW = halfH * 1.78f; // approximate 16:9
                    Gizmos.DrawWireCube(lockPos, new Vector3(halfW * 2f, halfH * 2f, 0f));
                    break;

                case TriggerAction.BossFight:
                    Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                    if (bossArenaCenter != null)
                    {
                        float bH = bossArenaSize;
                        float bW = bH * 1.78f;
                        Gizmos.DrawWireCube(bossArenaCenter.position, new Vector3(bW * 2f, bH * 2f, 0f));
                    }
                    if (bossSpawnPoint != null)
                    {
                        Gizmos.DrawWireSphere(bossSpawnPoint.position, 0.4f);
                    }
                    break;
            }

            // Label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"[{triggerAction}]{(oneShot ? " (One-Shot)" : "")}");
        }
#endif
    }
}
