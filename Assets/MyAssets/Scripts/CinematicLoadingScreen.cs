using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class CinematicLoadingScreen : MonoBehaviour
{
    [Header("Camera")]
    public Transform cameraRig; // Empty object holding the camera
    public float orbitSpeed = 10f;
    public float zoomAmplitude = 2f;
    public float zoomSpeed = 1f;

    [Header("Arena")]
    public Transform arenaSphere;  // The glowing arena model
    public float arenaRotationSpeed = 15f;

    [Header("Holographic Rings")]
    public Transform progressRing; // A neon energy ring around the arena
    public float ringMaxScale = 5f;

    [Header("UI")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI tipText;
    public string[] gameplayTips;

    private void Start()
    {
        ShowRandomTip();
        StartCoroutine(LoadLevelAsync("Arena01"));
    }

    private void Update()
    {
        // Camera orbit + zoom motion
        cameraRig.RotateAround(arenaSphere.position, Vector3.up, orbitSpeed * Time.deltaTime);
        float zoomOffset = Mathf.Sin(Time.time * zoomSpeed) * zoomAmplitude;
        cameraRig.localPosition = new Vector3(cameraRig.localPosition.x, cameraRig.localPosition.y + zoomOffset * Time.deltaTime, cameraRig.localPosition.z);

        // Arena rotation
        arenaSphere.Rotate(Vector3.up, arenaRotationSpeed * Time.deltaTime, Space.World);
    }

    IEnumerator LoadLevelAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);

            // Scale holographic ring to show progress
            progressRing.localScale = Vector3.one * Mathf.Lerp(1f, ringMaxScale, progress);

            // Update status hologram
            statusText.text = $"Calibrating Orbit Systems... {progress * 100:F0}%";

            if (op.progress >= 0.9f)
            {
                statusText.text = "Gravity Wells Stabilized. Press Any Key to Engage!";
                if (Input.anyKeyDown)
                {
                    op.allowSceneActivation = true;
                }
            }

            yield return null;
        }
    }

    void ShowRandomTip()
    {
        if (gameplayTips.Length > 0)
        {
            int index = Random.Range(0, gameplayTips.Length);
            tipText.text = "TIP: " + gameplayTips[index];
        }
    }
}
