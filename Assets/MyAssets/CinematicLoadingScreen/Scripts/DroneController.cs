using UnityEngine;

public class DroneController : MonoBehaviour
{
    public Transform center;
    public float orbitRadius = 6f;
    public float orbitSpeed = 40f;
    public float heightOffset = 0.5f;
    public float rotationOffset = 0f;

    void Start()
    {
        if (center == null) center = transform.parent;
    }

    void Update()
    {
        rotationOffset += orbitSpeed * Time.deltaTime;
        float rad = rotationOffset * Mathf.Deg2Rad;
        Vector3 pos = center.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitRadius + Vector3.up * heightOffset;
        transform.position = pos;
        transform.LookAt(center.position);
         transform.Rotate(0f, 90f, 0f, Space.Self); // flip front/back
    }
}
