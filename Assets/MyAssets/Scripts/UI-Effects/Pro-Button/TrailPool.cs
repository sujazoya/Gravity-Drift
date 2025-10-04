using System.Collections.Generic;
using UnityEngine;

public class TrailPool : MonoBehaviour
{
    [Tooltip("Prefab must contain an Image + CanvasGroup")]
    public RectTransform trailPrefab;
    public int initialSize = 30;

    private Queue<RectTransform> pool = new Queue<RectTransform>();

    void Awake()
    {
        // Pre-instantiate pool
        for (int i = 0; i < initialSize; i++)
        {
            var t = Instantiate(trailPrefab, transform);
            t.gameObject.SetActive(false);
            pool.Enqueue(t);
        }
    }

    public RectTransform Get(Transform parent)
    {
        RectTransform t;
        if (pool.Count > 0) t = pool.Dequeue();
        else t = Instantiate(trailPrefab, transform);

        t.SetParent(parent, false);
        t.gameObject.SetActive(true);
        return t;
    }

    public void Return(RectTransform t)
    {
        t.gameObject.SetActive(false);
        t.SetParent(transform, false);
        pool.Enqueue(t);
    }
}
