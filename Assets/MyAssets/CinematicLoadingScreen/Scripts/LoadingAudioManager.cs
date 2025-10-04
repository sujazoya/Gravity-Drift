using UnityEngine;

public class LoadingAudioManager : MonoBehaviour
{
    public AudioSource ambientLoop;
    public AudioSource riser; // rises with progress
    public AudioClip finalHit;

    public void SetProgress(float p)
    {
        if (riser != null)
            riser.pitch = 1f + p * 1.6f;
        if (ambientLoop != null)
            ambientLoop.volume = Mathf.Lerp(0.5f, 0.9f, p);
    }

    public void PlayFinal()
    {
        if (finalHit != null)
            AudioSource.PlayClipAtPoint(finalHit, Camera.main.transform.position);
    }
}
