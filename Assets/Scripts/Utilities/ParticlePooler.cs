using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Specialized pool for ParticleSystem prefabs. Automatically returns
    /// particle systems to the pool when they finish playing.
    /// </summary>
    public class ParticlePooler : MonoBehaviour
    {
        public static ParticlePooler Instance { get; private set; }

        [System.Serializable]
        public struct ParticlePoolEntry
        {
            public GameObject prefab;
            public int prewarmCount;
        }

        [SerializeField] private ParticlePoolEntry[] prewarmEntries;

        private readonly Dictionary<int, Queue<ParticleInstance>> pools = new();
        private readonly Dictionary<int, Transform> poolParents = new();
        private readonly List<ParticleInstance> activeParticles = new();

        private struct ParticleInstance
        {
            public GameObject gameObject;
            public ParticleSystem particleSystem;
            public int prefabId;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (prewarmEntries != null)
            {
                foreach (ParticlePoolEntry entry in prewarmEntries)
                {
                    if (entry.prefab != null)
                    {
                        Prewarm(entry.prefab, entry.prewarmCount);
                    }
                }
            }
        }

        private void Update()
        {
            // Check active particles and return finished ones to pool
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                ParticleInstance instance = activeParticles[i];

                if (instance.gameObject == null)
                {
                    activeParticles.RemoveAt(i);
                    continue;
                }

                if (!instance.particleSystem.IsAlive(true))
                {
                    ReturnToPool(instance);
                    activeParticles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Pre-create a number of particle system instances for a prefab.
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            int id = prefab.GetInstanceID();
            EnsurePoolExists(prefab, id);

            for (int i = 0; i < count; i++)
            {
                ParticleInstance instance = CreateInstance(prefab, id);
                instance.gameObject.SetActive(false);
                pools[id].Enqueue(instance);
            }
        }

        /// <summary>
        /// Spawn a particle effect at the given position and rotation.
        /// Returns the spawned GameObject.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            int id = prefab.GetInstanceID();
            EnsurePoolExists(prefab, id);

            ParticleInstance instance;
            Queue<ParticleInstance> queue = pools[id];

            // Find a valid pooled instance
            while (queue.Count > 0)
            {
                instance = queue.Dequeue();
                if (instance.gameObject != null)
                {
                    ActivateInstance(instance, position, rotation);
                    return instance.gameObject;
                }
            }

            // Pool exhausted, create new
            instance = CreateInstance(prefab, id);
            ActivateInstance(instance, position, rotation);
            return instance.gameObject;
        }

        /// <summary>
        /// Spawn a particle effect at the given position with default rotation.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return Spawn(prefab, position, Quaternion.identity);
        }

        /// <summary>
        /// Immediately stop and return a particle to the pool.
        /// </summary>
        public void Despawn(GameObject particleObject)
        {
            if (particleObject == null) return;

            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                if (activeParticles[i].gameObject == particleObject)
                {
                    ReturnToPool(activeParticles[i]);
                    activeParticles.RemoveAt(i);
                    return;
                }
            }

            // Not tracked, just disable
            particleObject.SetActive(false);
        }

        /// <summary>
        /// Return all active particles to their pools.
        /// </summary>
        public void DespawnAll()
        {
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                if (activeParticles[i].gameObject != null)
                {
                    ReturnToPool(activeParticles[i]);
                }
            }
            activeParticles.Clear();
        }

        private void ActivateInstance(ParticleInstance instance, Vector3 position, Quaternion rotation)
        {
            instance.gameObject.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);

            instance.particleSystem.Clear(true);
            instance.particleSystem.Play(true);

            activeParticles.Add(instance);
        }

        private void ReturnToPool(ParticleInstance instance)
        {
            if (instance.gameObject == null) return;

            instance.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.gameObject.SetActive(false);

            if (pools.ContainsKey(instance.prefabId) && poolParents.ContainsKey(instance.prefabId))
            {
                instance.gameObject.transform.SetParent(poolParents[instance.prefabId]);
                pools[instance.prefabId].Enqueue(instance);
            }
        }

        private void EnsurePoolExists(GameObject prefab, int id)
        {
            if (pools.ContainsKey(id)) return;

            pools[id] = new Queue<ParticleInstance>();

            Transform parent = new GameObject($"ParticlePool_{prefab.name}").transform;
            parent.SetParent(transform);
            poolParents[id] = parent;
        }

        private ParticleInstance CreateInstance(GameObject prefab, int id)
        {
            GameObject obj = Instantiate(prefab, poolParents[id]);
            ParticleSystem ps = obj.GetComponent<ParticleSystem>();

            if (ps == null)
            {
                ps = obj.GetComponentInChildren<ParticleSystem>();
            }

            // Ensure particle system does not loop indefinitely (would never auto-return)
            if (ps != null)
            {
                var main = ps.main;
                if (main.loop)
                {
                    Debug.LogWarning(
                        $"[ParticlePooler] Prefab '{prefab.name}' has looping ParticleSystem. " +
                        "Looping particles will not auto-return to pool. Call Despawn() manually.");
                }
            }

            return new ParticleInstance
            {
                gameObject = obj,
                particleSystem = ps,
                prefabId = id
            };
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
