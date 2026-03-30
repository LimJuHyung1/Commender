using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI 설정")]
    [SerializeField] private GameObject winPanel; // 승리 알림 UI 패널
    [SerializeField] private Text winMessageText;

    private void Awake()
    {
        if (winPanel != null) winPanel.SetActive(false);
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
        Debug.Log("<color=green>축하합니다! 모든 타겟을 체포했습니다.</color>");

        // 1. 모든 에이전트 및 타겟 멈추기 (최적화된 메서드 호출)
        StopAllMovingObjects();

        // 2. 승리 UI 창 띄우기
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            winMessageText.text = $"축하합니다! {target.name}을(를) 체포했습니다!";
        }

        // 3. 연출: 슬로우 모션
        Time.timeScale = 0.5f;
    }

    private void StopAllMovingObjects()
    {
        // [수정됨] FindObjectsOfType 대신 더 빠르고 최신 방식인 FindObjectsByType 사용
        // FindObjectsSortMode.None을 사용하여 불필요한 정렬 연산을 생략하고 성능을 높입니다.
        NavMeshAgent[] allAgents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

        foreach (NavMeshAgent agent in allAgents)
        {
            if (agent != null)
            {
                agent.isStopped = true; // 이동 중지
                agent.velocity = Vector3.zero; // 물리적 관성 제거
            }
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1.0f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}