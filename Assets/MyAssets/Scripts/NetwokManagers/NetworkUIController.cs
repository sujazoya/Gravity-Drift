using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUIController : MonoBehaviour
{
    [Header("Network Manager Reference")]
    public GameNetworkManager networkManager;

    [Header("Connection Panel")]
    public GameObject connectionPanel;
    public TMP_InputField ipInputField;
    public TMP_InputField portInputField;
    public TMP_InputField usernameInputField;
    public TMP_Dropdown connectionTypeDropdown;
    public TMP_Text connectionStatusText;
    public Button connectButton;
    public Button disconnectButton;
    public Button hostButton;

    [Header("Lobby Panel")]
    public GameObject lobbyPanel;
    public TMP_Text playerCountText;
    public TMP_Text serverInfoText;
    public TMP_Text playerListText;
    public Button startGameButton;
    public Button leaveLobbyButton;

    [Header("In-Game Panel")]
    public GameObject gamePanel;
    public TMP_Text gameStatusText;
    public TMP_Text pingText;
    public Button returnToLobbyButton;

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public TMP_InputField maxPlayersInputField;
    public Slider volumeSlider;
    public TMP_Text volumeValueText;
    public Button saveSettingsButton;
    public Button closeSettingsButton;

    [Header("Other References")]
    public GameObject loadingScreen;
    public TMP_Text loadingText;

    private float _pingUpdateTimer;
    private const float PING_UPDATE_INTERVAL = 1f;

    private PingTracker _pingTracker;

    void Start()
    {
        InitializeUI();
        SetupEventListeners();
        ShowConnectionPanel();

        _pingTracker = GetComponent<PingTracker>();
    }

    void Update()
    {
        UpdateUI();
        UpdatePingDisplay();
    }

    void InitializeUI()
    {
        // Set default values
        ipInputField.text = "127.0.0.1";
        portInputField.text = "7777";
        usernameInputField.text = "Player" + Random.Range(1000, 9999);
        maxPlayersInputField.text = "4";

        // Setup dropdown options
        connectionTypeDropdown.ClearOptions();
        connectionTypeDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "IPv4",
            "Localhost",
            "LAN Broadcast"
        });

        // Hide all panels initially
        connectionPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        gamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        loadingScreen.SetActive(false);
    }

    void SetupEventListeners()
    {
        // Connection panel
        connectButton.onClick.AddListener(OnConnectButton);
        disconnectButton.onClick.AddListener(OnDisconnectButton);
        hostButton.onClick.AddListener(OnHostButton);

        // Lobby panel
        startGameButton.onClick.AddListener(OnStartGameButton);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobbyButton);

        // Game panel
        returnToLobbyButton.onClick.AddListener(OnReturnToLobbyButton);

        // Settings panel
        volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        saveSettingsButton.onClick.AddListener(OnSaveSettingsButton);
        closeSettingsButton.onClick.AddListener(OnCloseSettingsButton);

        // Input field validation
        ipInputField.onValueChanged.AddListener(ValidateIpAddress);
        portInputField.onValueChanged.AddListener(ValidatePort);
        maxPlayersInputField.onValueChanged.AddListener(ValidateMaxPlayers);
    }

    void UpdateUI()
    {
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<GameNetworkManager>();
            if (networkManager == null) return;
        }

        // Update connection status
        connectionStatusText.text = GetFormattedStatus();

        // Update button states based on connection status
        UpdateButtonInteractivity();

        // Update lobby information
        if (lobbyPanel.activeSelf)
        {
            UpdateLobbyInfo();
        }

        // Update game information
        if (gamePanel.activeSelf)
        {
            UpdateGameInfo();
        }
    }

    void UpdatePingDisplay()
    {
        if (networkManager == null || !networkManager.IsClient()) return;

        _pingUpdateTimer += Time.deltaTime;
        if (_pingUpdateTimer >= PING_UPDATE_INTERVAL)
        {
            _pingUpdateTimer = 0f;
            
            // Get ping from Fish-Net TimeManager
            int ping = PingTracker.Instance.CurrentPing;
            pingText.text = $"Ping: {ping}ms";
            
            // Color code based on ping
            if (ping < 50) pingText.color = Color.green;
            else if (ping < 100) pingText.color = Color.yellow;
            else pingText.color = Color.red;
        }
    }

    string GetFormattedStatus()
    {
        if (networkManager == null) return "No Network Manager";

        string status = networkManager.GetConnectionStatus();
        int players = networkManager.GetPlayerCount();

        return $"Status: {status}\n" +
               $"Players: {players}/{networkManager.GetMaxPlayers()}\n" +
               $"Version: {networkManager.gameVersion}";
    }

    void UpdateButtonInteractivity()
{
    bool isConnected = networkManager != null &&
                       (networkManager.IsClient() || networkManager.IsServer());

    connectButton.interactable = !isConnected;
    hostButton.interactable = !isConnected;
    disconnectButton.interactable = isConnected;
    startGameButton.interactable = networkManager != null && networkManager.IsServer();
}


    void UpdateLobbyInfo()
    {
        playerCountText.text = $"Players: {networkManager.GetPlayerCount()}/{networkManager.GetMaxPlayers()}";
        serverInfoText.text = $"Server: {GetServerInfo()}";
        
        // Simulate player list (you'd replace this with actual player data)
        playerListText.text = "Player List:\n";
        playerListText.text += $"- {usernameInputField.text} (You)\n";
        playerListText.text += "- Player2\n- Player3\n- Player4";
    }

    void UpdateGameInfo()
    {
        gameStatusText.text = $"Game in Progress\n" +
                             $"Players: {networkManager.GetPlayerCount()}\n" +
                             $"Time: {Time.timeSinceLevelLoad:F1}s";
    }

    string GetServerInfo()
    {
        return $"{ipInputField.text}:{portInputField.text}";
    }

    #region Button Handlers

    public void OnConnectButton()
    {
        ShowLoadingScreen("Connecting...");
        
        string ip = connectionTypeDropdown.value switch
        {
            0 => ipInputField.text, // IPv4
            1 => "127.0.0.1",       // Localhost
            2 => "255.255.255.255", // LAN Broadcast
            _ => ipInputField.text
        };

        networkManager.StartClient(ip);
        Invoke(nameof(HideLoadingScreen), 2f);
    }

    public void OnHostButton()
    {
        ShowLoadingScreen("Starting Host...");
        networkManager.StartHost();
        Invoke(nameof(ShowLobbyPanel), 1f);
        Invoke(nameof(HideLoadingScreen), 2f);
    }

    public void OnDisconnectButton()
    {
        networkManager.StopConnection();
        ShowConnectionPanel();
    }

    public void OnStartGameButton()
    {
        ShowLoadingScreen("Starting Game...");
        // Add your game start logic here
        Invoke(nameof(ShowGamePanel), 2f);
    }

    public void OnLeaveLobbyButton()
    {
        networkManager.StopConnection();
        ShowConnectionPanel();
    }

    public void OnReturnToLobbyButton()
    {
        ShowLobbyPanel();
    }

    public void OnSettingsButton()
    {
        settingsPanel.SetActive(true);
    }

    public void OnCloseSettingsButton()
    {
        settingsPanel.SetActive(false);
    }

    public void OnSaveSettingsButton()
    {
        if (int.TryParse(maxPlayersInputField.text, out int maxPlayers))
        {
            networkManager.ChangeMaxPlayers(maxPlayers);
        }
        settingsPanel.SetActive(false);
    }

    public void OnVolumeSliderChanged(float value)
    {
        volumeValueText.text = $"{value * 100:F0}%";
        AudioListener.volume = value;
    }

    #endregion

    #region Panel Management

    void ShowConnectionPanel()
    {
        connectionPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        gamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        loadingScreen.SetActive(false);
    }

    void ShowLobbyPanel()
    {
        connectionPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        gamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        loadingScreen.SetActive(false);
    }

    void ShowGamePanel()
    {
        connectionPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        gamePanel.SetActive(true);
        settingsPanel.SetActive(false);
        loadingScreen.SetActive(false);
    }

    void ShowLoadingScreen(string message = "Loading...")
    {
        loadingScreen.SetActive(true);
        loadingText.text = message;
    }

    void HideLoadingScreen()
    {
        loadingScreen.SetActive(false);
    }

    #endregion

    #region Validation Methods

    void ValidateIpAddress(string ip)
    {
        // Basic IP validation
        if (System.Net.IPAddress.TryParse(ip, out _))
        {
            ipInputField.textComponent.color = Color.green;
        }
        else
        {
            ipInputField.textComponent.color = Color.red;
        }
    }

    void ValidatePort(string port)
    {
        if (int.TryParse(port, out int portNumber) && portNumber >= 1024 && portNumber <= 65535)
        {
            portInputField.textComponent.color = Color.green;
        }
        else
        {
            portInputField.textComponent.color = Color.red;
        }
    }

    void ValidateMaxPlayers(string maxPlayers)
    {
        if (int.TryParse(maxPlayers, out int players) && players >= 1 && players <= 16)
        {
            maxPlayersInputField.textComponent.color = Color.green;
        }
        else
        {
            maxPlayersInputField.textComponent.color = Color.red;
        }
    }

    #endregion

    #region Utility Methods for GameNetworkManager Extension

    // Add these methods to your GameNetworkManager class
    public int GetMaxPlayers()
    {
        return networkManager?.GetMaxPlayers() ?? 4;
    }

    public int GetPing()
    {
        // Fish-Net doesn't have direct ping access in the same way as other solutions
        // You might need to implement your own ping system or use:
        return Random.Range(20, 100); // Placeholder
    }

    #endregion

    // Quick access methods for other scripts
    public void ShowMessage(string message, float duration = 3f)
    {
        ShowLoadingScreen(message);
        Invoke(nameof(HideLoadingScreen), duration);
    }

    public void SetConnectionStatus(string status, Color color)
    {
        connectionStatusText.text = status;
        connectionStatusText.color = color;
    }
}