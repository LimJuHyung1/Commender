using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private UIController uiController;
    [SerializeField] private StageMapManager stageMapManager;
    [SerializeField] private SkillCameraDirector skillCameraDirector;
    [SerializeField] private UpgradeRewardUI upgradeRewardUI;

    [Header("Stage Settings")]
    [SerializeField] private string missionDescription = "제한 시간 안에 타겟을 체포하세요.";
    [SerializeField] private float timeLimitSeconds = 300f;

    [Header("Scene 이동")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private bool autoLoadNextStageOnWin = true;
    [SerializeField] private bool autoReturnToLobbyOnFinalWin = true;
    [SerializeField] private bool autoReturnToLobbyOnFail = false;
    [SerializeField] private float returnDelaySeconds = 2.0f;

    [Header("Upgrade Reward")]
    [SerializeField] private bool showUpgradeRewardOnStageClear = true;
    [SerializeField] private bool skipUpgradeRewardInDebugStage = true;

    [Header("시간 배속")]
    [SerializeField] private float winTimeScale = 0.5f;
    [SerializeField] private float failTimeScale = 0.5f;

    [Header("Fail Animation")]
    [SerializeField] private bool playTargetTimeOverAnimationOnFail = true;

    [Header("Fail Camera")]
    [SerializeField] private bool playTargetCameraOnFail = true;
    [SerializeField] private bool forceShowTargetOnFail = true;

    [Header("Debug Target Reveal")]
    [SerializeField] private bool startWithTargetDebugReveal = false;

    private bool stageFinished = false;
    private bool timerRunning = false;
    private bool stageResultMotionApplied = false;
    private float remainingTime = 0f;

    public bool IsTargetDebugRevealEnabled { get; private set; }
    public bool IsStageFinished => stageFinished;

    private const string DebugStageEnabledKey = "DebugStageEnabled";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveReferences();

        Time.timeScale = 1.0f;
        IsTargetDebugRevealEnabled = startWithTargetDebugReveal;

        if (uiController != null)
        {
            uiController.HideResultPanel();
            uiController.CloseOptionPanelImmediate();
            uiController.SetMissionText(missionDescription);
            uiController.SetTimerText(0f);
            uiController.SetTimerVisible(false);
        }
    }

    private void OnEnable()
    {
        CatchZone.OnTargetCaught += HandleWin;
    }

    private void OnDisable()
    {
        CatchZone.OnTargetCaught -= HandleWin;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        SetupStage();
    }

    private void Update()
    {
        UpdateStageTimer();
    }

    private void ResolveReferences()
    {
        if (uiController == null)
            uiController = FindFirstObjectByType<UIController>();

        if (stageMapManager == null)
            stageMapManager = FindFirstObjectByType<StageMapManager>();

        if (skillCameraDirector == null)
            skillCameraDirector = FindFirstObjectByType<SkillCameraDirector>();

        if (upgradeRewardUI == null)
            upgradeRewardUI = FindFirstObjectByType<UpgradeRewardUI>(FindObjectsInactive.Include);
    }

    private void SetupStage()
    {
        ResolveReferences();

        stageFinished = false;
        timerRunning = true;
        stageResultMotionApplied = false;
        remainingTime = Mathf.Max(0.1f, timeLimitSeconds);

        Time.timeScale = 1.0f;

        if (uiController != null)
        {
            uiController.HideResultPanel();
            uiController.SetMissionText(missionDescription);
            uiController.SetTimerText(remainingTime);
            uiController.SetTimerVisible(true);
        }

        if (upgradeRewardUI != null)
            upgradeRewardUI.HideImmediate();

        Debug.Log($"[GameManager] 스테이지 시작: mission={missionDescription}, timeLimit={timeLimitSeconds}");
    }

    private void UpdateStageTimer()
    {
        if (stageFinished || !timerRunning)
            return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;

            if (uiController != null)
                uiController.SetTimerText(remainingTime);

            FailStage("제한 시간 안에 타겟을 체포하지 못했습니다.");
            return;
        }

        if (uiController != null)
            uiController.SetTimerText(remainingTime);
    }

    private void HandleWin(GameObject target)
    {
        if (stageFinished)
            return;

        string message = target != null
            ? $"축하합니다! {target.name}을(를) 체포했습니다!"
            : "축하합니다! 타겟을 체포했습니다!";

        CaptureSequenceController captureSequenceController =
            FindFirstObjectByType<CaptureSequenceController>();

        if (captureSequenceController != null)
        {
            stageFinished = true;
            timerRunning = false;

            ApplyWinStageResultMotion();

            StartCoroutine(CompleteStageAfterCaptureSequence(
                captureSequenceController,
                target,
                message
            ));

            return;
        }

        CompleteStage(message);
    }

    private IEnumerator CompleteStageAfterCaptureSequence(
        CaptureSequenceController captureSequenceController,
        GameObject target,
        string message)
    {
        AgentController catchingAgent = CatchZone.LastCatchingAgent;

        yield return captureSequenceController.PlayCaptureSequence(catchingAgent, target);

        stageFinished = false;
        CompleteStage(message);
    }

    public void CompleteStage(string message)
    {
        if (stageFinished)
            return;

        stageFinished = true;
        timerRunning = false;

        ResolveReferences();

        Debug.Log($"<color=green>[GameManager]</color> 스테이지 클리어: {message}");

        VisionConeVisualizer.DisableAllVisionCones();

        UnlockNextStage();
        ApplyWinStageResultMotion();

        if (uiController != null)
            uiController.ShowResultPanel(true, message);

        Time.timeScale = winTimeScale;

        if (autoLoadNextStageOnWin)
        {
            if (HasNextStage())
            {
                StartCoroutine(HandleNextStageAfterWin());
            }
            else if (autoReturnToLobbyOnFinalWin)
            {
                StartCoroutine(ReturnToLobbyAfterDelay());
            }
        }
        else if (autoReturnToLobbyOnFinalWin)
        {
            StartCoroutine(ReturnToLobbyAfterDelay());
        }
    }

    private IEnumerator HandleNextStageAfterWin()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);

        if (ShouldShowUpgradeReward())
        {
            ShowUpgradeRewardThenLoadNextStage();
            yield break;
        }

        LoadNextStageNow();
    }

    private bool ShouldShowUpgradeReward()
    {
        if (!showUpgradeRewardOnStageClear)
            return false;

        if (!autoLoadNextStageOnWin)
            return false;

        if (!HasNextStage())
            return false;

        if (skipUpgradeRewardInDebugStage && IsDebugStageMode())
            return false;

        if (UpgradeManager.Instance == null)
            return false;

        if (upgradeRewardUI == null)
            return false;

        return true;
    }

    private void ShowUpgradeRewardThenLoadNextStage()
    {
        ResolveReferences();

        if (UpgradeManager.Instance == null || upgradeRewardUI == null)
        {
            LoadNextStageNow();
            return;
        }

        if (uiController != null)
        {
            uiController.HideResultPanel();
            uiController.SetStageHudVisible(false);
            uiController.SetOptionButtonVisible(false);
        }

        int rewardStageNumber = GetNextStageNumber();

        List<UpgradeDefinition> choices =
            UpgradeManager.Instance.BuildAgentRewardChoices(rewardStageNumber);

        if (choices == null || choices.Count <= 0)
        {
            LoadNextStageNow();
            return;
        }

        upgradeRewardUI.ShowChoices(choices, selectedUpgrade =>
        {
            LoadNextStageNow();
        });
    }

    private int GetNextStageNumber()
    {
        ResolveReferences();

        if (stageMapManager == null)
            return 1;

        return stageMapManager.CurrentStageNumber + 1;
    }

    private bool IsDebugStageMode()
    {
        return PlayerPrefs.GetInt(DebugStageEnabledKey, 0) == 1;
    }

    private void LoadNextStageNow()
    {
        ResolveReferences();

        if (stageMapManager == null)
        {
            ReturnToLobby();
            return;
        }

        stageMapManager.SelectNextStage();

        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void FailStage(string message)
    {
        if (stageFinished)
            return;

        StartCoroutine(FailStageRoutine(message));
    }

    private IEnumerator FailStageRoutine(string message)
    {
        if (stageFinished)
            yield break;

        stageFinished = true;
        timerRunning = false;

        ResolveReferences();

        Debug.Log($"<color=red>[GameManager]</color> 스테이지 실패: {message}");

        if (forceShowTargetOnFail)
            ForceShowAllTargetsForFail();

        if (playTargetTimeOverAnimationOnFail)
            PlayTargetTimeOverAnimations();

        ApplyFailStageResultMotion();

        yield return PlayFailTargetCameraIfPossible();

        if (uiController != null)
            uiController.ShowResultPanel(false, message);

        Time.timeScale = failTimeScale;

        if (autoReturnToLobbyOnFail)
            StartCoroutine(ReturnToLobbyAfterDelay());
    }

    public void FailAndReturnToLobby(string message)
    {
        if (!stageFinished)
            FailStage(message);

        ReturnToLobby();
    }

    private void ForceShowAllTargetsForFail()
    {
        TargetVisibilityController[] visibilityControllers =
            FindObjectsByType<TargetVisibilityController>(FindObjectsSortMode.None);

        for (int i = 0; i < visibilityControllers.Length; i++)
        {
            TargetVisibilityController visibilityController = visibilityControllers[i];

            if (visibilityController == null)
                continue;

            if (!visibilityController.isActiveAndEnabled)
                continue;

            visibilityController.ForceShow();
        }
    }

    private IEnumerator PlayFailTargetCameraIfPossible()
    {
        if (!playTargetCameraOnFail)
            yield break;

        ResolveReferences();

        if (skillCameraDirector == null)
            yield break;

        Transform target = FindFailCameraTarget();

        if (target == null)
            yield break;

        yield return skillCameraDirector.ForcePlaySkillCameraAndWait(
            SkillCameraFocusMode.StrongTargetEvent,
            target
        );
    }

    private Transform FindFailCameraTarget()
    {
        TargetAnimationController[] targetAnimationControllers =
            FindObjectsByType<TargetAnimationController>(FindObjectsSortMode.None);

        for (int i = 0; i < targetAnimationControllers.Length; i++)
        {
            TargetAnimationController targetAnimationController = targetAnimationControllers[i];

            if (targetAnimationController == null)
                continue;

            if (!targetAnimationController.isActiveAndEnabled)
                continue;

            return targetAnimationController.transform;
        }

        TargetSkillController[] targetSkillControllers =
            FindObjectsByType<TargetSkillController>(FindObjectsSortMode.None);

        for (int i = 0; i < targetSkillControllers.Length; i++)
        {
            TargetSkillController targetSkillController = targetSkillControllers[i];

            if (targetSkillController == null)
                continue;

            if (!targetSkillController.isActiveAndEnabled)
                continue;

            return targetSkillController.transform;
        }

        try
        {
            GameObject taggedTarget = GameObject.FindGameObjectWithTag("Target");

            if (taggedTarget != null)
                return taggedTarget.transform;
        }
        catch (UnityException)
        {
        }

        return null;
    }

    private void ApplyWinStageResultMotion()
    {
        if (stageResultMotionApplied)
            return;

        stageResultMotionApplied = true;

        PlayAgentVictoryAnimations();
        StopAllMovingObjects();
    }

    private void ApplyFailStageResultMotion()
    {
        if (stageResultMotionApplied)
            return;

        stageResultMotionApplied = true;

        PlayAgentDefeatAnimations();
        StopAllMovingObjects();
    }

    private void PlayAgentVictoryAnimations()
    {
        AgentController[] agents =
            Object.FindObjectsByType<AgentController>(FindObjectsSortMode.None);

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!agent.isActiveAndEnabled)
                continue;

            agent.PlayVictoryPose();
        }
    }

    private void PlayAgentDefeatAnimations()
    {
        AgentController[] agents =
            Object.FindObjectsByType<AgentController>(FindObjectsSortMode.None);

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!agent.isActiveAndEnabled)
                continue;

            agent.PlayDefeatPose();
        }
    }

    private void PlayTargetTimeOverAnimations()
    {
        TargetAnimationController[] targetAnimationControllers =
            FindObjectsByType<TargetAnimationController>(FindObjectsSortMode.None);

        for (int i = 0; i < targetAnimationControllers.Length; i++)
        {
            TargetAnimationController targetAnimationController = targetAnimationControllers[i];

            if (targetAnimationController == null)
                continue;

            if (!targetAnimationController.isActiveAndEnabled)
                continue;

            targetAnimationController.PlayTimeOverCelebration();
        }
    }

    private void UnlockNextStage()
    {
        ResolveReferences();

        if (stageMapManager == null)
        {
            Debug.LogWarning("[GameManager] StageMapManager를 찾지 못해서 다음 스테이지 처리를 하지 못했습니다.");
            return;
        }

        stageMapManager.CompleteStage();
    }

    private bool HasNextStage()
    {
        ResolveReferences();
        return stageMapManager != null && stageMapManager.HasNextStage();
    }

    private IEnumerator LoadNextStageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);
        LoadNextStageNow();
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);
        ReturnToLobby();
    }

    private void StopAllMovingObjects()
    {
        AgentController[] agentControllers =
            Object.FindObjectsByType<AgentController>(FindObjectsSortMode.None);

        for (int i = 0; i < agentControllers.Length; i++)
        {
            AgentController agentController = agentControllers[i];

            if (agentController == null)
                continue;

            if (!agentController.isActiveAndEnabled)
                continue;

            agentController.StopAllMovementForStageResult();
        }

        NavMeshAgent[] allAgents =
            Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

        for (int i = 0; i < allAgents.Length; i++)
        {
            NavMeshAgent agent = allAgents[i];

            if (agent == null)
                continue;

            if (!agent.isActiveAndEnabled)
                continue;

            if (!agent.isOnNavMesh)
                continue;

            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
    }

    public void ReturnToLobby()
    {
        Time.timeScale = 1.0f;

        ResolveReferences();

        if (stageMapManager != null)
            stageMapManager.ResetToFirstStageSelection();

        SceneManager.LoadScene(lobbySceneName);
    }

    public void RestartGame()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ToggleTargetDebugReveal()
    {
        IsTargetDebugRevealEnabled = !IsTargetDebugRevealEnabled;
    }

    public void EnableTargetDebugReveal()
    {
        IsTargetDebugRevealEnabled = true;
    }

    public void DisableTargetDebugReveal()
    {
        IsTargetDebugRevealEnabled = false;
    }

    public void SetTargetDebugReveal(bool enabled)
    {
        IsTargetDebugRevealEnabled = enabled;
    }
}