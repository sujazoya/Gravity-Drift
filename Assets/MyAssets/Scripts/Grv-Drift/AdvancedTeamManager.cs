using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using FishNet;

public class AdvancedTeamManager : NetworkBehaviour
{
    public static AdvancedTeamManager Instance { get; private set; }

    [System.Serializable]
    public class Team
    {
        public int teamId;
        public string teamName;
        public Color teamColor;
        public Transform[] spawnPoints;
        public int score;
        public List<AdvancedNetworkPlayerController> players = new List<AdvancedNetworkPlayerController>();
    }

    [Header("Team Settings")]
    public Team blueTeam = new Team { teamId = 1, teamName = "Blue Team", teamColor = Color.blue };
    public Team orangeTeam = new Team { teamId = 2, teamName = "Orange Team", teamColor = new Color(1f, 0.5f, 0f) };
    public int maxPlayersPerTeam = 3;
    public bool autoBalanceTeams = true;

    private Dictionary<int, Team> _teams = new Dictionary<int, Team>();
    private Dictionary<NetworkConnection, int> _playerTeams = new Dictionary<NetworkConnection, int>();

    public event System.Action<AdvancedNetworkPlayerController, int> OnPlayerJoinedTeam;
    public event System.Action<AdvancedNetworkPlayerController, int> OnPlayerLeftTeam;
    public event System.Action<int, int> OnTeamScoreUpdated; // teamId, newScore

    #region Initialization
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        if (Instance == null)
            Instance = this;
        
        InitializeTeams();
    }

    private void InitializeTeams()
    {
        _teams.Clear();
        _teams.Add(blueTeam.teamId, blueTeam);
        _teams.Add(orangeTeam.teamId, orangeTeam);
        
        blueTeam.players = new List<AdvancedNetworkPlayerController>();
        orangeTeam.players = new List<AdvancedNetworkPlayerController>();
    }
    #endregion

    #region Team Management
    [Server]
    public bool AssignPlayerToTeam(AdvancedNetworkPlayerController player, int teamId)
    {
        if (!_teams.ContainsKey(teamId) || !CanJoinTeam(teamId))
            return false;

        // Remove from current team if already on one
        if (_playerTeams.ContainsKey(player.Owner))
        {
            int currentTeam = _playerTeams[player.Owner];
            RemovePlayerFromTeam(player, currentTeam);
        }

        // Add to new team
        _teams[teamId].players.Add(player);
        _playerTeams[player.Owner] = teamId;
        
        player.SetTeam(teamId);
        player.SetTeamColor(_teams[teamId].teamColor);

        OnPlayerJoinedTeam?.Invoke(player, teamId);
        PlayerJoinedTeamRpc(player.ObjectId, teamId);

        return true;
    }

    [Server]
    public bool AssignPlayerToBalancedTeam(AdvancedNetworkPlayerController player)
    {
        int teamId = GetBalancedTeam();
        return AssignPlayerToTeam(player, teamId);
    }

    [Server]
    public void RemovePlayerFromTeam(AdvancedNetworkPlayerController player, int teamId)
    {
        if (_teams.ContainsKey(teamId))
        {
            _teams[teamId].players.Remove(player);
        }
        
        if (_playerTeams.ContainsKey(player.Owner))
        {
            _playerTeams.Remove(player.Owner);
        }

        OnPlayerLeftTeam?.Invoke(player, teamId);
        PlayerLeftTeamRpc(player.ObjectId, teamId);
    }

    [Server]
    public void BalanceTeams()
    {
        if (!autoBalanceTeams) return;

        int blueCount = blueTeam.players.Count;
        int orangeCount = orangeTeam.players.Count;
        int difference = Mathf.Abs(blueCount - orangeCount);

        if (difference <= 1) return;

        Team largerTeam = blueCount > orangeCount ? blueTeam : orangeTeam;
        Team smallerTeam = blueCount > orangeCount ? orangeTeam : blueTeam;

        // Move players from larger to smaller team
        for (int i = 0; i < difference / 2; i++)
        {
            if (largerTeam.players.Count > 0)
            {
                AdvancedNetworkPlayerController player = largerTeam.players[0];
                AssignPlayerToTeam(player, smallerTeam.teamId);
            }
        }
    }
    #endregion

    #region Team Utilities
    public bool CanJoinTeam(int teamId)
    {
        return _teams.ContainsKey(teamId) && _teams[teamId].players.Count < maxPlayersPerTeam;
    }

    public int GetBalancedTeam()
    {
        int blueCount = blueTeam.players.Count;
        int orangeCount = orangeTeam.players.Count;

        if (blueCount < orangeCount) return blueTeam.teamId;
        if (orangeCount < blueCount) return orangeTeam.teamId;
        return Random.Range(0, 2) == 0 ? blueTeam.teamId : orangeTeam.teamId;
    }

    public int GetPlayerTeam(NetworkConnection connection)
    {
        return _playerTeams.ContainsKey(connection) ? _playerTeams[connection] : 0;
    }

    public Team GetTeam(int teamId)
    {
        return _teams.ContainsKey(teamId) ? _teams[teamId] : null;
    }

    public Transform GetSpawnPoint(int teamId)
    {
        if (!_teams.ContainsKey(teamId)) return null;

        Team team = _teams[teamId];
        if (team.spawnPoints.Length == 0) return null;

        return team.spawnPoints[Random.Range(0, team.spawnPoints.Length)];
    }
    #endregion

    #region Score Management
    [Server]
    public void AddScore(int teamId, int points)
    {
        if (!_teams.ContainsKey(teamId)) return;

        _teams[teamId].score += points;
        OnTeamScoreUpdated?.Invoke(teamId, _teams[teamId].score);
        TeamScoreUpdatedRpc(teamId, _teams[teamId].score);
    }

    [Server]
    public void ResetScores()
    {
        foreach (var team in _teams.Values)
        {
            team.score = 0;
            OnTeamScoreUpdated?.Invoke(team.teamId, 0);
            TeamScoreUpdatedRpc(team.teamId, 0);
        }
    }

    public int GetTeamScore(int teamId)
    {
        return _teams.ContainsKey(teamId) ? _teams[teamId].score : 0;
    }

    public int GetWinningTeam()
    {
        if (blueTeam.score > orangeTeam.score) return blueTeam.teamId;
        if (orangeTeam.score > blueTeam.score) return orangeTeam.teamId;
        return 0; // Draw
    }
    #endregion

    #region Player Utilities
    public List<AdvancedNetworkPlayerController> GetTeamPlayers(int teamId)
    {
        return _teams.ContainsKey(teamId) ? _teams[teamId].players : new List<AdvancedNetworkPlayerController>();
    }

    public AdvancedNetworkPlayerController GetPlayerByConnection(NetworkConnection connection)
    {
        foreach (var team in _teams.Values)
        {
            foreach (var player in team.players)
            {
                if (player.Owner == connection)
                    return player;
            }
        }
        return null;
    }

    public int GetPlayerCount(int teamId)
    {
        return _teams.ContainsKey(teamId) ? _teams[teamId].players.Count : 0;
    }

    public int GetTotalPlayers()
    {
        return blueTeam.players.Count + orangeTeam.players.Count;
    }
    #endregion

    #region RPC Methods
    [ObserversRpc]
    
   private void PlayerJoinedTeamRpc(int playerObjectId, int teamId)
    {
        if (InstanceFinder.NetworkManager == null)
            return;

        NetworkObject playerObj;
        if (InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out playerObj))
        {
            AdvancedNetworkPlayerController player = playerObj.GetComponent<AdvancedNetworkPlayerController>();
            if (player != null)
            {
                player.SetTeam(teamId);
                player.SetTeamColor(_teams[teamId].teamColor);
                OnPlayerJoinedTeam?.Invoke(player, teamId);
            }
        }
    }


    [ObserversRpc]
   private void PlayerLeftTeamRpc(int playerObjectId, int teamId)
    {
        if (InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject playerObj))
        {
            AdvancedNetworkPlayerController player = playerObj.GetComponent<AdvancedNetworkPlayerController>();
            if (player != null)
                OnPlayerLeftTeam?.Invoke(player, teamId);
        }
    }


    [ObserversRpc]
    private void TeamScoreUpdatedRpc(int teamId, int score)
    {
        if (_teams.ContainsKey(teamId))
        {
            _teams[teamId].score = score;
            OnTeamScoreUpdated?.Invoke(teamId, score);
        }
    }
    #endregion

    #region Public API
    public Dictionary<int, Team> GetAllTeams() => _teams;
    public bool IsTeamFull(int teamId) => GetPlayerCount(teamId) >= maxPlayersPerTeam;
    public Color GetTeamColor(int teamId) => _teams.ContainsKey(teamId) ? _teams[teamId].teamColor : Color.white;
    public string GetTeamName(int teamId) => _teams.ContainsKey(teamId) ? _teams[teamId].teamName : "Unknown";
    #endregion
}