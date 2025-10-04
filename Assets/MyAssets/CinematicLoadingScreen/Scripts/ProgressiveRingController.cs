using UnityEngine;

public class ProgressiveRingController : MonoBehaviour
{
    public Transform ringTransform;
    public float minScale = 0.8f;
    public float maxScale = 3.5f;
    public float pulseAmount = 1.15f;
    public float pulseSpeed = 4f;

    private float currentProgress = 0f;
   [SerializeField] private bool pulse = false;

    public void SetProgress(float pr) // pr = 0..1
    {
        currentProgress = Mathf.Clamp01(pr);
        float s = Mathf.Lerp(minScale, maxScale, currentProgress);
        ringTransform.localScale = Vector3.one * s;
        if (currentProgress >= 0.999f) pulse = true;
    }

    void Update()
    {
        if (pulse)
        {
            float factor = 1 + Mathf.Sin(Time.time * pulseSpeed) * 0.05f;
            ringTransform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale * pulseAmount, currentProgress) * factor;
        }
    }
}
