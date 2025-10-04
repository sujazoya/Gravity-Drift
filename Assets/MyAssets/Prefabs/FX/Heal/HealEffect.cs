using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class HealEffect : MonoBehaviour
{
     public AudioSource audioSource;
    public AudioClip[] healClips; // impact, zap, woosh

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (healClips.Length > 0)
        {
            PlayDamageSound();
        }
    }

     void PlayDamageSound()
    {
        if(healClips.Length > 0)
        {
            var healClip = healClips[Random.Range(0, healClips.Length)];
            audioSource.PlayOneShot(healClip);
        }
    }
}