using UnityEngine;

public class ScoutAgent : AgentController
{
    [Header("참조")]
    [SerializeField] private VisionSensor visionSensor;
    [SerializeField] private ReconDrone reconDronePrefab;
    [SerializeField] private Transform reconDroneParent;

    [Header("정찰 드론 설정")]
    [SerializeField] private bool replaceExistingReconDrone = true;
    [SerializeField] private float reconDroneYOffset = 5f;

    private ReconDrone currentReconDrone;
    private bool trueSightEnabled = false;

    protected override void Awake()
    {
        agentID = 1;
        base.Awake();

        if (visionSensor == null)
            visionSensor = GetComponentInChildren<VisionSensor>();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Scout {AgentID}] 스킬 요청: {skillName} (위치: {targetPos})");

        if (skill.Contains("recondrone") || skill.Contains("reveal") || skill.Contains("recon"))
        {
            ForceStopForSkill();
            DeployReconDrone(targetPos);
        }
        else if (skill.Contains("truesight") || skill.Contains("wallsight"))
        {
            ForceStopForSkill();
            ToggleTrueSight();
        }
        else
        {
            Debug.LogWarning($"[Scout {AgentID}] 알 수 없는 스킬: {skillName}");
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

    private void DeployReconDrone(Vector3 targetPos)
    {
        if (reconDronePrefab == null)
        {
            Debug.LogWarning($"[Scout {AgentID}] ReconDrone 프리팹이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = new Vector3(
            targetPos.x,
            targetPos.y + reconDroneYOffset,
            targetPos.z
        );

        if (replaceExistingReconDrone && currentReconDrone != null)
        {
            Destroy(currentReconDrone.gameObject);
            currentReconDrone = null;
        }

        Transform parent = reconDroneParent != null ? reconDroneParent : null;
        currentReconDrone = Instantiate(reconDronePrefab, spawnPos, Quaternion.identity, parent);

        Debug.Log(
            $"<color=yellow>[Scout Skill]</color> Agent {AgentID} : " +
            $"정찰 드론 생성 위치 = {spawnPos}"
        );
    }

    private void ToggleTrueSight()
    {
        if (visionSensor == null)
        {
            Debug.LogWarning($"[Scout {AgentID}] VisionSensor 참조가 없습니다.");
            return;
        }

        trueSightEnabled = !trueSightEnabled;
        visionSensor.SetWallSightEnabled(trueSightEnabled);

        Debug.Log(
            $"<color=yellow>[Scout Skill]</color> Agent {AgentID} : " +
            $"투시 {(trueSightEnabled ? "활성화" : "비활성화")}"
        );
    }
}