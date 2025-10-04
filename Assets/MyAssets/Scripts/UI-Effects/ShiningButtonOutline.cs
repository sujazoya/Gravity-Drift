using UnityEngine;
using UnityEngine.EventSystems;

public class ShiningButtonOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Particle Reference")]
    public ParticleSystem outlineParticleSystem; // Now just need the ParticleSystem!

    [Header("Intensity Settings")]
    public float normalEmissionRate = 10f;
    public float hoverEmissionRate = 30f;

    private ParticleSystem.EmissionModule emissionModule;

    void Start()
    {
        // Get the emission module
        if (outlineParticleSystem != null)
        {
            emissionModule = outlineParticleSystem.emission;
            emissionModule.rateOverTime = normalEmissionRate;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (emissionModule.rateOverTime.constant != hoverEmissionRate)
            emissionModule.rateOverTime = hoverEmissionRate;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (emissionModule.rateOverTime.constant != normalEmissionRate)
            emissionModule.rateOverTime = normalEmissionRate;
    }
}