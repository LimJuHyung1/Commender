using UnityEngine;
using UnityEngine.AI;

public class EngineerAgent : AgentController
{
    [Header("설치 프리팹")]
    [SerializeField] private GameObject barricadePrefab;
    [SerializeField] private GameObject trapPrefab;
    [SerializeField] private Transform deployParent;

    [Header("설치 설정")]
    [SerializeField] private float deployY = 0f;
    [SerializeField] private float placementNavMeshSampleRadius = 2f;
    [SerializeField] private float groundProbeHeight = 4f;
    [SerializeField] private float groundProbeDistance = 20f;
    [SerializeField] private LayerMask placementGroundLayer;
    [SerializeField] private bool replaceExistingBarricade = true;
    [SerializeField] private bool replaceExistingTrap = false;

    [Header("함정 사용 횟수")]
    [SerializeField][Min(0)] private int trapMaxUses = 3;

    private GameObject currentBarricade;
    private GameObject currentTrap;
    private int remainingTrapUses;

    public int RemainingTrapUses => remainingTrapUses;

    protected override void Awake()
    {
        agentID = 2;
        base.Awake();

        remainingTrapUses = Mathf.Max(0, trapMaxUses);
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Engineer {AgentID}] 스킬 요청: {skillName} (위치: {targetPos})");

        if (skill.Contains("barricade"))
        {
            ForceStopForSkill();
            DeployBarricade(targetPos);
        }
        else if (
            skill.Contains("slowtrap") ||
            skill.Contains("trap") ||
            skill.Contains("트랩") ||
            skill.Contains("함정"))
        {
            ForceStopForSkill();
            DeployTrap(targetPos);
        }
        else
        {
            Debug.LogWarning($"[Engineer {AgentID}] 알 수 없는 스킬: {skillName}");
        }
    }

    private void ForceStopForSkill()
    {
        currentTarget = null;
        isManualMoving = false;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }
    }

    private void DeployBarricade(Vector3 targetPos)
    {
        if (barricadePrefab == null)
        {
            Debug.LogWarning($"[Engineer {AgentID}] barricadePrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(targetPos);
        Quaternion spawnRotation = BuildBarricadeRotation();

        if (replaceExistingBarricade && currentBarricade != null)
        {
            Destroy(currentBarricade);
            currentBarricade = null;
        }

        GameObject spawnedBarricade = Instantiate(
            barricadePrefab,
            Vector3.zero,
            spawnRotation,
            deployParent != null ? deployParent : null
        );

        BarricadeObject barricade = spawnedBarricade.GetComponent<BarricadeObject>();
        if (barricade != null)
            barricade.Deploy(spawnPos, spawnRotation);
        else
            spawnedBarricade.transform.SetPositionAndRotation(spawnPos, spawnRotation);

        currentBarricade = spawnedBarricade;

        Debug.Log($"[Engineer {AgentID}] 바리케이드 설치: {spawnPos}");
    }

    private void DeployTrap(Vector3 targetPos)
    {
        if (trapPrefab == null)
        {
            Debug.LogWarning($"[Engineer {AgentID}] trapPrefab이 연결되지 않았습니다.");
            return;
        }

        if (remainingTrapUses <= 0)
        {
            Debug.LogWarning($"[Engineer {AgentID}] 감속 함정 사용 가능 횟수가 없습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(targetPos);

        if (replaceExistingTrap && currentTrap != null)
        {
            Destroy(currentTrap);
            currentTrap = null;
        }

        GameObject spawnedTrap = Instantiate(
            trapPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        if (replaceExistingTrap)
            currentTrap = spawnedTrap;

        remainingTrapUses--;

        Debug.Log($"[Engineer {AgentID}] 감속 함정 설치: {spawnPos} | 남은 횟수: {remainingTrapUses}");
    }

    private Vector3 BuildSpawnPosition(Vector3 targetPos)
    {
        Vector3 desiredPosition = targetPos;

        if (NavMesh.SamplePosition(
                targetPos,
                out NavMeshHit navHit,
                placementNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            desiredPosition = navHit.position;
        }

        Vector3 rayOrigin = desiredPosition + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + groundProbeDistance;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                placementGroundLayer,
                QueryTriggerInteraction.Ignore))
        {
            return new Vector3(hit.point.x, hit.point.y + deployY, hit.point.z);
        }

        return new Vector3(
            desiredPosition.x,
            desiredPosition.y + deployY,
            desiredPosition.z
        );
    }

    private Quaternion BuildBarricadeRotation()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    public void ResetSlowTrapUses()
    {
        remainingTrapUses = Mathf.Max(0, trapMaxUses);
        Debug.Log($"[Engineer {AgentID}] 감속 함정 사용 횟수 초기화: {remainingTrapUses}");
    }
}