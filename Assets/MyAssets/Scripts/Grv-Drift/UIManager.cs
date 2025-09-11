using FishNet;
using FishNet.Connection;
using FishNet.Object;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Transporting;   // <-- required for RemoteConnectionStateArgs & RemoteConnectionState


public class UIManager : NetworkBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup mainCanvas;
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject spectatorPanel;

    [Header("Connection UI")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectionStatusText;

    [Header("Lobby UI")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;

    [Header("Game UI")]
    [SerializeField] private TMP_Text gameTimeText;
    [SerializeField] private TMP_Text possessionText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text abilityCooldownText;

    [Header("Pause Menu")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings")]
    [SerializeField] private float uiUpdateInterval = 0.1f;

    private bool _isPaused;
    private bool _isSpectator;
    private Coroutine _uiUpdateCoroutine;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (IsServerInitialized)
        {
           InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;
        }

        InstanceFinder.ClientManager.OnClientConnectionState += HandleClientConnectionState;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        InitializeUI();
        SetupButtonListeners();

        if (base.IsOwner)
        {
            ShowPanel(connectionPanel);
        }
    }

    private void InitializeUI()
    {
        mainCanvas.alpha = 1f;
        mainCanvas.interactable = true;
        mainCanvas.blocksRaycasts = true;
    }

    private void SetupButtonListeners()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        connectButton.onClick.AddListener(OnConnectClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        resumeButton.onClick.AddListener(OnResumeClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnHostClicked()
    {
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
    }

    private void OnConnectClicked()
    {
        string ip = string.IsNullOrEmpty(ipInputField.text) ? "localhost" : ipInputField.text;
        InstanceFinder.ClientManager.StartConnection(ip);
    }

    private void OnReadyClicked()
    {
        // Implement ready logic
        SetReadyStatus(!readyButton.interactable);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetReadyStatus(bool isReady)
    {
        // Handle ready status on server
    }

    private void OnResumeClicked()
    {
        TogglePauseMenu();
    }

    private void OnSettingsClicked()
    {
        // Open settings menu
    }

    private void OnQuitClicked()
    {
        if (IsServerInitialized)
        {
            InstanceFinder.ServerManager.StopConnection(true);
        }
        InstanceFinder.ClientManager.StopConnection();
        Application.Quit();
    }

    

    public void HandleServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"Server connection state: {args.ConnectionState}");

        // Update UI or internal status
        UpdateConnectionStatus(args.ConnectionState.ToString());

        // React to server starting
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            OnServerStarted();
        }
        // React to server stopping
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            OnServerStopped();
        }
    }    // Example methods to implement
    private void OnServerStarted()
    {
        Debug.Log("Server has started.");
    }

    private void OnServerStopped()
    {
        Debug.Log("Server has stopped.");
    }

    private void HandleClientConnectionState(ClientConnectionStateArgs args)
    {
        UpdateConnectionStatus(args.ConnectionState.ToString());

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            OnConnectedToServer();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            OnDisconnectedFromServer();
        }
    }
  

    private void UpdateConnectionStatus(string status)
    {
        connectionStatusText.text = $"Status: {status}";
    }

    private void OnConnectedToServer()
    {
        if (base.IsOwner)
        {
            ShowPanel(lobbyPanel);
        }
    }

    private void OnDisconnectedFromServer()
    {
        if (base.IsOwner)
        {
            ShowPanel(connectionPanel);
            ClearPlayerList();
        }
    }

    [ObserversRpc]
    public void OnGameStarted()
    {
        if (base.IsOwner)
        {
            ShowPanel(gamePanel);
            StartUIUpdates();
        }
    }

    [ObserversRpc]
    public void OnGameEnded()
    {
        if (base.IsOwner)
        {
            ShowPanel(lobbyPanel);
            StopUIUpdates();
        }
    }

    public void SetSpectatorMode(bool isSpectator)
    {
        _isSpectator = isSpectator;
        spectatorPanel.SetActive(isSpectator);
        
        if (isSpectator)
        {
            StopUIUpdates();
        }
    }

    private void StartUIUpdates()
    {
        if (_uiUpdateCoroutine != null)
        {
            StopCoroutine(_uiUpdateCoroutine);
        }
        _uiUpdateCoroutine = StartCoroutine(UpdateUI());
    }

    private void StopUIUpdates()
    {
        if (_uiUpdateCoroutine != null)
        {
            StopCoroutine(_uiUpdateCoroutine);
        }
    }

    private IEnumerator UpdateUI()
    {
        while (true)
        {
            UpdateGameUI();
            yield return new WaitForSeconds(uiUpdateInterval);
        }
    }

    private void UpdateGameUI()
    {
        // Update game-specific UI elements
        if (TryGetComponent<AdvancedPlayerStats>(out var playerStats))
        {
            healthSlider.value = playerStats.CurrentHealth.Value/ playerStats.MaxHealth.Value;
        }

        if (TryGetComponent<AdvancedPlayerAbilityManager>(out var abilityManager))
        {
            abilityCooldownText.text = abilityManager.GetCooldownStatus().ToString();
        }
    }

    [ObserversRpc]
    public void UpdatePlayerList(NetworkConnection[] connections)
    {
        ClearPlayerList();

        foreach (NetworkConnection conn in connections)
        {
            if (conn.IsActive)
            {
                AddPlayerToList(conn);
            }
        }
    }

   private void AddPlayerToList(NetworkConnection conn)
{
    if (conn == null) return;

    // ✅ Get the player object attached to this connection
    var playerObj = conn.FirstObject;  
    if (playerObj == null) return;

    var playerController = playerObj.GetComponent<AdvancedNetworkPlayerController>();
    if (playerController == null) return;

    // Create UI element
    GameObject listItem = Instantiate(playerListItemPrefab, playerListContent);
    PlayerListItem item = listItem.GetComponent<PlayerListItem>();

    if (item != null)
    {
        // Example: you might want to pass in teamColor (from playerController.TeamId or similar)
        Color teamColor = Color.white; 
        item.Initialize(playerController, teamColor);
    }
}

    private void ClearPlayerList()
    {
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }
    }

    public void ToggleScoreboard(bool show)
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(show);
        }
    }

    public void TogglePauseMenu()
    {
        _isPaused = !_isPaused;
        pauseMenuPanel.SetActive(_isPaused);
        
        Time.timeScale = _isPaused ? 0f : 1f;
        Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isPaused;
    }

    private void ShowPanel(GameObject panel)
    {
        connectionPanel.SetActive(panel == connectionPanel);
        lobbyPanel.SetActive(panel == lobbyPanel);
        gamePanel.SetActive(panel == gamePanel);
        pauseMenuPanel.SetActive(false);
        scoreboardPanel.SetActive(false);
    }

    [ObserversRpc]
    public void UpdateGameTime(float gameTime)
    {
        if (gameTimeText != null)
        {
            int minutes = Mathf.FloorToInt(gameTime / 60f);
            int seconds = Mathf.FloorToInt(gameTime % 60f);
            gameTimeText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    [ObserversRpc]
    public void UpdatePossessionText(string possessionInfo)
    {
        if (possessionText != null)
        {
            possessionText.text = possessionInfo;
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        StopUIUpdates();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (hostButton != null) hostButton.onClick.RemoveAllListeners();
        if (connectButton != null) connectButton.onClick.RemoveAllListeners();
        if (readyButton != null) readyButton.onClick.RemoveAllListeners();
        if (resumeButton != null) resumeButton.onClick.RemoveAllListeners();
        if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
        if (quitButton != null) quitButton.onClick.RemoveAllListeners();
    }
}