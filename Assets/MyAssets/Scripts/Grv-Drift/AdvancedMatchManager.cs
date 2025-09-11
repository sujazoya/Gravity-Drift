using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;
/*
Usage Examples:

csharp
// Start a match with specific game mode
AdvancedMatchManager.Instance.StartMatch(GameMode.TeamDeathmatch);

// Get current game mode
string mode = AdvancedMatchManager.Instance.GetGameMode();
GameMode enumMode = AdvancedMatchManager.Instance.GetGameModeEnum();

// Change game mode
AdvancedMatchManager.Instance.ChangeGameMode(GameMode.CaptureTheFlag);

// Get match time
float timeLeft = AdvancedMatchManager.Instance.GetMatchTime();

// Check if match is active
bool isActive = AdvancedMatchManager.Instance.IsMatchActive();

*/
public class AdvancedMatchManager : NetworkBehaviour
{
    public static AdvancedMatchManager Instance { get; private set; }

    public enum GameMode
    {
        Soccer,
        Deathmatch,
        TeamDeathmatch,
        CaptureTheFlag,
        KingOfTheHill,
        FreeForAll,
        Basketball,
        Hockey
    }

    [System.Serializable]
    public class GameModeSettings
    {
        [Header("Soccer Settings")]
        public int soccerGoalsToWin = 5;
        public float soccerGoalResetTime = 3f;

        [Header("Deathmatch Settings")]
        public int deathmatchKillsToWin = 20;
        public int deathmatchTimeLimit = 600;

        [Header("Team Deathmatch Settings")]
        public int teamDeathmatchKillsToWin = 30;
        public int teamDeathmatchTimeLimit = 600;

        [Header("Capture The Flag Settings")]
        public int ctfFlagsToWin = 3;
        public float ctfFlagReturnTime = 10f;

        [Header("King of the Hill Settings")]
        public float kothTimeToWin = 180f;
        public float kothHillCaptureTime = 5f;

        [Header("Basketball Settings")]
        public int basketballPointsToWin = 21;
        public float basketballResetTime = 2f;

        [Header("Hockey Settings")]
        public int hockeyGoalsToWin = 7;
        public float hockeyPuckResetTime = 2f;
    }

    [System.Serializable]
    public class MatchSettings
    {
        [Header("Time Settings")]
        public int defaultMatchDuration = 300; // 5 minutes
        public int overtimeDuration = 120; // 2 minutes
        public bool suddenDeath = true;
        public float respawnTime = 2f;

        [Header("Score Settings")]
        public int maxScoreDifference = 10; // Mercy rule
        public bool enableMercyRule = true;

        [Header("Game Rules")]
        public bool friendlyFire = false;
        public bool selfDestruct = true;
        public float selfDestructTime = 10f;
        public int maxPlayersPerTeam = 3;

        [Header("Game Mode")]
        public GameMode defaultGameMode = GameMode.Soccer;
        public GameModeSettings modeSettings = new GameModeSettings();
    }

    [System.Serializable]
    public class MatchState
    {
        public bool isMatchActive = false;
        public bool isOvertime = false;
        public bool isPaused = false;
        public bool matchEnded = false;
        public float matchTimeRemaining = 0f;
        public int blueScore = 0;
        public int orangeScore = 0;
        public int winningTeam = 0;
        public GameMode currentGameMode = GameMode.Soccer;
        public Dictionary<int, int> playerKills = new Dictionary<int, int>();
        public Dictionary<int, int> playerDeaths = new Dictionary<int, int>();
    }

    public MatchSettings settings = new MatchSettings();
    public MatchState state = new MatchState();

    [Header("References")]
    public AdvancedBall gameBall;
    public Transform ballSpawnPoint;
    public Transform[] blueSpawnPoints;
    public Transform[] orangeSpawnPoints;
    public TMP_Text matchTimerText;
    public TMP_Text scoreText;
    public TMP_Text gameModeText;

    private Dictionary<int, List<AdvancedNetworkPlayerController>> _teamPlayers = new Dictionary<int, List<AdvancedNetworkPlayerController>>();
    private Coroutine _matchTimerCoroutine;
    private Coroutine _goalResetCoroutine;
    private Coroutine _gameModeSpecificCoroutine;

    public event System.Action OnMatchStart;
    public event System.Action OnMatchEnd;
    public event System.Action OnGoalScored;
    public event System.Action OnOvertimeStart;
    public event System.Action<int> OnTeamScore; // teamId
    public event System.Action<GameMode> OnGameModeChanged;

    #region Initialization
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        if (Instance == null)
            Instance = this;
        
        InitializeTeams();
        InitializeGameMode();
    }

    private void InitializeTeams()
    {
        _teamPlayers.Clear();
        _teamPlayers.Add(1, new List<AdvancedNetworkPlayerController>()); // Blue team
        _teamPlayers.Add(2, new List<AdvancedNetworkPlayerController>()); // Orange team
    }

    private void InitializeGameMode()
    {
        state.currentGameMode = settings.defaultGameMode;
        UpdateGameModeUI();
    }
    #endregion

    #region Match Control
    [Server]
    public void StartMatch(GameMode gameMode = GameMode.Soccer)
    {
        if (state.isMatchActive) return;

        state.isMatchActive = true;
        state.matchEnded = false;
        state.currentGameMode = gameMode;
        state.matchTimeRemaining = GetModeDuration(gameMode);
        state.blueScore = 0;
        state.orangeScore = 0;
        state.winningTeam = 0;
        state.playerKills.Clear();
        state.playerDeaths.Clear();

        ResetBall();
        RespawnAllPlayers();
        UpdateMatchUI();

        OnMatchStart?.Invoke();
        OnGameModeChanged?.Invoke(gameMode);
        StartMatchRpc(gameMode);

        if (IsServerInitialized)
        {
            _matchTimerCoroutine = StartCoroutine(MatchTimerCoroutine());
            StartGameModeSpecificLogic(gameMode);
        }
    }

    [ObserversRpc]
    private void StartMatchRpc(GameMode gameMode)
    {
        if (!IsServerInitialized)
        {
            state.isMatchActive = true;
            state.currentGameMode = gameMode;
            OnMatchStart?.Invoke();
            OnGameModeChanged?.Invoke(gameMode);
            UpdateGameModeUI();
        }
    }

    [Server]
    public void EndMatch()
    {
        if (!state.isMatchActive) return;

        state.isMatchActive = false;
        state.matchEnded = true;
        DetermineWinner();

        OnMatchEnd?.Invoke();
        EndMatchRpc(state.winningTeam);

        StopAllCoroutines();
    }

    [ObserversRpc]
    private void EndMatchRpc(int winningTeam)
    {
        state.isMatchActive = false;
        state.matchEnded = true;
        state.winningTeam = winningTeam;
        OnMatchEnd?.Invoke();
    }

    [Server]
    public void PauseMatch()
    {
        state.isPaused = true;
        PauseMatchRpc();
    }

    [Server]
    public void ResumeMatch()
    {
        state.isPaused = false;
        ResumeMatchRpc();
    }

    [ObserversRpc]
    private void PauseMatchRpc()
    {
        state.isPaused = true;
        Time.timeScale = 0f;
    }

    [ObserversRpc]
    private void ResumeMatchRpc()
    {
        state.isPaused = false;
        Time.timeScale = 1f;
    }

    [Server]
    public void ChangeGameMode(GameMode newMode)
    {
        if (state.isMatchActive)
        {
            EndMatch();
        }

        state.currentGameMode = newMode;
        OnGameModeChanged?.Invoke(newMode);
        ChangeGameModeRpc(newMode);
    }

    [ObserversRpc]
    private void ChangeGameModeRpc(GameMode newMode)
    {
        state.currentGameMode = newMode;
        OnGameModeChanged?.Invoke(newMode);
        UpdateGameModeUI();
    }
    #endregion

    #region Game Mode Specific Logic
    private void StartGameModeSpecificLogic(GameMode gameMode)
    {
        if (_gameModeSpecificCoroutine != null)
            StopCoroutine(_gameModeSpecificCoroutine);

        switch (gameMode)
        {
            case GameMode.KingOfTheHill:
                _gameModeSpecificCoroutine = StartCoroutine(KingOfTheHillLogic());
                break;
            case GameMode.CaptureTheFlag:
                _gameModeSpecificCoroutine = StartCoroutine(CaptureTheFlagLogic());
                break;
        }
    }

    private IEnumerator KingOfTheHillLogic()
    {
        while (state.isMatchActive)
        {
            // Implement KOTH logic here
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator CaptureTheFlagLogic()
    {
        while (state.isMatchActive)
        {
            // Implement CTF logic here
            yield return new WaitForSeconds(1f);
        }
    }

    private int GetModeDuration(GameMode mode)
    {
        return mode switch
        {
            GameMode.Deathmatch => settings.modeSettings.deathmatchTimeLimit,
            GameMode.TeamDeathmatch => settings.modeSettings.teamDeathmatchTimeLimit,
            _ => settings.defaultMatchDuration
        };
    }

    private int GetWinScore(GameMode mode)
    {
        return mode switch
        {
            GameMode.Soccer => settings.modeSettings.soccerGoalsToWin,
            GameMode.Deathmatch => settings.modeSettings.deathmatchKillsToWin,
            GameMode.TeamDeathmatch => settings.modeSettings.teamDeathmatchKillsToWin,
            GameMode.CaptureTheFlag => settings.modeSettings.ctfFlagsToWin,
            GameMode.Basketball => settings.modeSettings.basketballPointsToWin,
            GameMode.Hockey => settings.modeSettings.hockeyGoalsToWin,
            _ => 5
        };
    }

    private float GetResetTime(GameMode mode)
    {
        return mode switch
        {
            GameMode.Soccer => settings.modeSettings.soccerGoalResetTime,
            GameMode.Basketball => settings.modeSettings.basketballResetTime,
            GameMode.Hockey => settings.modeSettings.hockeyPuckResetTime,
            _ => 3f
        };
    }
    #endregion

    #region Match Logic
    private IEnumerator MatchTimerCoroutine()
    {
        while (state.matchTimeRemaining > 0 && state.isMatchActive)
        {
            if (!state.isPaused)
            {
                state.matchTimeRemaining -= Time.deltaTime;
                UpdateMatchTimerUI();
            }
            yield return null;
        }

        if (state.isMatchActive)
        {
            CheckOvertime();
        }
    }

    private void CheckOvertime()
    {
        if (ShouldGoToOvertime() && settings.suddenDeath)
        {
            StartOvertime();
        }
        else
        {
            EndMatch();
        }
    }

    private bool ShouldGoToOvertime()
    {
        return state.currentGameMode switch
        {
            GameMode.Soccer => state.blueScore == state.orangeScore,
            GameMode.TeamDeathmatch => state.blueScore == state.orangeScore,
            GameMode.Basketball => state.blueScore == state.orangeScore,
            GameMode.Hockey => state.blueScore == state.orangeScore,
            _ => false
        };
    }

    [Server]
    private void StartOvertime()
    {
        state.isOvertime = true;
        state.matchTimeRemaining = settings.overtimeDuration;
        
        OnOvertimeStart?.Invoke();
        StartOvertimeRpc();

        _matchTimerCoroutine = StartCoroutine(OvertimeTimerCoroutine());
    }

    [ObserversRpc]
    private void StartOvertimeRpc()
    {
        state.isOvertime = true;
        OnOvertimeStart?.Invoke();
    }

    private IEnumerator OvertimeTimerCoroutine()
    {
        while (state.matchTimeRemaining > 0 && state.isOvertime)
        {
            if (!state.isPaused)
            {
                state.matchTimeRemaining -= Time.deltaTime;
                UpdateMatchTimerUI();
            }
            yield return null;
        }

        if (state.isOvertime)
        {
            EndMatch();
        }
    }

    [Server]
    public void ScoreGoal(int scoringTeam, int scoredOnTeam, int scorerPlayerId)
    {
        if (!state.isMatchActive) return;

        if (scoringTeam == 1)
            state.blueScore++;
        else
            state.orangeScore++;

        // Update player stats
        AdvancedNetworkPlayerController scorer = GetPlayerById(scorerPlayerId);
        if (scorer != null)
        {
            scorer.AddGoal();
        }

        // Check for mercy rule
        if (settings.enableMercyRule && Mathf.Abs(state.blueScore - state.orangeScore) >= settings.maxScoreDifference)
        {
            EndMatch();
            return;
        }

        OnGoalScored?.Invoke();
        OnTeamScore?.Invoke(scoringTeam);
        UpdateScoreUI();

        ScoreGoalRpc(scoringTeam, scoredOnTeam, scorerPlayerId);

        // Reset for next round
        if (_goalResetCoroutine != null)
            StopCoroutine(_goalResetCoroutine);
        
        _goalResetCoroutine = StartCoroutine(GoalResetCoroutine());
    }

    [ObserversRpc]
    private void ScoreGoalRpc(int scoringTeam, int scoredOnTeam, int scorerPlayerId)
    {
        if (scoringTeam == 1)
            state.blueScore++;
        else
            state.orangeScore++;

        OnGoalScored?.Invoke();
        OnTeamScore?.Invoke(scoringTeam);
        UpdateScoreUI();
    }

    private IEnumerator GoalResetCoroutine()
    {
        yield return new WaitForSeconds(GetResetTime(state.currentGameMode));
        
        ResetBall();
        RespawnAllPlayers();
        
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        int winScore = GetWinScore(state.currentGameMode);
        
        if (state.blueScore >= winScore || state.orangeScore >= winScore)
        {
            EndMatch();
        }
    }

    private void DetermineWinner()
    {
        if (state.blueScore > state.orangeScore)
            state.winningTeam = 1;
        else if (state.orangeScore > state.blueScore)
            state.winningTeam = 2;
        else
            state.winningTeam = 0; // Draw
    }
    #endregion

    #region Player Management
    [Server]
    public void RegisterPlayer(AdvancedNetworkPlayerController player, int teamId)
    {
        if (_teamPlayers.ContainsKey(teamId) && _teamPlayers[teamId].Count < settings.maxPlayersPerTeam)
        {
            _teamPlayers[teamId].Add(player);
            player.SetTeam(teamId);
            
            // Initialize player stats
            state.playerKills[player.Owner.ClientId] = 0;
            state.playerDeaths[player.Owner.ClientId] = 0;
        }
    }

    [Server]
    public void UnregisterPlayer(AdvancedNetworkPlayerController player)
    {
        foreach (var team in _teamPlayers.Values)
        {
            team.Remove(player);
        }
        
        // Remove player stats
        state.playerKills.Remove(player.Owner.ClientId);
        state.playerDeaths.Remove(player.Owner.ClientId);
    }

    [Server]
    public void RespawnAllPlayers()
    {
        foreach (var team in _teamPlayers)
        {
            for (int i = 0; i < team.Value.Count; i++)
            {
                if (team.Value[i] != null)
                {
                    RespawnPlayer(team.Value[i]);
                }
            }
        }
    }

    [Server]
    public void RespawnPlayer(AdvancedNetworkPlayerController player)
    {
        Transform spawnPoint = GetSpawnPoint(player.TeamId.Value);
        if (spawnPoint != null)
        {
            player.Teleport(spawnPoint.position);
            player.Respawn(spawnPoint.position);
        }
    }

    private Transform GetSpawnPoint(int teamId)
    {
        Transform[] spawnPoints = teamId == 1 ? blueSpawnPoints : orangeSpawnPoints;
        if (spawnPoints.Length > 0)
        {
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }
        return null;
    }

    private AdvancedNetworkPlayerController GetPlayerById(int playerId)
    {
        foreach (var team in _teamPlayers.Values)
        {
            foreach (var player in team)
            {
                if (player.Owner.ClientId == playerId)
                    return player;
            }
        }
        return null;
    }

    [Server]
    public void RecordKill(int killerPlayerId, int victimPlayerId)
    {
        if (state.playerKills.ContainsKey(killerPlayerId))
            state.playerKills[killerPlayerId]++;
        
        if (state.playerDeaths.ContainsKey(victimPlayerId))
            state.playerDeaths[victimPlayerId]++;
    }
    #endregion

    #region Ball Management
    [Server]
    public void ResetBall()
    {
        if (gameBall != null)
        {
            gameBall.Teleport(ballSpawnPoint.position);
            gameBall.ResetBall();
        }
    }

    [Server]
    public void EnableBall() => gameBall?.EnablePhysics(true);
    [Server]
    public void DisableBall() => gameBall?.EnablePhysics(false);
    #endregion

    #region UI Updates
    private void UpdateMatchUI()
    {
        UpdateScoreUI();
        UpdateMatchTimerUI();
        UpdateGameModeUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"<color=blue>{state.blueScore}</color> - <color=orange>{state.orangeScore}</color>";
        }
    }

    private void UpdateMatchTimerUI()
    {
        if (matchTimerText != null)
        {
            int minutes = Mathf.FloorToInt(state.matchTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(state.matchTimeRemaining % 60);
            string timeColor = state.isOvertime ? "red" : "white";
            matchTimerText.text = $"<color={timeColor}>{minutes:00}:{seconds:00}</color>";
        }
    }

    private void UpdateGameModeUI()
    {
        if (gameModeText != null)
        {
            gameModeText.text = GetGameModeDisplayName(state.currentGameMode);
        }
    }

    private string GetGameModeDisplayName(GameMode mode)
    {
        return mode switch
        {
            GameMode.Soccer => "SOCCER",
            GameMode.Deathmatch => "DEATHMATCH",
            GameMode.TeamDeathmatch => "TEAM DEATHMATCH",
            GameMode.CaptureTheFlag => "CAPTURE THE FLAG",
            GameMode.KingOfTheHill => "KING OF THE HILL",
            GameMode.FreeForAll => "FREE FOR ALL",
            GameMode.Basketball => "BASKETBALL",
            GameMode.Hockey => "HOCKEY",
            _ => "UNKNOWN"
        };
    }
    #endregion

    #region Public API
    public float GetMatchTime() => state.matchTimeRemaining;
    public string GetGameMode() => state.currentGameMode.ToString();
    public GameMode GetGameModeEnum() => state.currentGameMode;

    public bool CanJoinTeam(int teamId)
    {
        return _teamPlayers.ContainsKey(teamId) && _teamPlayers[teamId].Count < settings.maxPlayersPerTeam;
    }

    public int GetTeamScore(int teamId) => teamId == 1 ? state.blueScore : state.orangeScore;
    public bool IsMatchActive => state.isMatchActive;
    public bool IsOvertime => state.isOvertime;
    public int WinningTeam => state.winningTeam;

    public int GetPlayerKills(int playerId) => state.playerKills.ContainsKey(playerId) ? state.playerKills[playerId] : 0;
    public int GetPlayerDeaths(int playerId) => state.playerDeaths.ContainsKey(playerId) ? state.playerDeaths[playerId] : 0;
    public float GetPlayerKDRatio(int playerId) => GetPlayerDeaths(playerId) > 0 ? (float)GetPlayerKills(playerId) / GetPlayerDeaths(playerId) : GetPlayerKills(playerId);

    public string GetFormattedMatchTime()
    {
        int minutes = Mathf.FloorToInt(state.matchTimeRemaining / 60);
        int seconds = Mathf.FloorToInt(state.matchTimeRemaining % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    public bool IsMatchInProgress() => state.isMatchActive && !state.matchEnded;
    public int GetLeadingTeam() => state.blueScore > state.orangeScore ? 1 : state.orangeScore > state.blueScore ? 2 : 0;
    public int GetScoreDifference() => Mathf.Abs(state.blueScore - state.orangeScore);
    public float GetMatchProgress() => 1f - (state.matchTimeRemaining / GetModeDuration(state.currentGameMode));
    #endregion

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}