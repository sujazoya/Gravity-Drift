using UnityEngine;
using Coffee.UIExtensions;

[RequireComponent(typeof(UIParticle))] // Forces this script to require a UIParticle
public class UIParticleController : MonoBehaviour
{
    private UIParticle uiParticle;
    private new ParticleSystem particleSystem;
    [SerializeField] bool haveLoop;

    void Awake()
    {
        // Get the required components
        uiParticle = GetComponent<UIParticle>();
        particleSystem = GetComponent<ParticleSystem>();

        // Immediately enable the UIParticle renderer. THIS IS THE KEY.
        uiParticle.enabled = true;

        // Ensure the ParticleSystem is set to play and loop
        if (particleSystem != null)
        {
            var main = particleSystem.main;

            if (haveLoop)
            {
                main.loop = true; // Ensure looping is on
            }

            // Stop and clear any existing state, then play
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play();
        }
    }

    // Public method to safely refresh the system if it gets stuck
    public void Refresh()
    {
        if (uiParticle != null)
        {
            uiParticle.enabled = true; // Force the renderer on
        }
        if (particleSystem != null)
        {
            particleSystem.Play(); // Restart the particle system
        }
    }

    void OnEnable()
    {
        // Every time the object is enabled, ensure the renderer is on
        if (uiParticle != null)
        {
            uiParticle.enabled = true;
        }
    }
}