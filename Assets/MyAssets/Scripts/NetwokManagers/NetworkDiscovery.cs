using FishNet;
using FishNet.Broadcast;
using FishNet.Transporting;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class NetworkDiscovery : MonoBehaviour
{
    private UdpClient _udpClient;
    private const int DiscoveryPort = 7777;

    public void BroadcastServer()
    {
        // Server broadcasts its existence
        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;
        
        InvokeRepeating(nameof(SendBroadcast), 0f, 1f);
    }

    public void DiscoverServers()
    {
        // Client listens for servers
        _udpClient = new UdpClient(DiscoveryPort);
        _udpClient.BeginReceive(OnDiscoveryReceived, null);
    }

    private void SendBroadcast()
    {
        string message = "GRAVITY_DRIFT_SERVER";
        byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
        _udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
    }

    private void OnDiscoveryReceived(System.IAsyncResult result)
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, DiscoveryPort);
        byte[] data = _udpClient.EndReceive(result, ref remoteEndPoint);
        
        string message = System.Text.Encoding.UTF8.GetString(data);
        if (message == "GRAVITY_DRIFT_SERVER")
        {
            Debug.Log($"Found server at: {remoteEndPoint.Address}");
            // Connect to this server
            InstanceFinder.NetworkManager.ClientManager.StartConnection(remoteEndPoint.Address.ToString());
        }
        
        _udpClient.BeginReceive(OnDiscoveryReceived, null);
    }
}