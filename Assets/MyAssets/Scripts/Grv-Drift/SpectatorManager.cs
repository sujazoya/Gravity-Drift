using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class SpectatorManager : NetworkBehaviour
{
    public static SpectatorManager Instance { get; private set; }

    [System.Serializable]
    public class SpectatorSettings
    {
        public float cameraSmoothness = 5f;
        public float freeCameraSpeed = 10f;
        public float freeCameraFastSpeed = 20f;
        public float mouseSensitivity = 2f;
        public bool showSpectatorUI = true;
        public bool allowFreeCamera = true;
        public bool autoSpectateOnDeath = true;
    }

    [Header("UI References")]
    [SerializeField] private GameObject spectatorPanel;
    [SerializeField] private TMP_Text spectatedPlayerText;
    [SerializeField] private TMP_Text spectatorModeText;
    [SerializeField] private Button nextPlayerButton;
    [SerializeField] private Button previousPlayerButton;
    [SerializeField] private Button freeCameraButton;
    [SerializeField] private Button exitSpectatorButton;

    [Header("Camera References")]
    [SerializeField] private Camera spectatorCamera;
    [SerializeField] private Camera freeCamera;
    [SerializeField] private Transform freeCameraTransform;

    [Header("Settings")]
    [SerializeField] private SpectatorSettings settings = new SpectatorSettings();

    private List<AdvancedNetworkPlayerController> _spectatablePlayers = new List<AdvancedNetworkPlayerController>();
    private int _currentSpectatedIndex = -1;
    private bool _isSpectating = false;
    private bool _isFreeCamera = false;
    private AdvancedNetworkPlayerController _currentSpectatedPlayer;
    private Vector2 _freeCameraRotation;

    public bool IsSpectating => _isSpectating;
    public bool IsFreeCamera => _isFreeCamera;

    public event System.Action OnSpectatorModeStarted;
    public event System.Action OnSpectatorModeEnded;
    public event System.Action<AdvancedNetworkPlayerController> OnSpectatedPlayerChanged;

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
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        SetupUI();
        SetSpectatorCameraActive(false);
        SetFreeCameraActive(false);
    }

    private void SetupUI()
    {
        if (nextPlayerButton != null) nextPlayerButton.onClick.AddListener(NextPlayer);
        if (previousPlayerButton != null) previousPlayerButton.onClick.AddListener(PreviousPlayer);
        if (freeCameraButton != null) freeCameraButton.onClick.AddListener(ToggleFreeCamera);
        if (exitSpectatorButton != null) exitSpectatorButton.onClick.AddListener(ExitSpectatorMode);

        if (spectatorPanel != null) spectatorPanel.SetActive(false);
    }
    #endregion

    #region Public API
    public void StartSpectatorMode()
    {
        if (_isSpectating) return;

        _isSpectating = true;
        UpdateSpectatablePlayers();
        
        if (_spectatablePlayers.Count > 0)
        {
            SpectatePlayer(0);
        }
        else
        {
            StartFreeCamera();
        }

        SetSpectatorUI(true);
        OnSpectatorModeStarted?.Invoke();
    }

    public void ExitSpectatorMode()
    {
        if (!_isSpectating) return;

        _isSpectating = false;
        _isFreeCamera = false;

        SetSpectatorCameraActive(false);
        SetFreeCameraActive(false);
        SetSpectatorUI(false);

        OnSpectatorModeEnded?.Invoke();
    }

    public void SpectatePlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= _spectatablePlayers.Count) return;

        _currentSpectatedIndex = playerIndex;
        _currentSpectatedPlayer = _spectatablePlayers[playerIndex];
        _isFreeCamera = false;

        SetSpectatorCameraActive(true);
        SetFreeCameraActive(false);

        UpdateSpectatorUI();
        OnSpectatedPlayerChanged?.Invoke(_currentSpectatedPlayer);
    }

    public void SpectatePlayer(NetworkConnection conn)
    {
        for (int i = 0; i < _spectatablePlayers.Count; i++)
        {
            if (_spectatablePlayers[i].Owner.ClientId == conn.ClientId)
            {
                SpectatePlayer(i);
                return;
            }
        }
    }

    public void StartFreeCamera()
    {
        if (!settings.allowFreeCamera) return;

        _isFreeCamera = true;
        _currentSpectatedPlayer = null;

        SetSpectatorCameraActive(false);
        SetFreeCameraActive(true);

        // Position free camera at a good starting point
        if (freeCameraTransform != null && _spectatablePlayers.Count > 0)
        {
            freeCameraTransform.position = _spectatablePlayers[0].transform.position + Vector3.up * 5f;
            freeCameraTransform.rotation = Quaternion.LookRotation(Vector3.down);
        }

        UpdateSpectatorUI();
    }

    public void NextPlayer()
    {
        if (_spectatablePlayers.Count == 0) return;

        int nextIndex = (_currentSpectatedIndex + 1) % _spectatablePlayers.Count;
        SpectatePlayer(nextIndex);
    }

    public void PreviousPlayer()
    {
        if (_spectatablePlayers.Count == 0) return;

        int prevIndex = (_currentSpectatedIndex - 1 + _spectatablePlayers.Count) % _spectatablePlayers.Count;
        SpectatePlayer(prevIndex);
    }

    public void ToggleFreeCamera()
    {
        if (_isFreeCamera)
        {
            if (_spectatablePlayers.Count > 0)
            {
                SpectatePlayer(0);
            }
        }
        else
        {
            StartFreeCamera();
        }
    }
    #endregion

    #region Player Management
    public void AddSpectatablePlayer(AdvancedNetworkPlayerController player)
    {
        if (!_spectatablePlayers.Contains(player))
        {
            _spectatablePlayers.Add(player);
            player.OnDeath += HandlePlayerDeath;
        }
    }

    public void RemoveSpectatablePlayer(AdvancedNetworkPlayerController player)
    {
        if (_spectatablePlayers.Contains(player))
        {
            player.OnDeath -= HandlePlayerDeath;
            _spectatablePlayers.Remove(player);

            if (_currentSpectatedPlayer == player)
            {
                NextPlayer();
            }
        }
    }

    private void UpdateSpectatablePlayers()
    {
        _spectatablePlayers.Clear();
        
        // Find all active players
        var allPlayers = Object.FindObjectsByType<AdvancedNetworkPlayerController>(
         FindObjectsSortMode.None
         );      
        foreach (var player in allPlayers)
        {
            if (player != null && player.Owner != null && player.gameObject.activeInHierarchy)
            {
                AddSpectatablePlayer(player);
            }
        }

        // Remove local player if they're alive
        _spectatablePlayers.RemoveAll(p => p.Owner.IsLocalClient && !p.IsDead.Value);
    }

    private void HandlePlayerDeath(AdvancedNetworkPlayerController player)
    {
        if (settings.autoSpectateOnDeath && player.Owner.IsLocalClient)
        {
            StartSpectatorMode();
        }

        if (_currentSpectatedPlayer == player)
        {
            NextPlayer();
        }
    }
    #endregion

    #region Camera Management
    private void Update()
    {
        if (!_isSpectating) return;

        if (_isFreeCamera)
        {
            UpdateFreeCamera();
        }
        else if (_currentSpectatedPlayer != null)
        {
            UpdateSpectatorCamera();
        }

        HandleSpectatorInput();
    }

    private void UpdateSpectatorCamera()
    {
        if (spectatorCamera == null || _currentSpectatedPlayer == null) return;

        // Smoothly follow the spectated player
        Vector3 targetPosition = _currentSpectatedPlayer.transform.position + Vector3.up * 2f;
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        spectatorCamera.transform.position = Vector3.Lerp(
            spectatorCamera.transform.position, 
            targetPosition, 
            settings.cameraSmoothness * Time.deltaTime
        );

        spectatorCamera.transform.rotation = Quaternion.Slerp(
            spectatorCamera.transform.rotation, 
            targetRotation, 
            settings.cameraSmoothness * Time.deltaTime
        );
    }

    private void UpdateFreeCamera()
    {
        if (freeCameraTransform == null) return;

        // Handle free camera movement
        float speed = Input.GetKey(KeyCode.LeftShift) ? settings.freeCameraFastSpeed : settings.freeCameraSpeed;
        Vector3 movement = new Vector3(
            Input.GetAxis("Horizontal"),
            0f,
            Input.GetAxis("Vertical")
        );

        // Vertical movement with Q/E keys
        if (Input.GetKey(KeyCode.Q)) movement.y = -1f;
        if (Input.GetKey(KeyCode.E)) movement.y = 1f;

        freeCameraTransform.Translate(movement * speed * Time.deltaTime);

        // Mouse look
        if (Input.GetMouseButton(1)) // Right mouse button
        {
            _freeCameraRotation.x += Input.GetAxis("Mouse X") * settings.mouseSensitivity;
            _freeCameraRotation.y -= Input.GetAxis("Mouse Y") * settings.mouseSensitivity;
            _freeCameraRotation.y = Mathf.Clamp(_freeCameraRotation.y, -89f, 89f);

            freeCameraTransform.rotation = Quaternion.Euler(_freeCameraRotation.y, _freeCameraRotation.x, 0f);
        }
    }

    private void HandleSpectatorInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NextPlayer();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            PreviousPlayer();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleFreeCamera();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitSpectatorMode();
        }
    }

    private void SetSpectatorCameraActive(bool active)
    {
        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(active);
        }
    }

    private void SetFreeCameraActive(bool active)
    {
        if (freeCamera != null && freeCameraTransform != null)
        {
            freeCamera.gameObject.SetActive(active);
            freeCameraTransform.gameObject.SetActive(active);
        }
    }
    #endregion

    #region UI Management
    private void SetSpectatorUI(bool active)
    {
        if (spectatorPanel != null) spectatorPanel.SetActive(active && settings.showSpectatorUI);
    }

    private void UpdateSpectatorUI()
    {
        if (spectatedPlayerText != null)
        {
            if (_isFreeCamera)
            {
                spectatedPlayerText.text = "FREE CAMERA";
            }
            else if (_currentSpectatedPlayer != null)
            {
                spectatedPlayerText.text = $"SPECTATING: {_currentSpectatedPlayer.PlayerName.Value}";
            }
            else
            {
                spectatedPlayerText.text = "NO PLAYERS AVAILABLE";
            }
        }

        if (spectatorModeText != null)
        {
            spectatorModeText.text = _isFreeCamera ? "Free Camera Mode" : "Player Spectate Mode";
        }

        // Update button interactability
        if (nextPlayerButton != null) nextPlayerButton.interactable = _spectatablePlayers.Count > 1;
        if (previousPlayerButton != null) previousPlayerButton.interactable = _spectatablePlayers.Count > 1;
        if (freeCameraButton != null) freeCameraButton.interactable = settings.allowFreeCamera;
    }
    #endregion

    #region Cleanup
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        // Clean up event subscriptions
        foreach (var player in _spectatablePlayers)
        {
            if (player != null)
            {
                player.OnDeath -= HandlePlayerDeath;
            }
        }
        
        _spectatablePlayers.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region Public Helper Methods
    public List<AdvancedNetworkPlayerController> GetSpectatablePlayers() => new List<AdvancedNetworkPlayerController>(_spectatablePlayers);
    
    public AdvancedNetworkPlayerController GetCurrentSpectatedPlayer() => _currentSpectatedPlayer;
    
    public bool CanSpectatePlayer(NetworkConnection conn)
    {
        return _spectatablePlayers.Exists(p => p.Owner.ClientId == conn.ClientId);
    }
    
    public int GetSpectatorCount()
    {
        return _spectatablePlayers.Count;
    }
    #endregion
}