using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionManager : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button connectButton;
    public Button stopButton;
    public InputField ipInputField;
    public Text connectionStatusText;

    private NetworkManager _networkManager;
    private string _defaultIp = "127.0.0.1"; // Localhost

    private void Start()
    {
        _networkManager = InstanceFinder.NetworkManager;
        
        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager not found!");
            return;
        }

        // Setup UI listeners
        if (hostButton != null) hostButton.onClick.AddListener(StartHost);
        if (connectButton != null) connectButton.onClick.AddListener(StartClient);
        if (stopButton != null) stopButton.onClick.AddListener(StopConnection);
        
        if (ipInputField != null)
        {
            ipInputField.text = _defaultIp;
            ipInputField.onValueChanged.AddListener(OnIpChanged);
        }

        // Subscribe to connection events
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;

        UpdateConnectionStatus("Disconnected");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
    }

    #region Connection Methods

    public void StartHost()
    {
        if (_networkManager == null) return;

        // Start both server and client
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();
        
        UpdateConnectionStatus("Starting Host...");
    }

    public void StartClient()
    {
        if (_networkManager == null) return;

        string ip = ipInputField != null ? ipInputField.text : _defaultIp;
        _networkManager.ClientManager.StartConnection(ip);
        
        UpdateConnectionStatus($"Connecting to {ip}...");
    }

    public void StopConnection()
    {
        if (_networkManager == null) return;

        _networkManager.ServerManager.StopConnection(true);
        _networkManager.ClientManager.StopConnection();
        
        UpdateConnectionStatus("Disconnected");
    }

    private void OnIpChanged(string newIp)
    {
        _defaultIp = newIp;
    }

    #endregion

    #region Event Handlers

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"Server connection state: {args.ConnectionState}");
        
        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                UpdateConnectionStatus("Host Started");
                break;
            case LocalConnectionState.Stopped:
                UpdateConnectionStatus("Server Stopped");
                break;
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"Client connection state: {args.ConnectionState}");
        
        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                UpdateConnectionStatus("Connected to Server");
                break;
            case LocalConnectionState.Stopped:
                UpdateConnectionStatus("Disconnected from Server");
                break;
            case LocalConnectionState.Stopping:
                UpdateConnectionStatus("Connection Failed");
                break;
        }
    }

    private void UpdateConnectionStatus(string status)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = $"Status: {status}";
        }
        Debug.Log(status);
    }

    #endregion

    #region Quick Connection Methods

    // For quick testing and debugging
    [ContextMenu("Quick Host")]
    public void QuickHost()
    {
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();
    }

    [ContextMenu("Quick Client")]
    public void QuickClient()
    {
        _networkManager.ClientManager.StartConnection("127.0.0.1");
    }

    [ContextMenu("Quick Stop")]
    public void QuickStop()
    {
        _networkManager.ServerManager.StopConnection(true);
        _networkManager.ClientManager.StopConnection();
    }

    #endregion
}