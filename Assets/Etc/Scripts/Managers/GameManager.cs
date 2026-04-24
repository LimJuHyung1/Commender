using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private UIController uiController;
    [SerializeField] private StageMapManager stageMapManager;

    [Header("Stage Settings")]
    [SerializeField] private string missionDescription = "제한 시간 안에 타겟을 체포하세요.";
    [SerializeField] private float timeLimitSeconds = 300f;

    [Header("Scene 이동")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private bool autoLoadNextStageOnWin = true;
    [SerializeField] private bool autoReturnToLobbyOnFinalWin = true;
    [SerializeField] private bool autoReturnToLobbyOnFail = false;
    [SerializeField] private float returnDelaySeconds = 2.0f;

    [Header("시간 배속")]
    [SerializeField] private float winTimeScale = 0.5f;
    [SerializeField] private float failTimeScale = 0.5f;

    [Header("Debug Target Reveal")]
    [SerializeField] private bool startWithTargetDebugReveal = false;

    private bool stageFinished = false;
    private bool timerRunning = false;
    private float remainingTime = 0f;

    public bool IsTargetDebugRevealEnabled { get; private set; }
    public bool IsStageFinished => stageFinished;

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

    private void OnEnable()
    {
        CatchZone.OnTargetCaught += HandleWin;
    }

    private void OnDisable()
    {
        CatchZone.OnTargetCaught -= HandleWin;
    }

    private void ResolveReferences()
    {
        if (uiController == null)
            uiController = FindFirstObjectByType<UIController>();

        if (stageMapManager == null)
            stageMapManager = FindFirstObjectByType<StageMapManager>();
    }

    private void SetupStage()
    {
        ResolveReferences();

        stageFinished = false;
        timerRunning = true;
        remainingTime = Mathf.Max(0.1f, timeLimitSeconds);

        if (uiController != null)
        {
            uiController.HideResultPanel();
            uiController.SetMissionText(missionDescription);
            uiController.SetTimerText(remainingTime);
            uiController.SetTimerVisible(true);
        }

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

        CaptureSequenceController captureSequenceController = FindFirstObjectByType<CaptureSequenceController>();

        if (captureSequenceController != null)
        {
            stageFinished = true;
            timerRunning = false;

            StartCoroutine(CompleteStageAfterCaptureSequence(captureSequenceController, target, message));
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

        UnlockNextStage();
        StopAllMovingObjects();

        if (uiController != null)
            uiController.ShowResultPanel(true, message);

        Time.timeScale = winTimeScale;

        if (autoLoadNextStageOnWin)
        {
            if (HasNextStage())
                StartCoroutine(LoadNextStageAfterDelay());
            else if (autoReturnToLobbyOnFinalWin)
                StartCoroutine(ReturnToLobbyAfterDelay());
        }
        else if (autoReturnToLobbyOnFinalWin)
        {
            StartCoroutine(ReturnToLobbyAfterDelay());
        }
    }

    public void FailStage(string message)
    {
        if (stageFinished)
            return;

        stageFinished = true;
        timerRunning = false;

        Debug.Log($"<color=red>[GameManager]</color> 스테이지 실패: {message}");

        StopAllMovingObjects();

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

        ResolveReferences();

        if (stageMapManager == null)
        {
            ReturnToLobby();
            yield break;
        }

        stageMapManager.SelectNextStage();

        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);
        ReturnToLobby();
    }

    private void StopAllMovingObjects()
    {
        NavMeshAgent[] allAgents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

        for (int i = 0; i < allAgents.Length; i++)
        {
            NavMeshAgent agent = allAgents[i];
            if (agent == null)
                continue;

            agent.isStopped = true;
            agent.velocity = Vector3.zero;
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