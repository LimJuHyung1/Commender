using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Profiler : AgentController, IUpgradeReceiver
{
    private enum ProfilerMoveMode
    {
        IdleLookAround = 0,
        Run = 1,
        Reserved = 2,
        DebuffedRun = 3
    }

    private const string SkillEscapePatternAnalysis = "escape_pattern_analysis";
    private const string SkillBehaviorBriefing = "behavior_briefing";
    private const string SkillLinkedAnalysis = "linked_analysis";
    private const string SkillRouteIdentification = "route_identification";

    private const string UpgradeUnlockLinkedAnalysis = "profiler_unlock_linked_analysis";
    private const string UpgradeUnlockRouteIdentification = "profiler_unlock_route_identification";

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";
    private const string BriefingTriggerName = "Briefing";
    private const string RouteIdentificationTriggerName = "RouteIdentification";

    [Header("Target Reference")]
    [SerializeField] private TargetController targetController;
    [SerializeField] private bool autoFindTarget = true;

    [Header("Escape Pattern Analysis")]
    [SerializeField] private float escapePatternAnalysisGaugeMax = 100f;
    [SerializeField] private float escapePatternGaugeGainPerEscape = 10f;
    [SerializeField] private float escapePatternGainCooldown = 1f;
    [SerializeField] private float escapePatternTargetSpeedMultiplier = 0.8f;
    [SerializeField] private float escapePatternSlowDuration = 6f;

    [Header("Behavior Briefing")]
    [SerializeField] private float behaviorBriefingGaugeMax = 100f;
    [SerializeField] private float behaviorBriefingInitialSightGaugeGain = 25f;
    [SerializeField] private float behaviorBriefingSustainedGaugePerSecond = 5f;
    [SerializeField] private float behaviorBriefingDuration = 8f;
    [SerializeField] private float behaviorBriefingMoveSpeedMultiplier = 1.2f;
    [SerializeField] private float behaviorBriefingAllyGaugeGainOnUse = 0f;
    [SerializeField] private bool behaviorBriefingIncludesSelf = true;

    [Header("Linked Analysis")]
    [SerializeField] private bool linkedAnalysisUnlockedByDefault = false;
    [SerializeField] private float linkedAnalysisGaugeMax = 100f;
    [SerializeField] private float linkedAnalysisGaugeGainPerAllySkill = 20f;
    [SerializeField] private float linkedAnalysisTargetRevealDuration = 5f;
    [SerializeField] private bool linkedAnalysisBlocksTargetSkill = false;
    [SerializeField] private bool countProfilerSkillsForLinkedAnalysis = false;

    [Header("Route Identification")]
    [SerializeField] private bool routeIdentificationUnlockedByDefault = false;
    [SerializeField] private float routeIdentificationGaugeMax = 100f;
    [SerializeField] private float routeIdentificationGaugeGainPerMeter = 1f;
    [SerializeField] private float routeIdentificationDuration = 10f;
    [SerializeField] private float routeIdentificationViewRadiusMultiplier = 1.25f;
    [SerializeField] private float routeIdentificationViewAngleBonus = 20f;
    [SerializeField] private float routeIdentificationTargetHealthDrainMultiplier = 1f;
    [SerializeField] private float routeIdentificationTargetHealthDrainDuration = 5f;

    [Header("Profiler Animation")]
    [SerializeField] private float animationMovingThreshold = 0.05f;
    [SerializeField] private float destinationBuffer = 0.2f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private bool stopWhenUseSkill = true;

    [Header("Skill Animation Timing")]
    [SerializeField] private float briefingAnimationLockSeconds = 0.8f;
    [SerializeField] private float briefingEffectDelay = 0.3f;
    [SerializeField] private float routeIdentificationAnimationLockSeconds = 0.8f;
    [SerializeField] private float routeIdentificationEffectDelay = 0.3f;

    [Header("Skill Camera")]
    [SerializeField] private bool requestCameraOnBriefing = true;
    [SerializeField] private SkillCameraFocusMode briefingCameraFocusMode = SkillCameraFocusMode.UserOnly;
    [SerializeField] private bool requestCameraOnRouteIdentification = true;
    [SerializeField] private SkillCameraFocusMode routeIdentificationCameraFocusMode = SkillCameraFocusMode.UserOnly;

    [Header("Hit Animation")]
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Coroutine escapePatternRoutine;
    private Coroutine behaviorBriefingRoutine;
    private Coroutine linkedAnalysisRoutine;
    private Coroutine routeIdentificationRoutine;
    private Coroutine targetRevealRoutine;
    private Coroutine hitReactionRoutine;

    private TargetEscapeMotor activeSlowedTargetMotor;
    private TargetController revealedTargetController;
    private TargetSkillController linkedAnalysisBlockedSkillController;

    private TargetController routeIdentificationWeakPointTarget;
    private float routeIdentificationWeakPointExpireTime = -999f;

    private bool isBehaviorBriefingActive;
    private bool isLinkedAnalysisActive;
    private bool isRouteIdentificationActive;

    private bool isBehaviorBriefingAnimationLocked;
    private bool isRouteIdentificationAnimationLocked;
    private bool isHitReactionLocked;
    private bool isResultAnimationLocked;

    private bool hasSkillAnimationNavigationSnapshot;
    private bool skillAnimationPreviousStopped;
    private bool skillAnimationPreviousUpdateRotation;

    private float lastEscapePatternGaugeGainTime = -999f;

    private readonly List<AgentMoveSnapshot> behaviorBriefingSnapshots = new List<AgentMoveSnapshot>();

    private float originalSpotLightRange;
    private float originalSpotLightOuterAngle;
    private bool hasSpotLightSnapshot;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;
    private int briefingHash;
    private int routeIdentificationHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;
    private bool hasBriefingTrigger;
    private bool hasRouteIdentificationTrigger;

    public bool CanUseLinkedAnalysisSkill => linkedAnalysisUnlockedByDefault || IsAgentUpgradeUnlocked(UpgradeUnlockLinkedAnalysis);
    public bool CanUseRouteIdentificationSkill => routeIdentificationUnlockedByDefault || IsAgentUpgradeUnlocked(UpgradeUnlockRouteIdentification);

    protected override float SkillGaugeChargeMultiplier => 0f;

    protected override void Awake()
    {
        agentID = 4;

        CacheProfilerAnimationHashes();

        base.Awake();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheProfilerAnimatorParameters();
        CacheTargetIfNeeded();
        UpdateAnimationState(true);
    }

    private void OnEnable()
    {
        TargetEscapeMotor.OnAnyEscapeAction += HandleTargetEscapeAction;
        AgentSkillUseEventBus.OnAgentSkillExecuted += HandleAgentSkillExecuted;

        if (visionSensor != null)
            visionSensor.OnVisionChanged += HandleVisionChanged;
    }

    protected override void OnDisable()
    {
        TargetEscapeMotor.OnAnyEscapeAction -= HandleTargetEscapeAction;
        AgentSkillUseEventBus.OnAgentSkillExecuted -= HandleAgentSkillExecuted;

        if (visionSensor != null)
            visionSensor.OnVisionChanged -= HandleVisionChanged;

        StopAllProfilerEffects();
        StopHitReactionRoutine();

        isBehaviorBriefingAnimationLocked = false;
        isRouteIdentificationAnimationLocked = false;
        isHitReactionLocked = false;
        isResultAnimationLocked = false;

        RestoreNavigationAfterSkillAnimation();

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

        if ((isBehaviorBriefingAnimationLocked || isRouteIdentificationAnimationLocked) && stopWhenUseSkill)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        base.Update();

        UpdateBehaviorBriefingSustainedSightGauge();
        UpdateRouteIdentificationWeakPointDrain();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        escapePatternAnalysisGaugeMax = Mathf.Max(0f, escapePatternAnalysisGaugeMax);
        escapePatternGaugeGainPerEscape = Mathf.Max(0f, escapePatternGaugeGainPerEscape);
        escapePatternGainCooldown = Mathf.Max(0f, escapePatternGainCooldown);
        escapePatternTargetSpeedMultiplier = Mathf.Clamp(escapePatternTargetSpeedMultiplier, 0.05f, 1f);
        escapePatternSlowDuration = Mathf.Max(0f, escapePatternSlowDuration);

        behaviorBriefingGaugeMax = Mathf.Max(0f, behaviorBriefingGaugeMax);
        behaviorBriefingInitialSightGaugeGain = Mathf.Max(0f, behaviorBriefingInitialSightGaugeGain);
        behaviorBriefingSustainedGaugePerSecond = Mathf.Max(0f, behaviorBriefingSustainedGaugePerSecond);
        behaviorBriefingDuration = Mathf.Max(0f, behaviorBriefingDuration);
        behaviorBriefingMoveSpeedMultiplier = Mathf.Max(1f, behaviorBriefingMoveSpeedMultiplier);
        behaviorBriefingAllyGaugeGainOnUse = Mathf.Max(0f, behaviorBriefingAllyGaugeGainOnUse);

        linkedAnalysisGaugeMax = Mathf.Max(0f, linkedAnalysisGaugeMax);
        linkedAnalysisGaugeGainPerAllySkill = Mathf.Max(0f, linkedAnalysisGaugeGainPerAllySkill);
        linkedAnalysisTargetRevealDuration = Mathf.Max(0f, linkedAnalysisTargetRevealDuration);

        routeIdentificationGaugeMax = Mathf.Max(0f, routeIdentificationGaugeMax);
        routeIdentificationGaugeGainPerMeter = Mathf.Max(0f, routeIdentificationGaugeGainPerMeter);
        routeIdentificationDuration = Mathf.Max(0f, routeIdentificationDuration);
        routeIdentificationViewRadiusMultiplier = Mathf.Max(1f, routeIdentificationViewRadiusMultiplier);
        routeIdentificationViewAngleBonus = Mathf.Max(0f, routeIdentificationViewAngleBonus);
        routeIdentificationTargetHealthDrainMultiplier = Mathf.Max(1f, routeIdentificationTargetHealthDrainMultiplier);
        routeIdentificationTargetHealthDrainDuration = Mathf.Max(0f, routeIdentificationTargetHealthDrainDuration);

        animationMovingThreshold = Mathf.Max(0f, animationMovingThreshold);
        destinationBuffer = Mathf.Max(0f, destinationBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);

        briefingAnimationLockSeconds = Mathf.Max(0f, briefingAnimationLockSeconds);
        briefingEffectDelay = Mathf.Max(0f, briefingEffectDelay);
        routeIdentificationAnimationLockSeconds = Mathf.Max(0f, routeIdentificationAnimationLockSeconds);
        routeIdentificationEffectDelay = Mathf.Max(0f, routeIdentificationEffectDelay);

        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        CacheProfilerAnimationHashes();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillBehaviorBriefing))
        {
            TryUseBehaviorBriefing();
            return;
        }

        if (skill == NormalizeSkillKey(SkillRouteIdentification))
        {
            TryUseRouteIdentification();
            return;
        }

        Debug.LogWarning($"[Profiler {AgentID}] 처리할 수 없는 스킬입니다. skill={skillName}");
    }

    public override float GetSkillGaugeMaxForSkill(string skillName)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillEscapePatternAnalysis))
            return escapePatternAnalysisGaugeMax;

        if (skill == NormalizeSkillKey(SkillBehaviorBriefing))
            return behaviorBriefingGaugeMax;

        if (skill == NormalizeSkillKey(SkillLinkedAnalysis))
            return linkedAnalysisGaugeMax;

        if (skill == NormalizeSkillKey(SkillRouteIdentification))
            return routeIdentificationGaugeMax;

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeRequiredForSkill(string skillName)
    {
        return GetSkillGaugeMaxForSkill(skillName);
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        List<string> keys = new List<string>
        {
            SkillEscapePatternAnalysis,
            SkillBehaviorBriefing
        };

        if (CanUseLinkedAnalysisSkill)
            keys.Add(SkillLinkedAnalysis);

        if (CanUseRouteIdentificationSkill)
            keys.Add(SkillRouteIdentification);

        return keys.ToArray();
    }

    protected override void OnAgentMoved(float movedDistance)
    {
        if (!CanUseRouteIdentificationSkill)
            return;

        if (isRouteIdentificationActive)
            return;

        if (movedDistance <= 0f)
            return;

        if (!CanChargeRouteIdentificationFromMovement())
            return;

        AddSkillGaugeForSkill(
            SkillRouteIdentification,
            movedDistance * routeIdentificationGaugeGainPerMeter
        );
    }

    public override void ResetSkillGauge()
    {
        base.ResetSkillGauge();
        StopAllProfilerEffects();
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return CanApplyUpgradeByAgentDefinitionOrLegacy(
            upgrade,
            CommanderAgentType.Profiler
        );
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        if (upgrade.IsUnlockSkillUpgrade)
            return;

        ApplyProfilerUpgradeValue(upgrade);
    }

    private void HandleTargetEscapeAction(TargetEscapeMotor escapeMotor)
    {
        if (escapeMotor == null)
            return;

        if (Time.time - lastEscapePatternGaugeGainTime < escapePatternGainCooldown)
            return;

        lastEscapePatternGaugeGainTime = Time.time;

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 도주 행동 감지. 도주 패턴 분석 게이지 획득.");

        AddSkillGaugeForSkill(SkillEscapePatternAnalysis, escapePatternGaugeGainPerEscape);
        TryAutoActivateEscapePatternAnalysis(escapeMotor);
    }

    private void TryAutoActivateEscapePatternAnalysis(TargetEscapeMotor escapeMotor)
    {
        if (escapePatternRoutine != null)
            return;

        if (!CanUseSkillGaugeForSkill(SkillEscapePatternAnalysis, false))
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillEscapePatternAnalysis))
            return;

        if (escapeMotor == null)
            escapeMotor = ResolveTargetEscapeMotor();

        if (escapeMotor == null)
            return;

        escapePatternRoutine = StartCoroutine(EscapePatternAnalysisRoutine(escapeMotor));
    }

    private IEnumerator EscapePatternAnalysisRoutine(TargetEscapeMotor escapeMotor)
    {
        activeSlowedTargetMotor = escapeMotor;

        if (activeSlowedTargetMotor != null)
        {
            activeSlowedTargetMotor.SetExternalSpeedMultiplier(
                this,
                escapePatternTargetSpeedMultiplier
            );
        }

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 도주 패턴 분석 발동. 타겟 이동속도 감소.");

        yield return new WaitForSeconds(escapePatternSlowDuration);

        if (activeSlowedTargetMotor != null)
            activeSlowedTargetMotor.RemoveExternalSpeedMultiplier(this);

        activeSlowedTargetMotor = null;
        escapePatternRoutine = null;
    }

    private void HandleVisionChanged(VisionSensor sensor, bool isSeeingTarget, Transform seenTarget)
    {
        if (!isSeeingTarget)
            return;

        if (seenTarget == null)
            return;

        TargetController seenTargetController = ResolveTargetControllerFromTransform(seenTarget);

        if (seenTargetController == null)
            return;

        targetController = seenTargetController;

        if (isBehaviorBriefingActive)
            return;

        AddSkillGaugeForSkill(SkillBehaviorBriefing, behaviorBriefingInitialSightGaugeGain);

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 타겟 최초 포착. 브리핑 게이지 획득.");
    }

    private void UpdateBehaviorBriefingSustainedSightGauge()
    {
        if (isBehaviorBriefingActive)
            return;

        if (visionSensor == null)
            return;

        if (!visionSensor.IsSeeingTarget)
            return;

        if (visionSensor.CurrentSeenTarget == null)
            return;

        if (ResolveTargetControllerFromTransform(visionSensor.CurrentSeenTarget) == null)
            return;

        float gain = behaviorBriefingSustainedGaugePerSecond * Time.deltaTime;

        if (gain <= 0f)
            return;

        AddSkillGaugeForSkill(SkillBehaviorBriefing, gain);
    }

    private void TryUseBehaviorBriefing()
    {
        if (isBehaviorBriefingActive)
            return;

        if (isBehaviorBriefingAnimationLocked)
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillBehaviorBriefing))
            return;

        if (behaviorBriefingRoutine != null)
            StopBehaviorBriefingRoutine();

        behaviorBriefingRoutine = StartCoroutine(BehaviorBriefingRoutine());
    }

    private IEnumerator BehaviorBriefingRoutine()
    {
        isBehaviorBriefingActive = true;
        isBehaviorBriefingAnimationLocked = true;

        bool shouldPlayAnimation = animator != null && hasBriefingTrigger;
        float lockSeconds = shouldPlayAnimation ? briefingAnimationLockSeconds : 0f;
        float effectDelay = shouldPlayAnimation ? Mathf.Clamp(briefingEffectDelay, 0f, lockSeconds) : 0f;

        StopMovementForSkillAnimation();
        UpdateAnimationState(true);

        PlayProfilerSkillCinematic(
            requestCameraOnBriefing,
            briefingCameraFocusMode
        );

        if (shouldPlayAnimation)
            PlaySkillTrigger(briefingHash, BriefingTriggerName);

        if (effectDelay > 0f)
            yield return new WaitForSeconds(effectDelay);

        ApplyBehaviorBriefingMoveSpeedBuff();
        GrantBehaviorBriefingAllyGauge();

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 브리핑 발동. 아군 이동속도 증가.");

        float remainingLockSeconds = Mathf.Max(0f, lockSeconds - effectDelay);

        if (remainingLockSeconds > 0f)
            yield return new WaitForSeconds(remainingLockSeconds);

        isBehaviorBriefingAnimationLocked = false;
        RestoreNavigationAfterSkillAnimation();
        UpdateAnimationState(true);

        yield return new WaitForSeconds(behaviorBriefingDuration);

        RestoreBehaviorBriefingMoveSpeedBuff();

        isBehaviorBriefingActive = false;
        behaviorBriefingRoutine = null;
    }

    private void GrantBehaviorBriefingAllyGauge()
    {
        if (behaviorBriefingAllyGaugeGainOnUse <= 0f)
            return;

        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        if (agents == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!behaviorBriefingIncludesSelf && agent == this)
                continue;

            agent.AddSkillGauge(behaviorBriefingAllyGaugeGainOnUse);
        }

        if (debugLog)
        {
            Debug.Log(
                $"[Profiler {AgentID}] 행동 지침 적용. " +
                $"아군 스킬 게이지 +{behaviorBriefingAllyGaugeGainOnUse:0.#}"
            );
        }
    }

    private void ApplyBehaviorBriefingMoveSpeedBuff()
    {
        RestoreBehaviorBriefingMoveSpeedBuff();

        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        if (agents == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!behaviorBriefingIncludesSelf && agent == this)
                continue;

            NavMeshAgent targetNavAgent = agent.GetComponent<NavMeshAgent>();

            if (targetNavAgent == null)
                continue;

            AgentMoveSnapshot snapshot = new AgentMoveSnapshot(
                targetNavAgent,
                targetNavAgent.speed,
                targetNavAgent.acceleration
            );

            behaviorBriefingSnapshots.Add(snapshot);

            targetNavAgent.speed *= behaviorBriefingMoveSpeedMultiplier;
            targetNavAgent.acceleration *= behaviorBriefingMoveSpeedMultiplier;
        }
    }

    private void RestoreBehaviorBriefingMoveSpeedBuff()
    {
        for (int i = 0; i < behaviorBriefingSnapshots.Count; i++)
        {
            AgentMoveSnapshot snapshot = behaviorBriefingSnapshots[i];

            if (snapshot == null || snapshot.NavAgent == null)
                continue;

            snapshot.NavAgent.speed = snapshot.OriginalSpeed;
            snapshot.NavAgent.acceleration = snapshot.OriginalAcceleration;
        }

        behaviorBriefingSnapshots.Clear();
    }

    private void HandleAgentSkillExecuted(AgentController agent, string skillKey)
    {
        if (agent == null)
            return;

        if (agent == this && !countProfilerSkillsForLinkedAnalysis)
            return;

        if (string.IsNullOrWhiteSpace(skillKey))
            return;

        if (!CanUseLinkedAnalysisSkill)
            return;

        if (linkedAnalysisRoutine != null)
            return;

        AddSkillGaugeForSkill(SkillLinkedAnalysis, linkedAnalysisGaugeGainPerAllySkill);
        TryAutoActivateLinkedAnalysis();
    }

    private void TryAutoActivateLinkedAnalysis()
    {
        if (linkedAnalysisRoutine != null)
            return;

        if (!CanUseSkillGaugeForSkill(SkillLinkedAnalysis, false))
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillLinkedAnalysis))
            return;

        linkedAnalysisRoutine = StartCoroutine(LinkedAnalysisRoutine());
    }

    private IEnumerator LinkedAnalysisRoutine()
    {
        isLinkedAnalysisActive = true;

        TargetController target = ResolveTargetController();

        if (target == null)
        {
            isLinkedAnalysisActive = false;
            linkedAnalysisRoutine = null;
            yield break;
        }

        ApplyTargetReveal(target);

        if (debugLog)
        {
            Debug.Log(
                $"[Profiler {AgentID}] 연계 분석 발동. " +
                $"타겟 위치 {linkedAnalysisTargetRevealDuration:0.#}초 표시."
            );
        }

        yield return new WaitForSeconds(linkedAnalysisTargetRevealDuration);

        StopTargetReveal();

        isLinkedAnalysisActive = false;
        linkedAnalysisRoutine = null;
    }

    private void ApplyTargetReveal(TargetController target)
    {
        if (target == null)
            return;

        if (targetRevealRoutine != null ||
            revealedTargetController != null ||
            linkedAnalysisBlockedSkillController != null)
        {
            StopTargetReveal();
        }

        revealedTargetController = target;
        revealedTargetController.AddReconReveal();

        if (linkedAnalysisBlocksTargetSkill && target.SkillController != null)
        {
            linkedAnalysisBlockedSkillController = target.SkillController;
            linkedAnalysisBlockedSkillController.SetTargetSkillBlocked(this, true);
        }
    }

    private IEnumerator TargetRevealRoutine()
    {
        yield return new WaitForSeconds(linkedAnalysisTargetRevealDuration);
        StopTargetReveal();
    }

    private void StopTargetReveal()
    {
        if (targetRevealRoutine != null)
        {
            StopCoroutine(targetRevealRoutine);
            targetRevealRoutine = null;
        }

        if (linkedAnalysisBlockedSkillController != null)
            linkedAnalysisBlockedSkillController.SetTargetSkillBlocked(this, false);

        linkedAnalysisBlockedSkillController = null;

        if (revealedTargetController != null)
            revealedTargetController.RemoveReconReveal();

        revealedTargetController = null;
    }

    private void TryUseRouteIdentification()
    {
        if (!CanUseRouteIdentificationSkill)
        {
            Debug.LogWarning($"[Profiler {AgentID}] 동선 파악 스킬이 아직 해금되지 않았습니다.");
            return;
        }

        if (isRouteIdentificationActive)
            return;

        if (isRouteIdentificationAnimationLocked)
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillRouteIdentification))
            return;

        if (routeIdentificationRoutine != null)
            StopRouteIdentificationRoutine();

        routeIdentificationRoutine = StartCoroutine(RouteIdentificationRoutine());
    }

    private IEnumerator RouteIdentificationRoutine()
    {
        isRouteIdentificationAnimationLocked = true;

        bool shouldPlayAnimation = animator != null && hasRouteIdentificationTrigger;
        float lockSeconds = shouldPlayAnimation ? routeIdentificationAnimationLockSeconds : 0f;
        float effectDelay = shouldPlayAnimation ? Mathf.Clamp(routeIdentificationEffectDelay, 0f, lockSeconds) : 0f;

        StopMovementForSkillAnimation();
        UpdateAnimationState(true);

        PlayProfilerSkillCinematic(
            requestCameraOnRouteIdentification,
            routeIdentificationCameraFocusMode
        );

        if (shouldPlayAnimation)
            PlaySkillTrigger(routeIdentificationHash, RouteIdentificationTriggerName);

        if (effectDelay > 0f)
            yield return new WaitForSeconds(effectDelay);

        isRouteIdentificationActive = true;
        ApplyRouteIdentificationVisionBuff();

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 동선 파악 발동. 프로파일러 시야 강화.");

        float remainingLockSeconds = Mathf.Max(0f, lockSeconds - effectDelay);

        if (remainingLockSeconds > 0f)
            yield return new WaitForSeconds(remainingLockSeconds);

        isRouteIdentificationAnimationLocked = false;
        RestoreNavigationAfterSkillAnimation();
        UpdateAnimationState(true);

        yield return new WaitForSeconds(routeIdentificationDuration);

        RestoreRouteIdentificationVisionBuff();

        isRouteIdentificationActive = false;
        routeIdentificationRoutine = null;
    }

    private void ApplyRouteIdentificationVisionBuff()
    {
        if (visionSensor != null)
        {
            visionSensor.SetExternalViewRadiusMultiplier(
                this,
                routeIdentificationViewRadiusMultiplier
            );

            visionSensor.SetExternalViewAngleOffset(
                this,
                routeIdentificationViewAngleBonus
            );
        }

        if (spotLight != null)
        {
            originalSpotLightRange = spotLight.range;
            originalSpotLightOuterAngle = spotLight.spotAngle;
            hasSpotLightSnapshot = true;

            spotLight.range *= routeIdentificationViewRadiusMultiplier;
            spotLight.spotAngle = Mathf.Clamp(
                spotLight.spotAngle + routeIdentificationViewAngleBonus,
                1f,
                179f
            );
        }
    }

    private void RestoreRouteIdentificationVisionBuff()
    {
        if (visionSensor != null)
        {
            visionSensor.RemoveExternalViewRadiusMultiplier(this);
            visionSensor.RemoveExternalViewAngleOffset(this);
        }

        if (spotLight != null && hasSpotLightSnapshot)
        {
            spotLight.range = originalSpotLightRange;
            spotLight.spotAngle = originalSpotLightOuterAngle;
        }

        hasSpotLightSnapshot = false;
        ClearRouteIdentificationWeakPointTarget();
    }

    private void UpdateRouteIdentificationWeakPointDrain()
    {
        if (!isRouteIdentificationActive)
        {
            ClearRouteIdentificationWeakPointTarget();
            return;
        }

        if (routeIdentificationTargetHealthDrainMultiplier <= 1f)
            return;

        TryRefreshRouteIdentificationWeakPointTarget();

        if (routeIdentificationWeakPointTarget == null)
            return;

        if (Time.time > routeIdentificationWeakPointExpireTime)
        {
            ClearRouteIdentificationWeakPointTarget();
            return;
        }

        routeIdentificationWeakPointTarget.ApplyFleeHealthDrainMultiplier(
            routeIdentificationTargetHealthDrainMultiplier,
            Time.deltaTime
        );
    }

    private void TryRefreshRouteIdentificationWeakPointTarget()
    {
        if (visionSensor == null)
            return;

        if (!visionSensor.IsSeeingTarget)
            return;

        if (visionSensor.CurrentSeenTarget == null)
            return;

        TargetController seenTarget = ResolveTargetControllerFromTransform(
            visionSensor.CurrentSeenTarget
        );

        if (seenTarget == null)
            return;

        if (seenTarget.IsCaught || seenTarget.IsExhausted)
            return;

        targetController = seenTarget;
        routeIdentificationWeakPointTarget = seenTarget;
        routeIdentificationWeakPointExpireTime = Time.time + routeIdentificationTargetHealthDrainDuration;
    }

    private void ClearRouteIdentificationWeakPointTarget()
    {
        routeIdentificationWeakPointTarget = null;
        routeIdentificationWeakPointExpireTime = -999f;
    }

    private bool CanChargeRouteIdentificationFromMovement()
    {
        if (navAgent == null)
            return false;

        if (navAgent.isStopped)
            return false;

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold;

        if (!hasVelocity)
            return false;

        if (isManualMoving)
            return true;

        if (currentTarget != null)
            return true;

        if (IsFollowingSharedTargetPosition)
            return true;

        return navAgent.hasPath && !navAgent.pathPending;
    }

    private void PlayProfilerSkillCinematic(
        bool requestCamera,
        SkillCameraFocusMode cameraFocusMode)
    {
        if (!requestCamera)
            return;

        RequestSkillCamera(cameraFocusMode);
    }

    protected override void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        bool isMoving = ResolveProfilerIsMoving();
        ProfilerMoveMode moveMode = ResolveProfilerMoveMode(isMoving);

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

    private ProfilerMoveMode ResolveProfilerMoveMode(bool isMoving)
    {
        if (isResultAnimationLocked ||
            isHitReactionLocked ||
            isBehaviorBriefingAnimationLocked ||
            isRouteIdentificationAnimationLocked)
        {
            return ProfilerMoveMode.IdleLookAround;
        }

        if (!isMoving)
            return ProfilerMoveMode.IdleLookAround;

        if (IsSmokeDebuffed || IsSkillCommandBlocked)
            return ProfilerMoveMode.DebuffedRun;

        return ProfilerMoveMode.Run;
    }

    private bool ResolveProfilerIsMoving()
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
            HasActivePathForProfilerAnimation();

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForProfilerAnimation();

        return hasMovementIntent && (hasVelocity || hasNotReachedDestination);
    }

    private bool HasActivePathForProfilerAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForProfilerAnimation();
    }

    private bool HasReachedDestinationForProfilerAnimation()
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
        return navAgent.remainingDistance <= stopDistance + destinationBuffer;
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
        isBehaviorBriefingAnimationLocked = false;
        isRouteIdentificationAnimationLocked = false;

        StopHitReactionRoutine();
        RestoreNavigationAfterSkillAnimation();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private IEnumerator HitReactionRoutine(Vector3 hitSourcePosition)
    {
        isHitReactionLocked = true;

        StopBehaviorBriefingRoutine();
        StopRouteIdentificationRoutine();

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
        PlaySkillTrigger(hitReactionHash, HitReactionTriggerName);

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
            Debug.LogWarning($"[Profiler {AgentID}] Animator가 없어서 {triggerName} 애니메이션을 실행할 수 없습니다.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Profiler {AgentID}] Animator에 {triggerName} Trigger가 없습니다.");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;
        isBehaviorBriefingAnimationLocked = false;
        isRouteIdentificationAnimationLocked = false;

        StopAllProfilerEffects();
        StopHitReactionRoutine();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStopped();
        UpdateAnimationState(true);

        ResetAllProfilerTriggers();
        animator.SetTrigger(triggerHash);

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] {triggerName} 애니메이션 실행");
    }

    private void StopMovementForSkillAnimation()
    {
        if (!stopWhenUseSkill)
            return;

        if (hasSkillAnimationNavigationSnapshot)
            return;

        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        hasSkillAnimationNavigationSnapshot = true;
        skillAnimationPreviousStopped = navAgent.isStopped;
        skillAnimationPreviousUpdateRotation = navAgent.updateRotation;

        navAgent.isStopped = true;
        navAgent.updateRotation = false;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void RestoreNavigationAfterSkillAnimation()
    {
        if (!hasSkillAnimationNavigationSnapshot)
            return;

        if (navAgent != null &&
            navAgent.isActiveAndEnabled &&
            navAgent.isOnNavMesh &&
            !isResultAnimationLocked &&
            !isHitReactionLocked)
        {
            navAgent.isStopped = skillAnimationPreviousStopped;
            navAgent.updateRotation = skillAnimationPreviousUpdateRotation;
        }

        hasSkillAnimationNavigationSnapshot = false;
    }

    private void KeepStopped()
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void PlaySkillTrigger(int triggerHash, string triggerName)
    {
        if (animator == null)
            return;

        bool hasTrigger = HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger);

        if (!hasTrigger)
            return;

        ResetAllProfilerTriggers();
        animator.SetTrigger(triggerHash);
    }

    private void ResetAllProfilerTriggers()
    {
        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);
        ResetAnimatorTrigger(briefingHash, hasBriefingTrigger);
        ResetAnimatorTrigger(routeIdentificationHash, hasRouteIdentificationTrigger);
    }

    private void ResetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
            return;

        animator.ResetTrigger(triggerHash);
    }

    private void StopBehaviorBriefingRoutine()
    {
        if (behaviorBriefingRoutine == null)
            return;

        StopCoroutine(behaviorBriefingRoutine);
        behaviorBriefingRoutine = null;

        RestoreBehaviorBriefingMoveSpeedBuff();

        isBehaviorBriefingActive = false;
        isBehaviorBriefingAnimationLocked = false;

        RestoreNavigationAfterSkillAnimation();
    }

    private void StopRouteIdentificationRoutine()
    {
        if (routeIdentificationRoutine == null)
            return;

        StopCoroutine(routeIdentificationRoutine);
        routeIdentificationRoutine = null;

        RestoreRouteIdentificationVisionBuff();

        isRouteIdentificationActive = false;
        isRouteIdentificationAnimationLocked = false;

        RestoreNavigationAfterSkillAnimation();
    }

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
        isHitReactionLocked = false;
    }

    private void CacheProfilerAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
        briefingHash = Animator.StringToHash(BriefingTriggerName);
        routeIdentificationHash = Animator.StringToHash(RouteIdentificationTriggerName);
    }

    private void CacheProfilerAnimatorParameters()
    {
        hasIsMovingParameter = HasAnimatorParameter(IsMovingParameter, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(MoveSpeedParameter, AnimatorControllerParameterType.Float);
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasHitReactionTrigger = HasAnimatorParameter(HitReactionTriggerName, AnimatorControllerParameterType.Trigger);
        hasVictoryTrigger = HasAnimatorParameter(VictoryTriggerName, AnimatorControllerParameterType.Trigger);
        hasDefeatTrigger = HasAnimatorParameter(DefeatTriggerName, AnimatorControllerParameterType.Trigger);
        hasBriefingTrigger = HasAnimatorParameter(BriefingTriggerName, AnimatorControllerParameterType.Trigger);
        hasRouteIdentificationTrigger = HasAnimatorParameter(RouteIdentificationTriggerName, AnimatorControllerParameterType.Trigger);
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

        Debug.LogWarning($"[Profiler {AgentID}] Animator 파라미터가 없습니다: {parameterName} ({parameterType})");
        return false;
    }

    private TargetController ResolveTargetController()
    {
        if (targetController != null)
            return targetController;

        if (currentTarget != null)
        {
            targetController = ResolveTargetControllerFromTransform(currentTarget);

            if (targetController != null)
                return targetController;
        }

        if (!autoFindTarget)
            return null;

        targetController = FindFirstObjectByType<TargetController>();
        return targetController;
    }

    private TargetEscapeMotor ResolveTargetEscapeMotor()
    {
        TargetController target = ResolveTargetController();

        if (target == null)
            return null;

        return target.EscapeMotor;
    }

    private TargetController ResolveTargetControllerFromTransform(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        TargetController controller = targetTransform.GetComponentInParent<TargetController>();

        if (controller != null)
            return controller;

        return targetTransform.GetComponent<TargetController>();
    }

    private void CacheTargetIfNeeded()
    {
        if (targetController != null)
            return;

        if (!autoFindTarget)
            return;

        targetController = FindFirstObjectByType<TargetController>();
    }

    private bool IsAgentUpgradeUnlocked(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return false;

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return false;

        return upgradeManager.HasAgentUpgrade(upgradeId);
    }

    private void ApplyProfilerUpgradeValue(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
            return;

        string key = NormalizeUpgradeKey(upgrade.EffectKey);

        if (string.IsNullOrWhiteSpace(key))
            key = NormalizeUpgradeKey(upgrade.SkillId);

        switch (key)
        {
            case "escapepatternanalysisgaugegain":
                ApplyUpgradeFloat(ref escapePatternGaugeGainPerEscape, upgrade, 0f);
                break;

            case "escapepatternanalysisduration":
                ApplyUpgradeFloat(ref escapePatternSlowDuration, upgrade, 0f);
                break;

            case "escapepatternanalysisspeedmultiplier":
                ApplyUpgradeFloat(ref escapePatternTargetSpeedMultiplier, upgrade, 0.05f);
                escapePatternTargetSpeedMultiplier = Mathf.Clamp(escapePatternTargetSpeedMultiplier, 0.05f, 1f);
                break;

            case "behaviorbriefingduration":
                ApplyUpgradeFloat(ref behaviorBriefingDuration, upgrade, 0f);
                break;

            case "behaviorbriefingspeedmultiplier":
                ApplyUpgradeFloat(ref behaviorBriefingMoveSpeedMultiplier, upgrade, 1f);
                break;

            case "behaviorbriefinggaugegain":
                ApplyUpgradeFloat(ref behaviorBriefingInitialSightGaugeGain, upgrade, 0f);
                break;

            case "behaviorbriefingallygaugegain":
                ApplyUpgradeFloat(ref behaviorBriefingAllyGaugeGainOnUse, upgrade, 0f);
                break;

            case "linkedanalysisrevealduration":
                ApplyUpgradeFloat(ref linkedAnalysisTargetRevealDuration, upgrade, 0f);
                break;

            case "linkedanalysisgaugegain":
                ApplyUpgradeFloat(ref linkedAnalysisGaugeGainPerAllySkill, upgrade, 0f);
                break;

            case "linkedanalysistargetskillblock":
                linkedAnalysisBlocksTargetSkill = true;
                break;

            case "routeidentificationduration":
                ApplyUpgradeFloat(ref routeIdentificationDuration, upgrade, 0f);
                break;

            case "routeidentificationviewradius":
                ApplyUpgradeFloat(ref routeIdentificationViewRadiusMultiplier, upgrade, 1f);
                break;

            case "routeidentificationviewangle":
                ApplyUpgradeFloat(ref routeIdentificationViewAngleBonus, upgrade, 0f);
                break;

            case "routeidentificationgaugegain":
                ApplyUpgradeFloat(ref routeIdentificationGaugeGainPerMeter, upgrade, 0f);
                break;

            case "routeidentificationtargethealthdrainmultiplier":
                ApplyUpgradeFloat(ref routeIdentificationTargetHealthDrainMultiplier, upgrade, 1f);
                break;
        }
    }

    private void ApplyUpgradeFloat(ref float targetValue, UpgradeDefinition upgrade, float minValue)
    {
        if (upgrade == null)
            return;

        float value = upgrade.Value;

        switch (upgrade.EffectType)
        {
            case UpgradeEffectType.ValueAdd:
            case UpgradeEffectType.DurationAdd:
            case UpgradeEffectType.ViewAngleAdd:
            case UpgradeEffectType.MaxGaugeAdd:
                targetValue += value;
                break;

            case UpgradeEffectType.ValueMultiplier:
            case UpgradeEffectType.SpeedMultiplier:
            case UpgradeEffectType.ViewRadiusMultiplier:
            case UpgradeEffectType.GaugeCostMultiplier:
                targetValue *= value;
                break;

            default:
                targetValue = value;
                break;
        }

        targetValue = Mathf.Max(minValue, targetValue);
    }

    private string NormalizeSkillKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private string NormalizeUpgradeKey(string value)
    {
        return NormalizeSkillKey(value);
    }

    private void StopAllProfilerEffects()
    {
        if (escapePatternRoutine != null)
        {
            StopCoroutine(escapePatternRoutine);
            escapePatternRoutine = null;
        }

        StopBehaviorBriefingRoutine();

        if (linkedAnalysisRoutine != null)
        {
            StopCoroutine(linkedAnalysisRoutine);
            linkedAnalysisRoutine = null;
        }

        StopRouteIdentificationRoutine();

        if (activeSlowedTargetMotor != null)
            activeSlowedTargetMotor.RemoveExternalSpeedMultiplier(this);

        activeSlowedTargetMotor = null;

        StopTargetReveal();

        isBehaviorBriefingActive = false;
        isLinkedAnalysisActive = false;
        isRouteIdentificationActive = false;
    }

    private sealed class AgentMoveSnapshot
    {
        public readonly NavMeshAgent NavAgent;
        public readonly float OriginalSpeed;
        public readonly float OriginalAcceleration;

        public AgentMoveSnapshot(
            NavMeshAgent navAgent,
            float originalSpeed,
            float originalAcceleration)
        {
            NavAgent = navAgent;
            OriginalSpeed = originalSpeed;
            OriginalAcceleration = originalAcceleration;
        }
    }
}