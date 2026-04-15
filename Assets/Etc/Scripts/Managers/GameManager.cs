using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class StageRule
    {
        public string stageName = "Stage";
        public string missionDescription = "타겟을 체포하세요.";
        public bool useTimeLimit = false;
        public float timeLimitSeconds = 300f;
        public bool clearOnTargetCaught = true;
    }

    public static GameManager Instance { get; private set; }

    [Header("Stage Rules")]
    [SerializeField] private StageRule[] stageRules;

    [Header("UI 설정")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultTitleText;
    [SerializeField] private Text resultMessageText;
    [SerializeField] private Text missionText;
    [SerializeField] private Text timerText;

    [Header("Scene 이동")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private bool autoReturnToLobbyOnWin = true;
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
    private int currentStageIndex = 0;
    private StageRule currentRule;

    private const string SelectedStageKey = "SelectedStageIndex";

    public bool IsTargetDebugRevealEnabled { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Time.timeScale = 1.0f;
        IsTargetDebugRevealEnabled = startWithTargetDebugReveal;

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        SetupCurrentStageRule();
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

    private void SetupCurrentStageRule()
    {
        currentStageIndex = GetCurrentStageIndex();

        string currentStageDisplayName = GetCurrentStageDisplayName();
        currentRule = BuildRuntimeStageRule(currentStageIndex, currentStageDisplayName);

        if (missionText != null)
            missionText.text = $"[{currentRule.stageName}] {currentRule.missionDescription}";

        if (currentRule.useTimeLimit)
        {
            remainingTime = Mathf.Max(0.1f, currentRule.timeLimitSeconds);
            timerRunning = true;

            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
                RefreshTimerText();
            }
        }
        else
        {
            timerRunning = false;

            if (timerText != null)
                timerText.gameObject.SetActive(false);
        }

        Debug.Log($"[GameManager] {currentRule.stageName} 규칙 적용: " +
                  $"useTimeLimit={currentRule.useTimeLimit}, " +
                  $"timeLimit={currentRule.timeLimitSeconds}, " +
                  $"clearOnTargetCaught={currentRule.clearOnTargetCaught}");
    }

    private string GetCurrentStageDisplayName()
    {
        StageMapManager stageMapManager = FindFirstObjectByType<StageMapManager>();
        if (stageMapManager != null)
            return stageMapManager.CurrentStageDisplayName;

        int selectedStageIndex = PlayerPrefs.GetInt(SelectedStageKey, 0);
        return $"Stage {selectedStageIndex + 1}";
    }

    private StageRule BuildRuntimeStageRule(int stageIndex, string stageDisplayName)
    {
        StageRule sourceRule = GetStageRule(stageIndex);

        StageRule runtimeRule = new StageRule();
        runtimeRule.stageName = stageDisplayName;
        runtimeRule.missionDescription = sourceRule.missionDescription;
        runtimeRule.useTimeLimit = sourceRule.useTimeLimit;
        runtimeRule.timeLimitSeconds = sourceRule.timeLimitSeconds;
        runtimeRule.clearOnTargetCaught = sourceRule.clearOnTargetCaught;

        return runtimeRule;
    }

    private int GetCurrentStageIndex()
    {
        StageMapManager stageMapManager = FindFirstObjectByType<StageMapManager>();
        if (stageMapManager != null)
            return stageMapManager.CurrentStageIndex;

        return PlayerPrefs.GetInt(SelectedStageKey, 0);
    }

    private StageRule GetStageRule(int stageIndex)
    {
        if (stageRules != null &&
            stageIndex >= 0 &&
            stageIndex < stageRules.Length &&
            stageRules[stageIndex] != null)
        {
            return stageRules[stageIndex];
        }

        StageRule fallbackRule = new StageRule();
        fallbackRule.stageName = $"Stage {stageIndex + 1}";
        fallbackRule.missionDescription = "타겟을 체포하세요.";
        fallbackRule.useTimeLimit = false;
        fallbackRule.timeLimitSeconds = 60f;
        fallbackRule.clearOnTargetCaught = true;
        return fallbackRule;
    }

    private void UpdateStageTimer()
    {
        if (stageFinished || !timerRunning)
            return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            RefreshTimerText();
            FailStage("제한 시간 안에 타겟을 체포하지 못했습니다.");
            return;
        }

        RefreshTimerText();
    }

    private void RefreshTimerText()
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt(remainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void HandleWin(GameObject target)
    {
        if (stageFinished)
            return;

        if (currentRule != null && !currentRule.clearOnTargetCaught)
        {
            Debug.Log("[GameManager] 현재 스테이지는 타겟 체포만으로 클리어되지 않습니다.");
            return;
        }

        string message = target != null
            ? $"축하합니다! {target.name}을(를) 체포했습니다!"
            : "축하합니다! 타겟을 체포했습니다!";

        CompleteStage(message);
    }

    public void CompleteStage(string message)
    {
        if (stageFinished)
            return;

        stageFinished = true;
        timerRunning = false;

        Debug.Log($"<color=green>[GameManager]</color> 스테이지 클리어: {message}");

        UnlockNextStage();
        StopAllMovingObjects();
        ShowResultUI(true, message);

        Time.timeScale = winTimeScale;

        if (autoReturnToLobbyOnWin)
            StartCoroutine(ReturnToLobbyAfterDelay());
    }

    public void FailStage(string message)
    {
        if (stageFinished)
            return;

        stageFinished = true;
        timerRunning = false;

        Debug.Log($"<color=red>[GameManager]</color> 스테이지 실패: {message}");

        StopAllMovingObjects();
        ShowResultUI(false, message);

        Time.timeScale = failTimeScale;

        if (autoReturnToLobbyOnFail)
            StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private void UnlockNextStage()
    {
        StageMapManager stageMapManager = FindFirstObjectByType<StageMapManager>();
        if (stageMapManager == null)
        {
            Debug.LogWarning("[GameManager] StageMapManager를 찾지 못해서 다음 스테이지를 해금하지 못했습니다.");
            return;
        }

        stageMapManager.CompleteStage();
    }

    private void ShowResultUI(bool isSuccess, string message)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultTitleText != null)
            resultTitleText.text = isSuccess ? "성공" : "실패";

        if (resultMessageText != null)
            resultMessageText.text = message;
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