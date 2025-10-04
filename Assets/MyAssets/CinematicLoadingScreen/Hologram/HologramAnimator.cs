using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class HologramAnimator : MonoBehaviour
{
    private Material matInstance;
    public float pulseSpeed = 2f;
    public float minTransparency = 0.4f;
    public float maxTransparency = 0.7f;

    void Start()
    {
        matInstance = GetComponent<Renderer>().material;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float transparency = Mathf.Lerp(minTransparency, maxTransparency, t);
        matInstance.SetFloat("_Transparency", transparency);
    }
}
