using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Managing;
using FishNet;
using FishNet.Managing.Object;

public class AdvancedBallPossessionTracker : NetworkBehaviour
{
    [System.Serializable]
    public class PossessionSettings
    {
        [Header("Possession Detection")]
        public float possessionRadius = 3f;
        public float possessionCooldown = 1f;
        public float minPossessionTime = 0.5f;
        public float maxPossessionTime = 10f;
        public LayerMask playerLayerMask = -1;

        [Header("Visual Feedback")]
        public Color neutralColor = Color.white;
        public Color contestedColor = Color.yellow;
        public float possessionPulseSpeed = 2f;
        public float possessionPulseIntensity = 0.3f;
    }

    [System.Serializable]
    public class PossessionState
    {
        public AdvancedNetworkPlayerController currentOwner;
        public int owningTeam = 0;
        public float possessionTime = 0f;
        public float cooldownTimer = 0f;
        public bool isContested = false;
        public List<AdvancedNetworkPlayerController> contestingPlayers = new List<AdvancedNetworkPlayerController>();
    }

    public PossessionSettings settings = new PossessionSettings();
    public PossessionState state = new PossessionState();

    [Header("References")]
    public AdvancedBall ball;
    public Renderer possessionIndicator;
    public Light possessionLight;
    public ParticleSystem possessionParticles;
    public AudioSource possessionAudio;

    [Header("Audio")]
    public AudioClip possessionGainedSound;
    public AudioClip possessionLostSound;
    public AudioClip contestedSound;

    private Coroutine _possessionCoroutine;
    private Material _possessionMaterial;
    private Color _currentColor;
    private float _pulseTimer = 0f;

    public event System.Action<AdvancedNetworkPlayerController> OnPossessionGained;
    public event System.Action<AdvancedNetworkPlayerController> OnPossessionLost;
    public event System.Action<int> OnTeamPossessionChanged;
    public event System.Action OnBallContested;
    public event System.Action OnBallUncontested;

    #region Initialization
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (possessionIndicator != null)
        {
            _possessionMaterial = possessionIndicator.material;
            _currentColor = settings.neutralColor;
            UpdatePossessionVisuals();
        }

        if (IsServerInitialized)
        {
            StartCoroutine(PossessionUpdateCoroutine());
        }
    }
    #endregion

    #region Server-Side Possession Logic
    [Server]
    private IEnumerator PossessionUpdateCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f); // 10 updates per second

            if (ball == null || !ball.gameObject.activeInHierarchy) continue;

            UpdatePossessionState();
            HandlePossessionCooldown();
        }
    }

    [Server]
    private void UpdatePossessionState()
    {
        if (state.cooldownTimer > 0) return;

        Collider[] playersInRange = Physics.OverlapSphere(ball.transform.position, settings.possessionRadius, settings.playerLayerMask);
        List<AdvancedNetworkPlayerController> nearbyPlayers = new List<AdvancedNetworkPlayerController>();

        foreach (Collider col in playersInRange)
        {
            AdvancedNetworkPlayerController player = col.GetComponent<AdvancedNetworkPlayerController>();
            if (player != null && player.IsAlive)
            {
                nearbyPlayers.Add(player);
            }
        }

        if (nearbyPlayers.Count == 0)
        {
            ClearPossession();
            return;
        }

        HandlePossessionContest(nearbyPlayers);
        DetermineNewOwner(nearbyPlayers);
                foreach (Collider col in playersInRange)
        {
            AdvancedNetworkPlayerController player = col.GetComponent<AdvancedNetworkPlayerController>();
            if (player != null && player.IsAlive)
            {
                nearbyPlayers.Add(player);
            }
        }

    }

    [Server]
    private void HandlePossessionContest(List<AdvancedNetworkPlayerController> nearbyPlayers)
    {
        bool wasContested = state.isContested;
        state.contestingPlayers = nearbyPlayers;
        state.isContested = nearbyPlayers.Count > 1;

        if (state.isContested && !wasContested)
        {
            OnBallContested?.Invoke();
            BallContestedRpc();
        }
        else if (!state.isContested && wasContested)
        {
            OnBallUncontested?.Invoke();
            BallUncontestedRpc();
        }
    }

    [Server]
    private void DetermineNewOwner(List<AdvancedNetworkPlayerController> nearbyPlayers)
    {
        if (state.isContested && state.currentOwner != null)
        {
            // Keep current owner during contest, but reset timer
            state.possessionTime = 0f;
            return;
        }

        // Find closest player
        AdvancedNetworkPlayerController closestPlayer = nearbyPlayers
            .OrderBy(p => Vector3.Distance(p.transform.position, ball.transform.position))
            .FirstOrDefault();

        if (closestPlayer != null)
        {
            if (state.currentOwner != closestPlayer)
            {
                SetNewOwner(closestPlayer);
            }
            else
            {
                UpdatePossessionTime();
            }
        }
    }

    [Server]
    private void SetNewOwner(AdvancedNetworkPlayerController newOwner)
    {
        AdvancedNetworkPlayerController previousOwner = state.currentOwner;

        // Notify previous owner
        if (previousOwner != null)
        {
            previousOwner.InvokeLostBallPossession();
            OnPossessionLost?.Invoke(previousOwner);
        }

        // Set new owner
        state.currentOwner = newOwner;
        state.owningTeam = newOwner.TeamId.Value;
        state.possessionTime = 0f;
        state.cooldownTimer = settings.possessionCooldown;

        // Notify new owner
        newOwner.InvokeGainedBallPossession();   // ✅ correct
        OnPossessionGained?.Invoke(newOwner);
        OnTeamPossessionChanged?.Invoke(newOwner.TeamId.Value);

        // Update clients
        PossessionChangedRpc(newOwner?.ObjectId ?? 0, newOwner?.TeamId.Value ?? 0);
    }

    [Server]
    private void UpdatePossessionTime()
    {
        state.possessionTime += 0.1f;
        
        if (state.possessionTime >= settings.maxPossessionTime)
        {
            ClearPossession();
        }
    }

    [Server]
    private void ClearPossession()
    {
        if (state.currentOwner != null)
        {
            state.currentOwner.InvokeLostBallPossession();
            OnPossessionLost?.Invoke(state.currentOwner);
        }

        state.currentOwner = null;
        state.owningTeam = 0;
        state.possessionTime = 0f;
        state.cooldownTimer = settings.possessionCooldown;
        state.isContested = false;
        state.contestingPlayers.Clear();

        OnTeamPossessionChanged?.Invoke(0);
        PossessionChangedRpc(0, 0);
    }

    [Server]
    private void HandlePossessionCooldown()
    {
        if (state.cooldownTimer > 0)
        {
            state.cooldownTimer -= 0.1f;
        }
    }
    #endregion

    #region RPC Methods
    [ObserversRpc]
    private void PossessionChangedRpc(int playerObjectId, int teamId)
{
    AdvancedNetworkPlayerController newOwner = null;

    if (playerObjectId != 0)
    {
            // Convert int to ulong (FishNet uses ulong for NetworkObjectId)
            //  ulong objectId = (ulong)playerObjectId;

             playerObjectId = 42;

        // Get the NetworkObject from the server manager
            if (NetworkObjectFinder.TryGetNetworkObjectById(playerObjectId, out NetworkObject playerObj))
            {
                // Success, got the object
            }
            else
            {
                Debug.LogWarning($"NetworkObject with ID {playerObjectId} not found!");
            }
    }

    state.currentOwner = newOwner;
    state.owningTeam = teamId;

    UpdatePossessionVisuals();
    PlayPossessionSound(newOwner != null);
}


    [ObserversRpc]
    private void BallContestedRpc()
    {
        state.isContested = true;
        UpdatePossessionVisuals();
        PlayContestedSound();
    }

    [ObserversRpc]
    private void BallUncontestedRpc()
    {
        state.isContested = false;
        UpdatePossessionVisuals();
    }
    #endregion

    #region Visual & Audio Feedback
    private void Update()
    {
        if (!IsServerInitialized)
        {
            UpdatePossessionVisuals();
        }

        HandlePulseEffect();
    }

    private void UpdatePossessionVisuals()
    {
        if (_possessionMaterial == null) return;

        Color targetColor = GetPossessionColor();
        _currentColor = Color.Lerp(_currentColor, targetColor, 10f * Time.deltaTime);

        _possessionMaterial.SetColor("_EmissionColor", _currentColor);

        if (possessionLight != null)
        {
            possessionLight.color = _currentColor;
            possessionLight.intensity = state.isContested ? 2f : 1f;
        }

        if (possessionParticles != null)
        {
            var main = possessionParticles.main;
            main.startColor = _currentColor;
            
            if (state.currentOwner != null && !possessionParticles.isPlaying)
            {
                possessionParticles.Play();
            }
            else if (state.currentOwner == null && possessionParticles.isPlaying)
            {
                possessionParticles.Stop();
            }
        }
    }

    private Color GetPossessionColor()
    {
        if (state.isContested) return settings.contestedColor;
        if (state.currentOwner != null) return AdvancedTeamManager.Instance.GetTeamColor(state.owningTeam);
        return settings.neutralColor;
    }

    private void HandlePulseEffect()
    {
        if (state.currentOwner != null && !state.isContested)
        {
            _pulseTimer += Time.deltaTime * settings.possessionPulseSpeed;
            float pulse = Mathf.Sin(_pulseTimer) * settings.possessionPulseIntensity + 1f;
            
            if (possessionLight != null)
            {
                possessionLight.intensity = pulse;
            }
        }
    }

    private void PlayPossessionSound(bool gained)
    {
        if (possessionAudio != null)
        {
            possessionAudio.PlayOneShot(gained ? possessionGainedSound : possessionLostSound);
        }
    }

    private void PlayContestedSound()
    {
        if (possessionAudio != null && contestedSound != null)
        {
            possessionAudio.PlayOneShot(contestedSound);
        }
    }
    #endregion

    #region Public API
    public AdvancedNetworkPlayerController GetCurrentOwner() => state.currentOwner;
    public int GetOwningTeam() => state.owningTeam;
    public bool IsContested() => state.isContested;
    public float GetPossessionTime() => state.possessionTime;
    public bool HasPossession(AdvancedNetworkPlayerController player) => state.currentOwner == player;
    public bool TeamHasPossession(int teamId) => state.owningTeam == teamId;

    [Server]
    public void ForceSetOwner(AdvancedNetworkPlayerController newOwner)
    {
        if (newOwner != null)
        {
            SetNewOwner(newOwner);
        }
        else
        {
            ClearPossession();
        }
    }

    [Server]
    public void ResetPossession()
    {
        ClearPossession();
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        if (ball != null)
        {
            Gizmos.color = state.isContested ? Color.yellow : 
                          state.currentOwner != null ? AdvancedTeamManager.Instance.GetTeamColor(state.owningTeam) : Color.white;
            Gizmos.DrawWireSphere(ball.transform.position, settings.possessionRadius);
        }
    }
    #endregion

    private void OnDestroy()
    {
        if (_possessionCoroutine != null)
            StopCoroutine(_possessionCoroutine);

        if (_possessionMaterial != null)
            Destroy(_possessionMaterial);
    }
}