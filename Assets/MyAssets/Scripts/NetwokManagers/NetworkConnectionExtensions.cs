using FishNet.Connection;
using UnityEngine;

public static class NetworkConnectionExtensions
{
    public static void SendMessage(this NetworkConnection conn, string message)
    {
        // Implement your message sending logic here
        Debug.Log($"Sending to client {conn.ClientId}: {message}");
    }

    public static void Kick(this NetworkConnection conn, string reason = "")
    {
        Debug.Log($"Kicking client {conn.ClientId}: {reason}");
        conn.Disconnect(true);
    }

    public static void Teleport(this NetworkConnection conn, Vector3 position)
    {
        conn.SendMessage($"TELEPORT:{position.x},{position.y},{position.z}");
    }

    public static void SetScore(this NetworkConnection conn, int score)
    {
        conn.SendMessage($"SET_SCORE:{score}");
    }

    public static bool IsValid(this NetworkConnection conn)
    {
        return conn != null && conn.IsActive;
    }

    public static string GetInfo(this NetworkConnection conn)
    {
        return $"Client {conn.ClientId} - Active: {conn.IsActive}, Authenticated: {conn.Authenticated}";
    }

    public static void LoadScene(this NetworkConnection conn, string sceneName)
    {
        conn.SendMessage($"LOAD_SCENE:{sceneName}");
    }

    public static void SendWarning(this NetworkConnection conn, string warning)
    {
        conn.SendMessage($"WARNING:{warning}");
    }
}