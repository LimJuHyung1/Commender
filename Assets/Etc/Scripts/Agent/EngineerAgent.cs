using UnityEngine;
using UnityEngine.AI;

public class EngineerAgent : AgentController
{
    [Header("설치 프리팹")]
    [SerializeField] private GameObject barricadePrefab;
    [SerializeField] private GameObject trapPrefab;
    [SerializeField] private Transform deployParent;

    [Header("타겟 참조")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private bool autoFindTargetIfMissing = true;

    [Header("설치 설정")]
    [SerializeField] private float deployY = 0f;
    [SerializeField] private float placementNavMeshSampleRadius = 2f;
    [SerializeField] private float groundProbeHeight = 4f;
    [SerializeField] private float groundProbeDistance = 20f;
    [SerializeField] private LayerMask placementGroundLayer;
    [SerializeField] private bool replaceExistingBarricade = true;
    [SerializeField] private bool replaceExistingTrap = false;

    [Header("설치 회전 보정")]
    [SerializeField] private float barricadeYawOffset = 0f;
    [SerializeField] private float trapYawOffset = 0f;

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
        CacheTargetTransform();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Engineer {AgentID}] 스킬 요청: {skillName} 위치: {targetPos}");

        if (skill.Contains("barricade") || skill.Contains("바리케이드"))
        {
            if (barricadePrefab == null)
            {
                Debug.LogWarning($"[Engineer {AgentID}] barricadePrefab이 연결되지 않았습니다.");
                return;
            }

            if (!TryConsumeSkillGaugeForSkill("barricade"))
                return;

            ForceStopForSkill();
            DeployBarricade(targetPos);
        }
        else if (
            skill.Contains("slowtrap") ||
            skill.Contains("slow trap") ||
            skill.Contains("trap") ||
            skill.Contains("트랩") ||
            skill.Contains("함정"))
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

            if (!TryConsumeSkillGaugeForSkill("slowtrap"))
                return;

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
        Vector3 spawnPos = BuildSpawnPosition(targetPos);
        Quaternion spawnRotation = BuildPlacementRotationTowardTarget(spawnPos, barricadeYawOffset);

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

        Debug.Log($"[Engineer {AgentID}] 바리케이드 설치: {spawnPos}, 회전: {spawnRotation.eulerAngles}");
    }

    private void DeployTrap(Vector3 targetPos)
    {
        Vector3 spawnPos = BuildSpawnPosition(targetPos);
        Quaternion spawnRotation = BuildPlacementRotationTowardTarget(spawnPos, trapYawOffset);

        if (replaceExistingTrap && currentTrap != null)
        {
            Destroy(currentTrap);
            currentTrap = null;
        }

        GameObject spawnedTrap = Instantiate(
            trapPrefab,
            spawnPos,
            spawnRotation,
            deployParent != null ? deployParent : null
        );

        if (replaceExistingTrap)
            currentTrap = spawnedTrap;

        remainingTrapUses--;

        Debug.Log($"[Engineer {AgentID}] 감속 함정 설치: {spawnPos}, 회전: {spawnRotation.eulerAngles} | 남은 횟수: {remainingTrapUses}");
    }

    private Quaternion BuildPlacementRotationTowardTarget(Vector3 spawnPos, float yawOffset)
    {
        CacheTargetTransform();

        Vector3 direction;

        if (targetTransform != null)
            direction = targetTransform.position - spawnPos;
        else
            direction = transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        Quaternion baseRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion offsetRotation = Quaternion.Euler(0f, yawOffset, 0f);

        return baseRotation * offsetRotation;
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

    private void CacheTargetTransform()
    {
        if (targetTransform != null)
            return;

        if (!autoFindTargetIfMissing)
            return;

        TargetController foundTarget = FindFirstObjectByType<TargetController>();
        if (foundTarget != null)
            targetTransform = foundTarget.transform;
    }

    private Quaternion BuildPlacementRotation(Vector3 spawnPos, float yawOffset)
    {
        CacheTargetTransform();

        Vector3 direction;

        if (targetTransform != null)
        {
            direction = targetTransform.position - transform.position;
        }
        else
        {
            direction = spawnPos - transform.position;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        Quaternion baseRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion offsetRotation = Quaternion.Euler(0f, yawOffset, 0f);

        return baseRotation * offsetRotation;
    }

    public void ResetSlowTrapUses()
    {
        remainingTrapUses = Mathf.Max(0, trapMaxUses);
        Debug.Log($"[Engineer {AgentID}] 감속 함정 사용 횟수 초기화: {remainingTrapUses}");
    }
}