using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Chaser : AgentController, IUpgradeReceiver
{
    private enum ChaserMoveMode
    {
        IdleLookAround = 0,
        CommandRun = 1,
        AccessControlSprint = 2,
        DebuffedRun = 3
    }

    private const string SkillAccessControl = "accesscontrol";
    private const string SkillEscapeBlock = "escapeblock";
    private const string SkillPatrol = "patrol";
    private const string SkillTrackingInstinct = "trackinginstinct";

    private const string UpgradeAccessControlZoneMobility = "chaser_access_control_zone_mobility";
    private const string UpgradeAccessControlRadiusX2 = "chaser_access_control_radius_x2";
    private const string UpgradeEscapeBlockGaugeX2 = "chaser_escape_block_gauge_x2";
    private const string UpgradeEscapeBlockPressureVision = "chaser_escape_block_pressure_vision";

    private const string UpgradeUnlockPatrol = "chaser_unlock_patrol";
    private const string UpgradeUnlockTrackingInstinct = "chaser_unlock_tracking_instinct";

    private const string UpgradePatrolPressureTracking = "chaser_patrol_pressure_tracking";
    private const string UpgradePatrolHighSpeed = "chaser_patrol_high_speed";

    private const string UpgradeTrackingInstinctMaxStack10 = "chaser_tracking_instinct_max_stack_10";
    private const string UpgradeTrackingInstinctInstinctiveCharge = "chaser_tracking_instinct_instinctive_charge";

    private const string PatrolVisionModifierKey = "chaser_patrol_vision";

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";

    private const float EscapeBlockGaugeEmptyEpsilon = 0.001f;
    private const float ChaserStableMovingThreshold = 0.08f;
    private const float ChaserManualArrivalExtraBuffer = 0.25f;
    private const float ChaserArrivalVelocityStopThreshold = 0.2f;
    private const float AccessControlMoveSampleDistance = 2f;

    [Header("Access Control")]
    [SerializeField] private AccessControlZone accessControlZonePrefab;
    [SerializeField] private float accessControlRadius = 10f;
    [SerializeField] private float accessControlDuration = 20f;

    [SerializeField]
    [Tooltip("출입 통제 구역 내 타겟 이동 속도 배율")]
    private float targetSpeedMultiplierInAccessControl = 0.5f;

    [SerializeField]
    [Tooltip("출입 통제 구역 내 타겟 회전 속도 배율")]
    private float targetAngularSpeedMultiplierInAccessControl = 0.5f;

    [SerializeField]
    [Tooltip("출입 통제 구역 내 보안 요원 이동 속도 배율")]
    private float chaserSpeedMultiplierInAccessControl = 1.5f;

    [SerializeField]
    [Tooltip("출입 통제 구역 내 보안 요원 회전 속도 배율")]
    private float chaserAngularSpeedMultiplierInAccessControl = 1.5f;

    [SerializeField] private bool replacePreviousAccessControlZone = true;

    [Header("Access Control Visual")]
    [SerializeField] private Material accessControlLineMaterial;
    [SerializeField] private Color accessControlLineColor = new Color(1f, 0.15f, 0.05f, 1f);

    [Header("Escape Block")]
    [SerializeField] private float escapeBlockGaugeMax = 25f;
    [SerializeField] private float escapeBlockGaugeDrainPerSecond = 10f;
    [SerializeField] private bool escapeBlockStartsFull = true;
    [SerializeField] private float escapeBlockMaxDistance = 10f;
    [SerializeField] private float escapeBlockRequiredSightTime = 0.25f;
    [SerializeField] private float escapeBlockReleaseDelay = 0.5f;
    [SerializeField] private bool escapeBlockDebugLog = false;

    [Header("Upgrade - Escape Block")]
    [SerializeField] private float pressureVisionRadiusMultiplier = 1.5f;
    [SerializeField] private float pressureVisionHealthDrainMultiplier = 2f;

    [Header("New Skill - Patrol")]
    [SerializeField] private bool patrolSkillUnlocked;
    [SerializeField] private float patrolArrivalDistance = 0.8f;
    [SerializeField] private float patrolNavMeshSampleDistance = 2f;
    [SerializeField] private float patrolSpeedMultiplier = 1f;
    [SerializeField] private float patrolVisionRadiusMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float patrolSkillGaugeChargeMultiplier = 0.25f;

    [Header("Upgrade - Patrol")]
    [SerializeField] private bool patrolPressureTrackingUnlocked;
    [SerializeField] private float patrolPressureTrackingDuration = 4f;
    [SerializeField] private float patrolPressureTrackingHealthDrainMultiplier = 1.5f;
    [SerializeField] private float upgradedPatrolSpeedMultiplier = 1.5f;

    [Header("New Skill - Tracking Instinct")]
    [SerializeField] private bool trackingInstinctUnlocked;
    [SerializeField] private float trackingInstinctDistancePerStack = 30f;
    [SerializeField] private float trackingInstinctSpeedBonusPerStack = 0.05f;
    [SerializeField] private int trackingInstinctMaxStack = 5;
    [SerializeField] private bool resetTrackingInstinctOnSkillGaugeReset = true;

    [Header("Upgrade - Tracking Instinct")]
    [SerializeField] private bool instinctiveChargeUnlocked;
    [SerializeField] private int upgradedTrackingInstinctMaxStack = 10;
    [SerializeField] private int instinctiveChargeRequiredStack = 5;
    [SerializeField] private float instinctiveChargeDuration = 3f;
    [SerializeField] private float instinctiveChargeSpeedMultiplier = 1f;
    [SerializeField] private bool instinctiveChargeOncePerStage = true;

    [Header("Chaser Animation")]
    [SerializeField] private float chaserMovingThreshold = 0.03f;
    [SerializeField] private float chaserAnimationStopDelay = 0.2f;
    [SerializeField] private float chaserDestinationBuffer = 0.2f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    private AccessControlZone currentAccessControlZone;

    private bool pressureVisionEnabled;
    private bool pressureVisionActive;

    private Transform escapeBlockCandidateTarget;
    private Transform escapeBlockBlockedTarget;
    private ITargetEscapeSkillBlockReceiver escapeBlockBlockedReceiver;

    private float escapeBlockGauge;
    private float escapeBlockSightTimer;
    private float escapeBlockReleaseTimer;

    private readonly Vector3[] patrolPoints = new Vector3[2];

    private bool isPatrolling;
    private int currentPatrolPointIndex;
    private bool patrolVisionModifierActive;

    private bool patrolPressureTrackingActive;
    private float patrolPressureTrackingEndTime;
    private Transform patrolPressureTrackingTarget;

    private int trackingInstinctStack;
    private float trackingInstinctDistanceProgress;

    private bool isInstinctiveCharging;
    private bool instinctiveChargeUsedThisStage;
    private float instinctiveChargeEndTime;
    private Transform instinctiveChargeTarget;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;

    private bool cachedChaserAnimationIsMoving;
    private float lastChaserAnimationMovingTime = -999f;

    private bool isHitReactionLocked;
    private bool isResultAnimationLocked;

    private Coroutine hitReactionRoutine;

    public bool IsResultAnimationLocked => isResultAnimationLocked;

    public bool CanUsePatrolSkill => patrolSkillUnlocked;
    public bool HasTrackingInstinctSkill => trackingInstinctUnlocked;
    public bool IsPatrolling => isPatrolling;
    public int TrackingInstinctStack => trackingInstinctStack;

    public float EscapeBlockGauge => escapeBlockGauge;
    public float EscapeBlockGaugeMax => escapeBlockGaugeMax;

    public float EscapeBlockGaugeNormalized
    {
        get
        {
            if (escapeBlockGaugeMax <= 0f)
                return 0f;

            return Mathf.Clamp01(escapeBlockGauge / escapeBlockGaugeMax);
        }
    }

    protected override bool ShouldBlockSharedTargetMovement => isPatrolling || isInstinctiveCharging;

    protected override float SkillGaugeChargeMultiplier
    {
        get
        {
            return isPatrolling ? patrolSkillGaugeChargeMultiplier : 1f;
        }
    }

    public int TrackingInstinctMaxStack => trackingInstinctMaxStack;

    public float TrackingInstinctNormalized
    {
        get
        {
            if (!trackingInstinctUnlocked)
                return 0f;

            if (trackingInstinctMaxStack <= 0)
                return 0f;

            return Mathf.Clamp01((float)trackingInstinctStack / trackingInstinctMaxStack);
        }
    }


    protected override void Awake()
    {
        agentID = 0;

        CacheChaserAnimationHashes();

        base.Awake();

        ApplyChaserStatsFromSO();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheChaserAnimatorParameters();
        InitializeEscapeBlockGauge();
        UpdateAnimationState(true);
    }

    private void ApplyChaserStatsFromSO()
    {
        if (Stats == null)
            return;

        accessControlRadius = Stats.accessControlRadius;
        accessControlDuration = Stats.accessControlDuration;
        targetSpeedMultiplierInAccessControl = Stats.targetSpeedMultiplierInAccessControl;
        targetAngularSpeedMultiplierInAccessControl = Stats.targetAngularSpeedMultiplierInAccessControl;
        chaserSpeedMultiplierInAccessControl = Stats.chaserSpeedMultiplierInAccessControl;
        chaserAngularSpeedMultiplierInAccessControl = Stats.chaserAngularSpeedMultiplierInAccessControl;

        escapeBlockGaugeMax = Stats.escapeBlockGaugeMax;
        escapeBlockGaugeDrainPerSecond = Stats.escapeBlockGaugeDrainPerSecond;
        escapeBlockStartsFull = Stats.escapeBlockStartsFull;
        escapeBlockMaxDistance = Stats.escapeBlockMaxDistance;
        escapeBlockRequiredSightTime = Stats.escapeBlockRequiredSightTime;
        escapeBlockReleaseDelay = Stats.escapeBlockReleaseDelay;

        pressureVisionRadiusMultiplier = Stats.pressureVisionRadiusMultiplier;
        pressureVisionHealthDrainMultiplier = Stats.pressureVisionHealthDrainMultiplier;

        patrolArrivalDistance = Stats.patrolArrivalDistance;
        patrolNavMeshSampleDistance = Stats.patrolNavMeshSampleDistance;
        patrolSpeedMultiplier = Stats.patrolSpeedMultiplier;
        patrolVisionRadiusMultiplier = Stats.patrolVisionRadiusMultiplier;
        patrolSkillGaugeChargeMultiplier = Stats.patrolSkillGaugeChargeMultiplier;

        patrolPressureTrackingDuration = Stats.patrolPressureTrackingDuration;
        patrolPressureTrackingHealthDrainMultiplier = Stats.patrolPressureTrackingHealthDrainMultiplier;
        upgradedPatrolSpeedMultiplier = Stats.upgradedPatrolSpeedMultiplier;

        trackingInstinctDistancePerStack = Stats.trackingInstinctDistancePerStack;
        trackingInstinctSpeedBonusPerStack = Stats.trackingInstinctSpeedBonusPerStack;
        trackingInstinctMaxStack = Stats.trackingInstinctMaxStack;
        resetTrackingInstinctOnSkillGaugeReset = Stats.resetTrackingInstinctOnSkillGaugeReset;

        upgradedTrackingInstinctMaxStack = Stats.upgradedTrackingInstinctMaxStack;
        instinctiveChargeRequiredStack = Stats.instinctiveChargeRequiredStack;
        instinctiveChargeDuration = Stats.instinctiveChargeDuration;
        instinctiveChargeSpeedMultiplier = Stats.instinctiveChargeSpeedMultiplier;
        instinctiveChargeOncePerStage = Stats.instinctiveChargeOncePerStage;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        accessControlRadius = Mathf.Max(0f, accessControlRadius);
        accessControlDuration = Mathf.Max(0f, accessControlDuration);

        targetSpeedMultiplierInAccessControl = Mathf.Max(0.01f, targetSpeedMultiplierInAccessControl);
        targetAngularSpeedMultiplierInAccessControl = Mathf.Max(0.01f, targetAngularSpeedMultiplierInAccessControl);
        chaserSpeedMultiplierInAccessControl = Mathf.Max(0.01f, chaserSpeedMultiplierInAccessControl);
        chaserAngularSpeedMultiplierInAccessControl = Mathf.Max(0.01f, chaserAngularSpeedMultiplierInAccessControl);

        escapeBlockGaugeMax = Mathf.Max(0f, escapeBlockGaugeMax);
        escapeBlockGaugeDrainPerSecond = Mathf.Max(0f, escapeBlockGaugeDrainPerSecond);
        escapeBlockMaxDistance = Mathf.Max(0f, escapeBlockMaxDistance);
        escapeBlockRequiredSightTime = Mathf.Max(0f, escapeBlockRequiredSightTime);
        escapeBlockReleaseDelay = Mathf.Max(0f, escapeBlockReleaseDelay);

        pressureVisionRadiusMultiplier = Mathf.Max(1f, pressureVisionRadiusMultiplier);
        pressureVisionHealthDrainMultiplier = Mathf.Max(1f, pressureVisionHealthDrainMultiplier);

        patrolArrivalDistance = Mathf.Max(0.1f, patrolArrivalDistance);
        patrolNavMeshSampleDistance = Mathf.Max(0.1f, patrolNavMeshSampleDistance);
        patrolSpeedMultiplier = Mathf.Max(0.01f, patrolSpeedMultiplier);
        patrolVisionRadiusMultiplier = Mathf.Max(1f, patrolVisionRadiusMultiplier);
        patrolSkillGaugeChargeMultiplier = Mathf.Clamp01(patrolSkillGaugeChargeMultiplier);

        patrolPressureTrackingDuration = Mathf.Max(0f, patrolPressureTrackingDuration);
        patrolPressureTrackingHealthDrainMultiplier = Mathf.Max(1f, patrolPressureTrackingHealthDrainMultiplier);
        upgradedPatrolSpeedMultiplier = Mathf.Max(1f, upgradedPatrolSpeedMultiplier);

        trackingInstinctDistancePerStack = Mathf.Max(1f, trackingInstinctDistancePerStack);
        trackingInstinctSpeedBonusPerStack = Mathf.Max(0f, trackingInstinctSpeedBonusPerStack);
        trackingInstinctMaxStack = Mathf.Max(0, trackingInstinctMaxStack);

        upgradedTrackingInstinctMaxStack = Mathf.Max(5, upgradedTrackingInstinctMaxStack);
        instinctiveChargeRequiredStack = Mathf.Max(1, instinctiveChargeRequiredStack);
        instinctiveChargeDuration = Mathf.Max(0f, instinctiveChargeDuration);
        instinctiveChargeSpeedMultiplier = Mathf.Max(1f, instinctiveChargeSpeedMultiplier);

        chaserMovingThreshold = Mathf.Max(0f, chaserMovingThreshold);
        chaserAnimationStopDelay = Mathf.Max(0f, chaserAnimationStopDelay);
        chaserDestinationBuffer = Mathf.Max(0f, chaserDestinationBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        CacheChaserAnimationHashes();
    }

    protected override void Update()
    {
        if (isResultAnimationLocked)
        {
            KeepStoppedForResultAnimation();
            UpdateAnimationState();
            return;
        }

        if (isHitReactionLocked)
        {
            UpdateAnimationState();
            return;
        }

        if (isPatrolling && TrySwitchPatrolToSeenTarget())
        {
            base.Update();
            UpdatePatrolPressureTracking();
            UpdateInstinctiveChargeMovement();
            UpdateEscapeBlock();
            UpdatePressureVisionUpgrade();
            return;
        }

        base.Update();

        UpdatePatrolPressureTracking();
        UpdateInstinctiveChargeMovement();

        if (isPatrolling)
            UpdatePatrolMovement();

        UpdateEscapeBlock();
        UpdatePressureVisionUpgrade();
    }

    protected override void OnDisable()
    {
        StopPatrolInternal(false);
        StopInstinctiveCharge(false);
        StopPatrolPressureTracking();

        SetPressureVisionActive(false);
        ReleaseEscapeBlock();
        DestroyCurrentAccessControlZone();
        StopHitReactionRoutine();

        isHitReactionLocked = false;
        isResultAnimationLocked = false;
        cachedChaserAnimationIsMoving = false;
        lastChaserAnimationMovingTime = -999f;

        base.OnDisable();
    }

    protected override void OnAgentMoved(float movedDistance)
    {
        base.OnAgentMoved(movedDistance);
        UpdateTrackingInstinctByDistance(movedDistance);
    }

    protected override void ApplyNavAgentStats()
    {
        base.ApplyNavAgentStats();
        ApplyChaserMovementStatMultipliers();
    }

    public override void MoveTo(Vector3 destination)
    {
        StopPatrolInternal(true);
        StopInstinctiveCharge(true);
        base.MoveTo(destination);
    }

    public override void SetChaseTarget(Transform target)
    {
        StopPatrolInternal(true);
        StopInstinctiveCharge(false);
        base.SetChaseTarget(target);
    }

    public override void StopAllMovementForStageResult()
    {
        StopPatrolInternal(false);
        StopInstinctiveCharge(false);
        StopPatrolPressureTracking();

        base.StopAllMovementForStageResult();

        if (resetTrackingInstinctOnSkillGaugeReset)
            ResetTrackingInstinctRuntime();

        instinctiveChargeUsedThisStage = false;
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return upgrade != null && upgrade.MatchesAgent(CommanderAgentType.Chaser);
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        switch (upgrade.UpgradeId)
        {
            case UpgradeAccessControlZoneMobility:
                ApplyAccessControlZoneMobilityUpgrade(upgrade.Value);
                break;

            case UpgradeAccessControlRadiusX2:
                ApplyAccessControlRadiusUpgrade(upgrade.Value);
                break;

            case UpgradeEscapeBlockGaugeX2:
                ApplyEscapeBlockGaugeUpgrade(upgrade.Value);
                break;

            case UpgradeEscapeBlockPressureVision:
                pressureVisionEnabled = true;
                break;

            case UpgradeUnlockPatrol:
                UnlockPatrolSkill();
                break;

            case UpgradeUnlockTrackingInstinct:
                UnlockTrackingInstinctSkill();
                break;

            case UpgradePatrolPressureTracking:
                ApplyPatrolPressureTrackingUpgrade(upgrade.Value);
                break;

            case UpgradePatrolHighSpeed:
                ApplyPatrolHighSpeedUpgrade(upgrade.Value);
                break;

            case UpgradeTrackingInstinctMaxStack10:
                ApplyTrackingInstinctMaxStack10Upgrade(upgrade.Value);
                break;

            case UpgradeTrackingInstinctInstinctiveCharge:
                ApplyInstinctiveChargeUpgrade(upgrade.Value);
                break;
        }
    }

    private void ApplyAccessControlZoneMobilityUpgrade(float multiplier)
    {
        if (multiplier <= 0f)
            multiplier = 2f;

        float baseMultiplier = 1f;
        float currentBonus = Mathf.Max(0f, chaserSpeedMultiplierInAccessControl - baseMultiplier);

        chaserSpeedMultiplierInAccessControl = baseMultiplier + currentBonus * multiplier;

        Debug.Log(
            $"[Chaser {AgentID}] 출입 통제 기동 강화 적용. " +
            $"ChaserSpeedMultiplier={chaserSpeedMultiplierInAccessControl:F2}"
        );
    }

    private void ApplyAccessControlRadiusUpgrade(float multiplier)
    {
        if (multiplier <= 0f)
            multiplier = 2f;

        accessControlRadius *= multiplier;

        Debug.Log(
            $"[Chaser {AgentID}] 광역 통제 강화 적용. " +
            $"AccessControlRadius={accessControlRadius:F2}"
        );
    }

    private void ApplyEscapeBlockGaugeUpgrade(float multiplier)
    {
        if (multiplier <= 0f)
            multiplier = 2f;

        float previousMax = Mathf.Max(0.01f, escapeBlockGaugeMax);
        float currentRatio = Mathf.Clamp01(escapeBlockGauge / previousMax);

        escapeBlockGaugeMax *= multiplier;
        escapeBlockGauge = escapeBlockGaugeMax * currentRatio;

        Debug.Log(
            $"[Chaser {AgentID}] 예비 제지 게이지 강화 적용. " +
            $"EscapeBlockGaugeMax={escapeBlockGaugeMax:F2}"
        );
    }

    private void UnlockPatrolSkill()
    {
        patrolSkillUnlocked = true;

        trackingInstinctUnlocked = false;
        ResetTrackingInstinctRuntime();
        StopInstinctiveCharge(false);

        Debug.Log($"[Chaser {AgentID}] 신규 스킬 해금: 순찰");
    }

    private void UnlockTrackingInstinctSkill()
    {
        trackingInstinctUnlocked = true;

        patrolSkillUnlocked = false;
        StopPatrolInternal(true);

        Debug.Log($"[Chaser {AgentID}] 신규 스킬 해금: 추적 본능");
    }

    private void ApplyPatrolPressureTrackingUpgrade(float value)
    {
        patrolPressureTrackingUnlocked = true;

        if (value > 0f)
            patrolPressureTrackingHealthDrainMultiplier = Mathf.Max(1f, value);

        Debug.Log(
            $"[Chaser {AgentID}] 순찰 강화 적용: 압박 추적. " +
            $"HealthDrainMultiplier={patrolPressureTrackingHealthDrainMultiplier:F2}, " +
            $"Duration={patrolPressureTrackingDuration:F1}"
        );
    }

    private void ApplyPatrolHighSpeedUpgrade(float value)
    {
        float targetMultiplier = value > 0f ? value : upgradedPatrolSpeedMultiplier;

        patrolSpeedMultiplier = Mathf.Max(1f, targetMultiplier);

        if (isPatrolling)
            ReapplyStats();

        Debug.Log(
            $"[Chaser {AgentID}] 순찰 강화 적용: 고속 순찰. " +
            $"PatrolSpeedMultiplier={patrolSpeedMultiplier:F2}"
        );
    }

    private void ApplyTrackingInstinctMaxStack10Upgrade(float value)
    {
        int targetMaxStack = Mathf.RoundToInt(value);

        if (targetMaxStack <= trackingInstinctMaxStack)
            targetMaxStack = upgradedTrackingInstinctMaxStack;

        trackingInstinctMaxStack = Mathf.Max(trackingInstinctMaxStack, targetMaxStack);

        Debug.Log(
            $"[Chaser {AgentID}] 추적 본능 강화 적용: 강화된 본능. " +
            $"MaxStack={trackingInstinctMaxStack}"
        );
    }

    private void ApplyInstinctiveChargeUpgrade(float value)
    {
        instinctiveChargeUnlocked = true;

        if (value > 0f)
            instinctiveChargeDuration = value;

        Debug.Log(
            $"[Chaser {AgentID}] 추적 본능 강화 적용: 본능적 돌격. " +
            $"RequiredStack={instinctiveChargeRequiredStack}, Duration={instinctiveChargeDuration:F1}"
        );
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked)
            return;

        if (!CanReceivePlayerSkillCommand(true))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Chaser {AgentID}] 스킬 요청: {skillName}, 위치: {targetPos}");

        if (IsPatrolSkill(skill))
        {
            Debug.LogWarning($"[Chaser {AgentID}] 순찰은 두 좌표와 함께 입력해야 합니다. 예: (1,2), (5,8) 순찰");
            return;
        }

        if (IsTrackingInstinctSkill(skill))
        {
            Debug.LogWarning($"[Chaser {AgentID}] 추적 본능은 패시브 스킬이므로 직접 사용할 수 없습니다.");
            return;
        }

        if (IsAccessControlSkill(skill))
        {
            StopPatrolInternal(true);
            StopInstinctiveCharge(true);
            ExecuteAccessControl(targetPos);
            return;
        }

        if (IsEscapeBlockSkill(skill))
        {
            Debug.Log($"[Chaser {AgentID}] 도주 제지는 자동 스킬입니다. 타겟이 시야에 들어오면 게이지를 소모하며 자동으로 적용됩니다.");
            return;
        }

        Debug.LogWarning($"[Chaser {AgentID}] 알 수 없는 스킬입니다: {skillName}");
    }

    public bool TryStartPatrol(Vector3 firstPoint, Vector3 secondPoint)
    {
        if (!patrolSkillUnlocked)
        {
            Debug.LogWarning($"[Chaser {AgentID}] 순찰 스킬을 아직 배우지 않았습니다.");
            return false;
        }

        if (isResultAnimationLocked || isHitReactionLocked)
            return false;

        if (!CanReceivePlayerSkillCommand(true))
            return false;

        if (currentTarget != null)
        {
            Debug.LogWarning($"[Chaser {AgentID}] 현재 타겟을 추적 중이므로 순찰을 시작할 수 없습니다.");
            return false;
        }

        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"[Chaser {AgentID}] NavMeshAgent 상태가 올바르지 않아 순찰을 시작할 수 없습니다.");
            return false;
        }

        if (!TryResolvePatrolPoint(firstPoint, out Vector3 resolvedFirstPoint))
            return false;

        if (!TryResolvePatrolPoint(secondPoint, out Vector3 resolvedSecondPoint))
            return false;

        patrolPoints[0] = resolvedFirstPoint;
        patrolPoints[1] = resolvedSecondPoint;

        StopLookAroundFromDerived(false);
        StopInstinctiveCharge(false);
        ClearSharedTargetPosition();

        currentTarget = null;
        isManualMoving = false;
        currentPatrolPointIndex = 0;
        isPatrolling = true;

        SetPatrolVisionModifierActive(true);
        ReapplyStats();

        bool started = SetPatrolDestination(currentPatrolPointIndex);

        if (!started)
        {
            StopPatrolInternal(true);
            return false;
        }

        Debug.Log($"[Chaser {AgentID}] 순찰 시작: {patrolPoints[0]} <-> {patrolPoints[1]}");
        return true;
    }

    private bool TryResolvePatrolPoint(Vector3 sourcePoint, out Vector3 resolvedPoint)
    {
        resolvedPoint = sourcePoint;

        if (navAgent == null)
            return false;

        if (!NavMesh.SamplePosition(sourcePoint, out NavMeshHit hit, patrolNavMeshSampleDistance, navAgent.areaMask))
        {
            Debug.LogWarning($"[Chaser {AgentID}] 순찰 지점 근처에서 NavMesh 위치를 찾지 못했습니다. point={sourcePoint}");
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        bool pathFound = navAgent.CalculatePath(hit.position, path);

        if (!pathFound || path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning(
                $"[Chaser {AgentID}] 순찰 지점으로 이동할 수 없습니다. " +
                $"point={sourcePoint}, sampled={hit.position}, status={path.status}"
            );

            return false;
        }

        resolvedPoint = hit.position;
        return true;
    }

    private void UpdatePatrolMovement()
    {
        if (!isPatrolling)
            return;

        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
        {
            StopPatrolInternal(false);
            return;
        }

        if (TrySwitchPatrolToSeenTarget())
            return;

        if (navAgent.pathPending)
            return;

        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid ||
            navAgent.pathStatus == NavMeshPathStatus.PathPartial)
        {
            Debug.LogWarning($"[Chaser {AgentID}] 순찰 경로가 끊어져 순찰을 중단합니다.");
            StopPatrolInternal(true);
            return;
        }

        if (!HasReachedCurrentPatrolPoint())
            return;

        currentPatrolPointIndex = currentPatrolPointIndex == 0 ? 1 : 0;
        SetPatrolDestination(currentPatrolPointIndex);
    }

    private bool SetPatrolDestination(int pointIndex)
    {
        if (navAgent == null)
            return false;

        pointIndex = Mathf.Clamp(pointIndex, 0, 1);

        navAgent.isStopped = false;
        navAgent.ResetPath();

        bool success = navAgent.SetDestination(patrolPoints[pointIndex]);

        if (!success)
        {
            Debug.LogWarning($"[Chaser {AgentID}] 순찰 목적지를 설정하지 못했습니다. point={patrolPoints[pointIndex]}");
            return false;
        }

        cachedChaserAnimationIsMoving = true;
        lastChaserAnimationMovingTime = Time.time;

        UpdateAnimationState(true);
        UpdateStateIcon();

        return true;
    }

    private bool HasReachedCurrentPatrolPoint()
    {
        if (navAgent == null)
            return true;

        if (navAgent.pathPending)
            return false;

        Vector3 current = transform.position;
        Vector3 destination = patrolPoints[currentPatrolPointIndex];

        current.y = 0f;
        destination.y = 0f;

        float distance = Vector3.Distance(current, destination);
        float arrivalDistance = Mathf.Max(patrolArrivalDistance, navAgent.stoppingDistance + 0.2f);

        if (distance <= arrivalDistance)
            return true;

        if (!navAgent.hasPath && distance <= arrivalDistance + 0.2f)
            return true;

        return false;
    }

    private bool TrySwitchPatrolToSeenTarget()
    {
        if (!isPatrolling)
            return false;

        if (visionSensor == null)
            return false;

        if (!visionSensor.IsSeeingTarget)
            return false;

        Transform seenTarget = visionSensor.CurrentSeenTarget;

        if (seenTarget == null)
            return false;

        StopPatrolInternal(true);
        SetChaseTarget(seenTarget);
        StartPatrolPressureTracking(seenTarget);

        Debug.Log($"[Chaser {AgentID}] 순찰 중 타겟 발견. 순찰을 중단하고 추적을 시작합니다: {seenTarget.name}");
        return true;
    }

    private void StopPatrolInternal(bool resetPath)
    {
        if (!isPatrolling)
            return;

        isPatrolling = false;
        currentPatrolPointIndex = 0;

        SetPatrolVisionModifierActive(false);

        if (resetPath && navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        cachedChaserAnimationIsMoving = false;
        lastChaserAnimationMovingTime = -999f;

        ReapplyStats();
        UpdateAnimationState(true);
        UpdateStateIcon();

        Debug.Log($"[Chaser {AgentID}] 순찰 중단");
    }

    private void SetPatrolVisionModifierActive(bool active)
    {
        if (patrolVisionModifierActive == active)
            return;

        patrolVisionModifierActive = active;

        if (visionSensor == null)
            return;

        if (patrolVisionModifierActive && patrolVisionRadiusMultiplier > 1.0001f)
        {
            visionSensor.SetExternalViewRadiusMultiplier(
                PatrolVisionModifierKey,
                patrolVisionRadiusMultiplier
            );
        }
        else
        {
            visionSensor.RemoveExternalViewRadiusMultiplier(PatrolVisionModifierKey);
        }
    }

    private void StartPatrolPressureTracking(Transform target)
    {
        if (!patrolPressureTrackingUnlocked)
            return;

        if (target == null)
            return;

        if (patrolPressureTrackingDuration <= 0f)
            return;

        patrolPressureTrackingTarget = target;
        patrolPressureTrackingActive = true;
        patrolPressureTrackingEndTime = Time.time + patrolPressureTrackingDuration;

        Debug.Log(
            $"[Chaser {AgentID}] 압박 추적 발동. " +
            $"Target={target.name}, " +
            $"Duration={patrolPressureTrackingDuration:F1}, " +
            $"HealthDrainMultiplier={patrolPressureTrackingHealthDrainMultiplier:F2}"
        );
    }

    private void UpdatePatrolPressureTracking()
    {
        if (!patrolPressureTrackingActive)
            return;

        if (Time.time >= patrolPressureTrackingEndTime)
        {
            StopPatrolPressureTracking();
            return;
        }

        if (patrolPressureTrackingTarget == null)
        {
            StopPatrolPressureTracking();
            return;
        }

        TargetController targetController = FindTargetController(patrolPressureTrackingTarget);

        if (targetController == null)
        {
            StopPatrolPressureTracking();
            return;
        }

        targetController.ApplyFleeHealthDrainMultiplier(
            patrolPressureTrackingHealthDrainMultiplier,
            Time.deltaTime
        );
    }

    private void StopPatrolPressureTracking()
    {
        if (!patrolPressureTrackingActive)
            return;

        patrolPressureTrackingActive = false;
        patrolPressureTrackingTarget = null;
        patrolPressureTrackingEndTime = 0f;

        Debug.Log($"[Chaser {AgentID}] 압박 추적 종료");
    }

    private void UpdateTrackingInstinctByDistance(float movedDistance)
    {
        if (!trackingInstinctUnlocked)
            return;

        if (trackingInstinctMaxStack <= 0)
            return;

        if (trackingInstinctDistancePerStack <= 0f)
            return;

        if (trackingInstinctStack >= trackingInstinctMaxStack)
            return;

        if (navAgent == null || navAgent.isStopped)
            return;

        if (movedDistance <= 0.001f)
            return;

        trackingInstinctDistanceProgress += movedDistance;

        bool stackChanged = false;

        while (trackingInstinctDistanceProgress >= trackingInstinctDistancePerStack &&
               trackingInstinctStack < trackingInstinctMaxStack)
        {
            trackingInstinctDistanceProgress -= trackingInstinctDistancePerStack;
            trackingInstinctStack++;
            stackChanged = true;
        }

        if (!stackChanged)
            return;

        ReapplyStats();
        TryStartInstinctiveChargeByStack();

        Debug.Log(
            $"[Chaser {AgentID}] 추적 본능 스택 증가: " +
            $"{trackingInstinctStack}/{trackingInstinctMaxStack}, " +
            $"SpeedMultiplier={GetTrackingInstinctSpeedMultiplier():F2}"
        );
    }

    private void ResetTrackingInstinctRuntime()
    {
        trackingInstinctStack = 0;
        trackingInstinctDistanceProgress = 0f;
        ReapplyStats();
    }

    private float GetTrackingInstinctSpeedMultiplier()
    {
        if (!trackingInstinctUnlocked)
            return 1f;

        if (trackingInstinctStack <= 0)
            return 1f;

        return 1f + trackingInstinctSpeedBonusPerStack * trackingInstinctStack;
    }

    private void TryStartInstinctiveChargeByStack()
    {
        if (!instinctiveChargeUnlocked)
            return;

        if (isInstinctiveCharging)
            return;

        if (instinctiveChargeOncePerStage && instinctiveChargeUsedThisStage)
            return;

        if (trackingInstinctStack < instinctiveChargeRequiredStack)
            return;

        Transform target = FindInstinctiveChargeTarget();

        if (target == null)
        {
            Debug.LogWarning($"[Chaser {AgentID}] 본능적 돌격 대상 타겟을 찾지 못했습니다.");
            return;
        }

        StartInstinctiveCharge(target);
    }

    private void StartInstinctiveCharge(Transform target)
    {
        if (target == null)
            return;

        if (instinctiveChargeDuration <= 0f)
            return;

        StopPatrolInternal(true);
        StopLookAroundFromDerived(false);
        ClearSharedTargetPosition();

        instinctiveChargeTarget = target;
        instinctiveChargeEndTime = Time.time + instinctiveChargeDuration;
        isInstinctiveCharging = true;
        instinctiveChargeUsedThisStage = true;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
            navAgent.SetDestination(instinctiveChargeTarget.position);
        }

        ReapplyStats();

        Debug.Log(
            $"[Chaser {AgentID}] 본능적 돌격 발동. " +
            $"Target={target.name}, Duration={instinctiveChargeDuration:F1}"
        );
    }

    private void UpdateInstinctiveChargeMovement()
    {
        if (!isInstinctiveCharging)
            return;

        if (Time.time >= instinctiveChargeEndTime)
        {
            StopInstinctiveCharge(true);
            return;
        }

        if (instinctiveChargeTarget == null)
        {
            StopInstinctiveCharge(true);
            return;
        }

        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
        {
            StopInstinctiveCharge(false);
            return;
        }

        navAgent.isStopped = false;
        navAgent.SetDestination(instinctiveChargeTarget.position);
    }

    private void StopInstinctiveCharge(bool resetPath)
    {
        if (!isInstinctiveCharging)
            return;

        isInstinctiveCharging = false;
        instinctiveChargeTarget = null;
        instinctiveChargeEndTime = 0f;

        if (resetPath &&
            currentTarget == null &&
            !isManualMoving &&
            !isPatrolling &&
            navAgent != null &&
            navAgent.isActiveAndEnabled &&
            navAgent.isOnNavMesh)
        {
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        ReapplyStats();

        Debug.Log($"[Chaser {AgentID}] 본능적 돌격 종료");
    }

    private Transform FindInstinctiveChargeTarget()
    {
        if (currentTarget != null)
            return currentTarget;

        if (visionSensor != null && visionSensor.CurrentSeenTarget != null)
            return visionSensor.CurrentSeenTarget;

        TargetController[] targets = FindObjectsByType<TargetController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        if (targets == null || targets.Length == 0)
            return null;

        TargetController closestTarget = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            TargetController target = targets[i];

            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closestTarget = target;
        }

        return closestTarget != null ? closestTarget.transform : null;
    }

    private TargetController FindTargetController(Transform target)
    {
        if (target == null)
            return null;

        TargetController targetController = target.GetComponentInParent<TargetController>();

        if (targetController == null)
            targetController = target.GetComponentInChildren<TargetController>();

        return targetController;
    }

    private void ApplyChaserMovementStatMultipliers()
    {
        if (navAgent == null)
            return;

        float speedMultiplier = 1f;

        speedMultiplier *= GetTrackingInstinctSpeedMultiplier();

        if (isPatrolling)
            speedMultiplier *= patrolSpeedMultiplier;

        if (isInstinctiveCharging)
            speedMultiplier *= instinctiveChargeSpeedMultiplier;

        navAgent.speed *= Mathf.Max(0.01f, speedMultiplier);
    }

    protected override void CheckDestinationReached()
    {
        if (navAgent == null)
            return;

        if (!isManualMoving)
            return;

        if (navAgent.pathPending)
            return;

        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid ||
            navAgent.pathStatus == NavMeshPathStatus.PathPartial)
        {
            CompleteChaserManualMove("Manual move failed. Path is not complete.");
            return;
        }

        if (!navAgent.hasPath)
        {
            CompleteChaserManualMove("Manual move finished. Path no longer exists.");
            return;
        }

        if (float.IsInfinity(navAgent.remainingDistance))
            return;

        float stopDistance = Mathf.Max(navAgent.stoppingDistance, 0.05f);
        float arrivalDistance = stopDistance + chaserDestinationBuffer + ChaserManualArrivalExtraBuffer;

        float realDistance = Vector3.Distance(transform.position, navAgent.destination);

        bool closeByRemainingDistance = navAgent.remainingDistance <= arrivalDistance;
        bool closeByRealDistance = realDistance <= arrivalDistance;

        float velocityThresholdSqr = ChaserArrivalVelocityStopThreshold * ChaserArrivalVelocityStopThreshold;
        bool almostStopped = navAgent.velocity.sqrMagnitude <= velocityThresholdSqr;

        if ((closeByRemainingDistance && closeByRealDistance) ||
            ((closeByRemainingDistance || closeByRealDistance) && almostStopped))
        {
            CompleteChaserManualMove("Reached manual destination. Chaser forced to idle.");
        }
    }

    private void CompleteChaserManualMove(string logMessage)
    {
        isManualMoving = false;
        cachedChaserAnimationIsMoving = false;
        lastChaserAnimationMovingTime = -999f;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        UpdateAnimationState(true);
        UpdateStateIcon();

        Debug.Log($"[Chaser {AgentID}] {logMessage}");
    }

    protected override void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        bool isMoving = ResolveChaserAnimationIsMoving();
        ChaserMoveMode moveMode = ResolveChaserMoveMode(isMoving);

        if (hasIsMovingParameter)
            animator.SetBool(isMovingHash, isMoving);

        if (hasMoveModeParameter)
            animator.SetInteger(moveModeHash, (int)moveMode);

        if (!hasMoveSpeedParameter)
            return;

        float actualSpeed = navAgent.velocity.magnitude;
        float effectiveMovingThreshold = Mathf.Max(chaserMovingThreshold, ChaserStableMovingThreshold);

        if (!isMoving || actualSpeed <= effectiveMovingThreshold)
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

    public override float GetSkillGaugeMaxForSkill(string skillName)
    {
        if (IsEscapeBlockSkill(skillName))
            return escapeBlockGaugeMax;

        if (IsTrackingInstinctSkill(skillName))
            return trackingInstinctUnlocked ? Mathf.Max(1, trackingInstinctMaxStack) : 0f;

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeCurrentForSkill(string skillName)
    {
        if (IsEscapeBlockSkill(skillName))
            return Mathf.Clamp(escapeBlockGauge, 0f, escapeBlockGaugeMax);

        if (IsTrackingInstinctSkill(skillName))
            return trackingInstinctUnlocked ? Mathf.Clamp(trackingInstinctStack, 0, trackingInstinctMaxStack) : 0f;

        return base.GetSkillGaugeCurrentForSkill(skillName);
    }

    public override float GetSkillGaugeNormalizedForSkill(string skillName)
    {
        if (IsEscapeBlockSkill(skillName))
        {
            if (escapeBlockGaugeMax <= 0f)
                return 0f;

            return Mathf.Clamp01(escapeBlockGauge / escapeBlockGaugeMax);
        }

        if (IsTrackingInstinctSkill(skillName))
            return TrackingInstinctNormalized;

        return base.GetSkillGaugeNormalizedForSkill(skillName);
    }

    public override bool CanUseSkillGaugeForSkill(string skillName, bool showWarning = false)
    {
        if (IsEscapeBlockSkill(skillName))
        {
            bool canUse = HasEscapeBlockGauge();

            if (!canUse && showWarning)
                Debug.LogWarning($"[Chaser {AgentID}] 도주 제지 게이지가 없습니다.");

            return canUse;
        }

        return base.CanUseSkillGaugeForSkill(skillName, showWarning);
    }

    public override void ResetSkillGauge()
    {
        base.ResetSkillGauge();
        InitializeEscapeBlockGauge();

        if (resetTrackingInstinctOnSkillGaugeReset)
            ResetTrackingInstinctRuntime();

        instinctiveChargeUsedThisStage = false;
        StopInstinctiveCharge(false);
    }

    public override void FillSkillGauge()
    {
        base.FillSkillGauge();
        FillEscapeBlockGauge();
    }

    public void FillEscapeBlockGauge()
    {
        escapeBlockGauge = escapeBlockGaugeMax;
    }

    public void ResetEscapeBlockGauge()
    {
        InitializeEscapeBlockGauge();
    }

    public override void PlayHitReaction(Vector3 hitSourcePosition)
    {
        if (isResultAnimationLocked)
            return;

        StopPatrolInternal(true);
        StopInstinctiveCharge(true);

        if (hitReactionRoutine != null)
            StopCoroutine(hitReactionRoutine);

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

        StopHitReactionRoutine();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void InitializeEscapeBlockGauge()
    {
        if (escapeBlockStartsFull)
            escapeBlockGauge = escapeBlockGaugeMax;
        else
            escapeBlockGauge = Mathf.Clamp(escapeBlockGauge, 0f, escapeBlockGaugeMax);
    }

    private bool HasEscapeBlockGauge()
    {
        return escapeBlockGauge > EscapeBlockGaugeEmptyEpsilon;
    }

    private void UpdateEscapeBlock()
    {
        if (!HasEscapeBlockGauge())
        {
            ReleaseEscapeBlock();
            return;
        }

        Transform seenTarget = GetValidEscapeBlockTarget();

        if (seenTarget == null)
        {
            UpdateEscapeBlockRelease();
            return;
        }

        DrainEscapeBlockGauge();

        if (!HasEscapeBlockGauge())
        {
            ReleaseEscapeBlock();
            return;
        }

        escapeBlockReleaseTimer = escapeBlockReleaseDelay;

        if (escapeBlockCandidateTarget != seenTarget)
        {
            escapeBlockCandidateTarget = seenTarget;
            escapeBlockSightTimer = 0f;
        }

        escapeBlockSightTimer += Time.deltaTime;

        if (escapeBlockSightTimer < escapeBlockRequiredSightTime)
            return;

        ApplyEscapeBlock(seenTarget);
    }

    private void DrainEscapeBlockGauge()
    {
        if (escapeBlockGaugeDrainPerSecond <= 0f)
            return;

        float previousGauge = escapeBlockGauge;

        escapeBlockGauge = Mathf.Max(
            0f,
            escapeBlockGauge - escapeBlockGaugeDrainPerSecond * Time.deltaTime
        );

        if (previousGauge > 0f && escapeBlockGauge <= 0f && escapeBlockDebugLog)
            Debug.Log($"[Chaser {AgentID}] 도주 제지 게이지가 모두 소모되었습니다.");
    }

    private Transform GetValidEscapeBlockTarget()
    {
        if (visionSensor == null)
            return null;

        if (!visionSensor.IsSeeingTarget)
            return null;

        Transform seenTarget = visionSensor.CurrentSeenTarget;

        if (seenTarget == null)
            return null;

        if (escapeBlockMaxDistance > 0f)
        {
            float sqrDistance = (seenTarget.position - transform.position).sqrMagnitude;

            if (sqrDistance > escapeBlockMaxDistance * escapeBlockMaxDistance)
                return null;
        }

        return seenTarget;
    }

    private void UpdateEscapeBlockRelease()
    {
        escapeBlockCandidateTarget = null;
        escapeBlockSightTimer = 0f;

        if (escapeBlockBlockedReceiver == null)
            return;

        escapeBlockReleaseTimer -= Time.deltaTime;

        if (escapeBlockReleaseTimer > 0f)
            return;

        ReleaseEscapeBlock();
    }

    private void ApplyEscapeBlock(Transform target)
    {
        if (target == null)
            return;

        ITargetEscapeSkillBlockReceiver receiver = FindEscapeSkillBlockReceiver(target);

        if (receiver == null)
            return;

        if (escapeBlockBlockedReceiver == receiver)
            return;

        ReleaseEscapeBlock();

        escapeBlockBlockedReceiver = receiver;
        escapeBlockBlockedTarget = target;
        escapeBlockReleaseTimer = escapeBlockReleaseDelay;

        escapeBlockBlockedReceiver.SetEscapeSkillBlocked(this, true);

        if (escapeBlockDebugLog)
            Debug.Log($"[Chaser {AgentID}] 도주 제지 적용: {target.name}");
    }

    private void ReleaseEscapeBlock()
    {
        SetPressureVisionActive(false);

        if (escapeBlockBlockedReceiver != null)
        {
            escapeBlockBlockedReceiver.SetEscapeSkillBlocked(this, false);

            if (escapeBlockDebugLog && escapeBlockBlockedTarget != null)
                Debug.Log($"[Chaser {AgentID}] 도주 제지 해제: {escapeBlockBlockedTarget.name}");
        }

        escapeBlockBlockedReceiver = null;
        escapeBlockBlockedTarget = null;
        escapeBlockCandidateTarget = null;

        escapeBlockSightTimer = 0f;
        escapeBlockReleaseTimer = 0f;
    }

    private void UpdatePressureVisionUpgrade()
    {
        bool shouldActivate =
            pressureVisionEnabled &&
            HasEscapeBlockGauge() &&
            escapeBlockBlockedReceiver != null &&
            escapeBlockBlockedTarget != null;

        SetPressureVisionActive(shouldActivate);

        if (!shouldActivate)
            return;

        if (visionSensor == null)
            return;

        if (!visionSensor.CanDirectlySeeTransform(escapeBlockBlockedTarget))
            return;

        TargetController targetController = FindTargetController(escapeBlockBlockedTarget);

        if (targetController == null)
            return;

        targetController.ApplyFleeHealthDrainMultiplier(
            pressureVisionHealthDrainMultiplier,
            Time.deltaTime
        );
    }

    private void SetPressureVisionActive(bool active)
    {
        if (pressureVisionActive == active)
            return;

        pressureVisionActive = active;

        if (visionSensor == null)
            return;

        if (pressureVisionActive)
        {
            visionSensor.SetExternalViewRadiusMultiplier(
                this,
                pressureVisionRadiusMultiplier
            );
        }
        else
        {
            visionSensor.RemoveExternalViewRadiusMultiplier(this);
        }
    }

    private ITargetEscapeSkillBlockReceiver FindEscapeSkillBlockReceiver(Transform target)
    {
        if (target == null)
            return null;

        MonoBehaviour[] components = target.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is ITargetEscapeSkillBlockReceiver receiver)
                return receiver;
        }

        components = target.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is ITargetEscapeSkillBlockReceiver receiver)
                return receiver;
        }

        return null;
    }

    private void ExecuteAccessControl(Vector3 centerPosition)
    {
        if (!TryConsumeSkillGaugeForSkill(SkillAccessControl))
            return;

        if (replacePreviousAccessControlZone)
            DestroyCurrentAccessControlZone();

        AccessControlZone zone = CreateAccessControlZone(centerPosition);

        if (zone == null)
        {
            Debug.LogWarning($"[Chaser {AgentID}] 출입 통제 구역 프리팹이 설정되지 않았습니다.");
            return;
        }

        zone.Initialize(
            this,
            centerPosition,
            accessControlRadius,
            accessControlDuration,
            targetLayer,
            targetSpeedMultiplierInAccessControl,
            targetAngularSpeedMultiplierInAccessControl,
            chaserSpeedMultiplierInAccessControl,
            chaserAngularSpeedMultiplierInAccessControl,
            accessControlLineMaterial,
            accessControlLineColor
        );

        currentAccessControlZone = zone;

        RequestInstalledObjectCamera(zone.transform);

        MoveToAccessControlPoint(centerPosition);

        Debug.Log(
            $"[Chaser {AgentID}] 출입 통제 구역 생성 및 이동 시작. " +
            $"Center={centerPosition}, Radius={accessControlRadius}, Duration={accessControlDuration}, " +
            $"ChaserSpeedMultiplier={chaserSpeedMultiplierInAccessControl:F2}"
        );
    }

    private void MoveToAccessControlPoint(Vector3 centerPosition)
    {
        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning($"[Chaser {AgentID}] NavMeshAgent가 NavMesh 위에 없습니다. 출입 통제 지점 이동을 취소합니다.");
            return;
        }

        StopPatrolInternal(false);
        StopInstinctiveCharge(false);

        Vector3 destination = centerPosition;

        if (NavMesh.SamplePosition(centerPosition, out NavMeshHit hit, AccessControlMoveSampleDistance, NavMesh.AllAreas))
            destination = hit.position;

        currentTarget = null;
        ClearSharedTargetPosition();

        isManualMoving = true;

        navAgent.isStopped = false;
        navAgent.ResetPath();

        bool success = navAgent.SetDestination(destination);

        if (!success)
        {
            isManualMoving = false;
            Debug.LogWarning($"[Chaser {AgentID}] 출입 통제 지점으로 이동할 수 없습니다.");
            return;
        }

        cachedChaserAnimationIsMoving = true;
        lastChaserAnimationMovingTime = Time.time;

        UpdateAnimationState(true);
        UpdateStateIcon();
    }

    private AccessControlZone CreateAccessControlZone(Vector3 centerPosition)
    {
        if (accessControlZonePrefab != null)
        {
            AccessControlZone zone = Instantiate(
                accessControlZonePrefab,
                centerPosition,
                Quaternion.identity
            );

            zone.name = $"ChaserAccessControlZone_Agent{AgentID}";
            return zone;
        }

        GameObject zoneObject = new GameObject($"ChaserAccessControlZone_Agent{AgentID}");
        zoneObject.transform.position = centerPosition;

        return zoneObject.AddComponent<AccessControlZone>();
    }

    private void DestroyCurrentAccessControlZone()
    {
        if (currentAccessControlZone == null)
            return;

        Destroy(currentAccessControlZone.gameObject);
        currentAccessControlZone = null;
    }

    private bool ResolveChaserAnimationIsMoving()
    {
        if (navAgent == null)
            return false;

        if (isResultAnimationLocked || isHitReactionLocked)
        {
            cachedChaserAnimationIsMoving = false;
            return false;
        }

        if (navAgent.isStopped)
        {
            cachedChaserAnimationIsMoving = false;
            return false;
        }

        float effectiveMovingThreshold = Mathf.Max(chaserMovingThreshold, ChaserStableMovingThreshold);
        float movingThresholdSqr = effectiveMovingThreshold * effectiveMovingThreshold;

        bool reachedDestination = HasReachedDestinationForChaserAnimation();

        bool hasActualVelocity =
            navAgent.velocity.sqrMagnitude > movingThresholdSqr;

        if (reachedDestination && !hasActualVelocity)
        {
            cachedChaserAnimationIsMoving = false;
            return false;
        }

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            isPatrolling ||
            isInstinctiveCharging ||
            currentTarget != null ||
            IsFollowingSharedTargetPosition ||
            HasActivePathForChaserAnimation();

        bool hasNotReachedDestination = !reachedDestination;
        bool shouldMove = hasMovementIntent && (hasActualVelocity || hasNotReachedDestination);

        if (shouldMove)
        {
            cachedChaserAnimationIsMoving = true;
            lastChaserAnimationMovingTime = Time.time;
            return true;
        }

        if (cachedChaserAnimationIsMoving &&
            Time.time - lastChaserAnimationMovingTime <= chaserAnimationStopDelay)
        {
            return true;
        }

        cachedChaserAnimationIsMoving = false;
        return false;
    }

    private bool HasActivePathForChaserAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForChaserAnimation();
    }

    private bool HasReachedDestinationForChaserAnimation()
    {
        if (navAgent == null)
            return true;

        if (navAgent.pathPending)
            return false;

        if (!navAgent.hasPath)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return false;

        float stopDistance = Mathf.Max(navAgent.stoppingDistance, 0.05f);
        return navAgent.remainingDistance <= stopDistance + chaserDestinationBuffer;
    }

    private ChaserMoveMode ResolveChaserMoveMode(bool isMoving)
    {
        if (!isMoving)
            return ChaserMoveMode.IdleLookAround;

        if (isPatrolling)
            return ChaserMoveMode.AccessControlSprint;

        if (IsSmokeDebuffed)
            return ChaserMoveMode.DebuffedRun;

        if (IsInsideCurrentAccessControlZone())
            return ChaserMoveMode.AccessControlSprint;

        return ChaserMoveMode.CommandRun;
    }

    private bool IsInsideCurrentAccessControlZone()
    {
        if (currentAccessControlZone == null)
            return false;

        Vector3 center = currentAccessControlZone.transform.position;
        Vector3 agentPosition = transform.position;

        center.y = 0f;
        agentPosition.y = 0f;

        float sqrDistance = (agentPosition - center).sqrMagnitude;
        return sqrDistance <= accessControlRadius * accessControlRadius;
    }

    private IEnumerator HitReactionRoutine(Vector3 hitSourcePosition)
    {
        isHitReactionLocked = true;

        bool previousStopped = false;
        bool previousUpdateRotation = true;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            previousStopped = navAgent.isStopped;
            previousUpdateRotation = navAgent.updateRotation;

            navAgent.isStopped = true;
            navAgent.updateRotation = false;
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

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
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
            Debug.LogWarning($"[Chaser {AgentID}] Animator가 없어서 {triggerName} 애니메이션을 실행할 수 없습니다.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Chaser {AgentID}] Animator에 {triggerName} Trigger가 없습니다.");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;

        ReleaseEscapeBlock();
        StopPatrolInternal(false);
        StopInstinctiveCharge(false);
        StopPatrolPressureTracking();
        StopHitReactionRoutine();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStoppedForResultAnimation();
        UpdateAnimationState(true);

        animator.ResetTrigger(hitReactionHash);
        animator.ResetTrigger(victoryHash);
        animator.ResetTrigger(defeatHash);
        animator.SetTrigger(triggerHash);

        Debug.Log($"[Chaser {AgentID}] {triggerName} 애니메이션 실행");
    }

    private void KeepStoppedForResultAnimation()
    {
        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void SetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
            return;

        animator.SetTrigger(triggerHash);
    }

    private void CacheChaserAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
    }

    private void CacheChaserAnimatorParameters()
    {
        hasIsMovingParameter = HasAnimatorParameter(IsMovingParameter, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(MoveSpeedParameter, AnimatorControllerParameterType.Float);
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasHitReactionTrigger = HasAnimatorParameter(HitReactionTriggerName, AnimatorControllerParameterType.Trigger);
        hasVictoryTrigger = HasAnimatorParameter(VictoryTriggerName, AnimatorControllerParameterType.Trigger);
        hasDefeatTrigger = HasAnimatorParameter(DefeatTriggerName, AnimatorControllerParameterType.Trigger);
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

        Debug.LogWarning($"[Chaser {AgentID}] Animator 파라미터가 없습니다: {parameterName} ({parameterType})");
        return false;
    }

    private bool IsAccessControlSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillAccessControl) ||
               skill.Contains("access control") ||
               skill.Contains("control zone") ||
               skill.Contains("restricted zone") ||
               skill.Contains("출입 통제") ||
               skill.Contains("출입통제") ||
               skill.Contains("통제 구역") ||
               skill.Contains("통제구역") ||
               skill.Contains("금지 구역") ||
               skill.Contains("금지구역");
    }

    private bool IsEscapeBlockSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        string normalizedSkill = skill.Trim().ToLower();

        return normalizedSkill.Contains(SkillEscapeBlock) ||
               normalizedSkill.Contains("escape block") ||
               normalizedSkill.Contains("escape skill block") ||
               normalizedSkill.Contains("도주 제지") ||
               normalizedSkill.Contains("도주제지") ||
               normalizedSkill.Contains("도주 스킬 차단") ||
               normalizedSkill.Contains("도주스킬차단") ||
               normalizedSkill.Contains("도주 차단") ||
               normalizedSkill.Contains("도주차단");
    }

    private bool IsPatrolSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        string normalizedSkill = skill.Trim().ToLower();

        return normalizedSkill.Contains(SkillPatrol) ||
               normalizedSkill.Contains("patrol") ||
               normalizedSkill.Contains("순찰");
    }

    private bool IsTrackingInstinctSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        string normalizedSkill = skill.Trim().ToLower();

        return normalizedSkill.Contains(SkillTrackingInstinct) ||
               normalizedSkill.Contains("tracking instinct") ||
               normalizedSkill.Contains("추적 본능") ||
               normalizedSkill.Contains("추적본능");
    }
}