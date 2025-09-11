// NetworkObjectFinder.cs
using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet;

public static class NetworkObjectFinder
{
    /// <summary>
    /// Tries to find a NetworkObject by its object id in a way that is tolerant
    /// to different FishNet API surface variations (v4.x).
    /// </summary>
    public static bool TryGetNetworkObjectById(int objectId, out NetworkObject result)
    {
        result = null;

        // 1) Try ServerManager -> ServerObjects -> Spawned dictionary (if running on server)
        try
        {
            var serverManager = InstanceFinder.ServerManager;
            if (serverManager != null)
            {
                // Use reflection to avoid compile-time dependency on exact property names
                var serverManagerType = serverManager.GetType();
                // common property name is "ServerObjects"
                var serverObjectsProp = serverManagerType.GetProperty("ServerObjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (serverObjectsProp != null)
                {
                    var serverObjectsInstance = serverObjectsProp.GetValue(serverManager);
                    if (serverObjectsInstance != null)
                    {
                        if (TryGetFromSpawnedContainer(serverObjectsInstance, objectId, out result))
                            return true;
                    }
                }

                // sometimes ServerManager exposes a dictionary directly named "Spawned" or "Objects"
                if (TryGetFromSpawnedContainer(serverManager, objectId, out result))
                    return true;
            }
        }
        catch { /* swallow - we'll try other approaches */ }

        // 2) Try NetworkManager (some versions expose spawn collections on it)
        try
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm != null)
            {
                if (TryGetFromSpawnedContainer(nm, objectId, out result))
                    return true;
            }
        }
        catch { /* ignore and fall back */ }

        // 3) Fallback: brute-force scan of all NetworkObject instances in the scene (including inactive)
        try
        {
            // includeInactive: true -> needs Unity 2020+ overload; use FindObjectsOfType(typeof(NetworkObject), true) for compatibility
#if UNITY_2023_2_OR_NEWER
            var all = UnityEngine.Object.FindObjectsOfType<NetworkObject>(true);
#else
            var temp = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(NetworkObject));
            var list = new System.Collections.Generic.List<NetworkObject>();
            foreach (var o in temp)
            {
                if (o is NetworkObject no) list.Add(no);
            }
            var all = list.ToArray();
#endif
            foreach (var no in all)
            {
                // Try common property names for object id
                int id = TryReadObjectId(no);
                if (id == objectId)
                {
                    result = no;
                    return true;
                }
            }
        }
        catch { /* final fallback failed */ }

        return false;
    }

    // Tries to read a common object id field/property from a NetworkObject instance
    private static int TryReadObjectId(NetworkObject no)
    {
        if (no == null) return -1;

        Type t = no.GetType();
        // check properties first
        var p = t.GetProperty("ObjectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             ?? t.GetProperty("NetworkObjectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             ?? t.GetProperty("ObjectIdentifier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null)
        {
            object v = p.GetValue(no);
            if (v is int vi) return vi;
            if (v is uint vu) return (int)vu;
            if (v is long vl) return (int)vl;
        }

        // check fields
        var f = t.GetField("ObjectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             ?? t.GetField("_objectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             ?? t.GetField("objectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null)
        {
            object v = f.GetValue(no);
            if (v is int vi) return vi;
            if (v is uint vu) return (int)vu;
            if (v is long vl) return (int)vl;
        }

        // If nothing found, return -1
        return -1;
    }

    // Given an object that may contain a Spawned dictionary (ServerObjects, ServerManager, NetworkManager, etc.)
    // try to find the NetworkObject inside. This uses reflection and IDictionary fallback.
    private static bool TryGetFromSpawnedContainer(object container, int objectId, out NetworkObject result)
    {
        result = null;
        if (container == null) return false;

        Type contType = container.GetType();

        // 1) Try property "Spawned"
        var spawnedProp = contType.GetProperty("Spawned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (spawnedProp != null)
        {
            var spawnedValue = spawnedProp.GetValue(container);
            if (TryGetFromDictLike(spawnedValue, objectId, out result)) return true;
        }

        // 2) Try property "Objects" (some versions)
        var objectsProp = contType.GetProperty("Objects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (objectsProp != null)
        {
            var objectsValue = objectsProp.GetValue(container);
            if (TryGetFromDictLike(objectsValue, objectId, out result)) return true;
        }

        // 3) Try field "Spawned"
        var spawnedField = contType.GetField("Spawned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (spawnedField != null)
        {
            var spawnedValue = spawnedField.GetValue(container);
            if (TryGetFromDictLike(spawnedValue, objectId, out result)) return true;
        }

        // 4) As last resort the container itself might be the dictionary
        if (TryGetFromDictLike(container, objectId, out result)) return true;

        return false;
    }

    // Try to use IDictionary or a generic TryGetValue to retrieve the NetworkObject
    private static bool TryGetFromDictLike(object dictLike, int objectId, out NetworkObject result)
    {
        result = null;
        if (dictLike == null) return false;

        // If it implements IDictionary, use that
        if (dictLike is IDictionary idict)
        {
            if (idict.Contains(objectId))
            {
                object v = idict[objectId];
                result = v as NetworkObject;
                return result != null;
            }
            return false;
        }

        // Try generic TryGetValue via reflection (Dictionary<int, NetworkObject>)
        var t = dictLike.GetType();
        var tryGet = t.GetMethod("TryGetValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (tryGet != null)
        {
            // Try to invoke TryGetValue(int, out NetworkObject)
            var parameters = new object[] { objectId, null };
            var ok = (bool)tryGet.Invoke(dictLike, parameters);
            if (ok && parameters[1] is NetworkObject no)
            {
                result = no;
                return true;
            }
        }

        return false;
    }
}
