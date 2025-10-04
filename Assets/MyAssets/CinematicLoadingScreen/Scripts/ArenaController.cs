using UnityEngine;

public class ArenaController : MonoBehaviour
{
    public float rotateSpeed = 10f;
    public Material arenaMaterial; // assign NeonArena_Material (instance)
    public string emissionProperty = "_EmissionGain";
    public float baseEmission = 1f;
    public float maxEmission = 4f;
    public float emissionPulseSpeed = 0.8f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        if (arenaMaterial != null)
        {
            float t = (Mathf.Sin(Time.time * emissionPulseSpeed) + 1f) * 0.5f;
            float emission = Mathf.Lerp(baseEmission, maxEmission, t);
            arenaMaterial.SetFloat(emissionProperty, emission);
        }
    }
}
