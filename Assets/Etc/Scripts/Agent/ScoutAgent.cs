using UnityEngine;

public class ScoutAgent : AgentController
{
    [Header("참조")]
    [SerializeField] private Flare flarePrefab;
    [SerializeField] private Transform flareParent;
    [SerializeField] private Transform flareShootPoint;

    [Header("신호탄 설정")]
    [SerializeField] private bool replaceExistingFlare = true;
    [SerializeField] private bool flareSingleUse = true;
    [SerializeField] private Vector3 flareShootOffset = new Vector3(0f, 1.5f, 0f);

    [Header("투시 디메리트")]
    [SerializeField, Range(0.1f, 1f)] private float trueSightMoveSpeedMultiplier = 0.75f;

    private Flare currentFlare;
    private bool trueSightEnabled = false;
    private bool hasUsedFlare = false;

    protected override void Awake()
    {
        agentID = 1;
        base.Awake();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Scout {AgentID}] 스킬 요청: {skillName} (위치: {targetPos})");

        if (skill.Contains("flare") || skill.Contains("signalflare"))
        {
            if (!CanUseFlare())
            {
                Debug.LogWarning($"[Scout {AgentID}] 신호탄은 1회용이라 더 이상 사용할 수 없습니다.");
                return;
            }

            ForceStopForSkill();
            DeployFlare(targetPos);
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

    private bool CanUseFlare()
    {
        if (!flareSingleUse)
            return true;

        return !hasUsedFlare;
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

    private void DeployFlare(Vector3 targetPos)
    {
        if (flarePrefab == null)
        {
            Debug.LogWarning($"[Scout {AgentID}] ScoutFlare 프리팹이 연결되지 않았습니다.");
            return;
        }

        Vector3 shootStartPos = flareShootPoint != null
            ? flareShootPoint.position
            : transform.position + flareShootOffset;

        if (replaceExistingFlare && currentFlare != null)
        {
            Destroy(currentFlare.gameObject);
            currentFlare = null;
        }

        Transform parent = flareParent != null ? flareParent : null;
        currentFlare = Instantiate(flarePrefab, shootStartPos, Quaternion.identity, parent);
        currentFlare.Launch(shootStartPos, targetPos);

        hasUsedFlare = true;

        Debug.Log(
            $"<color=yellow>[Scout Skill]</color> Agent {AgentID} : " +
            $"신호탄 발사 시작 = {shootStartPos}, 목표 좌표 = {targetPos}"
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
        ApplyTrueSightMoveSpeedPenalty();

        Debug.Log(
            $"<color=yellow>[Scout Skill]</color> Agent {AgentID} : " +
            $"투시 {(trueSightEnabled ? "활성화" : "비활성화")} / " +
            $"이동속도 {(navAgent != null ? navAgent.speed.ToString("0.##") : "-")}"
        );
    }

    private void ApplyTrueSightMoveSpeedPenalty()
    {
        if (navAgent == null)
            return;

        float baseMoveSpeed = stats != null ? stats.moveSpeed : navAgent.speed;

        if (trueSightEnabled)
            navAgent.speed = baseMoveSpeed * trueSightMoveSpeedMultiplier;
        else
            navAgent.speed = baseMoveSpeed;
    }

    public void ResetFlareUsage()
    {
        hasUsedFlare = false;
    }
}