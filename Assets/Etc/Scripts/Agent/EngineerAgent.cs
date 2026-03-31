using UnityEngine;

public class EngineerAgent : AgentController
{
    [Header("설치 프리팹")]
    [SerializeField] private GameObject barricadePrefab;
    [SerializeField] private GameObject snareTrapPrefab;
    [SerializeField] private Transform deployParent;

    [Header("설치 설정")]
    [SerializeField] private float deployY = 0f;
    [SerializeField] private bool replaceExistingBarricade = true;
    [SerializeField] private bool replaceExistingSlowTrap = false;

    private GameObject currentBarricade;
    private GameObject currentSlowTrap;

    protected override void Awake()
    {
        agentID = 2;
        base.Awake();
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
        else if (skill.Contains("slowtrap") || skill.Contains("trap"))
        {
            ForceStopForSkill();
            DeploySlowTrap(targetPos);
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

        if (replaceExistingBarricade && currentBarricade != null)
        {
            Destroy(currentBarricade);
            currentBarricade = null;
        }

        currentBarricade = Instantiate(
            barricadePrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        Debug.Log($"[Engineer {AgentID}] 바리케이드 설치: {spawnPos}");
    }

    private void DeploySlowTrap(Vector3 targetPos)
    {
        if (snareTrapPrefab == null)
        {
            Debug.LogWarning($"[Engineer {AgentID}] snareTrapPrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(targetPos);

        if (replaceExistingSlowTrap && currentSlowTrap != null)
        {
            Destroy(currentSlowTrap);
            currentSlowTrap = null;
        }

        GameObject spawnedTrap = Instantiate(
            snareTrapPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        if (replaceExistingSlowTrap)
            currentSlowTrap = spawnedTrap;

        Debug.Log($"[Engineer {AgentID}] 감속 함정 설치: {spawnPos}");
    }

    private Vector3 BuildSpawnPosition(Vector3 targetPos)
    {
        Vector3 desired = new Vector3(targetPos.x, targetPos.y + 2f, targetPos.z);

        if (Physics.Raycast(desired, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(hit.point.x, hit.point.y + deployY, hit.point.z);
        }

        return new Vector3(targetPos.x, deployY, targetPos.z);
    }
}