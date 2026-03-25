using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [System.Serializable]
        public struct PoolEntry
        {
            public GameObject prefab;
            public int prewarmCount;
        }

        [SerializeField] private PoolEntry[] prewarmEntries;

        private readonly Dictionary<int, Queue<GameObject>> pools = new();
        private readonly Dictionary<int, Transform> poolParents = new();

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
                foreach (PoolEntry entry in prewarmEntries)
                {
                    if (entry.prefab != null)
                    {
                        Prewarm(entry.prefab, entry.prewarmCount);
                    }
                }
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            int id = prefab.GetInstanceID();
            EnsurePoolExists(prefab, id);

            for (int i = 0; i < count; i++)
            {
                GameObject obj = CreateNewInstance(prefab, id);
                obj.SetActive(false);
                pools[id].Enqueue(obj);
            }
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            int id = prefab.GetInstanceID();
            EnsurePoolExists(prefab, id);

            GameObject obj;
            Queue<GameObject> queue = pools[id];

            // Find a valid (non-destroyed) object or create a new one
            while (queue.Count > 0)
            {
                obj = queue.Dequeue();
                if (obj != null)
                {
                    obj.transform.SetPositionAndRotation(position, rotation);
                    obj.SetActive(true);
                    return obj;
                }
            }

            // Pool exhausted, auto-grow
            obj = CreateNewInstance(prefab, id);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            return obj;
        }

        public void Return(GameObject prefab, GameObject obj)
        {
            if (obj == null) return;

            int id = prefab.GetInstanceID();
            EnsurePoolExists(prefab, id);

            obj.SetActive(false);
            obj.transform.SetParent(poolParents[id]);
            pools[id].Enqueue(obj);
        }

        public void Return(GameObject obj)
        {
            if (obj == null) return;

            PoolMember member = obj.GetComponent<PoolMember>();
            if (member != null && member.SourcePrefab != null)
            {
                Return(member.SourcePrefab, obj);
            }
            else
            {
                obj.SetActive(false);
            }
        }

        private void EnsurePoolExists(GameObject prefab, int id)
        {
            if (pools.ContainsKey(id)) return;

            pools[id] = new Queue<GameObject>();

            Transform parent = new GameObject($"Pool_{prefab.name}").transform;
            parent.SetParent(transform);
            poolParents[id] = parent;
        }

        private GameObject CreateNewInstance(GameObject prefab, int id)
        {
            GameObject obj = Instantiate(prefab, poolParents[id]);
            PoolMember member = obj.GetComponent<PoolMember>();
            if (member == null)
            {
                member = obj.AddComponent<PoolMember>();
            }
            member.SourcePrefab = prefab;
            return obj;
        }
    }

    public class PoolMember : MonoBehaviour
    {
        public GameObject SourcePrefab { get; set; }

        public void ReturnToPool()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Return(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
