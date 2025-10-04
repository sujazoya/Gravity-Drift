using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Button))]
public class ButtonTrail : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Trail Settings")]
    public TrailPool trailPool;
    public float trailLifetime = 0.5f;      // How long each trail stays visible
    public float fadeSpeed = 3f;            // How fast trail fades

    private RectTransform buttonRect;

    void Awake()
    {
        buttonRect = GetComponent<RectTransform>();
        if (trailPool == null)
            Debug.LogError("TrailPool not assigned on " + name);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartCoroutine(SpawnTrail());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Optional: stop spawning trail on exit
        StopAllCoroutines();
    }

    private IEnumerator SpawnTrail()
    {
        while (true)
        {
            var trail = trailPool.Get(buttonRect.parent);

            // Match button position and rotation
            trail.position = buttonRect.position;
            trail.rotation = buttonRect.rotation;
            trail.localScale = buttonRect.localScale;

            // Make sure it renders **behind** the button
            trail.SetSiblingIndex(Mathf.Max(0, buttonRect.GetSiblingIndex() - 1));

            // Start fading
            StartCoroutine(FadeTrail(trail));

            // Wait a bit before spawning next trail for smooth effect
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator FadeTrail(RectTransform trail)
    {
        CanvasGroup cg = trail.GetComponent<CanvasGroup>();
        if (cg == null) yield break;

        cg.alpha = 1f;
        float timer = 0f;

        while (timer < trailLifetime)
        {
            timer += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, timer / trailLifetime);
            yield return null;
        }

        trailPool.Return(trail);
    }
}
        