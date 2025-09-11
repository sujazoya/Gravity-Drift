using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Unity.Cinemachine;

public enum ShakeType
{
    Impact,
    Explosion,
    Footstep,
    Earthquake,
    PlayerHit,
    GoalScored,
    Custom
}

public enum ShakePriority
{
    Low,
    Medium,
    High,
    Critical
}


[System.Serializable]
public class ShakeProfile
{
    public ShakeType Type;
    public ShakePriority Priority = ShakePriority.Medium;
    public float Duration = 0.5f;
    public float Intensity = 1f;
    public Vector3 Position;
    public float Range = 10f;
    public CameraShakeManager.FalloffType Falloff = CameraShakeManager.FalloffType.Linear; // <-- Add this line
   
   // public AnimationCurve FalloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
     public bool UseRumble = true;
    public float RumbleIntensity = 0.5f;
}

public class CameraShakeManager : NetworkBehaviour
{
    public static CameraShakeManager Instance { get; private set; }
    
    public enum FalloffType { Linear, EaseInOut }

    public static AnimationCurve GetFalloffCurve(FalloffType type)
    {
        switch (type)
        {
            case FalloffType.EaseInOut:
                return AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            case FalloffType.Linear:
            default:
                return AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }
    }

    [Header("Camera References")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private CinemachineCamera _virtualCamera;
    [SerializeField] private CinemachineBasicMultiChannelPerlin _noiseModule;

    [Header("Shake Settings")]
    [SerializeField] private float _maxShakeIntensity = 3f;
    [SerializeField] private float _shakeDamping = 1f;
    [SerializeField] private float _rotationMultiplier = 1f;
    [SerializeField] private bool _enablePositionShake = true;
    [SerializeField] private bool _enableRotationShake = true;

    [Header("Post Processing")]
    [SerializeField] private PostProcessVolume _postProcessVolume;
    [SerializeField] private float _chromaticAberrationIntensity = 0.5f;
    [SerializeField] private float _vignetteIntensity = 0.3f;

    [Header("Network Settings")]
    [SerializeField] private float _networkUpdateRate = 10f;
    [SerializeField] private bool _synchronizeShakes = true;

    [Header("Default Shake Profiles")]
    [SerializeField] private ShakeProfile _impactProfile;
    [SerializeField] private ShakeProfile _explosionProfile;
    [SerializeField] private ShakeProfile _footstepProfile;
    [SerializeField] private ShakeProfile _earthquakeProfile;
    [SerializeField] private ShakeProfile _playerHitProfile;
    [SerializeField] private ShakeProfile _goalScoredProfile;

    // Synchronized variables
    public readonly SyncVar<float> _currentShakeIntensity = new SyncVar<float>();
    public readonly SyncVar<Vector3> _shakeOffset = new SyncVar<Vector3>();
    public readonly SyncVar<Vector3> _shakeRotation = new SyncVar<Vector3>();

    // Private variables
    private List<ActiveShake> _activeShakes = new List<ActiveShake>();
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private ChromaticAberration _chromaticAberration;
    private Vignette _vignette;
    private Coroutine _networkUpdateCoroutine;
    private bool _isShaking;

    private class ActiveShake
    {
        public ShakeProfile Profile;
        public float ElapsedTime;
        public Vector3 SourcePosition;
        public NetworkConnection SourceConnection;
        public bool IsLocal;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (Instance == null)
        {
            Instance = this;
            InitializeCameraShake();
        }
        else
        {
            InstanceFinder.ServerManager.Despawn(gameObject);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (base.IsOwner)
        {
            InitializeCameraEffects();
            StartNetworkUpdates();
        }
    }

    private void InitializeCameraShake()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_virtualCamera != null && _noiseModule == null)
        {
            _noiseModule = _virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Noise)
                      as CinemachineBasicMultiChannelPerlin;
        }

        _originalPosition = transform.localPosition;
        _originalRotation = transform.localRotation;

        InitializePostProcessing();
    }

    private void InitializePostProcessing()
    {
        if (_postProcessVolume != null && _postProcessVolume.profile != null)
        {
            _postProcessVolume.profile.TryGetSettings(out _chromaticAberration);
            _postProcessVolume.profile.TryGetSettings(out _vignette);
        }
    }

    private void InitializeCameraEffects()
    {
        if (_chromaticAberration != null)
            _chromaticAberration.intensity.value = 0f;

        if (_vignette != null)
            _vignette.intensity.value = 0f;
    }

    private void Update()
    {
        if (!base.IsOwner) return;

        UpdateShakes();
        ApplyCameraShake(_currentShakeIntensity.Value);
        UpdatePostProcessingEffects();
    }

    private void UpdateShakes()
    {
        float totalIntensity = 0f;
        Vector3 totalOffset = Vector3.zero;
        Vector3 totalRotation = Vector3.zero;

        for (int i = _activeShakes.Count - 1; i >= 0; i--)
        {
            ActiveShake shake = _activeShakes[i];
            shake.ElapsedTime += Time.deltaTime;

            float progress = shake.ElapsedTime / shake.Profile.Duration;
            if (progress >= 1f)
            {
                _activeShakes.RemoveAt(i);
                continue;
            }

            float intensity = shake.Profile.Intensity *
             CameraShakeManager.GetFalloffCurve(shake.Profile.Falloff).Evaluate(progress);

            if (shake.Profile.Range > 0 && _mainCamera != null)
            {
                float distance = Vector3.Distance(_mainCamera.transform.position, shake.Profile.Position);
                float distanceFactor = Mathf.Clamp01(1f - (distance / shake.Profile.Range));
                intensity *= distanceFactor;
            }

            totalIntensity += intensity;
            totalOffset += new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity)
            );

            totalRotation += new Vector3(
                Random.Range(-intensity, intensity) * _rotationMultiplier,
                Random.Range(-intensity, intensity) * _rotationMultiplier,
                Random.Range(-intensity, intensity) * _rotationMultiplier
            );
        }

        _currentShakeIntensity.Value = Mathf.Lerp(
            _currentShakeIntensity.Value,
            totalIntensity,
            _shakeDamping * Time.deltaTime
        );

        _shakeOffset.Value = Vector3.Lerp(_shakeOffset.Value, totalOffset, _shakeDamping * Time.deltaTime);
        _shakeRotation.Value = Vector3.Lerp(_shakeRotation.Value, totalRotation, _shakeDamping * Time.deltaTime);

        _isShaking = _currentShakeIntensity.Value > 0.01f;
    }

    [SerializeField] private CinemachineImpulseSource _impulseSource;


    private void ApplyCameraShake(float intensity)
    {
        if (_impulseSource != null)
        {
            // Scale the raw shake intensity
            Vector3 impulse = UnityEngine.Random.insideUnitSphere * intensity;

            // Generate impulse (shakes all listeners in range)
            _impulseSource.GenerateImpulse(impulse);
        }
    }

    private void UpdatePostProcessingEffects()
    {
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = Mathf.Lerp(
                _chromaticAberration.intensity.value,
                _currentShakeIntensity.Value * _chromaticAberrationIntensity,
                _shakeDamping * Time.deltaTime
            );
        }

        if (_vignette != null)
        {
            _vignette.intensity.value = Mathf.Lerp(
                _vignette.intensity.value,
                _currentShakeIntensity.Value * _vignetteIntensity,
                _shakeDamping * Time.deltaTime
            );
        }
    }

    #region Public API

    [ServerRpc(RequireOwnership = false)]
    public void ShakeCameraServerRpc(ShakeType shakeType, Vector3 position, float intensity = 1f, float duration = 0.5f, NetworkConnection sourceConnection = null)
    {
        ShakeProfile profile = GetShakeProfile(shakeType);
        profile.Position = position;
        profile.Intensity = intensity;
        profile.Duration = duration;

        BroadcastShake(profile, sourceConnection);
    }

    [ObserversRpc]
    private void BroadcastShake(ShakeProfile profile, NetworkConnection sourceConnection)
    {
        AddShake(profile, sourceConnection, false);
    }

    public void AddShake(ShakeProfile profile, NetworkConnection sourceConnection = null, bool isLocal = true)
    {
        ActiveShake newShake = new ActiveShake
        {
            Profile = profile,
            SourcePosition = profile.Position,
            SourceConnection = sourceConnection,
            IsLocal = isLocal,
            ElapsedTime = 0f
        };

        if (profile.Priority == ShakePriority.Critical)
            _activeShakes.RemoveAll(s => s.Profile.Priority < ShakePriority.Critical);

        _activeShakes.Add(newShake);

        if (profile.UseRumble && isLocal && base.IsOwner)
        {
            TriggerRumble(profile.RumbleIntensity, profile.Duration);
        }
    }

    public void AddShake(ShakeType shakeType, Vector3 position, float intensity = 1f, float duration = 0.5f, bool isLocal = true)
    {
        ShakeProfile profile = GetShakeProfile(shakeType);
        profile.Position = position;
        profile.Intensity = intensity;
        profile.Duration = duration;

        AddShake(profile, null, isLocal);
    }

    public void StopAllShakes()
    {
        _activeShakes.Clear();
        _currentShakeIntensity.Value = 0f;
        _shakeOffset.Value = Vector3.zero;
        _shakeRotation.Value = Vector3.zero;

        if (base.IsOwner)
            ResetCameraEffects();
    }

    public void StopShakesByType(ShakeType shakeType)
    {
        _activeShakes.RemoveAll(shake => shake.Profile.Type == shakeType);
    }

    public void StopShakesByPriority(ShakePriority priority)
    {
        _activeShakes.RemoveAll(shake => shake.Profile.Priority <= priority);
    }

    #endregion

    #region Utility Methods

    private ShakeProfile GetShakeProfile(ShakeType type)
    {
        return type switch
        {
            ShakeType.Impact => _impactProfile,
            ShakeType.Explosion => _explosionProfile,
            ShakeType.Footstep => _footstepProfile,
            ShakeType.Earthquake => _earthquakeProfile,
            ShakeType.PlayerHit => _playerHitProfile,
            ShakeType.GoalScored => _goalScoredProfile,
            _ => new ShakeProfile { Type = type }
        };
    }

    private void TriggerRumble(float intensity, float duration)
    {
#if ENABLE_INPUT_SYSTEM
        // UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(intensity, intensity);
#endif
    }

    private void ResetCameraEffects()
    {
        // Stop shake state so no new impulses are generated
        _isShaking = false;
        _currentShakeIntensity.Value = 0f;
        _shakeOffset.Value = Vector3.zero;
        _shakeRotation.Value = Vector3.zero;

        // Reset camera transform to original
        transform.localPosition = _originalPosition;
        transform.localRotation = _originalRotation;

        // Reset post-processing
        if (_chromaticAberration != null)
            _chromaticAberration.intensity.value = 0f;

        if (_vignette != null)
            _vignette.intensity.value = 0f;
    }


    private void StartNetworkUpdates()
    {
        if (_networkUpdateCoroutine != null)
            StopCoroutine(_networkUpdateCoroutine);

        _networkUpdateCoroutine = StartCoroutine(NetworkUpdateRoutine());
    }

    private IEnumerator NetworkUpdateRoutine()
    {
        while (true)
        {
            if (_synchronizeShakes && base.IsServerInitialized)
                UpdateNetworkShakeState();

            yield return new WaitForSeconds(1f / _networkUpdateRate);
        }
    }

    [Server]
    private void UpdateNetworkShakeState()
    {
        BroadcastShakeState(_currentShakeIntensity.Value, _shakeOffset.Value, _shakeRotation.Value);
    }

    [ObserversRpc]
    private void BroadcastShakeState(float intensity, Vector3 offset, Vector3 rotation)
    {
        if (!base.IsOwner)
        {
            _currentShakeIntensity.Value = intensity;
            _shakeOffset.Value = offset;
            _shakeRotation.Value = rotation;
        }
    }

    #endregion

    #region Getters

    public bool IsShaking => _isShaking;
    public float CurrentIntensity => _currentShakeIntensity.Value;
    public int ActiveShakeCount => _activeShakes.Count;
    public ShakeProfile GetProfile(ShakeType type) => GetShakeProfile(type);

    #endregion

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (base.IsOwner)
        {
            StopAllShakes();
            ResetCameraEffects();

            if (_networkUpdateCoroutine != null)
                StopCoroutine(_networkUpdateCoroutine);
        }
    }

    private void OnDestroy()
    {
        StopAllShakes();
        ResetCameraEffects();
    }

#if UNITY_EDITOR
    [ContextMenu("Test Impact Shake")]
    private void TestImpactShake()
    {
        AddShake(ShakeType.Impact, transform.position, 1f, 0.5f, true);
    }

    [ContextMenu("Test Explosion Shake")]
    private void TestExplosionShake()
    {
        AddShake(ShakeType.Explosion, transform.position, 2f, 1f, true);
    }

    [ContextMenu("Stop All Shakes")]
    private void EditorStopAllShakes()
    {
        StopAllShakes();
    }
#endif

    #region Public API - Multiplayer Shake Methods

    [ServerRpc(RequireOwnership = false)]
    public void ShakeAllCamerasServerRpc(ShakeType shakeType, Vector3 position, float intensity = 1f, float duration = 0.5f)
    {
        ShakeProfile profile = GetShakeProfile(shakeType);
        profile.Position = position;
        profile.Intensity = intensity;
        profile.Duration = duration;

        BroadcastShakeAllCameras(profile);
    }

    [ObserversRpc]
    private void BroadcastShakeAllCameras(ShakeProfile profile)
    {
        // This will run on all clients
        AddShake(profile, null, false);
    }

    // The method you're trying to call from AdvancedGoalTrigger
    public void ShakeAllCameras(float intensity, float duration)
    {
        if (IsServerInitialized)
        {
            // Server can broadcast directly
            ShakeAllCamerasServerRpc(ShakeType.GoalScored, Vector3.zero, intensity, duration);
        }
        else if (IsClientInitialized)
        {
            // Client requests the shake from server
            CmdRequestShakeAllCameras(intensity, duration);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdRequestShakeAllCameras(float intensity, float duration)
    {
        ShakeAllCamerasServerRpc(ShakeType.GoalScored, Vector3.zero, intensity, duration);
    }  
   

    // Specific method for goal scoring with custom position
    public void ShakeAllCamerasForGoal(Vector3 goalPosition, float intensity = 1.5f, float duration = 1f)
    {
        if (IsServerInitialized)
        {
            ShakeAllCamerasServerRpc(ShakeType.GoalScored, goalPosition, intensity, duration);
        }
        else if (IsClientInitialized)
        {
            CmdRequestGoalShake(goalPosition, intensity, duration);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdRequestGoalShake(Vector3 position, float intensity, float duration)
    {
        ShakeAllCamerasServerRpc(ShakeType.GoalScored, position, intensity, duration);
    }

#endregion
}
