using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Connection;
using FishNet.Managing;
using System.Linq;
using UnityEngine.EventSystems;
using FishNet;
/// <summary>
/// Advanced player list UI with sorting, filtering, animations, and full customization
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class AdvancedPlayerListUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static AdvancedPlayerListUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text serverInfoText;
    [SerializeField] private TMP_Text sortModeText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Filter & Sort References")]
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Toggle showReadyOnlyToggle;
    [SerializeField] private Toggle showAliveOnlyToggle;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageInfoText;

    [Header("Team Colors")]
    [SerializeField]
    private Color[] teamColors = {
        new Color(0.8f, 0.8f, 0.8f, 1f),    // No team
        new Color(0.2f, 0.4f, 1f, 1f),      // Blue
        new Color(1f, 0.3f, 0.3f, 1f),      // Red
        new Color(0.3f, 0.8f, 0.3f, 1f),    // Green
        new Color(1f, 0.8f, 0.2f, 1f)       // Yellow
    };

    [Header("UI Settings")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private bool autoShowOnEvents = true;
    [SerializeField] private bool showPing = true;
    [SerializeField] private bool showKDA = true;
    [SerializeField] private int entriesPerPage = 8;

    [Header("Animation Settings")]
    [SerializeField] private float entryAnimationDelay = 0.1f;
    [SerializeField] private float entryAnimationDuration = 0.5f;

    public enum SortMode { Name, Score, Ping, Team, JoinTime }

    private Dictionary<int, AdvancedPlayerEntry> _playerEntries = new Dictionary<int, AdvancedPlayerEntry>();
    private List<AdvancedNetworkPlayerController> _allPlayers = new List<AdvancedNetworkPlayerController>();
    private SortMode _currentSortMode = SortMode.Score;
    private string _currentSearchFilter = "";
    private bool _showReadyOnly = false;
    private bool _showAliveOnly = false;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private bool _isVisible = false;
    private float _fadeProgress = 0f;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        // Singleton pattern with destruction protection
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-get CanvasGroup if not assigned
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        InitializeUI();
        SetupEventListeners();
        LoadPlayerPrefs();
        HideUIInstant(); // Start hidden
    }

    private void Update()
    {
        HandleInput();
        UpdateAutoHide();
    }

    private void InitializeUI()
    {
        // Clear existing entries
        ClearAllEntries();

        // Initialize sort dropdown
        if (sortDropdown != null)
        {
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(new List<string> { "Score", "Name", "Ping", "Team", "Join Time" });
            sortDropdown.value = (int)_currentSortMode;
        }

        // Set initial page info
        UpdatePageInfo();
    }

    private void SetupEventListeners()
    {
        // Sort dropdown
        if (sortDropdown != null)
            sortDropdown.onValueChanged.AddListener(OnSortModeChanged);

        // Search input
        if (searchInputField != null)
            searchInputField.onValueChanged.AddListener(OnSearchFilterChanged);

        // Filter toggles
        if (showReadyOnlyToggle != null)
            showReadyOnlyToggle.onValueChanged.AddListener(OnReadyFilterChanged);

        if (showAliveOnlyToggle != null)
            showAliveOnlyToggle.onValueChanged.AddListener(OnAliveFilterChanged);

        // Pagination buttons
        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(PreviousPage);

        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(NextPage);

        // Network events
        AdvancedGameManager.OnPlayerJoined += OnPlayerJoined;
        AdvancedGameManager.OnPlayerLeft += OnPlayerLeft;
        AdvancedGameManager.OnPlayerScoreChanged += OnPlayerScoreChanged;
    }

    #region Player Management
    public void AddOrUpdatePlayer(AdvancedNetworkPlayerController player)
    {
        if (player == null) return;

        int clientId = player.Owner.ClientId;

        if (_playerEntries.ContainsKey(clientId))
        {
            UpdatePlayerEntry(player);
        }
        else
        {
            CreatePlayerEntry(player);
        }

        UpdatePlayerCount();
        SortAndFilterPlayers();

        if (autoShowOnEvents)
            ShowUI();
    }

    private void CreatePlayerEntry(AdvancedNetworkPlayerController player)
    {
        if (playerEntryPrefab == null || playerListContent == null) return;

        GameObject entryGO = Instantiate(playerEntryPrefab, playerListContent);
        AdvancedPlayerEntry entry = entryGO.GetComponent<AdvancedPlayerEntry>();

        if (entry == null)
        {
            Debug.LogError("Player entry prefab missing AdvancedPlayerEntry component!");
            Destroy(entryGO);
            return;
        }

        Color teamColor = GetTeamColor(player.TeamIndex);
        entry.Initialize(player, teamColor);

        _playerEntries.Add(player.Owner.ClientId, entry);
        _allPlayers.Add(player);

        // Animate entry
        AnimateEntry(entryGO);

        Debug.Log($"Created UI entry for: {player.PlayerName}");
    }

    private void UpdatePlayerEntry(AdvancedNetworkPlayerController player)
    {
        if (_playerEntries.TryGetValue(player.Owner.ClientId, out AdvancedPlayerEntry entry))
        {
            entry.UpdateData(player);
        }
    }

    public void RemovePlayer(int clientId)
    {
        if (_playerEntries.TryGetValue(clientId, out AdvancedPlayerEntry entry))
        {
            // Find and remove from all players list
            var playerToRemove = _allPlayers.FirstOrDefault(p => p.Owner.ClientId == clientId);
            if (playerToRemove != null)
                _allPlayers.Remove(playerToRemove);

            Destroy(entry.gameObject);
            _playerEntries.Remove(clientId);

            UpdatePlayerCount();
            SortAndFilterPlayers();

            if (autoShowOnEvents)
                ShowUI();
        }
    }
    #endregion

    #region Sorting & Filtering
    private void SortAndFilterPlayers()
    {
        // Apply filters        
    var filteredPlayers = _allPlayers.Where(p =>
        (string.IsNullOrEmpty(_currentSearchFilter) ||
         p.PlayerName.Value.ToLower().Contains(_currentSearchFilter.ToLower())) &&
        (!_showReadyOnly || p.IsReady.Value) &&
        (!_showAliveOnly || !p.IsDead.Value)
    ).ToList();

        // Apply sorting
        switch (_currentSortMode)
        {
            case SortMode.Score:
                filteredPlayers = filteredPlayers.OrderByDescending(p => p.Score).ToList();
                break;
            case SortMode.Name:
                filteredPlayers = filteredPlayers.OrderBy(p => p.PlayerName).ToList();
                break;
            case SortMode.Ping:
                filteredPlayers = filteredPlayers.OrderBy(p => p.Ping).ToList();
                break;
            case SortMode.Team:
                filteredPlayers = filteredPlayers.OrderBy(p => p.TeamIndex).ThenByDescending(p => p.Score).ToList();
                break;
            case SortMode.JoinTime:
                filteredPlayers = filteredPlayers.OrderBy(p => p.JoinTime).ToList();
                break;
        }

        // Update pagination
        _totalPages = Mathf.Max(1, Mathf.CeilToInt(filteredPlayers.Count / (float)entriesPerPage));
        _currentPage = Mathf.Clamp(_currentPage, 1, _totalPages);

        // Show/hide entries based on filters and pagination
        int startIndex = (_currentPage - 1) * entriesPerPage;
        int endIndex = Mathf.Min(startIndex + entriesPerPage, filteredPlayers.Count);

        // Hide all entries first
        foreach (var entry in _playerEntries.Values)
        {
            entry.gameObject.SetActive(false);
        }

        // Show only filtered and paginated entries
        for (int i = startIndex; i < endIndex; i++)
        {
            int clientId = filteredPlayers[i].Owner.ClientId;
            if (_playerEntries.TryGetValue(clientId, out AdvancedPlayerEntry entry))
            {
                entry.gameObject.SetActive(true);
                entry.transform.SetAsLastSibling(); // Maintain sort order
            }
        }

        UpdatePageInfo();
        UpdateSortModeText();
    }
    #endregion

    #region UI Controls
    private void OnSortModeChanged(int modeIndex)
    {
        _currentSortMode = (SortMode)modeIndex;
        SortAndFilterPlayers();
        SavePlayerPrefs();
    }

    private void OnSearchFilterChanged(string filter)
    {
        _currentSearchFilter = filter;
        _currentPage = 1; // Reset to first page when searching
        SortAndFilterPlayers();
    }

    private void OnReadyFilterChanged(bool showReadyOnly)
    {
        _showReadyOnly = showReadyOnly;
        _currentPage = 1;
        SortAndFilterPlayers();
        SavePlayerPrefs();
    }

    private void OnAliveFilterChanged(bool showAliveOnly)
    {
        _showAliveOnly = showAliveOnly;
        _currentPage = 1;
        SortAndFilterPlayers();
        SavePlayerPrefs();
    }

    public void NextPage()
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            SortAndFilterPlayers();
        }
    }

    public void PreviousPage()
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            SortAndFilterPlayers();
        }
    }
    #endregion

    #region Visibility Control
    public void ShowUI()
    {
        if (_isVisible) return;

        _isVisible = true;
        StopAllCoroutines();
        StartCoroutine(FadeUI(1f, fadeDuration));
    }

    public void HideUI()
    {
        if (!_isVisible) return;

        _isVisible = false;
        StopAllCoroutines();
        StartCoroutine(FadeUI(0f, fadeDuration));
    }

    public void ToggleUI()
    {
        if (_isVisible) HideUI();
        else ShowUI();
    }

    private void HideUIInstant()
    {
        _isVisible = false;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private System.Collections.IEnumerator FadeUI(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        canvasGroup.blocksRaycasts = targetAlpha > 0.5f;
        canvasGroup.interactable = targetAlpha > 0.5f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
    #endregion

    #region Utility Methods
    private Color GetTeamColor(int teamIndex)
    {
        if (teamColors != null && teamIndex >= 0 && teamIndex < teamColors.Length)
            return teamColors[teamIndex];
        return Color.white;
    }

    private void UpdatePlayerCount()
    {
        if (playerCountText != null)
            playerCountText.text = $"Players: {_playerEntries.Count}";
    }

    private void UpdatePageInfo()
    {
        if (pageInfoText != null)
            pageInfoText.text = $"Page {_currentPage}/{_totalPages}";

        // Update button interactability
        if (previousPageButton != null)
            previousPageButton.interactable = _currentPage > 1;

        if (nextPageButton != null)
            nextPageButton.interactable = _currentPage < _totalPages;
    }

    private void UpdateSortModeText()
    {
        if (sortModeText != null)
            sortModeText.text = $"Sorted by: {_currentSortMode}";
    }

    private void AnimateEntry(GameObject entry)
    {
        // Scale animation
        entry.transform.localScale = Vector3.zero;
        LeanTween.scale(entry, Vector3.one, entryAnimationDuration)
            .setEase(LeanTweenType.easeOutBack)
            .setDelay(entryAnimationDelay);
    }

    public void ClearAllEntries()
    {
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }
        _playerEntries.Clear();
        _allPlayers.Clear();
        UpdatePlayerCount();
    }
    #endregion

    #region Input Handling
    private void HandleInput()
    {
        // Toggle with Tab key
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleUI();
        }

        // Scroll through pages with Q/E keys
        if (_isVisible)
        {
            if (Input.GetKeyDown(KeyCode.Q))
                PreviousPage();
            else if (Input.GetKeyDown(KeyCode.E))
                NextPage();
        }
    }

    private void UpdateAutoHide()
    {
        // Auto-hide when mouse leaves the panel
        if (_isVisible && !IsMouseOverUI())
        {
            // Start auto-hide timer here if desired
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Keep visible when mouse is over
        StopAllCoroutines();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Start auto-hide when mouse leaves
        if (_isVisible)
        {
            // Start auto-hide timer here
        }
    }

    private bool IsMouseOverUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }
    #endregion

    #region PlayerPrefs
    private void LoadPlayerPrefs()
    {
        _currentSortMode = (SortMode)PlayerPrefs.GetInt("PlayerList_SortMode", (int)SortMode.Score);
        _showReadyOnly = PlayerPrefs.GetInt("PlayerList_ShowReadyOnly", 0) == 1;
        _showAliveOnly = PlayerPrefs.GetInt("PlayerList_ShowAliveOnly", 0) == 1;

        // Apply loaded settings to UI
        if (sortDropdown != null)
            sortDropdown.value = (int)_currentSortMode;

        if (showReadyOnlyToggle != null)
            showReadyOnlyToggle.isOn = _showReadyOnly;

        if (showAliveOnlyToggle != null)
            showAliveOnlyToggle.isOn = _showAliveOnly;
    }

    private void SavePlayerPrefs()
    {
        PlayerPrefs.SetInt("PlayerList_SortMode", (int)_currentSortMode);
        PlayerPrefs.SetInt("PlayerList_ShowReadyOnly", _showReadyOnly ? 1 : 0);
        PlayerPrefs.SetInt("PlayerList_ShowAliveOnly", _showAliveOnly ? 1 : 0);
        PlayerPrefs.Save();
    }
    #endregion

    #region Public API
    public void RefreshAllEntries()
    {
        foreach (var player in _allPlayers.ToList())
        {
            AddOrUpdatePlayer(player);
        }
    }

    public void ForceShow()
    {
        ShowUI();
    }

    public void ForceHide()
    {
        HideUI();
    }

    public bool IsVisible => _isVisible;

    public List<AdvancedNetworkPlayerController> GetVisiblePlayers()
    {
        return _allPlayers.Where(p =>
            _playerEntries.ContainsKey(p.Owner.ClientId) &&
            _playerEntries[p.Owner.ClientId].gameObject.activeSelf
        ).ToList();
    }
    #endregion

    private void OnDestroy()
    {
        // Clean up event listeners
        if (sortDropdown != null)
            sortDropdown.onValueChanged.RemoveAllListeners();

        if (searchInputField != null)
            searchInputField.onValueChanged.RemoveAllListeners();

        if (InstanceFinder.NetworkManager != null)
        {
            AdvancedGameManager.OnPlayerJoined -= OnPlayerJoined;
            AdvancedGameManager.OnPlayerLeft -= OnPlayerLeft;
            AdvancedGameManager.OnPlayerScoreChanged -= OnPlayerScoreChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Player Management - Public API

/// <summary>
/// Attempts to add a player to the list. Returns true if successful, false if player already exists or is invalid.
/// </summary>
public bool TryAddPlayer(AdvancedNetworkPlayerController player)
{
    if (player == null || player.Owner == null)
    {
        Debug.LogWarning("Cannot add null player or player without owner.");
        return false;
    }

    int clientId = player.Owner.ClientId;

    // Check if player already exists
    if (_playerEntries.ContainsKey(clientId))
    {
        Debug.Log($"Player {player.PlayerName} already exists in the list.");
        UpdatePlayerEntry(player);
        return true;
    }

    try
    {
        CreatePlayerEntry(player);
        UpdatePlayerCount();
        SortAndFilterPlayers();

        if (autoShowOnEvents)
            ShowUI();

        Debug.Log($"Successfully added player: {player.PlayerName}");
        return true;
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Failed to add player {player.PlayerName}: {e.Message}");
        return false;
    }
}

/// <summary>
/// Attempts to remove a player from the list. Returns true if successful, false if player doesn't exist.
/// </summary>
public bool TryRemovePlayer(int clientId)
{
    if (_playerEntries.ContainsKey(clientId))
    {
        RemovePlayer(clientId);
        return true;
    }
    
    Debug.LogWarning($"Player with ID {clientId} not found in the list.");
    return false;
}

/// <summary>
/// Attempts to update a player's entry. Returns true if successful, false if player doesn't exist.
/// </summary>
public bool TryUpdatePlayer(AdvancedNetworkPlayerController player)
{
    if (player == null || player.Owner == null)
        return false;

    int clientId = player.Owner.ClientId;

    if (_playerEntries.ContainsKey(clientId))
    {
        UpdatePlayerEntry(player);
        SortAndFilterPlayers();
        return true;
    }

    // Player doesn't exist, try to add them
    return TryAddPlayer(player);
}

#endregion

    #region Network Event Handlers
    private void OnPlayerJoined(AdvancedNetworkPlayerController player)
    {
        AddOrUpdatePlayer(player);
    }

    private void OnPlayerLeft(int clientId)
    {
        RemovePlayer(clientId);
    }

    private void OnPlayerScoreChanged(int clientId, int newScore)
    {
        if (_playerEntries.TryGetValue(clientId, out AdvancedPlayerEntry entry))
        {
            entry.UpdateScore(newScore);
            SortAndFilterPlayers(); // Re-sort when scores change
        }
    }
    #endregion

    [ContextMenu("Debug - Add Test Players")]
    private void DebugAddTestPlayers()
    {
        for (int i = 0; i < 5; i++)
        {
            var testPlayer = new GameObject($"TestPlayer_{i}").AddComponent<AdvancedNetworkPlayerController>();

            // Use the existing methods and properties from your AdvancedNetworkPlayerController
            testPlayer.SetPlayerName($"Player_{i}");
            testPlayer.Score.Value = Random.Range(0, 100);
            testPlayer.TeamId.Value = Random.Range(0, 3);
            testPlayer.IsReady.Value = Random.value > 0.5f;
            testPlayer.IsDead.Value = Random.value > 0.7f;

            // For ping, you'll need to handle it differently since it's not in your original class
            // Option 1: Add a local ping field if needed for debug
            // testPlayer.GetComponent<AdvancedNetworkPlayerController>().SetPing(Random.Range(10, 150));

            // Option 2: Use a separate dictionary to track ping for debug players
            // _debugPlayerPing[testPlayer] = Random.Range(10, 150);

            AddOrUpdatePlayer(testPlayer);
        }
        
    }
        // Add this class definition somewhere in your file
private class DebugPlayerData
{
    public int Ping { get; set; }
    public int TeamIndex { get; set; }
}

// And this dictionary
private Dictionary<AdvancedNetworkPlayerController, DebugPlayerData> _debugPlayerData = new Dictionary<AdvancedNetworkPlayerController, DebugPlayerData>();
}