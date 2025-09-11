using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Scened;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Transporting.Multipass; // Multipass specific
using FishNet.Connection;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Transporting.Tugboat;
using System;
using System.Linq;

using UnityEngine.SceneManagement;
using FNSceneManager = FishNet.Managing.Scened.SceneManager; // alias for FishNet SceneManager
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;


public class GameNetworkManager : MonoBehaviour
{
    [Header("Network Configuration")]
    public string gameVersion = "1.0";
    public int maxPlayers = 4;
    public ushort serverPort = 7777;
    public string serverPassword = "";

    [Header("Prefab References")]
    public GameObject playerPrefab;
    public NetworkManager networkManagerPrefab;

    // Public access to all managers
    public NetworkManager NetworkManager { get; private set; }
    public ServerManager ServerManager { get; private set; }
    public ClientManager ClientManager { get; private set; }
    //public SceneManager SceneManager { get; private set; }
    public FNSceneManager SceneManager { get; private set; }
    public TimeManager TimeManager { get; private set; }
    public TransportManager TransportManager { get; private set; }
    public Multipass MultipassTransport { get; private set; } // Multipass specific

    private Dictionary<int, NetworkConnection> _connectedClients = new Dictionary<int, NetworkConnection>();
    private List<string> _loadedScenes = new List<string>();

    void Awake()
    {
        InitializeNetworkManager();
        ConfigureNetworkSettings();
        SetupEventHandlers();
    }

    void InitializeNetworkManager()
    {
        // Get existing NetworkManager or create one
        NetworkManager = InstanceFinder.NetworkManager;
        if (NetworkManager == null)
        {
            if (networkManagerPrefab != null)
            {
                NetworkManager = Instantiate(networkManagerPrefab);
            }
            else
            {
                GameObject nmGo = new GameObject("NetworkManager");
                NetworkManager = nmGo.AddComponent<NetworkManager>();
            }
        }

        // Get all sub-managers
        ServerManager = NetworkManager.ServerManager;
        ClientManager = NetworkManager.ClientManager;
        SceneManager = NetworkManager.SceneManager;
        TimeManager = NetworkManager.TimeManager;
        TransportManager = NetworkManager.TransportManager;

        // Get Multipass transport specifically
        MultipassTransport = TransportManager.Transport as Multipass;

        DontDestroyOnLoad(NetworkManager.gameObject);
    }

    void ConfigureNetworkSettings()
    {
        // Headless mode handling
        if (Application.isBatchMode)
        {
            Debug.Log("[CONFIG] Headless detected, starting server...");
            ServerManager.StartConnection(); // start server manually
        }

        // Multipass Transport configuration
        if (MultipassTransport != null)
        {
            foreach (var t in MultipassTransport.Transports)
            {
                // Try reflection for Port and MaxConnections
                var pPort = t.GetType().GetProperty("Port");
                var pMax = t.GetType().GetProperty("MaxConnections")
                           ?? t.GetType().GetProperty("MaximumClients");

                if (pPort != null && pPort.CanWrite)
                    pPort.SetValue(t, Convert.ChangeType(serverPort, pPort.PropertyType));

                if (pMax != null && pMax.CanWrite)
                    pMax.SetValue(t, Convert.ChangeType(maxPlayers, pMax.PropertyType));

                Debug.Log($"[MULTIPASS] Configured {t.GetType().Name} via reflection (Port={serverPort}, MaxConnections={maxPlayers})");
            }
        }

        // Time settings (must use SetTickRate, not property)
        NetworkManager.TimeManager.SetTickRate(60);

        // Scene management – no config needed in 4.x
        Debug.Log($"[CONFIG] MaxPlayers={maxPlayers}, Port={serverPort}, Version={gameVersion}");
    }



    void SetupEventHandlers()
    {
        // Server events
        ServerManager.OnServerConnectionState += OnServerConnectionState;
        ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        ServerManager.OnAuthenticationResult += OnAuthenticationResult;

        // Client events
        ClientManager.OnClientConnectionState += OnClientConnectionState;
        ClientManager.OnConnectedClients += OnConnectedClients;

        // Scene events
        SceneManager.OnLoadStart += OnSceneLoadStart;
        SceneManager.OnLoadEnd += OnSceneLoadEnd;
        SceneManager.OnUnloadStart += OnSceneUnloadStart;
        SceneManager.OnUnloadEnd += OnSceneUnloadEnd;


    }

    #region Connection Methods

    public void StartHost()
    {
        if (NetworkManager == null) return;

        Debug.Log("Starting as Host...");
        ServerManager.StartConnection();
        ClientManager.StartConnection();
    }

    public void StartServer()
    {
        if (NetworkManager == null) return;

        Debug.Log("Starting Dedicated Server...");
        ServerManager.StartConnection();
    }

    public void StartClient(string ipAddress = "127.0.0.1")
    {
        if (NetworkManager == null) return;

        // If Multipass + inner transport supports setting address, set it here for index 0
        if (MultipassTransport != null)
        {
            var t = MultipassTransport.GetTransport(0);
            if (t != null)
            {
                var propAddress = t.GetType().GetProperty("Address") ?? t.GetType().GetProperty("Host");
                if (propAddress != null && propAddress.CanWrite)
                    propAddress.SetValue(t, ipAddress);
            }
        }

        Debug.Log($"Connecting to {ipAddress}:{serverPort}...");
        ClientManager.StartConnection();
    }
    void InitializeManagers()
    {
        NetworkManager = InstanceFinder.NetworkManager;
        SceneManager = NetworkManager.SceneManager;
    }

    public void StopConnection()
    {
        if (NetworkManager == null) return;

        Debug.Log("Stopping all connections...");
        ServerManager.StopConnection(true);
        ClientManager.StopConnection();
    }

    public void DisconnectClient(int clientId)
    {
        if (NetworkManager == null) return;

        if (ServerManager.Clients.TryGetValue(clientId, out NetworkConnection conn))
        {
            // Try the usual StopConnection(NetworkConnection)
            var srvType = ServerManager.GetType();
            var stopMethod = srvType.GetMethod("StopConnection", new System.Type[] { typeof(NetworkConnection) });
            if (stopMethod != null)
            {
                stopMethod.Invoke(ServerManager, new object[] { conn });
                Debug.Log($"Disconnected client {clientId} via StopConnection");
                return;
            }

            // Fallback: try StopConnection(bool) for server (disconnect all) - not ideal
            Debug.LogWarning("Specific StopConnection(NetworkConnection) not found on ServerManager - client may remain connected.");
        }
        else
        {
            Debug.LogWarning($"Client {clientId} not found in ServerManager.Clients.");
        }
    }

    #endregion

    #region Event Handlers

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[SERVER] Connection State: {args.ConnectionState}");

        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                Debug.Log("Server started successfully!");
                break;
            case LocalConnectionState.Stopped:
                Debug.Log("Server stopped.");
                break;
            default: // Handle other states if needed
                Debug.Log($"Server state: {args.ConnectionState}");
                break;
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[CLIENT] Connection State: {args.ConnectionState}");

        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                Debug.Log("Connected to server successfully!");
                break;
            case LocalConnectionState.Stopped:
                Debug.Log("Disconnected from server.");
                break;
            default: // Handle other states if needed
                Debug.Log($"Client state: {args.ConnectionState}");
                break;
        }
    }


    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        Debug.Log($"[REMOTE] Client {conn.ClientId} Connection State: {args.ConnectionState}");

        switch (args.ConnectionState)
        {
            case RemoteConnectionState.Started:
                Debug.Log($"Client {conn.ClientId} connected");
                OnClientConnected(conn);
                break;
            case RemoteConnectionState.Stopped:
                Debug.Log($"Client {conn.ClientId} disconnected");
                OnClientDisconnected(conn);
                break;
        }
    }

    private void OnAuthenticationResult(NetworkConnection conn, bool authenticated)
    {
        if (authenticated)
        {
            Debug.Log($"Client {conn.ClientId} authenticated successfully");
        }
        else
        {
            Debug.LogWarning($"Client {conn.ClientId} failed authentication");
        }
    }

    private void OnConnectedClients(ConnectedClientsArgs args)
    {
        Debug.Log($"Connected clients updated. Count: {args.ClientIds.Count}");
    }
    private void OnEnable()
    {
        InstanceFinder.SceneManager.OnLoadStart += OnSceneLoadStart;
        InstanceFinder.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        InstanceFinder.SceneManager.OnUnloadStart += OnSceneUnloadStart;
    }

    private void OnDisable()
    {
        if (InstanceFinder.SceneManager != null)
        {
            InstanceFinder.SceneManager.OnLoadStart -= OnSceneLoadStart;
            InstanceFinder.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
            InstanceFinder.SceneManager.OnUnloadStart -= OnSceneUnloadStart;
        }
    }

    private void OnSceneLoadStart(SceneLoadStartEventArgs args)
    {
        if (args.QueueData != null && args.QueueData.SceneLoadData != null)
        {
            foreach (var lookup in args.QueueData.SceneLoadData.SceneLookupDatas)
            {
                Debug.Log($"[SceneLoadStart] Preparing to load: {lookup.Name}");
                _loadedScenes.Add(lookup.Name);
            }
        }
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        if (args.LoadedScenes != null)
        {
            foreach (Scene scene in args.LoadedScenes)
            {
                if (scene.IsValid())
                    Debug.Log($"[SceneLoadEnd] Scene loaded: {scene.name}");
            }
        }
    }

    private void OnSceneUnloadStart(SceneUnloadStartEventArgs args)
    {
        if (args.QueueData != null && args.QueueData.SceneUnloadData != null)
        {
            foreach (var lookup in args.QueueData.SceneUnloadData.SceneLookupDatas)
            {
                Debug.Log($"[SceneUnloadStart] Unloading: {lookup.Name}");
                _loadedScenes.Remove(lookup.Name);
            }
        }
    }

    private void OnSceneUnloadEnd(SceneUnloadEndEventArgs args)
    {
        if (args.UnloadedScenesV2 != null && args.UnloadedScenesV2.Count > 0)
        {
            foreach (Scene scene in args.UnloadedScenes)
            {
                Debug.Log($"[SceneUnloadEnd] Scene unloaded: {scene.name}");
                if (_loadedScenes.Contains(scene.name))
                    _loadedScenes.Remove(scene.name);
            }
        }
    }


    #endregion

    #region Multipass Specific Methods

    private int _activeTransportIndex = 0;

    public void SwitchTransport(int transportIndex)
    {
        if (MultipassTransport != null)
        {
            MultipassTransport.SetClientTransport(transportIndex);
            _activeTransportIndex = transportIndex; // ✅ keep track manually
            Debug.Log($"Switched to transport index: {transportIndex}");
        }
    }



    public string GetActiveTransportName()
    {
        if (MultipassTransport != null)
        {
            Transport activeTransport = MultipassTransport.GetTransport(_activeTransportIndex);
            return activeTransport?.GetType().Name ?? "Unknown";
        }
        return "No Multipass";
    }



    public List<string> GetAvailableTransports()
    {
        List<string> transports = new List<string>();
        if (MultipassTransport != null)
        {
            int count = 1;
            var mType = MultipassTransport.GetType();
            var propCount = mType.GetProperty("TransportCount");
            if (propCount != null) count = (int)propCount.GetValue(MultipassTransport);
            else
            {
                var arrProp = mType.GetProperty("Transports");
                if (arrProp != null)
                {
                    var arr = arrProp.GetValue(MultipassTransport) as System.Collections.ICollection;
                    count = arr != null ? arr.Count : 1;
                }
            }

            for (int i = 0; i < count; i++)
            {
                Transport t = null;
                try { t = MultipassTransport.GetTransport(i); }
                catch { t = null; }
                if (t != null) transports.Add($"{i}: {t.GetType().Name}");
            }
        }
        return transports;
    }


    #endregion

    #region Client Management

    private void OnClientConnected(NetworkConnection conn)
    {
        if (!_connectedClients.ContainsKey(conn.ClientId))
        {
            _connectedClients.Add(conn.ClientId, conn);
            Debug.Log($"Client connected: {conn.ClientId}. Total players: {_connectedClients.Count}");
        }
    }

    private void OnClientDisconnected(NetworkConnection conn)
    {
        if (_connectedClients.ContainsKey(conn.ClientId))
        {
            _connectedClients.Remove(conn.ClientId);
            Debug.Log($"Client disconnected: {conn.ClientId}. Total players: {_connectedClients.Count}");
        }
    }

    #endregion

    #region Scene Management Methods

    public void LoadScene(string sceneName)
    {
        if (NetworkManager == null || !NetworkManager.IsServerStarted) return;

        SceneLoadData sceneLoadData = new SceneLoadData(sceneName);
        SceneManager.LoadGlobalScenes(sceneLoadData);
        Debug.Log($"Loading scene: {sceneName}");
    }

    public void UnloadScene(string sceneName)
    {
        if (NetworkManager == null || !NetworkManager.IsServerStarted) return;

        SceneUnloadData sceneUnloadData = new SceneUnloadData(sceneName);
        SceneManager.UnloadGlobalScenes(sceneUnloadData);
        Debug.Log($"Unloading scene: {sceneName}");
    }

    public List<string> GetLoadedScenes()
    {
        return new List<string>(_loadedScenes);
    }

    public bool IsSceneLoaded(string sceneName)
    {
        return _loadedScenes.Contains(sceneName);
    }

    #endregion

    #region Utility Methods

    public bool IsServer()
    {
        return NetworkManager != null && NetworkManager.IsServerStarted;
    }

    public bool IsClient()
    {
        return NetworkManager != null && NetworkManager.IsClientStarted;
    }

    public bool IsHost()
    {
        return IsServer() && IsClient();
    }

    public int GetPlayerCount()
    {
       return ServerManager?.Clients.Count ?? 0;
    }
   

    public string GetConnectionStatus()
    {
        if (NetworkManager == null) return "No NetworkManager";

        if (IsHost()) return "Hosting";
        if (IsServer()) return "Server";
        if (IsClient()) return "Connected";
        return "Disconnected";
    }

    public void ChangeMaxPlayers(int newMax)
    {
        if (MultipassTransport != null)
        {
            int count = 1;
            var mType = MultipassTransport.GetType();
            var propCount = mType.GetProperty("TransportCount");
            if (propCount != null) count = (int)propCount.GetValue(MultipassTransport);
            else
            {
                var arrProp = mType.GetProperty("Transports");
                if (arrProp != null)
                {
                    var arr = arrProp.GetValue(MultipassTransport) as System.Collections.ICollection;
                    count = arr != null ? arr.Count : 1;
                }
            }

            for (int i = 0; i < count; i++)
            {
                Transport t = null;
                try { t = MultipassTransport.GetTransport(i); } catch { t = null; }
                if (t == null) continue;

                if (t is FishNet.Transporting.Tugboat.Tugboat tugboat)
                {
                    tugboat.SetMaximumClients(newMax); // ✅ use method instead of property
                    Debug.Log($"Max players changed to: {newMax} (Tugboat)");
                    return;
                }

                var pMax = t.GetType().GetProperty("MaximumClients") ?? t.GetType().GetProperty("MaxClients");
                if (pMax != null && pMax.CanWrite)
                {
                    pMax.SetValue(t, Convert.ChangeType(newMax, pMax.PropertyType));
                    Debug.Log($"Max players changed to: {newMax} (via reflection on {t.GetType().Name})");
                    return;
                }
            }
        }
        Debug.LogWarning("No inner transport found to change maximum clients.");
    }


    public void ChangeServerPort(ushort newPort)
    {
        if (MultipassTransport != null)
        {
            int count = 1;
            var mType = MultipassTransport.GetType();
            var propCount = mType.GetProperty("TransportCount");
            if (propCount != null) count = (int)propCount.GetValue(MultipassTransport);
            else
            {
                var arrProp = mType.GetProperty("Transports");
                if (arrProp != null)
                {
                    var arr = arrProp.GetValue(MultipassTransport) as System.Collections.ICollection;
                    count = arr != null ? arr.Count : 1;
                }
            }

            for (int i = 0; i < count; i++)
            {
                Transport t = null;
                try { t = MultipassTransport.GetTransport(i); } catch { t = null; }
                if (t == null) continue;

                if (t is FishNet.Transporting.Tugboat.Tugboat tugboat)
                {
                    tugboat.SetPort(newPort); // ✅ Use SetPort instead of tugboat.Port
                    Debug.Log($"Server port changed to: {newPort} (Tugboat)");
                    return;
                }

                var pPort = t.GetType().GetProperty("Port");
                if (pPort != null && pPort.CanWrite)
                {
                    pPort.SetValue(t, Convert.ChangeType(newPort, pPort.PropertyType));
                    Debug.Log($"Server port changed to: {newPort} (via reflection on {t.GetType().Name})");
                    return;
                }
            }
        }
        Debug.LogWarning("No inner transport found to change port.");
    }



    public NetworkConnection GetPlayerConnection(int clientId)
    {
        if (_connectedClients.TryGetValue(clientId, out NetworkConnection conn))
        {
            return conn;
        }
        return null;
    }

    public void BroadcastToAll(string message)
    {
        foreach (var conn in _connectedClients.Values)
        {
            if (conn.IsActive)
            {
                // Implement your message sending logic here
                Debug.Log($"Broadcasting to client {conn.ClientId}: {message}");
            }
        }
    }

    #endregion

    #region Quick Debug Commands

    [ContextMenu("Quick Start Host")]
    private void QuickStartHost()
    {
        StartHost();
    }

    [ContextMenu("Quick Start Client")]
    private void QuickStartClient()
    {
        StartClient("127.0.0.1");
    }

    [ContextMenu("Quick Stop All")]
    private void QuickStopAll()
    {
        StopConnection();
    }

    [ContextMenu("Print Network Info")]
    private void PrintNetworkInfo()
    {
        Debug.Log($"Network Status: {GetConnectionStatus()}");
        Debug.Log($"Player Count: {GetPlayerCount()}");
        Debug.Log($"Is Server: {IsServer()}");
        Debug.Log($"Is Client: {IsClient()}");
        Debug.Log($"Active Transport: {GetActiveTransportName()}");
        Debug.Log($"Loaded Scenes: {string.Join(", ", _loadedScenes)}");

        // List available transports
        List<string> transports = GetAvailableTransports();
        Debug.Log($"Available Transports: {string.Join(", ", transports)}");
    }

    #endregion

    void OnApplicationQuit()
    {
        StopConnection();
    }

    void OnValidate()
    {
        // Validate settings in editor
        maxPlayers = Mathf.Clamp(maxPlayers, 1, 64);
        serverPort = (ushort)Mathf.Clamp(serverPort, 1024, 65535);
    }

    void OnDestroy()
    {
        if (ServerManager != null)
        {
            ServerManager.OnServerConnectionState -= OnServerConnectionState;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
        }

        if (ClientManager != null)
            ClientManager.OnClientConnectionState -= OnClientConnectionState;

        if (ClientManager != null)
            ClientManager.OnConnectedClients -= OnConnectedClients;

        if (SceneManager != null)
        {
            SceneManager.OnLoadStart -= OnSceneLoadStart;
            SceneManager.OnLoadEnd -= OnSceneLoadEnd;
            SceneManager.OnUnloadStart -= OnSceneUnloadStart;
            SceneManager.OnUnloadEnd -= OnSceneUnloadEnd;
        }
    }
    public int GetMaxPlayers()
    {
        return maxPlayers;
    } 

}