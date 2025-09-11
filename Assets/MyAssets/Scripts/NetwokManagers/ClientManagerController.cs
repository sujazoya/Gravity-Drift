using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Transporting;

public class ClientManagerController : MonoBehaviour
{
    private ClientManager _clientManager;
    [Header("Default connection")]
    public string serverIp = "127.0.0.1";
    public ushort serverPort = 7777;

    private void Awake()
    {
        // If a NetworkManager exists in scene, auto-initialize using its ClientManager
        if (InstanceFinder.NetworkManager != null)
            Initialize(InstanceFinder.NetworkManager.ClientManager);
    }

    public void Initialize(ClientManager clientManager)
    {
        _clientManager = clientManager;
        SetupClientEvents();
    }

    void SetupClientEvents()
    {
        if (_clientManager == null) return;
        _clientManager.OnClientConnectionState += OnClientConnectionState;
    }

    #region Event Handlers
    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[CLIENT] State: {args.ConnectionState}");

        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                Debug.Log("✅ Connected to server successfully");
                break;
            case LocalConnectionState.Stopped:
                Debug.Log("❌ Disconnected from server");
                break;           
            case LocalConnectionState.Stopping:
                Debug.LogError("⚠️ Connection issue");
                break;
        }
    }
    #endregion

    #region Connect / Transport configuration

    // Public call to connect; will auto-configure the transport if Multipass is present
    public void ConnectToServer(string ip = null, ushort? port = null)
    {
        if (_clientManager == null)
        {
            Debug.LogError("ClientManager not initialized!");
            return;
        }

        string useIp = string.IsNullOrEmpty(ip) ? serverIp : ip;
        ushort usePort = port ?? serverPort;

        // Configure transport (reflective, multipass-safe)
        ConfigureTransport(useIp, usePort);

        // Start connection - prefer StartConnection(string) if available, otherwise parameterless
        MethodInfo startWithIp = _clientManager.GetType().GetMethod("StartConnection", new Type[] { typeof(string) });
        if (startWithIp != null)
        {
            startWithIp.Invoke(_clientManager, new object[] { useIp });
        }
        else
        {
            // fallback to parameterless StartConnection
            MethodInfo startNoArg = _clientManager.GetType().GetMethod("StartConnection", Type.EmptyTypes);
            if (startNoArg != null) startNoArg.Invoke(_clientManager, null);
            else Debug.LogWarning("No StartConnection method found on ClientManager.");
        }

        Debug.Log($"Connecting to {useIp}:{usePort}...");
    }

    public void DisconnectFromServer()
    {
        if (_clientManager == null) return;
        Debug.Log("Disconnecting from server...");
        _clientManager.StopConnection();
    }

    /// <summary>
    /// Configures underlying transport(s) for Multipass or single transport via reflection.
    /// It will attempt to set Address/Host and Port on the transport or its Settings, trying many common names.
    /// This avoids direct compile-time dependency on KCP/LiteNet/Tugboat assemblies.
    /// </summary>
    public void ConfigureTransport(string ip, ushort port)
    {
        if (_clientManager == null || _clientManager.NetworkManager == null)
        {
            Debug.LogWarning("Cannot configure transport: ClientManager or NetworkManager is null.");
            return;
        }

        Transport topTransport = null;
        try { topTransport = _clientManager.NetworkManager.TransportManager.Transport; }
        catch (Exception e) { Debug.LogWarning($"Failed to get Transport: {e.Message}"); return; }

        if (topTransport == null)
        {
            Debug.LogWarning("No transport found on TransportManager.");
            return;
        }

        Type topType = topTransport.GetType();

        // Helper: try set property or method on object (transport or its settings)
        void TrySetOnObject(object target, IEnumerable<string> propNames, object value)
        {
            if (target == null) return;
            Type t = target.GetType();
            foreach (var pn in propNames)
            {
                var p = t.GetProperty(pn, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null && p.CanWrite)
                {
                    try { p.SetValue(target, Convert.ChangeType(value, p.PropertyType)); return; }
                    catch { }
                }

                // try field too
                var f = t.GetField(pn, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (f != null)
                {
                    try { f.SetValue(target, Convert.ChangeType(value, f.FieldType)); return; }
                    catch { }
                }

                // try method SetXxx
                var m = t.GetMethod("Set" + pn, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (m != null)
                {
                    try
                    {
                        m.Invoke(target, new object[] { Convert.ChangeType(value, m.GetParameters()[0].ParameterType) });
                        return;
                    }
                    catch { }
                }
            }
        }

        // Try detect Multipass (collection of inner transports)
        var propTransports = topType.GetProperty("Transports", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propTransports != null)
        {
            var collection = propTransports.GetValue(topTransport) as IEnumerable;
            if (collection != null)
            {
                foreach (var inner in collection)
                {
                    if (inner == null) continue;
                    // Try set address on inner transport or its Settings object
                    TrySetOnObject(inner, new[] { "Address", "Host", "ServerAddress", "Ip", "IPAddress" }, ip);
                    TrySetOnObject(inner, new[] { "Port", "PortNumber", "RemotePort" }, port);

                    var settingsProp = inner.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (settingsProp != null)
                    {
                        var settingsObj = settingsProp.GetValue(inner);
                        TrySetOnObject(settingsObj, new[] { "Address", "Host", "ServerAddress", "Ip", "IPAddress" }, ip);
                        TrySetOnObject(settingsObj, new[] { "Port", "PortNumber", "RemotePort" }, port);
                    }

                    Debug.Log($"[ConfigureTransport] Attempted to configure inner transport: {inner.GetType().Name}");
                }
                return;
            }
        }

        // If not Multipass-like, try TransportCount + GetTransport(i)
        var propCount = topType.GetProperty("TransportCount", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var methodGetTransport = topType.GetMethod("GetTransport", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propCount != null && methodGetTransport != null)
        {
            int count = (int)propCount.GetValue(topTransport);
            for (int i = 0; i < count; i++)
            {
                var inner = methodGetTransport.Invoke(topTransport, new object[] { i });
                if (inner == null) continue;

                TrySetOnObject(inner, new[] { "Address", "Host", "ServerAddress", "Ip", "IPAddress" }, ip);
                TrySetOnObject(inner, new[] { "Port", "PortNumber", "RemotePort" }, port);

                var settingsProp = inner.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (settingsProp != null)
                {
                    var settingsObj = settingsProp.GetValue(inner);
                    TrySetOnObject(settingsObj, new[] { "Address", "Host", "ServerAddress", "Ip", "IPAddress" }, ip);
                    TrySetOnObject(settingsObj, new[] { "Port", "PortNumber", "RemotePort" }, port);
                }

                Debug.Log($"[ConfigureTransport] Attempted to configure transport index {i}: {inner.GetType().Name}");
            }
            return;
        }

        // Fallback: try to set directly on the top-level transport
        TrySetOnObject(topTransport, new[] { "Address", "Host", "ServerAddress", "Ip", "IPAddress" }, ip);
        TrySetOnObject(topTransport, new[] { "Port", "PortNumber", "RemotePort" }, port);
        var topSettings = topType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (topSettings != null)
        {
            var s = topSettings.GetValue(topTransport);
            TrySetOnObject(s, new[] { "Address", "Host", "ServerAddress", "Ip", "IPAddress" }, ip);
            TrySetOnObject(s, new[] { "Port", "PortNumber", "RemotePort" }, port);
        }

        Debug.Log("[ConfigureTransport] Finished transport configuration attempts.");
    }

    #endregion

    #region Utility

    public bool IsConnected() => _clientManager?.Started ?? false;

    public string GetConnectionStatus() => IsConnected() ? "Connected" : "Disconnected";

    // Use tick-based approximation for server time (NetworkTime removed in newer FishNet)
    public double GetServerTime()
    {
        var tm = _clientManager?.NetworkManager?.TimeManager;
        if (tm == null) return 0;
        // Use Tick * TickDelta if available; use reflection safe-guards
        try
        {
            // Tick property
            var tickProp = tm.GetType().GetProperty("Tick", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var tickDeltaProp = tm.GetType().GetProperty("TickDelta", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            double tick = tickProp != null ? Convert.ToDouble(tickProp.GetValue(tm)) : 0.0;
            double tickDelta = tickDeltaProp != null ? Convert.ToDouble(tickDeltaProp.GetValue(tm)) : 0.0;
            return tick * tickDelta;
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}
