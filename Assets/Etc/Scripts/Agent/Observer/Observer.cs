using System.Collections;
using UnityEngine;

public class Observer : AgentController
{
    private enum ObserverMoveMode
    {
        IdleLookAround = 0,
        Run = 1,
        Reserved = 2,
        DebuffedRun = 3
    }

    private const string SkillDrone = "drone";
    private const string SkillPositionShare = "positionshare";

    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";
    private const string DeployDroneTriggerName = "DeployDrone";

    private const float AnimationMovingThreshold = 0.05f;
    private const float DestinationBuffer = 0.2f;

    [Header("Drone")]
    [SerializeField] private Drone dronePrefab;
    [SerializeField] private Transform droneParent;
    [SerializeField] private float droneRadius = 15f;
    [SerializeField] private float droneDuration = 20f;
    [SerializeField] private float droneSpawnHeight = 6f;
    [SerializeField] private bool replaceExistingDrone = true;
    [SerializeField] private bool stopWhenDeployDrone = true;

    [Header("Position Share")]
    [SerializeField] private bool targetPositionShareEnabled = true;
    [SerializeField] private bool includeSelfInTargetPositionShare = false;

    [Header("Observer Animation")]
    [SerializeField] private float droneDeployLockSeconds = 0.8f;
    [SerializeField] private float droneSpawnDelay = 0.35f;
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    private Drone currentDrone;
    private AgentController[] cachedAgents;

    private bool isTargetPositionSharing;
    private float lastTargetPositionShareTime = -999f;

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

    public bool IsTargetPositionShareEnabled => targetPositionShareEnabled;
    public bool IsTargetPositionSharing => isTargetPositionSharing;
    public Drone CurrentDrone => currentDrone;
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
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        droneRadius = Mathf.Max(0f, droneRadius);
        droneDuration = Mathf.Max(0f, droneDuration);
        droneSpawnHeight = Mathf.Max(0f, droneSpawnHeight);

        droneDeployLockSeconds = Mathf.Max(0f, droneDeployLockSeconds);
        droneSpawnDelay = Mathf.Max(0f, droneSpawnDelay);
        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        CacheObserverAnimationHashes();
    }

    protected override void OnDisable()
    {
        DestroyCurrentDrone();
        ClearSharedTargetPositionFromThisObserver();

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
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked || isHitReactionLocked || isDroneDeployLocked)
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Observer {AgentID}] Skill request: {skillName}, Position: {targetPos}");

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
        }

        Debug.Log($"[Observer {AgentID}] Position share {(targetPositionShareEnabled ? "on" : "off")}");
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

    private void SpawnDrone(Vector3 targetPos)
    {
        if (replaceExistingDrone)
            DestroyCurrentDrone();

        Vector3 observationCenter = targetPos;
        Vector3 droneVisualPosition = GetDroneVisualPosition(observationCenter);

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
            droneDuration
        );

        currentDrone = drone;

        Debug.Log(
            $"[Observer {AgentID}] Drone deployed. " +
            $"Observation Center: {observationCenter}, " +
            $"Drone Position: {droneVisualPosition}, " +
            $"Radius: {droneRadius}, Duration: {droneDuration}"
        );
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

    private void DestroyCurrentDrone()
    {
        if (currentDrone == null)
            return;

        Destroy(currentDrone.gameObject);
        currentDrone = null;
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

    private void ClearSharedTargetPositionFromThisObserver()
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

    private bool IsDroneSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillDrone) ||
               skill.Contains("드론");
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

        return skill.Contains("_off") ||
               skill.Contains("off") ||
               skill.Contains("끄기") ||
               skill.Contains("끔") ||
               skill.Contains("중지") ||
               skill.Contains("비활성") ||
               skill.Contains("꺼");
    }
}