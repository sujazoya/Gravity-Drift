using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UITrailSpawner : MonoBehaviour
{
    [Header("Trail Settings")]
    public TrailPool pool;
    public float spawnInterval = 0.02f;      // how often a dot spawns
    public float fadeDuration = 0.6f;        // fade time for each dot
    public float scaleEnd = 0.2f;            // final scale multiplier
    public LeanTweenType fadeEase = LeanTweenType.linear;

    private Coroutine running;

    /// <summary>
    /// Starts the trail for a target RectTransform for the given duration
    /// </summary>
    public void StartTrail(RectTransform target, float duration)
    {
        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(TrailRoutine(target, duration));
    }

    private IEnumerator TrailRoutine(RectTransform target, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            SpawnDotAt(target);
            t += spawnInterval;
            yield return new WaitForSeconds(spawnInterval);
        }

        running = null;
    }

    private void SpawnDotAt(RectTransform target)
    {
        if (pool == null || target == null) return;

        // Get trail dot from pool
        RectTransform dot = pool.Get(target.parent);

        // Convert target world position to local of parent
        Vector3 worldPos = target.TransformPoint(target.rect.center);
        dot.position = worldPos;

        // Reset scale
        dot.localScale = Vector3.one;

        // Make sure the dot renders behind the button
        int buttonIndex = target.GetSiblingIndex();
        dot.SetSiblingIndex(Mathf.Max(0, buttonIndex - 1));

        // Handle fading using CanvasGroup
        CanvasGroup cg = dot.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            LeanTween.alphaCanvas(cg, 0f, fadeDuration)
                .setEase(fadeEase)
                .setOnComplete(() => pool.Return(dot));
        }

        // Scale down while fading
        LeanTween.scale(dot, Vector3.one * scaleEnd, fadeDuration).setEase(fadeEase);
    }
}
