using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine.Rendering;

namespace zoya.game
{




    /// <summary>
    /// Advanced player health UI with network synchronization, animations, and comprehensive status display
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class AdvancedPlayerHealthUIElement : NetworkBehaviour
    {
        [System.Serializable]
        public class HealthUISettings
        {
            [Header("Main Health Display")]
            public bool showHealthBar = true;
            public bool showHealthText = true;
            public bool showHealthPercent = true;
            public bool showShieldBar = true;

            [Header("Animation Settings")]
            public float damageFlashDuration = 0.3f;
            public float healFlashDuration = 0.3f;
            public float lowHealthPulseSpeed = 2f;
            public float lowHealthThreshold = 0.3f;

            [Header("Color Settings")]
            public Color fullHealthColor = Color.green;
            public Color midHealthColor = Color.yellow;
            public Color lowHealthColor = Color.red;
            public Color shieldColor = new Color(0.2f, 0.6f, 1f, 0.8f);
            public Color damageFlashColor = new Color(1f, 0.2f, 0.2f, 0.5f);
            public Color healFlashColor = new Color(0.2f, 1f, 0.2f, 0.5f);

            [Header("Positioning")]
            public Vector2 screenOffset = new Vector2(0, 50f);
            public bool followPlayer = true;
            public float smoothFollowSpeed = 5f;
        }

        [System.Serializable]
        public class StatusEffectSettings
        {
            [Header("Status Icons")]
            public GameObject poisonIcon;
            public GameObject burnIcon;
            public GameObject stunIcon;
            public GameObject slowIcon;
            public GameObject invulnerableIcon;

            [Header("Buff Icons")]
            public GameObject speedBoostIcon;
            public GameObject damageBoostIcon;
            public GameObject defenseBoostIcon;
            public GameObject regenerationIcon;

            [Header("Team Indicator")]
            public Image teamIndicator;
            public Color[] teamColors = {
            new Color(0.8f, 0.8f, 0.8f, 0.8f), // Neutral
            new Color(0.2f, 0.4f, 1f, 0.8f),   // Blue
            new Color(1f, 0.3f, 0.3f, 0.8f),   // Red
            new Color(0.3f, 0.8f, 0.3f, 0.8f), // Green
            new Color(1f, 0.8f, 0.2f, 0.8f)    // Yellow
        };
        }

        [Header("UI References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider shieldSlider;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image shieldFillImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform uiRoot;

        [Header("Settings")]
        [SerializeField] private HealthUISettings healthSettings = new HealthUISettings();
        [SerializeField] private StatusEffectSettings statusSettings = new StatusEffectSettings();

        [Header("Advanced Effects")]
        [SerializeField] private ParticleSystem damageEffect;
        [SerializeField] private ParticleSystem healEffect;
        [SerializeField] private AudioClip damageSound;
        [SerializeField] private AudioClip healSound;
        [SerializeField] private AudioClip lowHealthAlertSound;

        // Network sync vars    
        private readonly SyncVar<float> _currentHealth = new SyncVar<float>();
        private readonly SyncVar<float> _maxHealth = new SyncVar<float>();
        private readonly SyncVar<float> _currentShield = new SyncVar<float>();
        private readonly SyncVar<float> _maxShield = new SyncVar<float>();
        private readonly SyncVar<int> _teamId = new SyncVar<int>();
        private readonly SyncVar<string> _playerName = new SyncVar<string>();

        // Runtime variables
        private AdvancedNetworkPlayerController _targetPlayer;
        private Camera _mainCamera;
        private AudioSource _audioSource;
        private Coroutine _damageCoroutine;
        private Coroutine _healCoroutine;
        private Coroutine _lowHealthCoroutine;
        private bool _isLowHealth;
        private float _lastHealthValue;
        private float _lastShieldValue;

        // Status effect tracking
        private bool _hasPoison;
        private bool _hasBurn;
        private bool _hasStun;
        private bool _hasSlow;
        private bool _isInvulnerable;
        private bool _hasSpeedBoost;
        private bool _hasDamageBoost;
        private bool _hasDefenseBoost;
        private bool _hasRegeneration;

        public AdvancedNetworkPlayerController TargetPlayer => _targetPlayer;
        public float HealthPercent => _maxHealth.Value > 0 ? _currentHealth.Value / _maxHealth.Value : 0f;
        public float ShieldPercent => _maxShield.Value > 0 ? _currentShield.Value / _maxShield.Value : 0f;

        #region Initialization
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            _mainCamera = Camera.main;
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            InitializeUI();
        }

        [Server]
        public void InitializeForPlayer(AdvancedNetworkPlayerController player, int teamId)
        {
            if (player == null) return;

            _targetPlayer = player;
            _teamId.Value = teamId;
            _playerName.Value = player.PlayerName.Value;
            _maxHealth.Value = player.maxHealth;
            _currentHealth.Value = player.Health.Value;
            _maxShield.Value = player.MaxShield;
            _currentShield.Value = player.CurrentShield;

            // Sync initial values to clients
            UpdateHealthDisplay(_currentHealth.Value, _maxHealth.Value);
            UpdateShieldDisplay(_currentShield.Value, _maxShield.Value);
            UpdateTeamIndicator(_teamId.Value);
            UpdatePlayerName(_playerName.Value);

            // Subscribe to player events
            player.OnHealthChanged += HandleHealthChanged;
            player.OnShieldChanged += HandleShieldChanged;
            player.OnStatusEffectChanged += HandleStatusEffectChanged;
        }

        [ObserversRpc]
        private void InitializeUI()
        {
            // Set initial values
            if (healthSlider != null)
            {
                healthSlider.minValue = 0;
                healthSlider.maxValue = _maxHealth.Value;
                healthSlider.value = _currentHealth.Value;
            }

            if (shieldSlider != null)
            {
                shieldSlider.minValue = 0;
                shieldSlider.maxValue = _maxShield.Value;
                shieldSlider.value = _currentShield.Value;
            }

            UpdateHealthText();
            UpdateTeamIndicator(_teamId.Value);
            UpdatePlayerName(_playerName.Value);

            // Apply settings
            ApplyUISettings();
        }
        #endregion

        #region UI Update Methods
        private void Update()
        {
            if (!IsInitialized()) return;

            UpdatePosition();
            UpdateLowHealthEffect();
            UpdateStatusEffects();
        }

        private void UpdatePosition()
        {
            if (!healthSettings.followPlayer || _targetPlayer == null || uiRoot == null || _mainCamera == null)
                return;

            // Convert world position to screen position
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(_targetPlayer.transform.position);
            screenPos += new Vector3(healthSettings.screenOffset.x, healthSettings.screenOffset.y, 0);

            // Smooth follow
            uiRoot.position = Vector3.Lerp(uiRoot.position, screenPos, healthSettings.smoothFollowSpeed * Time.deltaTime);

            // Face camera
            uiRoot.rotation = Quaternion.LookRotation(_mainCamera.transform.forward);
        }

        private void UpdateHealthText()
        {
            if (healthText == null) return;

            string text = "";

            if (healthSettings.showHealthText)
            {
                text += $"{Mathf.CeilToInt(_currentHealth.Value)}";
            }

            if (healthSettings.showHealthPercent)
            {
                if (healthSettings.showHealthText) text += " / ";
                text += $"{HealthPercent:P0}";
            }

            if (healthSettings.showShieldBar && _currentShield.Value > 0)
            {
                text += $" (+{Mathf.CeilToInt(_currentShield.Value)})";
            }

            healthText.text = text;
        }

        private void UpdateTeamIndicator(int teamId)
        {
            if (statusSettings.teamIndicator == null) return;

            if (teamId >= 0 && teamId < statusSettings.teamColors.Length)
            {
                statusSettings.teamIndicator.color = statusSettings.teamColors[teamId];
                statusSettings.teamIndicator.gameObject.SetActive(true);
            }
            else
            {
                statusSettings.teamIndicator.gameObject.SetActive(false);
            }
        }
        private void UpdateLowHealthEffect()
        {
            bool isLowHealth = HealthPercent <= healthSettings.lowHealthThreshold;

            if (isLowHealth != _isLowHealth)
            {
                _isLowHealth = isLowHealth;

                if (_isLowHealth)
                {
                    if (_lowHealthCoroutine != null)
                        StopCoroutine(_lowHealthCoroutine);
                    _lowHealthCoroutine = StartCoroutine(LowHealthPulseCoroutine());

                    PlaySound(lowHealthAlertSound);
                }
                else
                {
                    if (_lowHealthCoroutine != null)
                    {
                        StopCoroutine(_lowHealthCoroutine);
                        _lowHealthCoroutine = null;
                    }

                    if (healthFillImage != null)
                        healthFillImage.color = GetHealthColor(HealthPercent);
                }
            }
        }

        private void UpdateStatusEffects()
        {
            // Update status effect icons
            SetStatusIcon(statusSettings.poisonIcon, _hasPoison);
            SetStatusIcon(statusSettings.burnIcon, _hasBurn);
            SetStatusIcon(statusSettings.stunIcon, _hasStun);
            SetStatusIcon(statusSettings.slowIcon, _hasSlow);
            SetStatusIcon(statusSettings.invulnerableIcon, _isInvulnerable);

            // Update buff icons
            SetStatusIcon(statusSettings.speedBoostIcon, _hasSpeedBoost);
            SetStatusIcon(statusSettings.damageBoostIcon, _hasDamageBoost);
            SetStatusIcon(statusSettings.defenseBoostIcon, _hasDefenseBoost);
            SetStatusIcon(statusSettings.regenerationIcon, _hasRegeneration);
        }

        private void SetStatusIcon(GameObject icon, bool active)
        {
            if (icon != null)
                icon.SetActive(active);
        }
        #endregion

        #region Event Handlers
        [Server]
        private void HandleHealthChanged(float currentHealth, float maxHealth, float delta)
        {
            _currentHealth.Value = currentHealth;
            _maxHealth.Value = maxHealth;

            // Sync to clients
            UpdateHealthDisplay(currentHealth, maxHealth);

            // Play effects based on damage/heal
            if (delta < 0)
            {
                PlayDamageEffects(Mathf.Abs(delta));
            }
            else if (delta > 0)
            {
                PlayHealEffects(delta);
            }
        }

        [Server]
        private void HandleShieldChanged(float currentShield, float maxShield, float delta)
        {
            _currentShield.Value = currentShield;
            _maxShield.Value = maxShield;

            UpdateShieldDisplay(currentShield, maxShield);
        }

        [Server]
        private void HandleStatusEffectChanged(string effectType, bool isActive, float duration)
        {
            switch (effectType.ToLower())
            {
                case "poison": _hasPoison = isActive; break;
                case "burn": _hasBurn = isActive; break;
                case "stun": _hasStun = isActive; break;
                case "slow": _hasSlow = isActive; break;
                case "invulnerable": _isInvulnerable = isActive; break;
                case "speedboost": _hasSpeedBoost = isActive; break;
                case "damageboost": _hasDamageBoost = isActive; break;
                case "defenseboost": _hasDefenseBoost = isActive; break;
                case "regeneration": _hasRegeneration = isActive; break;
            }

            UpdateStatusEffectsRpc(effectType, isActive);
        }

        [ObserversRpc]
        private void UpdateStatusEffectsRpc(string effectType, bool isActive)
        {
            switch (effectType.ToLower())
            {
                case "poison": _hasPoison = isActive; break;
                case "burn": _hasBurn = isActive; break;
                case "stun": _hasStun = isActive; break;
                case "slow": _hasSlow = isActive; break;
                case "invulnerable": _isInvulnerable = isActive; break;
                case "speedboost": _hasSpeedBoost = isActive; break;
                case "damageboost": _hasDamageBoost = isActive; break;
                case "defenseboost": _hasDefenseBoost = isActive; break;
                case "regeneration": _hasRegeneration = isActive; break;
            }
        }

        [ObserversRpc]
        private void UpdateHealthDisplay(float currentHealth, float maxHealth)
        {
            _currentHealth.Value = currentHealth;
            _maxHealth.Value = maxHealth;

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }

            if (healthFillImage != null)
            {
                healthFillImage.color = GetHealthColor(HealthPercent);
            }

            UpdateHealthText();

            // Check for low health
            UpdateLowHealthEffect();
        }

        [ObserversRpc]
        private void UpdateShieldDisplay(float currentShield, float maxShield)
        {
            _currentShield.Value = currentShield;
            _maxShield.Value = maxShield;

            if (shieldSlider != null)
            {
                shieldSlider.maxValue = maxShield;
                shieldSlider.value = currentShield;
                shieldSlider.gameObject.SetActive(currentShield > 0);
            }

            if (shieldFillImage != null)
            {
                shieldFillImage.color = healthSettings.shieldColor;
            }

            UpdateHealthText();
        }
        #endregion

        #region Visual Effects
        [ObserversRpc]
        private void PlayDamageEffects(float damageAmount)
        {
            if (_damageCoroutine != null)
                StopCoroutine(_damageCoroutine);
            _damageCoroutine = StartCoroutine(DamageEffectCoroutine(damageAmount));
        }

        [ObserversRpc]
        private void PlayHealEffects(float healAmount)
        {
            if (_healCoroutine != null)
                StopCoroutine(_healCoroutine);
            _healCoroutine = StartCoroutine(HealEffectCoroutine(healAmount));
        }

        private IEnumerator DamageEffectCoroutine(float damageAmount)
        {
            // Flash effect
            if (healthFillImage != null)
            {
                Color originalColor = healthFillImage.color;
                healthFillImage.color = healthSettings.damageFlashColor;

                yield return new WaitForSeconds(healthSettings.damageFlashDuration);

                healthFillImage.color = originalColor;
            }

            // Play damage particle effect
            if (damageEffect != null)
            {
                damageEffect.Play();
            }

            // Play damage sound
            PlaySound(damageSound);

            _damageCoroutine = null;
        }

        private IEnumerator HealEffectCoroutine(float healAmount)
        {
            // Flash effect
            if (healthFillImage != null)
            {
                Color originalColor = healthFillImage.color;
                healthFillImage.color = healthSettings.healFlashColor;

                yield return new WaitForSeconds(healthSettings.healFlashDuration);

                healthFillImage.color = GetHealthColor(HealthPercent);
            }

            // Play heal particle effect
            if (healEffect != null)
            {
                healEffect.Play();
            }

            // Play heal sound
            PlaySound(healSound);

            _healCoroutine = null;
        }

        private IEnumerator LowHealthPulseCoroutine()
        {
            while (_isLowHealth && healthFillImage != null)
            {
                float pulse = Mathf.PingPong(Time.time * healthSettings.lowHealthPulseSpeed, 1f);
                Color pulseColor = Color.Lerp(healthSettings.lowHealthColor, healthSettings.damageFlashColor, pulse);
                healthFillImage.color = pulseColor;

                yield return null;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private Color GetHealthColor(float healthPercent)
        {
            if (healthPercent > 0.6f)
                return healthSettings.fullHealthColor;
            else if (healthPercent > 0.3f)
                return healthSettings.midHealthColor;
            else
                return healthSettings.lowHealthColor;
        }
        #endregion

        #region Utility Methods
        private bool IsInitialized()
        {
            return _targetPlayer != null && canvasGroup != null;
        }

        private void ApplyUISettings()
        {
            if (healthSlider != null)
                healthSlider.gameObject.SetActive(healthSettings.showHealthBar);

            if (shieldSlider != null)
                shieldSlider.gameObject.SetActive(healthSettings.showShieldBar);

            if (healthText != null)
                healthText.gameObject.SetActive(healthSettings.showHealthText || healthSettings.showHealthPercent);

            if (healthFillImage != null)
                healthFillImage.color = GetHealthColor(HealthPercent);

            if (shieldFillImage != null)
                shieldFillImage.color = healthSettings.shieldColor;
        }

        public void SetVisibility(bool visible, float fadeDuration = 0.3f)
        {
            if (canvasGroup == null) return;

            StopAllCoroutines();
            StartCoroutine(FadeCanvasGroup(visible ? 1f : 0f, fadeDuration));
        }

        private IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }
        #endregion

        #region Public API
        [Server]
        public void UpdatePlayerTeam(int newTeamId)
        {
            _teamId.Value = newTeamId;
            UpdateTeamIndicatorRpc(newTeamId);
        }

        [ObserversRpc]
        private void UpdateTeamIndicatorRpc(int teamId)
        {
            UpdateTeamIndicator(teamId);
        }

        [Server]
        public void UpdatePlayerName(string newName)
        {
            _playerName.Value = newName;
            UpdatePlayerNameRpc(newName);
        }

        [ObserversRpc]
        private void UpdatePlayerNameRpc(string playerName)
        {
            UpdatePlayerName(playerName);
        }

        public void SetScreenOffset(Vector2 offset)
        {
            healthSettings.screenOffset = offset;
        }

        public void SetFollowPlayer(bool follow)
        {
            healthSettings.followPlayer = follow;
        }
        #endregion

        #region Cleanup
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Clean up coroutines
            if (_damageCoroutine != null)
                StopCoroutine(_damageCoroutine);

            if (_healCoroutine != null)
                StopCoroutine(_healCoroutine);

            if (_lowHealthCoroutine != null)
                StopCoroutine(_lowHealthCoroutine);

            // Unsubscribe from events
            if (_targetPlayer != null)
            {
                _targetPlayer.OnHealthChanged -= HandleHealthChanged;
                _targetPlayer.OnShieldChanged -= HandleShieldChanged;
                _targetPlayer.OnStatusEffectChanged -= HandleStatusEffectChanged;
            }
        }

        [Server]
        public void DestroyUI()
        {
            // Clean up on server before despawning
            if (_targetPlayer != null)
            {
                _targetPlayer.OnHealthChanged -= HandleHealthChanged;
                _targetPlayer.OnShieldChanged -= HandleShieldChanged;
                _targetPlayer.OnStatusEffectChanged -= HandleStatusEffectChanged;
            }

            base.Despawn();
        }
        #endregion

        #region Editor Utilities
#if UNITY_EDITOR
        [ContextMenu("Test Damage Effect")]
        private void TestDamageEffect()
        {
            PlayDamageEffects(10f);
        }

        [ContextMenu("Test Heal Effect")]
        private void TestHealEffect()
        {
            PlayHealEffects(10f);
        }

        [ContextMenu("Toggle Visibility")]
        private void ToggleVisibility()
        {
            SetVisibility(canvasGroup.alpha < 0.5f);
        }
#endif
        #endregion
    }
}