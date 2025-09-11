using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Connection;
namespace zoya.game
{



    /// <summary>
    /// Advanced player list item with interactive features, animations, and comprehensive player information
    /// </summary>

    /*
    // In your player list manager
    PlayerListItem item = Instantiate(playerListItemPrefab, contentParent);
    item.Initialize(player, teamColor);

    // Subscribe to events
    item.OnSelected += OnPlayerSelected;
    item.OnPlayerClicked += OnPlayerClicked;

    // Update player data
    item.UpdateData(updatedPlayer);

    // Programmatic selection
    item.SetSelected(true);

    // Show/hide buttons
    item.ShowButtons(true);
    */

    public class PlayerListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [System.Serializable]
        public class UIColors
        {
            public Color normalBackground = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            public Color hoverBackground = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            public Color selectedBackground = new Color(0.1f, 0.3f, 0.6f, 0.9f);
            public Color localPlayerBackground = new Color(0.1f, 0.4f, 0.2f, 0.9f);
            public Color hostBackground = new Color(0.4f, 0.2f, 0.1f, 0.9f);

            public Color highPingColor = Color.red;
            public Color mediumPingColor = Color.yellow;
            public Color lowPingColor = Color.green;

            public Color highScoreColor = Color.yellow;
            public Color positiveKDRColor = Color.green;
            public Color negativeKDRColor = Color.red;
        }

        [System.Serializable]
        public class AnimationSettings
        {
            public float hoverAnimationDuration = 0.2f;
            public float selectAnimationDuration = 0.3f;
            public float entryAnimationDuration = 0.5f;
            public float pulseDuration = 1f;
            public float updateInterval = 0.5f;
        }

        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text pingText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text kdaText;
        [SerializeField] private Image teamIndicator;
        [SerializeField] private Image healthBar;
        [SerializeField] private Image manaBar;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider manaSlider;

        [Header("Icons & Indicators")]
        [SerializeField] private GameObject hostIcon;
        [SerializeField] private GameObject readyIcon;
        [SerializeField] private GameObject deadIcon;
        [SerializeField] private GameObject talkingIcon;
        [SerializeField] private GameObject mutedIcon;
        [SerializeField] private Image classIcon;
        [SerializeField] private Image rankIcon;
        [SerializeField] private GameObject highlightEffect;

        [Header("Action Buttons")]
        [SerializeField] private Button whisperButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private Button kickButton;
        [SerializeField] private Button reportButton;
        [SerializeField] private Button spectateButton;

        [Header("Settings")]
        [SerializeField] private UIColors colors = new UIColors();
        [SerializeField] private AnimationSettings animationSettings = new AnimationSettings();
        [SerializeField] private bool showDetailedStats = true;
        [SerializeField] private bool realTimeUpdates = true;

        private AdvancedNetworkPlayerController _player;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector3 _originalScale;
        private Color _originalBackgroundColor;
        private bool _isHovered;
        private bool _isSelected;
        private bool _isInitialized;
        private Coroutine _updateCoroutine;
        private Coroutine _animationCoroutine;

        // Properties
        public int ClientId => _player?.Owner?.ClientId ?? -1;
        public string PlayerName => _player?.PlayerName.Value ?? "Unknown";
        public int TeamId => _player?.TeamId.Value ?? 0;
        public bool IsLocalPlayer => _player != null && _player.Owner.IsLocalClient;
        public bool IsHost => _player != null && _player.IsHostInitialized;

        // Events
        public event System.Action<PlayerListItem> OnSelected;
        public event System.Action<PlayerListItem> OnPlayerClicked;

        #region Initialization



        public void Initialize(AdvancedNetworkPlayerController player, Color teamColor)
        {
            if (player == null)
            {
                Debug.LogError("Cannot initialize with null player!");
                return;
            }

            _player = player;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _originalScale = _rectTransform.localScale;
            _originalBackgroundColor = backgroundImage != null ? backgroundImage.color : colors.normalBackground;

            SetupReferences();
            SetupButtonListeners();
            ApplyTeamColor(teamColor);
            UpdateAllData();

            _isInitialized = true;

            if (realTimeUpdates)
            {
                _updateCoroutine = StartCoroutine(UpdateDataCoroutine());
            }

            PlayEntryAnimation();
        }

        private void SetupReferences()
        {
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }

        private void SetupButtonListeners()
        {
            if (whisperButton != null) whisperButton.onClick.AddListener(OnWhisperClicked);
            if (muteButton != null) muteButton.onClick.AddListener(OnMuteClicked);
            if (kickButton != null) kickButton.onClick.AddListener(OnKickClicked);
            if (reportButton != null) reportButton.onClick.AddListener(OnReportClicked);
            if (spectateButton != null) spectateButton.onClick.AddListener(OnSpectateClicked);

            UpdateButtonVisibility();
        }
        #endregion

        #region Data Updates
        public void UpdateData(AdvancedNetworkPlayerController player)
        {
            if (player == null) return;

            _player = player;
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
            UpdateIndicators();
            UpdateButtonVisibility();
            UpdateBackgroundColor();
        }

        private void UpdatePlayerName()
        {
            if (playerNameText != null && _player != null)
            {
                playerNameText.text = _player.PlayerName.Value;

                // Color code by status
                if (_player.IsDead.Value)
                    playerNameText.color = Color.gray;
                else if (IsHost)
                    playerNameText.color = colors.hostBackground;
                else if (IsLocalPlayer)
                    playerNameText.color = colors.localPlayerBackground;
                else
                    playerNameText.color = Color.white;
            }
        }

        private void UpdateScore()
        {
            if (scoreText != null && _player != null)
            {
                scoreText.text = _player.Score.Value.ToString("N0");
                scoreText.color = GetScoreColor(_player.Score.Value);
            }
        }

        private void UpdatePing()
        {
            if (pingText != null && _player != null)
            {
                int ping = GetPlayerPing();
                pingText.text = ping.ToString();
                pingText.color = GetPingColor(ping);
            }
        }

        private void UpdateStatus()
        {
            if (statusText != null && _player != null)
            {
                if (_player.IsDead.Value)
                    statusText.text = "DEAD";
                else if (_player.IsReady.Value)
                    statusText.text = "READY";
                else if (_player.IsTalking.Value)
                    statusText.text = "TALKING";
                else
                    statusText.text = "";

                statusText.color = _player.IsDead.Value ? Color.red : Color.green;
            }
        }

        private void UpdateHealth()
        {
            if (_player == null) return;

            float healthPercent = _player.GetHealthPercent();

            if (healthBar != null)
            {
                healthBar.fillAmount = healthPercent;
                healthBar.color = GetHealthColor(healthPercent);
            }

            if (healthSlider != null)
            {
                healthSlider.value = _player.Health.Value;
                healthSlider.maxValue = _player.MaxHealth;
            }
        }

        private void UpdateMana()
        {
            if (_player == null || manaBar == null) return;

            float manaPercent = _player.ManaPercent;
            manaBar.fillAmount = manaPercent;
            manaBar.color = GetManaColor(manaPercent);

            if (manaSlider != null)
            {
                manaSlider.value = _player.Mana.Value;
                manaSlider.maxValue = _player.MaxMana;
            }
        }

        private void UpdateKDA()
        {
            if (kdaText != null && _player != null && showDetailedStats)
            {
                kdaText.text = $"{_player.Kills}/{_player.Deaths}/{_player.Assists}";
                kdaText.color = GetKDAColor(_player.Kills, _player.Deaths);
            }
        }

        private void UpdateIndicators()
        {
            if (hostIcon != null) hostIcon.SetActive(IsHost);
            if (readyIcon != null) readyIcon.SetActive(_player.IsReady.Value);
            if (deadIcon != null) deadIcon.SetActive(_player.IsDead.Value);
            if (talkingIcon != null) talkingIcon.SetActive(_player.IsTalking.Value);
            if (mutedIcon != null) mutedIcon.SetActive(_player.IsMuted.Value);

            UpdateClassIcon();
            UpdateRankIcon();
        }

        private void UpdateClassIcon()
        {
            if (classIcon != null && _player.ClassIcon != null)
            {
                // Load class icon based on player's class
                string iconKey = _player.ClassIcon.Value;
                // Implementation depends on your icon system
            }
        }

        private void UpdateRankIcon()
        {
            if (rankIcon != null && _player.RankIcon != null)
            {
                // Load rank icon based on player's rank
                string rankKey = _player.RankIcon.Value;
                // Implementation depends on your rank system
            }
        }

        private void UpdateButtonVisibility()
        {
            bool isLocal = IsLocalPlayer;
            bool isHost = false;

            // Check if this player is host or if we need to check global host status
            if (_player != null)
            {
                isHost = _player.IsHostInitialized;
            }
            else
            {
                // Alternative: check if local client is host
                isHost = InstanceFinder.NetworkManager.IsHostStarted;
            }

            if (whisperButton != null) whisperButton.gameObject.SetActive(!isLocal);
            if (muteButton != null) muteButton.gameObject.SetActive(!isLocal);
            if (kickButton != null) kickButton.gameObject.SetActive(isHost && !isLocal);
            if (reportButton != null) reportButton.gameObject.SetActive(!isLocal);
            if (spectateButton != null) spectateButton.gameObject.SetActive(!isLocal && _player != null && _player.IsDead.Value);
        }

        private void UpdateBackgroundColor()
        {
            if (backgroundImage == null) return;

            Color targetColor = colors.normalBackground;

            if (IsLocalPlayer)
                targetColor = colors.localPlayerBackground;
            else if (IsHost)
                targetColor = colors.hostBackground;
            else if (_isSelected)
                targetColor = colors.selectedBackground;
            else if (_isHovered)
                targetColor = colors.hoverBackground;

            backgroundImage.color = targetColor;
        }
        #endregion

        #region Animation & Effects
        private void PlayEntryAnimation()
        {
            if (_rectTransform == null) return;

            _rectTransform.localScale = Vector3.zero;
            LeanTween.scale(_rectTransform, _originalScale, animationSettings.entryAnimationDuration)
                .setEase(LeanTweenType.easeOutBack)
                .setDelay(UnityEngine.Random.Range(0f, 0.2f));

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                LeanTween.alphaCanvas(_canvasGroup, 1f, animationSettings.entryAnimationDuration * 0.8f);
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

            while (elapsed < animationSettings.pulseDuration)
            {
                float t = Mathf.PingPong(elapsed / animationSettings.pulseDuration * 2f, 1f);
                backgroundImage.color = Color.Lerp(startColor, pulseColor, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            backgroundImage.color = startColor;
        }

        public void PlayDamageEffect()
        {
            LeanTween.value(gameObject, 0f, 1f, 0.2f)
                .setOnUpdate((value) =>
                {
                    if (backgroundImage != null)
                        backgroundImage.color = Color.Lerp(_originalBackgroundColor, Color.red, value);
                })
                .setLoopPingPong(1);
        }

        public void PlayHealEffect()
        {
            LeanTween.value(gameObject, 0f, 1f, 0.2f)
                .setOnUpdate((value) =>
                {
                    if (backgroundImage != null)
                        backgroundImage.color = Color.Lerp(_originalBackgroundColor, Color.green, value);
                })
                .setLoopPingPong(1);
        }

        private void AnimateHover(bool isHovering)
        {
            if (backgroundImage != null)
            {
                Color targetColor = isHovering ? colors.hoverBackground :
                    (_isSelected ? colors.selectedBackground : _originalBackgroundColor);

                LeanTween.color(backgroundImage.rectTransform, targetColor, animationSettings.hoverAnimationDuration);
            }

            if (_rectTransform != null)
            {
                Vector3 targetScale = isHovering ? _originalScale * 1.02f : _originalScale;
                LeanTween.scale(_rectTransform, targetScale, animationSettings.hoverAnimationDuration);
            }

            if (highlightEffect != null)
            {
                highlightEffect.SetActive(isHovering);
            }
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

            OnPlayerClicked?.Invoke(this);
        }

        private void ToggleSelection()
        {
            _isSelected = !_isSelected;
            UpdateBackgroundColor();
            OnSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateBackgroundColor();
        }
        #endregion

        #region Button Actions
        private void OnWhisperClicked()
        {
            if (_player == null) return;
            ChatManager.Instance.OpenWhisperChat(_player.PlayerName.Value);
            PlayPulseAnimation(Color.blue);
        }

        private void OnMuteClicked()
        {
            if (_player == null) return;
            bool isMuted = VoiceChatManager.Instance.ToggleMutePlayer(_player.Owner.ClientId);
            mutedIcon.SetActive(isMuted);
            PlayPulseAnimation(isMuted ? Color.red : Color.green);
        }


        private void OnKickClicked()
        {
            if (_player == null) return;

            ConfirmationDialog.Show(
                $"Kick {_player.PlayerName.Value}?",
                $"Are you sure you want to kick {_player.PlayerName.Value}?",
                (System.Action)(() =>
                {
                    var networkObject = _player.GetComponent<FishNet.Object.NetworkObject>();
                    var conn = networkObject?.Owner;

                    if (conn != null && conn.IsActive)
                    {
                        //  InstanceFinder.ServerManager.StopConnection(conn);
                        Debug.Log($"Kicked player {conn.ClientId}");
                    }
                    else
                    {
                        Debug.LogWarning("Could not get NetworkConnection from NetworkObject.Owner.");
                    }
                })
            );
        }

        private void OnReportClicked()
        {
            if (_player == null) return;
            ReportDialog.Show(_player.PlayerName.Value, _player.Owner.ClientId);
        }

        private void OnSpectateClicked()
        {
            if (_player == null) return;
            SpectatorManager.Instance.SpectatePlayer(_player.Owner.ClientId);
        }

        private void ShowContextMenu()
        {
            ContextMenuManager.ShowContextMenu(
                _player.PlayerName.Value,   // ✅ use SyncVar<string>.Value
                new ContextMenuOption[] {
            new ContextMenuOption("Whisper", OnWhisperClicked),
            new ContextMenuOption(_player.IsMuted.Value ? "Unmute" : "Mute", OnMuteClicked), // ✅ .Value
            new ContextMenuOption("Report", OnReportClicked),
            new ContextMenuOption("View Profile", () => ProfileManager.Instance.ShowProfile(_player.Owner.ClientId.ToString())), // ✅ convert int → string
            new ContextMenuOption("Spectate", OnSpectateClicked,_player.IsDead.Value) // ✅ .Value
            }
        );
        }

        #endregion

        #region Utility Methods
        private int GetPlayerPing()
        {
            // Implement ping retrieval based on your network system
            return UnityEngine.Random.Range(20, 150); // Placeholder
        }

        private Color GetPingColor(int ping)
        {
            if (ping < 50) return colors.lowPingColor;
            if (ping < 100) return colors.mediumPingColor;
            return colors.highPingColor;
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
            if (score > 1000) return colors.highScoreColor;
            return Color.white;
        }

        private Color GetKDAColor(int kills, int deaths)
        {
            float ratio = deaths > 0 ? (float)kills / deaths : kills;
            return ratio > 1f ? colors.positiveKDRColor : colors.negativeKDRColor;
        }

        private void ApplyTeamColor(Color teamColor)
        {
            if (teamIndicator != null)
            {
                teamIndicator.color = teamColor;
            }
        }

        private IEnumerator UpdateDataCoroutine()
        {
            while (_isInitialized)
            {
                if (_player != null && gameObject.activeInHierarchy)
                {
                    UpdatePing();
                    UpdateHealth();
                    UpdateMana();
                }
                yield return new WaitForSeconds(animationSettings.updateInterval);
            }
        }
        #endregion

        #region Public API
        public void ShowButtons(bool show)
        {
            if (whisperButton != null) whisperButton.gameObject.SetActive(show);
            if (muteButton != null) muteButton.gameObject.SetActive(show);
            if (kickButton != null) kickButton.gameObject.SetActive(show);
            if (reportButton != null) reportButton.gameObject.SetActive(show);
            if (spectateButton != null) spectateButton.gameObject.SetActive(show);
        }

        public void SetInteractable(bool interactable)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = interactable ? 1f : 0.5f;
                _canvasGroup.blocksRaycasts = interactable;
            }
        }

        public void RefreshData()
        {
            UpdateAllData();
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            if (_updateCoroutine != null)
                StopCoroutine(_updateCoroutine);

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            LeanTween.cancel(gameObject);
        }
        #endregion

        #region Editor Utilities
#if UNITY_EDITOR
        [ContextMenu("Debug - Test Animation")]
        private void DebugTestAnimation()
        {
            PlayPulseAnimation(Color.cyan);
        }

        [ContextMenu("Debug - Test Damage Effect")]
        private void DebugTestDamage()
        {
            PlayDamageEffect();
        }

        [ContextMenu("Debug - Test Heal Effect")]
        private void DebugTestHeal()
        {
            PlayHealEffect();
        }
#endif
        #endregion
    }
}