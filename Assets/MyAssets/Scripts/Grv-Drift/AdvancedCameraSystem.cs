using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using System;
namespace zoya.game
{



    [RequireComponent(typeof(Camera))]
    public class AdvancedCameraSystem : MonoBehaviour
    {
        [System.Serializable]
        public class CameraModeSettings
        {
            [Header("Follow Settings")]
            public Vector3 followOffset = new Vector3(0f, 3f, -8f);
            public float followSmoothness = 5f;
            public float rotationSmoothness = 3f;
            public float lookAheadDistance = 2f;
            public float maxFollowDistance = 15f;
            public float minFollowDistance = 3f;

            [Header("Orbit Settings")]
            public float orbitSpeed = 120f;
            public float orbitDistance = 10f;
            public float orbitHeight = 3f;
            public float orbitSmoothness = 4f;

            [Header("FreeLook Settings")]
            public float freeLookSpeed = 120f;
            public float freeLookAcceleration = 2f;
            public float freeLookMaxSpeed = 360f;
            public float freeLookTopRadius = 3f;
            public float freeLookMidRadius = 5f;
            public float freeLookBottomRadius = 3f;
            public float freeLookTopHeight = 1f;
            public float freeLookMidHeight = 2f;
            public float freeLookBottomHeight = 1f;
        }

        [System.Serializable]
        public class CameraEffects
        {
            [Header("Field of View")]
            public float defaultFOV = 60f;
            public float boostFOV = 70f;
            public float fovTransitionSpeed = 3f;
            public AnimationCurve fovCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            [Header("Camera Shake")]
            public float shakeIntensity = 1f;
            public float shakeFrequency = 10f;
            public float shakeDuration = 0.5f;
            public AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

            [Header("Motion Blur")]
            public float motionBlurIntensity = 0.5f;
            public float motionBlurThreshold = 5f;

            [Header("Depth of Field")]
            public float dofFocusDistance = 10f;
            public float dofFocusRange = 3f;
            public float dofBlurRadius = 2f;
        }

        [System.Serializable]
        public class CollisionSettings
        {
            public LayerMask collisionMask = -1;
            public float collisionRadius = 0.3f;
            public float collisionPullbackSpeed = 5f;
            public float minimumDistance = 1f;
            public bool useSphereCast = true;
        }

        [System.Serializable]
        public class AudioSettings
        {
            public AudioClip cameraMoveSound;
            public AudioClip cameraSwitchSound;
            public float moveSoundVolume = 0.3f;
            public float switchSoundVolume = 0.5f;
        }

        public enum CameraMode { Follow, Orbit, FreeLook, Spectate, Replay }

        [Header("General")]
        public CameraMode currentMode = CameraMode.Follow;
        public CameraModeSettings modeSettings = new CameraModeSettings();
        public CameraEffects effects = new CameraEffects();
        public CollisionSettings collision = new CollisionSettings();
        public AudioSettings audioSettings = new AudioSettings();

        [Header("Targeting")]
        public Transform target;
        public List<Transform> additionalTargets = new List<Transform>();
        public bool autoFindPlayer = true;

        // Cinemachine 3.x cameras
        private CinemachineCamera _followCamera;
        private CinemachineCamera _orbitCamera;
        private CinemachineCamera _freeLookCamera;
        private CinemachineBrain _cameraBrain;

        // Components
        private Camera _unityCamera;
        private AudioSource _audioSource;
        private Volume _postProcessingVolume;
        private MotionBlur _motionBlur;
        private DepthOfField _depthOfField;

        // Runtime variables
        private float _currentFOV;
        private Vector3 _currentVelocity;

        // Shake state
        private float _shakeTimer;
        private float _currentShakeIntensity;
        private CinemachineBasicMultiChannelPerlin _followNoise;
        private CinemachineBasicMultiChannelPerlin _orbitNoise;
        private List<CinemachineBasicMultiChannelPerlin> _freeLookRigNoise = new List<CinemachineBasicMultiChannelPerlin>();

        // Input
        private float _mouseX;
        private float _mouseY;
        private float _scrollInput;
        private bool _orbitModeInput;
        private bool _freeLookInput;

        public Transform CurrentTarget => target;
        public CameraMode CurrentMode => currentMode;
        public Vector3 CurrentVelocity => _currentVelocity;

        #region Initialization
        private void Awake()
        {
            _unityCamera = GetComponent<Camera>();
            _audioSource = GetComponent<AudioSource>();
            _cameraBrain = GetComponent<CinemachineBrain>();

            InitializeCinemachineCameras();
            InitializePostProcessing();
            SetupCameraTargetsAndLens();
        }

        private void InitializeCinemachineCameras()
        {
            // FOLLOW CAMERA
            GameObject followGO = new GameObject("CM_FollowCamera");
            followGO.transform.SetParent(transform);
            _followCamera = followGO.AddComponent<CinemachineCamera>();
            _followCamera.Priority = 10;

            var followBody = followGO.AddComponent<CinemachineFollow>();
            var followAim = followGO.AddComponent<CinemachineRotationComposer>();
            _followNoise = followGO.AddComponent<CinemachineBasicMultiChannelPerlin>();

            // ORBIT CAMERA
            GameObject orbitGO = new GameObject("CM_OrbitCamera");
            orbitGO.transform.SetParent(transform);
            _orbitCamera = orbitGO.AddComponent<CinemachineCamera>();
            _orbitCamera.Priority = 0;

            var orbitBody = orbitGO.AddComponent<CinemachineOrbitalFollow>();
            var orbitPan = orbitGO.AddComponent<CinemachinePanTilt>();
            _orbitNoise = orbitGO.AddComponent<CinemachineBasicMultiChannelPerlin>();

            // FREELOOK CAMERA
            GameObject freeLookGO = new GameObject("CM_FreeLookCamera");
            freeLookGO.transform.SetParent(transform);
            _freeLookCamera = freeLookGO.AddComponent<CinemachineCamera>();
            _freeLookCamera.Priority = 0;

            var freeOrbitalBody = freeLookGO.AddComponent<CinemachineOrbitalFollow>();
            var freePanTilt = freeLookGO.AddComponent<CinemachinePanTilt>();
            var freeNoise = freeLookGO.AddComponent<CinemachineBasicMultiChannelPerlin>();
            _freeLookRigNoise.Add(freeNoise);

            ConfigureCinemachineComponents();
        }

        private void ConfigureCinemachineComponents()
        {
            // Configure Follow Camera
            if (_followCamera != null)
            {
                _followCamera.Lens.FieldOfView = effects.defaultFOV;

                var followBody = _followCamera.GetComponent<CinemachineFollow>();
                if (followBody != null)
                {
                    followBody.FollowOffset = modeSettings.followOffset;

                    // Try to set position damping
                    var type = followBody.GetType();
                    var pdProp = type.GetProperty("PositionDamping");
                    if (pdProp != null && pdProp.PropertyType == typeof(Vector3))
                    {
                        pdProp.SetValue(followBody, new Vector3(
                            modeSettings.followSmoothness,
                            modeSettings.followSmoothness,
                            modeSettings.followSmoothness
                        ), null);
                    }
                    else
                    {
                        var dampingProp = type.GetProperty("Damping");
                        if (dampingProp != null && dampingProp.PropertyType == typeof(float))
                        {
                            dampingProp.SetValue(followBody, modeSettings.followSmoothness, null);
                        }
                    }
                }
            }

            // Configure Orbit Camera
            if (_orbitCamera != null)
            {
                _orbitCamera.Lens.FieldOfView = effects.defaultFOV;

                var orbitBody = _orbitCamera.GetComponent<CinemachineOrbitalFollow>();
                var orbitPanTilt = _orbitCamera.GetComponent<CinemachinePanTilt>();

                if (orbitBody != null)
                {
                    orbitBody.Radius = modeSettings.orbitDistance;
                    orbitBody.TargetOffset = new Vector3(0f, modeSettings.orbitHeight, 0f);

                    var tracker = orbitBody.TrackerSettings;
                    tracker.PositionDamping = new Vector3(
                        modeSettings.orbitSmoothness,
                        modeSettings.orbitSmoothness,
                        modeSettings.orbitSmoothness
                    );
                    tracker.RotationDamping = new Vector3(
                        modeSettings.orbitSmoothness,
                        modeSettings.orbitSmoothness,
                        modeSettings.orbitSmoothness
                    );
                    orbitBody.TrackerSettings = tracker;
                }

                if (orbitPanTilt != null)
                {
                    orbitPanTilt.PanAxis.Range = new Vector2(-180f, 180f);
                    orbitPanTilt.TiltAxis.Range = new Vector2(-80f, 80f);
                }
            }

            // Configure FreeLook Camera
            if (_freeLookCamera != null)
            {
                _freeLookCamera.Lens.FieldOfView = effects.defaultFOV;

                var freeOrbital = _freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
                var freePanTilt = _freeLookCamera.GetComponent<CinemachinePanTilt>();

                if (freeOrbital != null)
                {
                    freeOrbital.Radius = modeSettings.freeLookMidRadius;
                    freeOrbital.TargetOffset = new Vector3(0f, modeSettings.freeLookMidHeight, 0f);

                    var tracker = freeOrbital.TrackerSettings;
                    tracker.PositionDamping = new Vector3(
                        modeSettings.freeLookSpeed,
                        modeSettings.freeLookSpeed,
                        modeSettings.freeLookSpeed
                    );
                    freeOrbital.TrackerSettings = tracker;
                }

                if (freePanTilt != null)
                {
                    freePanTilt.PanAxis.Range = new Vector2(-180f, 180f);
                    freePanTilt.TiltAxis.Range = new Vector2(-80f, 80f);
                }
            }
        }

        private void InitializePostProcessing()
        {
            _postProcessingVolume = FindFirstObjectByType<Volume>();
            if (_postProcessingVolume != null && _postProcessingVolume.profile != null)
            {
                _postProcessingVolume.profile.TryGet(out _motionBlur);
                _postProcessingVolume.profile.TryGet(out _depthOfField);
            }
        }
        private void SetupCameraTargetsAndLens()
        {
            _currentFOV = effects.defaultFOV;
            _unityCamera.fieldOfView = _currentFOV;

            if (autoFindPlayer && target == null)
            {
                FindPlayerTarget();
            }

            if (target != null)
            {
                SetCameraTargets(target);
            }
        }

        private void FindPlayerTarget()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                SetCameraTargets(target);
            }
        }

        private void SetCameraTargets(Transform newTarget)
        {
            if (_followCamera != null)
            {
                var camTarget = _followCamera.Target;
                camTarget.TrackingTarget = newTarget;
                _followCamera.Target = camTarget;
            }

            if (_orbitCamera != null)
            {
                var camTarget = _orbitCamera.Target;
                camTarget.TrackingTarget = newTarget;
                _orbitCamera.Target = camTarget;
            }

            if (_freeLookCamera != null)
            {
                var camTarget = _freeLookCamera.Target;
                camTarget.TrackingTarget = newTarget;
                _freeLookCamera.Target = camTarget;
            }
        }
        #endregion

        #region Update Loop
        private void Update()
        {
            HandleInput();
            UpdateCameraMode();
            UpdatePostProcessing();
            UpdateAudio();

            if (Input.GetKeyDown(KeyCode.F1)) ToggleCameraMode();
            if (Input.GetKeyDown(KeyCode.F2)) CycleTargets();

            // FOV smoothing
            _unityCamera.fieldOfView = Mathf.Lerp(_unityCamera.fieldOfView, _currentFOV,
                Time.deltaTime * effects.fovTransitionSpeed);

            if (_followCamera != null) _followCamera.Lens.FieldOfView = _unityCamera.fieldOfView;
            if (_orbitCamera != null) _orbitCamera.Lens.FieldOfView = _unityCamera.fieldOfView;
            if (_freeLookCamera != null) _freeLookCamera.Lens.FieldOfView = _unityCamera.fieldOfView;

            // Shake update
            ApplyCameraShakeTick();
        }

        private void LateUpdate()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayerTarget();
                if (target == null) return;
            }

            // Calculate current velocity for motion blur
            _currentVelocity = (transform.position - _previousPosition) / Time.deltaTime;
            _previousPosition = transform.position;
        }
        #endregion

        #region Input Handling
        private void HandleInput()
        {
            // Mouse input
            _mouseX = Input.GetAxis("Mouse X");
            _mouseY = Input.GetAxis("Mouse Y");
            _scrollInput = Input.GetAxis("Mouse ScrollWheel");

            // Mode switching
            _orbitModeInput = Input.GetKey(KeyCode.LeftAlt);
            _freeLookInput = Input.GetKey(KeyCode.LeftControl);

            // Update camera mode based on input
            if (_freeLookInput && currentMode != CameraMode.FreeLook)
            {
                SwitchMode(CameraMode.FreeLook);
            }
            else if (_orbitModeInput && currentMode != CameraMode.Orbit)
            {
                SwitchMode(CameraMode.Orbit);
            }
            else if (!_freeLookInput && !_orbitModeInput && currentMode != CameraMode.Follow)
            {
                SwitchMode(CameraMode.Follow);
            }

            // Zoom input
            if (_scrollInput != 0)
            {
                HandleZoomInput();
            }
        }

        private void HandleZoomInput()
        {
            if (currentMode == CameraMode.Follow && _followCamera != null)
            {
                var followBody = _followCamera.GetComponent<CinemachineFollow>();
                if (followBody != null)
                {
                    Vector3 offset = followBody.FollowOffset;
                    offset.z += _scrollInput * 2f;
                    offset.z = Mathf.Clamp(offset.z, -modeSettings.maxFollowDistance, -modeSettings.minFollowDistance);
                    followBody.FollowOffset = offset;
                }
            }
            else if (currentMode == CameraMode.Orbit && _orbitCamera != null)
            {
                var orbitBody = _orbitCamera.GetComponent<CinemachineOrbitalFollow>();
                if (orbitBody != null)
                {
                    float radius = orbitBody.Radius;
                    radius = Mathf.Clamp(radius - _scrollInput * 2f,
                        modeSettings.minFollowDistance, modeSettings.maxFollowDistance);
                    orbitBody.Radius = radius;
                }
            }
        }
        #endregion

        #region Camera Modes
        private void UpdateCameraMode()
        {
            // Set camera priorities based on current mode
            if (_followCamera != null) _followCamera.Priority = (currentMode == CameraMode.Follow) ? 10 : 0;
            if (_orbitCamera != null) _orbitCamera.Priority = (currentMode == CameraMode.Orbit) ? 10 : 0;
            if (_freeLookCamera != null) _freeLookCamera.Priority = (currentMode == CameraMode.FreeLook) ? 10 : 0;

            // Handle special modes
            if (currentMode == CameraMode.Spectate)
            {
                CalculateSpectatePosition();
            }
            else if (currentMode == CameraMode.Replay)
            {
                CalculateReplayPosition();
            }
        }

        private void CalculateSpectatePosition()
        {
            // Spectate mode logic
            if (additionalTargets.Count > 0)
            {
                Transform spectateTarget = additionalTargets[UnityEngine.Random.Range(0, additionalTargets.Count)];
                if (spectateTarget != null)
                {
                    transform.position = spectateTarget.position + Vector3.up * 5f;
                    transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                }
            }
        }

        private void CalculateReplayPosition()
        {
            // Replay system placeholder
            // Would use recorded transform data
        }
        #endregion

        #region Effects System
        private void ApplyCameraShakeTick()
        {
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float t = 1f - (_shakeTimer / effects.shakeDuration);
                float amplitude = _currentShakeIntensity * effects.shakeFalloff.Evaluate(t);

                if (_followNoise != null)
                {
                    _followNoise.AmplitudeGain = amplitude;
                    _followNoise.FrequencyGain = effects.shakeFrequency;
                }

                if (_orbitNoise != null)
                {
                    _orbitNoise.AmplitudeGain = amplitude;
                    _orbitNoise.FrequencyGain = effects.shakeFrequency;
                }

                for (int i = 0; i < _freeLookRigNoise.Count; i++)
                {
                    if (_freeLookRigNoise[i] != null)
                    {
                        _freeLookRigNoise[i].AmplitudeGain = amplitude;
                        _freeLookRigNoise[i].FrequencyGain = effects.shakeFrequency;
                    }
                }
            }
            else
            {
                // Fade out shake
                if (_followNoise != null) _followNoise.AmplitudeGain = 0f;
                if (_orbitNoise != null) _orbitNoise.AmplitudeGain = 0f;
                foreach (var noise in _freeLookRigNoise)
                {
                    if (noise != null) noise.AmplitudeGain = 0f;
                }
            }
        }

        private void UpdatePostProcessing()
        {
            if (_motionBlur != null)
            {
                float cameraSpeed = _currentVelocity.magnitude;
                _motionBlur.active = cameraSpeed > effects.motionBlurThreshold;
                _motionBlur.intensity.value = Mathf.Clamp01(cameraSpeed / 20f) * effects.motionBlurIntensity;
            }

            if (_depthOfField != null)
            {
                _depthOfField.active = true;
                _depthOfField.focusDistance.value = effects.dofFocusDistance;
                _depthOfField.focalLength.value = effects.dofFocusRange;
                _depthOfField.aperture.value = effects.dofBlurRadius;
            }
        }

        public void ShakeCamera(float intensity, float duration)
        {
            _currentShakeIntensity = intensity * effects.shakeIntensity;
            _shakeTimer = duration;

            // Apply initial amplitude
            if (_followNoise != null) _followNoise.AmplitudeGain = _currentShakeIntensity;
            if (_orbitNoise != null) _orbitNoise.AmplitudeGain = _currentShakeIntensity;
            foreach (var noise in _freeLookRigNoise)
            {
                if (noise != null) noise.AmplitudeGain = _currentShakeIntensity;
            }
        }

        public void SetFOV(float fov, bool instant = false)
        {
            _currentFOV = fov;
            if (instant)
            {
                _unityCamera.fieldOfView = _currentFOV;
                if (_followCamera != null) _followCamera.Lens.FieldOfView = _currentFOV;
                if (_orbitCamera != null) _orbitCamera.Lens.FieldOfView = _currentFOV;
                if (_freeLookCamera != null) _freeLookCamera.Lens.FieldOfView = _currentFOV;
            }
        }

        public void BoostFOV(bool boost)
        {
            _currentFOV = boost ? effects.boostFOV : effects.defaultFOV;
        }
        #endregion

        #region Camera Control
        public void SwitchMode(CameraMode newMode)
        {
            if (currentMode == newMode) return;

            currentMode = newMode;
            PlaySound(audioSettings.cameraSwitchSound, audioSettings.switchSoundVolume);

            UpdateCameraMode();
        }

        public void ToggleCameraMode()
        {
            CameraMode nextMode = (CameraMode)(((int)currentMode + 1) % System.Enum.GetValues(typeof(CameraMode)).Length);
            SwitchMode(nextMode);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SetCameraTargets(newTarget);

            if (currentMode == CameraMode.Follow)
            {
                SnapToTarget();
            }
        }

        public void AddTarget(Transform newTarget)
        {
            if (!additionalTargets.Contains(newTarget))
            {
                additionalTargets.Add(newTarget);
            }
        }

        public void RemoveTarget(Transform targetToRemove)
        {
            additionalTargets.Remove(targetToRemove);
        }

        public void CycleTargets()
        {
            if (additionalTargets.Count == 0) return;

            int currentIndex = additionalTargets.IndexOf(target);
            int nextIndex = (currentIndex + 1) % additionalTargets.Count;
            SetTarget(additionalTargets[nextIndex]);
        }

        private void SnapToTarget()
        {
            if (target == null) return;

            // Force immediate update of camera position
            if (_cameraBrain != null)
            {
                _cameraBrain.ManualUpdate();
            }
        }
        #endregion

        #region Audio
        private void UpdateAudio()
        {
            if (_audioSource != null && audioSettings.cameraMoveSound != null)
            {
                float volume = Mathf.Clamp01(_currentVelocity.magnitude / 10f) * audioSettings.moveSoundVolume;
                _audioSource.volume = volume;

                if (!_audioSource.isPlaying && volume > 0.1f)
                {
                    _audioSource.clip = audioSettings.cameraMoveSound;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
                else if (_audioSource.isPlaying && volume < 0.05f)
                {
                    _audioSource.Stop();
                }
            }
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }
        #endregion

        #region Public API
        public void EnableMotionBlur(bool enable)
        {
            if (_motionBlur != null)
            {
                _motionBlur.active = enable;
            }
        }

        public void EnableDepthOfField(bool enable)
        {
            if (_depthOfField != null)
            {
                _depthOfField.active = enable;
            }
        }

        public void SetFollowOffset(Vector3 offset)
        {
            var followBody = _followCamera.GetComponent<CinemachineFollow>();
            if (followBody != null)
            {
                followBody.FollowOffset = offset;
            }
        }

        public void SetOrbitDistance(float distance)
        {
            var orbitBody = _orbitCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitBody != null)
            {
                orbitBody.Radius = Mathf.Clamp(distance, modeSettings.minFollowDistance, modeSettings.maxFollowDistance);
            }
        }

        public bool IsColliding() => false; // Cinemachine handles collision automatically
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(target.position, modeSettings.maxFollowDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, modeSettings.minFollowDistance);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
        #endregion

        private Vector3 _previousPosition;

        private void OnDestroy()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }
}