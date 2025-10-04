using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ThrusterSparks : MonoBehaviour
{
    public ParticleSystem ps;
    ParticleSystem.EmissionModule em;

    [Range(0f,1f)] public float throttle = 0f; // 0..1
    public float maxRate = 200f;
    public bool engineOn = false;

    void Awake()
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();
        em = ps.emission;
    }

    void Update()
    {
        float rate = engineOn ? Mathf.Lerp(5f, maxRate, throttle) : 0f;
        em.rateOverTime = rate;

        var main = ps.main;
        Color baseTint = new Color(0f, 1.0f, 0.9f);
        main.startColor = baseTint * (0.5f + throttle * 1.5f);
    }

    public void Burst(int count = 30)
    {
        ps.Emit(count);
    }
}