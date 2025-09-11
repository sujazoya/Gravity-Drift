// ObjectPooler.cs
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
// Alias FishNet.SceneManager to avoid conflict
using FishNetSceneManager = FishNet.Managing.Scened.SceneManager;

namespace zoya.game
{



    [Serializable]
    public class Pool
    {
        public string poolId;
        public NetworkObject prefab;
        public int initialSize = 10;
        public int maxSize = 100;
        public bool expandable = true;
        public PoolCategory category = PoolCategory.Gameplay;
        public float autoCleanupTimeout = 60f;

        [HideInInspector] public int currentSize;
        [HideInInspector] public int activeCount;
    }

    public enum PoolCategory
    {
        Gameplay,
        UI,
        Effects,
        Audio,
        Projectiles,
        Environment
    }

    public class ObjectPooler : NetworkBehaviour
    {
        public static ObjectPooler Instance { get; private set; }

        [Header("Pool Configuration")]
        [SerializeField] private List<Pool> pools = new List<Pool>();
        [SerializeField] private bool prewarmOnStart = true;
        [SerializeField] private bool enableAutoCleanup = true;
        [SerializeField] private float cleanupCheckInterval = 30f;

        [Header("Performance Monitoring")]
        [SerializeField] private bool enablePerformanceStats = true;
        [SerializeField] private int maxPerformanceHistory = 100;

        public readonly SyncDictionary<string, int> _poolStatistics = new SyncDictionary<string, int>();

        // runtime collections (not networked)
        private Dictionary<string, Queue<NetworkObject>> _poolDictionary;
        private Dictionary<NetworkObject, PooledObjectInfo> _activeObjects;
        private Dictionary<PoolCategory, List<Pool>> _categorizedPools;

        private Coroutine _cleanupCoroutine;
        private PerformanceStats _performanceStats;

        [Serializable]
        private class PerformanceStats
        {
            public List<float> spawnTimes = new List<float>();
            public List<float> despawnTimes = new List<float>();
            public int totalSpawns;
            public int totalDespawns;
            public int peakActiveObjects;
            public DateTime lastCleanupTime;
        }

        private class PooledObjectInfo
        {
            public string poolId;
            public NetworkObject networkObject;
            public DateTime spawnTime;
            public int ownerClientId; // store owner by client id (or -1 for none)
            public bool isActive;
            public float lifetime;
        }

        #region Unity / Network lifecycle

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (Instance == null)
                Instance = this;
            else
            {
                Debug.LogWarning("Multiple ObjectPooler instances detected. Destroying duplicate.");
                // If this NetworkBehaviour was spawned, despawn it; else destroy gameobject
                if (IsServerInitialized)
                {
                    InstanceFinder.ServerManager.Despawn(NetworkObject);
                }
                Destroy(gameObject);
                return;
            }

            InitializePooler();

            // Only server manages pool creation, cleanup, connection events etc.
            if (IsServerInitialized)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;

                if (prewarmOnStart)
                    PrewarmPools();

                if (enableAutoCleanup)
                    StartAutoCleanup();
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe server events
            if (IsServerInitialized)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }

            if (_cleanupCoroutine != null)
            {
                StopCoroutine(_cleanupCoroutine);
                _cleanupCoroutine = null;
            }

            // Cleanup and despawn active network objects (server only)
            if (IsServerInitialized)
            {
                if (_activeObjects != null)
                {
                    var objectsToDespawn = _activeObjects.Keys.ToList();
                    foreach (NetworkObject obj in objectsToDespawn)
                    {
                        if (obj != null && obj.IsSpawned)
                            InstanceFinder.ServerManager.Despawn(obj);
                    }
                    _activeObjects.Clear();
                }

                if (_poolDictionary != null)
                {
                    foreach (var queue in _poolDictionary.Values)
                    {
                        while (queue.Count > 0)
                        {
                            NetworkObject obj = queue.Dequeue();
                            if (obj != null)
                            {
                                // Destroy clientside objects or non-spawned placeholders
                                Destroy(obj.gameObject);
                            }
                        }
                    }
                    _poolDictionary.Clear();
                }
            }

            if (Instance == this)
                Instance = null;
        }

        #endregion

        #region Initialization

        private void InitializePooler()
        {
            _poolDictionary = new Dictionary<string, Queue<NetworkObject>>();
            _activeObjects = new Dictionary<NetworkObject, PooledObjectInfo>();
            _categorizedPools = new Dictionary<PoolCategory, List<Pool>>();
            _performanceStats = new PerformanceStats();

            foreach (PoolCategory category in Enum.GetValues(typeof(PoolCategory)))
                _categorizedPools[category] = new List<Pool>();

            foreach (Pool pool in pools)
            {
                if (!_categorizedPools.ContainsKey(pool.category))
                    _categorizedPools[pool.category] = new List<Pool>();

                _categorizedPools[pool.category].Add(pool);

                // Ensure dictionary entry exists for pool
                if (!_poolDictionary.ContainsKey(pool.poolId))
                    _poolDictionary[pool.poolId] = new Queue<NetworkObject>();
            }

            if (enablePerformanceStats)
                InitializeStatistics();
        }

        #endregion

        #region Prewarm / Pool creation

        [Server]
        private void PrewarmPools()
        {
            foreach (Pool pool in pools)
            {
                CreatePoolObjects(pool, pool.initialSize);
                UpdatePoolStatistics(pool.poolId, "initial_size", pool.initialSize);
            }
        }

        [Server]
        private void CreatePoolObjects(Pool pool, int count)
        {
            if (!_poolDictionary.ContainsKey(pool.poolId))
                _poolDictionary[pool.poolId] = new Queue<NetworkObject>();

            for (int i = 0; i < count; i++)
            {
                if (pool.currentSize >= pool.maxSize && !pool.expandable)
                {
                    Debug.LogWarning($"Pool {pool.poolId} reached maximum size ({pool.maxSize}). Cannot create more objects.");
                    break;
                }

                if (pool.prefab == null)
                {
                    Debug.LogError($"Pool {pool.poolId} has no prefab assigned.");
                    return;
                }

                NetworkObject obj = Instantiate(pool.prefab, transform);
                obj.gameObject.SetActive(false);

                // Server spawn the object (server-owned)
                InstanceFinder.ServerManager.Spawn(obj);

                _poolDictionary[pool.poolId].Enqueue(obj);
                pool.currentSize++;

                // Initialize poolable if present
                var poolable = obj.GetComponent<IPoolable>();
                poolable?.OnPoolCreate(pool.poolId);
            }

            UpdatePoolStatistics(pool.poolId, "current_size", pool.currentSize);
        }

        #endregion

        #region Spawn / Return

        [Server]
        public NetworkObject SpawnFromPool(string poolId, Vector3 position, Quaternion rotation,
                                          int ownerClientId = -1, Transform parent = null)
        {
            if (!_poolDictionary.ContainsKey(poolId) || _poolDictionary[poolId].Count == 0)
            {
                Pool pool = pools.Find(p => p.poolId == poolId);
                if (pool == null)
                {
                    Debug.LogError($"Pool with ID {poolId} not found.");
                    return null;
                }

                if (pool.expandable && pool.currentSize < pool.maxSize)
                {
                    CreatePoolObjects(pool, 1);
                }
                else
                {
                    Debug.LogWarning($"Pool {poolId} is empty and not expandable.");
                    return null;
                }
            }

            float startTime = Time.realtimeSinceStartup;

            NetworkObject objectToSpawn = _poolDictionary[poolId].Dequeue();
            if (objectToSpawn == null)
            {
                Debug.LogWarning($"Dequeued a null object from pool {poolId}");
                return null;
            }

            // Set up object transform/parent
            objectToSpawn.transform.SetParent(parent ?? transform, worldPositionStays: true);
            objectToSpawn.transform.SetPositionAndRotation(position, rotation);
            objectToSpawn.gameObject.SetActive(true);

            // If owner specified, set ownership (server only)
            if (ownerClientId >= 0)
            {
                NetworkConnection ownerConn = InstanceFinder.ServerManager.Clients.GetValueOrDefault(ownerClientId);

                if (ownerConn != null)
                {
                    objectToSpawn.GiveOwnership(ownerConn);
                }

            }
            /*
            or if you want to clear ownership:

            objectToSpawn.RemoveOwnership();
            */

            // Register active
            var pooledInfo = new PooledObjectInfo
            {
                poolId = poolId,
                networkObject = objectToSpawn,
                spawnTime = DateTime.Now,
                ownerClientId = ownerClientId,
                isActive = true
            };
            _activeObjects[objectToSpawn] = pooledInfo;

            // Notify object
            var poolable = objectToSpawn.GetComponent<IPoolable>();
            poolable?.OnPoolSpawn();

            // Update pool counts & stats
            Pool poolRef = pools.Find(p => p.poolId == poolId);
            if (poolRef != null)
            {
                poolRef.activeCount++;
                UpdatePoolStatistics(poolId, "active_count", poolRef.activeCount);
            }

            if (enablePerformanceStats)
            {
                float spawnTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                _performanceStats.spawnTimes.Add(spawnTime);
                _performanceStats.totalSpawns++;
                if (_performanceStats.spawnTimes.Count > maxPerformanceHistory)
                    _performanceStats.spawnTimes.RemoveAt(0);

                _performanceStats.peakActiveObjects = Mathf.Max(_performanceStats.peakActiveObjects, _activeObjects.Count);
            }

            return objectToSpawn;
        }

        [Server]
        public void ReturnToPool(NetworkObject objectToReturn)
        {
            if (objectToReturn == null) return;

            if (!_activeObjects.TryGetValue(objectToReturn, out PooledObjectInfo info))
            {
                Debug.LogWarning($"Object {objectToReturn.name} is not managed by pooler.");
                return;
            }

            float startTime = Time.realtimeSinceStartup;

            // Notify object
            var poolable = objectToReturn.GetComponent<IPoolable>();
            poolable?.OnPoolDespawn();

            // Reset transform & state
            objectToReturn.gameObject.SetActive(false);
            objectToReturn.transform.SetParent(transform, worldPositionStays: false);
            objectToReturn.transform.localPosition = Vector3.zero;
            objectToReturn.transform.localRotation = Quaternion.identity;

            // Return to pool queue
            if (_poolDictionary.ContainsKey(info.poolId))
            {
                _poolDictionary[info.poolId].Enqueue(objectToReturn);
            }
            else
            {
                // If pool removed, destroy object
                Destroy(objectToReturn.gameObject);
            }

            // Update stats
            Pool pool = pools.Find(p => p.poolId == info.poolId);
            if (pool != null)
            {
                pool.activeCount = Mathf.Max(0, pool.activeCount - 1);
                UpdatePoolStatistics(info.poolId, "active_count", pool.activeCount);
            }

            // Remove from active dict
            _activeObjects.Remove(objectToReturn);

            if (enablePerformanceStats)
            {
                float despawnTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                _performanceStats.despawnTimes.Add(despawnTime);
                _performanceStats.totalDespawns++;
                if (_performanceStats.despawnTimes.Count > maxPerformanceHistory)
                    _performanceStats.despawnTimes.RemoveAt(0);
            }
        }

        [Server]
        public void ReturnAllFromPool(string poolId)
        {
            var objectsToReturn = _activeObjects.Values
                .Where(info => info.poolId == poolId)
                .Select(info => info.networkObject)
                .ToList();

            foreach (NetworkObject obj in objectsToReturn)
                ReturnToPool(obj);
        }

        [Server]
        public void ReturnAllFromCategory(PoolCategory category)
        {
            if (!_categorizedPools.TryGetValue(category, out var categoryPools))
                return;

            foreach (Pool pool in categoryPools)
                ReturnAllFromPool(pool.poolId);
        }

        [Server]
        public void CleanupInactiveObjects(float maxLifetime)
        {
            DateTime cutoffTime = DateTime.Now.AddSeconds(-maxLifetime);
            var objectsToCleanup = _activeObjects.Values
                .Where(info => info.spawnTime < cutoffTime && !info.isActive)
                .Select(info => info.networkObject)
                .ToList();

            foreach (NetworkObject obj in objectsToCleanup)
                ReturnToPool(obj);

            _performanceStats.lastCleanupTime = DateTime.Now;
        }

        #endregion

        #region Auto cleanup

        [Server]
        private void StartAutoCleanup()
        {
            if (_cleanupCoroutine != null)
                StopCoroutine(_cleanupCoroutine);

            _cleanupCoroutine = StartCoroutine(AutoCleanupRoutine());
        }

        private System.Collections.IEnumerator AutoCleanupRoutine()
        {
            while (enableAutoCleanup)
            {
                yield return new WaitForSeconds(cleanupCheckInterval);

                foreach (Pool pool in pools.Where(p => p.autoCleanupTimeout > 0))
                    CleanupInactiveObjects(pool.autoCleanupTimeout);
            }
        }

        #endregion

        #region Connection handling (server)

        // Server event handler signature uses RemoteConnectionStateArgs
        [Server]
        // Handler (signature must match the ServerManager event)
        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                if (conn != null)
                {
                    int clientId = conn.ClientId;
                    ReturnObjectsByOwner(clientId); // pass int, not NetworkConnection
                    Debug.Log($"Client {clientId} disconnected and objects returned to pool.");
                }
            }
        }

        /*
        And ensure when you register the owner on spawn you store ownerClientId:

          // when you create pooledInfo in SpawnFromPool:
         pooledInfo.ownerClientId = (ownerConn != null) ? ownerConn.ClientId : -1;
        */


        [Server]
        private void ReturnObjectsByOwner(int ownerClientId)
        {
            var objectsToReturn = _activeObjects.Values
                .Where(info => info.ownerClientId == ownerClientId)
                .Select(info => info.networkObject)
                .ToList();

            foreach (NetworkObject obj in objectsToReturn)
                ReturnToPool(obj);
        }

        #endregion

        #region Scene handling

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsServerInitialized)
            {
                ReturnAllObjects();
            }
        }

        [Server]
        public void ReturnAllObjects()
        {
            var allActiveObjects = _activeObjects.Keys.ToList();
            foreach (NetworkObject obj in allActiveObjects)
                ReturnToPool(obj);
        }

        #endregion

        #region Pool resizing / stats

        [Server]
        public void ResizePool(string poolId, int newSize)
        {
            Pool pool = pools.Find(p => p.poolId == poolId);
            if (pool == null) return;

            if (newSize > pool.maxSize && !pool.expandable)
            {
                Debug.LogWarning($"Cannot resize pool {poolId} beyond max size {pool.maxSize}");
                return;
            }

            int difference = newSize - pool.currentSize;
            if (difference > 0)
                CreatePoolObjects(pool, difference);
            else if (difference < 0)
            {
                int objectsToRemove = Mathf.Min(-difference, _poolDictionary[poolId].Count);
                for (int i = 0; i < objectsToRemove; i++)
                {
                    NetworkObject obj = _poolDictionary[poolId].Dequeue();
                    if (obj != null && obj.IsSpawned)
                        InstanceFinder.ServerManager.Despawn(obj);
                    else if (obj != null)
                        Destroy(obj.gameObject);

                    pool.currentSize = Mathf.Max(0, pool.currentSize - 1);
                }
            }

            UpdatePoolStatistics(poolId, "current_size", pool.currentSize);
        }

        private void InitializeStatistics()
        {
            _poolStatistics.OnChange += OnPoolStatisticsChanged;

            foreach (Pool pool in pools)
            {
                UpdatePoolStatistics(pool.poolId, "initial_size", pool.initialSize);
                UpdatePoolStatistics(pool.poolId, "current_size", pool.currentSize);
                UpdatePoolStatistics(pool.poolId, "active_count", pool.activeCount);
                UpdatePoolStatistics(pool.poolId, "max_size", pool.maxSize);
            }
        }

        private void UpdatePoolStatistics(string poolId, string statKey, int value)
        {
            string key = $"{poolId}_{statKey}";
            _poolStatistics[key] = value;
        }

        private void OnPoolStatisticsChanged(SyncDictionaryOperation op, string key, int value, bool asServer)
        {
            // client-side reactions to pool stats changes if you want
            if (!asServer && enablePerformanceStats)
            {
                // e.g., update a UI dashboard on clients
            }
        }

        #endregion

        #region Public API

        public PerformanceData GetPerformanceData()
        {
            return new PerformanceData
            {
                totalPools = pools.Count,
                totalObjects = pools.Sum(p => p.currentSize),
                activeObjects = _activeObjects.Count,
                totalSpawns = _performanceStats.totalSpawns,
                totalDespawns = _performanceStats.totalDespawns,
                averageSpawnTime = _performanceStats.spawnTimes.Count > 0 ? _performanceStats.spawnTimes.Average() : 0f,
                averageDespawnTime = _performanceStats.despawnTimes.Count > 0 ? _performanceStats.despawnTimes.Average() : 0f,
                peakActiveObjects = _performanceStats.peakActiveObjects,
                lastCleanupTime = _performanceStats.lastCleanupTime
            };
        }

        public PoolStats GetPoolStats(string poolId)
        {
            Pool pool = pools.Find(p => p.poolId == poolId);
            if (pool == null) return default;

            return new PoolStats
            {
                poolId = poolId,
                currentSize = pool.currentSize,
                activeCount = pool.activeCount,
                availableCount = _poolDictionary.ContainsKey(poolId) ? _poolDictionary[poolId].Count : 0,
                maxSize = pool.maxSize,
                expandable = pool.expandable
            };
        }

        public List<PoolStats> GetAllPoolStats()
        {
            return pools.Select(pool => GetPoolStats(pool.poolId)).ToList();
        }

        #endregion

        #region Data Structures

        public struct PerformanceData
        {
            public int totalPools;
            public int totalObjects;
            public int activeObjects;
            public int totalSpawns;
            public int totalDespawns;
            public float averageSpawnTime;
            public float averageDespawnTime;
            public int peakActiveObjects;
            public DateTime lastCleanupTime;
        }

        public struct PoolStats
        {
            public string poolId;
            public int currentSize;
            public int activeCount;
            public int availableCount;
            public int maxSize;
            public bool expandable;
        }

        #endregion
    }

    public interface IPoolable
    {
        void OnPoolCreate(string poolId);
        void OnPoolSpawn();
        void OnPoolDespawn();
    }

    // Base class for pooled networked objects
    public abstract class PoolableNetworkBehaviour : NetworkBehaviour, IPoolable
    {
        // Use SyncVar<T> if you want to replicate pool id to clients
        public readonly SyncVar<string> PoolId = new SyncVar<string>(string.Empty);

        public virtual void OnPoolCreate(string poolId)
        {
            PoolId.Value = poolId;
        }

        public virtual void OnPoolSpawn()
        {
            gameObject.SetActive(true);
        }

        public virtual void OnPoolDespawn()
        {
            gameObject.SetActive(false);
        }

        [Server]
        protected void ReturnToPool()
        {
            if (!string.IsNullOrEmpty(PoolId.Value) && ObjectPooler.Instance != null)
            {
                ObjectPooler.Instance.ReturnToPool(NetworkObject);
            }
            else
            {
                if (NetworkObject != null && NetworkObject.IsSpawned)
                    InstanceFinder.ServerManager.Despawn(NetworkObject);
                else if (gameObject != null)
                    Destroy(gameObject);
            }
        }
    }
}
