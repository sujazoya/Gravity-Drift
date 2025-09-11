using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Scened;
using FishNet.Transporting;



public enum GameState
{
    Lobby,
    Countdown,
    InProgress,
    HalfTime,
    Overtime,
    GameEnded,
    Paused
}

public enum TeamSide
{
    Red,
    Blue,
    Neutral
}

public enum GameMode
{
    Deathmatch,
    TeamDeathmatch,
    CaptureTheFlag,
    KingOfTheHill,
    Soccer,
    Basketball
}

[System.Serializable]
public class TeamData
{
    public int teamId;
    public string teamName;
    public Color teamColor;
    public int score;
    public int kills;
    public int deaths;
    public List<NetworkConnection> players = new List<NetworkConnection>();
    public Transform[] spawnPoints;
}

[System.Serializable]
public class GameRules
{
    public int scoreLimit = 10;
    public int timeLimit = 600; // 10 minutes in seconds
    public int respawnTime = 5;
    public bool friendlyFire = false;
    public bool allowOvertime = true;
    public int maxPlayers = 16;
    public int minPlayersToStart = 2;
}

public class AdvancedGameManager : NetworkBehaviour
{
    public static AdvancedGameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private GameMode currentGameMode = GameMode.TeamDeathmatch;
    [SerializeField] private GameRules gameRules = new GameRules();
    [SerializeField] private TeamData[] teams = new TeamData[2];
    [SerializeField] private Transform neutralSpawnPoints;

    [Header("UI References")]
    [SerializeField] private GameObject gameUI;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text gameStateText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Image team1ScoreBar;
    [SerializeField] private Image team2ScoreBar;
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private Transform scoreboardContent;
    [SerializeField] private GameObject playerScorePrefab;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private GameObject flagPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip countdownSound;
    [SerializeField] private AudioClip gameStartSound;
    [SerializeField] private AudioClip goalSound;
    [SerializeField] private AudioClip victorySound;

    // Synchronized variables
    private readonly SyncVar<GameState> _currentGameState = new SyncVar<GameState>(GameState.Lobby);
    private readonly SyncVar<float> _gameTimeRemaining = new SyncVar<float>();
    private readonly SyncVar<int> _countdownTime = new SyncVar<int>(5);
    private readonly SyncVar<int> _winningTeamId = new SyncVar<int>(-1);
    private readonly SyncVar<bool> _isOvertime = new SyncVar<bool>(false);

    // Game objects
    private GameObject _gameBall;
    private Dictionary<NetworkConnection, AdvancedNetworkPlayerController> _players =
        new Dictionary<NetworkConnection, AdvancedNetworkPlayerController>();
    private Dictionary<NetworkConnection, PlayerScoreData> _playerScores =
        new Dictionary<NetworkConnection, PlayerScoreData>();

    // Coroutines
    private Coroutine _countdownCoroutine;
    private Coroutine _gameTimerCoroutine;
    private Coroutine _respawnCoroutine;

    // Events
    public static event Action<GameState> OnGameStateChanged;
    public static event Action<int, int> OnScoreChanged;
    public static event Action<NetworkConnection, int> PlayerKilledEvent;
    public static event Action<NetworkConnection> OnPlayerRespawned;

    [System.Serializable]
    public class PlayerScoreData
    {
        public NetworkConnection connection;
        public string playerName;
        public int teamId;
        public int kills;
        public int deaths;
        public int score;
        public bool isReady;
    }

    public static event Action<AdvancedNetworkPlayerController> OnPlayerJoined;
    public static event Action<int> OnPlayerLeft;
    public static event Action<int, int> OnPlayerScoreChanged;

    NetworkManager _networkManager;
    void Awake()
    {
        _networkManager = InstanceFinder.NetworkManager;

        // Subscribe to server client connect event
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        // Subscribe to FishNet connection events
        _networkManager.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
    }

    // Example: Call this when score changes somewhere in your gameplay logic
    public void ChangePlayerScore(int clientId, int newScore)
    {
        OnPlayerScoreChanged?.Invoke(clientId, newScore);
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject); // ✅ allowed here

            InitializeGameManager();
        }
        else
        {
            InstanceFinder.ServerManager.Despawn(gameObject);
        }
    }

    private void InitializeGameManager()
    {
        _gameTimeRemaining.Value = gameRules.timeLimit;

        // Initialize teams
        teams[0] = new TeamData { teamId = 1, teamName = "Red Team", teamColor = Color.red, score = 0 };
        teams[1] = new TeamData { teamId = 2, teamName = "Blue Team", teamColor = Color.blue, score = 0 };

        if (IsServerInitialized)
        {
            InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoaded;
        }

        InstanceFinder.ClientManager.OnClientConnectionState += HandleClientConnectionState;
    }
    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            Debug.Log($"Player {conn.ClientId} connected");

            if (_currentGameState.Value == GameState.Lobby)
            {
                AssignPlayerToTeam(conn);
                SpawnPlayer(conn);
                UpdateLobbyState();
            }
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"Player {conn.ClientId} disconnected");

            // Handle player disconnection logic here
        }
    }

    #region Network Event Handlers
    private void HandleServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("Server started. Waiting for players...");
        }
    }

    private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            HandlePlayerConnected(conn);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            HandlePlayerDisconnected(conn);
        }
    }
    private void HandleClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            InitializeGameUI();
        }
    }

    private void HandleSceneLoaded(SceneLoadEndEventArgs args)
    {
        if (args.QueueData.AsServer && args.LoadedScenes.Length > 0)
        {
            SetupGameLevel();
        }
    }


    #endregion

    #region Player Management
    // Add this to your AdvancedGameManager class if it doesn't exist
    [Server]
    
    private void HandlePlayerConnected(NetworkConnection conn)
    {
        Debug.Log($"Player {conn.ClientId} connected");

        if (_currentGameState.Value == GameState.Lobby)
        {
            AssignPlayerToTeam(conn);
            SpawnPlayer(conn);
            UpdateLobbyState();
        }
    }

    [Server]    
    
    private void HandlePlayerDisconnected(NetworkConnection conn)
    {
        Debug.Log($"Player {conn.ClientId} disconnected");

        if (_players.ContainsKey(conn))
        {
            AdvancedNetworkPlayerController player = _players[conn];
            if (player != null)
            {
                InstanceFinder.ServerManager.Despawn(player.NetworkObject);
            }
            _players.Remove(conn);
        }

        if (_playerScores.ContainsKey(conn))
        {
            _playerScores.Remove(conn);
        }

        // Remove from team
        foreach (TeamData team in teams)
        {
            team.players.Remove(conn);
        }

        CheckGameConditions();
    }

    [Server]
    private void AssignPlayerToTeam(NetworkConnection conn)
    {
        // Balance teams
        TeamData smallestTeam = teams.OrderBy(t => t.players.Count).First();
        smallestTeam.players.Add(conn);

        _playerScores[conn] = new PlayerScoreData
        {
            connection = conn,
            teamId = smallestTeam.teamId,
            playerName = $"Player {conn.ClientId}",
            kills = 0,
            deaths = 0,
            score = 0,
            isReady = false
        };
    }

    [Server]
    private void SpawnPlayer(NetworkConnection conn)
    {
        if (_currentGameState.Value == GameState.Lobby || _currentGameState.Value == GameState.InProgress)
        {
            TeamData team = teams.FirstOrDefault(t => t.players.Contains(conn));
            if (team != null)
            {
                Vector3 spawnPosition = GetSpawnPosition(team.teamId);
                GameObject playerObj = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
                AdvancedNetworkPlayerController player = playerObj.GetComponent<AdvancedNetworkPlayerController>();

                InstanceFinder.ServerManager.Spawn(playerObj, conn);
                _players[conn] = player;

                // Initialize player
                player.SetTeam(team.teamId);
                player.SetPlayerName($"Player {conn.ClientId}");
            }
        }
    }

    #endregion

    #region Game State Management

    [Server]
    public void StartGame()
    {
        if (_currentGameState.Value != GameState.Lobby) return;
        if (_players.Count < gameRules.minPlayersToStart) return;

        SetGameState(GameState.Countdown);
        StartCountdown();
    }

    [Server]
    public void EndGame()
    {
        SetGameState(GameState.GameEnded);
        StopAllCoroutines();

        DetermineWinner();
        ShowEndGameScreen();
    }

    [Server]
    public void RestartGame()
    {
        ResetGame();
        SetGameState(GameState.Lobby);
        UpdateLobbyState();
    }

    [Server]
    private void ResetGame()
    {
        // Reset scores
        foreach (TeamData team in teams)
        {
            team.score = 0;
            team.kills = 0;
            team.deaths = 0;
        }

        // Reset player scores
        foreach (var scoreData in _playerScores.Values)
        {
            scoreData.kills = 0;
            scoreData.deaths = 0;
            scoreData.score = 0;
            scoreData.isReady = false;
        }

        _gameTimeRemaining.Value = gameRules.timeLimit;
        _isOvertime.Value = false;
        _winningTeamId.Value = -1;

        // Despawn all players and respawn them
        foreach (var player in _players.Values.ToList())
        {
            if (player != null)
            {
                InstanceFinder.ServerManager.Despawn(player.NetworkObject);
            }
        }
        _players.Clear();

        // Respawn all connected players
        foreach (NetworkConnection conn in InstanceFinder.ServerManager.Clients.Values)
        {
            SpawnPlayer(conn);
        }

        UpdateScoreUI();
    }

    [ObserversRpc]
    private void SetGameState(GameState newState)
    {
        _currentGameState.Value = newState;
        OnGameStateChanged?.Invoke(newState);
        UpdateGameStateUI();
    }

    #endregion

    #region Game Flow

    [Server]
    private void StartCountdown()
    {
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        _countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        _countdownTime.Value = 5;

        while (_countdownTime.Value > 0)
        {
            UpdateCountdownUI(_countdownTime.Value);
            yield return new WaitForSeconds(1f);
            _countdownTime.Value--;
        }

        UpdateCountdownUI(0);
        StartGameplay();
    }

    [Server]
    private void StartGameplay()
    {
        SetGameState(GameState.InProgress);
        StartGameTimer();

        // Spawn game objects based on mode
        SpawnGameObjects();

        // Enable player movement
        foreach (var player in _players.Values)
        {
            player.SetMovementEnabled(true);
        }
    }

    [Server]
    private void StartGameTimer()
    {
        if (_gameTimerCoroutine != null) StopCoroutine(_gameTimerCoroutine);
        _gameTimerCoroutine = StartCoroutine(GameTimerRoutine());
    }

    private IEnumerator GameTimerRoutine()
    {
        while (_gameTimeRemaining.Value > 0 && _currentGameState.Value == GameState.InProgress)
        {
            _gameTimeRemaining.Value -= 1f;
            UpdateTimerUI(_gameTimeRemaining.Value);
            yield return new WaitForSeconds(1f);

            CheckGameConditions();
        }

        if (_currentGameState.Value == GameState.InProgress)
        {
            HandleGameTimeEnd();
        }
    }

    [Server]
    private void CheckGameConditions()
    {
        if (_currentGameState.Value != GameState.InProgress) return;

        // Check score limit
        foreach (TeamData team in teams)
        {
            if (team.score >= gameRules.scoreLimit)
            {
                EndGame();
                return;
            }
        }

        // Check if enough players remain
        if (_players.Count < gameRules.minPlayersToStart)
        {
            SetGameState(GameState.Paused);
        }
    }

    [Server]
    private void HandleGameTimeEnd()
    {
        if (teams[0].score == teams[1].score && gameRules.allowOvertime)
        {
            StartOvertime();
        }
        else
        {
            EndGame();
        }
    }

    [Server]
    private void StartOvertime()
    {
        SetGameState(GameState.Overtime);
        _isOvertime.Value = true;

        // Overtime rules can be customized here
        _gameTimeRemaining.Value = 120f; // 2 minutes overtime
        StartGameTimer();
    }

    [Server]
    private void DetermineWinner()
    {
        if (teams[0].score > teams[1].score)
        {
            _winningTeamId.Value = 1;
        }
        else if (teams[1].score > teams[0].score)
        {
            _winningTeamId.Value = 2;
        }
        else
        {
            _winningTeamId.Value = 0; // Draw
        }
    }

    #endregion

    #region Game Mechanics

    [Server]
    public void OnPlayerKilled(NetworkConnection victim, NetworkConnection killer)
    {
        if (!_playerScores.ContainsKey(victim) || !_playerScores.ContainsKey(killer)) return;

        _playerScores[victim].deaths++;

        if (killer != null && _playerScores.ContainsKey(killer))
        {
            _playerScores[killer].kills++;
            _playerScores[killer].score += 100;

            // Team scoring
            TeamData killerTeam = teams.FirstOrDefault(t => t.players.Contains(killer));
            if (killerTeam != null)
            {
                killerTeam.kills++;
                killerTeam.score += 1;
            }
        }

        TeamData victimTeam = teams.FirstOrDefault(t => t.players.Contains(victim));
        if (victimTeam != null)
        {
            victimTeam.deaths++;
        }

        UpdateScoreUI();
        PlayerKilledEvent?.Invoke(victim, killer != null ? _playerScores[killer].teamId : -1);

        // Start respawn timer
        if (_respawnCoroutine != null) StopCoroutine(_respawnCoroutine);
        _respawnCoroutine = StartCoroutine(RespawnPlayerRoutine(victim));
    }

    [Server]
    public void OnPlayerScored(NetworkConnection scorer, int points)
    {
        if (!_playerScores.ContainsKey(scorer)) return;

        _playerScores[scorer].score += points;

        TeamData team = teams.FirstOrDefault(t => t.players.Contains(scorer));
        if (team != null)
        {
            team.score += points;
        }

        UpdateScoreUI();
        PlayGoalSound();

        CheckGameConditions();
    }

    private IEnumerator RespawnPlayerRoutine(NetworkConnection playerConn)
    {
        yield return new WaitForSeconds(gameRules.respawnTime);

        if (_players.ContainsKey(playerConn) && _currentGameState.Value == GameState.InProgress)
        {
            TeamData team = teams.FirstOrDefault(t => t.players.Contains(playerConn));
            if (team != null)
            {
                Vector3 spawnPos = GetSpawnPosition(team.teamId);
                _players[playerConn].RespawnPlayer(spawnPos);
                OnPlayerRespawned?.Invoke(playerConn);
            }
        }
    }

    [Server]
    private void SpawnGameObjects()
    {
        switch (currentGameMode)
        {
            case GameMode.Soccer:
            case GameMode.Basketball:
                SpawnBall();
                break;
            case GameMode.CaptureTheFlag:
                SpawnFlags();
                break;
        }
    }

    [Server]
    private void SpawnBall()
    {
        if (_gameBall != null) InstanceFinder.ServerManager.Despawn(_gameBall);

        _gameBall = Instantiate(ballPrefab, Vector3.zero, Quaternion.identity);
        InstanceFinder.ServerManager.Spawn(_gameBall);
    }

    [Server]
    private void SpawnFlags()
    {
        // Implement flag spawning logic
    }

    #endregion

    #region UI Methods

    [ObserversRpc]
    private void InitializeGameUI()
    {
        if (gameUI != null) gameUI.SetActive(true);
        UpdateGameStateUI();
        UpdateScoreUI();
        UpdateTimerUI(_gameTimeRemaining.Value);
    }

    [ObserversRpc]
    private void UpdateGameStateUI()
    {
        if (gameStateText != null)
        {
            gameStateText.text = _currentGameState.ToString();
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(_currentGameState.Value == GameState.Countdown);
        }
    }

    [ObserversRpc]
    private void UpdateCountdownUI(int time)
    {
        if (countdownText != null)
        {
            countdownText.text = time > 0 ? time.ToString() : "GO!";
        }
    }

    [ObserversRpc]
    private void UpdateTimerUI(float time)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    [ObserversRpc]
    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"{teams[0].score} - {teams[1].score}";
        }

        if (team1ScoreBar != null)
        {
            team1ScoreBar.fillAmount = (float)teams[0].score / gameRules.scoreLimit;
        }

        if (team2ScoreBar != null)
        {
            team2ScoreBar.fillAmount = (float)teams[1].score / gameRules.scoreLimit;
        }

        UpdateScoreboard();
    }

    [ObserversRpc]
    private void UpdateScoreboard()
    {
        if (scoreboardContent == null) return;

        // Clear existing entries
        foreach (Transform child in scoreboardContent)
        {
            Destroy(child.gameObject);
        }

        // Create new entries
        foreach (var scoreData in _playerScores.Values.OrderByDescending(p => p.score))
        {
            GameObject entry = Instantiate(playerScorePrefab, scoreboardContent);
            PlayerScoreEntryUI entryUI = entry.GetComponent<PlayerScoreEntryUI>();

            if (entryUI != null)
            {
                entryUI.Initialize(
                    scoreData.playerName,
                    scoreData.teamId,
                    scoreData.kills,
                    scoreData.deaths,
                    scoreData.score
                );
            }
        }
    }

    [ObserversRpc]
    private void ShowEndGameScreen()
    {
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        if (winnerText != null)
        {
            if (_winningTeamId.Value == 0)
            {
                winnerText.text = "DRAW!";
            }
            else
            {
                TeamData winningTeam = teams.FirstOrDefault(t => t.teamId == _winningTeamId.Value);
                winnerText.text = $"{winningTeam?.teamName} WINS!";
            }
        }

        PlayVictorySound();
    }

    [ObserversRpc]
    private void PlayGoalSound()
    {
        // Play goal sound
    }

    [ObserversRpc]
    private void PlayVictorySound()
    {
        // Play victory sound
    }

    #endregion

    #region Utility Methods

    private Vector3 GetSpawnPosition(int teamId)
    {
        TeamData team = teams.FirstOrDefault(t => t.teamId == teamId);
        if (team != null && team.spawnPoints.Length > 0)
        {
            Transform spawnPoint = team.spawnPoints[UnityEngine.Random.Range(0, team.spawnPoints.Length)];
            return spawnPoint.position;
        }
        return Vector3.zero;
    }

    [Server]
    private void SetupGameLevel()
    {
        // Find and assign spawn points, objectives, etc.
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        foreach (GameObject spawnPoint in spawnPoints)
        {
            TeamSide side = spawnPoint.GetComponent<SpawnPoint>()?.teamSide ?? TeamSide.Neutral;
            // Assign spawn points to teams
        }
    }

    [Server]
    private void UpdateLobbyState()
    {
        // Check if all players are ready to start
        bool allReady = _playerScores.Values.All(p => p.isReady);
        int playerCount = _players.Count;

        // Update lobby UI
        UpdateLobbyUI(playerCount, allReady);
    }

    [ObserversRpc]
    private void UpdateLobbyUI(int playerCount, bool allReady)
    {
        // Update lobby UI elements
    }

    #endregion

    #region Public API

    [Server]
    public void SetPlayerReady(NetworkConnection conn, bool ready)
    {
        if (_playerScores.ContainsKey(conn))
        {
            _playerScores[conn].isReady = ready;
            UpdateLobbyState();

            if (ready && AllPlayersReady())
            {
                StartGame();
            }
        }
    }

    [Server]
    public bool AllPlayersReady()
    {
        return _playerScores.Values.All(p => p.isReady) &&
               _playerScores.Count >= gameRules.minPlayersToStart;
    }

    [Server]
    public TeamData GetTeam(int teamId)
    {
        return teams.FirstOrDefault(t => t.teamId == teamId);
    }

    [Server]
    public PlayerScoreData GetPlayerStats(NetworkConnection conn)
    {
        return _playerScores.ContainsKey(conn) ? _playerScores[conn] : null;
    }

    [Server]
    public void ChangeGameMode(GameMode newMode)
    {
        currentGameMode = newMode;
        // Additional mode-specific setup
    }

    [Server]
    public void PauseGame()
    {
        if (_currentGameState.Value == GameState.InProgress)
        {
            SetGameState(GameState.Paused);
            Time.timeScale = 0f;
        }
    }

    [Server]
    public void ResumeGame()
    {
        if (_currentGameState.Value == GameState.Paused)
        {
            SetGameState(GameState.InProgress);
            Time.timeScale = 1f;
        }
    }

    #endregion

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        if (IsServerInitialized)
        {
            InstanceFinder.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            InstanceFinder.SceneManager.OnLoadEnd -= HandleSceneLoaded;
        }

        InstanceFinder.ClientManager.OnClientConnectionState -= HandleClientConnectionState;

        StopAllCoroutines();
        Time.timeScale = 1f;
    }
    #region Goal Scoring System

[Server]
public void ScoreGoal(int scoringTeamId, int scoredOnTeamId, int points = 1)
{
    if (_currentGameState.Value != GameState.InProgress && _currentGameState.Value != GameState.Overtime)
        return;

    // Validate team IDs
    if (scoringTeamId < 1 || scoringTeamId > teams.Length || scoredOnTeamId < 1 || scoredOnTeamId > teams.Length)
        return;

    // Find the scoring team
    TeamData scoringTeam = teams.FirstOrDefault(t => t.teamId == scoringTeamId);
    TeamData scoredOnTeam = teams.FirstOrDefault(t => t.teamId == scoredOnTeamId);

    if (scoringTeam == null || scoredOnTeam == null)
        return;

    // Add points to scoring team
    scoringTeam.score += points;
    
    // Update UI
    UpdateScoreUI();
    
    // Play goal effects
    PlayGoalSound();
    
    // Broadcast goal event
    RpcBroadcastGoal(scoringTeamId, scoredOnTeamId, scoringTeam.score);
    
    // Check if this goal ends the game
    CheckGameConditions();
}

[ObserversRpc]
private void RpcBroadcastGoal(int scoringTeamId, int scoredOnTeamId, int newScore)
{
    // You can add visual/audio effects here that all clients should see
    Debug.Log($"Team {scoringTeamId} scored on Team {scoredOnTeamId}! New score: {newScore}");
    
    // Optional: Add camera shake or other effects
    if (CameraShakeManager.Instance != null)
    {
        CameraShakeManager.Instance.ShakeAllCameras(0.5f, 0.7f);
    }
}

[Server]
public void ScoreGoalWithScorer(int scoringTeamId, int scoredOnTeamId, NetworkConnection scorerConnection, int points = 1)
{
    if (!_playerScores.ContainsKey(scorerConnection))
        return;

    // Add team score
    ScoreGoal(scoringTeamId, scoredOnTeamId, points);
    
    // Add individual player score
    _playerScores[scorerConnection].score += points * 100; // Example: 100 points per goal
    
    // Update UI
    UpdateScoreUI();
}

[Server]
public void ResetScores()
{
    foreach (TeamData team in teams)
    {
        team.score = 0;
    }
    
    foreach (var playerScore in _playerScores.Values)
    {
        playerScore.score = 0;
    }
    
    UpdateScoreUI();
}

#endregion
}

// UI component for scoreboard entries
        public class PlayerScoreEntryUI : MonoBehaviour
        {
            [SerializeField] private TMP_Text playerNameText;
            [SerializeField] private TMP_Text killsText;
            [SerializeField] private TMP_Text deathsText;
            [SerializeField] private TMP_Text scoreText;
            [SerializeField] private Image teamIndicator;

            public void Initialize(string name, int teamId, int kills, int deaths, int score)
            {
                playerNameText.text = name;
                killsText.text = kills.ToString();
                deathsText.text = deaths.ToString();
                scoreText.text = score.ToString();
                
                teamIndicator.color = teamId == 1 ? Color.red : Color.blue;
            }
        }


// Spawn point component
public class SpawnPoint : MonoBehaviour
{
    public TeamSide teamSide = TeamSide.Neutral;
    public bool isSafeZone = true;
}
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OpenWhisperChat(string playerName)
    {
        Debug.Log($"Opening whisper chat with {playerName}");
        // Implement whisper chat functionality
    }
}

public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool ToggleMutePlayer(int clientId)
    {
        Debug.Log($"Toggling mute for player {clientId}");
        // Implement mute functionality
        return false; // Return current mute state
    }
}