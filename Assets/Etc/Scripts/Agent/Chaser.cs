using System.Collections;
using UnityEngine;

public class Chaser : AgentController
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

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";

    private const float EscapeBlockGaugeEmptyEpsilon = 0.001f;

    [Header("Access Control")]
    [SerializeField] private AccessControlZone accessControlZonePrefab;
    [SerializeField] private float accessControlRadius = 10f;
    [SerializeField] private float accessControlDuration = 20f;
    [SerializeField] private float targetSpeedMultiplierInAccessControl = 0.8f;
    [SerializeField] private float targetAngularSpeedMultiplierInAccessControl = 0.7f;
    [SerializeField] private float chaserSpeedMultiplierInAccessControl = 1.2f;
    [SerializeField] private float chaserAngularSpeedMultiplierInAccessControl = 1.3f;
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

    [Header("Chaser Animation")]
    [SerializeField] private float chaserMovingThreshold = 0.03f;
    [SerializeField] private float chaserAnimationStopDelay = 0.2f;
    [SerializeField] private float chaserDestinationBuffer = 0.2f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    private AccessControlZone currentAccessControlZone;

    private Transform escapeBlockCandidateTarget;
    private Transform escapeBlockBlockedTarget;
    private ITargetEscapeSkillBlockReceiver escapeBlockBlockedReceiver;

    private float escapeBlockGauge;
    private float escapeBlockSightTimer;
    private float escapeBlockReleaseTimer;

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

    protected override void Awake()
    {
        agentID = 0;

        CacheChaserAnimationHashes();

        base.Awake();

        CacheChaserAnimatorParameters();
        InitializeEscapeBlockGauge();
        UpdateAnimationState(true);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        accessControlRadius = Mathf.Max(0f, accessControlRadius);
        accessControlDuration = Mathf.Max(0f, accessControlDuration);

        escapeBlockGaugeMax = Mathf.Max(0f, escapeBlockGaugeMax);
        escapeBlockGaugeDrainPerSecond = Mathf.Max(0f, escapeBlockGaugeDrainPerSecond);
        escapeBlockMaxDistance = Mathf.Max(0f, escapeBlockMaxDistance);
        escapeBlockRequiredSightTime = Mathf.Max(0f, escapeBlockRequiredSightTime);
        escapeBlockReleaseDelay = Mathf.Max(0f, escapeBlockReleaseDelay);

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

        base.Update();
        UpdateEscapeBlock();
    }

    protected override void OnDisable()
    {
        ReleaseEscapeBlock();
        DestroyCurrentAccessControlZone();
        StopHitReactionRoutine();

        isHitReactionLocked = false;
        isResultAnimationLocked = false;
        cachedChaserAnimationIsMoving = false;
        lastChaserAnimationMovingTime = -999f;

        base.OnDisable();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked)
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Chaser {AgentID}] 스킬 요청: {skillName}, 위치: {targetPos}");

        if (IsAccessControlSkill(skill))
        {
            ExecuteAccessControl(targetPos);
            return;
        }

        if (IsEscapeBlockSkill(skill))
        {
            Debug.Log($"[Chaser {AgentID}] 도주 제지는 자동 스킬입니다. 타겟을 시야에 담으면 게이지를 소모하며 자동으로 적용됩니다.");
            return;
        }

        Debug.LogWarning($"[Chaser {AgentID}] 알 수 없는 스킬입니다: {skillName}");
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

        if (!isMoving || actualSpeed <= chaserMovingThreshold)
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

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeCurrentForSkill(string skillName)
    {
        if (IsEscapeBlockSkill(skillName))
            return Mathf.Clamp(escapeBlockGauge, 0f, escapeBlockGaugeMax);

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

    public void PlayHitReaction(Vector3 hitSourcePosition)
    {
        if (isResultAnimationLocked)
            return;

        if (hitReactionRoutine != null)
            StopCoroutine(hitReactionRoutine);

        hitReactionRoutine = StartCoroutine(HitReactionRoutine(hitSourcePosition));
    }

    public void PlayVictoryPose()
    {
        PlayResultAnimation(victoryHash, hasVictoryTrigger, "Victory");
    }

    public void PlayDefeatPose()
    {
        PlayResultAnimation(defeatHash, hasDefeatTrigger, "Defeat");
    }

    public void ClearResultAnimationLock()
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

        Debug.Log(
            $"[Chaser {AgentID}] 출입 통제 구역 생성. " +
            $"Center={centerPosition}, Radius={accessControlRadius}, Duration={accessControlDuration}"
        );
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

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            currentTarget != null ||
            IsFollowingSharedTargetPosition ||
            HasActivePathForChaserAnimation();

        bool hasActualVelocity =
            navAgent.velocity.sqrMagnitude > chaserMovingThreshold * chaserMovingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForChaserAnimation();

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
}