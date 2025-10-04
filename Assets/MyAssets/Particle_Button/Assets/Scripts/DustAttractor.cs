using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class DustAttractor : MonoBehaviour {
    public RectTransform targetButton;
    public float attractSpeed = 3.0f;
    public float settleDistance = 0.05f;
    ParticleSystem ps;
    ParticleSystem.Particle[] particles;

    void Start() {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void LateUpdate() {
        int count = ps.GetParticles(particles);
        for (int i = 0; i < count; i++) {
            if (targetButton == null) continue;
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, targetButton.position);
            Vector2 size = targetButton.rect.size * targetButton.lossyScale;
            Vector3 randomTarget = screenPos + new Vector3(Random.Range(-size.x/2, size.x/2), Random.Range(-size.y/2, size.y/2), 0);
            Vector3 worldTarget = Camera.main.ScreenToWorldPoint(new Vector3(randomTarget.x, randomTarget.y, Camera.main.nearClipPlane + 1f));
            Vector3 dir = (worldTarget - particles[i].position);
            float dist = dir.magnitude;
            if (dist > settleDistance)
                particles[i].position += dir.normalized * attractSpeed * Time.deltaTime;
            else
                particles[i].remainingLifetime = 0;
        }
        ps.SetParticles(particles, count);
    }
}
