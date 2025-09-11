using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Managing;

namespace zoya.game
{



    public class AdvancedGravityWellSpawner : NetworkBehaviour
    {
        public static AdvancedGravityWellSpawner Instance { get; private set; }

        [System.Serializable]
        public class SpawnSettings
        {
            [Header("Spawning Limits")]
            public int maxWellsPerPlayer = 2;
            public int maxTotalWells = 8;
            public float minSpawnDistance = 2f;
            public float maxSpawnDistance = 10f;
            public LayerMask spawnLayerMask = -1;

            [Header("Cooldown Settings")]
            public float globalCooldown = 0.5f;
            public float wellLifetime = 5f;
            public float wellFadeoutTime = 1f;

            [Header("Performance")]
            public bool useObjectPooling = true;
            public int initialPoolSize = 10;
            public bool recycleOldestWells = true;
        }

        [System.Serializable]
        public class WellType
        {
            public string typeName;
            public GameObject wellPrefab;
            public float strengthMultiplier = 1f;
            public float radiusMultiplier = 1f;
            public Color wellColor = Color.blue;
        }

        public SpawnSettings settings = new SpawnSettings();
        public List<WellType> wellTypes = new List<WellType>();
        public WellType defaultWellType;

        [Header("References")]
        public Transform wellContainer;
        public AudioSource spawnAudioSource;
        public AudioClip spawnSound;
        public AudioClip denySound;

        private Dictionary<NetworkConnection, List<AdvancedGravityWell>> _playerWells = new Dictionary<NetworkConnection, List<AdvancedGravityWell>>();
        private Queue<AdvancedGravityWell> _wellPool = new Queue<AdvancedGravityWell>();
        private List<AdvancedGravityWell> _activeWells = new List<AdvancedGravityWell>();
        private float _globalCooldownTimer = 0f;

        public event System.Action<AdvancedGravityWell> OnWellSpawned;
        public event System.Action<AdvancedGravityWell> OnWellDespawned;
        public event System.Action<NetworkConnection> OnWellLimitReached;

        #region Initialization
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (Instance == null)
                Instance = this;

            if (IsServerInitialized)
            {
                InitializeObjectPool();
                StartCoroutine(WellCleanupCoroutine());
            }
        }

        [Server]
        private void InitializeObjectPool()
        {
            if (!settings.useObjectPooling || defaultWellType.wellPrefab == null) return;

            for (int i = 0; i < settings.initialPoolSize; i++)
            {
                CreatePooledWell();
            }
        }

        [Server]
        private void CreatePooledWell()
        {
            GameObject wellGO = Instantiate(defaultWellType.wellPrefab, wellContainer);
            AdvancedGravityWell well = wellGO.GetComponent<AdvancedGravityWell>();
            well.gameObject.SetActive(false);
            _wellPool.Enqueue(well);
        }
        #endregion

        #region Well Spawning
        [Server]
        public bool TrySpawnGravityWell(NetworkConnection spawnerConn, Vector3 position, Quaternion rotation, int teamId, string wellTypeName = "")
        {
            if (!CanSpawnWell(spawnerConn))
            {
                DenySpawnRpc(spawnerConn);
                return false;
            }

            WellType wellType = GetWellType(wellTypeName);
            if (wellType == null) wellType = defaultWellType;

            AdvancedGravityWell well = GetWellFromPool(wellType);
            if (well == null) return false;

            SetupWell(well, spawnerConn, position, rotation, teamId, wellType);
            FinalizeSpawn(well, spawnerConn);

            return true;
        }

        [Server]
        private bool CanSpawnWell(NetworkConnection spawnerConn)
        {
            if (_globalCooldownTimer > 0) return false;

            if (!_playerWells.ContainsKey(spawnerConn))
            {
                _playerWells[spawnerConn] = new List<AdvancedGravityWell>();
            }

            return _playerWells[spawnerConn].Count < settings.maxWellsPerPlayer;
        }

        [Server]
        private AdvancedGravityWell GetWellFromPool(WellType wellType)
        {
            if (settings.useObjectPooling && _wellPool.Count > 0)
            {
                AdvancedGravityWell well = _wellPool.Dequeue();
                well.gameObject.SetActive(true);
                return well;
            }

            // Create new well if pool is empty or not using pooling
            GameObject wellGO = Instantiate(wellType.wellPrefab, wellContainer);
            return wellGO.GetComponent<AdvancedGravityWell>();
        }

        [Server]
        private void SetupWell(AdvancedGravityWell well, NetworkConnection spawnerConn, Vector3 position, Quaternion rotation, int teamId, WellType wellType)
        {
            well.transform.position = position;
            well.transform.rotation = rotation;

            well.InitializeWell(spawnerConn.ClientId, spawnerConn);
            well.SetTeam(teamId);
            well.SetWellType(wellType.typeName, wellType.strengthMultiplier, wellType.radiusMultiplier, wellType.wellColor);

            base.Spawn(well.gameObject, spawnerConn);
        }

        [Server]
        private void FinalizeSpawn(AdvancedGravityWell well, NetworkConnection spawnerConn)
        {
            _activeWells.Add(well);
            _playerWells[spawnerConn].Add(well);
            _globalCooldownTimer = settings.globalCooldown;

            OnWellSpawned?.Invoke(well);
            WellSpawnedRpc(well.ObjectId, spawnerConn.ClientId);

            StartCoroutine(WellLifetimeCoroutine(well, spawnerConn));
        }
        #endregion

        #region Well Lifetime Management
        [Server]
        private IEnumerator WellLifetimeCoroutine(AdvancedGravityWell well, NetworkConnection ownerConn)
        {
            yield return new WaitForSeconds(settings.wellLifetime);

            // Start fadeout
            well.StartFadeOut(settings.wellFadeoutTime);
            yield return new WaitForSeconds(settings.wellFadeoutTime);

            // Despawn well
            DespawnWell(well, ownerConn);
        }

        [Server]
        private IEnumerator WellCleanupCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);

                if (settings.recycleOldestWells && _activeWells.Count > settings.maxTotalWells)
                {
                    AdvancedGravityWell oldestWell = _activeWells[0];
                    NetworkConnection ownerConn = FindWellOwner(oldestWell);
                    DespawnWell(oldestWell, ownerConn);
                }
            }
        }

        [Server]
        public void DespawnWell(AdvancedGravityWell well, NetworkConnection ownerConn)
        {
            if (well == null) return;

            _activeWells.Remove(well);

            if (ownerConn != null && _playerWells.ContainsKey(ownerConn))
            {
                _playerWells[ownerConn].Remove(well);
            }

            if (settings.useObjectPooling)
            {
                well.gameObject.SetActive(false);
                _wellPool.Enqueue(well);
            }
            else
            {
                base.Despawn(well.gameObject);
            }

            OnWellDespawned?.Invoke(well);
            WellDespawnedRpc(well.ObjectId);
        }

        [Server]
        public void DespawnAllPlayerWells(NetworkConnection playerConn)
        {
            if (_playerWells.ContainsKey(playerConn))
            {
                foreach (AdvancedGravityWell well in _playerWells[playerConn].ToList())
                {
                    DespawnWell(well, playerConn);
                }
            }
        }

        [Server]
        public void DespawnAllWells()
        {
            foreach (var well in _activeWells.ToList())
            {
                NetworkConnection ownerConn = FindWellOwner(well);
                DespawnWell(well, ownerConn);
            }
        }
        #endregion

        #region Utility Methods
        [Server]
        private NetworkConnection FindWellOwner(AdvancedGravityWell well)
        {
            foreach (var kvp in _playerWells)
            {
                if (kvp.Value.Contains(well))
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        [Server]
        private WellType GetWellType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return defaultWellType;
            return wellTypes.Find(t => t.typeName == typeName) ?? defaultWellType;
        }

        [Server]
        public Vector3 GetValidSpawnPosition(Vector3 desiredPosition, Vector3 spawnerPosition)
        {
            Vector3 direction = (desiredPosition - spawnerPosition).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(desiredPosition, spawnerPosition),
                settings.minSpawnDistance, settings.maxSpawnDistance);

            Vector3 spawnPos = spawnerPosition + direction * distance;

            // Check if position is valid
            if (Physics.CheckSphere(spawnPos, 1f, settings.spawnLayerMask))
            {
                // Find nearest valid position
                if (Physics.SphereCast(spawnerPosition, 0.5f, direction, out RaycastHit hit, distance, settings.spawnLayerMask))
                {
                    spawnPos = hit.point - direction * 0.5f;
                }
            }

            return spawnPos;
        }
        #endregion

        NetworkManager networkManager;

        // Track wells manually
        private Dictionary<int, AdvancedGravityWell> spawnedWells = new Dictionary<int, AdvancedGravityWell>();

        #region RPC Methods
        [ObserversRpc]
        private void WellSpawnedRpc(int wellObjectId, int ownerClientId)
        {
            networkManager = InstanceFinder.NetworkManager;

            // Get a NetworkObject by its ID
            if (spawnedWells.TryGetValue(wellObjectId, out AdvancedGravityWell well))
            {
                OnWellSpawned?.Invoke(well);
                PlaySpawnSound();
            }
        }

        [ObserversRpc]
        private void WellDespawnedRpc(int wellObjectId)
        {
            if (spawnedWells.TryGetValue(wellObjectId, out AdvancedGravityWell well))
            {
                OnWellDespawned?.Invoke(well);
                spawnedWells.Remove(wellObjectId);
            }
        }

        [TargetRpc]
        private void DenySpawnRpc(NetworkConnection conn)
        {
            PlayDenySound();
        }

        private void PlaySpawnSound()
        {
            if (spawnAudioSource != null && spawnSound != null)
            {
                spawnAudioSource.PlayOneShot(spawnSound);
            }
        }

        private void PlayDenySound()
        {
            if (spawnAudioSource != null && denySound != null)
            {
                spawnAudioSource.PlayOneShot(denySound);
            }
        }
        #endregion

        #region Public API
        public int GetPlayerWellCount(NetworkConnection playerConn)
        {
            return _playerWells.ContainsKey(playerConn) ? _playerWells[playerConn].Count : 0;
        }

        public int GetTotalWellCount() => _activeWells.Count;
        public List<AdvancedGravityWell> GetActiveWells() => _activeWells;
        public bool IsGlobalCooldownActive() => _globalCooldownTimer > 0;

        [Server]
        public void RegisterWellType(WellType newWellType)
        {
            if (!wellTypes.Exists(t => t.typeName == newWellType.typeName))
            {
                wellTypes.Add(newWellType);
            }
        }

        [Server]
        public void SetWellLimits(int maxPerPlayer, int maxTotal)
        {
            settings.maxWellsPerPlayer = maxPerPlayer;
            settings.maxTotalWells = maxTotal;
        }
        #endregion

        #region Update Loop
        private void Update()
        {
            if (IsServerInitialized && _globalCooldownTimer > 0)
            {
                _globalCooldownTimer -= Time.deltaTime;
            }
        }
        #endregion

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}