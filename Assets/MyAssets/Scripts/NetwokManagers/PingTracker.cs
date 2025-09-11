using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class PingTracker : NetworkBehaviour
{
    public static PingTracker Instance;   // Singleton-like reference

    private float _lastPingTime;
    private int _ping;

    public int CurrentPing => _ping;

    private void Awake()
    {
        Instance = this;
    }

    [ContextMenu("Send Ping")]
    public void SendPing()
    {
        if (IsClient)
        {
            _lastPingTime = Time.time;
            CmdSendPing();
        }
    }

    [ServerRpc]
    private void CmdSendPing()
    {
        TargetReceivePong(Owner);
    }

    [TargetRpc]
    private void TargetReceivePong(NetworkConnection conn)
    {
        _ping = Mathf.RoundToInt((Time.time - _lastPingTime) * 1000f);
        Debug.Log($"Ping: {_ping}ms");
    }
}
