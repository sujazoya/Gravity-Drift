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


public class MenuSystem : NetworkBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject videoPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject loadingPanel;

    [Header("Main Menu UI")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Button applySettingsButton;
    [SerializeField] private Button backButton;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Network Settings")]
    [SerializeField] private TMP_InputField serverIPInput;
    [SerializeField] private TMP_InputField serverPortInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button connectButton;

    [Header("Lobby UI")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private TMP_Text pingText;

    private Dictionary<int, PlayerLobbyItem> _playerLobbyItems = new Dictionary<int, PlayerLobbyItem>();
    private GameSettings _gameSettings;
    private bool _isInitialized;
    private Coroutine _pingUpdateCoroutine;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (base.IsServerInitialized)
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        InstanceFinder.ClientManager.OnClientConnectionState += HandleClientConnectionState;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!base.IsOwner)
        {
            enabled = false;
            return;
        }

        InitializeMenuSystem();
        LoadSettings();
        SetupResolutionDropdown();
        SetupQualityDropdown();

        ShowPanel(mainMenuPanel);
    }

    private void InitializeMenuSystem()
    {
        _gameSettings = GameSettings.Instance;

        // Main menu buttons
        playButton.onClick.AddListener(OnPlayClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        // Settings buttons
        applySettingsButton.onClick.AddListener(OnApplySettingsClicked);
        backButton.onClick.AddListener(OnBackClicked);

        // Network buttons
        hostButton.onClick.AddListener(OnHostClicked);
        connectButton.onClick.AddListener(OnConnectClicked);

        // Lobby buttons
        readyButton.onClick.AddListener(OnReadyClicked);

        // Input field events
        usernameInput.onValueChanged.AddListener(OnUsernameChanged);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggle);

        _isInitialized = true;
    }

    private void SetupResolutionDropdown()
    {
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            Resolution resolution = Screen.resolutions[i];
            string option = $"{resolution.width}x{resolution.height} {resolution.refreshRateRatio}Hz";
            options.Add(option);

            if (resolution.width == Screen.currentResolution.width &&
                resolution.height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void SetupQualityDropdown()
    {
        qualityDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (string qualityName in QualitySettings.names)
        {
            options.Add(qualityName);
        }

        qualityDropdown.AddOptions(options);
        qualityDropdown.value = QualitySettings.GetQualityLevel();
        qualityDropdown.RefreshShownValue();
    }

    private void LoadSettings()
    {
        if (_gameSettings != null)
        {
            usernameInput.text = _gameSettings.Username;
            masterVolumeSlider.value = _gameSettings.MasterVolume;
            musicVolumeSlider.value = _gameSettings.MusicVolume;
            sfxVolumeSlider.value = _gameSettings.SFXVolume;
            fullscreenToggle.isOn = _gameSettings.IsFullscreen;
        }
    }

    private void OnPlayClicked()
    {
        ShowPanel(lobbyPanel);
        UpdatePingDisplay();
    }

    private void OnSettingsClicked()
    {
        ShowPanel(settingsPanel);
    }

    private void OnQuitClicked()
    {
        Application.Quit();
    }

    private void OnApplySettingsClicked()
    {
        SaveSettings();
        ApplySettings();
        ShowPanel(mainMenuPanel);
    }

    private void OnBackClicked()
    {
        ShowPanel(mainMenuPanel);
    }

    private void OnHostClicked()
    {
        SaveSettings();
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
        ShowLoadingPanel();
    }

    private void OnConnectClicked()
    {
        SaveSettings();
        string ip = string.IsNullOrEmpty(serverIPInput.text) ? "localhost" : serverIPInput.text;
        InstanceFinder.ClientManager.StartConnection(ip);
        ShowLoadingPanel();
    }

    private void OnReadyClicked()
    {
        ToggleReadyStatusServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyStatusServerRpc()
    {
        // Server handles ready status logic
        UpdatePlayerReadyStatus(base.Owner, !readyButton.interactable);
    }

    [ObserversRpc]
    private void UpdatePlayerReadyStatus(NetworkConnection conn, bool isReady)
    {
        if (_playerLobbyItems.TryGetValue(conn.ClientId, out PlayerLobbyItem item))
        {
            item.SetReadyStatus(isReady);
        }
    }

    private void OnUsernameChanged(string username)
    {
        if (_isInitialized)
        {
            _gameSettings.Username = username;
        }
    }

    private void OnMasterVolumeChanged(float volume)
    {
        if (_isInitialized)
        {
            audioMixer.SetFloat("MasterVolume", Mathf.Log10(volume) * 20);
            _gameSettings.MasterVolume = volume;
        }
    }

    private void OnMusicVolumeChanged(float volume)
    {
        if (_isInitialized)
        {
            audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
            _gameSettings.MusicVolume = volume;
        }
    }

    private void OnSFXVolumeChanged(float volume)
    {
        if (_isInitialized)
        {
            audioMixer.SetFloat("SFXVolume", Mathf.Log10(volume) * 20);
            _gameSettings.SFXVolume = volume;
        }
    }

    private void OnQualityChanged(int qualityIndex)
    {
        if (_isInitialized)
        {
            QualitySettings.SetQualityLevel(qualityIndex);
            _gameSettings.QualityLevel = qualityIndex;
        }
    }

    private void OnFullscreenToggle(bool isFullscreen)
    {
        if (_isInitialized)
        {
            Screen.fullScreen = isFullscreen;
            _gameSettings.IsFullscreen = isFullscreen;
        }
    }

    private void SaveSettings()
    {
        if (_gameSettings != null)
        {
            _gameSettings.SaveSettings();
        }
    }

    private void ApplySettings()
    {
        // Apply resolution
        Resolution selectedResolution = Screen.resolutions[resolutionDropdown.value];
        Screen.SetResolution(selectedResolution.width, selectedResolution.height, fullscreenToggle.isOn);

        // Apply audio settings
        audioMixer.SetFloat("MasterVolume", Mathf.Log10(masterVolumeSlider.value) * 20);
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolumeSlider.value) * 20);
        audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolumeSlider.value) * 20);
    }

    private void ShowPanel(GameObject panel)
    {
        mainMenuPanel.SetActive(panel == mainMenuPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        lobbyPanel.SetActive(panel == lobbyPanel);
        audioPanel.SetActive(false);
        videoPanel.SetActive(false);
        controlsPanel.SetActive(false);
        loadingPanel.SetActive(false);
    }

    private void ShowLoadingPanel()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        loadingPanel.SetActive(true);
    }

    private void HandleClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            OnConnectedToServer();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            OnDisconnectedFromServer();
        }
    }

    private void OnConnectedToServer()
    {
        ShowPanel(lobbyPanel);
        SendPlayerInfoToServer();
        StartPingUpdates();
    }

    private void OnDisconnectedFromServer()
    {
        ShowPanel(mainMenuPanel);
        ClearPlayerList();
        StopPingUpdates();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendPlayerInfoToServer()
    {
        ReceivePlayerInfo(base.Owner, _gameSettings.Username);
    }

    [ObserversRpc]
    private void ReceivePlayerInfo(NetworkConnection conn, string username)
    {
        AddPlayerToLobbyList(conn, username);
    }

    private void AddPlayerToLobbyList(NetworkConnection conn, string username)
    {
        GameObject listItem = Instantiate(playerListItemPrefab, playerListContent);
        PlayerLobbyItem item = listItem.GetComponent<PlayerLobbyItem>();
        
        if (item != null)
        {
            item.Initialize(conn.ClientId, username, false);
            _playerLobbyItems[conn.ClientId] = item;
        }
    }
    

    private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            RemovePlayerFromLobbyList(conn);
        }
    }

    private void RemovePlayerFromLobbyList(NetworkConnection conn)
    {
        if (_playerLobbyItems.ContainsKey(conn.ClientId))
        {
            Destroy(_playerLobbyItems[conn.ClientId].gameObject);
            _playerLobbyItems.Remove(conn.ClientId);
        }
    }

    private void ClearPlayerList()
    {
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }
        _playerLobbyItems.Clear();
    }

    private void StartPingUpdates()
    {
        if (_pingUpdateCoroutine != null)
        {
            StopCoroutine(_pingUpdateCoroutine);
        }
        _pingUpdateCoroutine = StartCoroutine(UpdatePingRoutine());
    }

    private void StopPingUpdates()
    {
        if (_pingUpdateCoroutine != null)
        {
            StopCoroutine(_pingUpdateCoroutine);
        }
    }

    private IEnumerator UpdatePingRoutine()
    {
        while (true)
        {
            UpdatePingDisplay();
            yield return new WaitForSeconds(1f);
        }
    }    

   private float _pingUpdateTimer;
private const float PING_UPDATE_INTERVAL = 1f; // Update every second
private int _currentPing;

private void Update()
{
    _pingUpdateTimer += Time.deltaTime;
    if (_pingUpdateTimer >= PING_UPDATE_INTERVAL)
    {
        _pingUpdateTimer = 0f;
        UpdatePingDisplay();
    }
}

private void UpdatePingDisplay()
{
    if (pingText == null) return;

    if (InstanceFinder.ClientManager != null && 
        InstanceFinder.ClientManager.Connection != null &&
        InstanceFinder.ClientManager.Connection.IsActive &&
        InstanceFinder.NetworkManager != null &&
        InstanceFinder.NetworkManager.TransportManager != null)
    {
        try
        {
            int ping = -1;
            var transport = InstanceFinder.NetworkManager.TransportManager.Transport;
            
            // Try different method names that various transports might use
            if (TryGetPing(transport, InstanceFinder.ClientManager.Connection.ClientId, out ping))
            {
                pingText.text = $"Ping: {ping}ms";
                pingText.color = GetPingColor(ping);
            }
            else
            {
                pingText.text = "Ping: --ms";
            }
        }
        catch
        {
            pingText.text = "Ping: --ms";
        }
    }
    else
    {
        pingText.text = "Ping: --ms";
    }
}

private bool TryGetPing(Transport transport, int clientId, out int ping)
{
    ping = -1;
    
    try
    {
        // Try various method names that different transports might use
        var method = transport.GetType().GetMethod("GetRTT");
        if (method != null)
        {
            ping = (int)method.Invoke(transport, new object[] { clientId });
            return true;
        }
        
        method = transport.GetType().GetMethod("GetRoundTripTime");
        if (method != null)
        {
            ping = (int)method.Invoke(transport, new object[] { clientId });
            return true;
        }
        
        method = transport.GetType().GetMethod("GetPing");
        if (method != null)
        {
            ping = (int)method.Invoke(transport, new object[] { clientId });
            return true;
        }
        
        // For some transports, you might need to use a different approach
        return false;
    }
    catch
    {
        return false;
    }
}

private Color GetPingColor(int ping)
{
    if (ping < 50) return Color.green;        // Excellent
    if (ping < 100) return Color.yellow;      // Good
    if (ping < 150) return new Color(1f, 0.5f, 0f); // Orange - Fair
    if (ping < 200) return Color.red;         // Poor
    return new Color(0.5f, 0f, 0f);          // Very Poor - Dark red
}

// Public property to access current ping from other scripts
public int CurrentPing => _currentPing;

// Method to check if ping is acceptable for gameplay
public bool IsPingAcceptable(int threshold = 200)
{
    return _currentPing <= threshold;
}
    [ObserversRpc]
    public void OnGameStarted()
    {
        // Hide menu when game starts
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        loadingPanel.SetActive(false);
    }

    [ObserversRpc]
    public void OnGameEnded()
    {
        ShowPanel(lobbyPanel);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        StopPingUpdates();
        SaveSettings();
    }

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveAllListeners();
        if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
        if (quitButton != null) quitButton.onClick.RemoveAllListeners();
        if (applySettingsButton != null) applySettingsButton.onClick.RemoveAllListeners();
        if (backButton != null) backButton.onClick.RemoveAllListeners();
        if (hostButton != null) hostButton.onClick.RemoveAllListeners();
        if (connectButton != null) connectButton.onClick.RemoveAllListeners();
        if (readyButton != null) readyButton.onClick.RemoveAllListeners();

        if (usernameInput != null) usernameInput.onValueChanged.RemoveAllListeners();
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveAllListeners();
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveAllListeners();
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveAllListeners();
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveAllListeners();
    }
}

public class PlayerLobbyItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image readyStatusImage;
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.red;

    private int _clientId;

    public void Initialize(int clientId, string playerName, bool isReady)
    {
        _clientId = clientId;
        playerNameText.text = playerName;
        SetReadyStatus(isReady);
    }

    public void SetReadyStatus(bool isReady)
    {
        readyStatusImage.color = isReady ? readyColor : notReadyColor;
    }
}