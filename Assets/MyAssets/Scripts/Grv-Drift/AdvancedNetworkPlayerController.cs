// AdvancedNetworkPlayerController.cs
// FishNet v4.6.12R compatible
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using FishNet.Connection;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(AudioListener))]
public class AdvancedNetworkPlayerController : NetworkBehaviour
{
    #region Inspector - Movement / Camera / Audio
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float airControl = 0.3f;
    [SerializeField] private float slopeLimit = 45f;
    [SerializeField] private float stepOffset = 0.3f;

    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float cameraSmoothness = 10f;
    [SerializeField] private Vector2 cameraClamp = new Vector2(-89f, 89f);
    [SerializeField] private float fovSprintMultiplier = 1.1f;
    [SerializeField] private float fovChangeSpeed = 4f;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private Vector3 crouchCameraOffset = new Vector3(0, -0.5f, 0);

    [Header("Head Bob Settings")]
    [SerializeField] private float walkBobFrequency = 1.5f;
    [SerializeField] private float walkBobAmplitude = 0.05f;
    [SerializeField] private float sprintBobFrequency = 2f;
    [SerializeField] private float sprintBobAmplitude = 0.08f;
    [SerializeField] private float crouchBobFrequency = 1f;
    [SerializeField] private float crouchBobAmplitude = 0.03f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private float footstepInterval = 0.5f;

    [Header("Animation Settings")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Rig animationRig;
    [SerializeField] private float aimWeight = 0f;
    [SerializeField] private float aimTransitionSpeed = 5f;
    #endregion

    #region Network / SyncVars (FishNet v4 style)
    public readonly SyncVar<string> PlayerName = new SyncVar<string>();
    public readonly SyncVar<int> TeamId = new SyncVar<int>();


    public readonly SyncVar<int> Score = new SyncVar<int>();
    public readonly SyncVar<bool> IsReady = new SyncVar<bool>();
    public readonly SyncVar<bool> IsDead = new SyncVar<bool>();
    public bool IsAlive => !IsDead.Value;
    public readonly SyncVar<bool> IsTalking = new SyncVar<bool>();
    public readonly SyncVar<bool> IsMuted = new SyncVar<bool>();
    public readonly SyncVar<string> ClassIcon = new SyncVar<string>();
    public readonly SyncVar<string> RankIcon = new SyncVar<string>();


    public int maxHealth = 100;
    public readonly SyncVar<int> Health = new SyncVar<int>();

    public readonly SyncVar<bool> IsChargingShot = new SyncVar<bool>();
    private float _currentCharge = 0f;
    public float maxCharge = 2.0f;
    public float chargeReleaseThreshold = 0.2f;

    public readonly SyncVar<bool> IsBoosting = new SyncVar<bool>();
    private float boostMultiplier = 1.5f;
    private Coroutine _boostCoroutine;

    // small network fields used for owner->server state send   
    private readonly SyncVar<Vector3> _networkPosition = new SyncVar<Vector3>();
    private readonly SyncVar<Quaternion> _networkRotation = new SyncVar<Quaternion>();
    private readonly SyncVar<float> _networkCameraRotationX = new SyncVar<float>();
    #endregion

    #region Events
    public event Action<int> OnHealthChangedUI;
    public event Action<int> OnScoreChangedUI;
    public event Action<int> OnTeamChangedUI;
    public event Action OnDiedUI;
    public event Action OnRespawnedUI;
    public event Action OnGainedBallPossession;
    public event Action OnLostBallPossession;
    #endregion

    #region Local fields
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _crouchAction;
    private InputAction _aimAction;
    private InputAction _chargeAction;

    private CharacterController _characterController;
    private Vector3 _moveDirection;
    private Vector3 _velocity;
    private float _verticalRotation;
    private float _defaultFov;
    private float _currentHeight;
    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isCrouching;
    private bool _isAiming;
    private float _footstepTimer;

    // local booleans mirrored to RPC
    private bool isCrouchingLocal;
    private bool isAimingLocal;

    private readonly int _speedHash = Animator.StringToHash("Speed");
    private readonly int _verticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int _isCrouchingHash = Animator.StringToHash("IsCrouching");
    private readonly int _isSprintingHash = Animator.StringToHash("IsSprinting");
    private readonly int _isAimingHash = Animator.StringToHash("IsAiming");
    #endregion
    #region Player Info Properties
    private DateTime _joinTime;

    #region Player Stats Properties

    #region Missing Properties for UI
    public new bool IsHostInitialized => base.IsHostInitialized;
    public int Ping { get; set; } = 0; // Add ping property
                                       // Add these if they don't exist
    public int Kills { get; private set; } = 0;
    public int Deaths { get; private set; } = 0;
    public int Assists { get; private set; } = 0;
    #endregion


    // Add these private fields
    private int _kills = 0;
    private int _deaths = 0;
    private int _assists = 0;

    [Server]
    public void AddKill()
    {
        _kills++;
        // You might want to make these SyncVars if they need to sync across network
    }

    [Server]
    public void AddDeath()
    {
        _deaths++;
    }
    #endregion


    public DateTime JoinTime
    {
        get => _joinTime;
        set => _joinTime = value;
    }
    #endregion    

    #region Lifecycle
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _characterController = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        _currentHeight = standHeight;
        if (playerCamera != null) _defaultFov = playerCamera.fieldOfView;

        _joinTime = DateTime.Now;

        // SyncVar hooks
        Health.OnChange += HandleHealthChanged;
        Score.OnChange += HandleScoreChanged;
        TeamId.OnChange += HandleTeamChanged;
        IsDead.OnChange += HandleIsDeadChanged;
        Mana.OnChange += HandleManaChanged;

        if (IsServerInitialized)
        {
            Health.Value = maxHealth;
            Mana.Value = maxMana; // Add this line
            Score.Value = 0;
            TeamId.Value = 0;
            IsDead.Value = false;

            Mana.Value = maxMana;
            _lastManaUseTime = Time.time;

            // Start mana regeneration
            if (_manaRegenCoroutine != null)
                StopCoroutine(_manaRegenCoroutine);
            _manaRegenCoroutine = StartCoroutine(ManaRegenerationCoroutine());
        }


        // Local owner init
        if (base.Owner != null && base.Owner.IsLocalClient)
        {
            InitializeInput();
            InitializeCamera();
            InitializeAudio();

            if (playerAnimator != null)
                playerAnimator.SetLayerWeight(1, 1f);

            StartCoroutine(NetworkUpdateCoroutine());
        }
        else
        {
            if (playerCamera != null) playerCamera.enabled = false;
            var al = GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
            if (_playerInput != null) _playerInput.enabled = false;
        }

#if UNITY_2023_2_OR_NEWER
        var pl = UnityEngine.Object.FindFirstObjectByType<AdvancedPlayerListUI>();
#else
        var pl = UnityEngine.Object.FindObjectOfType<AdvancedPlayerListUI>();
#endif
        pl?.TryAddPlayer(this);
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        Health.OnChange -= HandleHealthChanged;
        Score.OnChange -= HandleScoreChanged;
        TeamId.OnChange -= HandleTeamChanged;
        IsDead.OnChange -= HandleIsDeadChanged;
        if (_manaRegenCoroutine != null)
        {
            StopCoroutine(_manaRegenCoroutine);
            _manaRegenCoroutine = null;
        }

        Mana.OnChange -= HandleManaChanged;

#if UNITY_2023_2_OR_NEWER
        var pl = UnityEngine.Object.FindFirstObjectByType<AdvancedPlayerListUI>();
#else
        var pl = UnityEngine.Object.FindObjectOfType<PlayerListUI>();
#endif
        pl?.RemovePlayer(Owner.ClientId);
    }
    #endregion

    #region Initialization helpers
    private void InitializeInput()
    {
        if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null) return;

        _moveAction = _playerInput.actions.FindAction("Move", throwIfNotFound: false);
        _lookAction = _playerInput.actions.FindAction("Look", throwIfNotFound: false);
        _jumpAction = _playerInput.actions.FindAction("Jump", throwIfNotFound: false);
        _sprintAction = _playerInput.actions.FindAction("Sprint", throwIfNotFound: false);
        _crouchAction = _playerInput.actions.FindAction("Crouch", throwIfNotFound: false);
        _aimAction = _playerInput.actions.FindAction("Aim", throwIfNotFound: false);
        _chargeAction = _playerInput.actions.FindAction("Charge", throwIfNotFound: false);

        if (_jumpAction != null) _jumpAction.performed += OnJumpPerformed;
        if (_sprintAction != null) { _sprintAction.performed += OnSprintPerformed; _sprintAction.canceled += OnSprintCanceled; }
        if (_crouchAction != null) { _crouchAction.performed += OnCrouchPerformed; }
        if (_aimAction != null) { _aimAction.performed += OnAimPerformed; _aimAction.canceled += OnAimCanceled; }
        if (_chargeAction != null) { _chargeAction.performed += OnChargePerformed; _chargeAction.canceled += OnChargeCanceled; }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void InitializeCamera()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null) playerCamera.enabled = true;
    }

    private void InitializeAudio()
    {
        if (movementAudioSource == null)
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
            movementAudioSource.spatialBlend = 1f;
            movementAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            movementAudioSource.maxDistance = 20f;
        }
    }
    #endregion

    #region Update / Movement
    private void Update()
    {
        if (!(base.Owner != null && base.Owner.IsLocalClient)) return;

        HandleInput();
        HandleMovement();
        HandleCamera();
        HandleCrouching();
        HandleAiming();
        HandleHeadBob();
        HandleFootsteps();
        UpdateAnimations();

        // charging local accumulation
        if (IsChargingShot.Value)
        {
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Min(_currentCharge, maxCharge);
        }

        // jump example using CheckGroundStatus
        bool isGrounded = CheckGroundStatus();
        _isGrounded = isGrounded;
    }

    private bool CheckGroundStatus()
    {
        if (_characterController != null)
            return _characterController.isGrounded;

        float rayDistance = 1.1f;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, out _, rayDistance);
    }

    private void HandleInput()
    {
        if (_moveAction == null || _lookAction == null) return;

        Vector2 moveInput = _moveAction.ReadValue<Vector2>();
        Vector2 lookInput = _lookAction.ReadValue<Vector2>();

        _moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        _moveDirection = transform.TransformDirection(_moveDirection);

        _verticalRotation -= lookInput.y * mouseSensitivity;
        _verticalRotation = Mathf.Clamp(_verticalRotation, cameraClamp.x, cameraClamp.y);
    }

    private void HandleMovement()
    {
        float currentSpeed = GetCurrentSpeed();
        if (IsBoosting.Value) currentSpeed *= boostMultiplier;
        Vector3 desiredVelocity = _moveDirection * currentSpeed;

        if (!_isGrounded) desiredVelocity *= airControl;

        if (_isGrounded && _velocity.y < 0f) _velocity.y = -2f;
        else _velocity.y -= gravity * Time.deltaTime;

        Vector3 finalVelocity = new Vector3(desiredVelocity.x, _velocity.y, desiredVelocity.z);
        _characterController.Move(finalVelocity * Time.deltaTime);
    }

    private float GetCurrentSpeed()
    {
        if (TryGetComponent<Rigidbody>(out var rb))
            return rb.linearVelocity.magnitude;

        if (TryGetComponent<CharacterController>(out var cc))
            return cc.velocity.magnitude;

        return 0f;
    }

    private void HandleCamera()
    {
        if (playerCamera == null) return;

        float currentXRotation = playerCamera.transform.localEulerAngles.x;
        if (currentXRotation > 180f) currentXRotation -= 360f;

        float smoothedXRotation = Mathf.LerpAngle(currentXRotation, _verticalRotation, cameraSmoothness * Time.deltaTime);
        playerCamera.transform.localEulerAngles = new Vector3(smoothedXRotation, 0f, 0f);

        float targetFov = _isSprinting ? _defaultFov * fovSprintMultiplier : _defaultFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fovChangeSpeed * Time.deltaTime);

        Vector3 targetCameraPosition = _isCrouching ? crouchCameraOffset : Vector3.zero;
        playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, targetCameraPosition, crouchTransitionSpeed * Time.deltaTime);
    }
    #endregion

    #region Crouch/Aim/Headbob/Footsteps/Animation
    private void HandleCrouching()
    {
        // We drive local boolean and send server RPC later
        // Toggle on local input handler (we hook input events)
        _isCrouching = isCrouchingLocal;
        // affect character controller height smoothly
        float targetHeight = _isCrouching ? crouchHeight : standHeight;
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        _characterController.height = _currentHeight;
        _characterController.center = new Vector3(0f, _currentHeight / 2f, 0f);

        // Update animator
        playerAnimator?.SetBool(_isCrouchingHash, _isCrouching);

        // Send state to server occasionally or from input event
        // Here we call a ServerRpc from owner to notify server then server will broadcast via RpcReceiveState
    }

    private void HandleAiming()
    {
        _isAiming = isAimingLocal;
        animationRig.weight = Mathf.Lerp(animationRig.weight, _isAiming ? 1f : 0f, aimTransitionSpeed * Time.deltaTime);
        playerAnimator?.SetBool(_isAimingHash, _isAiming);
    }

    private void HandleHeadBob()
    {
        if (!_isGrounded || _moveDirection.magnitude < 0.1f) return;
        float frequency = _isSprinting ? sprintBobFrequency : _isCrouching ? crouchBobFrequency : walkBobFrequency;
        float amplitude = _isSprinting ? sprintBobAmplitude : _isCrouching ? crouchBobAmplitude : walkBobAmplitude;
        float bobOffset = Mathf.Sin(Time.time * frequency) * amplitude;
        if (playerCamera != null) playerCamera.transform.localPosition += new Vector3(0f, bobOffset, 0f);
    }

    private void HandleFootsteps()
    {
        if (!_isGrounded || _moveDirection.magnitude < 0.1f) return;
        _footstepTimer += Time.deltaTime;
        float stepInterval = footstepInterval / (_isSprinting ? 1.5f : 1f);
        if (_footstepTimer >= stepInterval) { PlayFootstepSound(); _footstepTimer = 0f; }
    }

    private void UpdateAnimations()
    {
        if (playerAnimator == null) return;
        float speed = _moveDirection.magnitude * (_isSprinting ? 1.5f : 1f);
        playerAnimator.SetFloat(_speedHash, speed);
        playerAnimator.SetFloat(_verticalVelocityHash, _velocity.y);
        playerAnimator.SetBool(_isGroundedHash, _isGrounded);
        playerAnimator.SetBool(_isCrouchingHash, _isCrouching);
        playerAnimator.SetBool(_isSprintingHash, _isSprinting);
        playerAnimator.SetBool(_isAimingHash, _isAiming);
    }
    #endregion

    #region Input Handlers
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (_isGrounded && !_isCrouching)
        {
            _velocity.y = jumpForce;
            PlayJumpSound();
        }
    }

    private void OnSprintPerformed(InputAction.CallbackContext ctx) { _isSprinting = true; }
    private void OnSprintCanceled(InputAction.CallbackContext ctx) { _isSprinting = false; }
    private void OnCrouchPerformed(InputAction.CallbackContext ctx)
    {
        isCrouchingLocal = !isCrouchingLocal;
        // Let owner notify server of change
        if (base.Owner != null && base.Owner.IsLocalClient) CmdSendStateToServer(isCrouchingLocal, isAimingLocal);
    }
    private void OnAimPerformed(InputAction.CallbackContext ctx)
    {
        isAimingLocal = true;
        if (base.Owner != null && base.Owner.IsLocalClient) CmdSendStateToServer(isCrouchingLocal, isAimingLocal);
    }
    private void OnAimCanceled(InputAction.CallbackContext ctx)
    {
        isAimingLocal = false;
        if (base.Owner != null && base.Owner.IsLocalClient) CmdSendStateToServer(isCrouchingLocal, isAimingLocal);
    }

    private void OnChargePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsChargingShot.Value)
        {
            IsChargingShot.Value = true;
            _currentCharge = 0f;
            if (base.Owner != null && base.Owner.IsLocalClient) CmdStartCharge();
        }
    }

    private void OnChargeCanceled(InputAction.CallbackContext ctx)
    {
        if (IsChargingShot.Value)
        {
            IsChargingShot.Value = false;
            if (base.Owner != null && base.Owner.IsLocalClient) CmdReleaseCharge(_currentCharge);
            _currentCharge = 0f;
        }
    }
    #endregion

    #region Sounds
    private void PlayJumpSound() { if (jumpSound != null && movementAudioSource != null) movementAudioSource.PlayOneShot(jumpSound); }
    private void PlayLandSound() { if (landSound != null && movementAudioSource != null) movementAudioSource.PlayOneShot(landSound); }
    private void PlayFootstepSound() { if (footstepSounds != null && footstepSounds.Length > 0 && movementAudioSource != null) movementAudioSource.PlayOneShot(footstepSounds[UnityEngine.Random.Range(0, footstepSounds.Length)]); }
    #endregion

    #region Owner -> Server state sending & RPCs
    private IEnumerator NetworkUpdateCoroutine()
    {
        while (true)
        {
            if (base.Owner != null && base.Owner.IsLocalClient)
                SendStateToServer();
            yield return new WaitForSeconds(1f / 30f);
        }
    }

    [ServerRpc]
    private void SendStateToServer()
    {
        _networkPosition.Value = transform.position;
        _networkRotation.Value = transform.rotation;
        _networkCameraRotationX.Value = _verticalRotation;
        // Broadcast state: server will call observer RPC
        RpcReceiveState(isCrouchingLocal, isAimingLocal);
    }

    [ServerRpc]
    private void CmdSendStateToServer(bool crouch, bool aim)
    {
        isCrouchingLocal = crouch;
        isAimingLocal = aim;
        RpcReceiveState(crouch, aim);
    }

    [ObserversRpc]
    private void RpcReceiveState(bool crouch, bool aim)
    {
        // Applies on other clients (not owner). Owner already has local immediate state.
        playerAnimator?.SetBool(_isCrouchingHash, crouch);
        playerAnimator?.SetBool(_isAimingHash, aim);
    }
    #endregion

    #region Server gameplay methods / respawn / teleport
    [Server]
    public void ServerApplyDamage(int amount, NetworkConnection attacker = null)
    {
        if (!IsServerInitialized) return;
        int prev = Health.Value;
        Health.Value = Mathf.Clamp(Health.Value - Mathf.Abs(amount), 0, maxHealth);
        if (prev > 0 && Health.Value == 0)
        {
            IsDead.Value = true;
            var gm = AdvancedGameManager.Instance;
            if (gm != null) gm.OnPlayerKilled(Owner, attacker);
        }
    }

    [Server]
    public void ServerHeal(int amount)
    {
        if (!IsServerInitialized) return;
        int prev = Health.Value;
        Health.Value = Mathf.Clamp(Health.Value + Mathf.Abs(amount), 0, maxHealth);
        if (prev == 0 && Health.Value > 0)
        {
            IsDead.Value = false;
            OnRespawnedUI?.Invoke();
        }
    }

    [Server]
    public void SetTeam(int team)
    {
        if (!IsServerInitialized) return;
        TeamId.Value = team;
    }

    [Server]
    public void SetPlayerName(string name)
    {
        if (!IsServerInitialized) return;
        PlayerName.Value = name;
    }

    [Server]
    public void RespawnPlayer(Vector3 worldPosition)
    {
        if (!IsServerInitialized) return;
        Health.Value = maxHealth;
        IsDead.Value = false;
        // Broadcast teleport to all (observers) and also ensure owner gets teleport
        RpcTeleportObservers(worldPosition, Quaternion.identity);
        if (base.Owner != null) RpcTeleportOwner(base.Owner, worldPosition, Quaternion.identity);
        _velocity = Vector3.zero;
        OnRespawnedUI?.Invoke();
    }

    [ObserversRpc]
    private void RpcTeleportObservers(Vector3 pos, Quaternion rot)
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            _characterController.enabled = true;
        }
        else
        {
            transform.position = pos;
            transform.rotation = rot;
        }
    }

    [TargetRpc]
    private void RpcTeleportOwner(NetworkConnection conn, Vector3 pos, Quaternion rot)
    {
        // runs only on owner
        if (_characterController != null)
        {
            _characterController.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            _characterController.enabled = true;
        }
        else
        {
            transform.position = pos;
            transform.rotation = rot;
        }
    }
    #endregion

    #region Charge-shot ServerRPCs
    [ServerRpc]
    private void CmdStartCharge() { IsChargingShot.Value = true; }

    [ServerRpc]
    private void CmdReleaseCharge(float chargeAmount)
    {
        IsChargingShot.Value = false;
        float normalized = Mathf.Clamp01(chargeAmount / maxCharge);
        int damage = Mathf.RoundToInt(Mathf.Lerp(10, 60, normalized));
        _currentCharge = 0f;
        // implement projectile/spawn logic server-side here
    }

    [Server]
    public void ResetCharge()
    {
        IsChargingShot.Value = false;
        _currentCharge = 0f;
    }
    #endregion

    #region Possession events
    [Server]
    public void GrantPossession()
    {
        OnGainedBallPossession?.Invoke();
        RpcOnGainedPossession();
    }

    [Server]
    public void RemovePossession()
    {
        OnLostBallPossession?.Invoke();
        RpcOnLostPossession();
    }

    [ObserversRpc]
    private void RpcOnGainedPossession() => OnGainedBallPossession?.Invoke();

    [ObserversRpc]
    private void RpcOnLostPossession() => OnLostBallPossession?.Invoke();
    #endregion

    #region SyncVar handlers (UI forwarding)
    private void HandleHealthChanged(int prev, int next, bool asServer)
    {
        OnHealthChangedUI?.Invoke(next);
        if (prev > 0 && next == 0) OnDiedUI?.Invoke();
        if (prev == 0 && next > 0) OnRespawnedUI?.Invoke();
    }

    #region Health UI Support Properties
    public float MaxShield => 100f; // Adjust as needed
    public float CurrentShield => 50f; // Adjust as needed

    // Events for UI updates
    public event System.Action<float, float, float> OnHealthChanged;
    public event System.Action<float, float, float> OnShieldChanged;
    public event System.Action<string, bool, float> OnStatusEffectChanged;



    // Example method to trigger health changes
    [Server]
    public void ApplyDamage(int damage)
    {
        float oldHealth = Health.Value;
        Health.Value = Mathf.Max(0, Health.Value - damage);

        OnHealthChanged?.Invoke(Health.Value, maxHealth, oldHealth - Health.Value);
    }

    [Server]
    public void ApplyHeal(int healAmount)
    {
        float oldHealth = Health.Value;
        Health.Value = Mathf.Min(maxHealth, Health.Value + healAmount);

        OnHealthChanged?.Invoke(Health.Value, maxHealth, Health.Value - oldHealth);
    }
    #endregion

    private void HandleScoreChanged(int prev, int next, bool asServer) => OnScoreChangedUI?.Invoke(next);
    private void HandleTeamChanged(int prev, int next, bool asServer) => OnTeamChangedUI?.Invoke(next);
    private void HandleIsDeadChanged(bool prev, bool next, bool asServer) { if (next) OnDiedUI?.Invoke(); else OnRespawnedUI?.Invoke(); }
    #endregion

    #region Public helpers & cleanup

    #region Health Properties

    private int _maxHealth = 100;

    public int MaxHealth
    {
        get => _maxHealth;
        set
        {
            _maxHealth = Mathf.Max(1, value);
            // Adjust current health if needed
            if (Health.Value > _maxHealth)
            {
                Health.Value = _maxHealth;
            }
        }
    }

    public float HealthPercent => _maxHealth > 0 ? (float)Health.Value / _maxHealth : 0f;
    public bool IsAtFullHealth => Health.Value >= _maxHealth;

    public float GetHealthPercent()
    {
        return _maxHealth > 0 ? (float)Health.Value / _maxHealth : 0f;
    }

    [Server]
    public void SetMaxHealth(int newMaxHealth, bool healToFull = false)
    {
        int oldMaxHealth = _maxHealth;
        _maxHealth = Mathf.Max(1, newMaxHealth);

        if (healToFull)
        {
            Health.Value = _maxHealth;
        }
        else
        {
            // Scale current health proportionally
            float healthPercent = (float)Health.Value / oldMaxHealth;
            Health.Value = Mathf.RoundToInt(_maxHealth * healthPercent);
        }
    }

    #endregion

    public int GetTeam() => TeamId.Value;
    public bool GetIsDead() => IsDead.Value;
    public string GetPlayerName() => PlayerName.Value;
    public int GetScore() => Score.Value;
    /// <summary>
    /// Exposes the player's current charge (read-only).
    /// </summary>
    public float CurrentCharge => _currentCharge;

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (base.Owner != null && base.Owner.IsLocalClient)
        {
            if (_jumpAction != null) _jumpAction.performed -= OnJumpPerformed;
            if (_sprintAction != null) _sprintAction.performed -= OnSprintPerformed;
            if (_sprintAction != null) _sprintAction.canceled -= OnSprintCanceled;
            if (_crouchAction != null) _crouchAction.performed -= OnCrouchPerformed;
            if (_aimAction != null) _aimAction.performed -= OnAimPerformed;
            if (_aimAction != null) _aimAction.canceled -= OnAimCanceled;
            if (_chargeAction != null) { _chargeAction.performed -= OnChargePerformed; _chargeAction.canceled -= OnChargeCanceled; }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    #endregion
    public void InvokeLostBallPossession()
    {
        OnLostBallPossession?.Invoke();
    }
    public void InvokeGainedBallPossession()
    {
        OnGainedBallPossession?.Invoke();
    }
    #region Movement Control
    public void SetMovementEnabled(bool enabled)
    {
        if (_playerInput != null)
            _playerInput.enabled = enabled;

        if (_characterController != null)
            _characterController.enabled = enabled;
    }
    #endregion

    #region Teleportation
    public void Teleport(Vector3 position)
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }
        else
        {
            transform.position = position;
        }

        _velocity = Vector3.zero;
    }
    #endregion

    #region Respawn
    public void Respawn(Vector3 spawnPosition)
    {
        if (IsServerInitialized)
        {
            Health.Value = maxHealth;
            IsDead.Value = false;
            Teleport(spawnPosition);
            _velocity = Vector3.zero;
            OnRespawnedUI?.Invoke();
        }
        else if (IsOwner)
        {
            // Request respawn from server
            CmdRequestRespawn(spawnPosition);
        }
    }

    [ServerRpc]
    private void CmdRequestRespawn(Vector3 spawnPosition)
    {
        Respawn(spawnPosition);
    }
    #endregion

    #region Boost System
    public void ApplyBoost(float multiplier, float duration)
    {
        if (IsBoosting.Value) return;

        IsBoosting.Value = true;
        boostMultiplier = multiplier;

        if (_boostCoroutine != null)
            StopCoroutine(_boostCoroutine);

        _boostCoroutine = StartCoroutine(BoostDurationCoroutine(duration));
    }

    public void EndBoost()
    {
        if (!IsBoosting.Value) return;

        IsBoosting.Value = false;
        boostMultiplier = 1f;

        if (_boostCoroutine != null)
        {
            StopCoroutine(_boostCoroutine);
            _boostCoroutine = null;
        }
    }

    private IEnumerator BoostDurationCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        EndBoost();
    }
    #endregion

    #region Team Color
    public void SetTeamColor(Color teamColor)
    {
        // Apply team color to materials
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.materials)
            {
                if (material.HasProperty("_Color"))
                {
                    material.color = teamColor;
                }

                // Handle common shader properties
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", teamColor);
                }

                if (material.HasProperty("_MainColor"))
                {
                    material.SetColor("_MainColor", teamColor);
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", teamColor);
                    material.EnableKeyword("_EMISSION");
                }
            }
        }

        // Optional: Apply to UI elements like nameplates
        UpdateTeamUIElements(teamColor);

        // Sync across network if needed
        if (IsServerInitialized)
        {
            RpcSyncTeamColor(teamColor);
        }

    }
    #region Mana System

    [Header("Mana Settings")]
    public int maxMana = 100;
    public float manaRegenRate = 5f;
    public float manaRegenDelay = 2f;

    // SyncVar for mana
    public readonly SyncVar<float> Mana = new SyncVar<float>();

    /*
    // Check if player has enough mana
    if (player.HasMana(25f))
    {
        // Use mana
        player.UseMana(25f);
    }

    // Get mana values
    float currentMana = player.Mana.Value;
    float maxMana = player.MaxMana;
    float manaPercent = player.ManaPercent;
    */

    private float _lastManaUseTime;
    private Coroutine _manaRegenCoroutine;

    public float MaxMana => maxMana;
    public float ManaPercent => maxMana > 0 ? Mana.Value / maxMana : 0f;
    public bool HasMana(float amount) => Mana.Value >= amount;



    private void HandleManaChanged(float prev, float next, bool asServer)
    {
        // Optional: Add mana change effects here
        // Example: UI updates, visual effects, etc.
    }

    [Server]
    public bool UseMana(float amount)
    {
        if (Mana.Value < amount || IsDead.Value)
            return false;

        Mana.Value -= amount;
        _lastManaUseTime = Time.time;
        return true;
    }

    [Server]
    public void AddMana(float amount)
    {
        Mana.Value = Mathf.Min(Mana.Value + amount, maxMana);
    }

    [Server]
    public void SetMana(float amount)
    {
        Mana.Value = Mathf.Clamp(amount, 0, maxMana);
    }

    [Server]
    public void RestoreAllMana()
    {
        Mana.Value = maxMana;
    }

    private IEnumerator ManaRegenerationCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f); // Update every 0.1 seconds

            if (IsDead.Value)
                continue;

            // Check if enough time has passed since last mana use
            if (Time.time - _lastManaUseTime >= manaRegenDelay && Mana.Value < maxMana)
            {
                float regenAmount = manaRegenRate * 0.1f; // Adjust for update frequency
                Mana.Value = Mathf.Min(Mana.Value + regenAmount, maxMana);
            }
        }
    }

    [Server]
    public void ModifyManaRegen(float multiplier, float duration)
    {
        float originalRate = manaRegenRate;
        manaRegenRate *= multiplier;

        if (duration > 0f)
        {
            StartCoroutine(ResetManaRegenAfterDelay(originalRate, duration));
        }
    }

    private IEnumerator ResetManaRegenAfterDelay(float originalRate, float delay)
    {
        yield return new WaitForSeconds(delay);
        manaRegenRate = originalRate;
    }

    #endregion

    private void UpdateTeamUIElements(Color teamColor)
    {
        // Implement any UI color changes here
        // Example: player nameplate, health bar, etc.
        // You can add specific UI component references if needed
    }

    [ObserversRpc]
    private void RpcSyncTeamColor(Color teamColor)
    {
        // Only apply on non-owner clients (owner already has the color set)
        if (!IsOwner)
        {
            SetTeamColor(teamColor);
        }
    }
    #endregion

    #region Additional Properties for Debug/UI
    private int _teamIndex;
    public int TeamIndex
    {
        get => _teamIndex;
        set
        {
            _teamIndex = value;
            // Optionally sync with the existing TeamId SyncVar
            if (IsServerInitialized && TeamId.Value != value)
            {
                TeamId.Value = value;
            }
        }
    }

    #endregion

    #region Scoring System

    private int _goalsScored = 0;

    public int Goals => _goalsScored;

    [Server]
    public void AddGoal()
    {
        _goalsScored++;

        // Add points for scoring a goal
        Score.Value += 100; // Or whatever point value you want

        // Sync to clients
        RpcOnGoalScored(_goalsScored);

        Debug.Log($"Player {PlayerName} scored a goal! Total: {_goalsScored}");
    }

    [Server]
    public void AddAssist()
    {
        _assists++;

        // Add points for assist
        Score.Value += 50; // Or whatever point value you want

        // Sync to clients
        RpcOnAssist(_assists);

        Debug.Log($"Player {PlayerName} made an assist! Total: {_assists}");
    }

    [Server]
    public void ResetGoals()
    {
        _goalsScored = 0;
        _assists = 0;

        // Sync to clients
        RpcResetGoals();
    }

    [ObserversRpc]
    private void RpcOnGoalScored(int totalGoals)
    {
        _goalsScored = totalGoals;

        // Play goal scoring effects
        PlayGoalScoredEffect();
    }

    [ObserversRpc]
    private void RpcOnAssist(int totalAssists)
    {
        _assists = totalAssists;

        // Play assist effects
        PlayAssistEffect();
    }

    [ObserversRpc]
    private void RpcResetGoals()
    {
        _goalsScored = 0;
        _assists = 0;
    }

    private void PlayGoalScoredEffect()
    {
        // Add visual/audio effects for scoring a goal
        if (base.IsOwner)
        {
            // Play UI effects, sounds, etc.
            Debug.Log("You scored a goal!");
        }

        // You could also trigger particle effects, animations, etc.
    }

    private void PlayAssistEffect()
    {
        // Add visual/audio effects for making an assist
        if (base.IsOwner)
        {
            Debug.Log("You made an assist!");
        }
    }


    #endregion

// In AdvancedNetworkPlayerController.cs
        public event System.Action<AdvancedNetworkPlayerController> OnDeath;

        // In your death handling method:
        private void HandleDeath()
        {
            OnDeath?.Invoke(this);
            // ... rest of your death logic
        }

}
