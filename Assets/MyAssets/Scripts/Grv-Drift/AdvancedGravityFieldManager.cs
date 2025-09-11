using FishNet.Object;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;




public class AdvancedGravityFieldManager : NetworkBehaviour
{
    public static AdvancedGravityFieldManager Instance { get; private set; }
    

    public enum GravityTransitionCurveType
{
    Linear,
    EaseInOut
}

[System.Serializable]
public class GlobalGravitySettings
{
    [Header("Global Gravity")]
    public Vector3 globalGravity = Vector3.zero;
    public float globalGravityStrength = 0f;
    public bool useSphericalGravity = true;
    public Transform gravityCenter;
    public float sphericalGravityRadius = 50f;

    [Header("Gravity Transitions")]
    public float gravityChangeSpeed = 2f;
    public GravityTransitionCurveType gravityTransitionCurveType = GravityTransitionCurveType.EaseInOut;

    [Header("Zero-G Effects")]
    public float zeroGDrag = 0.1f;
    public float zeroGAngularDrag = 0.05f;
    public float normalDrag = 0.5f;
    public float normalAngularDrag = 0.2f;

    // Helper to get the actual AnimationCurve locally
    public AnimationCurve GetGravityTransitionCurve()
    {
        switch (gravityTransitionCurveType)
        {
            case GravityTransitionCurveType.Linear:
                return AnimationCurve.Linear(0, 0, 1, 1);
            case GravityTransitionCurveType.EaseInOut:
            default:
                return AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
    }
}

    [System.Serializable]
    public class GravityState
    {
        public Vector3 currentGravity;
        public float currentStrength;
        public bool isTransitioning;
        public List<GravityField> activeFields = new List<GravityField>();
    }


    public GlobalGravitySettings settings = new GlobalGravitySettings();
    public GravityState state = new GravityState();

    [Header("References")]
    public List<Rigidbody> affectedBodies = new List<Rigidbody>();
    public ParticleSystem gravityFieldParticles;
    public AudioSource gravityFieldAudio;

    private Vector3 _targetGravity;
    private float _targetStrength;
    private Coroutine _gravityTransitionCoroutine;

    public event System.Action OnGravityChanged;
    public event System.Action OnGravityTransitionStart;
    public event System.Action OnGravityTransitionEnd;

    #region Initialization
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (Instance == null)
            Instance = this;

        InitializeGravity();
    }

    private void InitializeGravity()
    {
        state.currentGravity = settings.globalGravity;
        state.currentStrength = settings.globalGravityStrength;
        _targetGravity = settings.globalGravity;
        _targetStrength = settings.globalGravityStrength;

        Physics.gravity = state.currentGravity * state.currentStrength;
        UpdateDragValues();
    }

    private void FixedUpdate()
    {
        if (IsServerInitialized)
        {
            ApplyGlobalGravity();
            ApplyGravityFields();
        }
    }
    #endregion

    #region Global Gravity Control
    [Server]
    public void SetGlobalGravity(Vector3 direction, float strength, bool instant = false)
    {
        _targetGravity = direction.normalized;
        _targetStrength = strength;

        if (instant)
        {
            state.currentGravity = _targetGravity;
            state.currentStrength = _targetStrength;
            Physics.gravity = state.currentGravity * state.currentStrength;
            UpdateDragValues();
        }
        else
        {
            StartGravityTransition();
        }
    }

    [Server]
    public void SetZeroGravity(bool instant = false)
    {
        SetGlobalGravity(Vector3.down, 0f, instant);
    }

    [Server]
    public void SetNormalGravity(bool instant = false)
    {
        SetGlobalGravity(Vector3.down, 9.81f, instant);
    }

    [Server]
    public void SetSphericalGravity(bool enabled)
    {
        settings.useSphericalGravity = enabled;
        UpdateGravityTypeRpc(enabled);
    }

    [Server]
    private void StartGravityTransition()
    {
        if (_gravityTransitionCoroutine != null)
        {
            StopCoroutine(_gravityTransitionCoroutine);
        }

        state.isTransitioning = true;
        OnGravityTransitionStart?.Invoke();
        _gravityTransitionCoroutine = StartCoroutine(GravityTransitionCoroutine());
    }

    [Server]
    private IEnumerator GravityTransitionCoroutine()
    {
        float duration = settings.gravityChangeSpeed;
        float elapsed = 0f;
        Vector3 startGravity = state.currentGravity;
        float startStrength = state.currentStrength;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = settings.GetGravityTransitionCurve().Evaluate(elapsed / duration);
            state.currentGravity = Vector3.Slerp(startGravity, _targetGravity, t);
            state.currentStrength = Mathf.Lerp(startStrength, _targetStrength, t);

            Physics.gravity = state.currentGravity * state.currentStrength;
            UpdateDragValues();
            UpdateGravityEffects();

            yield return null;
        }

        state.currentGravity = _targetGravity;
        state.currentStrength = _targetStrength;
        state.isTransitioning = false;

        Physics.gravity = state.currentGravity * state.currentStrength;
        UpdateDragValues();
        UpdateGravityEffects();

        OnGravityChanged?.Invoke();
        OnGravityTransitionEnd?.Invoke();
    }

    [Server]
    private void ApplyGlobalGravity()
    {
        if (settings.useSphericalGravity && settings.gravityCenter != null)
        {
            ApplySphericalGravity();
        }
        else
        {
            ApplyUniformGravity();
        }
    }

    [Server]
    private void ApplyUniformGravity()
    {
        foreach (Rigidbody rb in affectedBodies)
        {
            if (rb != null)
            {
                rb.AddForce(Physics.gravity * rb.mass, ForceMode.Force);
            }
        }
    }

    [Server]
    private void ApplySphericalGravity()
    {
        foreach (Rigidbody rb in affectedBodies)
        {
            if (rb != null)
            {
                Vector3 direction = (settings.gravityCenter.position - rb.position).normalized;
                float distance = Vector3.Distance(rb.position, settings.gravityCenter.position);
                float strength = Mathf.Clamp01(1f - (distance / settings.sphericalGravityRadius)) * state.currentStrength;

                rb.AddForce(direction * strength * rb.mass, ForceMode.Force);
            }
        }
    }
    #endregion

    #region Gravity Fields Management
    [Server]
    public void RegisterGravityField(GravityField field)
    {
        if (!state.activeFields.Contains(field))
        {
            state.activeFields.Add(field);
        }
    }

    [Server]
    public void UnregisterGravityField(GravityField field)
    {
        state.activeFields.Remove(field);
    }

    [Server]
    private void ApplyGravityFields()
    {
        foreach (Rigidbody rb in affectedBodies)
        {
            if (rb != null)
            {
                Vector3 totalFieldForce = Vector3.zero;

                foreach (GravityField field in state.activeFields)
                {
                    if (field != null && field.IsActive)
                    {
                        totalFieldForce += field.GetForceAtPosition(rb.position, rb.mass);
                    }
                }

                rb.AddForce(totalFieldForce, ForceMode.Force);
            }
        }
    }

    [Server]
    public Vector3 GetTotalGravityForceAtPosition(Vector3 position, float mass = 1f)
    {
        Vector3 totalForce = Vector3.zero;

        // Global gravity
        if (settings.useSphericalGravity && settings.gravityCenter != null)
        {
            Vector3 direction = (settings.gravityCenter.position - position).normalized;
            float distance = Vector3.Distance(position, settings.gravityCenter.position);
            float strength = Mathf.Clamp01(1f - (distance / settings.sphericalGravityRadius)) * state.currentStrength;
            totalForce += direction * strength * mass;
        }
        else
        {
            totalForce += Physics.gravity * mass;
        }

        // Gravity fields
        foreach (GravityField field in state.activeFields)
        {
            if (field != null && field.IsActive)
            {
                totalForce += field.GetForceAtPosition(position, mass);
            }
        }

        return totalForce;
    }
    #endregion

    #region Effects & Feedback
    private void UpdateDragValues()
    {
        float drag = state.currentStrength > 0.1f ? settings.normalDrag : settings.zeroGDrag;
        float angularDrag = state.currentStrength > 0.1f ? settings.normalAngularDrag : settings.zeroGAngularDrag;

        foreach (Rigidbody rb in affectedBodies)
        {
            if (rb != null)
            {
                rb.linearDamping = drag;
                rb.angularDamping = angularDrag;
            }
        }
    }

    private void UpdateGravityEffects()
    {
        if (gravityFieldParticles != null)
        {
            var emission = gravityFieldParticles.emission;
            emission.rateOverTime = state.currentStrength * 10f;
        }

        if (gravityFieldAudio != null)
        {
            gravityFieldAudio.volume = state.currentStrength * 0.1f;
            gravityFieldAudio.pitch = 0.5f + (state.currentStrength * 0.05f);
        }
    }
    #endregion

    #region RPC Methods
    [ObserversRpc]
    private void UpdateGravityTypeRpc(bool useSpherical)
    {
        settings.useSphericalGravity = useSpherical;
    }

    [ObserversRpc]
    private void UpdateGravityValuesRpc(Vector3 gravity, float strength)
    {
        state.currentGravity = gravity;
        state.currentStrength = strength;
        Physics.gravity = state.currentGravity * state.currentStrength;
        UpdateDragValues();
        UpdateGravityEffects();
    }
    #endregion

    #region Public API
    [Server]
    public void AddAffectedBody(Rigidbody rb)
    {
        if (!affectedBodies.Contains(rb))
        {
            affectedBodies.Add(rb);
            UpdateDragValues();
        }
    }

    [Server]
    public void RemoveAffectedBody(Rigidbody rb)
    {
        affectedBodies.Remove(rb);
    }

    public float GetCurrentGravityStrength() => state.currentStrength;
    public Vector3 GetCurrentGravityDirection() => state.currentGravity;
    public bool IsZeroG() => state.currentStrength < 0.1f;
    public bool IsTransitioning() => state.isTransitioning;

    public List<GravityField> GetActiveFields() => state.activeFields;
    public int GetActiveFieldCount() => state.activeFields.Count;
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        if (settings.useSphericalGravity && settings.gravityCenter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(settings.gravityCenter.position, settings.sphericalGravityRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(settings.gravityCenter.position, settings.gravityCenter.position + Vector3.up * 5f);
        }

        // Draw gravity field influences
        Gizmos.color = Color.magenta;
        foreach (var field in state.activeFields)
        {
            if (field != null)
            {
                Gizmos.DrawWireSphere(field.transform.position, field.GetInfluenceRadius());
            }
        }
    }
    #endregion
}

// Supporting class for gravity fields
public class GravityField : NetworkBehaviour
{
    public virtual bool IsActive => true;
    public virtual Vector3 GetForceAtPosition(Vector3 position, float mass) => Vector3.zero;
    public virtual float GetInfluenceRadius() => 0f;
}