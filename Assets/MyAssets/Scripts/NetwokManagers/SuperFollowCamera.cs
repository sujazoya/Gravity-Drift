using FishNet.Object;
using UnityEngine;

public class SuperFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Position Settings")]
    public Vector3 offset = new Vector3(0f, 2f, -5f);
    [Range(0.1f, 20f)] public float positionSmooth = 5f;

    [Header("Rotation Settings")]
    [Range(0.1f, 20f)] public float rotationSmooth = 2f; // Lower = smoother/slower
    public bool followRotation = true;
    public Vector3 lookOffset = Vector3.up * 1.5f; // Look slightly above player center

    [Header("Zoom Settings")]
    public string scrollAxis = "Mouse ScrollWheel";
    public float scrollSensitivity = 2f;
    public float minOffsetZ = -3f;
    public float maxOffsetZ = -10f;

    private void Start()
    {
        FindLocalPlayer();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindLocalPlayer();
            return;
        }

        HandleZoomInput();
        FollowTarget();
    }

    private void HandleZoomInput()
    {
        float scroll = Input.GetAxis(scrollAxis);
        offset.z += scroll * scrollSensitivity;
        offset.z = Mathf.Clamp(offset.z, maxOffsetZ, minOffsetZ);
    }

    private void FollowTarget()
    {
        // Smooth position
        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmooth * Time.deltaTime);

        if (followRotation)
        {
            // Look at player + offset
            Vector3 lookPoint = target.position + lookOffset;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

            // Smooth rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmooth * Time.deltaTime);
        }
        else
        {
            transform.LookAt(target.position + lookOffset);
        }
    }

    private void FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                target = player.transform;
                Debug.Log($"Camera following local player: {player.name}");
                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(target.position, target.TransformPoint(offset));
            Gizmos.DrawWireSphere(target.TransformPoint(offset), 0.5f);
        }
    }
}
