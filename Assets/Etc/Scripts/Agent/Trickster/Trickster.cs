using System.Collections;
using System.Reflection;
using UnityEngine;

public class Trickster : AgentController
{
    private enum TricksterMoveMode
    {
        IdleAlert = 0,
        CommandRun = 1,
        DebuffedRun = 2
    }

    private const string SkillFakeBox = "fakebox";
    private const string SkillJokerCard = "jokercard";

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string FakeBoxTriggerName = "FakeBox";
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

    private FakeBox currentFakeBox;
    private Coroutine jokerCardRoutine;
    private JokerCard currentJokerCardEffect;

    private bool isJokerCardActive;
    private bool hasCachedJokerCardValues;

    private float originalMoveSpeed;
    private float originalSpotLightRange;
    private float originalSpotLightOuterAngle;

    private FloatMemberSnapshot viewRadiusSnapshot;
    private FloatMemberSnapshot viewAngleSnapshot;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int fakeBoxHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasFakeBoxTrigger;
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

    protected override void Awake()
    {
        agentID = 3;

        CacheTricksterAnimationHashes();

        base.Awake();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheTricksterAnimatorParameters();
        AutoCacheJokerCardEffectReferences();

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
            return;
        }

        if (isHitReactionLocked || isSkillAnimationLocked)
        {
            UpdateAnimationState();
            return;
        }

        base.Update();
        TryAutoUseJokerCard();
    }

    protected override void OnDisable()
    {
        StopJokerCard(true);
        StopFakeBoxRoutine();
        StopHitReactionRoutine();

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

        tricksterMovingThreshold = Mathf.Max(0f, tricksterMovingThreshold);
        animationStopDelay = Mathf.Max(0f, animationStopDelay);
        destinationBuffer = Mathf.Max(0f, destinationBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);
        fakeBoxAnimationLockSeconds = Mathf.Max(0f, fakeBoxAnimationLockSeconds);
        fakeBoxDeployDelay = Mathf.Max(0f, fakeBoxDeployDelay);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        CacheTricksterAnimationHashes();
        AutoCacheJokerCardEffectReferences();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked || isHitReactionLocked || isSkillAnimationLocked)
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

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
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

        if (replaceExistingFakeBox && currentFakeBox != null)
        {
            Destroy(currentFakeBox.gameObject);
            currentFakeBox = null;
        }

        currentFakeBox = Instantiate(
            fakeBoxPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        currentFakeBox.SetOwner(this);

        RequestInstalledObjectCamera(currentFakeBox.transform);

        Debug.Log($"[Trickster {AgentID}] Fake Box deployed: {spawnPos}");
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

        CacheJokerCardOriginalValues();
        ApplyJokerCardBuff();
        PlayJokerCardEffect();

        RequestFollowUserSkillCamera();

        float duration = stats != null ? stats.jokerCardDuration : 6f;

        Debug.Log($"[Trickster {AgentID}] Joker Card activated. Duration: {duration:0.##}");

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

        if (IsSmokeDebuffed)
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

    private void CacheTricksterAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        fakeBoxHash = Animator.StringToHash(FakeBoxTriggerName);
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