using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Renderer))]
public class PressAnyKeyHologram : MonoBehaviour
{
    public Renderer targetRenderer; // assign the quad renderer
    public string flickerParam = "_NoiseStrength";
    public string glowParam = "_Glow";
    public float idleFlicker = 0.12f;
    public float activeFlicker = 0.35f;
    public float flickerSpeed = 2.0f;
    public float blinkPeriod = 1.0f;
    public UnityEvent onAnyKey;

    Material mat;
    float blinkTimer = 0f;

    void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        mat = targetRenderer.material;
    }

    void Update()
    {
        // simple flicker via noise/time
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, transform.position.x * 0.1f);
        float flick = Mathf.Lerp(idleFlicker, activeFlicker, noise);
        if (mat.HasProperty(flickerParam)) mat.SetFloat(flickerParam, flick);

        // Pulse the glow slowly
        blinkTimer += Time.deltaTime;
        float pct = Mathf.Abs(Mathf.Sin(blinkTimer * (Mathf.PI*2f / blinkPeriod)));
        if (mat.HasProperty(glowParam))
            mat.SetFloat(glowParam, Mathf.Lerp(0.8f, 1.6f, pct));

        // Detect any key / mouse button / controller button
        if (Input.anyKeyDown)
        {
            onAnyKey?.Invoke();
            // quick bright flash
            if (mat.HasProperty(glowParam))
                StartCoroutine(FlashGlow(2.5f, 0.12f));
        }
    }

    System.Collections.IEnumerator FlashGlow(float peak, float duration)
    {
        float start = mat.GetFloat(glowParam);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float val = Mathf.Lerp(start, peak, t / duration);
            mat.SetFloat(glowParam, val);
            yield return null;
        }
        // return to normal
        float recover = 0f;
        float dur2 = 0.35f;
        t = 0;
        while (t < dur2)
        {
            t += Time.deltaTime;
            float val = Mathf.Lerp(peak, 1.0f, t / dur2);
            mat.SetFloat(glowParam, val);
            yield return null;
        }
    }
}
