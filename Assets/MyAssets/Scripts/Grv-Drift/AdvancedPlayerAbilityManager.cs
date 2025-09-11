using FishNet.Object;
using UnityEngine;
using System.Collections;
using TMPro;

public class AdvancedPlayerAbilityManager : NetworkBehaviour
{
    [System.Serializable]
    public class AbilitySettings
    {
        [Header("Gravity Well Settings")]
        public float gravityWellCooldown = 10f;
        public int maxWells = 2;
        public float wellLifetime = 5f;
        public float wellPlacementRange = 5f;
        public KeyCode abilityKey = KeyCode.Space;

        [Header("Boost Settings")]
        public float boostCooldown = 5f;
        public float boostDuration = 2f;
        public float boostMultiplier = 2f;
        public KeyCode boostKey = KeyCode.LeftShift;

        [Header("Special Abilities")]
        public bool enableTeleport = true;
        public float teleportCooldown = 15f;
        public float teleportRange = 20f;
        public KeyCode teleportKey = KeyCode.Q;
    }

    [System.Serializable]
    public class AbilityState
    {
        public float gravityWellCooldown = 0f;
        public float boostCooldown = 0f;
        public float teleportCooldown = 0f;
        public int currentWells = 0;
        public bool isBoosting = false;
        public bool canUseAbilities = true;
    }

    public AbilitySettings settings = new AbilitySettings();
    public AbilityState state = new AbilityState();

    [Header("References")]
    public GameObject gravityWellPrefab;
    public Transform abilitySpawnPoint;
    public TMP_Text cooldownText;
    public UnityEngine.UI.Image cooldownFill;
    public ParticleSystem boostParticles;

    private AdvancedNetworkPlayerController _playerController;
    private Rigidbody _rb;

    public event System.Action OnAbilityUsed;
    public event System.Action OnBoostStart;
    public event System.Action OnBoostEnd;
    
#region Cooldown Status System

[System.Serializable]
public class CooldownStatus
{
    public float gravityWellCooldown;
    public float boostCooldown;
    public float teleportCooldown;
    public bool gravityWellReady;
    public bool boostReady;
    public bool teleportReady;
    public float longestCooldown;
    public float totalCooldownPercentage;
}

public CooldownStatus GetCooldownStatus()
{
    return new CooldownStatus
    {
        gravityWellCooldown = state.gravityWellCooldown,
        boostCooldown = state.boostCooldown,
        teleportCooldown = state.teleportCooldown,
        gravityWellReady = state.gravityWellCooldown <= 0,
        boostReady = state.boostCooldown <= 0,
        teleportReady = state.teleportCooldown <= 0,
        longestCooldown = GetLongestCooldown(),
        totalCooldownPercentage = GetTotalCooldownPercentage()
    };
}

public float GetCooldownProgress(string abilityName)
{
    return abilityName.ToLower() switch
    {
        "gravitywell" or "gravity" or "well" => GetCooldownProgress(state.gravityWellCooldown, settings.gravityWellCooldown),
        "boost" => GetCooldownProgress(state.boostCooldown, settings.boostCooldown),
        "teleport" => GetCooldownProgress(state.teleportCooldown, settings.teleportCooldown),
        _ => 0f
    };
}

public bool IsAbilityReady(string abilityName)
{
    return abilityName.ToLower() switch
    {
        "gravitywell" or "gravity" or "well" => state.gravityWellCooldown <= 0,
        "boost" => state.boostCooldown <= 0,
        "teleport" => state.teleportCooldown <= 0,
        _ => false
    };
}

public float GetCooldownTime(string abilityName)
{
    return abilityName.ToLower() switch
    {
        "gravitywell" or "gravity" or "well" => state.gravityWellCooldown,
        "boost" => state.boostCooldown,
        "teleport" => state.teleportCooldown,
        _ => 0f
    };
}

public string GetCooldownText(string abilityName)
{
    float cooldown = GetCooldownTime(abilityName);
    return cooldown > 0 ? $"{cooldown:0.0}s" : "Ready";
}

private float GetCooldownProgress(float currentCooldown, float maxCooldown)
{
    if (maxCooldown <= 0) return 1f;
    return Mathf.Clamp01(1f - (currentCooldown / maxCooldown));
}

private float GetLongestCooldown()
{
    return Mathf.Max(state.gravityWellCooldown, state.boostCooldown, state.teleportCooldown);
}

private float GetTotalCooldownPercentage()
{
    float totalMax = settings.gravityWellCooldown + settings.boostCooldown + settings.teleportCooldown;
    float totalCurrent = state.gravityWellCooldown + state.boostCooldown + state.teleportCooldown;
    
    if (totalMax <= 0) return 1f;
    return Mathf.Clamp01(1f - (totalCurrent / totalMax));
}

[Server]
public void ReduceCooldowns(float reductionPercent, float duration = 0f)
{
    float multiplier = 1f - Mathf.Clamp01(reductionPercent);
    
    state.gravityWellCooldown *= multiplier;
    state.boostCooldown *= multiplier;
    state.teleportCooldown *= multiplier;
    
    UpdateCooldownsRpc(state.gravityWellCooldown, state.boostCooldown, state.teleportCooldown);
    
    if (duration > 0f)
    {
        StartCoroutine(ResetCooldownReductionAfterDelay(reductionPercent, duration));
    }
}

[ObserversRpc]
private void UpdateCooldownsRpc(float gravityCooldown, float boostCooldown, float teleportCooldown)
{
    state.gravityWellCooldown = gravityCooldown;
    state.boostCooldown = boostCooldown;
    state.teleportCooldown = teleportCooldown;
}

private IEnumerator ResetCooldownReductionAfterDelay(float reductionPercent, float duration)
{
    yield return new WaitForSeconds(duration);
    
    // Reverse the cooldown reduction
    float multiplier = 1f / (1f - Mathf.Clamp01(reductionPercent));
    
    state.gravityWellCooldown *= multiplier;
    state.boostCooldown *= multiplier;
    state.teleportCooldown *= multiplier;
    
    UpdateCooldownsRpc(state.gravityWellCooldown, state.boostCooldown, state.teleportCooldown);
}

[Server]
public void ResetAllCooldowns()
{
    state.gravityWellCooldown = 0f;
    state.boostCooldown = 0f;
    state.teleportCooldown = 0f;
    
    UpdateCooldownsRpc(0f, 0f, 0f);
}

    [Server]
    public void SetCooldown(string abilityName, float cooldownTime)
    {
        switch (abilityName.ToLower())
        {
            case "gravitywell" or "gravity" or "well":
                state.gravityWellCooldown = Mathf.Max(0f, cooldownTime);
                break;
            case "boost":
                state.boostCooldown = Mathf.Max(0f, cooldownTime);
                break;
            case "teleport":
                state.teleportCooldown = Mathf.Max(0f, cooldownTime);
                break;
        }

        UpdateCooldownsRpc(state.gravityWellCooldown, state.boostCooldown, state.teleportCooldown);
    }

/*
// Get complete cooldown status
CooldownStatus status = abilityManager.GetCooldownStatus();
Debug.Log($"Gravity Well ready: {status.gravityWellReady}");
Debug.Log($"Longest cooldown: {status.longestCooldown}");

// Check specific ability
bool isBoostReady = abilityManager.IsAbilityReady("boost");
float teleportProgress = abilityManager.GetCooldownProgress("teleport");

// Get cooldown text for UI
string cooldownText = abilityManager.GetCooldownText("gravitywell");

// Reduce all cooldowns by 50% for 10 seconds
abilityManager.ReduceCooldowns(0.5f, 10f);

// Reset all cooldowns
abilityManager.ResetAllCooldowns();

// Set specific cooldown
abilityManager.SetCooldown("boost", 3.5f);
*/

    #endregion

    #region Initialization
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _playerController = GetComponent<AdvancedNetworkPlayerController>();
        _rb = GetComponent<Rigidbody>();

        if (base.Owner.IsLocalClient)
        {
            SetupAbilityUI();
        }
    }

    private void SetupAbilityUI()
    {
        if (cooldownText != null) cooldownText.text = "Ready";
        if (cooldownFill != null) cooldownFill.fillAmount = 0f;
    }
    #endregion

    #region Update Loop
    private void Update()
    {
        if (!IsOwner || !state.canUseAbilities) return;

        UpdateCooldowns();
        HandleInput();
        UpdateAbilityUI();
    }

    private void UpdateCooldowns()
    {
        if (state.gravityWellCooldown > 0) state.gravityWellCooldown -= Time.deltaTime;
        if (state.boostCooldown > 0) state.boostCooldown -= Time.deltaTime;
        if (state.teleportCooldown > 0) state.teleportCooldown -= Time.deltaTime;
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(settings.abilityKey))
        {
            TryPlaceGravityWell();
        }

        if (Input.GetKey(settings.boostKey) && CanUseBoost())
        {
            StartBoost();
        }
        else if (state.isBoosting)
        {
            EndBoost();
        }

        if (Input.GetKeyDown(settings.teleportKey) && CanUseTeleport())
        {
            TryTeleport();
        }
    }
    #endregion

    #region Gravity Well Ability
    private bool CanPlaceGravityWell()
    {
        return state.gravityWellCooldown <= 0 && state.currentWells < settings.maxWells;
    }

    private void TryPlaceGravityWell()
    {
        if (!CanPlaceGravityWell()) return;

        Vector3 spawnPosition = CalculateSpawnPosition();
        if (spawnPosition != Vector3.zero)
        {
            PlaceGravityWellServer(spawnPosition);
        }
    }

    [ServerRpc]
    private void PlaceGravityWellServer(Vector3 spawnPosition)
    {
        if (!CanPlaceGravityWell()) return;

        GameObject wellGO = Instantiate(gravityWellPrefab, spawnPosition, Quaternion.identity);
        AdvancedGravityWell well = wellGO.GetComponent<AdvancedGravityWell>();
        
        well.InitializeWell(_playerController.Owner.ClientId, _playerController.Owner);
        well.SetTeam(_playerController.TeamId.Value);
        
        base.Spawn(wellGO);
        
        state.gravityWellCooldown = settings.gravityWellCooldown;
        state.currentWells++;
        
        OnAbilityUsed?.Invoke();
        UpdateAbilityStateRpc(state.gravityWellCooldown, state.currentWells);

        StartCoroutine(WellCountdownCoroutine(well));
    }

    [ObserversRpc]
    private void UpdateAbilityStateRpc(float cooldown, int wellCount)
    {
        state.gravityWellCooldown = cooldown;
        state.currentWells = wellCount;
    }

    private IEnumerator WellCountdownCoroutine(AdvancedGravityWell well)
    {
        yield return new WaitForSeconds(settings.wellLifetime);
        
        if (well != null)
        {
            well.Despawn();
        }
        
        state.currentWells--;
    }

    private Vector3 CalculateSpawnPosition()
    {
        Ray ray = new Ray(transform.position + Vector3.up, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, settings.wellPlacementRange))
        {
            return hit.point;
        }
        return transform.position + transform.forward * settings.wellPlacementRange;
    }
    #endregion

    #region Boost Ability
    private bool CanUseBoost()
    {
        return state.boostCooldown <= 0 && !state.isBoosting;
    }

    private void StartBoost()
    {
        if (!CanUseBoost()) return;

        state.isBoosting = true;
        _playerController.ApplyBoost(settings.boostMultiplier,2);
        
        OnBoostStart?.Invoke();
        StartBoostServer();
    }

    [ServerRpc]
    private void StartBoostServer()
    {
        state.isBoosting = true;
        StartBoostRpc();
    }

    [ObserversRpc]
    private void StartBoostRpc()
    {
        if (boostParticles != null) boostParticles.Play();
        OnBoostStart?.Invoke();
    }

    private void EndBoost()
    {
        state.isBoosting = false;
        state.boostCooldown = settings.boostCooldown;
        _playerController.EndBoost();
        
        OnBoostEnd?.Invoke();
        EndBoostServer();
    }

    [ServerRpc]
    private void EndBoostServer()
    {
        state.isBoosting = false;
        state.boostCooldown = settings.boostCooldown;
        EndBoostRpc();
    }

    [ObserversRpc]
    private void EndBoostRpc()
    {
        if (boostParticles != null) boostParticles.Stop();
        OnBoostEnd?.Invoke();
    }
    #endregion

    #region Teleport Ability
    private bool CanUseTeleport()
    {
        return settings.enableTeleport && state.teleportCooldown <= 0;
    }

    private void TryTeleport()
    {
        if (!CanUseTeleport()) return;

        Vector3 targetPosition = CalculateTeleportPosition();
        if (targetPosition != Vector3.zero)
        {
            TeleportServer(targetPosition);
        }
    }

    [ServerRpc]
    private void TeleportServer(Vector3 targetPosition)
    {
        if (!CanUseTeleport()) return;

        transform.position = targetPosition;
        state.teleportCooldown = settings.teleportCooldown;
        
        OnAbilityUsed?.Invoke();
        TeleportRpc(targetPosition, state.teleportCooldown);
    }

    [ObserversRpc]
    private void TeleportRpc(Vector3 position, float cooldown)
    {
        transform.position = position;
        state.teleportCooldown = cooldown;
    }

    private Vector3 CalculateTeleportPosition()
    {
        Ray ray = new Ray(transform.position + Vector3.up, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, settings.teleportRange))
        {
            return hit.point + hit.normal * 2f;
        }
        return transform.position + transform.forward * settings.teleportRange;
    }
    #endregion

    #region UI Management
    private void UpdateAbilityUI()
    {
        if (cooldownText == null || cooldownFill == null) return;

        float cooldown = Mathf.Max(state.gravityWellCooldown, state.boostCooldown, state.teleportCooldown);
        float maxCooldown = Mathf.Max(settings.gravityWellCooldown, settings.boostCooldown, settings.teleportCooldown);

        if (cooldown > 0)
        {
            cooldownText.text = $"{cooldown:0.0}s";
            cooldownFill.fillAmount = cooldown / maxCooldown;
        }
        else
        {
            cooldownText.text = "Ready";
            cooldownFill.fillAmount = 0f;
        }
    }
    #endregion

    #region Public API
    public void ResetAbilities()
    {
        state.gravityWellCooldown = 0f;
        state.boostCooldown = 0f;
        state.teleportCooldown = 0f;
        state.currentWells = 0;
        state.isBoosting = false;
        state.canUseAbilities = true;
    }

    public void SetAbilitiesEnabled(bool enabled)
    {
        state.canUseAbilities = enabled;
        if (!enabled && state.isBoosting)
        {
            EndBoost();
        }
    }

    public bool IsBoosting => state.isBoosting;
    public bool CanUseAbilities => state.canUseAbilities;
    #endregion

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}