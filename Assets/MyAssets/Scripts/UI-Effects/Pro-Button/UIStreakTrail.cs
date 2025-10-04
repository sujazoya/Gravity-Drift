using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIStreakTrail : MonoBehaviour
{
    public Image streakPrefab;      // assign thin glowing sprite
    public float spawnInterval = 0.05f;
    public float lifeTime = 0.5f;
    public float scale = 1f;

    private float timer;
    private List<Image> pool = new List<Image>();

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnStreak();
        }
    }

    void SpawnStreak()
    {
        Image streak = GetFromPool();
        streak.transform.SetParent(transform.parent, false);
        streak.rectTransform.position = transform.position;
        streak.rectTransform.localScale = Vector3.one * scale;

        // fade + shrink out
        LeanTween.alpha(streak.rectTransform, 0, lifeTime)
                 .setOnComplete(() => streak.gameObject.SetActive(false));
        LeanTween.scale(streak.rectTransform, Vector3.zero, lifeTime);
    }

    Image GetFromPool()
    {
        foreach (var s in pool)
        {
            if (!s.gameObject.activeInHierarchy)
            {
                s.gameObject.SetActive(true);
                return s;
            }
        }
        var newStreak = Instantiate(streakPrefab, transform.position, Quaternion.identity);
        pool.Add(newStreak);
        return newStreak;
    }
}
