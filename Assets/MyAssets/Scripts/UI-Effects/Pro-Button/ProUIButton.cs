using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ProUIButton : MonoBehaviour
{
    [Header("References")]
    public RectTransform rect;            // the button's RectTransform
    public Button button;                // Unity UI Button (for interact)
    public CanvasGroup canvasGroup;      // for fading
    public ParticleSystem shineParticles; // press sparkle (child)
    public ParticleSystem blastParticles; // finish blast (child)

    [Header("Trail (UI)")]
    public UITrailSpawner trailSpawner;
    public float enterDuration = 0.9f;
    public Vector2 enterFrom = new Vector2(-2000, 0); // screen-space offset for entry

    [Header("Press / Pop")]
    public float popScale = 1.18f;
    public float popTime = 0.12f;
    public LeanTweenType entryEase = LeanTweenType.easeInElastic; // exposed in Inspector
    public float fadeDuration = 0.1f;


    [Header("Vanish")]
    public float vanishDuration = 0.6f;
    public float vanishScale = 0f;

    Vector2 originalAnchoredPos;
    Vector3 originalScale;

    void Reset()
    {
        // auto-assign if possible
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        button = GetComponent<Button>();
    }

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rect == null) rect = GetComponent<RectTransform>();
        originalAnchoredPos = rect.anchoredPosition;
        originalScale = rect.localScale;
        canvasGroup.alpha = 0f;
        // place off-screen for entry
        rect.anchoredPosition = originalAnchoredPos + enterFrom;
    }

    void Start()
    {
        // animate in
        CanvasShowAndEnter();
        if (button != null)
        {
            // Wire up button click
            button.onClick.AddListener(OnButtonPressed);
        }
    }

    public void CanvasShowAndEnter()
    {
        canvasGroup.alpha = 1f; // reveal and then animate movement

        LeanTween.move(rect, originalAnchoredPos, enterDuration)
         .setEase(entryEase)   // now configurable
         .setOnStart(() => {
             canvasGroup.alpha = 1;
         })
         .setOnComplete(() => {
             if (trailSpawner != null) trailSpawner.enabled = false;
         });       
        
        // Start a trail for the duration of the entry
        if (trailSpawner != null)
            trailSpawner.StartTrail(rect, enterDuration);
    }

    public void OnButtonPressed()
    {
        // Press sparkle
        if (shineParticles != null)
        {
            shineParticles.Play();
        }

        // Pop scale
        LeanTween.scale(rect, originalScale * popScale, popTime).setEasePunch()
            .setOnComplete(() =>
            {
                LeanTween.scale(rect, originalScale, popTime).setEaseOutBack();
            });

        // User can trigger start of work here — your code should do actual work and then call OnWorkDone()
    }

    /// <summary>
    /// Call this when the task finishes and you want the button to vanish with a blast
    /// </summary>
    public void OnWorkDone()
    {
        // Fade out fast
        LeanTween.alphaCanvas(canvasGroup, 0f, fadeDuration).setEaseInQuad();

        // Shrink the button with a small delay to sync with fade
        LeanTween.scale(rect, Vector3.one * vanishScale, vanishDuration).setEaseInBack();

        // Play blast effect after fade out starts (can adjust timing)
        if (blastParticles != null)
        {
            blastParticles.Play();
        }

        // Disable button after animation completes
        StartCoroutine(DisableAfterAnimation());
    }

    private IEnumerator DisableAfterAnimation()
    {
        // Wait for the longer of vanish or fade
        yield return new WaitForSeconds(Mathf.Max(vanishDuration, fadeDuration));
        gameObject.SetActive(false);
    }

    // Optional: reset if you want to reuse the button (reactivate)
    public void ResetAndShow()
    {
        rect.localScale = originalScale;
        rect.anchoredPosition = originalAnchoredPos + enterFrom;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(true);
        blastParticles.gameObject.SetActive(true);
        //shineParticles.gameObject.SetActive(true);
        CanvasShowAndEnter();
    }
}
