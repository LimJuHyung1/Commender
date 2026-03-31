using UnityEngine;

public class DisruptorAgent : AgentController
{
    [Header("설치 프리팹")]
    [SerializeField] private GameObject noisemakerPrefab;
    [SerializeField] private GameObject hologramPrefab;
    [SerializeField] private Transform deployParent;

    [Header("설치 설정")]
    [SerializeField] private float deployYOffset = 0f;
    [SerializeField] private bool replaceExistingNoisemaker = true;

    private GameObject currentNoisemaker;
    private GameObject currentHologram;

    protected override void Awake()
    {
        agentID = 3;
        base.Awake();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Disruptor {AgentID}] 스킬 요청: {skillName} (위치: {targetPos})");

        if (skill.Contains("noisemaker") || skill.Contains("noise"))
        {
            ForceStopForSkill();
            DeployNoisemaker(targetPos);
        }
        else if (skill.Contains("hologram"))
        {
            ForceStopForSkill();
            DeployHologram();
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

    private void DeployNoisemaker(Vector3 targetPos)
    {
        if (noisemakerPrefab == null)
        {
            Debug.LogWarning($"[Disruptor {AgentID}] noisemakerPrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(targetPos);

        if (replaceExistingNoisemaker && currentNoisemaker != null)
        {
            Destroy(currentNoisemaker);
            currentNoisemaker = null;
        }

        currentNoisemaker = Instantiate(
            noisemakerPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        Debug.Log($"[Disruptor {AgentID}] 소란 장치 설치: {spawnPos}");
    }

    private void DeployHologram()
    {
        if (hologramPrefab == null)
        {
            Debug.LogWarning($"[Disruptor {AgentID}] hologramPrefab이 연결되지 않았습니다.");
            return;
        }

        if (currentHologram != null)
        {
            Debug.LogWarning($"[Disruptor {AgentID}] 홀로그램은 하나만 생성할 수 있습니다.");
            return;
        }

        Vector3 spawnPos = BuildSpawnPosition(transform.position);

        currentHologram = Instantiate(
            hologramPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        Debug.Log($"[Disruptor {AgentID}] 홀로그램 생성: {spawnPos}");
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