using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    [Serializable]
    public class CheckpointData
    {
        [SerializeField] private string id;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private bool activated;

        public string Id => id;
        public Transform SpawnPoint => spawnPoint;
        public bool Activated { get => activated; set => activated = value; }
    }

    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Info")]
        [SerializeField] private int levelIndex;
        [SerializeField] private string levelDisplayName = "Level 1";

        [Header("Spawn")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private GameObject playerPrefab;

        [Header("Checkpoints")]
        [SerializeField] private List<CheckpointData> checkpoints = new();

        [Header("Camera Bounds")]
        [SerializeField] private Collider2D cameraBoundsCollider;
        [SerializeField] private Vector2 cameraBoundsMin;
        [SerializeField] private Vector2 cameraBoundsMax;
        [SerializeField] private bool useColliderBounds = true;

        [Header("Level Settings")]
        [SerializeField] private AudioClip levelMusic;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Color ambientColor = Color.white;

        [Header("Timer")]
        [SerializeField] private bool enableTimer;
        [SerializeField] private float timeLimitSeconds = 300f;

        [Header("Completion")]
        [SerializeField] private bool requireAllEnemiesDefeated;
        [SerializeField] private bool requireReachEnd = true;

        private CheckpointData activeCheckpoint;
        private float elapsedTime;
        private bool timerRunning;
        private bool levelCompleted;
        private int enemiesRemaining;

        public string LevelDisplayName => levelDisplayName;
        public int LevelIndex => levelIndex;
        public float ElapsedTime => elapsedTime;
        public float TimeRemaining => Mathf.Max(0f, timeLimitSeconds - elapsedTime);
        public bool TimerEnabled => enableTimer;
        public bool IsTimerRunning => timerRunning;
        public bool IsLevelCompleted => levelCompleted;
        public CheckpointData ActiveCheckpoint => activeCheckpoint;

        public event Action OnLevelCompleted;
        public event Action OnTimerExpired;
        public event Action<CheckpointData> OnCheckpointActivated;
        public event Action<float> OnTimerTick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(Instance.gameObject);
            }

            Instance = this;
        }

        private void Start()
        {
            InitializeLevel();
        }

        private void Update()
        {
            if (!timerRunning) return;

            elapsedTime += Time.deltaTime;
            OnTimerTick?.Invoke(enableTimer ? TimeRemaining : elapsedTime);

            if (enableTimer && elapsedTime >= timeLimitSeconds)
            {
                timerRunning = false;
                OnTimerExpired?.Invoke();
            }
        }

        private void InitializeLevel()
        {
            elapsedTime = 0f;
            levelCompleted = false;

            ApplyLevelSettings();
            SpawnPlayer();
            StartTimer();
        }

        private void ApplyLevelSettings()
        {
            if (levelMusic != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(levelMusic);
            }

            if (backgroundSprite != null)
            {
                SpriteRenderer bgRenderer = GameObject.FindWithTag("Background")?.GetComponent<SpriteRenderer>();
                if (bgRenderer != null)
                {
                    bgRenderer.sprite = backgroundSprite;
                }
            }

            Camera.main.backgroundColor = ambientColor;

            if (useColliderBounds && cameraBoundsCollider != null)
            {
                Bounds bounds = cameraBoundsCollider.bounds;
                cameraBoundsMin = bounds.min;
                cameraBoundsMax = bounds.max;
            }
        }

        private void SpawnPlayer()
        {
            if (playerPrefab == null || playerSpawnPoint == null) return;

            Vector3 spawnPos = GetCurrentSpawnPosition();
            GameObject existingPlayer = GameObject.FindWithTag("Player");

            if (existingPlayer == null)
            {
                Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                existingPlayer.transform.position = spawnPos;
            }
        }

        public Vector3 GetCurrentSpawnPosition()
        {
            if (activeCheckpoint != null && activeCheckpoint.SpawnPoint != null)
                return activeCheckpoint.SpawnPoint.position;

            if (playerSpawnPoint != null)
                return playerSpawnPoint.position;

            return Vector3.zero;
        }

        public void ActivateCheckpoint(string checkpointId)
        {
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i].Id == checkpointId && !checkpoints[i].Activated)
                {
                    checkpoints[i].Activated = true;
                    activeCheckpoint = checkpoints[i];
                    OnCheckpointActivated?.Invoke(activeCheckpoint);
                    return;
                }
            }
        }

        public void RespawnPlayer()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                player.transform.position = GetCurrentSpawnPosition();
            }
        }

        public void RegisterEnemy()
        {
            enemiesRemaining++;
        }

        public void OnEnemyDefeated()
        {
            enemiesRemaining--;
            if (enemiesRemaining < 0) enemiesRemaining = 0;

            if (requireAllEnemiesDefeated && enemiesRemaining <= 0 && !requireReachEnd)
            {
                TriggerLevelComplete();
            }
        }

        public void ReachLevelEnd()
        {
            if (requireAllEnemiesDefeated && enemiesRemaining > 0)
                return;

            if (requireReachEnd)
            {
                TriggerLevelComplete();
            }
        }

        public void TriggerLevelComplete()
        {
            if (levelCompleted) return;

            levelCompleted = true;
            timerRunning = false;

            OnLevelCompleted?.Invoke();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CompleteLevel();
            }
        }

        public void StartTimer()
        {
            timerRunning = true;
        }

        public void PauseTimer()
        {
            timerRunning = false;
        }

        public void ResumeTimer()
        {
            timerRunning = true;
        }

        public Vector2 GetCameraBoundsMin()
        {
            return cameraBoundsMin;
        }

        public Vector2 GetCameraBoundsMax()
        {
            return cameraBoundsMax;
        }

        public Vector2 ClampPositionToBounds(Vector2 position)
        {
            float clampedX = Mathf.Clamp(position.x, cameraBoundsMin.x, cameraBoundsMax.x);
            float clampedY = Mathf.Clamp(position.y, cameraBoundsMin.y, cameraBoundsMax.y);
            return new Vector2(clampedX, clampedY);
        }

        public int GetStarRating()
        {
            if (!levelCompleted) return 0;

            int stars = 1;

            if (enableTimer && elapsedTime < timeLimitSeconds * 0.5f)
                stars++;

            if (GameManager.Instance != null && GameManager.Instance.Lives >= GameManager.Instance.MaxLives)
                stars++;

            return stars;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
