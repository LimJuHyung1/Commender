using UnityEngine;

public class ScoutAgent : AgentController
{
    [Header("참조")]
    public Flare flarePrefab;
    public Transform flareParent;
    public Transform flareShootPoint;

    [Header("신호탄 설정")]
    public bool replaceExistingFlare = true;
    public bool flareSingleUse = true;
    public Vector3 flareShootOffset = new Vector3(0f, 1.5f, 0f);

    [Header("위치 공유 설정")]
    public bool targetPositionShareEnabled = true;
    public bool includeSelfInTargetPositionShare = false;

    private Flare currentFlare;
    private bool hasUsedFlare = false;
    private bool isTargetPositionSharing = false;
    private AgentController[] cachedAgents;

    public bool IsTargetPositionShareEnabled => targetPositionShareEnabled;
    public bool IsTargetPositionSharing => isTargetPositionSharing;

    protected override void Awake()
    {
        agentID = 1;
        base.Awake();
    }

    private void Start()
    {
        RefreshCachedAgents();
    }

    protected override void OnDisable()
    {
        ClearSharedTargetPositionFromThisScout();
        isTargetPositionSharing = false;
        base.OnDisable();
    }

    protected override void Update()
    {
        base.Update();
        UpdateTargetPositionShare();
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
            return;
        }

        if (IsTargetPositionShareSkill(skill))
        {
            bool enable = !IsTargetPositionShareOffCommand(skill);
            SetTargetPositionShareEnabled(enable);
            return;
        }

        Debug.LogWarning($"[Scout {AgentID}] 알 수 없는 스킬: {skillName}");
    }

    public void SetTargetPositionShareEnabled(bool enabled)
    {
        if (targetPositionShareEnabled == enabled)
        {
            Debug.Log($"[Scout {AgentID}] 위치 공유는 이미 {(enabled ? "켜짐" : "꺼짐")} 상태입니다.");
            return;
        }

        targetPositionShareEnabled = enabled;

        if (!targetPositionShareEnabled)
        {
            isTargetPositionSharing = false;
            ClearSharedTargetPositionFromThisScout();
        }

        Debug.Log($"[Scout {AgentID}] 위치 공유 {(targetPositionShareEnabled ? "켜짐" : "꺼짐")}");
    }

    public void ToggleTargetPositionShare()
    {
        SetTargetPositionShareEnabled(!targetPositionShareEnabled);
    }

    public void ResetFlareUsage()
    {
        hasUsedFlare = false;
    }

    private void UpdateTargetPositionShare()
    {
        isTargetPositionSharing = false;

        if (!targetPositionShareEnabled)
            return;

        if (visionSensor == null)
            return;

        if (!visionSensor.IsSeeingTarget)
            return;

        Transform seenTarget = visionSensor.CurrentSeenTarget;
        if (seenTarget == null)
            return;

        ShareTargetPosition(seenTarget.position);
        isTargetPositionSharing = true;
    }

    private void ShareTargetPosition(Vector3 targetPosition)
    {
        if (cachedAgents == null || cachedAgents.Length == 0 || HasInvalidCachedAgent())
            RefreshCachedAgents();

        if (cachedAgents == null)
            return;

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            AgentController agent = cachedAgents[i];

            if (agent == null)
                continue;

            if (!includeSelfInTargetPositionShare && agent == this)
                continue;

            agent.ReceiveSharedTargetPosition(targetPosition, this);
        }
    }

    private void ClearSharedTargetPositionFromThisScout()
    {
        if (cachedAgents == null || cachedAgents.Length == 0 || HasInvalidCachedAgent())
            RefreshCachedAgents();

        if (cachedAgents == null)
            return;

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            AgentController agent = cachedAgents[i];

            if (agent == null)
                continue;

            agent.ClearSharedTargetPosition(this);
        }
    }

    private void RefreshCachedAgents()
    {
        cachedAgents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
    }

    private bool HasInvalidCachedAgent()
    {
        if (cachedAgents == null)
            return true;

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            if (cachedAgents[i] == null)
                return true;
        }

        return false;
    }

    private bool IsTargetPositionShareSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("positionshare") ||
               skill.Contains("position share") ||
               skill.Contains("target position share") ||
               skill.Contains("위치공유") ||
               skill.Contains("위치 공유");
    }

    private bool IsTargetPositionShareOffCommand(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("_off") ||
               skill.Contains("off") ||
               skill.Contains("꺼") ||
               skill.Contains("끄") ||
               skill.Contains("중지") ||
               skill.Contains("비활성") ||
               skill.Contains("하지마") ||
               skill.Contains("하지 마");
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
            $"[Scout Skill] Agent {AgentID} : " +
            $"신호탄 발사 시작 = {shootStartPos}, 목표 좌표 = {targetPos}"
        );
    }
}