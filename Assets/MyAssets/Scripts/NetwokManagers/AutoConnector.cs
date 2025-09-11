using FishNet;
using FishNet.Managing;
using UnityEngine;

public class AutoConnector : MonoBehaviour
{
    public enum AutoConnectMode { None, Host, Client, Server }
    public AutoConnectMode connectMode = AutoConnectMode.None;
    public string serverIp = "127.0.0.1";
    public float connectDelay = 1f;

    private void Start()
    {
        if (connectMode != AutoConnectMode.None)
            Invoke(nameof(AutoConnect), connectDelay);
    }

    private void AutoConnect()
    {
        NetworkManager nm = InstanceFinder.NetworkManager; // ✅ Correct type
        if (nm == null)
        {
            Debug.LogError("No NetworkManager instance found in the scene.");
            return;
        }

        switch (connectMode)
        {
            case AutoConnectMode.Host:
                nm.ServerManager.StartConnection();
                nm.ClientManager.StartConnection();
                Debug.Log("Auto-started as Host");
                break;

            case AutoConnectMode.Client:
                nm.ClientManager.StartConnection(serverIp);
                Debug.Log($"Auto-connecting to {serverIp}");
                break;

            case AutoConnectMode.Server:
                nm.ServerManager.StartConnection();
                Debug.Log("Auto-started as Server");
                break;
        }
    }
}
