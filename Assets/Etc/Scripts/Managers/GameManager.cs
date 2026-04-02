using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI 설정")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winMessageText;

    [Header("Scene 이동")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private bool autoReturnToLobbyOnWin = true;
    [SerializeField] private float returnDelaySeconds = 2.0f;

    [Header("연출")]
    [SerializeField] private float winTimeScale = 0.5f;

    private bool stageCleared = false;

    private void Awake()
    {
        Time.timeScale = 1.0f;

        if (winPanel != null)
            winPanel.SetActive(false);
    }

    private void OnEnable()
    {
        CatchZone.OnTargetCaught += HandleWin;
    }

    private void OnDisable()
    {
        CatchZone.OnTargetCaught -= HandleWin;
    }

    private void HandleWin(GameObject target)
    {
        if (stageCleared)
            return;

        stageCleared = true;

        Debug.Log("<color=green>축하합니다! 모든 타겟을 체포했습니다.</color>");

        UnlockNextStage();
        StopAllMovingObjects();
        ShowWinUI(target);

        Time.timeScale = winTimeScale;

        if (autoReturnToLobbyOnWin)
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

    private void ShowWinUI(GameObject target)
    {
        if (winPanel == null)
            return;

        winPanel.SetActive(true);

        if (winMessageText != null)
            winMessageText.text = $"축하합니다! {target.name}을(를) 체포했습니다!";
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnDelaySeconds);
        ReturnToLobby();
    }

    private void StopAllMovingObjects()
    {
        NavMeshAgent[] allAgents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

        foreach (NavMeshAgent agent in allAgents)
        {
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
}