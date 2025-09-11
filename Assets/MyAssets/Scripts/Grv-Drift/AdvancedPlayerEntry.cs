using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Connection;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEditorInternal;
using FishNet; 
using FishNet.Transporting;
using FishNet.Managing; 
using FishNet.Managing.Server;

/// <summary>
/// Advanced player entry UI element with animations, interactivity, and comprehensive data display
/// </summary>
public class AdvancedPlayerEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Text Components")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text pingText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text kdaText;
    [SerializeField] private TMP_Text joinTimeText;

    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image teamIndicator;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image manaBarFill;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider manaSlider;

    [Header("Icons & Indicators")]
    [SerializeField] private GameObject hostIndicator;
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject deadIndicator;
    [SerializeField] private GameObject talkingIndicator;
    [SerializeField] private GameObject mutedIndicator;
    [SerializeField] private Image classIcon;
    [SerializeField] private Image rankIcon;

    [Header("Action Buttons")]
    [SerializeField] private Button whisperButton;
    [SerializeField] private Button muteButton;
    [SerializeField] private Button kickButton;
    [SerializeField] private Button reportButton;

    [Header("Colors & Themes")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(0.1f, 0.3f, 0.6f, 0.9f);
    [SerializeField] private Color highPingColor = Color.red;
    [SerializeField] private Color lowPingColor = Color.green;

    [Header("Animation Settings")]
    [SerializeField] private float hoverAnimationDuration = 0.2f;
    [SerializeField] private float selectAnimationDuration = 0.3f;
    [SerializeField] private float pulseIntensity = 1.1f;
    [SerializeField] private float pulseDuration = 1f;

    [Header("Performance Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private bool updatePingContinuously = true;
    [SerializeField] private bool updateHealthSmoothly = true;

    // Private variables
    private AdvancedNetworkPlayerController _boundPlayer;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private Color _originalBackgroundColor;
    private bool _isInitialized = false;
    private bool _isHovered = false;
    private bool _isSelected = false;
    private Coroutine _updateCoroutine;
    private Coroutine _animationCoroutine;

    // Properties
    public int ClientId => _boundPlayer?.Owner?.ClientId ?? -1;
    public string PlayerName => _boundPlayer?.PlayerName.Value ?? "Unknown";
    public bool IsVisible => gameObject.activeInHierarchy;

    #region Initialization
    public void Initialize(AdvancedNetworkPlayerController player, Color teamColor)
    {
        if (player == null)
        {
            Debug.LogError("Cannot initialize with null player!");
            return;
        }

        _boundPlayer = player;
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _originalScale = _rectTransform.localScale;
        _originalBackgroundColor = backgroundImage != null ? backgroundImage.color : normalColor;

        SetupReferences();
        SetupButtonListeners();
        ApplyTeamColor(teamColor);
        UpdateAllData();

        _isInitialized = true;

        // Start update coroutine
        if (updatePingContinuously || updateHealthSmoothly)
        {
            _updateCoroutine = StartCoroutine(UpdateDataCoroutine());
        }

        // Play entry animation
        PlayEntryAnimation();
    }

    private void SetupReferences()
    {
        // Auto-find references if not assigned
        if (playerNameText == null) playerNameText = transform.Find("PlayerNameText")?.GetComponent<TMP_Text>();
        if (scoreText == null) scoreText = transform.Find("ScoreText")?.GetComponent<TMP_Text>();
        if (pingText == null) pingText = transform.Find("PingText")?.GetComponent<TMP_Text>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        
        // Ensure we have required components
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetupButtonListeners()
    {
        if (whisperButton != null) whisperButton.onClick.AddListener(OnWhisperButtonClicked);
        if (muteButton != null) muteButton.onClick.AddListener(OnMuteButtonClicked);
        if (kickButton != null) kickButton.onClick.AddListener(OnKickButtonClicked);
        if (reportButton != null) reportButton.onClick.AddListener(OnReportButtonClicked);

        // Hide kick button for non-hosts and self
        UpdateButtonVisibility();
    }
    #endregion

    #region Data Updates
    public void UpdateData(AdvancedNetworkPlayerController player)
    {
        if (player == null) return;

        _boundPlayer = player;
        UpdateAllData();
    }

    private void UpdateAllData()
    {
        UpdatePlayerName();
        UpdateScore();
        UpdatePing();
        UpdateStatus();
        UpdateHealth();
        UpdateMana();
        UpdateKDA();
        UpdateJoinTime();
        UpdateIndicators();
        UpdateButtonVisibility();
    }

    private void UpdatePlayerName()
    {
        if (playerNameText != null && _boundPlayer != null)
        {
            playerNameText.text = _boundPlayer.PlayerName.Value;
            
            // Color code by team or status
            if (_boundPlayer.IsDead.Value)
                playerNameText.color = Color.gray;
            else if (_boundPlayer.IsHostInitialized)
                playerNameText.color = Color.yellow;
            else
                playerNameText.color = Color.white;
        }
    }

    private void UpdateScore()
    {
        if (scoreText != null && _boundPlayer != null)
        {
            scoreText.text = _boundPlayer.Score.Value.ToString("N0");
            scoreText.color = GetScoreColor(_boundPlayer.Score.Value);
        }
    }

    private void UpdatePing()
    {
        if (pingText != null && _boundPlayer != null)
        {
            pingText.text = _boundPlayer.Ping.ToString();
            pingText.color = GetPingColor(_boundPlayer.Ping);
        }
    }

    private void UpdateStatus()
    {
        if (statusText != null && _boundPlayer != null)
        {
            if (_boundPlayer.IsDead.Value)
                statusText.text = "DEAD";
            else if (_boundPlayer.IsReady.Value)
                statusText.text = "READY";
            else
                statusText.text = "";

            statusText.color = _boundPlayer.IsDead.Value ? Color.red : Color.green;
        }
    }

    private void UpdateHealth()
    {
        if (_boundPlayer == null) return;

        float targetHealth = _boundPlayer.Health.Value;
        float maxHealth = _boundPlayer.maxHealth;

        if (healthBarFill != null)
        {
            if (updateHealthSmoothly)
            {
                // Smooth animation
                LeanTween.value(healthBarFill.fillAmount, targetHealth / maxHealth, 0.3f)
                    .setOnUpdate((value) => healthBarFill.fillAmount = value);
            }
            else
            {
                healthBarFill.fillAmount = targetHealth / maxHealth;
            }

            healthBarFill.color = GetHealthColor(targetHealth / maxHealth);
        }

        if (healthSlider != null)
        {
            healthSlider.value = targetHealth;
            healthSlider.maxValue = maxHealth;
        }
    }

    private void UpdateMana()
    {
        if (_boundPlayer == null || manaBarFill == null) return;

        float targetMana = _boundPlayer.Mana.Value;
        float maxMana = _boundPlayer.MaxMana;

        if (updateHealthSmoothly)
        {
            LeanTween.value(manaBarFill.fillAmount, targetMana / maxMana, 0.3f)
                .setOnUpdate((value) => manaBarFill.fillAmount = value);
        }
        else
        {
            manaBarFill.fillAmount = targetMana / maxMana;
        }

        manaBarFill.color = GetManaColor(targetMana / maxMana);
    }

    private void UpdateKDA()
    {
        if (kdaText != null && _boundPlayer != null)
        {
            kdaText.text = $"{_boundPlayer.Kills}/{_boundPlayer.Deaths}/{_boundPlayer.Assists}";
            kdaText.color = GetKDAColor(_boundPlayer.Kills, _boundPlayer.Deaths);
        }
    }

    private void UpdateJoinTime()
    {
        if (joinTimeText != null && _boundPlayer != null)
        {
            System.TimeSpan playTime = System.DateTime.Now - _boundPlayer.JoinTime;
            joinTimeText.text = $"{playTime.Hours:00}:{playTime.Minutes:00}";
        }
    }

    private void UpdateIndicators()
    {
        if (hostIndicator != null) hostIndicator.SetActive(_boundPlayer.IsHostInitialized);
        if (readyIndicator != null) readyIndicator.SetActive(_boundPlayer.IsReady.Value);
        if (deadIndicator != null) deadIndicator.SetActive(_boundPlayer.IsDead.Value);
        if (talkingIndicator != null) talkingIndicator.SetActive(_boundPlayer.IsTalking.Value);
        if (mutedIndicator != null) mutedIndicator.SetActive(_boundPlayer.IsMuted.Value);

        // Update class icon if available
        if (classIcon != null && _boundPlayer.ClassIcon != null)
        {
            // When updating UI
            string iconKey = _boundPlayer.ClassIcon.Value;
             classIcon.sprite = Resources.Load<Sprite>($"Icons/{iconKey}");

            
        }

        // Update rank icon if available
        if (rankIcon != null && _boundPlayer.RankIcon != null)
        {
           string rankIcon = _boundPlayer.RankIcon.Value;
           classIcon.sprite = Resources.Load<Sprite>($"Icons/{rankIcon}");            
        }
    }

    private void UpdateButtonVisibility()
    {
        bool isHost = InstanceFinder.IsHostStarted;
        bool isSelf = _boundPlayer != null && _boundPlayer.Owner.IsLocalClient;

        if (kickButton != null)
        {
            kickButton.gameObject.SetActive(isHost && !isSelf);
        }

        if (muteButton != null)
        {
            muteButton.gameObject.SetActive(!isSelf);
        }
    }
    #endregion

    #region Animation & Effects
    private void PlayEntryAnimation()
    {
        // Scale animation
        _rectTransform.localScale = Vector3.zero;
        LeanTween.scale(_rectTransform, _originalScale, 0.5f)
            .setEase(LeanTweenType.easeOutBack)
            .setDelay(Random.Range(0f, 0.2f)); // Stagger effect

        // Fade animation
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            LeanTween.alphaCanvas(_canvasGroup, 1f, 0.4f);
        }
    }

    public void PlayPulseAnimation(Color pulseColor)
    {
        if (_animationCoroutine != null)
            StopCoroutine(_animationCoroutine);

        _animationCoroutine = StartCoroutine(PulseEffectCoroutine(pulseColor));
    }

    private IEnumerator PulseEffectCoroutine(Color pulseColor)
    {
        float elapsed = 0f;
        Color startColor = backgroundImage.color;

        while (elapsed < pulseDuration)
        {
            float t = Mathf.PingPong(elapsed / pulseDuration * 2f, 1f);
            backgroundImage.color = Color.Lerp(startColor, pulseColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        backgroundImage.color = startColor;
    }

    public void PlayDamageEffect()
    {
        // Flash red on damage
        LeanTween.value(gameObject, 0f, 1f, 0.2f)
            .setOnUpdate((value) => {
                if (backgroundImage != null)
                    backgroundImage.color = Color.Lerp(_originalBackgroundColor, Color.red, value);
            })
            .setLoopPingPong(1);
    }

    public void PlayHealEffect()
    {
        // Flash green on heal
        LeanTween.value(gameObject, 0f, 1f, 0.2f)
            .setOnUpdate((value) => {
                if (backgroundImage != null)
                    backgroundImage.color = Color.Lerp(_originalBackgroundColor, Color.green, value);
            })
            .setLoopPingPong(1);
    }
    #endregion

    #region Event Handlers
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        AnimateHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        AnimateHover(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ToggleSelection();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            ShowContextMenu();
        }
    }

    private void AnimateHover(bool isHovering)
    {
        if (backgroundImage != null)
        {
            Color targetColor = isHovering ? hoverColor : (_isSelected ? selectedColor : normalColor);
            LeanTween.color(backgroundImage.rectTransform, targetColor, hoverAnimationDuration);
        }

        if (_rectTransform != null)
        {
            Vector3 targetScale = isHovering ? _originalScale * 1.02f : _originalScale;
            LeanTween.scale(_rectTransform, targetScale, hoverAnimationDuration);
        }
    }

    private void ToggleSelection()
    {
        _isSelected = !_isSelected;
        AnimateSelection(_isSelected);
    }

    private void AnimateSelection(bool isSelected)
    {
        if (backgroundImage != null)
        {
            Color targetColor = isSelected ? selectedColor : (_isHovered ? hoverColor : normalColor);
            LeanTween.color(backgroundImage.rectTransform, targetColor, selectAnimationDuration);
        }
    }
    #endregion

    #region Button Actions
    private void OnWhisperButtonClicked()
    {
        if (_boundPlayer == null) return;

        ChatManager.Instance.OpenWhisperChat(_boundPlayer.PlayerName.Value);
        PlayPulseAnimation(Color.blue);
    }

    private void OnMuteButtonClicked()
    {
        if (_boundPlayer == null) return;

        bool isMuted = VoiceChatManager.Instance.ToggleMutePlayer(_boundPlayer.Owner.ClientId);
        mutedIndicator.SetActive(isMuted);
        
        PlayPulseAnimation(isMuted ? Color.red : Color.green);
    }

    NetworkManager _networkManager;

    private void OnKickButtonClicked()
    {
        if (_boundPlayer == null) return;

        // Guard: only the server should call Kick
        if (!InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("Only the server can kick players.");
            return;
        }

        int clientId = _boundPlayer.Owner.ClientId;

        ConfirmationDialog.Show(
            $"Kick {_boundPlayer.PlayerName.Value}?",
            $"Are you sure you want to kick {_boundPlayer.PlayerName.Value}?",
            () =>
            {
                // use KickReason (choose the appropriate reason enum value)
                InstanceFinder.ServerManager.Kick(clientId, KickReason.Unset);
            }
        );
    }

    private void OnReportButtonClicked()
    {
        if (_boundPlayer == null) return;

        ReportDialog.Show(_boundPlayer.PlayerName.Value, _boundPlayer.Owner.ClientId);
    }

   private void ShowContextMenu()
{
    ContextMenuManager.ShowContextMenu(
        _boundPlayer.PlayerName.Value,   // .Value because PlayerName is a SyncVar<string>
        new ContextMenuOption[] {
            new ContextMenuOption("Whisper", OnWhisperButtonClicked),
            new ContextMenuOption(_boundPlayer.IsMuted.Value ? "Unmute" : "Mute", OnMuteButtonClicked),
            new ContextMenuOption("Report", OnReportButtonClicked),
            new ContextMenuOption("View Profile", () => 
            ProfileManager.Instance.ShowProfile(_boundPlayer.Owner.ClientId.ToString()))
        }
    );
}

    #endregion

    #region Utility Methods
    private Color GetPingColor(int ping)
    {
        if (ping < 50) return lowPingColor;
        if (ping < 100) return Color.yellow;
        if (ping < 200) return Color.Lerp(Color.yellow, highPingColor, (ping - 100) / 100f);
        return highPingColor;
    }

    private Color GetHealthColor(float healthPercent)
    {
        if (healthPercent > 0.6f) return Color.green;
        if (healthPercent > 0.3f) return Color.yellow;
        return Color.red;
    }

    private Color GetManaColor(float manaPercent)
    {
        return Color.Lerp(Color.white, Color.blue, manaPercent);
    }

    private Color GetScoreColor(int score)
    {
        if (score > 1000) return Color.yellow;
        if (score > 500) return Color.Lerp(Color.white, Color.yellow, (score - 500) / 500f);
        return Color.white;
    }

    private Color GetKDAColor(int kills, int deaths)
    {
        float ratio = deaths > 0 ? (float)kills / deaths : kills;
        
        if (ratio > 2f) return Color.green;
        if (ratio > 1f) return Color.yellow;
        if (ratio > 0.5f) return Color.white;
        return Color.red;
    }

    private void ApplyTeamColor(Color teamColor)
    {
        if (teamIndicator != null)
        {
            teamIndicator.color = teamColor;
        }

        if (backgroundImage != null)
        {
            _originalBackgroundColor = new Color(teamColor.r * 0.3f, teamColor.g * 0.3f, teamColor.b * 0.3f, 0.8f);
            backgroundImage.color = _originalBackgroundColor;
        }
    }
    #endregion

    #region Coroutines
    private IEnumerator UpdateDataCoroutine()
    {
        while (_isInitialized)
        {
            if (_boundPlayer != null && IsVisible)
            {
                if (updatePingContinuously)
                    UpdatePing();
                
                if (updateHealthSmoothly)
                    UpdateHealth();
            }

            yield return new WaitForSeconds(updateInterval);
        }
    }
    #endregion

    #region Public API
    public void UpdateScore(int score) => UpdateScore();
    public void UpdatePing(int ping) => UpdatePing();
    public void UpdateHealth(float health, float maxHealth) => UpdateHealth();
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        AnimateSelection(selected);
    }

    public void ShowButtons(bool show)
    {
        if (whisperButton != null) whisperButton.gameObject.SetActive(show);
        if (muteButton != null) muteButton.gameObject.SetActive(show);
        if (kickButton != null) kickButton.gameObject.SetActive(show);
        if (reportButton != null) reportButton.gameObject.SetActive(show);
    }
    #endregion

    private void OnDestroy()
    {
        // Clean up coroutines
        if (_updateCoroutine != null)
            StopCoroutine(_updateCoroutine);

        if (_animationCoroutine != null)
            StopCoroutine(_animationCoroutine);

        // Remove all tweens
        LeanTween.cancel(gameObject);
    }

    [ContextMenu("Debug - Test Animation")]
    private void DebugTestAnimation()
    {
        PlayPulseAnimation(Color.cyan);
    }

    [ContextMenu("Debug - Test Damage")]
    private void DebugTestDamage()
    {
        PlayDamageEffect();
    }

    [ContextMenu("Debug - Test Heal")]
    private void DebugTestHeal()
    {
        PlayHealEffect();
    }
}