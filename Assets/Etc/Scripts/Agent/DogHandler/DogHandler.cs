using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DogHandler : AgentController
{
    private enum DogHandlerMoveMode
    {
        Idle = 0,
        Run = 1,
        DebuffedRun = 2
    }

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";
    private const string DeployDogTriggerName = "DeployDog";
    private const string UseTreatTriggerName = "UseTreat";
    private const string OffLeashTriggerName = "OffLeash";

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
    [SerializeField] private bool registerSpawnedDogVisionToTargetVisibility = true;

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

    [Header("Animation")]
    [SerializeField] private float animationMovingThreshold = 0.05f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private float hitReactionLockSeconds = 0.35f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    [Header("Skill Animation")]
    [SerializeField] private bool stopWhenUseSkill = true;
    [SerializeField] private float dogDeployAnimationLockSeconds = 0.8f;
    [SerializeField] private float dogDeployExecuteDelay = 0.2f;
    [SerializeField] private float treatAnimationLockSeconds = 0.6f;
    [SerializeField] private float treatExecuteDelay = 0.15f;
    [SerializeField] private float offLeashAnimationLockSeconds = 0.8f;
    [SerializeField] private float offLeashExecuteDelay = 0.2f;

    [Header("Off Leash Camera")]
    [SerializeField] private bool useOffLeashSkillCamera = true;

    private AgentController[] cachedAgents;
    private float lastAgentCacheRefreshTime = -999f;

    private int guardInstinctStack;
    private float lastDogReportTime = -999f;

    private bool ownsSpawnedDetectionDog;
    private TargetVisibilityController registeredTargetVisibility;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;
    private int deployDogHash;
    private int useTreatHash;
    private int offLeashHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;
    private bool hasDeployDogTrigger;
    private bool hasUseTreatTrigger;
    private bool hasOffLeashTrigger;

    private bool isResultAnimationLocked;
    private bool isHitReactionLocked;
    private bool isDogDeployAnimationLocked;
    private bool isTreatAnimationLocked;
    private bool isOffLeashAnimationLocked;

    private Coroutine hitReactionRoutine;
    private Coroutine dogDeployAnimationRoutine;
    private Coroutine treatAnimationRoutine;
    private Coroutine offLeashAnimationRoutine;

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

        CacheDogHandlerAnimationHashes();
        CacheDogHandlerAnimatorParameters();

        EnsureDetectionDog();
        InitializeDetectionDog();
        ApplyGuardInstinctToDog();
        UpdateAnimationState(true);
    }

    protected override void OnDisable()
    {
        StopDogActions(false);
        ClearSharedTargetPositionFromThisHandler();
        StopDogHandlerAnimationRoutines();

        isResultAnimationLocked = false;
        isHitReactionLocked = false;
        isDogDeployAnimationLocked = false;
        isTreatAnimationLocked = false;
        isOffLeashAnimationLocked = false;

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

        animationMovingThreshold = Mathf.Max(0.001f, animationMovingThreshold);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        dogDeployAnimationLockSeconds = Mathf.Max(0f, dogDeployAnimationLockSeconds);
        dogDeployExecuteDelay = Mathf.Clamp(dogDeployExecuteDelay, 0f, dogDeployAnimationLockSeconds);

        treatAnimationLockSeconds = Mathf.Max(0f, treatAnimationLockSeconds);
        treatExecuteDelay = Mathf.Clamp(treatExecuteDelay, 0f, treatAnimationLockSeconds);

        offLeashAnimationLockSeconds = Mathf.Max(0f, offLeashAnimationLockSeconds);
        offLeashExecuteDelay = Mathf.Clamp(offLeashExecuteDelay, 0f, offLeashAnimationLockSeconds);

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
        if (hasDeployDogTrigger && animator != null)
        {
            StopDogDeployAnimationRoutine();
            dogDeployAnimationRoutine = StartCoroutine(DogDeployAnimationRoutine(targetPos));
            return;
        }

        ExecuteDogDeployInstant(targetPos);
    }

    private void ExecuteDogDeployInstant(Vector3 targetPos)
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
        if (hasUseTreatTrigger && animator != null)
        {
            StopTreatAnimationRoutine();
            treatAnimationRoutine = StartCoroutine(TreatAnimationRoutine());
            return;
        }

        ExecuteTreatInstant();
    }

    private void ExecuteTreatInstant()
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
        if (hasOffLeashTrigger && animator != null)
        {
            StopOffLeashAnimationRoutine();
            offLeashAnimationRoutine = StartCoroutine(OffLeashAnimationRoutine());
            return;
        }

        ExecuteOffLeashInstant();
    }

    private void ExecuteOffLeashInstant()
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

        RequestOffLeashDogSkillCamera();

        Debug.Log($"[DogHandler {AgentID}] 오프리쉬 시작");
    }

    private void RequestOffLeashDogSkillCamera()
    {
        if (!useOffLeashSkillCamera)
            return;

        if (detectionDog == null)
            return;

        SkillCameraEventBus.Request(
            SkillCameraFocusMode.ObjectOnly,
            null,
            detectionDog.transform
        );
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

        RegisterSpawnedDogVisionToTargetVisibility();
    }

    private void RegisterSpawnedDogVisionToTargetVisibility()
    {
        if (!registerSpawnedDogVisionToTargetVisibility)
            return;

        if (detectionDog == null)
            return;

        if (detectionDog.transform.IsChildOf(transform))
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

        Debug.Log($"[DogHandler {AgentID}] 독립 생성된 탐지견 VisionSensor를 TargetVisibilityController에 등록했습니다.");
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

    protected override void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        bool isMoving = ResolveDogHandlerIsMoving();
        DogHandlerMoveMode moveMode = ResolveDogHandlerMoveMode(isMoving);

        if (hasIsMovingParameter)
            animator.SetBool(isMovingHash, isMoving);

        if (hasMoveModeParameter)
            animator.SetInteger(moveModeHash, (int)moveMode);

        if (!hasMoveSpeedParameter)
            return;

        float actualSpeed = navAgent.velocity.magnitude;

        if (!isMoving || actualSpeed <= animationMovingThreshold)
        {
            animator.SetFloat(moveSpeedHash, 0f);
            return;
        }

        float normalizedSpeed;

        if (stats != null && stats.moveSpeed > 0.01f)
            normalizedSpeed = Mathf.Clamp01(actualSpeed / stats.moveSpeed);
        else
            normalizedSpeed = Mathf.Clamp01(actualSpeed);

        normalizedSpeed = Mathf.Max(normalizedSpeed, minimumMovingNormalizedSpeed);

        if (immediate)
            animator.SetFloat(moveSpeedHash, normalizedSpeed);
        else
            animator.SetFloat(moveSpeedHash, normalizedSpeed, 0.08f, Time.deltaTime);
    }

    public override void PlayHitReaction(Vector3 hitSourcePosition)
    {
        if (isResultAnimationLocked)
            return;

        StopHitReactionRoutine();
        hitReactionRoutine = StartCoroutine(HitReactionRoutine(hitSourcePosition));
    }

    public override void PlayVictoryPose()
    {
        PlayResultAnimation(victoryHash, hasVictoryTrigger, "Victory");
    }

    public override void PlayDefeatPose()
    {
        PlayResultAnimation(defeatHash, hasDefeatTrigger, "Defeat");
    }

    public override void ClearResultAnimationLock()
    {
        isResultAnimationLocked = false;
        isHitReactionLocked = false;
        isDogDeployAnimationLocked = false;
        isTreatAnimationLocked = false;
        isOffLeashAnimationLocked = false;

        StopDogHandlerAnimationRoutines();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private IEnumerator DogDeployAnimationRoutine(Vector3 targetPos)
    {
        isDogDeployAnimationLocked = true;

        if (stopWhenUseSkill)
            ForceStopForDogHandlerSkill();

        UpdateAnimationState(true);
        ResetSkillAnimatorTriggers();
        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);

        animator.SetTrigger(deployDogHash);

        float executeDelay = Mathf.Clamp(dogDeployExecuteDelay, 0f, dogDeployAnimationLockSeconds);

        if (executeDelay > 0f)
            yield return new WaitForSeconds(executeDelay);

        ExecuteDogDeployInstant(targetPos);

        float remainTime = dogDeployAnimationLockSeconds - executeDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isDogDeployAnimationLocked = false;
        dogDeployAnimationRoutine = null;

        UpdateAnimationState(true);
    }

    private IEnumerator TreatAnimationRoutine()
    {
        isTreatAnimationLocked = true;

        if (stopWhenUseSkill)
            ForceStopForDogHandlerSkill();

        UpdateAnimationState(true);
        ResetSkillAnimatorTriggers();
        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);

        animator.SetTrigger(useTreatHash);

        float executeDelay = Mathf.Clamp(treatExecuteDelay, 0f, treatAnimationLockSeconds);

        if (executeDelay > 0f)
            yield return new WaitForSeconds(executeDelay);

        ExecuteTreatInstant();

        float remainTime = treatAnimationLockSeconds - executeDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isTreatAnimationLocked = false;
        treatAnimationRoutine = null;

        UpdateAnimationState(true);
    }

    private IEnumerator OffLeashAnimationRoutine()
    {
        isOffLeashAnimationLocked = true;

        if (stopWhenUseSkill)
            ForceStopForDogHandlerSkill();

        UpdateAnimationState(true);
        ResetSkillAnimatorTriggers();
        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);

        animator.SetTrigger(offLeashHash);

        float executeDelay = Mathf.Clamp(offLeashExecuteDelay, 0f, offLeashAnimationLockSeconds);

        if (executeDelay > 0f)
            yield return new WaitForSeconds(executeDelay);

        ExecuteOffLeashInstant();

        float remainTime = offLeashAnimationLockSeconds - executeDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isOffLeashAnimationLocked = false;
        offLeashAnimationRoutine = null;

        UpdateAnimationState(true);
    }

    private IEnumerator HitReactionRoutine(Vector3 hitSourcePosition)
    {
        isHitReactionLocked = true;

        StopDogHandlerSkillAnimationRoutines();

        bool previousStopped = false;
        bool previousUpdateRotation = true;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            previousStopped = navAgent.isStopped;
            previousUpdateRotation = navAgent.updateRotation;

            navAgent.isStopped = true;
            navAgent.updateRotation = false;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        if (faceAwayFromHitSource)
            FaceAwayFromHitSource(hitSourcePosition);

        UpdateAnimationState(true);
        SetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);

        if (hitReactionLockSeconds > 0f)
            yield return new WaitForSeconds(hitReactionLockSeconds);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh && !isResultAnimationLocked)
        {
            navAgent.isStopped = previousStopped;
            navAgent.updateRotation = previousUpdateRotation;
        }

        isHitReactionLocked = false;
        hitReactionRoutine = null;

        UpdateAnimationState(true);
    }

    private DogHandlerMoveMode ResolveDogHandlerMoveMode(bool isMoving)
    {
        if (isResultAnimationLocked ||
            isHitReactionLocked ||
            isDogDeployAnimationLocked ||
            isTreatAnimationLocked ||
            isOffLeashAnimationLocked)
        {
            return DogHandlerMoveMode.Idle;
        }

        if (!isMoving)
            return DogHandlerMoveMode.Idle;

        if (IsSmokeDebuffed)
            return DogHandlerMoveMode.DebuffedRun;

        return DogHandlerMoveMode.Run;
    }

    private bool ResolveDogHandlerIsMoving()
    {
        if (navAgent == null)
            return false;

        if (navAgent.isStopped)
            return false;

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            currentTarget != null ||
            IsFollowingSharedTargetPosition ||
            HasActivePathForDogHandlerAnimation();

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForDogHandlerAnimation();

        return hasMovementIntent && (hasVelocity || hasNotReachedDestination);
    }

    private bool HasActivePathForDogHandlerAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForDogHandlerAnimation();
    }

    private bool HasReachedDestinationForDogHandlerAnimation()
    {
        if (navAgent == null)
            return true;

        if (navAgent.pathPending)
            return false;

        if (float.IsInfinity(navAgent.remainingDistance))
            return false;

        float stoppingDistance = Mathf.Max(navAgent.stoppingDistance, 0.05f);

        if (navAgent.remainingDistance > stoppingDistance)
            return false;

        if (navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold)
            return false;

        return true;
    }

    private void FaceAwayFromHitSource(Vector3 hitSourcePosition)
    {
        Vector3 awayDirection = transform.position - hitSourcePosition;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(awayDirection.normalized, Vector3.up);
    }

    private void PlayResultAnimation(int triggerHash, bool hasTrigger, string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] Animator가 없어서 {triggerName} 애니메이션을 실행할 수 없습니다.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[DogHandler {AgentID}] Animator에 {triggerName} Trigger가 없습니다.");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;
        isDogDeployAnimationLocked = false;
        isTreatAnimationLocked = false;
        isOffLeashAnimationLocked = false;

        StopDogHandlerAnimationRoutines();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStoppedForDogHandlerResult();
        UpdateAnimationState(true);

        ResetSkillAnimatorTriggers();
        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);

        animator.SetTrigger(triggerHash);

        Debug.Log($"[DogHandler {AgentID}] {triggerName} 애니메이션 실행");
    }

    private void ForceStopForDogHandlerSkill()
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void KeepStoppedForDogHandlerResult()
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void StopDogHandlerAnimationRoutines()
    {
        StopDogHandlerSkillAnimationRoutines();
        StopHitReactionRoutine();
    }

    private void StopDogHandlerSkillAnimationRoutines()
    {
        StopDogDeployAnimationRoutine();
        StopTreatAnimationRoutine();
        StopOffLeashAnimationRoutine();
    }

    private void StopDogDeployAnimationRoutine()
    {
        if (dogDeployAnimationRoutine == null)
            return;

        StopCoroutine(dogDeployAnimationRoutine);
        dogDeployAnimationRoutine = null;
        isDogDeployAnimationLocked = false;
    }

    private void StopTreatAnimationRoutine()
    {
        if (treatAnimationRoutine == null)
            return;

        StopCoroutine(treatAnimationRoutine);
        treatAnimationRoutine = null;
        isTreatAnimationLocked = false;
    }

    private void StopOffLeashAnimationRoutine()
    {
        if (offLeashAnimationRoutine == null)
            return;

        StopCoroutine(offLeashAnimationRoutine);
        offLeashAnimationRoutine = null;
        isOffLeashAnimationLocked = false;
    }

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
        isHitReactionLocked = false;
    }

    private void ResetSkillAnimatorTriggers()
    {
        ResetAnimatorTrigger(deployDogHash, hasDeployDogTrigger);
        ResetAnimatorTrigger(useTreatHash, hasUseTreatTrigger);
        ResetAnimatorTrigger(offLeashHash, hasOffLeashTrigger);
    }

    private void SetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
            return;

        animator.SetTrigger(triggerHash);
    }

    private void ResetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
            return;

        animator.ResetTrigger(triggerHash);
    }

    private void CacheDogHandlerAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
        deployDogHash = Animator.StringToHash(DeployDogTriggerName);
        useTreatHash = Animator.StringToHash(UseTreatTriggerName);
        offLeashHash = Animator.StringToHash(OffLeashTriggerName);
    }

    private void CacheDogHandlerAnimatorParameters()
    {
        hasIsMovingParameter = HasAnimatorParameter(IsMovingParameter, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(MoveSpeedParameter, AnimatorControllerParameterType.Float);
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasHitReactionTrigger = HasAnimatorParameter(HitReactionTriggerName, AnimatorControllerParameterType.Trigger);
        hasVictoryTrigger = HasAnimatorParameter(VictoryTriggerName, AnimatorControllerParameterType.Trigger);
        hasDefeatTrigger = HasAnimatorParameter(DefeatTriggerName, AnimatorControllerParameterType.Trigger);
        hasDeployDogTrigger = HasAnimatorParameter(DeployDogTriggerName, AnimatorControllerParameterType.Trigger);
        hasUseTreatTrigger = HasAnimatorParameter(UseTreatTriggerName, AnimatorControllerParameterType.Trigger);
        hasOffLeashTrigger = HasAnimatorParameter(OffLeashTriggerName, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == parameterType)
                return true;
        }

        Debug.LogWarning($"[DogHandler {AgentID}] Animator 파라미터가 없습니다: {parameterName} ({parameterType})");
        return false;
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