using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Wave-based enemy spawner. Supports multiple spawn points, maximum active
    /// enemy cap, player-position or event triggering, and configurable cooldown
    /// between waves.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        // ================================================================== //
        //  DATA TYPES
        // ================================================================== //

        [Serializable]
        public struct SpawnEntry
        {
            [Tooltip("Enemy prefab to spawn.")]
            public GameObject Prefab;

            [Min(1)]
            [Tooltip("How many of this enemy to spawn in the wave.")]
            public int Count;

            [Tooltip("Delay before spawning the next batch in this entry.")]
            public float SpawnDelay;
        }

        [Serializable]
        public struct Wave
        {
            [Tooltip("Display name (for debug / UI).")]
            public string Name;

            [Tooltip("Enemies that make up this wave.")]
            public SpawnEntry[] Entries;

            [Tooltip("Cooldown after this wave completes before the next can start.")]
            public float CooldownAfter;
        }

        // ================================================================== //
        //  INSPECTOR
        // ================================================================== //
        [Header("Waves")]
        [SerializeField] private Wave[] waves;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Limits")]
        [SerializeField] private int maxActiveEnemies = 10;

        [Header("Trigger")]
        [Tooltip("If set, waves start only when the player enters the trigger zone.")]
        [SerializeField] private bool usePlayerProximityTrigger;
        [SerializeField] private float triggerDistance = 12f;
        [SerializeField] private bool triggerOnce = true;

        [Header("Auto Start")]
        [Tooltip("Start spawning immediately on enable (ignored if proximity trigger is on).")]
        [SerializeField] private bool autoStart;

        // ================================================================== //
        //  EVENTS
        // ================================================================== //
        /// <summary>Raised when a new wave begins. Arg: wave index (0-based).</summary>
        public event Action<int> OnWaveStarted;
        /// <summary>Raised when a wave's enemies are all dead. Arg: wave index.</summary>
        public event Action<int> OnWaveCleared;
        /// <summary>Raised when all waves are complete.</summary>
        public event Action OnAllWavesComplete;

        // ================================================================== //
        //  RUNTIME
        // ================================================================== //
        private readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>();
        private Transform _playerTarget;
        private int _currentWaveIndex;
        private bool _isSpawning;
        private bool _allWavesDone;
        private bool _hasTriggered;
        private Coroutine _spawnRoutine;

        public int CurrentWaveIndex => _currentWaveIndex;
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int ActiveEnemyCount => _activeEnemies.Count;
        public bool IsSpawning => _isSpawning;
        public bool AllWavesComplete => _allWavesDone;

        // ================================================================== //
        //  UNITY LIFECYCLE
        // ================================================================== //

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTarget = player.transform;

            if (autoStart && !usePlayerProximityTrigger)
            {
                BeginSpawning();
            }
        }

        private void Update()
        {
            if (_allWavesDone || _isSpawning) return;

            if (usePlayerProximityTrigger && !_hasTriggered)
            {
                if (_playerTarget != null)
                {
                    float dist = Vector2.Distance(transform.position, _playerTarget.position);
                    if (dist <= triggerDistance)
                    {
                        _hasTriggered = true;
                        BeginSpawning();
                    }
                }
            }

            // Clean up destroyed references.
            _activeEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
        }

        private void OnDisable()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
            _isSpawning = false;
        }

        // ================================================================== //
        //  PUBLIC API
        // ================================================================== //

        /// <summary>
        /// Externally trigger the spawn sequence (e.g., from a cutscene or event).
        /// </summary>
        public void BeginSpawning()
        {
            if (_allWavesDone) return;
            if (_isSpawning) return;

            _spawnRoutine = StartCoroutine(SpawnAllWaves());
        }

        /// <summary>
        /// Force-stop spawning and kill all active enemies.
        /// </summary>
        public void StopAndClear()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
            _isSpawning = false;

            foreach (EnemyBase enemy in _activeEnemies)
            {
                if (enemy != null && !enemy.IsDead)
                    enemy.ForceKill();
            }

            _activeEnemies.Clear();
        }

        // ================================================================== //
        //  SPAWN LOGIC
        // ================================================================== //

        private IEnumerator SpawnAllWaves()
        {
            _isSpawning = true;

            for (int w = _currentWaveIndex; w < waves.Length; w++)
            {
                _currentWaveIndex = w;
                OnWaveStarted?.Invoke(w);

                yield return SpawnWave(waves[w]);

                // Wait until every enemy in the wave is dead.
                yield return WaitForWaveClear();
                OnWaveCleared?.Invoke(w);

                // Cooldown between waves.
                if (w < waves.Length - 1 && waves[w].CooldownAfter > 0f)
                {
                    yield return new WaitForSeconds(waves[w].CooldownAfter);
                }
            }

            _allWavesDone = true;
            _isSpawning = false;
            _spawnRoutine = null;
            OnAllWavesComplete?.Invoke();
        }

        private IEnumerator SpawnWave(Wave wave)
        {
            if (wave.Entries == null) yield break;

            int spawnPointIndex = 0;

            foreach (SpawnEntry entry in wave.Entries)
            {
                if (entry.Prefab == null) continue;

                for (int i = 0; i < entry.Count; i++)
                {
                    // Wait if we've hit the active enemy cap.
                    while (_activeEnemies.Count >= maxActiveEnemies)
                    {
                        _activeEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
                        yield return null;
                    }

                    Vector3 pos = GetSpawnPosition(ref spawnPointIndex);
                    GameObject obj;

                    if (ObjectPool.Instance != null)
                    {
                        obj = ObjectPool.Instance.Get(entry.Prefab, pos, Quaternion.identity);
                    }
                    else
                    {
                        obj = Instantiate(entry.Prefab, pos, Quaternion.identity);
                    }

                    EnemyBase enemy = obj.GetComponent<EnemyBase>();
                    if (enemy != null)
                    {
                        _activeEnemies.Add(enemy);
                        enemy.OnDeath += HandleEnemyDeath;
                    }

                    if (entry.SpawnDelay > 0f)
                        yield return new WaitForSeconds(entry.SpawnDelay);
                }
            }
        }

        private IEnumerator WaitForWaveClear()
        {
            while (true)
            {
                _activeEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);
                if (_activeEnemies.Count == 0) yield break;
                yield return null;
            }
        }

        // ================================================================== //
        //  HELPERS
        // ================================================================== //

        private Vector3 GetSpawnPosition(ref int spawnPointIndex)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform.position;

            Vector3 pos = spawnPoints[spawnPointIndex % spawnPoints.Length].position;
            spawnPointIndex++;
            return pos;
        }

        private void HandleEnemyDeath(EnemyBase enemy)
        {
            enemy.OnDeath -= HandleEnemyDeath;
            _activeEnemies.Remove(enemy);

            // Award score.
            if (GameManager.Instance != null && enemy.Data != null)
            {
                GameManager.Instance.AddScore(enemy.Data.ScoreValue);
            }
        }

        // ================================================================== //
        //  GIZMOS
        // ================================================================== //

        private void OnDrawGizmosSelected()
        {
            if (usePlayerProximityTrigger)
            {
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, triggerDistance);
            }

            if (spawnPoints != null)
            {
                Gizmos.color = Color.red;
                foreach (Transform sp in spawnPoints)
                {
                    if (sp != null)
                        Gizmos.DrawWireCube(sp.position, Vector3.one * 0.4f);
                }
            }
        }
    }
}
