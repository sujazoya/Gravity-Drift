using FishNet.Object;
using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using System.Collections;

namespace zoya.game
{




    public class AdvancedPlayerStats : NetworkBehaviour
    {
        [System.Serializable]
        public class PlayerStatistics
        {
            public int goals = 0;
            public int assists = 0;
            public int saves = 0;
            public int shots = 0;
            public int passes = 0;
            public int steals = 0;
            public int deaths = 0;
            public int suicides = 0;
            public float timeWithBall = 0f;
            public float distanceTraveled = 0f;
            public int gravityWellsUsed = 0;
            public int boostsUsed = 0;
        }

        [System.Serializable]
        public class MatchAwards
        {
            public bool mvp = false;
            public bool topScorer = false;
            public bool mostAssists = false;
            public bool mostSaves = false;
            public bool playmaker = false;
        }

        public PlayerStatistics stats = new PlayerStatistics();
        public MatchAwards awards = new MatchAwards();

        [Header("Session Stats")]
        public int totalGoals = 0;
        public int totalAssists = 0;
        public int totalSaves = 0;
        public int matchesPlayed = 0;
        public int matchesWon = 0;

        private AdvancedNetworkPlayerController _playerController;
        private Vector3 _lastPosition;
        private float _ballPossessionTimer = 0f;
        private bool _hasBallPossession = false;

        public event System.Action OnStatsUpdated;
        public event System.Action OnAwardEarned;

        #region Death & Respawn System
        public event System.Action<NetworkConnection> OnPlayerDeath;
        public event System.Action<NetworkConnection> OnPlayerRespawn;

        private NetworkConnection _lastDamager;
        private float _deathTime;
        private bool _isDead = false;

        public bool IsDead => _isDead;


        #region Scoreboard Properties

        public int TeamID => _playerController != null ? _playerController.TeamId.Value : 0;
        public int Kills => stats.goals; // Assuming goals are kills in your game mode
        public int Deaths => stats.deaths;
        public int Assists => stats.assists;
        public int Score => (int)GetScore();

        // If you have separate kill tracking, add this:
        private int _kills = 0;
        public int KillCount
        {
            get => _kills;
            set => _kills = value;
        }

        // Add these properties for network synchronization if needed
        public readonly SyncVar<int> NetworkKills = new SyncVar<int>();
        public readonly SyncVar<int> NetworkDeaths = new SyncVar<int>();
        public readonly SyncVar<int> NetworkAssists = new SyncVar<int>();
        public readonly SyncVar<int> NetworkScore = new SyncVar<int>();

        #endregion

        #region Initialization
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _playerController = GetComponent<AdvancedNetworkPlayerController>();
            _lastPosition = transform.position;

            // Initialize health values on server
            if (IsServerInitialized)
            {
                CurrentHealth.Value = 100f;
                MaxHealth.Value = 100f;
                CurrentHealth.Value = 100f;
                MaxHealth.Value = 100f;
                NetworkKills.Value = 0;
                NetworkDeaths.Value = 0;
                NetworkAssists.Value = 0;
                NetworkScore.Value = 0;
            }

            // Subscribe to SyncVar changes
            CurrentHealth.OnChange += HandleHealthChanged;
            MaxHealth.OnChange += HandleHealthChanged;
            CurrentHealth.OnChange += HandleHealthChanged;
            MaxHealth.OnChange += HandleHealthChanged;
            NetworkKills.OnChange += HandleKillsChanged;
            NetworkDeaths.OnChange += HandleDeathsChanged;
            NetworkAssists.OnChange += HandleAssistsChanged;
            NetworkScore.OnChange += HandleScoreChanged;
        }
        private void HandleKillsChanged(int prev, int next, bool asServer)
        {
            // Update UI or trigger events when kills change
        }

        private void HandleDeathsChanged(int prev, int next, bool asServer)
        {
            // Update UI or trigger events when deaths change
        }

        private void HandleAssistsChanged(int prev, int next, bool asServer)
        {
            // Update UI or trigger events when assists change
        }

        private void HandleScoreChanged(int prev, int next, bool asServer)
        {
            // Update UI or trigger events when score changes
        }

        [Server]
        public void AddKill(int amount = 1)
        {
            _kills += amount;
            NetworkKills.Value = _kills;
            NetworkScore.Value += 100; // Example: 100 points per kill
            stats.goals += amount; // Also add to goals if they're the same
        }

        [Server]
        public void AddDeath(int amount = 1)
        {
            stats.deaths += amount;
            NetworkDeaths.Value = stats.deaths;
            NetworkScore.Value -= 50; // Example: -50 points per death
        }

        [Server]
        public void AddAssist(int amount = 1)
        {
            stats.assists += amount;
            NetworkAssists.Value = stats.assists;
            NetworkScore.Value += 50; // Example: 50 points per assist
        }

        [Server]
        public void AddScore(int points, string reason = "")
        {
            NetworkScore.Value += points;
        }

        [Server]
        public void SetScore(int score)
        {
            NetworkScore.Value = score;
        }

        #endregion

        #region Player Identification
        public string PlayerName => _playerController != null ? _playerController.PlayerName.Value : "Unknown";
        public int ClientId => _playerController != null && _playerController.Owner != null ? _playerController.Owner.ClientId : -1;
        public NetworkConnection OwnerConnection => _playerController != null ? _playerController.Owner : null;

        public bool IsOnline => _playerController != null && _playerController.Owner.IsActive;
        public float Ping => GetPlayerPing();

        private float GetPlayerPing()
        {
            if (_playerController == null || _playerController.Owner == null) return 0f;

            // Implement ping retrieval based on your network system
            // This is a placeholder - you'll need to implement actual ping measurement
            return Random.Range(20f, 150f);
        }
        #endregion

        #region Team Properties
        public Color TeamColor => GetTeamColor(TeamID);

        private Color GetTeamColor(int teamId)
        {
            return teamId switch
            {
                1 => Color.blue,
                2 => Color.red,
                3 => Color.green,
                4 => Color.yellow,
                _ => Color.gray
            };
        }

        public string TeamName => GetTeamName(TeamID);

        private string GetTeamName(int teamId)
        {
            return teamId switch
            {
                1 => "Blue Team",
                2 => "Red Team",
                3 => "Green Team",
                4 => "Yellow Team",
                _ => "Neutral"
            };
        }
        #endregion

        #region Utility Methods for Scoreboard
        public string GetFormattedKDR()
        {
            if (Deaths == 0) return Kills.ToString();
            return $"{(float)Kills / Deaths:0.0}";
        }

        public float GetKDRatio()
        {
            if (Deaths == 0) return Kills;
            return (float)Kills / Deaths;
        }

        public string GetPerformanceGrade()
        {
            float kd = GetKDRatio();

            if (kd >= 3.0f) return "S";
            if (kd >= 2.0f) return "A";
            if (kd >= 1.5f) return "B";
            if (kd >= 1.0f) return "C";
            if (kd >= 0.5f) return "D";
            return "F";
        }

        public bool IsMVP()
        {
            return awards.mvp;
        }

        public bool IsTopScorer()
        {
            return awards.topScorer;
        }
        #endregion

        private void Update()
        {
            if (IsOwner)
            {
                UpdateDistanceTraveled();
                UpdateBallPossessionTime();
            }
        }
        #endregion

        #region Health Properties with SyncVars
        public readonly SyncVar<float> CurrentHealth = new SyncVar<float>();
        public readonly SyncVar<float> MaxHealth = new SyncVar<float>();

        public float HealthPercent => MaxHealth.Value > 0 ? CurrentHealth.Value / MaxHealth.Value : 0f;
        public bool IsAlive => CurrentHealth.Value > 0f && !_isDead;

        public event System.Action<float, float> OnHealthChanged;

        private void HandleHealthChanged(float prev, float next, bool asServer)
        {
            OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealth.Value);

            // Check if player died from health change
            if (IsServerInitialized && next <= 0f && prev > 0f)
            {
                OnDeath(_lastDamager);
            }
        }

        [Server]
        public void ApplyDamage(float damage, NetworkConnection damager = null)
        {
            if (!IsAlive || _isDead) return;

            CurrentHealth.Value = Mathf.Max(0f, CurrentHealth.Value - damage);
            _lastDamager = damager;

            if (CurrentHealth.Value <= 0f)
            {
                OnDeath(damager);
            }
        }

        [Server]
        public void ApplyHeal(float healAmount, NetworkConnection healer = null)
        {
            if (!IsAlive || _isDead) return;

            CurrentHealth.Value = Mathf.Min(MaxHealth.Value, CurrentHealth.Value + healAmount);
        }

        [Server]
        public void SetHealth(float health, float maxHealth)
        {
            MaxHealth.Value = Mathf.Max(1f, maxHealth);
            CurrentHealth.Value = Mathf.Clamp(health, 0f, MaxHealth.Value);

            // If setting health above 0 and player was dead, respawn them
            if (health > 0f && _isDead)
            {
                Respawn();
            }
        }
        #endregion

        #region Death & Respawn Implementation
        [Server]
        private void OnDeath(NetworkConnection killer = null)
        {
            if (_isDead) return;

            _isDead = true;
            _deathTime = Time.time;
            _lastDamager = killer;

            stats.deaths++;
            OnStatsUpdated?.Invoke();

            // Notify about the death
            OnPlayerDeath?.Invoke(killer);
            RpcOnDeath(killer?.ClientId ?? -1);

            Debug.Log($"Player died! Killer: {(killer != null ? killer.ClientId.ToString() : "Unknown")}");

            // Start respawn timer
            StartCoroutine(RespawnAfterDelay(3f));
        }

        [ObserversRpc]
        private void RpcOnDeath(int killerClientId)
        {
            _isDead = true;

            // Play death effects on all clients
            PlayDeathEffects();

            // Disable player controller on death
            if (_playerController != null)
            {
                _playerController.SetMovementEnabled(false);
                _playerController.enabled = false;
            }
        }

        [Server]
        private IEnumerator RespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Respawn();
        }

        [Server]
        public void Respawn()
        {
            if (!_isDead) return;

            CurrentHealth.Value = MaxHealth.Value;
            _isDead = false;

            // Notify about respawn
            OnPlayerRespawn?.Invoke(_lastDamager);
            RpcOnRespawn();

            Debug.Log("Player respawned!");

            // Reset last damager
            _lastDamager = null;
        }

        [ObserversRpc]
        private void RpcOnRespawn()
        {
            _isDead = false;

            // Play respawn effects
            PlayRespawnEffects();

            // Re-enable player controller
            if (_playerController != null)
            {
                _playerController.enabled = true;
                _playerController.SetMovementEnabled(true);
            }
        }

        [Server]
        public void ForceRespawn()
        {
            if (_isDead)
            {
                Respawn();
            }
            else
            {
                // If not dead, just restore health
                CurrentHealth.Value = MaxHealth.Value;
            }
        }

        private void PlayDeathEffects()
        {
            // Add visual/audio effects for death
            if (base.IsOwner)
            {
                Debug.Log("You died!");
                // Play first-person death effects (screen effects, sounds, etc.)
            }
        }

        private void PlayRespawnEffects()
        {
            // Add visual/audio effects for respawn
            if (base.IsOwner)
            {
                Debug.Log("You respawned!");
                // Play first-person respawn effects
            }
        }

        [Server]
        public NetworkConnection GetLastDamager()
        {
            return _lastDamager;
        }

        [Server]
        public float GetTimeSinceDeath()
        {
            return _isDead ? Time.time - _deathTime : 0f;
        }
        #endregion

        #region Stat Tracking
        private void UpdateDistanceTraveled()
        {
            float distance = Vector3.Distance(transform.position, _lastPosition);
            stats.distanceTraveled += distance;
            _lastPosition = transform.position;
        }

        private void UpdateBallPossessionTime()
        {
            if (_hasBallPossession)
            {
                _ballPossessionTimer += Time.deltaTime;
                stats.timeWithBall = _ballPossessionTimer;
            }
        }

        public void SetBallPossession(bool hasPossession)
        {
            _hasBallPossession = hasPossession;
        }
        #endregion

        #region Stat Modification
        [Server]
        public void AddGoal()
        {
            stats.goals++;
            totalGoals++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.goals, totalGoals);
        }

        [Server]
        public void AddAssist()
        {
            stats.assists++;
            totalAssists++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.assists, totalAssists);
        }

        [Server]
        public void AddSave()
        {
            stats.saves++;
            totalSaves++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.saves, totalSaves);
        }

        [Server]
        public void AddShot()
        {
            stats.shots++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.shots, 0);
        }

        [Server]
        public void AddPass()
        {
            stats.passes++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.passes, 0);
        }

        [Server]
        public void AddSteal()
        {
            stats.steals++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.steals, 0);
        }

        [Server]
        public void AddDeath()
        {
            stats.deaths++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.deaths, 0);
        }

        [Server]
        public void AddSuicide()
        {
            stats.suicides++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.suicides, 0);
        }

        [Server]
        public void AddGravityWellUsed()
        {
            stats.gravityWellsUsed++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.gravityWellsUsed, 0);
        }

        [Server]
        public void AddBoostUsed()
        {
            stats.boostsUsed++;
            OnStatsUpdated?.Invoke();
            UpdateStatsRpc(stats.boostsUsed, 0);
        }

        [ObserversRpc]
        private void UpdateStatsRpc(int statValue, int totalValue)
        {
            OnStatsUpdated?.Invoke();
        }
        #endregion

        #region Match Results
        [Server]
        public void RecordMatchResult(bool won)
        {
            matchesPlayed++;
            if (won) matchesWon++;

            CalculateAwards();
            ResetMatchStats();
        }

        [Server]
        private void CalculateAwards()
        {
            awards.mvp = stats.goals >= 3;
            awards.topScorer = stats.goals >= 2;
            awards.mostAssists = stats.assists >= 2;
            awards.mostSaves = stats.saves >= 3;
            awards.playmaker = stats.assists >= 2 && stats.goals >= 1;

            if (awards.mvp || awards.topScorer || awards.mostAssists || awards.mostSaves || awards.playmaker)
            {
                OnAwardEarned?.Invoke();
            }
        }

        [Server]
        public void ResetMatchStats()
        {
            stats = new PlayerStatistics();
            awards = new MatchAwards();
            _ballPossessionTimer = 0f;
            _hasBallPossession = false;
        }
        #endregion

        #region Public API
        public float GetScore()
        {
            return (stats.goals * 5f) + (stats.assists * 3f) + (stats.saves * 2f) +
                   (stats.steals * 1.5f) - (stats.deaths * 0.5f) - (stats.suicides * 1f);
        }

        public Dictionary<string, object> GetStatSnapshot()
        {
            return new Dictionary<string, object>
        {
            {"goals", stats.goals},
            {"assists", stats.assists},
            {"saves", stats.saves},
            {"shots", stats.shots},
            {"steals", stats.steals},
            {"timeWithBall", stats.timeWithBall},
            {"distance", stats.distanceTraveled}
        };
        }

        public bool HasAward() => awards.mvp || awards.topScorer || awards.mostAssists ||
                                awards.mostSaves || awards.playmaker;
        #endregion

        #region Cleanup
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe from SyncVar changes
            CurrentHealth.OnChange -= HandleHealthChanged;
            MaxHealth.OnChange -= HandleHealthChanged;
        }
        #endregion
    }
}