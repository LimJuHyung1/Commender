using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Actor : AgentController, IUpgradeReceiver
{
    private enum ActorMoveMode
    {
        Idle = 0,
        Run = 1,
        Reserved = 2,
        DebuffedRun = 3
    }

    private const string SkillSceneStealer = "scene_stealer";
    private const string SkillAdLib = "ad_lib";
    private const string SkillAction = "action";
    private const string SkillMethodActing = "method_acting";

    private const string UpgradeSceneStealerRadius = "actor_scene_stealer_radius_150";
    private const string UpgradeSceneStealerRadiusLegacy = "actor_scene_stealer_radius_125";
    private const string UpgradeSceneStealerGaugeRefund = "actor_scene_stealer_gauge_refund";
    private const string UpgradeAdLibCooldownReduction = "actor_ad_lib_cooldown_reduction";
    private const string UpgradeAdLibPathRecalculationDelay = "actor_ad_lib_path_recalculation_delay";
    private const string UpgradeActionCloseUpVisibility = "actor_action_close_up_visibility";
    private const string UpgradeActionStarMobility = "actor_action_star_mobility";
    private const string UpgradeMethodActingDurationIncrease = "actor_method_acting_duration_increase";
    private const string UpgradeMethodActingHyperImmersion = "actor_method_acting_hyper_immersion";

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "moveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";
    private const string SceneStealerTriggerName = "SceneStealer";
    private const string ActionTriggerName = "Action";

    [Header("Actor Identity")]
    [SerializeField] private bool overrideAgentIdOnAwake = false;
    [SerializeField] private int actorAgentId = 6;

    [Header("Scene Stealer")]
    [SerializeField] private float sceneStealerGaugeMax = 100f;
    [SerializeField] private float sceneStealerDuration = 10f;
    [SerializeField] private float sceneStealerRadius = 12f;
    [SerializeField] private float sceneStealerCaptivationDuration = 3.5f;
    [SerializeField] private float sceneStealerRevealDuration = 4f;
    [SerializeField] private float sceneStealerTurnSpeed = 720f;
    [SerializeField] private bool sceneStealerRevealTarget = true;
    [SerializeField] private bool sceneStealerRootTarget = true;
    [SerializeField] private bool sceneStealerTurnTargetToActor = true;
    [SerializeField] private bool disableActorCatchDuringSceneStealer = true;
    [SerializeField] private bool requestSceneStealerCamera = true;

    [Header("Scene Stealer Visual")]
    [SerializeField] private GameObject sceneStealerRangeIndicatorPrefab;
    [SerializeField] private float sceneStealerIndicatorYOffset = 0.03f;
    [SerializeField] private bool destroySceneStealerIndicatorOnEnd = true;

    [Header("Scene Stealer Feedback")]
    [SerializeField] private Material sceneStealerLureLineMaterial;
    [SerializeField] private float sceneStealerLureLineWidth = 0.08f;
    [SerializeField] private float sceneStealerLureLineYOffset = 1.2f;
    [SerializeField] private GameObject sceneStealerLuredTargetMarkerPrefab;
    [SerializeField] private float sceneStealerLuredTargetMarkerYOffset = 2.2f;

    [Header("Ad Lib")]
    [SerializeField] private float adLibDuration = 5f;
    [SerializeField] private float adLibCooldownSeconds = 20f;
    [SerializeField] private float adLibUpgradedCooldownSeconds = 14f;
    [SerializeField] private float adLibMoveSpeedMultiplier = 1.5f;
    [SerializeField] private float adLibTargetPathDelayMultiplier = 1.25f;
    [SerializeField] private float adLibTargetPathDelayDuration = 5f;

    [Header("Action")]
    [SerializeField] private float actionGaugeMax = 75f;
    [SerializeField] private float actionDuration = 15f;
    [SerializeField] private float actionMoveSpeedMultiplier = 1.25f;
    [SerializeField] private float actionViewRadiusMultiplier = 1.25f;
    [SerializeField] private float actionStarMoveSpeedMultiplier = 1.5f;
    [SerializeField] private float actionStarAccelerationMultiplier = 1.5f;
    [SerializeField] private bool requestActionCamera = true;

    [Header("Close-Up Upgrade")]
    [SerializeField] private AgentCameraFollow cameraFollow;
    [SerializeField] private float closeUpVisibilityRefreshInterval = 0.25f;

    [Header("Method Acting")]
    [SerializeField] private float methodActingGaugeMax = 100f;
    [SerializeField] private float methodActingChargeSeconds = 25f;
    [SerializeField] private float methodActingDuration = 10f;
    [SerializeField] private float methodActingUpgradedDuration = 15f;
    [SerializeField] private float methodActingPathDelayMultiplier = 1.25f;

    [Header("Actor Animation")]
    [SerializeField] private float animationMovingThreshold = 0.05f;
    [SerializeField] private float animationDestinationBuffer = 0.2f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private bool stopWhenUseSkillAnimation = true;

    [Header("Skill Animation Timing")]
    [SerializeField] private float sceneStealerAnimationLockSeconds = 0.8f;
    [SerializeField] private float actionAnimationLockSeconds = 0.65f;

    [Header("Hit Animation")]
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    [Header("Debug")]
    [SerializeField] private bool debugActorLog = false;

    private readonly Collider[] sceneStealerResults = new Collider[32];
    private readonly HashSet<TargetVisibilityController> closeUpVisibleTargets = new HashSet<TargetVisibilityController>();
    private readonly Dictionary<TargetEscapeMotor, RouteDelayState> routeDelayStates = new Dictionary<TargetEscapeMotor, RouteDelayState>();
    private readonly Dictionary<TargetSkillController, Coroutine> targetSkillBlockRoutines = new Dictionary<TargetSkillController, Coroutine>();

    private readonly object adLibRouteDelaySource = new object();
    private readonly object methodActingRouteDelaySource = new object();

    private Coroutine sceneStealerRoutine;
    private Coroutine sceneStealerRevealRoutine;
    private Coroutine sceneStealerFaceRoutine;
    private Coroutine adLibRoutine;
    private Coroutine actionRoutine;
    private Coroutine closeUpVisibilityRoutine;
    private Coroutine hitReactionRoutine;
    private Coroutine sceneStealerAnimationRoutine;
    private Coroutine actionAnimationRoutine;

    private bool sceneStealerRadiusUpgradeApplied;
    private bool sceneStealerGaugeRefundUpgradeApplied;
    private bool adLibPathDelayUpgradeApplied;
    private bool actionCloseUpUpgradeApplied;
    private bool actionStarUpgradeApplied;
    private bool methodActingDurationUpgradeApplied;
    private bool methodActingHyperImmersionUpgradeApplied;

    private float sceneStealerRadiusMultiplier = 1f;
    private float sceneStealerGaugeRefundRatio = 0f;

    private bool isSceneStealerActive;
    private bool isAdLibActive;
    private bool isActionActive;
    private bool isHitReactionLocked;
    private bool isSceneStealerAnimationLocked;
    private bool isActionAnimationLocked;
    private bool isResultAnimationLocked;

    private float adLibCooldownReadyTime = -999f;
    private float methodActingGauge;

    private float originalSpotLightRange;
    private bool hasOriginalSpotLightRange;

    private GameObject activeSceneStealerIndicatorInstance;
    private LineRenderer activeSceneStealerLureLine;
    private GameObject activeSceneStealerLuredTargetMarker;
    private TargetVisibilityController activeSceneStealerVisibleTarget;
    private NavMeshAgent activeSceneStealerFacingAgent;
    private bool activeSceneStealerFacingAgentOriginalUpdateRotation;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;
    private int sceneStealerHash;
    private int actionHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;
    private bool hasSceneStealerTrigger;
    private bool hasActionTrigger;

    private bool hitReactionHadNavAgentState;
    private bool hitReactionPreviousStopped;
    private bool hitReactionPreviousUpdateRotation = true;

    public override bool CanCatchTarget
    {
        get
        {
            if (disableActorCatchDuringSceneStealer && isSceneStealerActive)
                return false;

            return base.CanCatchTarget;
        }
    }

    private class RouteDelayState
    {
        public float OriginalCooldown;
        public readonly Dictionary<object, float> Multipliers = new Dictionary<object, float>();
        public readonly Dictionary<object, Coroutine> Routines = new Dictionary<object, Coroutine>();
    }

    protected override void Awake()
    {
        if (overrideAgentIdOnAwake)
            agentID = actorAgentId;

        CacheActorAnimationHashes();

        base.Awake();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheActorAnimatorParameters();

        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<AgentCameraFollow>();

        if (visionSensor != null)
            visionSensor.OnVisionChanged += HandleVisionChanged;

        if (spotLight != null)
        {
            originalSpotLightRange = spotLight.range;
            hasOriginalSpotLightRange = true;
        }

        UpdateAnimationState(true);
    }

    protected override void OnDisable()
    {
        if (visionSensor != null)
            visionSensor.OnVisionChanged -= HandleVisionChanged;

        StopAllActorEffects();

        base.OnDisable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        sceneStealerGaugeMax = Mathf.Max(0f, sceneStealerGaugeMax);
        sceneStealerDuration = Mathf.Max(0f, sceneStealerDuration);
        sceneStealerRadius = Mathf.Max(0f, sceneStealerRadius);
        sceneStealerCaptivationDuration = Mathf.Max(0f, sceneStealerCaptivationDuration);
        sceneStealerRevealDuration = Mathf.Max(0f, sceneStealerRevealDuration);
        sceneStealerTurnSpeed = Mathf.Max(0f, sceneStealerTurnSpeed);
        sceneStealerIndicatorYOffset = Mathf.Max(0f, sceneStealerIndicatorYOffset);
        sceneStealerLureLineWidth = Mathf.Max(0.001f, sceneStealerLureLineWidth);
        sceneStealerLureLineYOffset = Mathf.Max(0f, sceneStealerLureLineYOffset);
        sceneStealerLuredTargetMarkerYOffset = Mathf.Max(0f, sceneStealerLuredTargetMarkerYOffset);

        adLibDuration = Mathf.Max(0f, adLibDuration);
        adLibCooldownSeconds = Mathf.Max(0f, adLibCooldownSeconds);
        adLibUpgradedCooldownSeconds = Mathf.Max(0f, adLibUpgradedCooldownSeconds);
        adLibMoveSpeedMultiplier = Mathf.Max(1f, adLibMoveSpeedMultiplier);
        adLibTargetPathDelayMultiplier = Mathf.Max(1f, adLibTargetPathDelayMultiplier);
        adLibTargetPathDelayDuration = Mathf.Max(0f, adLibTargetPathDelayDuration);

        actionGaugeMax = Mathf.Max(0f, actionGaugeMax);
        actionDuration = Mathf.Max(0f, actionDuration);
        actionMoveSpeedMultiplier = Mathf.Max(1f, actionMoveSpeedMultiplier);
        actionViewRadiusMultiplier = Mathf.Max(1f, actionViewRadiusMultiplier);
        actionStarMoveSpeedMultiplier = Mathf.Max(1f, actionStarMoveSpeedMultiplier);
        actionStarAccelerationMultiplier = Mathf.Max(1f, actionStarAccelerationMultiplier);
        closeUpVisibilityRefreshInterval = Mathf.Max(0.05f, closeUpVisibilityRefreshInterval);

        methodActingGaugeMax = Mathf.Max(0f, methodActingGaugeMax);
        methodActingChargeSeconds = Mathf.Max(0.01f, methodActingChargeSeconds);
        methodActingDuration = Mathf.Max(0f, methodActingDuration);
        methodActingUpgradedDuration = Mathf.Max(0f, methodActingUpgradedDuration);
        methodActingPathDelayMultiplier = Mathf.Max(1f, methodActingPathDelayMultiplier);

        animationMovingThreshold = Mathf.Max(0f, animationMovingThreshold);
        animationDestinationBuffer = Mathf.Max(0f, animationDestinationBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);
        sceneStealerAnimationLockSeconds = Mathf.Max(0f, sceneStealerAnimationLockSeconds);
        actionAnimationLockSeconds = Mathf.Max(0f, actionAnimationLockSeconds);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);
    }

    protected override void Update()
    {
        base.Update();

        UpdateMethodActingGauge();
        TryActivateMethodActingFromCurrentSight();
    }

    protected override void ApplyNavAgentStats()
    {
        base.ApplyNavAgentStats();
        RefreshMovementMultipliers();
    }

    protected override void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        bool isMoving = ResolveActorIsMoving();
        ActorMoveMode moveMode = ResolveActorMoveMode(isMoving);

        if (hasIsMovingParameter)
            animator.SetBool(isMovingHash, isMoving);

        if (hasMoveModeParameter)
            animator.SetInteger(moveModeHash, (int)moveMode);

        if (!hasMoveSpeedParameter)
            return;

        float actualSpeed = Mathf.Max(navAgent.velocity.magnitude, navAgent.desiredVelocity.magnitude);

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

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillSceneStealer))
        {
            TryUseSceneStealer();
            return;
        }

        if (skill == NormalizeSkillKey(SkillAction))
        {
            TryUseAction();
            return;
        }

        if (skill == NormalizeSkillKey(SkillAdLib) || skill == NormalizeSkillKey(SkillMethodActing))
        {
            Debug.LogWarning($"[Actor {AgentID}] {skillName}은 자동 발동 패시브 스킬입니다.");
            return;
        }

        Debug.LogWarning($"[Actor {AgentID}] 처리할 수 없는 스킬입니다. skill={skillName}");
    }

    public override float GetSkillGaugeMaxForSkill(string skillName)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillSceneStealer))
            return sceneStealerGaugeMax;

        if (skill == NormalizeSkillKey(SkillAction))
            return actionGaugeMax;

        if (skill == NormalizeSkillKey(SkillMethodActing))
            return methodActingGaugeMax;

        if (skill == NormalizeSkillKey(SkillAdLib))
            return 0f;

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeRequiredForSkill(string skillName)
    {
        return GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeCurrentForSkill(string skillName)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillMethodActing))
            return Mathf.Clamp(methodActingGauge, 0f, methodActingGaugeMax);

        return base.GetSkillGaugeCurrentForSkill(skillName);
    }

    public override float GetSkillGaugeNormalizedForSkill(string skillName)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillMethodActing))
        {
            if (methodActingGaugeMax <= 0f)
                return 1f;

            return Mathf.Clamp01(methodActingGauge / methodActingGaugeMax);
        }

        return base.GetSkillGaugeNormalizedForSkill(skillName);
    }

    public override bool AddSkillGaugeForSkill(string skillName, float amount)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillMethodActing))
        {
            if (amount <= 0f || methodActingGaugeMax <= 0f)
                return false;

            float previous = methodActingGauge;
            methodActingGauge = Mathf.Min(methodActingGaugeMax, methodActingGauge + amount);

            return methodActingGauge > previous;
        }

        return base.AddSkillGaugeForSkill(skillName, amount);
    }

    public override void ResetSkillGauge()
    {
        base.ResetSkillGauge();
        methodActingGauge = 0f;
    }

    public override void FillSkillGauge()
    {
        base.FillSkillGauge();
        methodActingGauge = methodActingGaugeMax;
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        return new[]
        {
            SkillSceneStealer,
            SkillAction
        };
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return CanApplyUpgradeByAgentDefinitionOrLegacy(upgrade, CommanderAgentType.Actor);
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        if (upgrade.IsUnlockSkillUpgrade)
            return;

        switch (upgrade.UpgradeId)
        {
            case UpgradeSceneStealerRadius:
            case UpgradeSceneStealerRadiusLegacy:
                sceneStealerRadiusUpgradeApplied = true;
                sceneStealerRadiusMultiplier = upgrade.Value > 0f ? upgrade.Value : 1.5f;
                break;

            case UpgradeSceneStealerGaugeRefund:
                sceneStealerGaugeRefundUpgradeApplied = true;
                sceneStealerGaugeRefundRatio = upgrade.Value > 0f ? upgrade.Value : 0.25f;
                break;

            case UpgradeAdLibCooldownReduction:
                adLibCooldownSeconds = upgrade.Value > 0f ? upgrade.Value : adLibUpgradedCooldownSeconds;
                break;

            case UpgradeAdLibPathRecalculationDelay:
                adLibPathDelayUpgradeApplied = true;
                adLibTargetPathDelayMultiplier = upgrade.Value > 0f ? upgrade.Value : 1.25f;
                break;

            case UpgradeActionCloseUpVisibility:
                actionCloseUpUpgradeApplied = true;

                if (isActionActive)
                    StartCloseUpVisibilityRoutine();

                break;

            case UpgradeActionStarMobility:
                actionStarUpgradeApplied = true;
                RefreshMovementMultipliers();
                break;

            case UpgradeMethodActingDurationIncrease:
                methodActingDurationUpgradeApplied = true;
                methodActingUpgradedDuration = upgrade.Value > 0f ? upgrade.Value : methodActingUpgradedDuration;
                break;

            case UpgradeMethodActingHyperImmersion:
                methodActingHyperImmersionUpgradeApplied = true;
                break;
        }
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
        PlayResultAnimation(victoryHash, hasVictoryTrigger, VictoryTriggerName);
    }

    public override void PlayDefeatPose()
    {
        PlayResultAnimation(defeatHash, hasDefeatTrigger, DefeatTriggerName);
    }

    public override void ClearResultAnimationLock()
    {
        isResultAnimationLocked = false;
        isHitReactionLocked = false;
        isSceneStealerAnimationLocked = false;
        isActionAnimationLocked = false;

        StopHitReactionRoutine();
        StopSceneStealerAnimationRoutine();
        StopActionAnimationRoutine();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void HandleVisionChanged(VisionSensor sensor, bool isSeeingTarget, Transform seenTarget)
    {
        if (!isSeeingTarget || seenTarget == null)
            return;

        TargetController target = ResolveTargetController(seenTarget);

        if (target == null)
            return;

        TryActivateAdLib(target);
        TryActivateMethodActing(target);
    }

    private void TryUseSceneStealer()
    {
        if (sceneStealerRoutine != null)
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillSceneStealer))
            return;

        sceneStealerRoutine = StartCoroutine(SceneStealerRoutine());
    }

    private IEnumerator SceneStealerRoutine()
    {
        isSceneStealerActive = true;

        float usedGauge = GetSkillGaugeRequiredForSkill(SkillSceneStealer);
        bool success = false;

        if (requestSceneStealerCamera)
            RequestFollowUserSkillCamera();

        StartSceneStealerAnimation();

        Vector3 sceneStealerCastPosition = transform.position;
        TargetController affectedTarget = FindSceneStealerTargetAtCastTime(sceneStealerCastPosition);

        ShowSceneStealerIndicator(sceneStealerCastPosition);
        StopActorMovementForSceneStealer();

        if (affectedTarget != null)
        {
            success = true;
            ApplySceneStealerCaptivation(affectedTarget);

            if (debugActorLog)
                Debug.Log($"[Actor {AgentID}] 씬 스틸러 대상 적용: {affectedTarget.name}");
        }
        else if (debugActorLog)
        {
            Debug.Log($"[Actor {AgentID}] 시전 순간 씬 스틸러 범위 안에 타겟이 없습니다.");
        }

        float endTime = Time.time + sceneStealerDuration;

        while (Time.time < endTime)
        {
            StopActorMovementForSceneStealer();

            if (affectedTarget != null)
                UpdateSceneStealerTargetFeedback(affectedTarget);

            yield return null;
        }

        if (success && sceneStealerGaugeRefundUpgradeApplied && sceneStealerGaugeRefundRatio > 0f)
        {
            AddSkillGaugeForSkill(SkillSceneStealer, usedGauge * sceneStealerGaugeRefundRatio);
        }

        HideSceneStealerTargetFeedback();
        HideSceneStealerIndicator();

        isSceneStealerActive = false;
        sceneStealerRoutine = null;

        if (navAgent != null)
            navAgent.isStopped = false;

        RefreshMovementMultipliers();
    }

    private TargetController FindSceneStealerTargetAtCastTime(Vector3 castPosition)
    {
        float radius = GetSceneStealerRadius();

        int resultCount = Physics.OverlapSphereNonAlloc(
            castPosition,
            radius,
            sceneStealerResults,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        TargetController bestTarget = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < resultCount; i++)
        {
            Collider hit = sceneStealerResults[i];

            if (hit == null)
                continue;

            TargetController target = ResolveTargetController(hit.transform);

            if (target == null)
                continue;

            if (!CanApplySceneStealerToTarget(target))
                continue;

            float distanceSqr = (target.transform.position - castPosition).sqrMagnitude;

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestTarget = target;
            bestDistanceSqr = distanceSqr;
        }

        return bestTarget;
    }

    private bool CanApplySceneStealerToTarget(TargetController target)
    {
        if (target == null)
            return false;

        if (target.IsCaught || target.IsExhausted)
            return false;

        if (target.HasActiveThreat)
            return false;

        return true;
    }

    private void ApplySceneStealerCaptivation(TargetController target)
    {
        if (target == null)
            return;

        if (sceneStealerRevealTarget)
        {
            TargetVisibilityController visibility = ResolveTargetVisibilityController(target);

            if (visibility != null)
            {
                if (sceneStealerRevealRoutine != null)
                    StopCoroutine(sceneStealerRevealRoutine);

                sceneStealerRevealRoutine = StartCoroutine(RevealSceneStealerTargetRoutine(visibility, sceneStealerRevealDuration));
            }
        }

        if (sceneStealerRootTarget)
        {
            TargetEscapeMotor escapeMotor = target.EscapeMotor != null
                ? target.EscapeMotor
                : target.GetComponent<TargetEscapeMotor>();

            if (escapeMotor != null)
                escapeMotor.ApplyRoot(sceneStealerCaptivationDuration);
        }

        if (sceneStealerTurnTargetToActor)
        {
            if (sceneStealerFaceRoutine != null)
                StopCoroutine(sceneStealerFaceRoutine);

            sceneStealerFaceRoutine = StartCoroutine(FaceTargetTowardActorRoutine(target, sceneStealerCaptivationDuration));
        }

        ShowSceneStealerTargetFeedback(target);
    }

    private IEnumerator RevealSceneStealerTargetRoutine(TargetVisibilityController visibility, float duration)
    {
        if (visibility == null)
            yield break;

        activeSceneStealerVisibleTarget = visibility;
        visibility.SetForceVisible(this, true);

        yield return new WaitForSeconds(duration);

        if (activeSceneStealerVisibleTarget != null)
            activeSceneStealerVisibleTarget.ClearForceVisible(this);

        activeSceneStealerVisibleTarget = null;
        sceneStealerRevealRoutine = null;
    }

    private IEnumerator FaceTargetTowardActorRoutine(TargetController target, float duration)
    {
        if (target == null)
            yield break;

        NavMeshAgent targetAgent = target.GetComponent<NavMeshAgent>();

        activeSceneStealerFacingAgent = targetAgent;

        if (activeSceneStealerFacingAgent != null)
        {
            activeSceneStealerFacingAgentOriginalUpdateRotation = activeSceneStealerFacingAgent.updateRotation;
            activeSceneStealerFacingAgent.updateRotation = false;
        }

        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            if (target == null)
                break;

            Vector3 direction = transform.position - target.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

                target.transform.rotation = Quaternion.RotateTowards(
                    target.transform.rotation,
                    targetRotation,
                    sceneStealerTurnSpeed * Time.deltaTime
                );
            }

            yield return null;
        }

        RestoreSceneStealerFacingAgentRotation();
        sceneStealerFaceRoutine = null;
    }

    private void RestoreSceneStealerFacingAgentRotation()
    {
        if (activeSceneStealerFacingAgent != null)
            activeSceneStealerFacingAgent.updateRotation = activeSceneStealerFacingAgentOriginalUpdateRotation;

        activeSceneStealerFacingAgent = null;
    }

    private void StopActorMovementForSceneStealer()
    {
        if (navAgent == null)
            return;

        currentTarget = null;
        isManualMoving = false;

        navAgent.ResetPath();
        navAgent.isStopped = true;
    }

    private float GetSceneStealerRadius()
    {
        float multiplier = sceneStealerRadiusUpgradeApplied ? sceneStealerRadiusMultiplier : 1f;
        return sceneStealerRadius * multiplier;
    }

    private void ShowSceneStealerIndicator(Vector3 centerPosition)
    {
        HideSceneStealerIndicator();

        if (sceneStealerRangeIndicatorPrefab == null)
            return;

        Quaternion prefabRotation = sceneStealerRangeIndicatorPrefab.transform.rotation;

        activeSceneStealerIndicatorInstance = Instantiate(
            sceneStealerRangeIndicatorPrefab,
            GetSceneStealerIndicatorPosition(centerPosition),
            prefabRotation
        );

        float diameter = GetSceneStealerRadius() * 2f;
        Vector3 scale = activeSceneStealerIndicatorInstance.transform.localScale;
        scale.x = diameter;
        scale.y = diameter;
        activeSceneStealerIndicatorInstance.transform.localScale = scale;
    }

    private void HideSceneStealerIndicator()
    {
        if (activeSceneStealerIndicatorInstance == null)
            return;

        if (destroySceneStealerIndicatorOnEnd)
            Destroy(activeSceneStealerIndicatorInstance);
        else
            activeSceneStealerIndicatorInstance.SetActive(false);

        activeSceneStealerIndicatorInstance = null;
    }

    private Vector3 GetSceneStealerIndicatorPosition(Vector3 centerPosition)
    {
        return new Vector3(
            centerPosition.x,
            centerPosition.y + sceneStealerIndicatorYOffset,
            centerPosition.z
        );
    }

    private void ShowSceneStealerTargetFeedback(TargetController target)
    {
        if (target == null)
            return;

        HideSceneStealerTargetFeedback();

        GameObject lineObject = new GameObject("SceneStealer_TargetLine");
        activeSceneStealerLureLine = lineObject.AddComponent<LineRenderer>();
        activeSceneStealerLureLine.useWorldSpace = true;
        activeSceneStealerLureLine.positionCount = 2;
        activeSceneStealerLureLine.startWidth = sceneStealerLureLineWidth;
        activeSceneStealerLureLine.endWidth = sceneStealerLureLineWidth;
        activeSceneStealerLureLine.numCapVertices = 8;
        activeSceneStealerLureLine.numCornerVertices = 8;

        if (sceneStealerLureLineMaterial != null)
            activeSceneStealerLureLine.material = sceneStealerLureLineMaterial;

        if (sceneStealerLuredTargetMarkerPrefab != null)
        {
            activeSceneStealerLuredTargetMarker = Instantiate(
                sceneStealerLuredTargetMarkerPrefab,
                target.transform.position + Vector3.up * sceneStealerLuredTargetMarkerYOffset,
                Quaternion.identity
            );
        }

        UpdateSceneStealerTargetFeedback(target);
    }

    private void UpdateSceneStealerTargetFeedback(TargetController target)
    {
        if (target == null)
            return;

        if (activeSceneStealerLureLine != null)
        {
            Vector3 actorLinePosition = transform.position + Vector3.up * sceneStealerLureLineYOffset;
            Vector3 targetLinePosition = target.transform.position + Vector3.up * sceneStealerLureLineYOffset;

            activeSceneStealerLureLine.SetPosition(0, actorLinePosition);
            activeSceneStealerLureLine.SetPosition(1, targetLinePosition);
        }

        if (activeSceneStealerLuredTargetMarker != null)
        {
            activeSceneStealerLuredTargetMarker.transform.position =
                target.transform.position + Vector3.up * sceneStealerLuredTargetMarkerYOffset;
        }
    }

    private void HideSceneStealerTargetFeedback()
    {
        if (activeSceneStealerLureLine != null)
        {
            Destroy(activeSceneStealerLureLine.gameObject);
            activeSceneStealerLureLine = null;
        }

        if (activeSceneStealerLuredTargetMarker != null)
        {
            Destroy(activeSceneStealerLuredTargetMarker);
            activeSceneStealerLuredTargetMarker = null;
        }
    }

    private TargetVisibilityController ResolveTargetVisibilityController(TargetController target)
    {
        if (target == null)
            return null;

        TargetVisibilityController visibility = target.VisibilityController;

        if (visibility != null)
            return visibility;

        visibility = target.GetComponent<TargetVisibilityController>();

        if (visibility != null)
            return visibility;

        return target.GetComponentInChildren<TargetVisibilityController>();
    }

    private void TryActivateAdLib(TargetController target)
    {
        if (isAdLibActive)
            return;

        if (Time.time < adLibCooldownReadyTime)
            return;

        adLibCooldownReadyTime = Time.time + adLibCooldownSeconds;

        if (adLibRoutine != null)
            StopCoroutine(adLibRoutine);

        adLibRoutine = StartCoroutine(AdLibRoutine(target));
    }

    private IEnumerator AdLibRoutine(TargetController target)
    {
        isAdLibActive = true;
        RefreshMovementMultipliers();

        if (adLibPathDelayUpgradeApplied && target != null)
        {
            TargetEscapeMotor escapeMotor = target.EscapeMotor != null
                ? target.EscapeMotor
                : target.GetComponent<TargetEscapeMotor>();

            ApplyTemporaryRouteDelay(
                escapeMotor,
                adLibRouteDelaySource,
                adLibTargetPathDelayMultiplier,
                adLibTargetPathDelayDuration
            );
        }

        yield return new WaitForSeconds(adLibDuration);

        isAdLibActive = false;
        adLibRoutine = null;

        RefreshMovementMultipliers();
    }

    private void TryUseAction()
    {
        if (actionRoutine != null)
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillAction))
            return;

        actionRoutine = StartCoroutine(ActionRoutine());
    }

    private IEnumerator ActionRoutine()
    {
        isActionActive = true;

        if (requestActionCamera)
            RequestFollowUserSkillCamera();

        StartActionAnimation();

        ApplyActionVision();
        RefreshMovementMultipliers();
        StartCloseUpVisibilityRoutine();

        yield return new WaitForSeconds(actionDuration);

        isActionActive = false;
        actionRoutine = null;

        StopCloseUpVisibilityRoutine();
        RemoveActionVision();
        RefreshMovementMultipliers();
    }

    private void ApplyActionVision()
    {
        if (visionSensor != null)
            visionSensor.SetExternalViewRadiusMultiplier(this, actionViewRadiusMultiplier);

        if (spotLight != null)
        {
            if (!hasOriginalSpotLightRange)
            {
                originalSpotLightRange = spotLight.range;
                hasOriginalSpotLightRange = true;
            }

            spotLight.range = originalSpotLightRange * actionViewRadiusMultiplier;
        }
    }

    private void RemoveActionVision()
    {
        if (visionSensor != null)
            visionSensor.RemoveExternalViewRadiusMultiplier(this);

        if (spotLight != null && hasOriginalSpotLightRange)
            spotLight.range = originalSpotLightRange;
    }

    private void StartCloseUpVisibilityRoutine()
    {
        if (!actionCloseUpUpgradeApplied)
            return;

        if (!isActionActive)
            return;

        if (closeUpVisibilityRoutine != null)
            return;

        closeUpVisibilityRoutine = StartCoroutine(CloseUpVisibilityRoutine());
    }

    private void StopCloseUpVisibilityRoutine()
    {
        if (closeUpVisibilityRoutine != null)
        {
            StopCoroutine(closeUpVisibilityRoutine);
            closeUpVisibilityRoutine = null;
        }

        ClearCloseUpVisibility();
    }

    private IEnumerator CloseUpVisibilityRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(closeUpVisibilityRefreshInterval);

        while (isActionActive && actionCloseUpUpgradeApplied)
        {
            if (IsCameraFocusedOnActor())
                ApplyCloseUpVisibility();
            else
                ClearCloseUpVisibility();

            yield return wait;
        }

        ClearCloseUpVisibility();
        closeUpVisibilityRoutine = null;
    }

    private bool IsCameraFocusedOnActor()
    {
        if (cameraFollow == null)
            return false;

        return cameraFollow.FocusedAgent == transform;
    }

    private void ApplyCloseUpVisibility()
    {
        TargetVisibilityController[] visibilityControllers = FindObjectsByType<TargetVisibilityController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < visibilityControllers.Length; i++)
        {
            TargetVisibilityController visibility = visibilityControllers[i];

            if (visibility == null)
                continue;

            visibility.SetForceVisible(this, true);
            closeUpVisibleTargets.Add(visibility);
        }
    }

    private void ClearCloseUpVisibility()
    {
        foreach (TargetVisibilityController visibility in closeUpVisibleTargets)
        {
            if (visibility == null)
                continue;

            visibility.ClearForceVisible(this);
        }

        closeUpVisibleTargets.Clear();
    }

    private void UpdateMethodActingGauge()
    {
        if (methodActingGaugeMax <= 0f)
            return;

        if (methodActingGauge >= methodActingGaugeMax)
        {
            methodActingGauge = methodActingGaugeMax;
            return;
        }

        float chargePerSecond = methodActingGaugeMax / methodActingChargeSeconds;
        methodActingGauge = Mathf.Min(
            methodActingGaugeMax,
            methodActingGauge + chargePerSecond * Time.deltaTime
        );
    }

    private void TryActivateMethodActingFromCurrentSight()
    {
        if (visionSensor == null)
            return;

        if (!visionSensor.IsSeeingTarget)
            return;

        if (visionSensor.CurrentSeenTarget == null)
            return;

        TargetController target = ResolveTargetController(visionSensor.CurrentSeenTarget);

        if (target == null)
            return;

        TryActivateMethodActing(target);
    }

    private void TryActivateMethodActing(TargetController target)
    {
        if (target == null)
            return;

        if (methodActingGaugeMax <= 0f)
            return;

        if (methodActingGauge < methodActingGaugeMax)
            return;

        float duration = GetMethodActingDuration();
        methodActingGauge = 0f;

        if (methodActingHyperImmersionUpgradeApplied)
        {
            ApplyTemporaryTargetSkillBlock(target, duration);
        }
        else
        {
            TargetEscapeMotor escapeMotor = target.EscapeMotor != null
                ? target.EscapeMotor
                : target.GetComponent<TargetEscapeMotor>();

            ApplyTemporaryRouteDelay(
                escapeMotor,
                methodActingRouteDelaySource,
                methodActingPathDelayMultiplier,
                duration
            );
        }

        if (debugActorLog)
            Debug.Log($"[Actor {AgentID}] 메소드 연기 발동. target={target.name}");
    }

    private float GetMethodActingDuration()
    {
        return methodActingDurationUpgradeApplied
            ? methodActingUpgradedDuration
            : methodActingDuration;
    }

    private void ApplyTemporaryTargetSkillBlock(TargetController target, float duration)
    {
        if (target == null)
            return;

        TargetSkillController skillController = target.SkillController != null
            ? target.SkillController
            : target.GetComponent<TargetSkillController>();

        if (skillController == null)
            return;

        if (targetSkillBlockRoutines.TryGetValue(skillController, out Coroutine runningRoutine) && runningRoutine != null)
            StopCoroutine(runningRoutine);

        skillController.SetTargetSkillBlocked(this, true);
        targetSkillBlockRoutines[skillController] = StartCoroutine(RemoveTargetSkillBlockAfter(skillController, duration));
    }

    private IEnumerator RemoveTargetSkillBlockAfter(TargetSkillController skillController, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (skillController != null)
            skillController.SetTargetSkillBlocked(this, false);

        targetSkillBlockRoutines.Remove(skillController);
    }

    private void ApplyTemporaryRouteDelay(TargetEscapeMotor escapeMotor, object source, float multiplier, float duration)
    {
        if (escapeMotor == null || escapeMotor.settings == null || source == null)
            return;

        multiplier = Mathf.Max(1f, multiplier);
        duration = Mathf.Max(0f, duration);

        if (!routeDelayStates.TryGetValue(escapeMotor, out RouteDelayState state))
        {
            state = new RouteDelayState
            {
                OriginalCooldown = escapeMotor.settings.repathCooldown
            };

            routeDelayStates.Add(escapeMotor, state);
        }

        if (state.Routines.TryGetValue(source, out Coroutine runningRoutine) && runningRoutine != null)
            StopCoroutine(runningRoutine);

        state.Multipliers[source] = multiplier;
        ApplyRouteDelayState(escapeMotor, state);

        state.Routines[source] = StartCoroutine(RemoveRouteDelayAfter(escapeMotor, source, duration));
    }

    private IEnumerator RemoveRouteDelayAfter(TargetEscapeMotor escapeMotor, object source, float duration)
    {
        yield return new WaitForSeconds(duration);
        RemoveRouteDelay(escapeMotor, source);
    }

    private void RemoveRouteDelay(TargetEscapeMotor escapeMotor, object source)
    {
        if (escapeMotor == null || source == null)
            return;

        if (!routeDelayStates.TryGetValue(escapeMotor, out RouteDelayState state))
            return;

        state.Multipliers.Remove(source);

        if (state.Routines.TryGetValue(source, out Coroutine runningRoutine) && runningRoutine != null)
            StopCoroutine(runningRoutine);

        state.Routines.Remove(source);

        if (state.Multipliers.Count <= 0)
        {
            if (escapeMotor.settings != null)
                escapeMotor.settings.repathCooldown = state.OriginalCooldown;

            routeDelayStates.Remove(escapeMotor);
            return;
        }

        ApplyRouteDelayState(escapeMotor, state);
    }

    private void ApplyRouteDelayState(TargetEscapeMotor escapeMotor, RouteDelayState state)
    {
        if (escapeMotor == null || escapeMotor.settings == null || state == null)
            return;

        float highestMultiplier = 1f;

        foreach (float multiplier in state.Multipliers.Values)
        {
            if (multiplier > highestMultiplier)
                highestMultiplier = multiplier;
        }

        escapeMotor.settings.repathCooldown = state.OriginalCooldown * highestMultiplier;
    }

    private void RefreshMovementMultipliers()
    {
        if (navAgent == null || stats == null)
            return;

        float speed = stats.moveSpeed;
        float acceleration = stats.acceleration;

        if (isActionActive)
        {
            speed *= actionStarUpgradeApplied ? actionStarMoveSpeedMultiplier : actionMoveSpeedMultiplier;

            if (actionStarUpgradeApplied)
                acceleration *= actionStarAccelerationMultiplier;
        }

        if (isAdLibActive)
        {
            float adLibSpeed = stats.moveSpeed * adLibMoveSpeedMultiplier;

            if (adLibSpeed > speed)
                speed = adLibSpeed;
        }

        navAgent.speed = speed;
        navAgent.acceleration = acceleration;
    }

    private bool ResolveActorIsMoving()
    {
        if (navAgent == null)
            return false;

        if (isResultAnimationLocked || isHitReactionLocked || isSceneStealerAnimationLocked || isActionAnimationLocked)
            return false;

        if (navAgent.isStopped)
            return false;

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            currentTarget != null ||
            HasActivePathForActorAnimation();

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForActorAnimation();

        return hasMovementIntent && (hasVelocity || hasNotReachedDestination);
    }

    private ActorMoveMode ResolveActorMoveMode(bool isMoving)
    {
        if (!isMoving)
            return ActorMoveMode.Idle;

        if (IsSmokeDebuffed || IsSkillCommandBlocked)
            return ActorMoveMode.DebuffedRun;

        return ActorMoveMode.Run;
    }

    private bool HasActivePathForActorAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForActorAnimation();
    }

    private bool HasReachedDestinationForActorAnimation()
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
        return navAgent.remainingDistance <= stopDistance + animationDestinationBuffer;
    }

    private void StartSceneStealerAnimation()
    {
        if (!CanStartActorSkillAnimation(hasSceneStealerTrigger, SceneStealerTriggerName))
            return;

        if (sceneStealerAnimationRoutine != null)
            StopCoroutine(sceneStealerAnimationRoutine);

        sceneStealerAnimationRoutine = StartCoroutine(SceneStealerAnimationRoutineForAnimator());
    }

    private void StartActionAnimation()
    {
        if (!CanStartActorSkillAnimation(hasActionTrigger, ActionTriggerName))
            return;

        if (actionAnimationRoutine != null)
            StopCoroutine(actionAnimationRoutine);

        actionAnimationRoutine = StartCoroutine(ActionAnimationRoutineForAnimator());
    }

    private bool CanStartActorSkillAnimation(bool hasTrigger, string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[Actor {AgentID}] Animator가 없어서 {triggerName} 애니메이션을 실행할 수 없습니다.");
            return false;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Actor {AgentID}] Animator에 {triggerName} Trigger가 없습니다.");
            return false;
        }

        return true;
    }

    private IEnumerator SceneStealerAnimationRoutineForAnimator()
    {
        isSceneStealerAnimationLocked = true;

        yield return PlayActorSkillAnimationRoutine(
            sceneStealerHash,
            hasSceneStealerTrigger,
            SceneStealerTriggerName,
            sceneStealerAnimationLockSeconds
        );

        isSceneStealerAnimationLocked = false;
        sceneStealerAnimationRoutine = null;

        UpdateAnimationState(true);
    }

    private IEnumerator ActionAnimationRoutineForAnimator()
    {
        isActionAnimationLocked = true;

        yield return PlayActorSkillAnimationRoutine(
            actionHash,
            hasActionTrigger,
            ActionTriggerName,
            actionAnimationLockSeconds
        );

        isActionAnimationLocked = false;
        actionAnimationRoutine = null;

        UpdateAnimationState(true);
    }

    private IEnumerator PlayActorSkillAnimationRoutine(int triggerHash, bool hasTrigger, string triggerName, float lockSeconds)
    {
        if (animator == null || !hasTrigger)
            yield break;

        if (stopWhenUseSkillAnimation && navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
        }

        currentTarget = null;
        isManualMoving = false;

        UpdateAnimationState(true);

        ResetActorSkillAndReactionTriggers();
        animator.SetTrigger(triggerHash);

        if (debugActorLog)
            Debug.Log($"[Actor {AgentID}] {triggerName} 애니메이션 실행");

        if (lockSeconds > 0f)
            yield return new WaitForSeconds(lockSeconds);

        if (!isResultAnimationLocked && !isSceneStealerActive && navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;
    }

    private IEnumerator HitReactionRoutine(Vector3 hitSourcePosition)
    {
        isHitReactionLocked = true;

        StopSceneStealerAnimationRoutine();
        StopActionAnimationRoutine();

        hitReactionHadNavAgentState = navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh;
        hitReactionPreviousStopped = navAgent != null && navAgent.isStopped;
        hitReactionPreviousUpdateRotation = navAgent == null || navAgent.updateRotation;

        if (hitReactionHadNavAgentState)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
            navAgent.updateRotation = false;
        }

        currentTarget = null;
        isManualMoving = false;

        if (faceAwayFromHitSource)
            FaceAwayFromHitSource(hitSourcePosition);

        UpdateAnimationState(true);
        ResetActorSkillAndReactionTriggers();
        SetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);

        if (hitReactionLockSeconds > 0f)
            yield return new WaitForSeconds(hitReactionLockSeconds);

        RestoreHitReactionNavAgentState();

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

    private void KeepStopped()
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        navAgent.ResetPath();
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
    }

    private void PlayResultAnimation(int triggerHash, bool hasTrigger, string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[Actor {AgentID}] Animator가 없어서 {triggerName} 애니메이션을 실행할 수 없습니다.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Actor {AgentID}] Animator에 {triggerName} Trigger가 없습니다.");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;
        isSceneStealerAnimationLocked = false;
        isActionAnimationLocked = false;

        StopHitReactionRoutine();
        StopSceneStealerAnimationRoutine();
        StopActionAnimationRoutine();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStopped();
        UpdateAnimationState(true);

        ResetActorSkillAndReactionTriggers();
        animator.SetTrigger(triggerHash);

        if (debugActorLog)
            Debug.Log($"[Actor {AgentID}] {triggerName} 애니메이션 실행");
    }

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
        isHitReactionLocked = false;
        RestoreHitReactionNavAgentState();
    }

    private void RestoreHitReactionNavAgentState()
    {
        if (!hitReactionHadNavAgentState)
            return;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh && !isResultAnimationLocked)
        {
            navAgent.isStopped = hitReactionPreviousStopped;
            navAgent.updateRotation = hitReactionPreviousUpdateRotation;
        }

        hitReactionHadNavAgentState = false;
    }

    private void StopSceneStealerAnimationRoutine()
    {
        if (sceneStealerAnimationRoutine == null)
            return;

        StopCoroutine(sceneStealerAnimationRoutine);
        sceneStealerAnimationRoutine = null;
        isSceneStealerAnimationLocked = false;
    }

    private void StopActionAnimationRoutine()
    {
        if (actionAnimationRoutine == null)
            return;

        StopCoroutine(actionAnimationRoutine);
        actionAnimationRoutine = null;
        isActionAnimationLocked = false;
    }

    private void ResetActorSkillAndReactionTriggers()
    {
        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);
        ResetAnimatorTrigger(sceneStealerHash, hasSceneStealerTrigger);
        ResetAnimatorTrigger(actionHash, hasActionTrigger);
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

    private void CacheActorAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
        sceneStealerHash = Animator.StringToHash(SceneStealerTriggerName);
        actionHash = Animator.StringToHash(ActionTriggerName);
    }

    private void CacheActorAnimatorParameters()
    {
        hasIsMovingParameter = HasAnimatorParameter(IsMovingParameter, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(MoveSpeedParameter, AnimatorControllerParameterType.Float);
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasHitReactionTrigger = HasAnimatorParameter(HitReactionTriggerName, AnimatorControllerParameterType.Trigger);
        hasVictoryTrigger = HasAnimatorParameter(VictoryTriggerName, AnimatorControllerParameterType.Trigger);
        hasDefeatTrigger = HasAnimatorParameter(DefeatTriggerName, AnimatorControllerParameterType.Trigger);
        hasSceneStealerTrigger = HasAnimatorParameter(SceneStealerTriggerName, AnimatorControllerParameterType.Trigger);
        hasActionTrigger = HasAnimatorParameter(ActionTriggerName, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    private TargetController ResolveTargetController(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        TargetController target = targetTransform.GetComponent<TargetController>();

        if (target != null)
            return target;

        target = targetTransform.GetComponentInParent<TargetController>();

        if (target != null)
            return target;

        return targetTransform.GetComponentInChildren<TargetController>();
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

    private void StopAllActorEffects()
    {
        if (sceneStealerRoutine != null)
        {
            StopCoroutine(sceneStealerRoutine);
            sceneStealerRoutine = null;
        }

        if (sceneStealerRevealRoutine != null)
        {
            StopCoroutine(sceneStealerRevealRoutine);
            sceneStealerRevealRoutine = null;
        }

        if (sceneStealerFaceRoutine != null)
        {
            StopCoroutine(sceneStealerFaceRoutine);
            sceneStealerFaceRoutine = null;
        }

        StopHitReactionRoutine();
        StopSceneStealerAnimationRoutine();
        StopActionAnimationRoutine();

        if (adLibRoutine != null)
        {
            StopCoroutine(adLibRoutine);
            adLibRoutine = null;
        }

        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        if (activeSceneStealerVisibleTarget != null)
        {
            activeSceneStealerVisibleTarget.ClearForceVisible(this);
            activeSceneStealerVisibleTarget = null;
        }

        RestoreSceneStealerFacingAgentRotation();
        StopCloseUpVisibilityRoutine();
        HideSceneStealerTargetFeedback();
        HideSceneStealerIndicator();

        foreach (KeyValuePair<TargetSkillController, Coroutine> pair in targetSkillBlockRoutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);

            if (pair.Key != null)
                pair.Key.SetTargetSkillBlocked(this, false);
        }

        targetSkillBlockRoutines.Clear();

        foreach (KeyValuePair<TargetEscapeMotor, RouteDelayState> pair in routeDelayStates)
        {
            TargetEscapeMotor escapeMotor = pair.Key;
            RouteDelayState state = pair.Value;

            if (escapeMotor != null && escapeMotor.settings != null && state != null)
                escapeMotor.settings.repathCooldown = state.OriginalCooldown;

            if (state == null)
                continue;

            foreach (Coroutine routine in state.Routines.Values)
            {
                if (routine != null)
                    StopCoroutine(routine);
            }
        }

        routeDelayStates.Clear();

        isSceneStealerActive = false;
        isAdLibActive = false;
        isActionActive = false;
        isHitReactionLocked = false;
        isSceneStealerAnimationLocked = false;
        isActionAnimationLocked = false;

        RemoveActionVision();

        if (navAgent != null)
            navAgent.isStopped = false;

        RefreshMovementMultipliers();
    }
}