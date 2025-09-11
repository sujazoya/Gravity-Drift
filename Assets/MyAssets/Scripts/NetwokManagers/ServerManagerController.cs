using FishNet.Managing.Server;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Transporting.Multipass;

public class ServerManagerController : MonoBehaviour
{
    private ServerManager _serverManager;
    private NetworkManager _networkManager;   // ✅ Added this
    private Dictionary<int, PlayerData> _playerData = new Dictionary<int, PlayerData>();

    public void Initialize(ServerManager serverManager, NetworkManager networkManager)
    {
        _networkManager = networkManager;
        _serverManager = serverManager;
        SetupServerEvents();
    }

    void SetupServerEvents()
    {
        _serverManager.OnServerConnectionState += OnServerConnectionState;
        _serverManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _serverManager.OnAuthenticationResult += OnAuthenticationResult;
    }

    #region Server Event Handlers

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[SERVER] State: {args.ConnectionState}");

        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                Debug.Log("Server started successfully");
                Debug.Log("Port: " + GetServerPort());
                break;
            case LocalConnectionState.Stopped:
                Debug.Log("Server stopped");
                ClearPlayerData();
                break;
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        Debug.Log($"[CLIENT {conn.ClientId}] State: {args.ConnectionState}");

        switch (args.ConnectionState)
        {
            case RemoteConnectionState.Started:
                OnClientConnected(conn);
                break;
            case RemoteConnectionState.Stopped:
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
            conn.Disconnect(false);
        }
    }

    #endregion

    #region Client Management

    private void OnClientConnected(NetworkConnection conn)
    {
        // Create player data for new connection
        PlayerData playerData = new PlayerData
        {
            connectionId = conn.ClientId,
            username = $"Player{conn.ClientId}",
            isReady = false,
            ping = 0
        };

        _playerData[conn.ClientId] = playerData;

        Debug.Log($"Client connected: {conn.ClientId}. Total players: {_playerData.Count}");

        // Send welcome message to client
        SendWelcomeMessage(conn);
    }

    private void OnClientDisconnected(NetworkConnection conn)
    {
        if (_playerData.ContainsKey(conn.ClientId))
        {
            Debug.Log($"Client disconnected: {conn.ClientId} ({_playerData[conn.ClientId].username})");
            _playerData.Remove(conn.ClientId);
        }

        Debug.Log($"Total players remaining: {_playerData.Count}");
    }

    private void ClearPlayerData()
    {
        _playerData.Clear();
        Debug.Log("Cleared all player data");
    }

    #endregion

    #region Server Commands

    public void BroadcastMessage(string message)
    {
        foreach (var conn in _serverManager.Clients.Values)
        {
            SendMessageToClient(conn, message);
        }
    }

    public void SendMessageToClient(NetworkConnection conn, string message)
    {
        // Implement your custom message sending logic here
        Debug.Log($"Sending to client {conn.ClientId}: {message}");
    }

    public void SendWelcomeMessage(NetworkConnection conn)
    {
        string welcomeMsg = $"Welcome to the server! Your ID: {conn.ClientId}";
        SendMessageToClient(conn, welcomeMsg);
    }

    public void DisconnectClient(int clientId, string reason = "")
    {
        if (_serverManager.Clients.TryGetValue(clientId, out NetworkConnection conn))
        {
            Debug.Log($"Disconnecting client {clientId}: {reason}");
            conn.Disconnect(false);
        }
    }

    public int GetPlayerCount()
    {
        return _playerData.Count;
    }

    public Dictionary<int, PlayerData> GetAllPlayers()
    {
        return new Dictionary<int, PlayerData>(_playerData);
    }

    #endregion

    #region Utility Methods

    public bool IsServerRunning()
    {
        return _serverManager.Started;
    }

    public string GetServerStatus()
    {
        return _serverManager.Started ? "Running" : "Stopped";
    }

   

public int GetServerPort()
    {
        if (_networkManager == null)
        {
            Debug.LogWarning("NetworkManager not set.");
            return -1;
        }

        var transport = _networkManager.TransportManager.Transport;

        // If you are using MultipassTransport (optional package)
        // if (transport is MultipassTransport multipass)
        //     return multipass.Port;

        // Otherwise, just use the default ITransport API if available
        return transport.GetPort();
    }


    #endregion
}

[System.Serializable]
public class PlayerData
{
    public int connectionId;
    public string username;
    public bool isReady;
    public int ping;
    public int score;
}