using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class DamageEffect : MonoBehaviour
{
     public AudioSource audioSource;
    public AudioClip[] damageClips; // impact, zap, woosh

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (damageClips.Length > 0)
        {
            PlayDamageSound();
        }
    }

     void PlayDamageSound()
    {
        if (damageClips.Length > 0)
        {
            var clip = damageClips[Random.Range(0, damageClips.Length)];
            audioSource.PlayOneShot(clip);
        }
    }
}