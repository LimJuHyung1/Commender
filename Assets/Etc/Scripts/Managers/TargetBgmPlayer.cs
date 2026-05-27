using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TargetBgmPlayer : MonoBehaviour
{
    public static TargetBgmPlayer Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    [Header("Options")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private float fadeDuration = 0.75f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    public void PlayTargetMusic(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        TargetMusicData musicData = targetObject.GetComponentInChildren<TargetMusicData>(true);

        if (musicData == null)
        {
            Debug.LogWarning("[TargetBgmPlayer] TargetMusicData를 찾지 못했습니다.");
            return;
        }

        PlayMusic(musicData.MusicClip, musicData.Volume);
    }

    public void PlayMusic(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Debug.LogWarning("[TargetBgmPlayer] 재생할 음악 클립이 없습니다.");
            return;
        }

        if (audioSource.clip == clip && audioSource.isPlaying)
        {
            audioSource.volume = volume;
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(ChangeMusicRoutine(clip, volume));
    }

    public void StopMusic()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(StopMusicRoutine());
    }

    private IEnumerator ChangeMusicRoutine(AudioClip nextClip, float targetVolume)
    {
        float startVolume = audioSource.volume;

        if (audioSource.isPlaying)
        {
            float timer = 0f;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float ratio = timer / fadeDuration;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, ratio);
                yield return null;
            }
        }

        audioSource.Stop();
        audioSource.clip = nextClip;
        audioSource.volume = 0f;
        audioSource.Play();

        float fadeInTimer = 0f;

        while (fadeInTimer < fadeDuration)
        {
            fadeInTimer += Time.deltaTime;
            float ratio = fadeInTimer / fadeDuration;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, ratio);
            yield return null;
        }

        audioSource.volume = targetVolume;
        fadeRoutine = null;
    }

    private IEnumerator StopMusicRoutine()
    {
        float startVolume = audioSource.volume;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float ratio = timer / fadeDuration;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, ratio);
            yield return null;
        }

        audioSource.Stop();
        audioSource.clip = null;
        fadeRoutine = null;
    }
}