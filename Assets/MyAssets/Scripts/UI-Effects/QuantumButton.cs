using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Coffee.UIExtensions; // Namespace for UIParticle

public class QuantumButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
{
    [Header("Animation")]
    public float hoverScale = 1.1f;
    public float clickScale = 0.95f;
    public float animDuration = 0.2f;

    [Header("UIParticle References")]
    public UIParticle absorbUIParticle; // Reference to the UIParticle component, not the ParticleSystem
    public UIParticle burstUIParticle;

    private Vector3 originalScale;
    private RectTransform rectTransform;
    private ParticleSystem absorbParticleSystem;
    private ParticleSystem burstParticleSystem;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;

        // Get the ParticleSystem components from the UIParticle objects
        if (absorbUIParticle != null)
            absorbParticleSystem = absorbUIParticle.GetComponent<ParticleSystem>();
        if (burstUIParticle != null)
            burstParticleSystem = burstUIParticle.GetComponent<ParticleSystem>();

        // Ensure UIParticles and their systems are stopped and hidden on start
        StopAndHideParticle(absorbUIParticle, absorbParticleSystem);
        StopAndHideParticle(burstUIParticle, burstParticleSystem);
    }

    // Helper method to properly stop and hide a UIParticle effect
    private void StopAndHideParticle(UIParticle uiParticle, ParticleSystem ps)
    {
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (uiParticle != null)
        {
            uiParticle.enabled = false; // This is key - disables the rendering
        }
    }

    // Helper method to play a UIParticle effect
    private void PlayParticle(UIParticle uiParticle, ParticleSystem ps)
    {
        if (uiParticle != null && ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            uiParticle.enabled = true; // Enable the UIParticle renderer
            ps.Play(); // Play the particle system
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        LeanTween.cancel(rectTransform);
        LeanTween.scale(rectTransform, originalScale * hoverScale, animDuration).setEase(LeanTweenType.easeOutBack);

        // FIX: Use the new method to play the absorb effect
        PlayParticle(absorbUIParticle, absorbParticleSystem);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LeanTween.cancel(rectTransform);
        LeanTween.scale(rectTransform, originalScale * clickScale, animDuration * 0.5f).setEase(LeanTweenType.easeInOutCubic)
            .setOnComplete(() => {
                LeanTween.scale(rectTransform, originalScale, animDuration * 0.5f).setEase(LeanTweenType.easeOutBack);
            });

        // FIX: Play the burst effect
        PlayParticle(burstUIParticle, burstParticleSystem);

        // Stop the absorption effect on click
        StopAndHideParticle(absorbUIParticle, absorbParticleSystem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        LeanTween.cancel(rectTransform);
        LeanTween.scale(rectTransform, originalScale, animDuration).setEase(LeanTweenType.easeOutCubic);

        // FIX: Stop the absorb effect
        StopAndHideParticle(absorbUIParticle, absorbParticleSystem);
    }
}