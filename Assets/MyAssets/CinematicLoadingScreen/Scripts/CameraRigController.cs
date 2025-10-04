using UnityEngine;

public class CameraRigController : MonoBehaviour
{
    public Transform target;
    public float orbitSpeed = 6f;
    public float distance = 10f;
    public float height = 1.75f;
    public bool allowManualRotate = true;
    public float manualSensitivity = 80f;
    public float smoothSpeed = 8f;

    private Vector3 localOffset;
    private float yaw = 0f;

    void Start()
    {
        if (target == null) Debug.LogWarning("CameraRig requires a target.");
        localOffset = new Vector3(0, height, -distance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Auto orbit
        yaw += orbitSpeed * Time.deltaTime;
        float desiredYaw = yaw;

        // manual input (mouse or joystick)
        if (allowManualRotate)
        {
            float mouseX = Input.GetAxis("Mouse X");
            if (Mathf.Abs(mouseX) > 0.01f) desiredYaw += mouseX * manualSensitivity * Time.deltaTime;
        }

        Quaternion rot = Quaternion.Euler(0f, desiredYaw, 0f);
        Vector3 desiredPos = target.position + rot * localOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);
        transform.LookAt(target.position + Vector3.up * 0.5f); // slight look offset
    }
}
