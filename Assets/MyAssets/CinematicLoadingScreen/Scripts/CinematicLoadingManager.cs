using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using TMPro;
using System.Collections;

[RequireComponent(typeof(PlayableDirector))]
public class CinematicLoadingManager : MonoBehaviour
{
    [Header("Target Scene")]
    public string sceneToLoad = "Arena01";

    [Header("UI")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI tipText;
    public TextMeshProUGUI percentageText;
    public GameObject pressAnyKeyHologram;

    [Header("Gameplay Tips")]
    [TextArea(3,6)] public string[] gameplayTips;

    [Header("Progress & Timing")]
    public float progressSmoothSpeed = 4f;
    public float minDisplayTime = 2.0f;

    [Header("Playable/Timeline")]
    public PlayableDirector loadingTimeline;
    public double timelineBufferSeconds = 0.5;

    [Header("Linked Systems")]
    public ProgressiveRingController ringController;
    public VFXSpawner vfxSpawner;
    public LoadingAudioManager audioManager;

    private AsyncOperation loadOp;
    private float visualProgress = 0f;
    private bool readyToActivate = false;
    private bool playerConfirmed = false;

    void Start()
    {
        if (gameplayTips != null && gameplayTips.Length > 0)
            tipText.text = "TIP: " + gameplayTips[Random.Range(0, gameplayTips.Length)];

        if (pressAnyKeyHologram != null) pressAnyKeyHologram.SetActive(false);
        StartCoroutine(BeginLoad());
    }

    IEnumerator BeginLoad()
    {
        float startTime = Time.time;
        loadOp = SceneManager.LoadSceneAsync(sceneToLoad);
        loadOp.allowSceneActivation = false;

        if (loadingTimeline != null) loadingTimeline.Play();

        while (loadOp.progress < 0.9f)
        {
            float target = Mathf.Clamp01(loadOp.progress / 0.9f);
            visualProgress = Mathf.MoveTowards(visualProgress, target, Time.deltaTime * progressSmoothSpeed);
            UpdateUI(visualProgress);
            yield return null;
        }

        visualProgress = 1f;
        UpdateUI(visualProgress);

        float elapsed = Time.time - startTime;
        float remaining = Mathf.Max(0f, minDisplayTime - elapsed);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        if (loadingTimeline != null)
        {
            while (loadingTimeline.state == PlayState.Playing) yield return null;
            yield return new WaitForSeconds((float)timelineBufferSeconds);
        }

        if (pressAnyKeyHologram != null) pressAnyKeyHologram.SetActive(true);
        if (statusText != null) statusText.text = "GRAVITY WELLS STABILIZED";

        readyToActivate = true;
        float autoActivateDelay = 8f;
        float timer = 0f;
        while (!playerConfirmed && timer < autoActivateDelay)
        {
            if (Input.anyKeyDown) { playerConfirmed = true; break; }
            timer += Time.deltaTime;
            yield return null;
        }

        loadOp.allowSceneActivation = true;
    }

    void UpdateUI(float prog)
    {
        int percent = Mathf.RoundToInt(prog * 100f);
        if (percentageText != null) percentageText.text = percent + "%";
        if (statusText != null) statusText.text = $"CALIBRATING ORBIT SYSTEMS... {percent}%";

        // propagate to other systems
        if (ringController != null) ringController.SetProgress(prog);
        if (vfxSpawner != null) vfxSpawner.SetProgress(prog);
        if (audioManager != null) audioManager.SetProgress(prog);

        if (prog >= 0.999f && audioManager != null) audioManager.PlayFinal();
    }
}
