using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public class Trickster : AgentController, IUpgradeReceiver
{
    private enum TricksterMoveMode
    {
        IdleAlert = 0,
        CommandRun = 1,
        DebuffedRun = 2
    }

    private const string SkillFakeBox = "fakebox";
    private const string SkillJokerCard = "jokercard";
    private const string SkillVanishing = "vanishing";
    private const string SkillMisdirection = "misdirection";

    private const string UpgradeFakeBoxMultiTrick = "trickster_fake_box_multi_trick";
    private const string UpgradeFakeBoxReverseRoute = "trickster_fake_box_reverse_route";
    private const string UpgradeJokerCardWildJoker = "trickster_joker_card_wild_joker";
    private const string UpgradeJokerCardStageControl = "trickster_joker_card_stage_control";

    private const string UpgradeUnlockVanishing = "trickster_unlock_vanishing";
    private const string UpgradeUnlockMisdirection = "trickster_unlock_misdirection";

    private const string UpgradeVanishingStageTransition = "trickster_vanishing_stage_transition";
    private const string UpgradeVanishingSpotlight = "trickster_vanishing_spotlight";
    private const string UpgradeMisdirectionFlawlessActing = "trickster_misdirection_flawless_acting";
    private const string UpgradeMisdirectionPerfectGaze = "trickster_misdirection_perfect_gaze";

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string FakeBoxTriggerName = "FakeBox";
    private const string VanishingStartTriggerName = "VanishingStart";
    private const string VanishingSuccessTriggerName = "VanishingSuccess";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";

    private const string JokerCardEffectObjectName = "JokerCard";
    private const string JokerCardEffectParentObjectName = "EffectAnchor";

    private const float StableMovingThreshold = 0.08f;
    private const float ArrivalVelocityStopThreshold = 0.2f;

    [Header("Fake Box")]
    [SerializeField] private FakeBox fakeBoxPrefab;
    [SerializeField] private Transform deployParent;
    [SerializeField] private float deployYOffset = 0f;
    [SerializeField] private bool replaceExistingFakeBox = true;

    [Header("Vanishing")]
    [SerializeField] private GameObject vanishingCurtainPrefab;
    [SerializeField] private Transform vanishingEffectParent;
    [SerializeField] private float vanishingCastTime = 5f;
    [SerializeField] private float vanishingRecoveryLockSeconds = 5f;
    [SerializeField] private float vanishingNavMeshSampleDistance = 0.35f;
    [SerializeField] private float vanishingEffectLifetime = 2.5f;
    [SerializeField] private bool spawnCurtainAtStart = true;
    [SerializeField] private bool spawnCurtainAtEnd = true;

    [Header("Vanishing Camera")]
    [SerializeField] private bool requestCameraOnVanishingStart = true;
    [SerializeField] private bool requestCameraOnVanishingSuccess = true;

    [Header("Vanishing Spotlight Upgrade")]
    [SerializeField] private GameObject spotlightEffectPrefab;
    [SerializeField] private float vanishingSpotlightDuration = 5f;
    [SerializeField] private Vector3 spotlightEffectLocalOffset = Vector3.zero;

    [Header("Misdirection")]
    [SerializeField] private float misdirectionDuration = 10f;

    [Header("Misdirection Visual")]
    [SerializeField] private Transform misdirectionMaterialRoot;
    [SerializeField] private string misdirectionMaterialRootName = "char1";
    [SerializeField] private Material misdirectionMaterial;
    [SerializeField] private bool autoFindMisdirectionMaterialRoot = true;
    [SerializeField] private bool applyMisdirectionMaterialToChildren = true;
    [SerializeField] private bool replaceAllMisdirectionMaterials = true;

    [Header("Upgrade - Trickster")]
    [SerializeField] private float multiFakeBoxGaugeCostMultiplier = 0.5f;
    [SerializeField] private int multiFakeBoxMaxActiveCount = 3;
    [SerializeField] private float wildJokerDurationMultiplier = 2f;
    [SerializeField] private float wildJokerBuffEffectMultiplier = 1.5f;
    [SerializeField] private float stageTransitionVanishingTimeMultiplier = 0.5f;
    [SerializeField] private float flawlessActingMoveSpeedMultiplier = 1.5f;
    [SerializeField] private float perfectGazeDurationMultiplier = 1.5f;
    [SerializeField] private float perfectGazeGaugeCostMultiplier = 0.75f;

    [Header("Joker Card Effect")]
    [SerializeField] private JokerCard jokerCardEffectInstance;
    [SerializeField] private JokerCard jokerCardEffectPrefab;
    [SerializeField] private Transform jokerCardEffectParent;
    [SerializeField] private bool autoFindJokerCardEffect = true;
    [SerializeField] private bool destroyJokerCardEffectOnEnd = false;

    [Header("Trickster Animation")]
    [SerializeField] private float tricksterMovingThreshold = 0.03f;
    [SerializeField] private float animationStopDelay = 0.2f;
    [SerializeField] private float destinationBuffer = 0.2f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private float fakeBoxAnimationLockSeconds = 0.45f;
    [SerializeField] private float fakeBoxDeployDelay = 0.15f;
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    private readonly List<FakeBox> currentFakeBoxes = new List<FakeBox>();

    private FakeBox currentFakeBox;
    private Coroutine jokerCardRoutine;
    private Coroutine vanishingRoutine;
    private Coroutine misdirectionRoutine;
    private Coroutine vanishingSpotlightRoutine;
    private JokerCard currentJokerCardEffect;

    private bool isJokerCardActive;
    private bool isVanishing;
    private bool isVanishingRecoveryLocked;
    private bool isMisdirectionActive;
    private bool hasCachedJokerCardValues;

    private float currentFakeBoxGaugeCostMultiplier = 1f;
    private bool currentAllowMultipleFakeBoxes;
    private int currentMaxActiveFakeBoxCount = 1;
    private bool currentFakeBoxReverseRouteEnabled;
    private float currentJokerCardDurationMultiplier = 1f;
    private float currentJokerCardBuffEffectMultiplier = 1f;
    private bool currentJokerCardDebuffImmunity;

    private float currentVanishingCastTimeMultiplier = 1f;
    private float currentVanishingRecoveryLockMultiplier = 1f;
    private bool currentVanishingSpotlightEnabled;

    private float currentMisdirectionMoveSpeedMultiplier = 1f;
    private float currentMisdirectionDurationMultiplier = 1f;
    private float currentMisdirectionGaugeCostMultiplier = 1f;

    private bool hasMisdirectionSpeedSnapshot;
    private float misdirectionSpeedSnapshot;

    private Renderer[] misdirectionRenderers;
    private Material[][] originalMisdirectionMaterials;
    private bool hasCachedMisdirectionMaterials;
    private bool isMisdirectionMaterialApplied;

    private GameObject currentSpotlightEffectInstance;
    private TargetController currentSpotlightTarget;

    private float originalMoveSpeed;
    private float originalSpotLightRange;
    private float originalSpotLightOuterAngle;

    private FloatMemberSnapshot viewRadiusSnapshot;
    private FloatMemberSnapshot viewAngleSnapshot;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int fakeBoxHash;
    private int vanishingStartHash;
    private int vanishingSuccessHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasFakeBoxTrigger;
    private bool hasVanishingStartTrigger;
    private bool hasVanishingSuccessTrigger;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;

    private bool cachedTricksterAnimationIsMoving;
    private float lastTricksterAnimationMovingTime = -999f;

    private bool isSkillAnimationLocked;
    private bool isHitReactionLocked;
    private bool isResultAnimationLocked;

    private Coroutine fakeBoxRoutine;
    private Coroutine hitReactionRoutine;

    public bool IsResultAnimationLocked => isResultAnimationLocked;
    public bool IsJokerCardIgnoringDebuffs => IsJokerCardDebuffImmunityActive;
    public bool IsVanishing => isVanishing;
    public bool IsVanishingRecoveryLocked => isVanishingRecoveryLocked;
    public bool IsVanishingMovementLocked => isVanishing || isVanishingRecoveryLocked;
    public bool IsMisdirectionActive => isMisdirectionActive;
    public bool CanUseVanishingSkill => IsAgentUpgradeUnlocked(UpgradeUnlockVanishing);
    public bool CanUseMisdirectionSkill => IsAgentUpgradeUnlocked(UpgradeUnlockMisdirection);

    public override bool CanCatchTarget => !isMisdirectionActive;
    public override bool CanBeDetectedByTarget => !isMisdirectionActive;

    protected override bool ShouldIgnoreDebuffStateIcon => IsJokerCardDebuffImmunityActive;

    private bool IsJokerCardDebuffImmunityActive => isJokerCardActive && currentJokerCardDebuffImmunity;

    protected override void Awake()
    {
        agentID = 3;

        CacheTricksterAnimationHashes();

        base.Awake();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheTricksterAnimatorParameters();
        AutoCacheJokerCardEffectReferences();
        AutoCacheMisdirectionMaterialReferences();
        CacheOriginalMisdirectionMaterials();

        if (jokerCardEffectInstance != null)
            currentJokerCardEffect = jokerCardEffectInstance;

        UpdateAnimationState(true);
    }

    protected override void Update()
    {
        if (isResultAnimationLocked)
        {
            KeepStoppedForLockedAnimation();
            UpdateAnimationState();
            MaintainJokerCardDebuffImmunity();
            return;
        }

        if (isHitReactionLocked || isSkillAnimationLocked || IsVanishingMovementLocked)
        {
            KeepStoppedForLockedAnimation();
            UpdateAnimationState();
            MaintainJokerCardDebuffImmunity();
            return;
        }

        base.Update();
        TryAutoUseJokerCard();
        MaintainJokerCardDebuffImmunity();
    }

    protected override void OnDisable()
    {
        StopJokerCard(true);
        StopFakeBoxRoutine();
        StopHitReactionRoutine();
        StopVanishingRoutine();
        StopMisdirectionRoutine();
        StopVanishingSpotlightReveal();

        isSkillAnimationLocked = false;
        isHitReactionLocked = false;
        isResultAnimationLocked = false;
        cachedTricksterAnimationIsMoving = false;
        lastTricksterAnimationMovingTime = -999f;

        base.OnDisable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        deployYOffset = Mathf.Max(0f, deployYOffset);
        multiFakeBoxGaugeCostMultiplier = Mathf.Clamp(multiFakeBoxGaugeCostMultiplier, 0.01f, 1f);
        multiFakeBoxMaxActiveCount = Mathf.Max(2, multiFakeBoxMaxActiveCount);
        wildJokerDurationMultiplier = Mathf.Max(1f, wildJokerDurationMultiplier);
        wildJokerBuffEffectMultiplier = Mathf.Max(1f, wildJokerBuffEffectMultiplier);
        stageTransitionVanishingTimeMultiplier = Mathf.Clamp(stageTransitionVanishingTimeMultiplier, 0.01f, 1f);
        flawlessActingMoveSpeedMultiplier = Mathf.Max(1f, flawlessActingMoveSpeedMultiplier);
        perfectGazeDurationMultiplier = Mathf.Max(1f, perfectGazeDurationMultiplier);
        perfectGazeGaugeCostMultiplier = Mathf.Clamp(perfectGazeGaugeCostMultiplier, 0.01f, 1f);

        vanishingCastTime = Mathf.Max(0f, vanishingCastTime);
        vanishingRecoveryLockSeconds = Mathf.Max(0f, vanishingRecoveryLockSeconds);
        vanishingNavMeshSampleDistance = Mathf.Max(0.01f, vanishingNavMeshSampleDistance);
        vanishingEffectLifetime = Mathf.Max(0f, vanishingEffectLifetime);
        vanishingSpotlightDuration = Mathf.Max(0f, vanishingSpotlightDuration);
        misdirectionDuration = Mathf.Max(0f, misdirectionDuration);

        tricksterMovingThreshold = Mathf.Max(0f, tricksterMovingThreshold);
        animationStopDelay = Mathf.Max(0f, animationStopDelay);
        destinationBuffer = Mathf.Max(0f, destinationBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);
        fakeBoxAnimationLockSeconds = Mathf.Max(0f, fakeBoxAnimationLockSeconds);
        fakeBoxDeployDelay = Mathf.Max(0f, fakeBoxDeployDelay);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        CacheTricksterAnimationHashes();
        AutoCacheJokerCardEffectReferences();

        if (Application.isPlaying)
        {
            AutoCacheMisdirectionMaterialReferences();
            CacheOriginalMisdirectionMaterials();
        }
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked || isHitReactionLocked || isSkillAnimationLocked || IsVanishingMovementLocked)
            return;

        if (!CanReceivePlayerSkillCommand(true))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Trickster {AgentID}] Skill request: {skillName}, Position: {targetPos}");

        if (IsFakeBoxSkill(skill))
        {
            ExecuteFakeBox(targetPos);
            return;
        }

        if (IsVanishingSkill(skill))
        {
            ExecuteVanishing(targetPos);
            return;
        }

        if (IsMisdirectionSkill(skill))
        {
            ExecuteMisdirection();
            return;
        }

        if (IsJokerCardSkill(skill))
        {
            Debug.LogWarning($"[Trickster {AgentID}] Joker Card is an automatic skill. It activates when its gauge is full.");
            return;
        }

        Debug.LogWarning($"[Trickster {AgentID}] Unknown skill: {skillName}");
    }

    protected override void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        bool isMoving = ResolveTricksterAnimationIsMoving();
        TricksterMoveMode moveMode = ResolveTricksterMoveMode(isMoving);

        if (hasIsMovingParameter)
            animator.SetBool(isMovingHash, isMoving);

        if (hasMoveModeParameter)
            animator.SetInteger(moveModeHash, (int)moveMode);

        if (!hasMoveSpeedParameter)
            return;

        float actualSpeed = navAgent.velocity.magnitude;
        float effectiveMovingThreshold = Mathf.Max(tricksterMovingThreshold, StableMovingThreshold);

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

    protected override void CheckDestinationReached()
    {
        if (navAgent == null)
            return;

        if (!isManualMoving)
            return;

        if (navAgent.pathPending)
            return;

        if (!navAgent.hasPath)
        {
            CompleteManualMove("Manual move finished. Path no longer exists.");
            return;
        }

        if (float.IsInfinity(navAgent.remainingDistance))
            return;

        float stopDistance = Mathf.Max(navAgent.stoppingDistance, 0.05f);
        float arrivalDistance = stopDistance + destinationBuffer;
        float realDistance = Vector3.Distance(transform.position, navAgent.destination);

        bool closeByRemainingDistance = navAgent.remainingDistance <= arrivalDistance;
        bool closeByRealDistance = realDistance <= arrivalDistance;
        bool almostStopped = navAgent.velocity.sqrMagnitude <= ArrivalVelocityStopThreshold * ArrivalVelocityStopThreshold;

        if ((closeByRemainingDistance && closeByRealDistance) ||
            ((closeByRemainingDistance || closeByRealDistance) && almostStopped))
        {
            CompleteManualMove("Reached manual destination. Trickster forced to idle.");
        }
    }

    public override void PlayHitReaction(Vector3 hitSourcePosition)
    {
        if (isResultAnimationLocked)
            return;

        if (IsJokerCardDebuffImmunityActive)
        {
            Debug.Log($"[Trickster {AgentID}] 조커 카드 효과로 피격 방해 효과를 무효화했습니다.");
            return;
        }

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
        isSkillAnimationLocked = false;

        StopFakeBoxRoutine();
        StopHitReactionRoutine();
        StopVanishingRoutine();
        StopMisdirectionRoutine();
        StopVanishingSpotlightReveal();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    public override float GetSkillGaugeRequiredForSkill(string skillName)
    {
        float requiredGauge = base.GetSkillGaugeRequiredForSkill(skillName);

        if (IsFakeBoxSkill(skillName))
            requiredGauge *= currentFakeBoxGaugeCostMultiplier;

        if (IsMisdirectionSkill(skillName))
            requiredGauge *= currentMisdirectionGaugeCostMultiplier;

        return Mathf.Max(0f, requiredGauge);
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        List<string> gaugeKeys = new List<string>
        {
            SkillFakeBox,
            SkillJokerCard
        };

        if (CanUseVanishingSkill)
            gaugeKeys.Add(SkillVanishing);

        if (CanUseMisdirectionSkill)
            gaugeKeys.Add(SkillMisdirection);

        return gaugeKeys.ToArray();
    }

    public override void AddSkillCommandBlocker(object source)
    {
        if (IsJokerCardDebuffImmunityActive)
        {
            Debug.Log($"[Trickster {AgentID}] 조커 카드 효과로 스킬 차단 디버프를 무효화했습니다.");
            return;
        }

        base.AddSkillCommandBlocker(source);
    }

    public override bool CanReceivePlayerSkillCommand(bool showWarning = false)
    {
        if (IsVanishingMovementLocked)
        {
            if (showWarning)
                Debug.LogWarning($"[Trickster {AgentID}] 배니싱 중이거나 배니싱 직후 경직 상태라 명령을 받을 수 없습니다.");

            return false;
        }

        if (IsJokerCardDebuffImmunityActive)
            return true;

        return base.CanReceivePlayerSkillCommand(showWarning);
    }

    public override void MoveTo(Vector3 destination)
    {
        if (IsVanishingMovementLocked)
        {
            Debug.Log($"[Trickster {AgentID}] 배니싱 중이거나 배니싱 직후 경직 상태라 이동 명령을 무시합니다. destination={destination}");

            ForceStopForSkill();
            UpdateAnimationState(true);
            UpdateStateIcon();
            return;
        }

        base.MoveTo(destination);
    }

    public override void SetChaseTarget(Transform target)
    {
        if (IsVanishingMovementLocked)
        {
            if (target != null)
                Debug.Log($"[Trickster {AgentID}] 배니싱 중이거나 배니싱 직후 경직 상태라 타겟을 봐도 추격하지 않습니다. target={target.name}");

            ForceStopForSkill();
            UpdateAnimationState(true);
            UpdateStateIcon();
            return;
        }

        base.SetChaseTarget(target);
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return CanApplyUpgradeByAgentDefinitionOrLegacy(
            upgrade,
            CommanderAgentType.Trickster
        );
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        switch (upgrade.UpgradeId)
        {
            case UpgradeFakeBoxMultiTrick:
                ApplyMultiTrickUpgrade(upgrade.Value);
                break;

            case UpgradeFakeBoxReverseRoute:
                ApplyReverseRouteUpgrade();
                break;

            case UpgradeJokerCardWildJoker:
                ApplyWildJokerUpgrade(upgrade.Value);
                break;

            case UpgradeJokerCardStageControl:
                ApplyStageControlUpgrade(upgrade);
                break;

            case UpgradeVanishingStageTransition:
                ApplyVanishingStageTransitionUpgrade(upgrade.Value);
                break;

            case UpgradeVanishingSpotlight:
                ApplyVanishingSpotlightUpgrade(upgrade);
                break;

            case UpgradeMisdirectionFlawlessActing:
                ApplyMisdirectionFlawlessActingUpgrade(upgrade.Value);
                break;

            case UpgradeMisdirectionPerfectGaze:
                ApplyMisdirectionPerfectGazeUpgrade(upgrade.Value);
                break;

            default:
                Debug.LogWarning($"[Trickster {AgentID}] 알 수 없는 강화 ID입니다: {upgrade.UpgradeId}");
                break;
        }
    }

    private void ApplyMultiTrickUpgrade(float value)
    {
        currentFakeBoxGaugeCostMultiplier = value > 0f
            ? Mathf.Clamp(value, 0.01f, 1f)
            : multiFakeBoxGaugeCostMultiplier;

        currentAllowMultipleFakeBoxes = true;
        currentMaxActiveFakeBoxCount = Mathf.Max(2, multiFakeBoxMaxActiveCount);

        Debug.Log(
            $"[Trickster {AgentID}] 다중 속임수 강화 적용. " +
            $"GaugeCostMultiplier={currentFakeBoxGaugeCostMultiplier:F2}, " +
            $"MaxActiveFakeBoxCount={currentMaxActiveFakeBoxCount}"
        );
    }

    private void ApplyReverseRouteUpgrade()
    {
        currentFakeBoxReverseRouteEnabled = true;

        Debug.Log($"[Trickster {AgentID}] 반전된 경로 강화 적용.");
    }

    private void ApplyWildJokerUpgrade(float value)
    {
        currentJokerCardDurationMultiplier = value > 0f
            ? Mathf.Max(1f, value)
            : wildJokerDurationMultiplier;

        currentJokerCardBuffEffectMultiplier = wildJokerBuffEffectMultiplier;

        Debug.Log(
            $"[Trickster {AgentID}] 와일드 조커 강화 적용. " +
            $"DurationMultiplier={currentJokerCardDurationMultiplier:F2}, " +
            $"BuffEffectMultiplier={currentJokerCardBuffEffectMultiplier:F2}"
        );
    }

    private void ApplyStageControlUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
            return;

        if (upgrade.EffectType != UpgradeEffectType.BoolEnable)
        {
            Debug.LogWarning(
                $"[Trickster {AgentID}] 무대 장악 강화의 Effect Type은 BoolEnable을 권장합니다. " +
                $"CurrentType={upgrade.EffectType}"
            );
        }

        if (upgrade.Value <= 0f)
        {
            Debug.LogWarning(
                $"[Trickster {AgentID}] 무대 장악 강화의 Value가 0 이하입니다. " +
                "BoolEnable 강화는 Value를 1로 설정하세요."
            );
            return;
        }

        currentJokerCardDebuffImmunity = true;

        Debug.Log(
            $"[Trickster {AgentID}] 무대 장악 강화 적용. " +
            "조커 카드 지속시간 동안 디버프를 무효화합니다."
        );
    }

    private void ApplyVanishingStageTransitionUpgrade(float value)
    {
        float multiplier = value > 0f
            ? Mathf.Clamp(value, 0.01f, 1f)
            : stageTransitionVanishingTimeMultiplier;

        currentVanishingCastTimeMultiplier = multiplier;
        currentVanishingRecoveryLockMultiplier = multiplier;

        Debug.Log(
            $"[Trickster {AgentID}] 무대 전환 강화 적용. " +
            $"VanishingCastTimeMultiplier={currentVanishingCastTimeMultiplier:F2}, " +
            $"RecoveryLockMultiplier={currentVanishingRecoveryLockMultiplier:F2}"
        );
    }

    private void ApplyVanishingSpotlightUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
            return;

        if (upgrade.EffectType != UpgradeEffectType.BoolEnable)
        {
            Debug.LogWarning(
                $"[Trickster {AgentID}] 스포트라이트 강화의 Effect Type은 BoolEnable을 권장합니다. " +
                $"CurrentType={upgrade.EffectType}"
            );
        }

        if (upgrade.Value <= 0f)
        {
            Debug.LogWarning($"[Trickster {AgentID}] 스포트라이트 강화 Value가 0 이하입니다. BoolEnable 강화는 Value를 1로 설정하세요.");
            return;
        }

        currentVanishingSpotlightEnabled = true;
        Debug.Log($"[Trickster {AgentID}] 스포트라이트 강화 적용. 배니싱 성공 시 타겟 위치를 표시합니다.");
    }

    private void ApplyMisdirectionFlawlessActingUpgrade(float value)
    {
        currentMisdirectionMoveSpeedMultiplier = value > 0f
            ? Mathf.Max(1f, value)
            : flawlessActingMoveSpeedMultiplier;

        Debug.Log(
            $"[Trickster {AgentID}] 빈틈 없는 연기 강화 적용. " +
            $"MoveSpeedMultiplier={currentMisdirectionMoveSpeedMultiplier:F2}"
        );
    }

    private void ApplyMisdirectionPerfectGazeUpgrade(float value)
    {
        currentMisdirectionDurationMultiplier = perfectGazeDurationMultiplier;
        currentMisdirectionGaugeCostMultiplier = value > 0f
            ? Mathf.Clamp(value, 0.01f, 1f)
            : perfectGazeGaugeCostMultiplier;

        Debug.Log(
            $"[Trickster {AgentID}] 완벽한 시선 유도 강화 적용. " +
            $"DurationMultiplier={currentMisdirectionDurationMultiplier:F2}, " +
            $"GaugeCostMultiplier={currentMisdirectionGaugeCostMultiplier:F2}"
        );
    }

    private void ExecuteVanishing(Vector3 targetPos)
    {
        if (!CanUseVanishingSkill)
        {
            Debug.LogWarning($"[Trickster {AgentID}] 아직 배니싱 스킬을 사용할 수 없습니다.");
            return;
        }

        if (IsVanishingMovementLocked)
        {
            Debug.LogWarning($"[Trickster {AgentID}] 이미 배니싱 중이거나 배니싱 직후 경직 상태입니다.");
            return;
        }

        if (!TryResolveVanishingPosition(targetPos, out Vector3 warpPosition))
        {
            Debug.LogWarning($"[Trickster {AgentID}] 배니싱 취소. 지정 좌표가 NavMesh 위가 아닙니다. Target={targetPos}");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillVanishing))
            return;

        ForceStopForSkill();

        if (vanishingRoutine != null)
            StopCoroutine(vanishingRoutine);

        vanishingRoutine = StartCoroutine(VanishingRoutine(warpPosition));
    }

    private IEnumerator VanishingRoutine(Vector3 warpPosition)
    {
        isVanishing = true;
        isVanishingRecoveryLocked = false;
        isSkillAnimationLocked = true;

        ForceStopForSkill();
        UpdateAnimationState(true);

        PlayVanishingStartAnimation();
        RequestVanishingStartCamera();

        if (spawnCurtainAtStart)
            SpawnVanishingCurtain(transform.position);

        float castTime = stats != null ? stats.vanishingCastTime : vanishingCastTime;
        castTime *= currentVanishingCastTimeMultiplier;
        castTime = Mathf.Max(0f, castTime);

        Debug.Log($"[Trickster {AgentID}] 배니싱 시전 시작. CastTime={castTime:0.##}");

        if (castTime > 0f)
            yield return new WaitForSeconds(castTime);

        WarpToVanishingPosition(warpPosition);

        PlayVanishingSuccessAnimation();
        RequestVanishingSuccessCamera();

        if (spawnCurtainAtEnd)
            SpawnVanishingCurtain(warpPosition);

        if (currentVanishingSpotlightEnabled)
            StartVanishingSpotlightReveal();

        isVanishing = false;
        isVanishingRecoveryLocked = true;

        ForceStopForSkill();
        UpdateAnimationState(true);
        UpdateStateIcon();

        float recoveryLockSeconds = stats != null
            ? stats.vanishingRecoveryLockSeconds
            : vanishingRecoveryLockSeconds;

        recoveryLockSeconds *= currentVanishingRecoveryLockMultiplier;
        recoveryLockSeconds = Mathf.Max(0f, recoveryLockSeconds);

        Debug.Log($"[Trickster {AgentID}] 배니싱 성공. {recoveryLockSeconds:0.##}초 동안 이동/명령/추격이 제한됩니다. Position={warpPosition}");

        if (recoveryLockSeconds > 0f)
            yield return new WaitForSeconds(recoveryLockSeconds);

        isVanishingRecoveryLocked = false;
        isSkillAnimationLocked = false;
        vanishingRoutine = null;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        UpdateAnimationState(true);
        UpdateStateIcon();

        Debug.Log($"[Trickster {AgentID}] 배니싱 후 경직 종료.");
    }

    private bool TryResolveVanishingPosition(Vector3 targetPos, out Vector3 warpPosition)
    {
        warpPosition = targetPos;

        if (navAgent == null)
            return false;

        float sampleDistance = Mathf.Max(0.01f, vanishingNavMeshSampleDistance);

        if (!NavMesh.SamplePosition(targetPos, out NavMeshHit hit, sampleDistance, navAgent.areaMask))
            return false;

        warpPosition = hit.position;
        return true;
    }

    private void WarpToVanishingPosition(Vector3 warpPosition)
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(warpPosition);
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
            return;
        }

        transform.position = warpPosition;
    }

    private void SpawnVanishingCurtain(Vector3 position)
    {
        if (vanishingCurtainPrefab == null)
            return;

        Transform parent = vanishingEffectParent != null ? vanishingEffectParent : null;
        GameObject curtain = Instantiate(vanishingCurtainPrefab, position, Quaternion.identity, parent);

        if (vanishingEffectLifetime > 0f)
            Destroy(curtain, vanishingEffectLifetime);
    }

    private void RequestVanishingStartCamera()
    {
        if (!requestCameraOnVanishingStart)
            return;

        RequestFollowUserSkillCamera();
    }

    private void RequestVanishingSuccessCamera()
    {
        if (!requestCameraOnVanishingSuccess)
            return;

        RequestFollowUserSkillCamera();
    }

    private void StartVanishingSpotlightReveal()
    {
        StopVanishingSpotlightReveal();

        TargetController target = FindFirstObjectByType<TargetController>();

        if (target == null)
        {
            Debug.LogWarning($"[Trickster {AgentID}] 스포트라이트 대상을 찾지 못했습니다.");
            return;
        }

        vanishingSpotlightRoutine = StartCoroutine(VanishingSpotlightRevealRoutine(target));
    }

    private IEnumerator VanishingSpotlightRevealRoutine(TargetController target)
    {
        currentSpotlightTarget = target;

        if (currentSpotlightTarget != null)
            currentSpotlightTarget.AddReconReveal();

        SpawnSpotlightEffect(currentSpotlightTarget);

        float duration = Mathf.Max(0f, vanishingSpotlightDuration);

        Debug.Log($"[Trickster {AgentID}] 스포트라이트 시작. Duration={duration:0.##}");

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        ClearVanishingSpotlightState();
        vanishingSpotlightRoutine = null;
    }

    private void SpawnSpotlightEffect(TargetController target)
    {
        if (spotlightEffectPrefab == null || target == null)
            return;

        if (currentSpotlightEffectInstance != null)
            Destroy(currentSpotlightEffectInstance);

        currentSpotlightEffectInstance = Instantiate(spotlightEffectPrefab, target.transform);
        currentSpotlightEffectInstance.transform.localPosition = spotlightEffectLocalOffset;
        currentSpotlightEffectInstance.transform.localRotation = Quaternion.identity;
        currentSpotlightEffectInstance.transform.localScale = Vector3.one;
    }

    private void StopVanishingSpotlightReveal()
    {
        if (vanishingSpotlightRoutine != null)
        {
            StopCoroutine(vanishingSpotlightRoutine);
            vanishingSpotlightRoutine = null;
        }

        ClearVanishingSpotlightState();
    }

    private void ClearVanishingSpotlightState()
    {
        if (currentSpotlightTarget != null)
        {
            currentSpotlightTarget.RemoveReconReveal();
            currentSpotlightTarget = null;
        }

        if (currentSpotlightEffectInstance != null)
        {
            Destroy(currentSpotlightEffectInstance);
            currentSpotlightEffectInstance = null;
        }
    }

    private void ExecuteMisdirection()
    {
        if (!CanUseMisdirectionSkill)
        {
            Debug.LogWarning($"[Trickster {AgentID}] 아직 미스디렉션 스킬을 사용할 수 없습니다.");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillMisdirection))
            return;

        if (misdirectionRoutine != null)
        {
            StopCoroutine(misdirectionRoutine);
            misdirectionRoutine = null;
            EndMisdirectionState();
        }

        misdirectionRoutine = StartCoroutine(MisdirectionRoutine());
    }

    private IEnumerator MisdirectionRoutine()
    {
        BeginMisdirectionState();

        float duration = stats != null ? stats.misdirectionDuration : misdirectionDuration;
        duration *= currentMisdirectionDurationMultiplier;
        duration = Mathf.Max(0f, duration);

        Debug.Log($"[Trickster {AgentID}] 미스디렉션 시작. Duration={duration:0.##}, 충돌 유지, 포획 불가");

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        EndMisdirectionState();
        misdirectionRoutine = null;

        Debug.Log($"[Trickster {AgentID}] 미스디렉션 종료.");
    }

    private void BeginMisdirectionState()
    {
        isMisdirectionActive = true;

        ApplyMisdirectionMoveSpeedBoost();
        ApplyMisdirectionMaterial();

        UpdateStateIcon();
        RequestFollowUserSkillCamera();
    }

    private void EndMisdirectionState()
    {
        isMisdirectionActive = false;

        RestoreMisdirectionMoveSpeed();
        RestoreMisdirectionMaterial();

        UpdateStateIcon();
    }

    private void ApplyMisdirectionMoveSpeedBoost()
    {
        if (currentMisdirectionMoveSpeedMultiplier <= 1f)
            return;

        if (navAgent == null)
            return;

        if (hasMisdirectionSpeedSnapshot)
            return;

        misdirectionSpeedSnapshot = navAgent.speed;
        hasMisdirectionSpeedSnapshot = true;
        navAgent.speed = misdirectionSpeedSnapshot * currentMisdirectionMoveSpeedMultiplier;
    }

    private void RestoreMisdirectionMoveSpeed()
    {
        if (!hasMisdirectionSpeedSnapshot)
            return;

        if (navAgent != null)
            navAgent.speed = misdirectionSpeedSnapshot;

        hasMisdirectionSpeedSnapshot = false;
    }

    private void ExecuteFakeBox(Vector3 targetPos)
    {
        if (fakeBoxPrefab == null)
        {
            Debug.LogWarning($"[Trickster {AgentID}] fakeBoxPrefab is not assigned.");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillFakeBox))
            return;

        ForceStopForSkill();

        if (fakeBoxRoutine != null)
            StopCoroutine(fakeBoxRoutine);

        fakeBoxRoutine = StartCoroutine(FakeBoxRoutine(targetPos));
    }

    private IEnumerator FakeBoxRoutine(Vector3 targetPos)
    {
        isSkillAnimationLocked = true;

        KeepStoppedForLockedAnimation();
        UpdateAnimationState(true);
        PlayFakeBoxAnimation();

        float deployDelay = Mathf.Min(fakeBoxDeployDelay, fakeBoxAnimationLockSeconds);

        if (deployDelay > 0f)
            yield return new WaitForSeconds(deployDelay);

        DeployFakeBox(targetPos);

        float remainingLockTime = fakeBoxAnimationLockSeconds - deployDelay;

        if (remainingLockTime > 0f)
            yield return new WaitForSeconds(remainingLockTime);

        isSkillAnimationLocked = false;
        fakeBoxRoutine = null;

        UpdateAnimationState(true);
    }

    private void DeployFakeBox(Vector3 targetPos)
    {
        Vector3 spawnPos = BuildSpawnPosition(targetPos);

        CleanupNullFakeBoxes();

        bool shouldReplaceExisting = replaceExistingFakeBox && !currentAllowMultipleFakeBoxes;

        if (shouldReplaceExisting)
            DestroyAllFakeBoxes();
        else if (currentAllowMultipleFakeBoxes)
            TrimFakeBoxesBeforeDeploy();

        currentFakeBox = Instantiate(
            fakeBoxPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        currentFakeBox.SetOwner(this);
        currentFakeBox.SetReverseRouteEnabled(currentFakeBoxReverseRouteEnabled);

        currentFakeBoxes.Add(currentFakeBox);

        Debug.Log(
            $"[Trickster {AgentID}] Fake Box deployed: {spawnPos}, " +
            $"GaugeCostMultiplier={currentFakeBoxGaugeCostMultiplier:F2}, " +
            $"Multiple={currentAllowMultipleFakeBoxes}, " +
            $"ReverseRoute={currentFakeBoxReverseRouteEnabled}, " +
            $"ActiveCount={currentFakeBoxes.Count}"
        );
    }

    private void CleanupNullFakeBoxes()
    {
        for (int i = currentFakeBoxes.Count - 1; i >= 0; i--)
        {
            if (currentFakeBoxes[i] == null)
                currentFakeBoxes.RemoveAt(i);
        }

        if (currentFakeBox == null && currentFakeBoxes.Count > 0)
            currentFakeBox = currentFakeBoxes[currentFakeBoxes.Count - 1];
    }

    private void DestroyAllFakeBoxes()
    {
        for (int i = currentFakeBoxes.Count - 1; i >= 0; i--)
        {
            if (currentFakeBoxes[i] != null)
                Destroy(currentFakeBoxes[i].gameObject);
        }

        currentFakeBoxes.Clear();

        if (currentFakeBox != null)
        {
            Destroy(currentFakeBox.gameObject);
            currentFakeBox = null;
        }
    }

    private void TrimFakeBoxesBeforeDeploy()
    {
        int maxCount = Mathf.Max(1, currentMaxActiveFakeBoxCount);

        while (currentFakeBoxes.Count >= maxCount)
        {
            FakeBox oldestFakeBox = currentFakeBoxes[0];
            currentFakeBoxes.RemoveAt(0);

            if (oldestFakeBox != null)
                Destroy(oldestFakeBox.gameObject);
        }
    }

    private void AutoCacheMisdirectionMaterialReferences()
    {
        if (!autoFindMisdirectionMaterialRoot)
            return;

        if (misdirectionMaterialRoot != null)
            return;

        Transform foundRoot = FindChildRecursive(transform, misdirectionMaterialRootName);

        if (foundRoot != null)
            misdirectionMaterialRoot = foundRoot;
    }

    private void CacheOriginalMisdirectionMaterials()
    {
        AutoCacheMisdirectionMaterialReferences();

        Transform root = misdirectionMaterialRoot != null ? misdirectionMaterialRoot : transform;

        misdirectionRenderers = applyMisdirectionMaterialToChildren
            ? root.GetComponentsInChildren<Renderer>(true)
            : root.GetComponents<Renderer>();

        if (misdirectionRenderers == null || misdirectionRenderers.Length == 0)
        {
            hasCachedMisdirectionMaterials = false;
            originalMisdirectionMaterials = null;
            return;
        }

        originalMisdirectionMaterials = new Material[misdirectionRenderers.Length][];

        for (int i = 0; i < misdirectionRenderers.Length; i++)
        {
            Renderer targetRenderer = misdirectionRenderers[i];

            if (targetRenderer == null)
                continue;

            originalMisdirectionMaterials[i] = targetRenderer.sharedMaterials;
        }

        hasCachedMisdirectionMaterials = true;
    }

    private void ApplyMisdirectionMaterial()
    {
        if (misdirectionMaterial == null)
            return;

        if (!hasCachedMisdirectionMaterials || misdirectionRenderers == null)
            CacheOriginalMisdirectionMaterials();

        if (misdirectionRenderers == null || misdirectionRenderers.Length == 0)
            return;

        for (int i = 0; i < misdirectionRenderers.Length; i++)
        {
            Renderer targetRenderer = misdirectionRenderers[i];

            if (targetRenderer == null)
                continue;

            Material[] currentMaterials = targetRenderer.sharedMaterials;

            if (currentMaterials == null || currentMaterials.Length == 0)
                continue;

            Material[] newMaterials = new Material[currentMaterials.Length];

            if (replaceAllMisdirectionMaterials)
            {
                for (int j = 0; j < newMaterials.Length; j++)
                    newMaterials[j] = misdirectionMaterial;
            }
            else
            {
                for (int j = 0; j < newMaterials.Length; j++)
                    newMaterials[j] = currentMaterials[j];

                newMaterials[0] = misdirectionMaterial;
            }

            targetRenderer.sharedMaterials = newMaterials;
        }

        isMisdirectionMaterialApplied = true;
    }

    private void RestoreMisdirectionMaterial()
    {
        if (!isMisdirectionMaterialApplied)
            return;

        if (!hasCachedMisdirectionMaterials ||
            misdirectionRenderers == null ||
            originalMisdirectionMaterials == null)
        {
            isMisdirectionMaterialApplied = false;
            return;
        }

        int count = Mathf.Min(misdirectionRenderers.Length, originalMisdirectionMaterials.Length);

        for (int i = 0; i < count; i++)
        {
            Renderer targetRenderer = misdirectionRenderers[i];

            if (targetRenderer == null)
                continue;

            Material[] originalMaterials = originalMisdirectionMaterials[i];

            if (originalMaterials == null)
                continue;

            targetRenderer.sharedMaterials = originalMaterials;
        }

        isMisdirectionMaterialApplied = false;
    }

    private void MaintainJokerCardDebuffImmunity()
    {
        if (!IsJokerCardDebuffImmunityActive)
            return;

        ClearSkillCommandBlockers(false);
        UpdateStateIcon();
    }

    private void TryAutoUseJokerCard()
    {
        if (isJokerCardActive)
            return;

        if (isResultAnimationLocked || isHitReactionLocked || isSkillAnimationLocked)
            return;

        if (!CanUseSkillGaugeForSkill(SkillJokerCard, false))
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillJokerCard))
            return;

        jokerCardRoutine = StartCoroutine(JokerCardRoutine());
    }

    private IEnumerator JokerCardRoutine()
    {
        isJokerCardActive = true;

        if (currentJokerCardDebuffImmunity)
        {
            ClearSkillCommandBlockers(false);
            UpdateStateIcon();
        }

        CacheJokerCardOriginalValues();
        ApplyJokerCardBuff();
        PlayJokerCardEffect();

        RequestFollowUserSkillCamera();

        float duration = stats != null ? stats.jokerCardDuration : 6f;
        duration *= currentJokerCardDurationMultiplier;

        Debug.Log(
            $"[Trickster {AgentID}] Joker Card activated. " +
            $"Duration={duration:0.##}, " +
            $"DurationMultiplier={currentJokerCardDurationMultiplier:F2}, " +
            $"BuffEffectMultiplier={currentJokerCardBuffEffectMultiplier:F2}, " +
            $"DebuffImmunity={currentJokerCardDebuffImmunity}"
        );

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        StopJokerCard(false);
    }

    private void CacheJokerCardOriginalValues()
    {
        hasCachedJokerCardValues = true;

        if (navAgent != null)
            originalMoveSpeed = navAgent.speed;

        if (spotLight != null)
        {
            originalSpotLightRange = spotLight.range;
            originalSpotLightOuterAngle = spotLight.spotAngle;
        }

        viewRadiusSnapshot = FloatMemberSnapshot.Create(visionSensor, "viewRadius");
        viewAngleSnapshot = FloatMemberSnapshot.Create(visionSensor, "viewAngle");
    }

    private void ApplyJokerCardBuff()
    {
        if (!hasCachedJokerCardValues)
            return;

        float moveSpeedMultiplier = stats != null ? stats.jokerCardMoveSpeedMultiplier : 1.25f;
        float viewRadiusMultiplier = stats != null ? stats.jokerCardViewRadiusMultiplier : 1.2f;
        float viewAngleBonus = stats != null ? stats.jokerCardViewAngleBonus : 15f;

        moveSpeedMultiplier = ApplyJokerBuffEffectMultiplier(moveSpeedMultiplier);
        viewRadiusMultiplier = ApplyJokerBuffEffectMultiplier(viewRadiusMultiplier);
        viewAngleBonus *= currentJokerCardBuffEffectMultiplier;

        moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
        viewRadiusMultiplier = Mathf.Max(0f, viewRadiusMultiplier);

        if (navAgent != null)
            navAgent.speed = originalMoveSpeed * moveSpeedMultiplier;

        if (viewRadiusSnapshot != null && viewRadiusSnapshot.IsValid)
            viewRadiusSnapshot.Set(viewRadiusSnapshot.OriginalValue * viewRadiusMultiplier);

        if (viewAngleSnapshot != null && viewAngleSnapshot.IsValid)
            viewAngleSnapshot.Set(Mathf.Clamp(viewAngleSnapshot.OriginalValue + viewAngleBonus, 1f, 360f));

        if (spotLight != null)
        {
            spotLight.range = originalSpotLightRange * viewRadiusMultiplier;
            spotLight.spotAngle = Mathf.Clamp(originalSpotLightOuterAngle + viewAngleBonus, 1f, 179f);
        }
    }

    private float ApplyJokerBuffEffectMultiplier(float baseMultiplier)
    {
        if (currentJokerCardBuffEffectMultiplier <= 1f)
            return baseMultiplier;

        float bonus = baseMultiplier - 1f;
        return 1f + bonus * currentJokerCardBuffEffectMultiplier;
    }

    private void StopJokerCard(bool immediate)
    {
        if (jokerCardRoutine != null)
        {
            StopCoroutine(jokerCardRoutine);
            jokerCardRoutine = null;
        }

        bool wasActive = isJokerCardActive;

        if (isJokerCardActive)
        {
            RestoreJokerCardOriginalValues();

            isJokerCardActive = false;
            hasCachedJokerCardValues = false;
        }

        if (immediate)
            CleanupJokerCardEffect();

        if (!immediate && wasActive)
            Debug.Log($"[Trickster {AgentID}] Joker Card finished.");
    }

    private void CleanupJokerCardEffect()
    {
        if (currentJokerCardEffect == null)
            return;

        currentJokerCardEffect.StopImmediate();
    }

    private void RestoreJokerCardOriginalValues()
    {
        if (!hasCachedJokerCardValues)
            return;

        if (navAgent != null)
            navAgent.speed = originalMoveSpeed;

        if (viewRadiusSnapshot != null && viewRadiusSnapshot.IsValid)
            viewRadiusSnapshot.Restore();

        if (viewAngleSnapshot != null && viewAngleSnapshot.IsValid)
            viewAngleSnapshot.Restore();

        if (spotLight != null)
        {
            spotLight.range = originalSpotLightRange;
            spotLight.spotAngle = originalSpotLightOuterAngle;
        }
    }

    private void PlayJokerCardEffect()
    {
        JokerCard effect = GetOrCreateJokerCardEffect();

        if (effect == null)
        {
            Debug.LogWarning($"[Trickster {AgentID}] Joker Card effect is not assigned.");
            return;
        }

        currentJokerCardEffect = effect;
        currentJokerCardEffect.gameObject.SetActive(true);
        currentJokerCardEffect.Play();
    }

    private JokerCard GetOrCreateJokerCardEffect()
    {
        AutoCacheJokerCardEffectReferences();

        if (jokerCardEffectInstance != null)
            return jokerCardEffectInstance;

        if (currentJokerCardEffect != null)
            return currentJokerCardEffect;

        if (jokerCardEffectPrefab == null)
            return null;

        Transform parent = jokerCardEffectParent != null ? jokerCardEffectParent : transform;

        currentJokerCardEffect = Instantiate(jokerCardEffectPrefab, parent);
        currentJokerCardEffect.transform.localPosition = Vector3.zero;
        currentJokerCardEffect.transform.localRotation = Quaternion.identity;
        currentJokerCardEffect.transform.localScale = Vector3.one;

        return currentJokerCardEffect;
    }

    private void AutoCacheJokerCardEffectReferences()
    {
        if (!autoFindJokerCardEffect)
            return;

        if (jokerCardEffectParent == null)
        {
            Transform effectAnchor = FindChildRecursive(transform, JokerCardEffectParentObjectName);

            if (effectAnchor != null)
                jokerCardEffectParent = effectAnchor;
        }

        if (jokerCardEffectInstance == null)
        {
            jokerCardEffectInstance = FindChildComponentByObjectName<JokerCard>(
                transform,
                JokerCardEffectObjectName
            );
        }

        if (jokerCardEffectInstance == null)
            jokerCardEffectInstance = GetComponentInChildren<JokerCard>(true);

        if (jokerCardEffectInstance == null)
            return;

        currentJokerCardEffect = jokerCardEffectInstance;

        if (jokerCardEffectParent == null)
            jokerCardEffectParent = jokerCardEffectInstance.transform.parent;
    }

    private T FindChildComponentByObjectName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        T[] components = root.GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];

            if (component == null)
                continue;

            if (string.Equals(
                component.gameObject.name,
                objectName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return component;
            }
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (string.Equals(
                child.name,
                childName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
                return found;
        }

        return null;
    }

    private void ForceStopForSkill()
    {
        currentTarget = null;
        isManualMoving = false;

        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void CompleteManualMove(string logMessage)
    {
        isManualMoving = false;
        cachedTricksterAnimationIsMoving = false;
        lastTricksterAnimationMovingTime = -999f;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        UpdateAnimationState(true);
        UpdateStateIcon();

        Debug.Log($"[Trickster {AgentID}] {logMessage}");
    }

    private Vector3 BuildSpawnPosition(Vector3 targetPos)
    {
        Vector3 rayOrigin = new Vector3(targetPos.x, targetPos.y + 2f, targetPos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return new Vector3(hit.point.x, hit.point.y + deployYOffset, hit.point.z);

        return new Vector3(targetPos.x, deployYOffset, targetPos.z);
    }

    private bool ResolveTricksterAnimationIsMoving()
    {
        if (navAgent == null)
            return false;

        if (isResultAnimationLocked || isHitReactionLocked || isSkillAnimationLocked)
        {
            cachedTricksterAnimationIsMoving = false;
            return false;
        }

        if (navAgent.isStopped)
        {
            cachedTricksterAnimationIsMoving = false;
            return false;
        }

        float effectiveMovingThreshold = Mathf.Max(tricksterMovingThreshold, StableMovingThreshold);
        float movingThresholdSqr = effectiveMovingThreshold * effectiveMovingThreshold;

        bool reachedDestination = HasReachedDestinationForTricksterAnimation();
        bool hasActualVelocity = navAgent.velocity.sqrMagnitude > movingThresholdSqr;

        if (reachedDestination && !hasActualVelocity)
        {
            cachedTricksterAnimationIsMoving = false;
            return false;
        }

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            currentTarget != null ||
            IsFollowingSharedTargetPosition ||
            HasActivePathForTricksterAnimation();

        bool hasNotReachedDestination = !reachedDestination;
        bool shouldMove = hasMovementIntent && (hasActualVelocity || hasNotReachedDestination);

        if (shouldMove)
        {
            cachedTricksterAnimationIsMoving = true;
            lastTricksterAnimationMovingTime = Time.time;
            return true;
        }

        if (cachedTricksterAnimationIsMoving &&
            Time.time - lastTricksterAnimationMovingTime <= animationStopDelay)
        {
            return true;
        }

        cachedTricksterAnimationIsMoving = false;
        return false;
    }

    private TricksterMoveMode ResolveTricksterMoveMode(bool isMoving)
    {
        if (!isMoving)
            return TricksterMoveMode.IdleAlert;

        if (!IsJokerCardDebuffImmunityActive && IsSmokeDebuffed)
            return TricksterMoveMode.DebuffedRun;

        return TricksterMoveMode.CommandRun;
    }

    private bool HasActivePathForTricksterAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForTricksterAnimation();
    }

    private bool HasReachedDestinationForTricksterAnimation()
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
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        if (faceAwayFromHitSource)
            FaceAwayFromHitSource(hitSourcePosition);

        UpdateAnimationState(true);
        PlayAnimatorTrigger(hitReactionHash, hasHitReactionTrigger, "HitReaction");

        if (hitReactionLockSeconds > 0f)
            yield return new WaitForSeconds(hitReactionLockSeconds);

        if (navAgent != null &&
            navAgent.isActiveAndEnabled &&
            navAgent.isOnNavMesh &&
            !isResultAnimationLocked)
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

    private void PlayFakeBoxAnimation()
    {
        PlayAnimatorTrigger(fakeBoxHash, hasFakeBoxTrigger, "FakeBox");
    }

    private void PlayVanishingStartAnimation()
    {
        PlayAnimatorTrigger(vanishingStartHash, hasVanishingStartTrigger, VanishingStartTriggerName);
    }

    private void PlayVanishingSuccessAnimation()
    {
        PlayAnimatorTrigger(vanishingSuccessHash, hasVanishingSuccessTrigger, VanishingSuccessTriggerName);
    }

    private void PlayResultAnimation(int triggerHash, bool hasTrigger, string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[Trickster {AgentID}] Animator is missing. Cannot play {triggerName} animation.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Trickster {AgentID}] Animator parameter is missing: {triggerName}");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;
        isSkillAnimationLocked = false;

        StopFakeBoxRoutine();
        StopHitReactionRoutine();
        StopVanishingRoutine();
        StopMisdirectionRoutine();
        StopVanishingSpotlightReveal();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStoppedForLockedAnimation();
        UpdateAnimationState(true);

        ResetAnimatorTriggers();
        animator.SetTrigger(triggerHash);

        Debug.Log($"[Trickster {AgentID}] {triggerName} animation played.");
    }

    private void KeepStoppedForLockedAnimation()
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

    private void PlayAnimatorTrigger(int triggerHash, bool hasTrigger, string triggerName)
    {
        if (animator == null)
            return;

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Trickster {AgentID}] Animator parameter is missing: {triggerName}");
            return;
        }

        ResetAnimatorTriggers();
        animator.SetTrigger(triggerHash);
    }

    private void ResetAnimatorTriggers()
    {
        if (animator == null)
            return;

        if (hasFakeBoxTrigger)
            animator.ResetTrigger(fakeBoxHash);

        if (hasVanishingStartTrigger)
            animator.ResetTrigger(vanishingStartHash);

        if (hasVanishingSuccessTrigger)
            animator.ResetTrigger(vanishingSuccessHash);

        if (hasHitReactionTrigger)
            animator.ResetTrigger(hitReactionHash);

        if (hasVictoryTrigger)
            animator.ResetTrigger(victoryHash);

        if (hasDefeatTrigger)
            animator.ResetTrigger(defeatHash);
    }

    private void StopFakeBoxRoutine()
    {
        if (fakeBoxRoutine == null)
            return;

        StopCoroutine(fakeBoxRoutine);
        fakeBoxRoutine = null;
    }

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
    }

    private void StopVanishingRoutine()
    {
        if (vanishingRoutine != null)
        {
            StopCoroutine(vanishingRoutine);
            vanishingRoutine = null;
        }

        isVanishing = false;
        isVanishingRecoveryLocked = false;
    }

    private void StopMisdirectionRoutine()
    {
        if (misdirectionRoutine != null)
        {
            StopCoroutine(misdirectionRoutine);
            misdirectionRoutine = null;
        }

        EndMisdirectionState();
    }

    private void CacheTricksterAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        fakeBoxHash = Animator.StringToHash(FakeBoxTriggerName);
        vanishingStartHash = Animator.StringToHash(VanishingStartTriggerName);
        vanishingSuccessHash = Animator.StringToHash(VanishingSuccessTriggerName);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
    }

    private void CacheTricksterAnimatorParameters()
    {
        hasIsMovingParameter = HasAnimatorParameter(IsMovingParameter, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(MoveSpeedParameter, AnimatorControllerParameterType.Float);
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasFakeBoxTrigger = HasAnimatorParameter(FakeBoxTriggerName, AnimatorControllerParameterType.Trigger);
        hasVanishingStartTrigger = HasAnimatorParameter(VanishingStartTriggerName, AnimatorControllerParameterType.Trigger);
        hasVanishingSuccessTrigger = HasAnimatorParameter(VanishingSuccessTriggerName, AnimatorControllerParameterType.Trigger);
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

        Debug.LogWarning($"[Trickster {AgentID}] Animator parameter is missing: {parameterName} ({parameterType})");
        return false;
    }

    private bool IsFakeBoxSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("fakebox") ||
               skill.Contains("fake box") ||
               skill.Contains("magicbox") ||
               skill.Contains("magic box") ||
               skill.Contains("페이크박스") ||
               skill.Contains("페이크 박스") ||
               skill.Contains("마술상자") ||
               skill.Contains("마술 상자") ||
               skill.Contains("가짜상자") ||
               skill.Contains("가짜 상자");
    }

    private bool IsJokerCardSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("jokercard") ||
               skill.Contains("joker card") ||
               skill.Contains("조커카드") ||
               skill.Contains("조커 카드");
    }

    private bool IsVanishingSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("vanishing") ||
               skill.Contains("vanish") ||
               skill.Contains("배니싱");
    }

    private bool IsMisdirectionSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("misdirection") ||
               skill.Contains("mis direction") ||
               skill.Contains("미스디렉션") ||
               skill.Contains("미스 디렉션");
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

    private sealed class FloatMemberSnapshot
    {
        private readonly object target;
        private readonly FieldInfo field;
        private readonly PropertyInfo property;

        public bool IsValid { get; private set; }
        public float OriginalValue { get; private set; }

        private FloatMemberSnapshot(object target, FieldInfo field, PropertyInfo property, float originalValue)
        {
            this.target = target;
            this.field = field;
            this.property = property;
            OriginalValue = originalValue;
            IsValid = target != null && (field != null || property != null);
        }

        public static FloatMemberSnapshot Create(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return new FloatMemberSnapshot(null, null, null, 0f);

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            System.Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, flags);

            if (field != null && field.FieldType == typeof(float))
            {
                float value = (float)field.GetValue(target);
                return new FloatMemberSnapshot(target, field, null, value);
            }

            PropertyInfo property = type.GetProperty(memberName, flags);

            if (property != null &&
                property.PropertyType == typeof(float) &&
                property.CanRead &&
                property.CanWrite)
            {
                float value = (float)property.GetValue(target);
                return new FloatMemberSnapshot(target, null, property, value);
            }

            return new FloatMemberSnapshot(null, null, null, 0f);
        }

        public void Set(float value)
        {
            if (!IsValid)
                return;

            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            if (property != null)
                property.SetValue(target, value);
        }

        public void Restore()
        {
            Set(OriginalValue);
        }
    }
}