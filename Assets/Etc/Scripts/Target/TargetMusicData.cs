using UnityEngine;

public class TargetMusicData : MonoBehaviour
{
    [Header("Target Music")]
    [SerializeField] private AudioClip musicClip;

    [SerializeField, Range(0f, 1f)]
    private float volume = 1f;

    public AudioClip MusicClip => musicClip;
    public float Volume => volume;
}