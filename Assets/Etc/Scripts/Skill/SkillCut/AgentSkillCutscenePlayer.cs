using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class AgentSkillCutscenePlayer : MonoBehaviour
{
    [System.Serializable]
    private class AgentSkillCutsceneEntry
    {
        [SerializeField] private AgentRole agentRole;
        [SerializeField] private string skillKey;
        [SerializeField] private Sprite cutsceneSprite;

        public AgentRole AgentRole => agentRole;
        public string SkillKey => NormalizeSkillKey(skillKey);
        public Sprite CutsceneSprite => cutsceneSprite;
    }

    [Header("References")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image skillCutImage;

    [Header("DOTween Animations")]
    [SerializeField] private DOTweenAnimation backgroundFadeAnimation;
    [SerializeField] private DOTweenAnimation skillCutEnterAnimation;
    [SerializeField] private DOTweenAnimation skillCutExitAnimation;

    [Header("Cutscene Sprites")]
    [SerializeField] private List<AgentSkillCutsceneEntry> cutsceneEntries = new List<AgentSkillCutsceneEntry>();

    [Header("Timing")]
    [SerializeField] private float holdSecondsAfterEnter = 2f;
    [SerializeField] private float fallbackAnimationSeconds = 0.3f;

    [Header("Options")]
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool deactivateAfterPlay = false;
    [SerializeField] private bool cancelPreviousCutscene = true;
    [SerializeField] private bool logMissingSprite = true;

    private readonly Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();
    private Coroutine playCoroutine;

    private void Awake()
    {
        if (rootObject == null)
            rootObject = gameObject;

        RebuildLookup();

        if (hideOnAwake)
            HideImmediately();
    }

    private void OnEnable()
    {
        SkillCutsceneEventBus.AgentSkillCutsceneRequested += PlayAgentCutscene;
    }

    private void OnDisable()
    {
        SkillCutsceneEventBus.AgentSkillCutsceneRequested -= PlayAgentCutscene;

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        StopAnimations();
    }

    private void RebuildLookup()
    {
        spriteLookup.Clear();

        for (int i = 0; i < cutsceneEntries.Count; i++)
        {
            AgentSkillCutsceneEntry entry = cutsceneEntries[i];

            if (entry == null)
                continue;

            if (entry.CutsceneSprite == null)
                continue;

            if (string.IsNullOrWhiteSpace(entry.SkillKey))
                continue;

            string key = BuildLookupKey(entry.AgentRole, entry.SkillKey);

            if (!spriteLookup.ContainsKey(key))
                spriteLookup.Add(key, entry.CutsceneSprite);
        }
    }

    private void PlayAgentCutscene(AgentController agent, string skillKey)
    {
        if (agent == null)
            return;

        if (agent.Stats == null)
            return;

        string normalizedSkillKey = NormalizeSkillKey(skillKey);
        string lookupKey = BuildLookupKey(agent.Stats.role, normalizedSkillKey);

        if (!spriteLookup.TryGetValue(lookupKey, out Sprite sprite))
        {
            if (logMissingSprite)
            {
                Debug.LogWarning(
                    $"[AgentSkillCutscenePlayer] 등록된 컷신 이미지가 없습니다. Role: {agent.Stats.role}, Skill: {normalizedSkillKey}"
                );
            }

            return;
        }

        if (playCoroutine != null)
        {
            if (!cancelPreviousCutscene)
                return;

            StopCoroutine(playCoroutine);
            playCoroutine = null;

            StopAnimations();
        }

        playCoroutine = StartCoroutine(PlayCutsceneRoutine(sprite));
    }

    private IEnumerator PlayCutsceneRoutine(Sprite sprite)
    {
        ShowImmediately(sprite);

        ResetAnimationToStart(backgroundFadeAnimation);
        ResetAnimationToStart(skillCutEnterAnimation);
        ResetAnimationToStart(skillCutExitAnimation);

        PlayForward(backgroundFadeAnimation);
        PlayForward(skillCutEnterAnimation);

        yield return new WaitForSeconds(GetAnimationSeconds(skillCutEnterAnimation));

        if (holdSecondsAfterEnter > 0f)
            yield return new WaitForSeconds(holdSecondsAfterEnter);

        PlayBackward(backgroundFadeAnimation);

        ResetAnimationToStart(skillCutExitAnimation);
        PlayForward(skillCutExitAnimation);

        float endWaitSeconds = Mathf.Max(
            GetAnimationSeconds(backgroundFadeAnimation),
            GetAnimationSeconds(skillCutExitAnimation)
        );

        yield return new WaitForSeconds(endWaitSeconds);

        StopAnimations();
        HideImmediately();

        playCoroutine = null;
    }

    private void PlayForward(DOTweenAnimation animation)
    {
        if (animation == null)
            return;

        animation.DORestart();
    }

    private void PlayBackward(DOTweenAnimation animation)
    {
        if (animation == null)
            return;

        animation.DOPlayBackwards();
    }

    private void ResetAnimationToStart(DOTweenAnimation animation)
    {
        if (animation == null)
            return;

        animation.DORewind();
    }

    private void StopAnimations()
    {
        PauseAnimation(backgroundFadeAnimation);
        PauseAnimation(skillCutEnterAnimation);
        PauseAnimation(skillCutExitAnimation);
    }

    private void PauseAnimation(DOTweenAnimation animation)
    {
        if (animation == null)
            return;

        animation.DOPause();
    }

    private float GetAnimationSeconds(DOTweenAnimation animation)
    {
        if (animation == null)
            return fallbackAnimationSeconds;

        int loopCount = animation.loops;

        if (loopCount <= 0)
            loopCount = 1;

        float seconds = animation.delay + animation.duration * loopCount;

        if (seconds <= 0f)
            seconds = fallbackAnimationSeconds;

        return seconds;
    }

    private void ShowImmediately(Sprite sprite)
    {
        if (rootObject != null)
            rootObject.SetActive(true);

        if (backgroundImage != null)
        {
            Color color = backgroundImage.color;
            color.a = 0f;
            backgroundImage.color = color;
            backgroundImage.enabled = true;
        }

        if (skillCutImage != null)
        {
            skillCutImage.sprite = sprite;
            skillCutImage.enabled = true;
        }
    }

    private void HideImmediately()
    {
        if (backgroundImage != null)
        {
            Color color = backgroundImage.color;
            color.a = 0f;
            backgroundImage.color = color;
            backgroundImage.enabled = false;
        }

        if (skillCutImage != null)
        {
            skillCutImage.sprite = null;
            skillCutImage.enabled = false;
        }

        if (deactivateAfterPlay && rootObject != null && rootObject != gameObject)
            rootObject.SetActive(false);
    }

    private string BuildLookupKey(AgentRole role, string skillKey)
    {
        return $"{role}_{NormalizeSkillKey(skillKey)}";
    }

    public static string NormalizeSkillKey(string skillKey)
    {
        if (string.IsNullOrWhiteSpace(skillKey))
            return string.Empty;

        return skillKey
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "");
    }
}