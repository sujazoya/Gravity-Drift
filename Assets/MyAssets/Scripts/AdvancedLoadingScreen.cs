using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class AdvancedLoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    public Image progressRing;   // Circular progress indicator
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI tipText;

    [Header("3D Elements")]
    public Transform arenaSphere;  // Rotating 3D planet/arena
    public float rotationSpeed = 15f;

    [Header("Tips")]
    public string[] gameplayTips;

    private void Start()
    {
        StartCoroutine(LoadLevelAsync("Arena01"));
        ShowRandomTip();
    }

    private void Update()
    {
        // Rotate planet for dynamic effect
        if (arenaSphere != null)
            arenaSphere.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    IEnumerator LoadLevelAsync(string sceneName)
    {
        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);

            // Update circular ring
            progressRing.fillAmount = progress;

            // Update status text
            statusText.text = $"Initializing Gravity Field... {progress * 100:F0}%";

            if (op.progress >= 0.9f)
            {
                statusText.text = "Press any key to Engage!";
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
