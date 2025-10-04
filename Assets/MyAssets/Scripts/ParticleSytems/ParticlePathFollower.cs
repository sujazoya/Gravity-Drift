using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class ParticlePathFollower : MonoBehaviour
{
#if UNITY_EDITOR
    public Vector3 GetPathPositionEditor(float t) => GetPathPosition(t);
#endif

    public enum PathType { Linear, CatmullRom, Bezier }
    public enum TravelMode { Lifetime, FixedTime, Randomized }
    public enum ShapeMode { None, Circle, Spiral, Curve }

    [Header("Path Settings")]
    public PathType pathType = PathType.CatmullRom;
    public List<Transform> pathPoints = new List<Transform>();
    public bool loop = false;

    [Header("Movement Settings")]
    public TravelMode travelMode = TravelMode.Lifetime;
    public float fixedTravelTime = 3f;
    public float travelSpeed = 1f;
    public bool alignToPath = true;
    public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

    [Header("Velocity Settings")]
    public AnimationCurve velocityOverPath = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);
    public float velocityMultiplier = 5f;

    [Header("Distribution Settings")]
    public bool distributeAlongPath = false;
    [Range(0f, 1f)] public float distributionSpread = 1f;
    [Range(0f, 1f)] public float randomPhase = 0.1f;

    [Header("Shape Adjustment")]
    public ShapeMode shapeMode = ShapeMode.None;
    public float shapeRadius = 0.5f;
    public float spiralTurns = 2f;
    public AnimationCurve curveY = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Spread Settings")]
    public float spreadAngle = 10f; // degrees
    public float spreadRadius = 1f;

    [Header("Noise & Turbulence")]
    public bool useNoise = true;
    public float noiseAmplitude = 0.5f;
    public float noiseFrequency = 1f;
    public float noiseSpeed = 1f;

    [Header("Visual Settings")]
    public Gradient colorOverLifetime = new Gradient();
    public AnimationCurve sizeOverLifetime = AnimationCurve.EaseInOut(0, 0.1f, 1, 0f);

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    void Awake() => ps = GetComponent<ParticleSystem>();

    void LateUpdate()
    {
        if (ps == null || pathPoints.Count < 2) return;

        if (particles == null || particles.Length < ps.main.maxParticles)
            particles = new ParticleSystem.Particle[ps.main.maxParticles];

        int count = ps.GetParticles(particles);

        for (int i = 0; i < count; i++)
        {
            float t = GetProgress(particles[i], i, count);
            Vector3 basePos = GetPathPosition(t);

            // Shape offset
            basePos += GetShapeOffset(t, i);

            // Spread cone
            if (spreadAngle > 0f || spreadRadius > 0f)
            {
                Vector3 randomDir = Random.insideUnitSphere;
                Quaternion spreadRot = Quaternion.AngleAxis(Random.Range(-spreadAngle, spreadAngle), Vector3.up);
                basePos += spreadRot * randomDir * spreadRadius * t;
            }

            // Add turbulence
            if (useNoise)
                basePos += GetTurbulence(particles[i].position, Time.time);

            // Velocity along tangent
            float vel = velocityOverPath.Evaluate(t) * velocityMultiplier;
            basePos += GetPathTangent(t) * vel * Time.deltaTime;

            particles[i].position = basePos;

            // Align particles to path
            if (alignToPath)
            {
                Vector3 forward = GetPathTangent(t);
                particles[i].rotation3D = Quaternion.LookRotation(forward).eulerAngles;
            }

            // Visuals
            particles[i].startColor = colorOverLifetime.Evaluate(t);
            particles[i].startSize = sizeOverLifetime.Evaluate(t);
        }

        ps.SetParticles(particles, count);
    }

    private float GetProgress(ParticleSystem.Particle p, int index, int total)
    {
        float baseT = 0f;

        switch (travelMode)
        {
            case TravelMode.Lifetime:
                baseT = 1f - (p.remainingLifetime / p.startLifetime);
                break;

            case TravelMode.FixedTime:
                float age = (p.startLifetime - p.remainingLifetime);
                baseT = (age / fixedTravelTime) * travelSpeed;
                break;

            case TravelMode.Randomized:
                float seedOffset = (p.randomSeed % 1000) / 1000f;
                baseT = ((Time.time + seedOffset) / fixedTravelTime) % 1f;
                break;
        }

        if (distributeAlongPath)
            baseT += (index / (float)total) * distributionSpread;

        if (randomPhase > 0f)
            baseT += Random.Range(-randomPhase, randomPhase);

        baseT = Mathf.Repeat(baseT, 1f);
        return Mathf.Clamp01(speedCurve.Evaluate(baseT));
    }

    private Vector3 GetShapeOffset(float t, int index)
    {
        switch (shapeMode)
        {
            case ShapeMode.Circle:
                float angle = (index / 20f) * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * shapeRadius;

            case ShapeMode.Spiral:
                float spiralT = t * spiralTurns * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(spiralT), Mathf.Sin(spiralT), 0) * shapeRadius * t;

            case ShapeMode.Curve:
                float y = curveY.Evaluate(t) * shapeRadius;
                return new Vector3(0, y, 0);

            default: return Vector3.zero;
        }
    }

    private Vector3 GetTurbulence(Vector3 pos, float time)
    {
        float n1 = Mathf.PerlinNoise(pos.x * noiseFrequency, time * noiseSpeed);
        float n2 = Mathf.PerlinNoise(pos.y * noiseFrequency * 0.5f, time * noiseSpeed * 1.5f);
        float n3 = Mathf.PerlinNoise(pos.z * noiseFrequency * 2f, time * noiseSpeed * 0.7f);

        return new Vector3(n1 - 0.5f, n2 - 0.5f, n3 - 0.5f) * noiseAmplitude * 2f;
    }

    private Vector3 GetPathTangent(float t)
    {
        float delta = 0.001f;
        Vector3 p1 = GetPathPosition(t);
        Vector3 p2 = GetPathPosition(Mathf.Min(1f, t + delta));
        return (p2 - p1).normalized;
    }

    private Vector3 GetPathPosition(float t)
    {
        switch (pathType)
        {
            case PathType.Linear: return GetLinear(t);
            case PathType.CatmullRom: return GetCatmullRom(t);
            case PathType.Bezier: return GetBezier(t);
            default: return Vector3.zero;
        }
    }

    private Vector3 GetLinear(float t)
    {
        int last = pathPoints.Count - 1;
        float scaled = t * last;
        int i = Mathf.FloorToInt(scaled);
        float u = scaled - i;
        if (loop) i %= pathPoints.Count; else i = Mathf.Clamp(i, 0, last - 1);
        int next = (i + 1) % pathPoints.Count;
        return Vector3.Lerp(pathPoints[i].position, pathPoints[next].position, u);
    }

    private Vector3 GetCatmullRom(float t)
    {
        int numSections = loop ? pathPoints.Count : pathPoints.Count - 3;
        float scaled = t * numSections;
        int currPt = Mathf.FloorToInt(scaled);
        float u = scaled - currPt;

        int p0 = (currPt - 1 + pathPoints.Count) % pathPoints.Count;
        int p1 = (currPt + 0) % pathPoints.Count;
        int p2 = (currPt + 1) % pathPoints.Count;
        int p3 = (currPt + 2) % pathPoints.Count;

        return 0.5f * (
            (2f * pathPoints[p1].position) +
            (-pathPoints[p0].position + pathPoints[p2].position) * u +
            (2f * pathPoints[p0].position - 5f * pathPoints[p1].position + 4f * pathPoints[p2].position - pathPoints[p3].position) * u * u +
            (-pathPoints[p0].position + 3f * pathPoints[p1].position - 3f * pathPoints[p2].position + pathPoints[p3].position) * u * u * u
        );
    }

    private Vector3 GetBezier(float t)
    {
        if (pathPoints.Count < 3) return GetLinear(t);
        Vector3 p0 = pathPoints[0].position;
        Vector3 p1 = pathPoints[1].position;
        Vector3 p2 = pathPoints[2].position;
        return Mathf.Pow(1 - t, 2) * p0 + 2 * (1 - t) * t * p1 + Mathf.Pow(t, 2) * p2;
    }
}
