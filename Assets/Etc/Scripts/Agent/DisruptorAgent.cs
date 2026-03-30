using UnityEngine;

public class DisruptorAgent : AgentController
{
    [Header("설치 프리팹")]
    [SerializeField] private GameObject decoySignalPrefab;
    [SerializeField] private GameObject phantomPrefab;
    [SerializeField] private Transform deployParent;

    [Header("설치 설정")]
    [SerializeField] private float deployYOffset = 0f;
    [SerializeField] private bool replaceExistingDecoySignal = true;

    private GameObject currentDecoySignal;
    private GameObject currentPhantom;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Disruptor {AgentID}] 스킬 요청: {skillName} (위치: {targetPos})");

        if (skill.Contains("decoysignal") || skill.Contains("decoy"))
        {
            ForceStopForSkill();
            DeployDecoySignal(targetPos);
        }
        else if (skill.Contains("phantom"))
        {
            ForceStopForSkill();
            DeployPhantom();
        }
        else
        {
            Debug.LogWarning($"[Disruptor {AgentID}] 알 수 없는 스킬: {skillName}");
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

    private void DeployDecoySignal(Vector3 targetPos)
    {
        if (decoySignalPrefab == null)
        {
            Debug.LogWarning($"[Disruptor {AgentID}] decoySignalPrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(targetPos);

        if (replaceExistingDecoySignal && currentDecoySignal != null)
        {
            Destroy(currentDecoySignal);
            currentDecoySignal = null;
        }

        currentDecoySignal = Instantiate(
            decoySignalPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        Debug.Log($"[Disruptor {AgentID}] 유인 신호 설치: {spawnPos}");
    }

    private void DeployPhantom()
    {
        if (phantomPrefab == null)
        {
            Debug.LogWarning($"[Disruptor {AgentID}] phantomPrefab이 연결되지 않았습니다.");
            return;
        }

        if (currentPhantom != null)
        {
            Debug.LogWarning($"[Disruptor {AgentID}] Phantom은 하나만 생성할 수 있습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(transform.position);

        currentPhantom = Instantiate(
            phantomPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        Debug.Log($"[Disruptor {AgentID}] 가짜 위협 생성: {spawnPos}");
    }

    private Vector3 BuildSpawnPosition(Vector3 targetPos)
    {
        Vector3 rayOrigin = new Vector3(targetPos.x, targetPos.y + 2f, targetPos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(hit.point.x, hit.point.y + deployYOffset, hit.point.z);
        }

        return new Vector3(targetPos.x, deployYOffset, targetPos.z);
    }
}