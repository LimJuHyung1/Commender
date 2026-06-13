using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DogHandler : AgentController
{
    private const string SkillDogDeploy = "dogdeploy";
    private const string SkillGuardInstinct = "guardinstinct";
    private const string SkillTreat = "treat";
    private const string SkillOffLeash = "offleash";

    [Header("Detection Dog")]
    [SerializeField] private DetectionDogController detectionDog;
    [SerializeField] private DetectionDogController detectionDogPrefab;
    [SerializeField] private Transform dogSpawnPoint;
    [SerializeField] private Transform dogFollowTarget;
    [SerializeField] private bool autoFindDogInChildren = true;
    [SerializeField] private bool spawnDogOnAwake = true;
    [SerializeField] private float dogSpawnNavMeshSampleRadius = 2f;

    [Header("Target Visibility")]
    [SerializeField] private bool registerDogVisionToTargetVisibility = true;

    [Header("Unlocked Skills")]
    [SerializeField] private bool treatUnlocked = false;
    [SerializeField] private bool offLeashUnlocked = false;

    [Header("Treat")]
    [SerializeField] private float treatGaugeRequirement = 75f;
    [SerializeField] private float treatDuration = 20f;
    [SerializeField] private float treatMoveSpeedMultiplier = 1.5f;
    [SerializeField] private float treatViewRadiusMultiplier = 1.5f;
    [SerializeField] private float treatViewAngleOffset = 20f;

    [Header("Off Leash")]
    [SerializeField] private float offLeashGaugeRequirement = 100f;
    [SerializeField] private float offLeashDuration = 30f;

    [Header("Guard Instinct")]
    [SerializeField] private int guardInstinctMaxStack = 5;
    [SerializeField] private float guardInstinctSpeedBonusPerStack = 0.05f;
    [SerializeField] private bool resetGuardInstinctOnSkillGaugeReset = true;

    [Header("Target Report")]
    [SerializeField] private bool includeSelfInDogReport = true;
    [SerializeField] private float dogReportCooldown = 0.5f;
    [SerializeField] private float sharedTargetMoveSpeedMultiplier = 1f;
    [SerializeField] private float agentCacheRefreshInterval = 0.5f;
    [SerializeField] private bool debugSharedTargetReceivers = false;

    [Header("Skill Gauge")]
    [SerializeField] private float skillGaugeChargeBlockSecondsOnUse = 0.05f;

    private AgentController[] cachedAgents;
    private float lastAgentCacheRefreshTime = -999f;

    private int guardInstinctStack;
    private float lastDogReportTime = -999f;

    private bool ownsSpawnedDetectionDog;
    private TargetVisibilityController registeredTargetVisibility;

    public bool IsTreatUnlocked => treatUnlocked;
    public bool IsOffLeashUnlocked => offLeashUnlocked;
    public bool HasDetectionDog => detectionDog != null;
    public bool IsDogOffLeash => detectionDog != null && detectionDog.IsOffLeash;
    public int GuardInstinctStack => guardInstinctStack;
    public int GuardInstinctMaxStack => guardInstinctMaxStack;

    public float GuardInstinctMoveSpeedMultiplier
    {
        get
        {
            int stack = Mathf.Clamp(guardInstinctStack, 0, guardInstinctMaxStack);
            return 1f + stack * Mathf.Max(0f, guardInstinctSpeedBonusPerStack);
        }
    }

    protected override void Awake()
    {
        agentID = 5;

        base.Awake();

        ApplyDogHandlerStats();

        if (animator != null)
            animator.applyRootMotion = false;

        EnsureDetectionDog();
        InitializeDetectionDog();
        ApplyGuardInstinctToDog();
        UpdateAnimationState(true);
    }

    protected override void OnDisable()
    {
        StopDogActions(false);
        ClearSharedTargetPositionFromThisHandler();

        cachedAgents = null;
        lastAgentCacheRefreshTime = -999f;
        lastDogReportTime = -999f;

        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (!ownsSpawnedDetectionDog)
            return;

        if (detectionDog == null)
            return;

        Destroy(detectionDog.gameObject);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        treatGaugeRequirement = Mathf.Max(0f, treatGaugeRequirement);
        treatDuration = Mathf.Max(0f, treatDuration);
        treatMoveSpeedMultiplier = Mathf.Max(1f, treatMoveSpeedMultiplier);
        treatViewRadiusMultiplier = Mathf.Max(1f, treatViewRadiusMultiplier);
        treatViewAngleOffset = Mathf.Clamp(treatViewAngleOffset, -359f, 359f);

        offLeashGaugeRequirement = Mathf.Max(0f, offLeashGaugeRequirement);
        offLeashDuration = Mathf.Max(0f, offLeashDuration);

        guardInstinctMaxStack = Mathf.Max(0, guardInstinctMaxStack);
        guardInstinctSpeedBonusPerStack = Mathf.Max(0f, guardInstinctSpeedBonusPerStack);

        dogReportCooldown = Mathf.Max(0f, dogReportCooldown);
        sharedTargetMoveSpeedMultiplier = Mathf.Max(0.01f, sharedTargetMoveSpeedMultiplier);
        agentCacheRefreshInterval = Mathf.Max(0.05f, agentCacheRefreshInterval);
        skillGaugeChargeBlockSecondsOnUse = Mathf.Max(0f, skillGaugeChargeBlockSecondsOnUse);
        dogSpawnNavMeshSampleRadius = Mathf.Max(0.1f, dogSpawnNavMeshSampleRadius);

        guardInstinctStack = Mathf.Clamp(guardInstinctStack, 0, guardInstinctMaxStack);
    }

    protected override void ApplyNavAgentStats()
    {
        base.ApplyNavAgentStats();
        ApplyGuardInstinctToHandler();
    }

    private void ApplyDogHandlerStats()
    {
        if (stats == null)
            return;

        treatGaugeRequirement = stats.treatSkillGaugeMax;
        treatDuration = stats.treatDuration;
        treatMoveSpeedMultiplier = stats.treatMoveSpeedMultiplier;
        treatViewRadiusMultiplier = stats.treatViewRadiusMultiplier;
        treatViewAngleOffset = stats.treatViewAngleBonus;

        offLeashGaugeRequirement = stats.offLeashSkillGaugeMax;
        offLeashDuration = stats.offLeashDuration;

        guardInstinctMaxStack = stats.dogGuardInstinctMaxStack;
        guardInstinctSpeedBonusPerStack = stats.dogGuardInstinctSpeedBonusPerStack;
        resetGuardInstinctOnSkillGaugeReset = stats.resetDogGuardInstinctOnSkillGaugeReset;

        dogReportCooldown = stats.dogReportCooldown;
        sharedTargetMoveSpeedMultiplier = stats.dogSharedTargetMoveSpeedMultiplier;
        includeSelfInDogReport = stats.includeDogHandlerInDogReport;

        guardInstinctStack = Mathf.Clamp(guardInstinctStack, 0, guardInstinctMaxStack);
    }

    public override void ReapplyStats()
    {
        base.ReapplyStats();

        ApplyDogHandlerStats();

        EnsureDetectionDog();
        InitializeDetectionDog();
        ApplyGuardInstinctToHandler();
        ApplyGuardInstinctToDog();
    }

    public override void ResetSkillGauge()
    {
        base.ResetSkillGauge();

        if (resetGuardInstinctOnSkillGaugeReset)
            SetGuardInstinctStack(0);
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        return new[]
        {
            SkillTreat,
            SkillOffLeash
        };
    }

    public override float GetSkillGaugeMaxForSkill(string skillName)
    {
        if (IsDogDeploySkill(skillName))
            return 0f;

        if (IsGuardInstinctSkill(skillName))
            return 0f;

        if (IsTreatSkill(skillName))
            return Mathf.Max(0f, treatGaugeRequirement);

        if (IsOffLeashSkill(skillName))
            return Mathf.Max(0f, offLeashGaugeRequirement);

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeRequiredForSkill(string skillName)
    {
        if (IsDogDeploySkill(skillName))
            return 0f;

        if (IsGuardInstinctSkill(skillName))
            return 0f;

        if (IsTreatSkill(skillName))
            return Mathf.Max(0f, treatGaugeRequirement);

        if (IsOffLeashSkill(skillName))
            return Mathf.Max(0f, offLeashGaugeRequirement);

        return base.GetSkillGaugeRequiredForSkill(skillName);
    }

    public override float GetSkillGaugeCurrentForSkill(string skillName)
    {
        return base.GetSkillGaugeCurrentForSkill(GetCanonicalGaugeSkillName(skillName));
    }

    public override float GetSkillGaugeNormalizedForSkill(string skillName)
    {
        return base.GetSkillGaugeNormalizedForSkill(GetCanonicalGaugeSkillName(skillName));
    }

    public override bool CanUseSkillGaugeForSkill(string skillName, bool showWarning = false)
    {
        if (IsDogDeploySkill(skillName))
            return true;

        if (IsGuardInstinctSkill(skillName))
            return true;

        if (IsTreatSkill(skillName))
        {
            if (!treatUnlocked)
            {
                if (showWarning)
                    Debug.LogWarning($"[DogHandler {AgentID}] 간식 스킬이 아직 해금되지 않았습니다.");

                return false;
            }

            return base.CanUseSkillGaugeForSkill(SkillTreat, showWarning);
        }

        if (IsOffLeashSkill(skillName))
        {
            if (!offLeashUnlocked)
            {
                if (showWarning)
                    Debug.LogWarning($"[DogHandler {AgentID}] 오프리쉬 스킬이 아직 해금되지 않았습니다.");

                return false;
            }

            return base.CanUseSkillGaugeForSkill(SkillOffLeash, showWarning);
        }

        return base.CanUseSkillGaugeForSkill(skillName, showWarning);
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (!CanReceivePlayerSkillCommand(true))
            return;

        EnsureDetectionDog();
        InitializeDetectionDog();

        if (IsDogDeploySkill(skillName))
        {
            ExecuteDogDeploy(targetPos);
            return;
        }

        if (IsGuardInstinctSkill(skillName))
        {
            Debug.LogWarning($"[DogHandler {AgentID}] 경계 본능은 패시브 스킬이므로 직접 사용할 수 없습니다.");
            return;
        }

        if (IsTreatSkill(skillName))
        {
            ExecuteTreat();
            return;
        }

        if (IsOffLeashSkill(skillName))
        {
            ExecuteOffLeash();
            return;
        }

        Debug.LogWarning($"[DogHandler {AgentID}] 알 수 없는 스킬입니다: {skillName}");
    }

    public override void ReceiveSharedTargetPosition(
        Vector3 position,
        AgentController reporter,
        float moveSpeedMultiplier)
    {
        float finalMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier) * GuardInstinctMoveSpeedMultiplier;
        base.ReceiveSharedTargetPosition(position, reporter, finalMultiplier);
    }

    public override void StopAllMovementForStageResult()
    {
        StopDogActions(true);
        ClearSharedTargetPositionFromThisHandler();
        base.StopAllMovementForStageResult();
    }

    public void UnlockTreat()
    {
        treatUnlocked = true;
        Debug.Log($"[DogHandler {AgentID}] 간식 스킬 해금");
    }

    public void UnlockOffLeash()
    {
        offLeashUnlocked = true;
        Debug.Log($"[DogHandler {AgentID}] 오프리쉬 스킬 해금");
    }

    public void NotifyDogFoundTarget(Transform target)
    {
        if (target == null)
            return;

        NotifyDogFoundTargetPosition(target.position);
    }

    public void NotifyDogFoundTargetPosition(Vector3 targetPosition)
    {
        if (Time.time - lastDogReportTime < dogReportCooldown)
            return;

        lastDogReportTime = Time.time;

        AddGuardInstinctStack(1);
        ShareTargetPosition(targetPosition);

        if (detectionDog != null)
            detectionDog.ReturnToHandler();

        Debug.Log(
            $"[DogHandler {AgentID}] 탐지견이 타겟을 발견했습니다. " +
            $"공유 위치: {targetPosition}, 경계 본능 스택: {guardInstinctStack}/{guardInstinctMaxStack}"
        );
    }

    private void ExecuteDogDeploy(Vector3 targetPos)
    {
        if (!HasValidDog())
            return;

        ClearSharedTargetPosition();
        StopLookAroundFromDerived(false);

        bool started = detectionDog.DeployTo(targetPos);

        if (!started)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] 탐지견 배치에 실패했습니다. 위치: {targetPos}");
            return;
        }

        Debug.Log($"[DogHandler {AgentID}] 탐지견 배치 명령 실행. 위치: {targetPos}");
    }

    private void ExecuteTreat()
    {
        if (!treatUnlocked)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] 간식 스킬이 아직 해금되지 않았습니다.");
            return;
        }

        if (!HasValidDog())
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillTreat, skillGaugeChargeBlockSecondsOnUse))
            return;

        detectionDog.ApplyTreat(
            treatDuration,
            treatMoveSpeedMultiplier,
            treatViewRadiusMultiplier,
            treatViewAngleOffset
        );

        Debug.Log($"[DogHandler {AgentID}] 간식 스킬 실행");
    }

    private void ExecuteOffLeash()
    {
        if (!offLeashUnlocked)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] 오프리쉬 스킬이 아직 해금되지 않았습니다.");
            return;
        }

        if (!HasValidDog())
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillOffLeash, skillGaugeChargeBlockSecondsOnUse))
            return;

        bool started = detectionDog.StartOffLeash(offLeashDuration);

        if (!started)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] 오프리쉬를 시작하지 못했습니다.");
            return;
        }

        Debug.Log($"[DogHandler {AgentID}] 오프리쉬 시작");
    }

    private void AddGuardInstinctStack(int amount)
    {
        if (amount <= 0)
            return;

        SetGuardInstinctStack(guardInstinctStack + amount);
    }

    private void SetGuardInstinctStack(int stack)
    {
        guardInstinctStack = Mathf.Clamp(stack, 0, guardInstinctMaxStack);

        ApplyGuardInstinctToHandler();
        ApplyGuardInstinctToDog();
    }

    private void ApplyGuardInstinctToHandler()
    {
        if (navAgent == null || stats == null)
            return;

        navAgent.speed = stats.moveSpeed * GuardInstinctMoveSpeedMultiplier;
    }

    private void ApplyGuardInstinctToDog()
    {
        if (detectionDog == null)
            return;

        detectionDog.SetGuardInstinctMoveSpeedMultiplier(GuardInstinctMoveSpeedMultiplier);
    }

    private void ShareTargetPosition(Vector3 targetPosition)
    {
        EnsureAgentCache(false);

        if (cachedAgents == null)
            return;

        float moveSpeedMultiplier = Mathf.Max(0.01f, sharedTargetMoveSpeedMultiplier);

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            AgentController agent = cachedAgents[i];

            if (agent == null)
                continue;

            if (!agent.isActiveAndEnabled)
                continue;

            bool isSelf = agent == this;

            if (isSelf && !includeSelfInDogReport)
                continue;

            agent.ReceiveSharedTargetPosition(targetPosition, this, moveSpeedMultiplier);

            if (debugSharedTargetReceivers)
            {
                Debug.Log(
                    $"[DogHandler {AgentID}] 타겟 위치 공유 -> " +
                    $"{agent.GetType().Name} AgentID={agent.AgentID}"
                );
            }
        }
    }

    private void ClearSharedTargetPositionFromThisHandler()
    {
        EnsureAgentCache(true);

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

    private void EnsureAgentCache(bool forceRefresh)
    {
        bool shouldRefresh =
            forceRefresh ||
            cachedAgents == null ||
            cachedAgents.Length == 0 ||
            HasInvalidCachedAgent() ||
            Time.time - lastAgentCacheRefreshTime >= agentCacheRefreshInterval;

        if (!shouldRefresh)
            return;

        cachedAgents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        lastAgentCacheRefreshTime = Time.time;
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

    private void EnsureDetectionDog()
    {
        CacheDetectionDog();

        if (detectionDog != null)
            return;

        if (!spawnDogOnAwake)
            return;

        if (detectionDogPrefab == null)
            return;

        Vector3 spawnPosition = ResolveDogSpawnPosition();
        Quaternion spawnRotation = dogSpawnPoint != null ? dogSpawnPoint.rotation : transform.rotation;

        detectionDog = Instantiate(detectionDogPrefab, spawnPosition, spawnRotation);
        detectionDog.name = $"{name}_DetectionDog";
        ownsSpawnedDetectionDog = true;
    }

    private Vector3 ResolveDogSpawnPosition()
    {
        Vector3 spawnPosition;

        if (dogSpawnPoint != null)
        {
            spawnPosition = dogSpawnPoint.position;
        }
        else
        {
            spawnPosition =
                transform.position +
                transform.right * 0.75f -
                transform.forward * 0.75f;
        }

        if (NavMesh.SamplePosition(
                spawnPosition,
                out NavMeshHit hit,
                dogSpawnNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        return spawnPosition;
    }

    private void CacheDetectionDog()
    {
        if (detectionDog != null)
            return;

        if (!autoFindDogInChildren)
            return;

        detectionDog = GetComponentInChildren<DetectionDogController>(true);
    }

    private void InitializeDetectionDog()
    {
        if (detectionDog == null)
            return;

        Transform followTarget = dogFollowTarget != null ? dogFollowTarget : transform;

        detectionDog.Initialize(this, followTarget, targetLayer);
        detectionDog.ApplyStats(stats);
        detectionDog.SetGuardInstinctMoveSpeedMultiplier(GuardInstinctMoveSpeedMultiplier);

        RegisterDogVisionToTargetVisibility();
    }

    private void RegisterDogVisionToTargetVisibility()
    {
        if (!registerDogVisionToTargetVisibility)
            return;

        if (detectionDog == null)
            return;

        VisionSensor dogSensor = detectionDog.GetVisionSensor();

        if (dogSensor == null)
            return;

        TargetVisibilityController targetVisibility = FindFirstObjectByType<TargetVisibilityController>();

        if (targetVisibility == null)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] TargetVisibilityController를 찾지 못해 탐지견 시야를 등록하지 못했습니다.");
            return;
        }

        if (registeredTargetVisibility == targetVisibility)
            return;

        targetVisibility.RegisterSensor(dogSensor);
        registeredTargetVisibility = targetVisibility;

        Debug.Log($"[DogHandler {AgentID}] 탐지견 VisionSensor를 TargetVisibilityController에 등록했습니다.");
    }

    private bool HasValidDog()
    {
        EnsureDetectionDog();
        InitializeDetectionDog();

        if (detectionDog != null)
            return true;

        Debug.LogWarning(
            $"[DogHandler {AgentID}] 탐지견 컨트롤러를 찾지 못했습니다. " +
            "detectionDogPrefab을 할당하거나 detectionDog를 직접 연결해주세요."
        );

        return false;
    }

    private void StopDogActions(bool resetPath)
    {
        if (detectionDog == null)
            return;

        detectionDog.StopAllDogActions(resetPath);
    }

    private string GetCanonicalGaugeSkillName(string skillName)
    {
        if (IsTreatSkill(skillName))
            return SkillTreat;

        if (IsOffLeashSkill(skillName))
            return SkillOffLeash;

        if (IsDogDeploySkill(skillName))
            return SkillDogDeploy;

        if (IsGuardInstinctSkill(skillName))
            return SkillGuardInstinct;

        return skillName;
    }

    private bool IsDogDeploySkill(string skillName)
    {
        string skill = NormalizeSkillName(skillName);

        return skill.Contains("dogdeploy") ||
               skill.Contains("detectiondogdeploy") ||
               skill.Contains("deploydog") ||
               skill.Contains("탐지견배치") ||
               skill.Contains("탐지견") ||
               skill.Contains("배치");
    }

    private bool IsGuardInstinctSkill(string skillName)
    {
        string skill = NormalizeSkillName(skillName);

        return skill.Contains("guardinstinct") ||
               skill.Contains("경계본능");
    }

    private bool IsTreatSkill(string skillName)
    {
        string skill = NormalizeSkillName(skillName);

        return skill.Contains("treat") ||
               skill.Contains("dogtreat") ||
               skill.Contains("간식");
    }

    private bool IsOffLeashSkill(string skillName)
    {
        string skill = NormalizeSkillName(skillName);

        return skill.Contains("offleash") ||
               skill.Contains("오프리쉬");
    }

    private string NormalizeSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "";

        return skillName
            .Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }
}