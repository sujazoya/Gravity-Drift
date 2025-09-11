using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;

namespace zoya.game
{




    [RequireComponent(typeof(SphereCollider))]
    public class AdvancedGravityWell : NetworkBehaviour
    {
        [System.Serializable]
        public class GravityWellSettings
        {
            [Header("Core Settings")]
            public float strength = 50f;
            public float radius = 10f;
            public float lifetime = 3f;
            public bool isRepulsive = false;
            public ForceMode forceMode = ForceMode.Force;

            [Header("Advanced Physics")]
            public bool useInverseSquareLaw = true;
            public float minForceThreshold = 0.1f;
            public float maxForceCap = 100f;
            public bool affectRotation = false;
            public float rotationalForceMultiplier = 0.5f;

            [Header("Team Settings")]
            public bool teamSpecific = false;
            public int ownerTeamId = 0;
            public bool affectAllies = true;
            public bool affectEnemies = true;
            public bool affectNeutral = true;
        }

        [System.Serializable]
        public class VisualSettings
        {
            [Header("Appearance")]
            public Gradient colorGradient;
            public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 1, 1, 0);
            public ParticleSystem explosionEffect;
            public ParticleSystem constantEffect;

            [Header("Animation")]
            public float spawnAnimationDuration = 0.5f;
            public float collapseAnimationDuration = 0.3f;
            public AnimationCurve spawnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }

        [System.Serializable]
        public class AudioSettings
        {
            public AudioClip spawnSound;
            public AudioClip loopSound;
            public AudioClip collapseSound;
            public float volume = 0.7f;
            public float pitchVariation = 0.1f;
        }

        public GravityWellSettings settings = new GravityWellSettings();
        public VisualSettings visualSettings = new VisualSettings();
        public AudioSettings audioSettings = new AudioSettings();

        [Header("Network Settings")]
        public bool syncPhysics = true;
        public float networkUpdateRate = 0.1f;

        // Components
        private SphereCollider _triggerCollider;
        private AudioSource _audioSource;
        private ParticleSystem _constantEffectInstance;
        private Renderer _visualRenderer;
        private Material _dynamicMaterial;

        // Runtime variables
        private float _currentLifetime;
        private bool _isCollapsing = false;
        private HashSet<NetworkObject> _affectedObjects = new HashSet<NetworkObject>();
        private NetworkConnection _ownerConnection;
        private int _ownerPlayerId = -1;

        // Properties
        public NetworkConnection OwnerConnection => _ownerConnection;
        public int OwnerPlayerId => _ownerPlayerId;
        public float CurrentStrength => settings.strength * GetLifetimeFactor();
        public bool IsActive => !_isCollapsing && _currentLifetime > 0;

        #region Network Initialization
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            InitializeGravityWell();
        }

        /// <summary>
        /// Sets the well type with multipliers and custom color
        /// </summary>
        /// <param name="typeName">Name or type identifier</param>
        /// <param name="strengthMultiplier">Multiplier for the base strength</param>
        /// <param name="radiusMultiplier">Multiplier for the base radius</param>
        /// <param name="wellColor">Custom color for the well</param>
        public void SetWellType(string typeName, float strengthMultiplier = 1f, float radiusMultiplier = 1f, Color? wellColor = null)
        {
            // Determine if repulsive based on type name (you can customize this logic)
            bool isRepulsive = typeName.ToLower().Contains("repulsive") ||
                              typeName.ToLower().Contains("push") ||
                              typeName.ToLower().Contains("explosive");

            // Calculate new values based on multipliers
            float newStrength = settings.strength * strengthMultiplier;
            float newRadius = settings.radius * radiusMultiplier;

            // Set the basic well type
            SetWellType(isRepulsive.ToString(), newStrength, newRadius);

            // Apply custom color if provided
            if (wellColor.HasValue)
            {
                SetWellColor(wellColor.Value);
            }

            // Optional: You can add type-specific behaviors here
            ApplyTypeSpecificBehavior(typeName);
        }

        /// <summary>
        /// Sets a custom color for the gravity well
        /// </summary>
        /// <summary>
        /// Sets a custom color for the gravity well
        /// </summary>
        public void SetWellColor(Color color)
        {
            // Update the gradient with the custom color
            visualSettings.colorGradient = CreateDefaultGradient(color);

            // Update current material color
            if (_dynamicMaterial != null)
            {
                float lifetimeFactor = GetLifetimeFactor();
                Color currentColor = visualSettings.colorGradient.Evaluate(1f - lifetimeFactor);
                _dynamicMaterial.color = currentColor;
            }

            // Update particle system color if exists
            if (_constantEffectInstance != null)
            {
                var main = _constantEffectInstance.main;
                float lifetimeFactor = GetLifetimeFactor();
                Color currentColor = visualSettings.colorGradient.Evaluate(1f - lifetimeFactor);
                main.startColor = currentColor;
            }

            // Sync across network
            if (IsServerInitialized)
            {
                SyncWellColorRpc(color);
            }
        }

        [ObserversRpc]
        private void SyncWellColorRpc(Color color)
        {
            SetWellColor(color);
        }

        /// <summary>
        /// Applies type-specific behaviors based on the type name
        /// </summary>
        private void ApplyTypeSpecificBehavior(string typeName)
        {
            typeName = typeName.ToLower();

            if (typeName.Contains("vortex"))
            {
                // Vortex type: stronger rotational forces
                settings.affectRotation = true;
                settings.rotationalForceMultiplier = 2f;
            }
            else if (typeName.Contains("blackhole"))
            {
                // Black hole type: very strong attraction
                settings.isRepulsive = false;
                settings.strength *= 2f;
                settings.useInverseSquareLaw = true;
            }
            else if (typeName.Contains("explosive"))
            {
                // Explosive type: repulsive with team damage settings
                settings.isRepulsive = true;
                settings.affectAllies = false; // Don't affect allies with explosions
            }
            else if (typeName.Contains("healing"))
            {
                // Healing type: attractive but only affects allies
                settings.isRepulsive = false;
                settings.teamSpecific = true;
                settings.affectEnemies = false;
                settings.affectNeutral = false;
            }

            // Sync type-specific behaviors across network
            if (IsServerInitialized)
            {
                SyncTypeBehaviorRpc(typeName);
            }
        }

        [ObserversRpc]
        private void SyncTypeBehaviorRpc(string typeName)
        {
            ApplyTypeSpecificBehavior(typeName);
        }


        /// <summary>
        /// Changes the type of gravity well (attractive/repulsive) and its properties
        /// </summary>
        /// <param name="isRepulsive">Whether the well should be repulsive</param>
        /// <param name="newStrength">Optional new strength value</param>
        /// <param name="newRadius">Optional new radius value</param>
        public void SetWellType(bool isRepulsive, float newStrength = -1f, float newRadius = -1f)
        {
            settings.isRepulsive = isRepulsive;

            if (newStrength > 0)
                settings.strength = newStrength;

            if (newRadius > 0)
            {
                settings.radius = newRadius;
                // Update collider size
                if (_triggerCollider != null)
                    _triggerCollider.radius = newRadius;

                // Update visual scale
                if (_visualRenderer != null)
                    _visualRenderer.transform.localScale = Vector3.one * newRadius * 2f;

                // Update particle effect scale
                if (_constantEffectInstance != null)
                    _constantEffectInstance.transform.localScale = Vector3.one * newRadius;

                // Update audio max distance
                if (_audioSource != null)
                    _audioSource.maxDistance = newRadius * 2f;
            }

            // Update visuals to reflect new type
            UpdateWellTypeVisuals();

            // Sync across network
            if (IsServerInitialized)
            {
                SyncWellTypeRpc(isRepulsive, newStrength, newRadius);
            }
        }


        /// <summary>
        /// Updates the visual appearance based on the current well type
        /// </summary>
        private void UpdateWellTypeVisuals()
        {
            if (_visualRenderer != null && _dynamicMaterial != null)
            {
                // Determine base color based on well type
                Color baseColor = settings.isRepulsive ? Color.red : Color.blue;

                // Create or update gradient
                if (visualSettings.colorGradient == null || visualSettings.colorGradient.colorKeys.Length == 0)
                {
                    visualSettings.colorGradient = CreateDefaultGradient(baseColor);
                }
                else
                {
                    // Update existing gradient with new base color
                    UpdateGradientWithBaseColor(baseColor);
                }

                // Update current material color
                float lifetimeFactor = GetLifetimeFactor();
                Color currentColor = visualSettings.colorGradient.Evaluate(1f - lifetimeFactor);
                _dynamicMaterial.color = currentColor;
            }

            // Update particle system color if exists
            if (_constantEffectInstance != null)
            {
                var main = _constantEffectInstance.main;
                float lifetimeFactor = GetLifetimeFactor();
                Color currentColor = visualSettings.colorGradient.Evaluate(1f - lifetimeFactor);
                main.startColor = currentColor;
            }
        }

        /// <summary>
        /// Creates a default gradient based on a base color
        /// </summary>
        private Gradient CreateDefaultGradient(Color baseColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
            new GradientColorKey(baseColor, 0f),
            new GradientColorKey(baseColor * 1.5f, 1f)
                },
                new GradientAlphaKey[] {
            new GradientAlphaKey(0.8f, 0f),
            new GradientAlphaKey(0.3f, 1f)
                }
            );
            return gradient;
        }

        /// <summary>
        /// Updates the existing gradient with a new base color
        /// </summary>
        private void UpdateGradientWithBaseColor(Color baseColor)
        {
            GradientColorKey[] colorKeys = visualSettings.colorGradient.colorKeys;
            GradientAlphaKey[] alphaKeys = visualSettings.colorGradient.alphaKeys;

            // Update the first color key with the new base color
            if (colorKeys.Length > 0)
            {
                colorKeys[0].color = baseColor;
            }

            // Update the second color key with a brighter version
            if (colorKeys.Length > 1)
            {
                colorKeys[1].color = baseColor * 1.5f;
            }

            visualSettings.colorGradient.SetKeys(colorKeys, alphaKeys);
        }

        /// <summary>
        /// Network synchronization for well type changes
        /// </summary>
        [ObserversRpc]
        private void SyncWellTypeRpc(bool isRepulsive, float newStrength, float newRadius)
        {
            settings.isRepulsive = isRepulsive;

            if (newStrength > 0)
                settings.strength = newStrength;

            if (newRadius > 0)
            {
                settings.radius = newRadius;
                if (_triggerCollider != null)
                    _triggerCollider.radius = newRadius;
                if (_visualRenderer != null)
                    _visualRenderer.transform.localScale = Vector3.one * newRadius * 2f;
                if (_constantEffectInstance != null)
                    _constantEffectInstance.transform.localScale = Vector3.one * newRadius;
                if (_audioSource != null)
                    _audioSource.maxDistance = newRadius * 2f;
            }

            UpdateWellTypeVisuals();
        }


        [ServerRpc(RequireOwnership = false)]
        public void InitializeWell(int ownerId, NetworkConnection ownerConn = null)
        {
            _ownerPlayerId = ownerId;
            _ownerConnection = ownerConn;
            InitializeGravityWellRpc(ownerId);
        }

        [ObserversRpc]
        private void InitializeGravityWellRpc(int ownerId)
        {
            _ownerPlayerId = ownerId;
            InitializeGravityWell();
        }

        private void InitializeGravityWell()
        {
            SetupComponents();
            SetupVisuals();
            SetupAudio();
            StartCoroutine(LifetimeCoroutine());

            if (IsServerInitialized)
            {
                StartCoroutine(NetworkUpdateCoroutine());
            }
        }
        #endregion

        #region Setup Methods
        private void SetupComponents()
        {
            // Collider setup
            _triggerCollider = GetComponent<SphereCollider>();
            _triggerCollider.radius = settings.radius;
            _triggerCollider.isTrigger = true;

            // Audio source
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.maxDistance = settings.radius * 2f;
        }

        private void SetupVisuals()
        {
            // Create visual representation
            GameObject visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualSphere.transform.SetParent(transform);
            visualSphere.transform.localPosition = Vector3.zero;
            visualSphere.transform.localScale = Vector3.one * settings.radius * 2f;

            // Remove collider from visual
            Destroy(visualSphere.GetComponent<Collider>());

            _visualRenderer = visualSphere.GetComponent<Renderer>();

            // Create dynamic material
            _dynamicMaterial = new Material(Shader.Find("Standard"));
            _dynamicMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _dynamicMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _dynamicMaterial.SetInt("_ZWrite", 0);
            _dynamicMaterial.DisableKeyword("_ALPHATEST_ON");
            _dynamicMaterial.EnableKeyword("_ALPHABLEND_ON");
            _dynamicMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _dynamicMaterial.renderQueue = 3000;

            _visualRenderer.material = _dynamicMaterial;

            // Create constant effect if specified
            if (visualSettings.constantEffect != null)
            {
                _constantEffectInstance = Instantiate(visualSettings.constantEffect, transform);
                _constantEffectInstance.transform.localScale = Vector3.one * settings.radius;
            }

            // Start spawn animation
            StartCoroutine(SpawnAnimation());
        }

        private void SetupAudio()
        {
            if (audioSettings.spawnSound != null)
            {
                _audioSource.PlayOneShot(audioSettings.spawnSound, audioSettings.volume);
            }

            if (audioSettings.loopSound != null)
            {
                _audioSource.clip = audioSettings.loopSound;
                _audioSource.loop = true;
                _audioSource.PlayDelayed(0.1f);
            }
        }
        #endregion

        #region Core Functionality
        private void FixedUpdate()
        {
            if (!IsActive) return;

            UpdateVisuals();
            ApplyGravityForces();
        }

        private void ApplyGravityForces()
        {
            // This is now handled in OnTriggerStay for better performance
            // Forces are applied there when objects enter the trigger
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsActive) return;
            if (!ShouldAffectObject(other)) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            ApplyGravityForce(rb);
        }

        private void ApplyGravityForce(Rigidbody rb)
        {
            Vector3 direction = (rb.position - transform.position).normalized;
            if (settings.isRepulsive)
                direction = -direction;

            float distance = Vector3.Distance(rb.position, transform.position);
            float distanceFactor = CalculateDistanceFactor(distance);

            if (distanceFactor < settings.minForceThreshold)
                return;

            Vector3 force = direction * CurrentStrength * distanceFactor;
            force = Vector3.ClampMagnitude(force, settings.maxForceCap);

            // Apply force
            rb.AddForce(force, settings.forceMode);

            // Apply rotational force if enabled
            if (settings.affectRotation)
            {
                Vector3 torque = Vector3.Cross(rb.transform.up, direction).normalized *
                               settings.rotationalForceMultiplier * distanceFactor;
                rb.AddTorque(torque, settings.forceMode);
            }

            // Track affected object
            NetworkObject netObj = rb.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                _affectedObjects.Add(netObj);
            }
        }

        private float CalculateDistanceFactor(float distance)
        {
            if (distance > settings.radius) return 0f;

            if (settings.useInverseSquareLaw)
            {
                // Inverse square law with falloff
                float normalizedDistance = distance / settings.radius;
                return Mathf.Clamp01(1f - (normalizedDistance * normalizedDistance));
            }
            else
            {
                // Linear falloff
                return 1f - (distance / settings.radius);
            }
        }

        private bool ShouldAffectObject(Collider other)
        {
            // Don't affect self or other gravity wells
            if (other.gameObject == gameObject || other.GetComponent<AdvancedGravityWell>() != null)
                return false;

            // Check team restrictions if enabled
            if (settings.teamSpecific)
            {
                TeamAffiliation targetTeam = other.GetComponent<TeamAffiliation>();
                if (targetTeam != null)
                {
                    int targetTeamId = targetTeam.TeamId;

                    if (targetTeamId == settings.ownerTeamId)
                        return settings.affectAllies;
                    else if (targetTeamId == 0) // Neutral
                        return settings.affectNeutral;
                    else
                        return settings.affectEnemies;
                }
            }

            return true;
        }
        #endregion

        #region Visual & Audio Effects
        private void UpdateVisuals()
        {
            if (_visualRenderer != null)
            {
                float lifetimeFactor = GetLifetimeFactor();
                Color currentColor = visualSettings.colorGradient.Evaluate(1f - lifetimeFactor);
                _dynamicMaterial.color = currentColor;

                float scale = visualSettings.scaleCurve.Evaluate(1f - lifetimeFactor);
                transform.localScale = Vector3.one * scale;
            }

            if (_constantEffectInstance != null)
            {
                var main = _constantEffectInstance.main;
                main.startColor = visualSettings.colorGradient.Evaluate(1f - GetLifetimeFactor());
            }
        }

        private IEnumerator SpawnAnimation()
        {
            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.1f;
            Vector3 targetScale = Vector3.one;

            while (elapsed < visualSettings.spawnAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = visualSettings.spawnCurve.Evaluate(elapsed / visualSettings.spawnAnimationDuration);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
        }

        private IEnumerator CollapseAnimation()
        {
            _isCollapsing = true;

            // Play collapse sound
            if (audioSettings.collapseSound != null)
            {
                _audioSource.Stop();
                _audioSource.PlayOneShot(audioSettings.collapseSound, audioSettings.volume);
            }

            // Play explosion effect
            if (visualSettings.explosionEffect != null)
            {
                ParticleSystem explosion = Instantiate(visualSettings.explosionEffect, transform.position, Quaternion.identity);
                explosion.transform.localScale = Vector3.one * settings.radius * 0.5f;
                explosion.Play();
                Destroy(explosion.gameObject, explosion.main.duration);
            }

            // Animate collapse
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < visualSettings.collapseAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / visualSettings.collapseAnimationDuration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                // Fade out audio
                if (_audioSource.isPlaying)
                {
                    _audioSource.volume = Mathf.Lerp(audioSettings.volume, 0f, t);
                }

                yield return null;
            }

            // Network destruction
            if (IsServerInitialized)
            {
                DespawnGravityWell();
            }
        }
        #endregion

        #region Network & Lifetime Management
        private IEnumerator LifetimeCoroutine()
        {
            _currentLifetime = settings.lifetime;

            while (_currentLifetime > 0 && !_isCollapsing)
            {
                _currentLifetime -= Time.deltaTime;
                yield return null;
            }

            if (!_isCollapsing)
            {
                StartCoroutine(CollapseAnimation());
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TriggerCollapse()
        {
            TriggerCollapseRpc();
        }

        [ObserversRpc]
        private void TriggerCollapseRpc()
        {
            if (!_isCollapsing)
            {
                StartCoroutine(CollapseAnimation());
            }
        }

        [Server]
        private void DespawnGravityWell()
        {
            // Notify affected objects
            foreach (var netObj in _affectedObjects)
            {
                if (netObj != null)
                {
                    GravityWellAffected affected = netObj.GetComponent<GravityWellAffected>();
                    if (affected != null)
                    {
                        affected.OnGravityWellEnded(this);
                    }
                }
            }

            // Despawn network object
            base.Despawn();
        }

        private IEnumerator NetworkUpdateCoroutine()
        {
            while (IsActive)
            {
                yield return new WaitForSeconds(networkUpdateRate);
                UpdateNetworkState();
            }
        }

        [ObserversRpc]
        private void UpdateNetworkState()
        {
            // Sync visual state across network
            UpdateVisuals();
        }
        #endregion

        #region Utility Methods
        private float GetLifetimeFactor()
        {
            return Mathf.Clamp01(_currentLifetime / settings.lifetime);
        }

        public float GetForceAtPosition(Vector3 position)
        {
            float distance = Vector3.Distance(position, transform.position);
            if (distance > settings.radius) return 0f;

            float distanceFactor = CalculateDistanceFactor(distance);
            return CurrentStrength * distanceFactor;
        }

        public Vector3 GetForceVectorAtPosition(Vector3 position)
        {
            Vector3 direction = (position - transform.position).normalized;
            if (settings.isRepulsive)
                direction = -direction;

            float forceMagnitude = GetForceAtPosition(position);
            return direction * forceMagnitude;
        }
        #endregion

        #region Public API
        public void ModifyStrength(float multiplier, float duration = 0f)
        {
            float originalStrength = settings.strength;
            settings.strength *= multiplier;

            if (duration > 0f)
            {
                StartCoroutine(ResetStrengthAfterDelay(originalStrength, duration));
            }
        }

        private IEnumerator ResetStrengthAfterDelay(float originalStrength, float delay)
        {
            yield return new WaitForSeconds(delay);
            settings.strength = originalStrength;
        }

        public void ExtendLifetime(float additionalTime)
        {
            _currentLifetime += additionalTime;
        }

        public void SetTeamRestrictions(int teamId, bool affectAllies, bool affectEnemies, bool affectNeutral)
        {
            settings.teamSpecific = true;
            settings.ownerTeamId = teamId;
            settings.affectAllies = affectAllies;
            settings.affectEnemies = affectEnemies;
            settings.affectNeutral = affectNeutral;
        }
        #endregion

        #region Editor & Debug
        private void OnDrawGizmosSelected()
        {
            // Draw force field
            Gizmos.color = settings.isRepulsive ? Color.red : Color.blue;
            Gizmos.DrawWireSphere(transform.position, settings.radius);

            // Draw force direction indicators
            int rays = 12;
            for (int i = 0; i < rays; i++)
            {
                float angle = i * Mathf.PI * 2 / rays;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 forceDir = settings.isRepulsive ? dir : -dir;

                Gizmos.color = settings.isRepulsive ? Color.red : Color.blue;
                Gizmos.DrawRay(transform.position + dir * settings.radius * 0.9f,
                              forceDir * settings.radius * 0.2f);
            }

            // Draw strength indicator
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, settings.radius * GetLifetimeFactor());
        }

        [ContextMenu("Test Force Calculation")]
        private void TestForceCalculation()
        {
            Vector3 testPosition = transform.position + Vector3.forward * settings.radius * 0.5f;
            float force = GetForceAtPosition(testPosition);
            Debug.Log($"Force at {testPosition}: {force}");
        }
        #endregion

        private void OnDestroy()
        {
            // Cleanup
            if (_dynamicMaterial != null)
            {
                Destroy(_dynamicMaterial);
            }

            if (_constantEffectInstance != null)
            {
                Destroy(_constantEffectInstance.gameObject);
            }
        }
        #region Team Management

        public void SetTeam(int teamId)
        {
            settings.teamSpecific = true;
            settings.ownerTeamId = teamId;

            // Update visual team color if needed
            UpdateTeamVisuals(teamId);

            // Sync across network if this is the server
            if (IsServerInitialized)
            {
                RpcSyncTeam(teamId);
            }
        }

        [ObserversRpc]
        private void RpcSyncTeam(int teamId)
        {
            settings.teamSpecific = true;
            settings.ownerTeamId = teamId;
            UpdateTeamVisuals(teamId);
        }

        private void UpdateTeamVisuals(int teamId)
        {
            // Change visual appearance based on team
            if (_visualRenderer != null && _dynamicMaterial != null)
            {
                Color teamColor = GetTeamColor(teamId);

                // Create a new gradient with team colors
                Gradient newGradient = new Gradient();
                newGradient.SetKeys(
                    new GradientColorKey[] {
                new GradientColorKey(teamColor, 0f),
                new GradientColorKey(teamColor * 1.5f, 1f)
                    },
                    new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.3f, 1f)
                    }
                );

                visualSettings.colorGradient = newGradient;

                // Update current color
                _dynamicMaterial.color = teamColor;
            }
        }

        #region Fade Out Methods

        /// <summary>
        /// Starts a smooth fade out animation for the gravity well
        /// </summary>
        /// <param name="fadeDuration">Duration of the fade out in seconds</param>
        public void StartFadeOut(float fadeDuration = 1.0f)
        {
            if (_isCollapsing) return;

            // Override the collapse animation duration
            visualSettings.collapseAnimationDuration = fadeDuration;

            // Trigger collapse with custom duration
            if (IsServerInitialized)
            {
                StartFadeOutRpc(fadeDuration);
            }
            else
            {
                // If not server, request server to trigger fade out
                RequestFadeOutServerRpc(fadeDuration);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestFadeOutServerRpc(float fadeDuration)
        {
            StartFadeOutRpc(fadeDuration);
        }

        [ObserversRpc]
        private void StartFadeOutRpc(float fadeDuration)
        {
            visualSettings.collapseAnimationDuration = fadeDuration;
            if (!_isCollapsing)
            {
                StartCoroutine(CollapseAnimation());
            }
        }

        /// <summary>
        /// Coroutine for smooth fade out effect
        /// </summary>
        private IEnumerator FadeOutCoroutine(float duration)
        {
            _isCollapsing = true;

            float elapsed = 0f;
            float startVolume = _audioSource != null ? _audioSource.volume : 0f;
            Vector3 startScale = transform.localScale;
            Color startColor = _dynamicMaterial != null ? _dynamicMaterial.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Fade out scale
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                // Fade out audio
                if (_audioSource != null && _audioSource.isPlaying)
                {
                    _audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
                }

                // Fade out color
                if (_dynamicMaterial != null)
                {
                    Color fadedColor = startColor;
                    fadedColor.a = Mathf.Lerp(startColor.a, 0f, t);
                    _dynamicMaterial.color = fadedColor;
                }

                yield return null;
            }

            // Network destruction
            if (IsServerInitialized)
            {
                DespawnGravityWell();
            }
        }

        #endregion

        private Color GetTeamColor(int teamId)
        {
            // Default team colors - you can customize these
            return teamId switch
            {
                1 => Color.red,
                2 => Color.blue,
                3 => Color.green,
                4 => Color.yellow,
                _ => Color.magenta // Neutral/unknown team
            };
        }

        public void SetTeamRestrictions(bool affectAllies, bool affectEnemies, bool affectNeutral)
        {
            settings.affectAllies = affectAllies;
            settings.affectEnemies = affectEnemies;
            settings.affectNeutral = affectNeutral;

            if (IsServerInitialized)
            {
                RpcSyncTeamRestrictions(affectAllies, affectEnemies, affectNeutral);
            }
        }

        [ObserversRpc]
        private void RpcSyncTeamRestrictions(bool affectAllies, bool affectEnemies, bool affectNeutral)
        {
            settings.affectAllies = affectAllies;
            settings.affectEnemies = affectEnemies;
            settings.affectNeutral = affectNeutral;
        }

        public int GetTeam()
        {
            return settings.ownerTeamId;
        }

        public bool IsFriendlyTeam(int otherTeamId)
        {
            if (!settings.teamSpecific) return true;
            return settings.ownerTeamId == otherTeamId;
        }

        public bool ShouldAffectTeam(int otherTeamId)
        {
            if (!settings.teamSpecific) return true;

            if (otherTeamId == settings.ownerTeamId)
                return settings.affectAllies;
            else if (otherTeamId == 0) // Neutral
                return settings.affectNeutral;
            else
                return settings.affectEnemies;
        }

        #endregion
    }

    // Supporting class for team system
    public class TeamAffiliation : MonoBehaviour
    {
        public int TeamId = 0;
        public string TeamName = "Neutral";
    }

    // Supporting class for affected objects
    public class GravityWellAffected : MonoBehaviour
    {
        public virtual void OnGravityWellEnded(AdvancedGravityWell well)
        {
            // Implement reaction to gravity well ending
            Debug.Log($"Gravity well ended affecting {gameObject.name}");
        }
    }
}