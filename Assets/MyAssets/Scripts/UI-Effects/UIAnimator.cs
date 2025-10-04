using UnityEngine;
using UnityEngine.UI; // Required for UI types like Image, Text
using tweens = LeanTween; // Alias for LeanTween namespace

public class UIAnimator : MonoBehaviour
{
    public RectTransform menuPanel;
    public Image backgroundImage;

    void Start()
    {
        // Slide a panel in from the left
        LeanTween.moveX(menuPanel, 0f, 0.7f)
            .setEase(LeanTweenType.easeOutBack) // Adds a nice "overshoot" effect
            .setDelay(0.2f); // Wait before starting

        // Fade in a background image
        LeanTween.alpha(backgroundImage.rectTransform, 1f, 0.5f)
            .setEase(LeanTweenType.easeInOutSine);
    }





    public void ShowPopup(CanvasGroup popup)
    {
        // Enable interactivity first
        popup.blocksRaycasts = true;
        popup.interactable = true;

        // Animate fade-in and gentle scale
        LeanTween.alphaCanvas(popup, 1f, 0.3f);
        LeanTween.scale(popup.gameObject, Vector3.one, 0.3f)
            .setEase(LeanTweenType.easeOutBack);
    }

    public void HidePopup(CanvasGroup popup)
    {
        // Disable interactivity during fade-out
        popup.blocksRaycasts = false;
        popup.interactable = false;

        LeanTween.alphaCanvas(popup, 0f, 0.3f);
        LeanTween.scale(popup.gameObject, Vector3.zero, 0.3f)
            .setEase(LeanTweenType.easeInBack);
    }
    void AnimateButtonPingPong(RectTransform button)
    {
        // This is the cleanest and most performant way
        LeanTween.scale(button, Vector3.one * 1.2f, 0.25f)
            .setEase(LeanTweenType.easeInOutSine) // Use a smooth in-out curve
            .setLoopPingPong(); // <-- This correctly works on the tween itself
    }
    public class ButtonAnimation : MonoBehaviour
{
    public void OnButtonClick()
    {
        RectTransform buttonRect = GetComponent<RectTransform>();
        
        // Scale down on press, then back to normal. Provides tactile feedback.
        LeanTween.scale(buttonRect, Vector3.one * 0.95f, 0.1f)
            .setOnComplete(() => {
                LeanTween.scale(buttonRect, Vector3.one, 0.1f);
            });
    }
}
}