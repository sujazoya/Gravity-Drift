using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;

namespace zoya.game
{




    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    public class AdvancedBall : NetworkBehaviour
    {
        [System.Serializable]
        public class BallSettings
        {
            [Header("Physics Settings")]
            public float mass = 1f;
            public float drag = 0.1f;
            public float angularDrag = 0.05f;
            public float bounceFactor = 0.8f;
            public float gravityScale = 1f;

            [Header("Game Settings")]
            public float maxSpeed = 50f;
            public float minSpeed = 0.1f;
            public float sleepThreshold = 0.5f;
            public int maxBounces = 3;
        }

        [System.Serializable]
        public class VisualSettings
        {
            [Header("Ball Visuals")]
            public GameObject ballModel;
            public Material defaultMaterial;
            public Material chargedMaterial;
            public Material teamBlueMaterial;
            public Material teamOrangeMaterial;

            [Header("Trail Effects")]
            public TrailRenderer trailRenderer;
            public ParticleSystem speedTrailParticles;
            public ParticleSystem bounceParticles;
            public ParticleSystem goalParticles;

            [Header("Light Effects")]
            public Light ballLight;
            public Color defaultLightColor = Color.white;
            public Color chargedLightColor = Color.yellow;
            public Color blueTeamColor = Color.blue;
            public Color orangeTeamColor = new Color(1f, 0.5f, 0f);

            [Header("Audio Settings")]
            public AudioClip bounceSound;
            public AudioClip goalSound;
            public AudioClip chargeSound;
            public AudioClip powerfulHitSound;
            public float minBounceVolume = 0.1f;
            public float maxBounceVolume = 1f;
            public float minBouncePitch = 0.8f;
            public float maxBouncePitch = 1.2f;
        }

        [System.Serializable]
        public class ChargeSettings
        {
            public float maxChargeTime = 3f;
            public float chargeMultiplier = 2f;
            public float chargeDrainRate = 0.5f;
            public float minChargeForEffect = 0.3f;
            public AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0, 1, 1, 3);
        }

        public BallSettings settings = new BallSettings();
        public VisualSettings visual = new VisualSettings();
        public ChargeSettings chargeSettings = new ChargeSettings();
        private readonly SyncVar<float> _currentCharge = new SyncVar<float>(0);
        private readonly SyncVar<int> _lastTeamTouch = new SyncVar<int>(0);
        private readonly SyncVar<int> _bounceCount = new SyncVar<int>(0);
        private readonly SyncVar<bool> _isCharging = new SyncVar<bool>(false);
        private readonly SyncVar<Vector3> _networkVelocity = new SyncVar<Vector3>();
        private readonly SyncVar<Vector3> _networkAngularVelocity = new SyncVar<Vector3>();

        private Rigidbody _rb;
        private Collider _ballCollider;
        private AudioSource _audioSource;
        private Vector3 _lastVelocity;
        private float _chargeTimer = 0f;
        private bool _wasInGoalArea = false;
        private List<AdvancedNetworkPlayerController> _recentTouchers = new List<AdvancedNetworkPlayerController>();

        public event System.Action<int> OnTeamTouch;
        public event System.Action<float> OnChargeChanged;
        public event System.Action OnGoalScored;
        public event System.Action OnBallReset;

        #region Initialization
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            InitializeBall();
        }

        private void InitializeBall()
        {
            _rb = GetComponent<Rigidbody>();
            _ballCollider = GetComponent<Collider>();
            _audioSource = GetComponent<AudioSource>();

            SetupPhysics();
            SetupVisuals();
            SetupAudio();

            if (IsServerInitialized)
            {
                StartCoroutine(NetworkSyncCoroutine());
            }
        }

        private void SetupPhysics()
        {
            _rb.mass = settings.mass;
            _rb.linearDamping = settings.drag;
            _rb.angularDamping = settings.angularDrag;
            _rb.maxLinearVelocity = settings.maxSpeed;
            _rb.sleepThreshold = settings.sleepThreshold;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Setup physics material
            PhysicsMaterial physMat = new PhysicsMaterial
            {
                bounciness = settings.bounceFactor,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            _ballCollider.material = physMat;
        }

        private void SetupVisuals()
        {
            if (visual.ballLight != null)
            {
                visual.ballLight.color = visual.defaultLightColor;
            }

            if (visual.trailRenderer != null)
            {
                visual.trailRenderer.emitting = false;
            }
        }

        private void SetupAudio()
        {
            if (_audioSource != null)
            {
                _audioSource.spatialBlend = 1f;
                _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                _audioSource.maxDistance = 50f;
            }
        }
        #endregion

        #region Network Synchronization
        private IEnumerator NetworkSyncCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f); // Sync 10 times per second

                if (_rb != null)
                {
                    _networkVelocity.Value = _rb.linearVelocity;
                    _networkAngularVelocity.Value = _rb.angularVelocity;
                }
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized)
            {
                // Client-side prediction smoothing
                StartCoroutine(ClientSmoothingCoroutine());
            }
        }

        private IEnumerator ClientSmoothingCoroutine()
        {
            while (!IsServerInitialized)
            {
                if (_rb != null)
                {
                    // Smoothly interpolate towards server state
                    _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, _networkVelocity.Value, 0.5f);
                    _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, _networkAngularVelocity.Value, 0.5f);
                }
                yield return null;
            }
        }
        #endregion

        #region Physics Handling
        private void FixedUpdate()
        {
            if (IsServerInitialized)
            {
                HandleServerPhysics();
            }

            HandleVisualEffects();
            HandleChargeDrain();
        }

        private void HandleServerPhysics()
        {
            // Apply custom gravity
            _rb.AddForce(Physics.gravity * settings.gravityScale, ForceMode.Acceleration);

            // Speed limiting
            if (_rb.linearVelocity.magnitude > settings.maxSpeed)
            {
                _rb.linearVelocity = _rb.linearVelocity.normalized * settings.maxSpeed;
            }

            // Sleep if moving too slowly
            if (_rb.linearVelocity.magnitude < settings.minSpeed)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // Store velocity for collision detection
            _lastVelocity = _rb.linearVelocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServerInitialized) return;

            HandleBallCollision(collision);
        }

        private void HandleBallCollision(Collision collision)
        {
            // Calculate bounce effects
            float impactStrength = collision.relativeVelocity.magnitude;
            Vector3 impactPoint = collision.contacts[0].point;
            Vector3 impactNormal = collision.contacts[0].normal;

            // Handle player hits
            AdvancedNetworkPlayerController player = collision.gameObject.GetComponent<AdvancedNetworkPlayerController>();
            if (player != null)
            {
                HandlePlayerHit(player, impactStrength, impactPoint);
            }

            // Handle environment bounces
            HandleEnvironmentBounce(impactStrength, impactPoint, impactNormal);

            // Increment bounce count
            _bounceCount.Value++;

            // Reset if too many bounces
            if (_bounceCount.Value >= settings.maxBounces)
            {
                StartCoroutine(ResetBallAfterDelay(1f));
            }
        }

        private void HandlePlayerHit(AdvancedNetworkPlayerController player, float impactStrength, Vector3 impactPoint)
        {
            // Set last team to touch
            _lastTeamTouch.Value = player.TeamId.Value;
            OnTeamTouch?.Invoke(_lastTeamTouch.Value);

            // Add to recent touchers
            if (!_recentTouchers.Contains(player))
            {
                _recentTouchers.Add(player);
                if (_recentTouchers.Count > 3)
                {
                    _recentTouchers.RemoveAt(0);
                }
            }

            // Apply charge if player was charging
            if (player.IsChargingShot.Value)
            {
                float chargeTransfer = player.CurrentCharge * chargeSettings.chargeMultiplier;
                AddCharge(chargeTransfer);
                player.ResetCharge();
            }

            // Visual and audio effects
            PlayBounceEffects(impactStrength, impactPoint);
            UpdateTeamVisuals();
        }

        private void HandleEnvironmentBounce(float impactStrength, Vector3 impactPoint, Vector3 impactNormal)
        {
            // Enhanced bounce physics
            float bouncePower = Mathf.Clamp01(impactStrength / 10f) * settings.bounceFactor;
            Vector3 bounceDirection = Vector3.Reflect(_lastVelocity.normalized, impactNormal);

            _rb.linearVelocity = bounceDirection * _lastVelocity.magnitude * bouncePower;

            // Effects
            PlayBounceEffects(impactStrength, impactPoint);
        }
        #endregion

        #region Charge System
        public void AddCharge(float amount)
        {
            _currentCharge.Value = Mathf.Clamp01(_currentCharge.Value + amount);
            OnChargeChanged?.Invoke(_currentCharge.Value);
            UpdateChargeEffects();

            if (_currentCharge.Value >= chargeSettings.minChargeForEffect && !_isCharging.Value)
            {
                StartCharging();
            }
        }

        private void StartCharging()
        {
            _isCharging.Value = true;
            PlayChargeEffects();
        }

        private void HandleChargeDrain()
        {
            if (_isCharging.Value && _currentCharge.Value > 0)
            {
                _currentCharge.Value -= chargeSettings.chargeDrainRate * Time.deltaTime;
                OnChargeChanged?.Invoke(_currentCharge.Value);

                if (_currentCharge.Value <= 0)
                {
                    StopCharging();
                }
            }
        }

        private void StopCharging()
        {
            _isCharging.Value = false;
            _currentCharge.Value = 0f;
            OnChargeChanged?.Invoke(_currentCharge.Value);
            UpdateChargeEffects();
        }

        public float GetChargeMultiplier()
        {
            return chargeSettings.chargeCurve.Evaluate(_currentCharge.Value);
        }
        #endregion

        #region Goal System
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerInitialized) return;

            AdvancedGoalTrigger goal = other.GetComponent<AdvancedGoalTrigger>();
            if (goal != null)
            {
                HandleGoalTrigger(goal);
            }
        }

        private void HandleGoalTrigger(AdvancedGoalTrigger goal)
        {
            // Prevent multiple goal triggers
            if (_wasInGoalArea) return;

            _wasInGoalArea = true;

            // Score goal - opposite team scores when ball enters their goal
            int scoringTeam = _lastTeamTouch.Value == 1 ? 2 : 1;

            if (goal.settings.teamId != _lastTeamTouch.Value)
            {
                AdvancedGameManager.Instance.ScoreGoal(scoringTeam, _lastTeamTouch.Value);
                PlayGoalEffects();
                OnGoalScored?.Invoke();

                // Reset ball after goal
                StartCoroutine(ResetBallAfterDelay(2f));
            }
        }

        [Server]
        public IEnumerator ResetBallAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ResetBall();
        }

        [Server]
        public void ResetBall()
        {
            // Reset physics
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = Vector3.zero;

            // Reset state
            _currentCharge.Value = 0f;
            _lastTeamTouch.Value = 0;
            _bounceCount.Value = 0;
            _isCharging.Value = false;
            _wasInGoalArea = false;
            _recentTouchers.Clear();

            // Reset visuals
            UpdateTeamVisuals();
            UpdateChargeEffects();

            OnBallReset?.Invoke();
        }
        #endregion

        #region Effects System
        private void HandleVisualEffects()
        {
            // Update trail based on speed
            float speedPercent = _rb.linearVelocity.magnitude / settings.maxSpeed;
            UpdateSpeedEffects(speedPercent);

            // Update charge effects
            if (_isCharging.Value)
            {
                UpdateChargeEffects();
            }
        }

        private void UpdateSpeedEffects(float speedPercent)
        {
            // Trail effects
            if (visual.trailRenderer != null)
            {
                visual.trailRenderer.emitting = speedPercent > 0.3f;
                visual.trailRenderer.widthMultiplier = speedPercent * 0.5f;
            }

            // Particle effects
            if (visual.speedTrailParticles != null)
            {
                var emission = visual.speedTrailParticles.emission;
                emission.rateOverTime = speedPercent * 100f;
            }

            // Light effects
            if (visual.ballLight != null)
            {
                visual.ballLight.intensity = speedPercent * 2f;
            }
        }

        private void UpdateChargeEffects()
        {
            if (visual.ballModel != null)
            {
                Renderer renderer = visual.ballModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (_isCharging.Value)
                    {
                        renderer.material = visual.chargedMaterial;
                        if (visual.ballLight != null)
                        {
                            visual.ballLight.color = visual.chargedLightColor;
                            visual.ballLight.intensity = _currentCharge.Value * 3f;
                        }
                    }
                    else
                    {
                        UpdateTeamVisuals();
                    }
                }
            }
        }

        private void UpdateTeamVisuals()
        {
            if (visual.ballModel != null)
            {
                Renderer renderer = visual.ballModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    switch (_lastTeamTouch.Value)
                    {
                        case 1: // Blue team
                            renderer.material = visual.teamBlueMaterial;
                            if (visual.ballLight != null)
                                visual.ballLight.color = visual.blueTeamColor;
                            break;
                        case 2: // Orange team
                            renderer.material = visual.teamOrangeMaterial;
                            if (visual.ballLight != null)
                                visual.ballLight.color = visual.orangeTeamColor;
                            break;
                        default:
                            renderer.material = visual.defaultMaterial;
                            if (visual.ballLight != null)
                                visual.ballLight.color = visual.defaultLightColor;
                            break;
                    }
                }
            }
        }

        private void PlayBounceEffects(float strength, Vector3 position)
        {
            // Audio effects
            if (_audioSource != null && visual.bounceSound != null)
            {
                float volume = Mathf.Lerp(visual.minBounceVolume, visual.maxBounceVolume, strength / 20f);
                float pitch = Mathf.Lerp(visual.minBouncePitch, visual.maxBouncePitch, strength / 20f);

                _audioSource.pitch = pitch;
                _audioSource.PlayOneShot(visual.bounceSound, volume);
            }

            // Particle effects
            if (visual.bounceParticles != null)
            {
                visual.bounceParticles.transform.position = position;
                visual.bounceParticles.Play();
            }
        }

        private void PlayGoalEffects()
        {
            if (_audioSource != null && visual.goalSound != null)
            {
                _audioSource.PlayOneShot(visual.goalSound);
            }

            if (visual.goalParticles != null)
            {
                visual.goalParticles.Play();
            }
        }

        private void PlayChargeEffects()
        {
            if (_audioSource != null && visual.chargeSound != null)
            {
                _audioSource.PlayOneShot(visual.chargeSound);
            }
        }
        #endregion

        #region Public API
        public void ApplyForce(Vector3 force, ForceMode forceMode = ForceMode.Impulse)
        {
            if (IsServerInitialized)
            {
                _rb.AddForce(force * GetChargeMultiplier(), forceMode);
            }
        }

        public void Kick(Vector3 direction, float strength, int teamId)
        {
            if (IsServerInitialized)
            {
                Vector3 force = direction.normalized * strength * GetChargeMultiplier();
                _rb.AddForce(force, ForceMode.Impulse);

                _lastTeamTouch.Value = teamId;
                OnTeamTouch?.Invoke(teamId);
                UpdateTeamVisuals();

                PlayBounceEffects(strength, transform.position);
            }
        }
        public void EnablePhysics(bool enable, bool resetVelocity = true, bool resetPosition = false, Vector3? newPosition = null)
        {
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = !enable;
                rb.detectCollisions = enable;

                if (enable)
                {
                    rb.WakeUp();

                    if (resetVelocity)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    if (resetPosition && newPosition.HasValue)
                    {
                        rb.position = newPosition.Value;
                    }
                }
                else
                {
                    rb.Sleep();
                }
            }

            var colliders = GetComponents<Collider>();
            foreach (var collider in colliders)
            {
                collider.enabled = enable;
            }
        }
        /*
        // Basic usage
    ball.EnablePhysics(true);  // Enable physics
    ball.EnablePhysics(false); // Disable physics

    // Advanced usage with options
    ball.EnablePhysics(true, resetVelocity: true); // Enable and reset velocity
    ball.EnablePhysics(true, resetPosition: true, newPosition: Vector3.zero); // Enable and reposition
        */

        public void Teleport(Vector3 position)
        {
            if (IsServerInitialized)
            {
                _rb.position = position;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        public bool IsMoving => _rb.linearVelocity.magnitude > settings.minSpeed;
        public float CurrentSpeed => _rb.linearVelocity.magnitude;
        public int LastTeamTouch => _lastTeamTouch.Value;
        public float CurrentCharge => _currentCharge.Value;
        public bool IsCharged => _isCharging.Value;
        public Vector3 Velocity => _rb.linearVelocity;
        #endregion

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

    }
}