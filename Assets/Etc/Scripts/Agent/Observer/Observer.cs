using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Observer : AgentController, IUpgradeReceiver
{
    private enum ObserverMoveMode
    {
        IdleLookAround = 0,
        Run = 1,
        Reserved = 2,
        DebuffedRun = 3
    }

    private const string SkillDrone = "drone";
    private const string SkillReconnaissance = "reconnaissance";
    private const string SkillObservationSupport = "observationsupport";
    private const string SkillPositionShare = "positionshare";

    private const string UpgradeDroneTrackingWatch = "observer_drone_tracking_watch";
    private const string UpgradeDroneHighPowerBattery = "observer_drone_high_power_battery";
    private const string UpgradePositionShareQuickResponse = "observer_position_share_quick_response";
    private const string UpgradePositionShareLinkedSurveillance = "observer_position_share_linked_surveillance";

    private const string UpgradeUnlockReconnaissance = "observer_unlock_reconnaissance";
    private const string UpgradeUnlockObservationSupport = "observer_unlock_observation_support";
    private const string UpgradeReconnaissanceUpgradeModule = "observer_reconnaissance_upgrade_module";
    private const string UpgradeReconnaissanceSkilledPilot = "observer_reconnaissance_skilled_pilot";
    private const string UpgradeObservationSupportHawkeye = "observer_observation_support_hawkeye";
    private const string UpgradeObservationSupportEfficientObservation = "observer_observation_support_efficient_observation";

    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";
    private const string DeployDroneTriggerName = "DeployDrone";

    private const float AnimationMovingThreshold = 0.05f;
    private const float DestinationBuffer = 0.2f;
    private const float LinkedSurveillanceShareInterval = 0.15f;
    private const float DefaultObserverSkillGaugeMax = 100f;

    [Header("Drone")]
    [SerializeField] private Drone dronePrefab;
    [SerializeField] private Transform droneParent;
    [SerializeField] private float droneRadius = 15f;
    [SerializeField] private float droneDuration = 20f;
    [SerializeField] private float droneSpawnHeight = 6f;
    [SerializeField] private bool replaceExistingDrone = true;
    [SerializeField] private bool stopWhenDeployDrone = true;

    [Header("Reconnaissance")]
    [SerializeField] private Reconnaissance reconnaissancePrefab;
    [SerializeField] private bool reconnaissanceUnlocked = false;
    [SerializeField] private float reconnaissanceGaugeRequirement = 75f;
    [SerializeField] private float reconnaissanceRadius = 3.5f;
    [SerializeField] private float reconnaissanceMaxDistance = 18f;
    [SerializeField] private float reconnaissanceFlightSpeed = 12f;
    [SerializeField] private float reconnaissanceRevealHoldDuration = 2.5f;
    [SerializeField] private bool replaceExistingReconnaissance = true;
    [SerializeField] private bool requestCameraOnReconnaissance = false;

    [Header("Observation Support")]
    [SerializeField] private bool observationSupportUnlocked = false;
    [SerializeField] private float observationSupportGaugeRequirement = 100f;
    [SerializeField] private float observationSupportDuration = 10f;
    [SerializeField] private float observationSupportViewRadiusMultiplier = 1.25f;
    [SerializeField] private bool includeSelfInObservationSupport = false;

    [Header("Position Share")]
    [SerializeField] private bool targetPositionShareEnabled = true;
    [SerializeField] private bool includeSelfInTargetPositionShare = false;
    [SerializeField] private float agentCacheRefreshInterval = 0.5f;
    [SerializeField] private bool debugPositionShareTargets = false;

    [Header("Upgrade - Observer")]
    [SerializeField] private float trackingWatchDurationMultiplier = 0.5f;
    [SerializeField] private float highPowerBatteryGaugeRequirementMultiplier = 0.5f;
    [SerializeField] private float quickResponseMoveSpeedMultiplier = 1.5f;
    [SerializeField] private float upgradeModuleReconnaissanceGaugeRequirement = 50f;
    [SerializeField] private float upgradeModuleReconnaissanceRadiusMultiplier = 1.25f;
    [SerializeField] private float skilledPilotCurveAmplitude = 1.25f;
    [SerializeField] private float skilledPilotCurveWaveLength = 8f;
    [SerializeField] private float hawkeyeObservationSupportDuration = 20f;
    [SerializeField] private float hawkeyeViewAngleOffset = -10f;
    [SerializeField] private float efficientObservationGaugeRequirement = 50f;

    [Header("Observer Animation")]
    [SerializeField] private float droneDeployLockSeconds = 0.8f;
    [SerializeField] private float droneSpawnDelay = 0.35f;
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    private Drone currentDrone;
    private Reconnaissance currentReconnaissance;

    private AgentController[] cachedAgents;
    private float lastAgentCacheRefreshTime = -999f;

    private bool isTargetPositionSharing;
    private float lastTargetPositionShareTime = -999f;

    private bool droneTrackingWatchEnabled;
    private bool highPowerBatteryEnabled;
    private bool quickResponseEnabled;
    private bool linkedSurveillanceNetworkEnabled;
    private bool reconnaissanceUpgradeModuleEnabled;
    private bool reconnaissanceSkilledPilotEnabled;
    private bool observationSupportHawkeyeEnabled;
    private bool observationSupportEfficientObservationEnabled;

    private readonly HashSet<VisionSensor> linkedSurveillanceSensors = new HashSet<VisionSensor>();
    private readonly List<VisionSensor> linkedSurveillanceSensorsToRemove = new List<VisionSensor>();
    private float nextLinkedSurveillanceShareTime;

    private readonly HashSet<VisionSensor> observationSupportSensors = new HashSet<VisionSensor>();
    private readonly List<VisionSensor> observationSupportSensorsToRemove = new List<VisionSensor>();

    private int moveModeHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;
    private int deployDroneHash;

    private bool hasMoveModeParameter;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;
    private bool hasDeployDroneTrigger;

    private bool isDroneDeployLocked;
    private bool isHitReactionLocked;
    private bool isResultAnimationLocked;

    private Coroutine droneDeployRoutine;
    private Coroutine hitReactionRoutine;
    private Coroutine observationSupportRoutine;

    public bool IsTargetPositionShareEnabled => targetPositionShareEnabled;
    public bool IsTargetPositionSharing => isTargetPositionSharing;
    public bool IsReconnaissanceUnlocked => reconnaissanceUnlocked;
    public bool IsObservationSupportUnlocked => observationSupportUnlocked;
    public Drone CurrentDrone => currentDrone;
    public Reconnaissance CurrentReconnaissance => currentReconnaissance;
    public bool IsObservationSupportActive => observationSupportRoutine != null;
    public bool IsResultAnimationLocked => isResultAnimationLocked;
    public bool IsDroneDeployLocked => isDroneDeployLocked;

    protected override void Awake()
    {
        agentID = 1;

        CacheObserverAnimationHashes();

        base.Awake();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheObserverAnimatorParameters();
        UpdateAnimationState(true);
    }

    private void Start()
    {
        RefreshCachedAgents();
        UpdateLinkedSurveillanceSubscriptions(true);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        droneRadius = Mathf.Max(0f, droneRadius);
        droneDuration = Mathf.Max(0f, droneDuration);
        droneSpawnHeight = Mathf.Max(0f, droneSpawnHeight);

        reconnaissanceGaugeRequirement = Mathf.Max(0f, reconnaissanceGaugeRequirement);
        reconnaissanceRadius = Mathf.Max(0f, reconnaissanceRadius);
        reconnaissanceMaxDistance = Mathf.Max(0f, reconnaissanceMaxDistance);
        reconnaissanceFlightSpeed = Mathf.Max(0.01f, reconnaissanceFlightSpeed);
        reconnaissanceRevealHoldDuration = Mathf.Max(0f, reconnaissanceRevealHoldDuration);

        observationSupportGaugeRequirement = Mathf.Max(0f, observationSupportGaugeRequirement);
        observationSupportDuration = Mathf.Max(0f, observationSupportDuration);
        observationSupportViewRadiusMultiplier = Mathf.Max(1f, observationSupportViewRadiusMultiplier);

        droneDeployLockSeconds = Mathf.Max(0f, droneDeployLockSeconds);
        droneSpawnDelay = Mathf.Max(0f, droneSpawnDelay);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        agentCacheRefreshInterval = Mathf.Max(0.05f, agentCacheRefreshInterval);

        trackingWatchDurationMultiplier = Mathf.Clamp(trackingWatchDurationMultiplier, 0.01f, 1f);
        highPowerBatteryGaugeRequirementMultiplier = Mathf.Clamp(highPowerBatteryGaugeRequirementMultiplier, 0.01f, 1f);
        quickResponseMoveSpeedMultiplier = Mathf.Max(1f, quickResponseMoveSpeedMultiplier);
        upgradeModuleReconnaissanceGaugeRequirement = Mathf.Max(0f, upgradeModuleReconnaissanceGaugeRequirement);
        upgradeModuleReconnaissanceRadiusMultiplier = Mathf.Max(1f, upgradeModuleReconnaissanceRadiusMultiplier);

        skilledPilotCurveAmplitude = Mathf.Max(0f, skilledPilotCurveAmplitude);
        skilledPilotCurveWaveLength = Mathf.Max(0.01f, skilledPilotCurveWaveLength);
        hawkeyeObservationSupportDuration = Mathf.Max(0f, hawkeyeObservationSupportDuration);
        hawkeyeViewAngleOffset = Mathf.Clamp(hawkeyeViewAngleOffset, -359f, 359f);
        efficientObservationGaugeRequirement = Mathf.Max(0f, efficientObservationGaugeRequirement);

        CacheObserverAnimationHashes();
    }

    protected override void OnDisable()
    {
        DestroyCurrentDrone();
        DestroyCurrentReconnaissance();
        StopObservationSupport();

        ClearSharedTargetPositionFromThisObserver();
        ClearLinkedSurveillanceSubscriptions();

        StopDroneDeployRoutine();
        StopHitReactionRoutine();

        isTargetPositionSharing = false;
        lastTargetPositionShareTime = -999f;

        isDroneDeployLocked = false;
        isHitReactionLocked = false;
        isResultAnimationLocked = false;

        base.OnDisable();
    }

    protected override void Update()
    {
        if (isResultAnimationLocked)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        if (isHitReactionLocked)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        if (isDroneDeployLocked && stopWhenDeployDrone)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        base.Update();

        UpdateTargetPositionShareFromVision();
        UpdateTargetPositionShareState();
        UpdateLinkedSurveillanceSubscriptions(false);
        UpdateLinkedSurveillanceCurrentVision();
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        return new[]
        {
            SkillDrone,
            SkillReconnaissance,
            SkillObservationSupport
        };
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return upgrade != null && upgrade.MatchesAgent(CommanderAgentType.Observer);
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        switch (upgrade.UpgradeId)
        {
            case UpgradeDroneTrackingWatch:
                ApplyDroneTrackingWatchUpgrade(upgrade.Value);
                break;

            case UpgradeDroneHighPowerBattery:
                ApplyHighPowerBatteryUpgrade(upgrade.Value);
                break;

            case UpgradePositionShareQuickResponse:
                ApplyQuickResponseUpgrade(upgrade.Value);
                break;

            case UpgradePositionShareLinkedSurveillance:
                ApplyLinkedSurveillanceNetworkUpgrade();
                break;

            case UpgradeUnlockReconnaissance:
                UnlockReconnaissance();
                break;

            case UpgradeUnlockObservationSupport:
                UnlockObservationSupport();
                break;

            case UpgradeReconnaissanceUpgradeModule:
                ApplyReconnaissanceUpgradeModule();
                break;

            case UpgradeReconnaissanceSkilledPilot:
                ApplyReconnaissanceSkilledPilot();
                break;

            case UpgradeObservationSupportHawkeye:
                ApplyObservationSupportHawkeye();
                break;

            case UpgradeObservationSupportEfficientObservation:
                ApplyObservationSupportEfficientObservation();
                break;

            default:
                Debug.LogWarning($"[Observer {AgentID}] 알 수 없는 강화 ID입니다: {upgrade.UpgradeId}");
                break;
        }
    }

    private void ApplyObservationSupportEfficientObservation()
    {
        if (observationSupportEfficientObservationEnabled)
            return;

        observationSupportEfficientObservationEnabled = true;
        observationSupportGaugeRequirement = efficientObservationGaugeRequirement;

        Debug.Log(
            $"[Observer {AgentID}] 효율적 관측 강화 적용. " +
            $"ObservationSupportGaugeRequirement={observationSupportGaugeRequirement:0.#}"
        );
    }

    private void ApplyObservationSupportHawkeye()
    {
        if (observationSupportHawkeyeEnabled)
            return;

        observationSupportHawkeyeEnabled = true;
        observationSupportDuration = hawkeyeObservationSupportDuration;

        Debug.Log(
            $"[Observer {AgentID}] 호크아이 강화 적용. " +
            $"ObservationSupportDuration={observationSupportDuration:0.#}, " +
            $"ViewAngleOffset={hawkeyeViewAngleOffset:0.#}"
        );
    }

    private void ApplyReconnaissanceSkilledPilot()
    {
        if (reconnaissanceSkilledPilotEnabled)
            return;

        reconnaissanceSkilledPilotEnabled = true;

        Debug.Log(
            $"[Observer {AgentID}] 숙련된 조종사 강화 적용. " +
            $"CurveAmplitude={skilledPilotCurveAmplitude:0.##}, " +
            $"CurveWaveLength={skilledPilotCurveWaveLength:0.##}"
        );
    }

    public override float GetSkillGaugeMaxForSkill(string skillName)
    {
        if (IsReconnaissanceSkillName(skillName))
        {
            float maxGauge = base.GetSkillGaugeMaxForSkill(SkillReconnaissance);
            return maxGauge > 0f ? maxGauge : DefaultObserverSkillGaugeMax;
        }

        if (IsObservationSupportSkillName(skillName))
        {
            float maxGauge = base.GetSkillGaugeMaxForSkill(SkillObservationSupport);
            return maxGauge > 0f ? maxGauge : DefaultObserverSkillGaugeMax;
        }

        if (IsDroneSkillName(skillName))
            return base.GetSkillGaugeMaxForSkill(SkillDrone);

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeRequiredForSkill(string skillName)
    {
        if (IsReconnaissanceSkillName(skillName))
            return reconnaissanceGaugeRequirement;

        if (IsObservationSupportSkillName(skillName))
            return observationSupportGaugeRequirement;

        if (IsDroneSkillName(skillName) && highPowerBatteryEnabled)
        {
            float maxGauge = GetSkillGaugeMaxForSkill(SkillDrone);
            return maxGauge * highPowerBatteryGaugeRequirementMultiplier;
        }

        return base.GetSkillGaugeRequiredForSkill(skillName);
    }

    public override float GetSkillGaugeCurrentForSkill(string skillName)
    {
        string canonicalSkillName = GetCanonicalGaugeSkillName(skillName);
        return base.GetSkillGaugeCurrentForSkill(canonicalSkillName);
    }

    public override float GetSkillGaugeNormalizedForSkill(string skillName)
    {
        string canonicalSkillName = GetCanonicalGaugeSkillName(skillName);
        return base.GetSkillGaugeNormalizedForSkill(canonicalSkillName);
    }

    public override bool CanUseSkillGaugeForSkill(string skillName, bool showWarning = false)
    {
        if (IsReconnaissanceSkillName(skillName) && !reconnaissanceUnlocked)
        {
            if (showWarning)
                Debug.LogWarning($"[Observer {AgentID}] 정찰 스킬이 아직 해금되지 않았습니다.");

            return false;
        }

        if (IsObservationSupportSkillName(skillName) && !observationSupportUnlocked)
        {
            if (showWarning)
                Debug.LogWarning($"[Observer {AgentID}] 관측 지원 스킬이 아직 해금되지 않았습니다.");

            return false;
        }

        string canonicalSkillName = GetCanonicalGaugeSkillName(skillName);
        return base.CanUseSkillGaugeForSkill(canonicalSkillName, showWarning);
    }

    private void UnlockReconnaissance()
    {
        reconnaissanceUnlocked = true;
        Debug.Log($"[Observer {AgentID}] 정찰 스킬 해금");
    }

    private void UnlockObservationSupport()
    {
        observationSupportUnlocked = true;
        Debug.Log($"[Observer {AgentID}] 관측 지원 스킬 해금");
    }

    private void ApplyLinkedSurveillanceNetworkUpgrade()
    {
        linkedSurveillanceNetworkEnabled = true;

        UpdateLinkedSurveillanceSubscriptions(true);
        UpdateTeamVisionColorOverride();

        Debug.Log($"[Observer {AgentID}] 연계 감시망 강화 적용");
    }

    private void ApplyDroneTrackingWatchUpgrade(float value)
    {
        droneTrackingWatchEnabled = true;

        if (value > 0f)
            trackingWatchDurationMultiplier = Mathf.Clamp(value, 0.01f, 1f);

        Debug.Log(
            $"[Observer {AgentID}] 추적 감시 강화 적용. " +
            $"DroneDurationMultiplier={trackingWatchDurationMultiplier:F2}"
        );
    }

    private void ApplyHighPowerBatteryUpgrade(float value)
    {
        highPowerBatteryEnabled = true;

        if (value > 0f)
            highPowerBatteryGaugeRequirementMultiplier = Mathf.Clamp(value, 0.01f, 1f);

        Debug.Log(
            $"[Observer {AgentID}] 고출력 배터리 강화 적용. " +
            $"DroneGaugeRequirement={highPowerBatteryGaugeRequirementMultiplier * 100f:0.#}%"
        );
    }

    private void ApplyQuickResponseUpgrade(float value)
    {
        quickResponseEnabled = true;

        if (value > 0f)
            quickResponseMoveSpeedMultiplier = Mathf.Max(1f, value);

        Debug.Log(
            $"[Observer {AgentID}] 신속 대응 강화 적용. " +
            $"SharedMoveSpeedMultiplier={quickResponseMoveSpeedMultiplier:F2}"
        );
    }

    private void ApplyReconnaissanceUpgradeModule()
    {
        if (reconnaissanceUpgradeModuleEnabled)
            return;

        reconnaissanceUpgradeModuleEnabled = true;

        reconnaissanceGaugeRequirement = upgradeModuleReconnaissanceGaugeRequirement;
        reconnaissanceRadius *= upgradeModuleReconnaissanceRadiusMultiplier;

        Debug.Log(
            $"[Observer {AgentID}] 업그레이드 모듈 강화 적용. " +
            $"ReconGaugeRequirement={reconnaissanceGaugeRequirement:0.#}, " +
            $"ReconRadius={reconnaissanceRadius:0.##}"
        );
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked || isHitReactionLocked || isDroneDeployLocked)
            return;

        if (!CanReceivePlayerSkillCommand(true))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Observer {AgentID}] Skill request: {skillName}, Position: {targetPos}");

        if (IsReconnaissanceSkill(skill))
        {
            if (!reconnaissanceUnlocked)
            {
                Debug.LogWarning($"[Observer {AgentID}] 정찰 스킬이 아직 해금되지 않았습니다.");
                return;
            }

            ExecuteReconnaissanceSkill(targetPos);
            return;
        }

        if (IsObservationSupportSkill(skill))
        {
            if (!observationSupportUnlocked)
            {
                Debug.LogWarning($"[Observer {AgentID}] 관측 지원 스킬이 아직 해금되지 않았습니다.");
                return;
            }

            ExecuteObservationSupportSkill();
            return;
        }

        if (IsDroneSkill(skill))
        {
            ExecuteDroneSkill(targetPos);
            return;
        }

        if (IsTargetPositionShareSkill(skill))
        {
            bool enable = !IsTargetPositionShareOffCommand(skill);
            SetTargetPositionShareEnabled(enable);
            return;
        }

        Debug.LogWarning($"[Observer {AgentID}] Unknown skill: {skillName}");
    }

    protected override void UpdateAnimationState(bool immediate = false)
    {
        base.UpdateAnimationState(immediate);

        if (animator == null)
            return;

        if (!hasMoveModeParameter)
            return;

        ObserverMoveMode moveMode = ResolveObserverMoveMode();
        animator.SetInteger(moveModeHash, (int)moveMode);
    }

    public void SetTargetPositionShareEnabled(bool enabled)
    {
        if (targetPositionShareEnabled == enabled)
        {
            Debug.Log($"[Observer {AgentID}] Position share is already {(enabled ? "on" : "off")}.");
            return;
        }

        targetPositionShareEnabled = enabled;

        if (!targetPositionShareEnabled)
        {
            isTargetPositionSharing = false;
            ClearSharedTargetPositionFromThisObserver();
            ClearLinkedSurveillanceSubscriptions();
        }
        else
        {
            UpdateLinkedSurveillanceSubscriptions(true);
        }

        UpdateTeamVisionColorOverride();

        Debug.Log($"[Observer {AgentID}] Position share {(targetPositionShareEnabled ? "on" : "off")}");
    }

    private void UpdateTeamVisionColorOverride()
    {
        bool shouldUseTeamVisionColor =
            linkedSurveillanceNetworkEnabled &&
            targetPositionShareEnabled;

        VisionConeVisualizer.SetTeamVisionColorOverride(
            shouldUseTeamVisionColor,
            VisionConeVisualizer.GetDefaultObserverSharedVisionColor()
        );
    }

    public void ToggleTargetPositionShare()
    {
        SetTargetPositionShareEnabled(!targetPositionShareEnabled);
    }

    public void NotifyDroneTargetObserved(Transform target)
    {
        if (target == null)
            return;

        if (!targetPositionShareEnabled)
            return;

        ShareTargetPosition(target.position);
        lastTargetPositionShareTime = Time.time;
        isTargetPositionSharing = true;
    }

    public void NotifyDroneSkillRevealedTarget(Transform target)
    {
        if (target == null)
            return;

        ConstructionWorker constructionWorker =
            target.GetComponentInParent<ConstructionWorker>();

        if (constructionWorker == null)
            return;

        constructionWorker.OnTargetPositionRevealedByAgentSkill();
    }

    public override void PlayHitReaction(Vector3 hitSourcePosition)
    {
        if (isResultAnimationLocked)
            return;

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
        isDroneDeployLocked = false;

        StopDroneDeployRoutine();
        StopHitReactionRoutine();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void ExecuteDroneSkill(Vector3 targetPos)
    {
        if (!TryConsumeSkillGaugeForSkill(SkillDrone))
            return;

        if (hasDeployDroneTrigger)
        {
            if (droneDeployRoutine != null)
                StopCoroutine(droneDeployRoutine);

            droneDeployRoutine = StartCoroutine(DroneDeployRoutine(targetPos));
            return;
        }

        Debug.LogWarning($"[Observer {AgentID}] Animator parameter is missing: DeployDrone. Drone will be deployed without animation.");
        SpawnDrone(targetPos);
    }

    private void ExecuteReconnaissanceSkill(Vector3 targetPos)
    {
        if (!TryConsumeSkillGaugeForSkill(SkillReconnaissance))
            return;

        if (hasDeployDroneTrigger)
        {
            if (droneDeployRoutine != null)
                StopCoroutine(droneDeployRoutine);

            droneDeployRoutine = StartCoroutine(ReconnaissanceDeployRoutine(targetPos));
            return;
        }

        Debug.LogWarning($"[Observer {AgentID}] Animator parameter is missing: DeployDrone. Reconnaissance will start without animation.");
        SpawnReconnaissance(targetPos);
    }

    private void ExecuteObservationSupportSkill()
    {
        if (!TryConsumeSkillGaugeForSkill(SkillObservationSupport))
            return;

        if (observationSupportRoutine != null)
            StopObservationSupport();

        observationSupportRoutine = StartCoroutine(ObservationSupportRoutine());

        Debug.Log(
            $"[Observer {AgentID}] 관측 지원 사용. " +
            $"Duration={observationSupportDuration:0.#}, " +
            $"ViewRadiusMultiplier={observationSupportViewRadiusMultiplier:0.##}"
        );
    }

    private IEnumerator DroneDeployRoutine(Vector3 targetPos)
    {
        isDroneDeployLocked = true;

        if (stopWhenDeployDrone)
            ForceStopForSkill();

        UpdateAnimationState(true);

        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);

        animator.SetTrigger(deployDroneHash);

        float spawnDelay = Mathf.Clamp(droneSpawnDelay, 0f, droneDeployLockSeconds);

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        SpawnDrone(targetPos);

        float remainTime = droneDeployLockSeconds - spawnDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isDroneDeployLocked = false;
        droneDeployRoutine = null;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private IEnumerator ReconnaissanceDeployRoutine(Vector3 targetPos)
    {
        isDroneDeployLocked = true;

        if (stopWhenDeployDrone)
            ForceStopForSkill();

        UpdateAnimationState(true);

        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);

        animator.SetTrigger(deployDroneHash);

        float spawnDelay = Mathf.Clamp(droneSpawnDelay, 0f, droneDeployLockSeconds);

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        SpawnReconnaissance(targetPos);

        float remainTime = droneDeployLockSeconds - spawnDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isDroneDeployLocked = false;
        droneDeployRoutine = null;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private IEnumerator ObservationSupportRoutine()
    {
        ApplyObservationSupportToTeam();

        if (observationSupportDuration > 0f)
            yield return new WaitForSeconds(observationSupportDuration);

        RemoveObservationSupportFromTeam();
        observationSupportRoutine = null;
    }

    private void SpawnDrone(Vector3 targetPos)
    {
        if (replaceExistingDrone)
            DestroyCurrentDrone();

        Vector3 observationCenter = targetPos;
        Vector3 droneVisualPosition = GetDroneVisualPosition(observationCenter);
        float finalDroneDuration = GetCurrentDroneDuration();

        Drone drone = CreateDrone(droneVisualPosition);

        if (drone == null)
        {
            Debug.LogWarning($"[Observer {AgentID}] Failed to create drone.");
            return;
        }

        drone.Initialize(
            this,
            observationCenter,
            droneVisualPosition,
            targetLayer,
            droneRadius,
            finalDroneDuration,
            droneTrackingWatchEnabled
        );

        currentDrone = drone;

        RequestInstalledObjectCamera(drone.transform);

        Debug.Log(
            $"[Observer {AgentID}] Drone deployed. " +
            $"Observation Center: {observationCenter}, " +
            $"Drone Position: {droneVisualPosition}, " +
            $"Radius: {droneRadius}, Duration: {finalDroneDuration}, " +
            $"TrackingWatch={droneTrackingWatchEnabled}"
        );
    }

    private void SpawnReconnaissance(Vector3 targetPos)
    {
        if (replaceExistingReconnaissance)
            DestroyCurrentReconnaissance();

        Vector3 startCenter = transform.position;
        Vector3 droneVisualPosition = GetDroneVisualPosition(startCenter);
        Vector3 direction = targetPos - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        Reconnaissance reconnaissance = CreateReconnaissance(droneVisualPosition, direction);

        if (reconnaissance == null)
        {
            Debug.LogWarning($"[Observer {AgentID}] Failed to create reconnaissance.");
            return;
        }

        reconnaissance.Initialize(
            this,
            startCenter,
            droneVisualPosition,
            direction,
            targetLayer,
            reconnaissanceRadius,
            reconnaissanceMaxDistance,
            reconnaissanceFlightSpeed,
            reconnaissanceRevealHoldDuration
        );

        if (reconnaissanceSkilledPilotEnabled)
        {
            reconnaissance.SetCurvedFlightEnabled(
                true,
                skilledPilotCurveAmplitude,
                skilledPilotCurveWaveLength
            );
        }

        currentReconnaissance = reconnaissance;

        if (requestCameraOnReconnaissance)
            RequestInstalledObjectCamera(reconnaissance.transform);

        Debug.Log(
            $"[Observer {AgentID}] Reconnaissance started. " +
            $"Start Center: {startCenter}, " +
            $"Direction: {direction}, " +
            $"Radius: {reconnaissanceRadius}, " +
            $"MaxDistance: {reconnaissanceMaxDistance}, " +
            $"FlightSpeed: {reconnaissanceFlightSpeed}"
        );
    }

    private float GetCurrentDroneDuration()
    {
        if (!droneTrackingWatchEnabled)
            return droneDuration;

        return droneDuration * trackingWatchDurationMultiplier;
    }

    private Vector3 GetDroneVisualPosition(Vector3 observationCenter)
    {
        return new Vector3(
            observationCenter.x,
            observationCenter.y + droneSpawnHeight,
            observationCenter.z
        );
    }

    private Drone CreateDrone(Vector3 droneVisualPosition)
    {
        if (dronePrefab != null)
        {
            Drone drone = Instantiate(
                dronePrefab,
                droneVisualPosition,
                Quaternion.identity,
                droneParent
            );

            drone.name = $"ObserverDrone_Agent{AgentID}";
            return drone;
        }

        GameObject droneObject = new GameObject($"ObserverDrone_Agent{AgentID}");
        droneObject.transform.position = droneVisualPosition;

        if (droneParent != null)
            droneObject.transform.SetParent(droneParent);

        return droneObject.AddComponent<Drone>();
    }

    private Reconnaissance CreateReconnaissance(Vector3 droneVisualPosition, Vector3 direction)
    {
        Quaternion rotation = Quaternion.identity;

        if (direction.sqrMagnitude > 0.0001f)
            rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (reconnaissancePrefab != null)
        {
            Reconnaissance reconnaissance = Instantiate(
                reconnaissancePrefab,
                droneVisualPosition,
                rotation,
                droneParent
            );

            reconnaissance.name = $"ObserverReconnaissance_Agent{AgentID}";
            return reconnaissance;
        }

        GameObject reconnaissanceObject = new GameObject($"ObserverReconnaissance_Agent{AgentID}");
        reconnaissanceObject.transform.position = droneVisualPosition;
        reconnaissanceObject.transform.rotation = rotation;

        if (droneParent != null)
            reconnaissanceObject.transform.SetParent(droneParent);

        return reconnaissanceObject.AddComponent<Reconnaissance>();
    }

    private void DestroyCurrentDrone()
    {
        if (currentDrone == null)
            return;

        Destroy(currentDrone.gameObject);
        currentDrone = null;
    }

    private void DestroyCurrentReconnaissance()
    {
        if (currentReconnaissance == null)
            return;

        Destroy(currentReconnaissance.gameObject);
        currentReconnaissance = null;
    }

    private void ApplyObservationSupportToTeam()
    {
        EnsureAgentCache(true);
        RemoveObservationSupportFromTeam();

        if (cachedAgents == null)
            return;

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            AgentController agent = cachedAgents[i];

            if (agent == null)
                continue;

            if (!agent.isActiveAndEnabled)
                continue;

            if (agent == this && !includeSelfInObservationSupport)
                continue;

            VisionSensor sensor = agent.VisionSensor;

            if (sensor == null)
                continue;

            sensor.SetExternalViewRadiusMultiplier(this, observationSupportViewRadiusMultiplier);

            if (observationSupportHawkeyeEnabled)
                sensor.SetExternalViewAngleOffset(this, hawkeyeViewAngleOffset);

            observationSupportSensors.Add(sensor);
        }
    }

    private void RemoveObservationSupportFromTeam()
    {
        if (observationSupportSensors.Count == 0)
            return;

        foreach (VisionSensor sensor in observationSupportSensors)
        {
            if (sensor == null)
                continue;

            sensor.RemoveExternalViewRadiusMultiplier(this);
            sensor.RemoveExternalViewAngleOffset(this);
        }

        observationSupportSensors.Clear();
        observationSupportSensorsToRemove.Clear();
    }

    private void StopObservationSupport()
    {
        if (observationSupportRoutine != null)
        {
            StopCoroutine(observationSupportRoutine);
            observationSupportRoutine = null;
        }

        RemoveObservationSupportFromTeam();
    }

    private void UpdateTargetPositionShareFromVision()
    {
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
        lastTargetPositionShareTime = Time.time;
        isTargetPositionSharing = true;
    }

    private void UpdateTargetPositionShareState()
    {
        if (!isTargetPositionSharing)
            return;

        if (Time.time - lastTargetPositionShareTime <= 0.2f)
            return;

        isTargetPositionSharing = false;
    }

    private void ShareTargetPosition(Vector3 targetPosition)
    {
        ShareTargetPosition(targetPosition, false);
    }

    private void ShareTargetPosition(Vector3 targetPosition, bool includeObserverSelf)
    {
        EnsureAgentCache(false);

        if (cachedAgents == null)
            return;

        float moveSpeedMultiplier = quickResponseEnabled ? quickResponseMoveSpeedMultiplier : 1f;

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            AgentController agent = cachedAgents[i];

            if (agent == null)
                continue;

            if (!agent.isActiveAndEnabled)
                continue;

            bool isSelf = agent == this;

            if (isSelf && !includeObserverSelf && !includeSelfInTargetPositionShare)
                continue;

            agent.ReceiveSharedTargetPosition(targetPosition, this, moveSpeedMultiplier);

            if (debugPositionShareTargets)
            {
                Debug.Log(
                    $"[Observer {AgentID}] Share target position to {agent.GetType().Name} AgentID: {agent.AgentID}"
                );
            }
        }
    }

    private void ClearSharedTargetPositionFromThisObserver()
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

        RefreshCachedAgents();
    }

    private void RefreshCachedAgents()
    {
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

    private void UpdateLinkedSurveillanceSubscriptions(bool forceRefresh)
    {
        if (!linkedSurveillanceNetworkEnabled)
        {
            ClearLinkedSurveillanceSubscriptions();
            return;
        }

        EnsureAgentCache(forceRefresh);
        RemoveInvalidLinkedSurveillanceSubscriptions();

        if (cachedAgents == null)
            return;

        for (int i = 0; i < cachedAgents.Length; i++)
        {
            AgentController agent = cachedAgents[i];

            if (agent == null || agent == this)
                continue;

            VisionSensor sensor = agent.VisionSensor;

            if (sensor == null)
                continue;

            if (linkedSurveillanceSensors.Add(sensor))
                sensor.OnVisionChanged += HandleLinkedSurveillanceVisionChanged;
        }
    }

    private void RemoveInvalidLinkedSurveillanceSubscriptions()
    {
        if (linkedSurveillanceSensors.Count == 0)
            return;

        linkedSurveillanceSensorsToRemove.Clear();

        foreach (VisionSensor sensor in linkedSurveillanceSensors)
        {
            if (sensor == null || !sensor.isActiveAndEnabled || sensor.owner == this)
                linkedSurveillanceSensorsToRemove.Add(sensor);
        }

        for (int i = 0; i < linkedSurveillanceSensorsToRemove.Count; i++)
        {
            VisionSensor sensor = linkedSurveillanceSensorsToRemove[i];

            if (sensor != null)
                sensor.OnVisionChanged -= HandleLinkedSurveillanceVisionChanged;

            linkedSurveillanceSensors.Remove(sensor);
        }

        linkedSurveillanceSensorsToRemove.Clear();
    }

    private void ClearLinkedSurveillanceSubscriptions()
    {
        if (linkedSurveillanceSensors.Count == 0)
            return;

        foreach (VisionSensor sensor in linkedSurveillanceSensors)
        {
            if (sensor != null)
                sensor.OnVisionChanged -= HandleLinkedSurveillanceVisionChanged;
        }

        linkedSurveillanceSensors.Clear();
        linkedSurveillanceSensorsToRemove.Clear();
    }

    private void UpdateLinkedSurveillanceCurrentVision()
    {
        if (!linkedSurveillanceNetworkEnabled || !targetPositionShareEnabled)
            return;

        if (Time.time < nextLinkedSurveillanceShareTime)
            return;

        nextLinkedSurveillanceShareTime = Time.time + LinkedSurveillanceShareInterval;

        foreach (VisionSensor sensor in linkedSurveillanceSensors)
        {
            if (sensor == null || sensor.owner == this)
                continue;

            if (!sensor.IsSeeingTarget)
                continue;

            Transform seenTarget = sensor.CurrentSeenTarget;

            if (seenTarget == null)
                continue;

            ShareTargetPosition(seenTarget.position, true);
            lastTargetPositionShareTime = Time.time;
            isTargetPositionSharing = true;
        }
    }

    private void HandleLinkedSurveillanceVisionChanged(VisionSensor sensor, bool isSeeingTarget, Transform target)
    {
        if (!linkedSurveillanceNetworkEnabled)
            return;

        if (!targetPositionShareEnabled)
            return;

        if (!isSeeingTarget || target == null)
            return;

        if (sensor == null || sensor.owner == this)
            return;

        ShareTargetPosition(target.position, true);
        lastTargetPositionShareTime = Time.time;
        isTargetPositionSharing = true;
    }

    private void ForceStopForSkill()
    {
        currentTarget = null;
        isManualMoving = false;

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

    private void KeepStopped()
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

    private ObserverMoveMode ResolveObserverMoveMode()
    {
        if (isResultAnimationLocked || isHitReactionLocked || isDroneDeployLocked)
            return ObserverMoveMode.IdleLookAround;

        bool isMoving = ResolveObserverIsMoving();

        if (!isMoving)
            return ObserverMoveMode.IdleLookAround;

        if (IsSmokeDebuffed)
            return ObserverMoveMode.DebuffedRun;

        return ObserverMoveMode.Run;
    }

    private bool ResolveObserverIsMoving()
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
            HasActivePathForObserverAnimation();

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > AnimationMovingThreshold * AnimationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > AnimationMovingThreshold * AnimationMovingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForObserverAnimation();

        return hasMovementIntent && (hasVelocity || hasNotReachedDestination);
    }

    private bool HasActivePathForObserverAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForObserverAnimation();
    }

    private bool HasReachedDestinationForObserverAnimation()
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
        return navAgent.remainingDistance <= stopDistance + DestinationBuffer;
    }

    private IEnumerator HitReactionRoutine(Vector3 hitSourcePosition)
    {
        isHitReactionLocked = true;

        StopDroneDeployRoutine();

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
            Debug.LogWarning($"[Observer {AgentID}] Animator is missing. Cannot play {triggerName} animation.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Observer {AgentID}] Animator parameter is missing: {triggerName}");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;
        isDroneDeployLocked = false;

        StopDroneDeployRoutine();
        StopHitReactionRoutine();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStopped();
        UpdateAnimationState(true);

        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);
        ResetAnimatorTrigger(deployDroneHash, hasDeployDroneTrigger);

        animator.SetTrigger(triggerHash);

        Debug.Log($"[Observer {AgentID}] {triggerName} animation played.");
    }

    private void StopDroneDeployRoutine()
    {
        if (droneDeployRoutine == null)
            return;

        StopCoroutine(droneDeployRoutine);
        droneDeployRoutine = null;
        isDroneDeployLocked = false;
    }

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
        isHitReactionLocked = false;
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

    private void CacheObserverAnimationHashes()
    {
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
        deployDroneHash = Animator.StringToHash(DeployDroneTriggerName);
    }

    private void CacheObserverAnimatorParameters()
    {
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasHitReactionTrigger = HasAnimatorParameter(HitReactionTriggerName, AnimatorControllerParameterType.Trigger);
        hasVictoryTrigger = HasAnimatorParameter(VictoryTriggerName, AnimatorControllerParameterType.Trigger);
        hasDefeatTrigger = HasAnimatorParameter(DefeatTriggerName, AnimatorControllerParameterType.Trigger);
        hasDeployDroneTrigger = HasAnimatorParameter(DeployDroneTriggerName, AnimatorControllerParameterType.Trigger);
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

        Debug.LogWarning($"[Observer {AgentID}] Animator parameter is missing: {parameterName} ({parameterType})");
        return false;
    }

    private string GetCanonicalGaugeSkillName(string skillName)
    {
        if (IsReconnaissanceSkillName(skillName))
            return SkillReconnaissance;

        if (IsObservationSupportSkillName(skillName))
            return SkillObservationSupport;

        if (IsDroneSkillName(skillName))
            return SkillDrone;

        return skillName;
    }

    private bool IsDroneSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        return IsDroneSkill(skillName.Trim().ToLower());
    }

    private bool IsReconnaissanceSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        return IsReconnaissanceSkill(skillName.Trim().ToLower());
    }

    private bool IsObservationSupportSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        return IsObservationSupportSkill(skillName.Trim().ToLower());
    }

    private bool IsDroneSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillDrone) ||
               skill.Contains("드론");
    }

    private bool IsReconnaissanceSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillReconnaissance) ||
               skill.Contains("recon") ||
               skill.Contains("scout") ||
               skill.Contains("정찰");
    }

    private bool IsObservationSupportSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillObservationSupport) ||
               skill.Contains("observation support") ||
               skill.Contains("vision support") ||
               skill.Contains("관측지원") ||
               skill.Contains("관측 지원") ||
               skill.Contains("시야지원") ||
               skill.Contains("시야 지원");
    }

    private bool IsTargetPositionShareSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillPositionShare) ||
               skill.Contains("position share") ||
               skill.Contains("target position share") ||
               skill.Contains("위치공유") ||
               skill.Contains("위치 공유");
    }

    private bool IsTargetPositionShareOffCommand(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("off") ||
               skill.Contains("disable") ||
               skill.Contains("stop") ||
               skill.Contains("해제") ||
               skill.Contains("끄기") ||
               skill.Contains("중지");
    }
}