using UnityEngine;

public class HologramInteractor : MonoBehaviour
{
    public Material mat;
    private float targetGlow = 2f;

    void Update()
    {
        // Player aims at hologram
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 8f))
        {
            if (hit.collider.gameObject == gameObject)
                targetGlow = 5f; // highlight
            else
                targetGlow = 2f;
        }

        float currentGlow = Mathf.Lerp(mat.GetFloat("_GlowIntensity"), targetGlow, Time.deltaTime * 5);
        mat.SetFloat("_GlowIntensity", currentGlow);
    }
}
