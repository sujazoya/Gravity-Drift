using FishNet;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using FishNet.Transporting;
using System;


public class ScoreboardUI : NetworkBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup scoreboardCanvas;
    [SerializeField] private Transform team1Content;
    [SerializeField] private Transform team2Content;
    [SerializeField] private Transform spectatorContent;
    [SerializeField] private GameObject playerScorePrefab;

    [Header("Team Info")]
    [SerializeField] private TMP_Text team1NameText;
    [SerializeField] private TMP_Text team2NameText;
    [SerializeField] private TMP_Text team1ScoreText;
    [SerializeField] private TMP_Text team2ScoreText;

    [Header("Match Info")]
    [SerializeField] private TMP_Text matchTimeText;
    [SerializeField] private TMP_Text gameModeText;

    private Dictionary<int, PlayerScoreItem> _playerScoreItems = new Dictionary<int, PlayerScoreItem>();
    private bool _isScoreboardVisible;
    private float _updateInterval = 0.5f;
    private Coroutine _updateCoroutine;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (IsServerInitialized)
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        scoreboardCanvas.alpha = 0f;
        scoreboardCanvas.interactable = false;
        scoreboardCanvas.blocksRaycasts = false;

        if (base.IsOwner)
        {
            _updateCoroutine = StartCoroutine(UpdateScoreboardPeriodically());
        }
    }

    private System.Collections.IEnumerator UpdateScoreboardPeriodically()
    {
        while (true)
        {
            if (_isScoreboardVisible)
            {
                UpdateScoreboard();
            }
            yield return new WaitForSeconds(_updateInterval);
        }
    }

    public void ToggleScoreboard(bool show)
    {
        _isScoreboardVisible = show;
        scoreboardCanvas.alpha = show ? 1f : 0f;
        scoreboardCanvas.interactable = show;
        scoreboardCanvas.blocksRaycasts = show;

        if (show)
        {
            UpdateScoreboard();
        }
    }

    [Client]
    private void UpdateScoreboard()
    {
        ClearScoreboard();
        UpdateTeamScores();
        UpdatePlayerList();
        UpdateMatchInfo();
    }

    [Client]
    private void UpdateTeamScores()
    {
        if (AdvancedTeamManager.Instance != null)
        {
            team1NameText.text = AdvancedTeamManager.Instance.GetTeamName(1);
            team2NameText.text = AdvancedTeamManager.Instance.GetTeamName(2);
            team1ScoreText.text = AdvancedTeamManager.Instance.GetTeamScore(1).ToString();
            team2ScoreText.text = AdvancedTeamManager.Instance.GetTeamScore(2).ToString();
        }
    }

    [Client]
    private void UpdatePlayerList()
    {
        IReadOnlyDictionary<int, NetworkObject> spawnedObjects = null;

        if (IsServerStarted)
            spawnedObjects = InstanceFinder.ServerManager.Objects.Spawned;
        else if (IsServerStarted)
            spawnedObjects = InstanceFinder.ClientManager.Objects.Spawned;

        if (spawnedObjects == null)
            return;

        foreach (NetworkObject playerObj in spawnedObjects.Values)
        {
            if (playerObj != null && playerObj.TryGetComponent<AdvancedPlayerStats>(out var playerStats))
            {
                AddPlayerToScoreboard(playerObj.Owner, playerStats);
            }
        }
    }


    [Client]
    private void AddPlayerToScoreboard(NetworkConnection owner, AdvancedPlayerStats playerStats)
    {
        if (owner == null) return;

        Transform parent = GetParentForTeam(playerStats.TeamID);
        GameObject scoreItem = Instantiate(playerScorePrefab, parent);
        PlayerScoreItem item = scoreItem.GetComponent<PlayerScoreItem>();

        if (item != null)
        {
            item.Initialize(
                owner.ClientId,
                $"Player {owner.ClientId}",
                playerStats.Kills,
                playerStats.Deaths,
                playerStats.Assists,
                playerStats.Score
            );

            _playerScoreItems[owner.ClientId] = item;
        }
    }

    private Transform GetParentForTeam(int teamId)
    {
        switch (teamId)
        {
            case 1: return team1Content;
            case 2: return team2Content;
            default: return spectatorContent;
        }
    }

    [Client]
    private void UpdateMatchInfo()
    {
        if (AdvancedMatchManager.Instance != null)
        {
            matchTimeText.text = FormatTime(AdvancedMatchManager.Instance.GetMatchTime());
            gameModeText.text = AdvancedMatchManager.Instance.GetGameMode();
        }
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    [ObserversRpc]
    public void UpdatePlayerScore(int clientId, int kills, int deaths, int assists, int score)
    {
        if (_playerScoreItems.TryGetValue(clientId, out PlayerScoreItem item))
        {
            item.UpdateStats(kills, deaths, assists, score);
        }
    }

    [ObserversRpc]
    public void OnPlayerJoined(NetworkConnection conn)
    {
        if (IsClientInitialized && conn != null && conn.IsActive)
        {
            // Player will be added in the next update cycle
        }
    }

    [ObserversRpc]
    public void OnPlayerLeft(NetworkConnection conn)
    {
        if (IsClientInitialized && conn != null && _playerScoreItems.ContainsKey(conn.ClientId))
        {
            Destroy(_playerScoreItems[conn.ClientId].gameObject);
            _playerScoreItems.Remove(conn.ClientId);
        }
    }

    private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
{
    if (args.ConnectionState == RemoteConnectionState.Stopped)
    {
        OnPlayerLeft(conn);
    }
}

    

    [ObserversRpc]
    public void OnTeamScoreUpdated(int teamId, int score)
    {
        if (teamId == 1)
        {
            team1ScoreText.text = score.ToString();
        }
        else if (teamId == 2)
        {
            team2ScoreText.text = score.ToString();
        }
    }

    [ObserversRpc]
    public void OnMatchTimeUpdated(float matchTime)
    {
        matchTimeText.text = FormatTime(matchTime);
    }

    private void ClearScoreboard()
    {
        foreach (Transform child in team1Content)
        {
            if (child.gameObject.activeInHierarchy)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (Transform child in team2Content)
        {
            if (child.gameObject.activeInHierarchy)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (Transform child in spectatorContent)
        {
            if (child.gameObject.activeInHierarchy)
            {
                Destroy(child.gameObject);
            }
        }

        _playerScoreItems.Clear();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
        }
    }

    private void OnDestroy()
    {
        ClearScoreboard();
    }
}

// Player Score Item class for individual player entries
public class PlayerScoreItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text deathsText;
    [SerializeField] private TMP_Text assistsText;
    [SerializeField] private TMP_Text scoreText;

    private int _clientId;

    public void Initialize(int clientId, string playerName, int kills, int deaths, int assists, int score)
    {
        _clientId = clientId;
        playerNameText.text = playerName;
        UpdateStats(kills, deaths, assists, score);
    }

    public void UpdateStats(int kills, int deaths, int assists, int score)
    {
        killsText.text = kills.ToString();
        deathsText.text = deaths.ToString();
        assistsText.text = assists.ToString();
        scoreText.text = score.ToString();
    }
}