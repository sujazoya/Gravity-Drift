
using FishNet.Managing.Timing;
using UnityEngine;
using FishNet;

public class TimeManagerController : MonoBehaviour
{
    private TimeManager _timeManager;

    public void Initialize()
    {
        _timeManager = InstanceFinder.TimeManager;
        if (_timeManager != null)
            SetupTimeEvents();
    }

    void SetupTimeEvents()
    {
        _timeManager.OnRoundTripTimeUpdated += OnRoundTripTimeUpdated;
        _timeManager.OnTick += OnTick;
    }

    void OnDestroy()
    {
        if (_timeManager != null)
        {
            _timeManager.OnRoundTripTimeUpdated -= OnRoundTripTimeUpdated;
            _timeManager.OnTick -= OnTick;
        }
    }

    private void OnRoundTripTimeUpdated(long rtt)
    {
        Debug.Log($"Ping updated: {rtt} ms");
    }

    private void OnTick()
    {
        // Called once per network tick
    }

    // Expose basic timing info that does exist
    public int GetTickRate() => _timeManager?.TickRate ?? 0;
    public float GetPingInterval() => _timeManager?.PingInterval ?? 0f;
}
