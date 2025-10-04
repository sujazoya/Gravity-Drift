using UnityEngine;

public class VFXSpawner : MonoBehaviour
{
    public ParticleSystem starfield;
    public ParticleSystem dust;
    public float maxRateOverTime = 120f;

    public void SetProgress(float p)
    {
        if (starfield != null)
        {
            var em = starfield.emission;
            em.rateOverTime = Mathf.Lerp(10f, maxRateOverTime, p);
        }

        if (dust != null)
        {
            var em = dust.emission;
            em.rateOverTime = Mathf.Lerp(0f, 60f, p);
        }
    }
}
