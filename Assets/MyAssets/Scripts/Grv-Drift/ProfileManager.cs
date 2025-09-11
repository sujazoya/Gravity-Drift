using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.Serialization;
using System;
using System.IO;
using UnityEngine.Networking;

namespace zoya.game
{



    /*
    Usage Examples:

    csharp
    // Add experience after a match
    ProfileManager.Instance.AddExperience(connection, 200, "match_victory");

    // Award currency
    ProfileManager.Instance.AddCurrency(connection, 100, "daily_reward");

    // Unlock new content
    ProfileManager.Instance.UnlockTitle(profile, "Champion");

    // Check if player is blocked
    bool isBlocked = ProfileManager.Instance.IsPlayerBlocked("player123");
    */

    /// <summary>
    /// Advanced player profile system with network synchronization, statistics tracking, and customization
    /// </summary>
    public class ProfileManager : NetworkBehaviour
    {
        public static ProfileManager Instance { get; private set; }

        [System.Serializable]
        public class PlayerProfile
        {
            public string playerId;
            public string playerName;
            public int level = 1;
            public int experience = 0;
            public int totalExperience = 0;
            public int currency = 0;
            public int premiumCurrency = 0;

            [Header("Statistics")]
            public int totalMatchesPlayed = 0;
            public int totalMatchesWon = 0;
            public int totalGoalsScored = 0;
            public int totalAssists = 0;
            public int totalSaves = 0;
            public int totalDeaths = 0;
            public float totalPlayTime = 0f;
            public int highestKillStreak = 0;
            public int currentWinStreak = 0;
            public int longestWinStreak = 0;

            [Header("Customization")]
            public string avatarId = "default";
            public string title = "Rookie";
            public List<string> unlockedAvatars = new List<string> { "default" };
            public List<string> unlockedTitles = new List<string> { "Rookie" };
            public Color nameColor = Color.white;
            public int selectedEmote = 0;
            public List<int> unlockedEmotes = new List<int> { 0 };

            [Header("Preferences")]
            public float mouseSensitivity = 2f;
            public float masterVolume = 1f;
            public float musicVolume = 0.8f;
            public float sfxVolume = 1f;
            public int graphicsQuality = 2;
            public bool showBloodEffects = true;
            public bool showGore = true;
            public bool motionBlur = true;
            public bool cameraShake = true;

            [Header("Social")]
            public List<string> friends = new List<string>();
            public List<string> blockedPlayers = new List<string>();
            public List<string> recentPlayers = new List<string>();
            public DateTime lastOnline;
            public bool showOnlineStatus = true;
            public bool allowFriendRequests = true;
            public bool allowMessages = true;

            public int ExperienceToNextLevel => CalculateExperienceForLevel(level + 1);
            public float LevelProgress => (float)experience / ExperienceToNextLevel;
            public int KDRatio => totalDeaths > 0 ? totalGoalsScored / totalDeaths : totalGoalsScored;
            public float WinRate => totalMatchesPlayed > 0 ? (float)totalMatchesWon / totalMatchesPlayed : 0f;

            private int CalculateExperienceForLevel(int targetLevel)
            {
                return Mathf.RoundToInt(100 * Mathf.Pow(1.2f, targetLevel - 1));
            }
        }

        [System.Serializable]
        public class ProfileSettings
        {
            public bool autoSave = true;
            public float autoSaveInterval = 60f;
            public bool cloudSave = true;
            public bool compressData = true;
            public int maxRecentPlayers = 20;
            public int maxFriends = 100;
            public int maxBlockedPlayers = 50;
        }

        [Header("UI References")]
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Slider experienceSlider;
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Transform statsContainer;
        [SerializeField] private Transform customizationContainer;
        [SerializeField] private GameObject statPrefab;
        [SerializeField] private GameObject customizationPrefab;

        [Header("Settings")]
        [SerializeField] private ProfileSettings settings = new ProfileSettings();
        [SerializeField] private List<string> defaultAvatars = new List<string> { "default" };
        [SerializeField] private List<string> defaultTitles = new List<string> { "Rookie" };

        // [Header("Experience Curve")]
        //[SerializeField] private AnimationCurve experienceCurve = AnimationCurve.Linear(0, 100, 100, 10000);

        // Network SyncVars
        public readonly SyncVar<PlayerProfile> CurrentProfile = new SyncVar<PlayerProfile>();
        private readonly SyncDictionary<string, PlayerProfile> _cachedProfiles = new SyncDictionary<string, PlayerProfile>();

        // Local storage
        private Dictionary<string, PlayerProfile> _localProfiles = new Dictionary<string, PlayerProfile>();
        private Dictionary<string, Sprite> _avatarCache = new Dictionary<string, Sprite>();
        private Coroutine _autoSaveCoroutine;
        private float _playTimeTimer = 0f;

        // Events
        public event Action<PlayerProfile> OnProfileLoaded;
        public event Action<PlayerProfile> OnProfileUpdated;
        public event Action<int> OnLevelUp;
        public event Action<int> OnCurrencyChanged;
        public event Action<string> OnTitleUnlocked;
        public event Action<string> OnAvatarUnlocked;

        #region Initialization
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeProfileSystem();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (IsServerInitialized)
            {
                InitializeServerProfileSystem();
            }

            if (IsClientInitialized)
            {
                InitializeClientProfileSystem();
            }
        }

        private void InitializeProfileSystem()
        {
            LoadLocalProfiles();
            _cachedProfiles.OnChange += HandleCachedProfileChange;

            if (settings.autoSave)
            {
                _autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
            }
        }

        private void InitializeServerProfileSystem()
        {
            // Server-specific initialization
            Debug.Log("Profile Manager Server Initialized");
        }

        private void InitializeClientProfileSystem()
        {
            // Client-specific initialization
            Debug.Log("Profile Manager Client Initialized");
        }
        #endregion

        #region Profile Management
        [Server]
        public void LoadOrCreateProfile(NetworkConnection conn, string playerId, string playerName)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError("Cannot load profile: Player ID is null or empty");
                return;
            }

            PlayerProfile profile;

            // Try to get from cached profiles first
            if (_cachedProfiles.TryGetValue(playerId, out profile))
            {
                CurrentProfile.Value = profile;
                SendProfileToClient(conn, profile);
                return;
            }

            // Try to load from local storage
            if (_localProfiles.TryGetValue(playerId, out profile))
            {
                _cachedProfiles.Add(playerId, profile);
                CurrentProfile.Value = profile;
                SendProfileToClient(conn, profile);
                return;
            }

            // Create new profile
            profile = CreateNewProfile(playerId, playerName);
            _localProfiles.Add(playerId, profile);
            _cachedProfiles.Add(playerId, profile);
            CurrentProfile.Value = profile;

            SaveProfileLocal(profile);
            SendProfileToClient(conn, profile);

            Debug.Log($"Created new profile for {playerName} ({playerId})");
        }

        [TargetRpc]
        private void SendProfileToClient(NetworkConnection conn, PlayerProfile profile)
        {
            CurrentProfile.Value = profile;
            OnProfileLoaded?.Invoke(profile);
            Debug.Log($"Profile loaded for {profile.playerName}");
        }

        private PlayerProfile CreateNewProfile(string playerId, string playerName)
        {
            return new PlayerProfile
            {
                playerId = playerId,
                playerName = playerName,
                level = 1,
                experience = 0,
                currency = 1000,
                premiumCurrency = 50,
                unlockedAvatars = new List<string>(defaultAvatars),
                unlockedTitles = new List<string>(defaultTitles),
                avatarId = "default",
                title = "Rookie",
                lastOnline = DateTime.Now
            };
        }

        [Server]
        public void SaveProfile(NetworkConnection conn, PlayerProfile profile)
        {
            if (profile == null) return;

            string playerId = profile.playerId;

            // Update cached profile
            if (_cachedProfiles.ContainsKey(playerId))
            {
                _cachedProfiles[playerId] = profile;
            }
            else
            {
                _cachedProfiles.Add(playerId, profile);
            }

            // Update local storage
            _localProfiles[playerId] = profile;

            // Save to disk
            SaveProfileLocal(profile);

            // Sync to client
            CurrentProfile.Value = profile;
            UpdateClientProfile(conn, profile);

            Debug.Log($"Profile saved for {profile.playerName}");
        }

        [TargetRpc]
        private void UpdateClientProfile(NetworkConnection conn, PlayerProfile profile)
        {
            CurrentProfile.Value = profile;
            OnProfileUpdated?.Invoke(profile);
        }
        #endregion

        #region Experience & Leveling
        [Server]
        public void AddExperience(NetworkConnection conn, int amount, string source = "gameplay")
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;
            profile.experience += amount;
            profile.totalExperience += amount;

            // Check for level up
            while (profile.experience >= profile.ExperienceToNextLevel)
            {
                LevelUp(conn, profile);
            }

            SaveProfile(conn, profile);
        }

        [Server]
        private void LevelUp(NetworkConnection conn, PlayerProfile profile)
        {
            profile.experience -= profile.ExperienceToNextLevel;
            profile.level++;

            // Reward for leveling up
            int currencyReward = profile.level * 100;
            profile.currency += currencyReward;

            // Unlock new titles/avatars based on level
            UnlockLevelRewards(profile);

            OnLevelUp?.Invoke(profile.level);
            RpcOnLevelUp(conn, profile.level, currencyReward);

            Debug.Log($"{profile.playerName} leveled up to {profile.level}! +{currencyReward} currency");
        }

        [TargetRpc]
        private void RpcOnLevelUp(NetworkConnection conn, int newLevel, int currencyReward)
        {
            // Client-side level up effects
            Debug.Log($"You leveled up to {newLevel}! +{currencyReward} currency");
        }

        [Server]
        private void UnlockLevelRewards(PlayerProfile profile)
        {
            // Unlock rewards based on level
            switch (profile.level)
            {
                case 5:
                    UnlockTitle(profile, "Veteran");
                    break;
                case 10:
                    UnlockAvatar(profile, "advanced");
                    break;
                case 20:
                    UnlockTitle(profile, "Elite");
                    break;
                case 30:
                    UnlockAvatar(profile, "master");
                    break;
                case 50:
                    UnlockTitle(profile, "Legend");
                    break;
            }
        }
        #endregion

        #region Currency System
        [Server]
        public void AddCurrency(NetworkConnection conn, int amount, string source = "gameplay")
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;
            profile.currency += amount;

            SaveProfile(conn, profile);
            OnCurrencyChanged?.Invoke(profile.currency);

            Debug.Log($"{profile.playerName} gained {amount} currency from {source}");
        }

        [Server]
        public void AddPremiumCurrency(NetworkConnection conn, int amount, string source = "purchase")
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;
            profile.premiumCurrency += amount;

            SaveProfile(conn, profile);

            Debug.Log($"{profile.playerName} gained {amount} premium currency from {source}");
        }

        [Server]
        public bool SpendCurrency(NetworkConnection conn, int amount, string itemId)
        {
            if (CurrentProfile.Value == null || CurrentProfile.Value.currency < amount)
                return false;

            var profile = CurrentProfile.Value;
            profile.currency -= amount;

            SaveProfile(conn, profile);
            OnCurrencyChanged?.Invoke(profile.currency);

            Debug.Log($"{profile.playerName} spent {amount} currency on {itemId}");
            return true;
        }

        [Server]
        public bool SpendPremiumCurrency(NetworkConnection conn, int amount, string itemId)
        {
            if (CurrentProfile.Value == null || CurrentProfile.Value.premiumCurrency < amount)
                return false;

            var profile = CurrentProfile.Value;
            profile.premiumCurrency -= amount;

            SaveProfile(conn, profile);

            Debug.Log($"{profile.playerName} spent {amount} premium currency on {itemId}");
            return true;
        }
        #endregion

        #region Unlockables
        [Server]
        public void UnlockTitle(PlayerProfile profile, string titleId)
        {
            if (profile.unlockedTitles.Contains(titleId)) return;

            profile.unlockedTitles.Add(titleId);
            OnTitleUnlocked?.Invoke(titleId);

            Debug.Log($"{profile.playerName} unlocked title: {titleId}");
        }

        [Server]
        public void UnlockAvatar(PlayerProfile profile, string avatarId)
        {
            if (profile.unlockedAvatars.Contains(avatarId)) return;

            profile.unlockedAvatars.Add(avatarId);
            OnAvatarUnlocked?.Invoke(avatarId);

            Debug.Log($"{profile.playerName} unlocked avatar: {avatarId}");
        }

        [Server]
        public void UnlockEmote(PlayerProfile profile, int emoteId)
        {
            if (profile.unlockedEmotes.Contains(emoteId)) return;

            profile.unlockedEmotes.Add(emoteId);
            profile.unlockedEmotes.Sort();

            Debug.Log($"{profile.playerName} unlocked emote: {emoteId}");
        }
        #endregion

        #region Statistics Tracking
        [Server]
        public void UpdateMatchStatistics(NetworkConnection conn, bool won, AdvancedPlayerStats matchStats)
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;

            profile.totalMatchesPlayed++;
            if (won)
            {
                profile.totalMatchesWon++;
                profile.currentWinStreak++;
                profile.longestWinStreak = Mathf.Max(profile.longestWinStreak, profile.currentWinStreak);
            }
            else
            {
                profile.currentWinStreak = 0;
            }

            profile.totalGoalsScored += matchStats.stats.goals;
            profile.totalAssists += matchStats.stats.assists;
            profile.totalSaves += matchStats.stats.saves;
            profile.totalDeaths += matchStats.stats.deaths;
            profile.totalPlayTime += matchStats.stats.timeWithBall;

            // Add experience based on performance
            int experienceGained = CalculateExperienceFromMatch(won, matchStats);
            AddExperience(conn, experienceGained, "match_completion");

            // Add currency reward
            int currencyReward = CalculateCurrencyReward(won, matchStats);
            AddCurrency(conn, currencyReward, "match_reward");

            SaveProfile(conn, profile);
        }

        private int CalculateExperienceFromMatch(bool won, AdvancedPlayerStats stats)
        {
            int baseExp = won ? 200 : 100;
            int performanceExp = stats.stats.goals * 50 + stats.stats.assists * 30 + stats.stats.saves * 20;
            return baseExp + performanceExp;
        }

        private int CalculateCurrencyReward(bool won, AdvancedPlayerStats stats)
        {
            int baseCurrency = won ? 100 : 50;
            int performanceCurrency = stats.stats.goals * 10 + stats.stats.assists * 5 + stats.stats.saves * 3;
            return baseCurrency + performanceCurrency;
        }
        #endregion

        #region Social Features
        [Server]
        public void AddFriend(NetworkConnection conn, string friendId)
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;

            if (profile.friends.Count >= settings.maxFriends)
            {
                Debug.Log("Friend list is full");
                return;
            }

            if (!profile.friends.Contains(friendId))
            {
                profile.friends.Add(friendId);
                SaveProfile(conn, profile);
                Debug.Log($"{profile.playerName} added friend: {friendId}");
            }
        }

        [Server]
        public void RemoveFriend(NetworkConnection conn, string friendId)
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;

            if (profile.friends.Contains(friendId))
            {
                profile.friends.Remove(friendId);
                SaveProfile(conn, profile);
                Debug.Log($"{profile.playerName} removed friend: {friendId}");
            }
        }

        [Server]
        public void BlockPlayer(NetworkConnection conn, string playerId)
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;

            if (profile.blockedPlayers.Count >= settings.maxBlockedPlayers)
            {
                Debug.Log("Blocked players list is full");
                return;
            }

            if (!profile.blockedPlayers.Contains(playerId))
            {
                profile.blockedPlayers.Add(playerId);
                SaveProfile(conn, profile);
                Debug.Log($"{profile.playerName} blocked player: {playerId}");
            }
        }

        [Server]
        public void UnblockPlayer(NetworkConnection conn, string playerId)
        {
            if (CurrentProfile.Value == null) return;

            var profile = CurrentProfile.Value;

            if (profile.blockedPlayers.Contains(playerId))
            {
                profile.blockedPlayers.Remove(playerId);
                SaveProfile(conn, profile);
                Debug.Log($"{profile.playerName} unblocked player: {playerId}");
            }
        }
        #endregion

        #region Local Storage
        private void LoadLocalProfiles()
        {
            string path = GetProfileStoragePath();

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return;
            }

            string[] profileFiles = Directory.GetFiles(path, "*.profile");

            foreach (string filePath in profileFiles)
            {
                try
                {
                    string jsonData = File.ReadAllText(filePath);
                    PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(jsonData);

                    if (profile != null && !string.IsNullOrEmpty(profile.playerId))
                    {
                        _localProfiles[profile.playerId] = profile;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load profile from {filePath}: {e.Message}");
                }
            }

            Debug.Log($"Loaded {_localProfiles.Count} local profiles");
        }

        private void SaveProfileLocal(PlayerProfile profile)
        {
            if (profile == null) return;

            try
            {
                string path = GetProfileStoragePath();
                string filePath = Path.Combine(path, $"{profile.playerId}.profile");

                string jsonData = JsonUtility.ToJson(profile, true);
                File.WriteAllText(filePath, jsonData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save profile: {e.Message}");
            }
        }

        private string GetProfileStoragePath()
        {
            return Path.Combine(Application.persistentDataPath, "Profiles");
        }

        private IEnumerator AutoSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(settings.autoSaveInterval);

                if (CurrentProfile.Value != null)
                {
                    SaveProfileLocal(CurrentProfile.Value);
                }
            }
        }
        #endregion

        #region UI Methods
        public void ShowProfile(string playerId)
        {
            if (_cachedProfiles.TryGetValue(playerId, out PlayerProfile profile))
            {
                DisplayProfile(profile);
            }
            else if (_localProfiles.TryGetValue(playerId, out profile))
            {
                DisplayProfile(profile);
            }
            else
            {
                Debug.LogWarning($"Profile not found: {playerId}");
            }
        }

        private void DisplayProfile(PlayerProfile profile)
        {
            if (profilePanel != null)
            {
                profilePanel.SetActive(true);
            }

            if (playerNameText != null)
            {
                playerNameText.text = profile.playerName;
                playerNameText.color = profile.nameColor;
            }

            if (levelText != null)
            {
                levelText.text = $"Level {profile.level}";
            }

            if (experienceSlider != null)
            {
                experienceSlider.value = profile.LevelProgress;
            }

            if (currencyText != null)
            {
                currencyText.text = $"{profile.currency} | {profile.premiumCurrency}";
            }

            // Load avatar image
            LoadAvatarImage(profile.avatarId);

            // Populate statistics
            PopulateStats(profile);

            // Populate customization
            PopulateCustomization(profile);
        }

        private async void LoadAvatarImage(string avatarId)
        {
            if (_avatarCache.TryGetValue(avatarId, out Sprite cachedSprite))
            {
                avatarImage.sprite = cachedSprite;
                return;
            }

            // Load avatar sprite (implement your avatar loading logic here)
            // This could be from Resources, Addressables, or network download
            Sprite avatarSprite = await LoadAvatarSpriteAsync(avatarId);

            if (avatarSprite != null)
            {
                _avatarCache[avatarId] = avatarSprite;
                avatarImage.sprite = avatarSprite;
            }
        }

        private System.Threading.Tasks.Task<Sprite> LoadAvatarSpriteAsync(string avatarId)
        {
            // Implement your avatar loading logic here
            return System.Threading.Tasks.Task.FromResult<Sprite>(null);
        }

        private void PopulateStats(PlayerProfile profile)
        {
            // Clear existing stats
            foreach (Transform child in statsContainer)
            {
                Destroy(child.gameObject);
            }

            // Add stats entries
            AddStatEntry("Matches Played", profile.totalMatchesPlayed.ToString());
            AddStatEntry("Matches Won", profile.totalMatchesWon.ToString());
            AddStatEntry("Win Rate", $"{profile.WinRate:P1}");
            AddStatEntry("Goals Scored", profile.totalGoalsScored.ToString());
            AddStatEntry("Assists", profile.totalAssists.ToString());
            AddStatEntry("Saves", profile.totalSaves.ToString());
            AddStatEntry("K/D Ratio", profile.KDRatio.ToString());
            AddStatEntry("Play Time", FormatPlayTime(profile.totalPlayTime));
        }

        private void PopulateCustomization(PlayerProfile profile)
        {
            // Clear existing customization
            foreach (Transform child in customizationContainer)
            {
                Destroy(child.gameObject);
            }

            // Add customization options (implement based on your game's customization system)
        }

        private void AddStatEntry(string statName, string statValue)
        {
            GameObject statEntry = Instantiate(statPrefab, statsContainer);
            TMP_Text[] texts = statEntry.GetComponentsInChildren<TMP_Text>();

            if (texts.Length >= 2)
            {
                texts[0].text = statName;
                texts[1].text = statValue;
            }
        }

        private string FormatPlayTime(float seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        #endregion

        #region Event Handlers
        private void HandleCachedProfileChange(SyncDictionaryOperation op, string key, PlayerProfile value, bool asServer)
        {
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    // Profile updated
                    break;
                case SyncDictionaryOperation.Remove:
                    // Profile removed
                    break;
                case SyncDictionaryOperation.Clear:
                    // All profiles cleared
                    break;
            }
        }

        private void Update()
        {
            // Update play time timer
            if (CurrentProfile.Value != null)
            {
                _playTimeTimer += Time.deltaTime;
                if (_playTimeTimer >= 60f) // Update every minute
                {
                    _playTimeTimer = 0f;
                    CurrentProfile.Value.totalPlayTime += 60f;
                }
            }
        }
        #endregion

        #region Cleanup
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Save all profiles before shutdown
            foreach (var profile in _localProfiles.Values)
            {
                SaveProfileLocal(profile);
            }

            // Stop auto-save coroutine
            if (_autoSaveCoroutine != null)
            {
                StopCoroutine(_autoSaveCoroutine);
            }

            _cachedProfiles.OnChange -= HandleCachedProfileChange;
        }

        private void OnApplicationQuit()
        {
            // Force save on application quit
            if (CurrentProfile.Value != null)
            {
                SaveProfileLocal(CurrentProfile.Value);
            }
        }
        #endregion

        #region Public API
        public PlayerProfile GetProfile(string playerId)
        {
            if (_cachedProfiles.TryGetValue(playerId, out PlayerProfile profile))
                return profile;

            if (_localProfiles.TryGetValue(playerId, out profile))
                return profile;

            return null;
        }

        public List<PlayerProfile> GetFriendsProfiles()
        {
            if (CurrentProfile.Value == null) return new List<PlayerProfile>();

            return CurrentProfile.Value.friends
                .Select(friendId => GetProfile(friendId))
                .Where(profile => profile != null)
                .ToList();
        }

        public bool IsPlayerBlocked(string playerId)
        {
            return CurrentProfile.Value?.blockedPlayers.Contains(playerId) ?? false;
        }

        public bool CanReceiveMessagesFrom(string playerId)
        {
            return CurrentProfile.Value?.allowMessages ?? true && !IsPlayerBlocked(playerId);
        }
        #endregion

        #region Editor Utilities
#if UNITY_EDITOR
        [ContextMenu("Debug - Print Current Profile")]
        private void DebugPrintCurrentProfile()
        {
            if (CurrentProfile.Value != null)
            {
                Debug.Log(JsonUtility.ToJson(CurrentProfile.Value, true));
            }
        }

        [ContextMenu("Debug - Add Test Currency")]
        private void DebugAddTestCurrency()
        {
            if (CurrentProfile.Value != null && IsServerInitialized)
            {
                AddCurrency(null, 1000, "debug");
            }
        }

        [ContextMenu("Debug - Add Test Experience")]
        private void DebugAddTestExperience()
        {
            if (CurrentProfile.Value != null && IsServerInitialized)
            {
                AddExperience(null, 500, "debug");
            }
        }
#endif
        #endregion
    }
}