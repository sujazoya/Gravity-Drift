using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ButtonGlowController : MonoBehaviour {
    public Image glowImage;
    public float fadeSpeed = 2f;
    private bool revealed;

    void Update() {
        if (!revealed && FindObjectsOfType<ParticleSystem>().Length == 0) {
            revealed = true;
            StartCoroutine(FadeGlow());
        }
    }

    IEnumerator FadeGlow() {
        Color c = glowImage.color;
        while (c.a < 1f) {
            c.a += Time.deltaTime * fadeSpeed;
            glowImage.color = c;
            yield return null;
        }
    }
}
