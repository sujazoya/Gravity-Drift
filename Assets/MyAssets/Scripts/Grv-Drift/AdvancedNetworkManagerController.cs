using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Scened;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using FishNet.Transporting.Yak;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Serialization;
using FishNet.Object.Synchronizing;
using FishNet.Object; // Required for ServerRpc, ObserversRpc, TargetRpc


namespace zoya.game
{
    



#if UNITY_EDITOR || DEVELOPMENT_BUILD
//#define DEVELOPMENT
#endif

[RequireComponent(typeof(NetworkManager))]
public class AdvancedNetworkManagerController : NetworkBehaviour
{
    public static AdvancedNetworkManagerController Instance { get; private set; }

    [Header("Network Settings")]
    [SerializeField] private string gameVersion = "1.0.0";
    [SerializeField] private int maxPlayers = 16;
    [SerializeField] private int port = 7770;
    [SerializeField] private string defaultServerIP = "127.0.0.1";
    [SerializeField] private TransportType defaultTransport = TransportType.Tugboat;
    [SerializeField] private bool enableUPnP = true;
    [SerializeField] private int targetFrameRate = 144;
    [SerializeField] private int serverTickRate = 60;

    [Header("UI References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_InputField serverIPInput;
    [SerializeField] private TMP_InputField serverPortInput;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text pingText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Slider loadingProgressBar;

    [Header("Scene Management")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string lobbyScene = "Lobby";
    [SerializeField] private string[] gameScenes = { "GameScene" };
    [SerializeField] private float minLoadTime = 2f;

    [Header("Transport Settings")]
    [SerializeField] private Tugboat tugboatTransport;
    [SerializeField] private Yak yakTransport;
    [SerializeField] private Multipass multipassTransport;

    [Header("Quality of Service")]
    [SerializeField] private int clientUpdateRate = 30;
    [SerializeField] private int serverUpdateRate = 60;
    [SerializeField] private float interpolationTime = 0.1f;
    [SerializeField] private bool enablePrediction = true;
    [SerializeField] private bool enableReconciliation = true;

    [Header("Security")]
    [SerializeField] private bool enableEncryption = true;
    [SerializeField] private string encryptionKey = "YourEncryptionKeyHere";
    [SerializeField] private bool validateScenes = true;
    [SerializeField] private bool preventDuplicateLogins = true;

    [Header("Logging & Debug")]
    [SerializeField] private bool enableDetailedLogging = true;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private GameObject networkStatsPanel;
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private TMP_Text rttText;
    [SerializeField] private TMP_Text packetLossText;

    // Private variables
    public NetworkManager _networkManager;
    private Transport _currentTransport;
    private Dictionary<NetworkConnection, PlayerLobbyData> _lobbyPlayers = new Dictionary<NetworkConnection, PlayerLobbyData>();
    private Coroutine _pingUpdateCoroutine;
    private Coroutine _networkStatsCoroutine;
    private bool _isReady = false;    
  
    private readonly SyncVar<string> _playerName = new SyncVar<string>();
    

    public enum TransportType
    {
        Tugboat,
        Yak,
        Multipass
    }

    [System.Serializable]
    public class PlayerLobbyData
    {
        public NetworkConnection Connection;
        public string PlayerName;
        public int TeamId;
        public bool IsReady;
        public bool IsHost;
        public int Ping;
    }

    public new bool IsServer => _networkManager.IsServerStarted;
    public new bool IsClient => _networkManager.IsClientStarted;
    public new bool IsHost => _networkManager.IsServerStarted && _networkManager.IsClientStarted;
    public int PlayerCount => _lobbyPlayers.Count;
    public string GameVersion => gameVersion;
    

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        _networkManager = GetComponent<NetworkManager>();
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;

        SetupTransports();
        ConfigureNetworkManager();
        SetupEventHandlers();
        SetupUI();

        LoadPlayerPreferences();
    }

    private void SetupTransports()
    {
        switch (defaultTransport)
        {
            case TransportType.Tugboat:
                _currentTransport = tugboatTransport;
                break;
            case TransportType.Yak:
                _currentTransport = yakTransport;
                break;
            case TransportType.Multipass:
                _currentTransport = multipassTransport;
                break;
        }

        if (_currentTransport != null)
        {
            _networkManager.TransportManager.Transport = _currentTransport;
        }
    }

    [SerializeField] private Tugboat _transport;    
    private void ConfigureNetworkManager()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        if (_transport == null && _networkManager != null)
            _transport = _networkManager.TransportManager.Transport as Tugboat;

        Configure();
    }
    

    private void Configure()
    {
        if (_transport != null)
        {
            // Set port before starting
            _transport.SetPort((ushort)port);
        }

        // ✅ Tickrate & interpolation must be set in Inspector (NetworkManager > TimeManager).
        Debug.Log($"[NetworkConfigurator] TickRate = {_networkManager.TimeManager.TickRate}");

        // ✅ Logging must also be configured in Inspector (DebugManager component).
        Debug.Log($"[NetworkConfigurator] Logging is controlled via DebugManager inspector.");

        // ✅ Max players must be enforced manually using connection approval.
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

        // Scenes: handled via NetworkSceneManager now.
        // Use: _networkManager.SceneManager.LoadGlobalScenes() when needed.    

    }


        private void SetupEventHandlers()
    {
        if (_networkManager == null) return;

        // Server events
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnAuthenticationResult += OnAuthenticationResult;

        // Client events
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;

        // ✅ Replace latency updates with TimeManager event
        _networkManager.TimeManager.OnRoundTripTimeUpdated += OnRoundTripTimeUpdated;

        // Scene events
        _networkManager.SceneManager.OnLoadStart += OnSceneLoadStart;
        _networkManager.SceneManager.OnLoadPercentChange += OnSceneLoadProgress;
        _networkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;

        // ✅ No longer available directly
        // Use OnRemoteConnectionState instead of OnConnected / OnDisconnected
    }
    
        private void OnRoundTripTimeUpdated(long rtt)
    {
        Debug.Log($"Local client latency: {rtt} ms");
    }


    private void SetupUI()
    {
        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);
        if (disconnectButton != null) disconnectButton.onClick.AddListener(OnDisconnectClicked);
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);

        if (serverIPInput != null) serverIPInput.text = defaultServerIP;
        if (serverPortInput != null) serverPortInput.text = port.ToString();
        if (playerNameInput != null) playerNameInput.text = _playerName.Value;

        ShowPanel(mainMenuPanel);
    }

    #region UI Methods

    private void ShowPanel(GameObject panel)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(panel == mainMenuPanel);
        if (connectionPanel != null) connectionPanel.SetActive(panel == connectionPanel);
        if (lobbyPanel != null) lobbyPanel.SetActive(panel == lobbyPanel);
        if (loadingPanel != null) loadingPanel.SetActive(panel == loadingPanel);
    }

    private void UpdateConnectionStatus(string status)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = $"Status: {status}";
        }
    }

    private void UpdatePlayerCount()
    {
        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {_lobbyPlayers.Count}/{maxPlayers}";
        }
    }

    private void UpdatePlayerList()
    {
        if (playerListContent == null) return;

        // Clear existing list
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        // Create new list items
        foreach (var playerData in _lobbyPlayers.Values)
        {
            GameObject listItem = Instantiate(playerListItemPrefab, playerListContent);
            PlayerListItemUI itemUI = listItem.GetComponent<PlayerListItemUI>();
            
            if (itemUI != null)
            {
                itemUI.Initialize(
                    playerData.PlayerName,
                    playerData.TeamId,
                    playerData.IsReady,
                    playerData.IsHost,
                    playerData.Ping
                );
            }
        }
    }

    private void UpdateLoadingProgress(float progress)
    {
        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = progress;
        }
    }

    #endregion

    #region Network Event Handlers

    public void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"Server connection state: {args.ConnectionState}");
        UpdateConnectionStatus(args.ConnectionState.ToString());

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            OnServerStarted();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            OnServerStopped();
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"Client connection state: {args.ConnectionState}");
        UpdateConnectionStatus(args.ConnectionState.ToString());

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            OnClientConnected();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            OnClientDisconnected();
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        Debug.Log($"Remote connection {conn.ClientId} state: {args.ConnectionState}");        

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            OnPlayerJoined(conn);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            OnPlayerLeft(conn);
        }
    }

    private void OnAuthenticationResult(NetworkConnection conn, bool authenticated)
    {
        if (!authenticated)
        {
            Debug.LogWarning($"Player {conn.ClientId} failed authentication");
            conn.Disconnect(false);
        }
    }

    private void OnConnectionLatencyUpdated(NetworkConnection conn, int latency)
    {
        if (_lobbyPlayers.ContainsKey(conn))
        {
            _lobbyPlayers[conn].Ping = latency;
            UpdatePlayerList();
        }
    }

    private void OnSceneLoadStart(SceneLoadStartEventArgs args)
    {
        ShowPanel(loadingPanel);
        UpdateLoadingProgress(0f);
    }

    private void OnSceneLoadProgress(SceneLoadPercentEventArgs args)
    {
        UpdateLoadingProgress(args.Percent);
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        if (args.QueueData.AsServer)
        {
            OnServerSceneLoaded(args.LoadedScenes);
        }
        else
        {
            OnClientSceneLoaded(args.LoadedScenes);
        }
    }

    #endregion

    #region Connection Management

    public void StartHost()
    {
        if (_networkManager.IsServerStarted) return;

        Debug.Log("Starting host...");
        UpdateConnectionStatus("Starting Host...");

        // Configure server first
        _networkManager.ServerManager.StartConnection();

        // Then connect locally
        _networkManager.ClientManager.StartConnection();
    }

    public void StartClient(string ipAddress = null, int port = 0)
    {
        if (_networkManager.IsClientStarted) return;

        string targetIP = string.IsNullOrEmpty(ipAddress) ? defaultServerIP : ipAddress;
        int targetPort = port == 0 ? this.port : port;

        Debug.Log($"Connecting to {targetIP}:{targetPort}");
        UpdateConnectionStatus("Connecting...");

        if (_currentTransport is Tugboat tugboat)
        {
            tugboat.SetClientAddress(targetIP);
            tugboat.SetPort((ushort)targetPort);
        }

        _networkManager.ClientManager.StartConnection();
    }

    public void Disconnect()
    {
        if (_networkManager.IsServerStarted)
        {
            _networkManager.ServerManager.StopConnection(true);
        }

        if (_networkManager.IsClientStarted)
        {
            _networkManager.ClientManager.StopConnection();
        }

        ClearLobbyData();
        ShowPanel(mainMenuPanel);
    }

    private void ClearLobbyData()
    {
        _lobbyPlayers.Clear();
        _isReady = false;
        UpdatePlayerList();
        UpdatePlayerCount();
    }

    #endregion

    #region Game State Handlers

    private void OnServerStarted()
    {
        Debug.Log("Server started successfully");
        LoadLobbyScene();
    }

    private void OnServerStopped()
    {
        Debug.Log("Server stopped");
        ClearLobbyData();
    }

    private void OnClientConnected()
    {
        Debug.Log("Client connected successfully");
        ShowPanel(lobbyPanel);
        StartPingUpdates();
        StartNetworkStatsUpdates();

        // Send player info to server
        SendPlayerInfo();
    }

    private void OnClientDisconnected()
    {
        Debug.Log("Client disconnected");
        StopPingUpdates();
        StopNetworkStatsUpdates();
        ClearLobbyData();
        ShowPanel(mainMenuPanel);
    }

    private void OnPlayerJoined(NetworkConnection conn)
    {
        Debug.Log($"Player {conn.ClientId} joined");

        // Add to lobby
        _lobbyPlayers[conn] = new PlayerLobbyData
        {
            Connection = conn,
            PlayerName = $"Player {conn.ClientId}",
            TeamId = 0,
            IsReady = false,
            IsHost = conn.IsLocalClient,
            Ping = 0
        };

        UpdatePlayerList();
        UpdatePlayerCount();

        // If we're the host, send current lobby state to new player
        if (IsHost)
        {
            SendLobbyStateToClient(conn);
        }
    }

    private void OnPlayerLeft(NetworkConnection conn)
    {
        Debug.Log($"Player {conn.ClientId} left");

        if (_lobbyPlayers.ContainsKey(conn))
        {
            _lobbyPlayers.Remove(conn);
        }

        UpdatePlayerList();
        UpdatePlayerCount();

        // Check if host left and we need to migrate
        if (IsHost && _lobbyPlayers.Count > 0)
        {
            CheckHostMigration();
        }
    }

    private void OnServerSceneLoaded(Scene[] loadedScenes)
    {
        Debug.Log("Server scene loading complete");
        
        // Initialize game state for loaded scenes
        foreach (Scene scene in loadedScenes)
        {
            if (gameScenes.Contains(scene.name))
            {
                InitializeGameScene(scene);
            }
        }
    }

    private void OnClientSceneLoaded(Scene[] loadedScenes)
    {
        Debug.Log("Client scene loading complete");
        ShowPanel(lobbyPanel);

        // Handle scene-specific initialization
        foreach (Scene scene in loadedScenes)
        {
            if (scene.name == lobbyScene)
            {
                InitializeLobbyScene();
            }
            else if (gameScenes.Contains(scene.name))
            {
                InitializeGameScene(scene);
            }
        }
    }

    #endregion

    #region Scene Management

    public void LoadLobbyScene()
    {
        SceneLoadData sld = new SceneLoadData(lobbyScene);
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    public void LoadGameScene(string sceneName)
    {
        if (!gameScenes.Contains(sceneName))
        {
            Debug.LogError($"Scene {sceneName} is not a valid game scene");
            return;
        }

        SceneLoadData sld = new SceneLoadData(sceneName);
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    public void LoadMainMenu()
    {
        SceneLoadData sld = new SceneLoadData(mainMenuScene);
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    private void InitializeLobbyScene()
    {
        Debug.Log("Initializing lobby scene");
        // Lobby-specific initialization
    }

    private void InitializeGameScene(Scene scene)
    {
        Debug.Log($"Initializing game scene: {scene.name}");
        // Game-specific initialization
    }

    #endregion

    #region Player Management

    private void SendPlayerInfo()
    {
        // Send player name and preferences to server
        UpdatePlayerName(_playerName.Value);
    }

    public void UpdatePlayerName(string newName)
    {
        _playerName.Value = newName;
        SavePlayerPreferences();

        if (IsClient)
        {
            SendPlayerNameToServer(newName);
        }
    }
    

    [ServerRpc(RequireOwnership = false)]
    private void SendPlayerNameToServer(string name, NetworkConnection conn = null)
    {
        if (conn != null && _lobbyPlayers.ContainsKey(conn))
        {
            _lobbyPlayers[conn].PlayerName = name;
            BroadcastPlayerUpdate(conn);
        }
    }

    [Server]
    private void SendLobbyStateToClient(NetworkConnection conn)
    {
        // Send current lobby state to newly connected client
        foreach (var playerData in _lobbyPlayers.Values)
        {
            if (playerData.Connection != conn)
            {
                SendPlayerUpdateToClient(conn, playerData);
            }
        }
    }

    [TargetRpc]
    private void SendPlayerUpdateToClient(NetworkConnection target, PlayerLobbyData playerData)
    {
        if (!_lobbyPlayers.ContainsKey(playerData.Connection))
        {
            _lobbyPlayers[playerData.Connection] = playerData;
        }
        else
        {
            _lobbyPlayers[playerData.Connection] = playerData;
        }

        UpdatePlayerList();
    }

    [ObserversRpc]
    private void BroadcastPlayerUpdate(NetworkConnection conn)
    {
        if (_lobbyPlayers.ContainsKey(conn))
        {
            UpdatePlayerList();
        }
    }

    public void SetPlayerReady(bool ready)
    {
        _isReady = ready;

        if (IsClient)
        {
            SendReadyStateToServer(ready);
        }

        UpdateReadyButtonUI();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendReadyStateToServer(bool ready, NetworkConnection conn = null)
    {
        if (conn != null && _lobbyPlayers.ContainsKey(conn))
        {
            _lobbyPlayers[conn].IsReady = ready;
            BroadcastPlayerUpdate(conn);
            CheckAllPlayersReady();
        }
    }

    [Server]
    private void CheckAllPlayersReady()
    {
        if (_lobbyPlayers.Values.All(p => p.IsReady) && _lobbyPlayers.Count >= 2)
        {
            StartGame();
        }
    }

    [Server]
    private void CheckHostMigration()
    {
        // Implement host migration logic if needed
    }

    #endregion

    #region Game Flow

    [Server]
    private void StartGame()
    {
        Debug.Log("All players ready, starting game...");
        
        // Load first game scene
        LoadGameScene(gameScenes[0]);
        
        // Notify clients game is starting
        NotifyGameStart();
    }

    [ObserversRpc]
    private void NotifyGameStart()
    {
        Debug.Log("Game is starting!");
        // Handle game start on clients
    }

    #endregion

    #region Utility Methods

    private void StartPingUpdates()
    {
        if (_pingUpdateCoroutine != null) StopCoroutine(_pingUpdateCoroutine);
        _pingUpdateCoroutine = StartCoroutine(PingUpdateRoutine());
    }

    private void StopPingUpdates()
    {
        if (_pingUpdateCoroutine != null) StopCoroutine(_pingUpdateCoroutine);
    }

   private IEnumerator PingUpdateRoutine()
{
    while (true)
    {
        if (pingText != null && _networkManager.ClientManager.Connection != null)
        {
            // FishNet v4: RTT is in milliseconds as a float
            float ping = _networkManager.TimeManager.RoundTripTime;
            pingText.text = $"Ping: {Mathf.RoundToInt(ping)} ms";
        }
        yield return new WaitForSeconds(1f);
    }
}


    private void StartNetworkStatsUpdates()
    {
        if (_networkStatsCoroutine != null) StopCoroutine(_networkStatsCoroutine);
        _networkStatsCoroutine = StartCoroutine(NetworkStatsRoutine());
    }

    private void StopNetworkStatsUpdates()
    {
        if (_networkStatsCoroutine != null) StopCoroutine(_networkStatsCoroutine);
    }

    private IEnumerator NetworkStatsRoutine()
    {
        while (true)
        {
            if (networkStatsPanel != null && networkStatsPanel.activeSelf)
            {
                UpdateNetworkStats();
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void UpdateNetworkStats()
{
    // FPS
    if (fpsText != null)
    {
        fpsText.text = $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}";
    }

    // RTT / Ping
    if (rttText != null)
    {
        // FishNet v4: get RTT from TimeManager
        float rtt = _networkManager.TimeManager.RoundTripTime; // in milliseconds
        rttText.text = $"RTT: {Mathf.RoundToInt(rtt)}ms";
    }

    // Note: Packet loss requires custom tracking or using TimeManager stats
}


    private void LoadPlayerPreferences()
    {
        _playerName.Value = PlayerPrefs.GetString("PlayerName", "Player");
        // Load other preferences...
    }

    private void SavePlayerPreferences()
    {
        PlayerPrefs.SetString("PlayerName", _playerName.Value);
        PlayerPrefs.Save();
    }

    #endregion

    #region UI Event Handlers

    private void OnHostClicked()
    {
        StartHost();
    }

    private void OnConnectClicked()
    {
        string ip = serverIPInput != null ? serverIPInput.text : defaultServerIP;
        int port = serverPortInput != null ? int.Parse(serverPortInput.text) : this.port;
        StartClient(ip, port);
    }

    private void OnDisconnectClicked()
    {
        Disconnect();
    }

    private void OnReadyClicked()
    {
        SetPlayerReady(!_isReady);
    }

    private void UpdateReadyButtonUI()
    {
        if (readyButton != null)
        {
            readyButton.GetComponentInChildren<TMP_Text>().text = _isReady ? "Unready" : "Ready";
            // Change color or other visual feedback
        }
    }

    #endregion

    #region Public API

    public bool IsPlayerReady(NetworkConnection conn)
    {
        return _lobbyPlayers.ContainsKey(conn) && _lobbyPlayers[conn].IsReady;
    }

    public List<NetworkConnection> GetConnectedPlayers()
    {
        return _lobbyPlayers.Keys.ToList();
    }

    public int GetPlayerPing(NetworkConnection conn)
    {
        return _lobbyPlayers.ContainsKey(conn) ? _lobbyPlayers[conn].Ping : -1;
    }

    public void KickPlayer(NetworkConnection conn, string reason = "")
    {
        if (IsServer && conn != null)
        {
            Debug.Log($"Kicking player {conn.ClientId}: {reason}");
            conn.Disconnect(false);
        }
    }

    public void BanPlayer(string ipAddress, float duration = 3600)
    {
        // Implement IP banning logic
    }

    public void ChangeTransport(TransportType newTransport)
    {
        // Implement transport switching logic
    }

    public void SetServerTickRate(int tickRate)
{
    if (IsServer)
    {
        _networkManager.TimeManager.SetTickRate((ushort)tickRate);
    }
}

    #endregion

    private void OnDestroy()
    {
        // Clean up event handlers
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _networkManager.SceneManager.OnLoadStart -= OnSceneLoadStart;
            _networkManager.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        }

        StopAllCoroutines();
    }
}

    // UI component for player list items
    public class PlayerListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text readyStatusText;
        [SerializeField] private TMP_Text pingText;
        [SerializeField] private Image teamIndicator;
        [SerializeField] private Image hostIndicator;

        public void Initialize(string name, int teamId, bool isReady, bool isHost, int ping)
        {
            playerNameText.text = name;
            readyStatusText.text = isReady ? "Ready" : "Not Ready";
            readyStatusText.color = isReady ? Color.green : Color.red;
            pingText.text = $"{ping}ms";

            teamIndicator.color = teamId == 1 ? Color.red :
                                teamId == 2 ? Color.blue : Color.gray;

            hostIndicator.gameObject.SetActive(isHost);
        }

    }
}