using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum DetectionDogState
{
    FollowHandler,
    MoveToPoint,
    GuardArea,
    Howling,
    ReturnToHandler,
    OffLeashSearch
}

[RequireComponent(typeof(NavMeshAgent))]
public class DetectionDogController : MonoBehaviour
{
    private const float DefaultNavMeshSampleRadius = 2f;
    private const float MinMoveSpeedMultiplier = 0.01f;

    [Header("References")]
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private VisionSensor visionSensor;
    [SerializeField] private Animator animator;

    [Header("Dog Movement")]
    [SerializeField] private float baseMoveSpeed = 4.5f;
    [SerializeField] private float followStartDistance = 2.5f;
    [SerializeField] private float followStopDistance = 1.25f;
    [SerializeField] private float followRepathInterval = 0.15f;
    [SerializeField] private float arrivalDistance = 0.35f;
    [SerializeField] private float navMeshSampleRadius = 2f;

    [Header("Dog Vision")]
    [SerializeField] private float dogViewRadius = 7.5f;
    [SerializeField, Range(1f, 360f)] private float dogViewAngle = 90f;
    [SerializeField] private bool disableAutoChaseOnDogVision = true;

    [Header("Guard / Off Leash")]
    [SerializeField] private bool rotateWhileGuarding = false;
    [SerializeField] private float guardScanTurnSpeed = 120f;
    [SerializeField] private float offLeashWaypointReachDistance = 0.7f;
    [SerializeField] private float offLeashRepathInterval = 0.2f;
    [SerializeField] private int offLeashPointSearchTries = 24;
    [SerializeField] private bool useWholeNavMeshForOffLeash = true;
    [SerializeField] private float fallbackOffLeashSearchRadius = 25f;

    [Header("Howling")]
    [SerializeField] private float howlingDuration = 1.25f;
    [SerializeField] private bool stopWhileHowling = true;

    [Header("Animation")]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string howlTriggerParameter = "Howl";
    [SerializeField] private string eatTriggerParameter = "Eat";
    [SerializeField] private float animationMovingThreshold = 0.05f;
    [SerializeField] private bool disableAnimatorRootMotion = true;
    [SerializeField] private float moveSpeedDampTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private DogHandler handler;
    private Transform followTarget;
    private LayerMask targetLayer;

    private DetectionDogState currentState = DetectionDogState.FollowHandler;

    private float nextFollowRepathTime;
    private float nextOffLeashRepathTime;
    private float offLeashEndTime = -1f;

    private bool hasDeployDestination;
    private Vector3 deployDestination;

    private bool hasOffLeashDestination;
    private Vector3 offLeashDestination;

    private float guardInstinctMoveSpeedMultiplier = 1f;

    private bool isTreatActive;
    private float treatEndTime = -1f;
    private float activeTreatMoveSpeedMultiplier = 1f;
    private float activeTreatViewRadiusMultiplier = 1f;
    private float activeTreatViewAngleOffset = 0f;

    private Coroutine howlingRoutine;
    private bool movementSuspended;

    private int isMovingHash;
    private int moveSpeedHash;
    private int howlHash;
    private int eatHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasHowlTriggerParameter;
    private bool hasEatTriggerParameter;

    public bool IsOffLeash => currentState == DetectionDogState.OffLeashSearch;
    public DetectionDogState CurrentState => currentState;
    public bool IsTreatActive => isTreatActive;
    public float TreatRemainingTime => isTreatActive ? Mathf.Max(0f, treatEndTime - Time.time) : 0f;

    private void Awake()
    {
        CacheReferences();
        CacheAnimatorParameters();

        if (animator != null && disableAnimatorRootMotion)
            animator.applyRootMotion = false;

        if (navAgent != null)
        {
            if (baseMoveSpeed <= 0f)
                baseMoveSpeed = Mathf.Max(0.01f, navAgent.speed);

            navAgent.speed = baseMoveSpeed;
            navAgent.stoppingDistance = Mathf.Max(0f, arrivalDistance);
        }

        ApplyMovementSpeed();
    }

    private void OnEnable()
    {
        SubscribeVisionEvent();
    }

    private void OnDisable()
    {
        UnsubscribeVisionEvent();
        StopHowlingRoutine();
        RemoveTreatModifiers();
    }

    private void OnDestroy()
    {
        UnsubscribeVisionEvent();
    }

    private void OnValidate()
    {
        baseMoveSpeed = Mathf.Max(0.01f, baseMoveSpeed);

        followStartDistance = Mathf.Max(0.1f, followStartDistance);
        followStopDistance = Mathf.Clamp(followStopDistance, 0.05f, followStartDistance);
        followRepathInterval = Mathf.Max(0.01f, followRepathInterval);
        arrivalDistance = Mathf.Max(0.01f, arrivalDistance);
        navMeshSampleRadius = Mathf.Max(0.01f, navMeshSampleRadius);

        dogViewRadius = Mathf.Max(0.1f, dogViewRadius);
        dogViewAngle = Mathf.Clamp(dogViewAngle, 1f, 360f);

        guardScanTurnSpeed = Mathf.Max(0f, guardScanTurnSpeed);
        offLeashWaypointReachDistance = Mathf.Max(0.05f, offLeashWaypointReachDistance);
        offLeashRepathInterval = Mathf.Max(0.01f, offLeashRepathInterval);
        offLeashPointSearchTries = Mathf.Max(1, offLeashPointSearchTries);
        fallbackOffLeashSearchRadius = Mathf.Max(1f, fallbackOffLeashSearchRadius);

        howlingDuration = Mathf.Max(0f, howlingDuration);
        animationMovingThreshold = Mathf.Max(0f, animationMovingThreshold);
        moveSpeedDampTime = Mathf.Max(0f, moveSpeedDampTime);

        guardInstinctMoveSpeedMultiplier = Mathf.Max(MinMoveSpeedMultiplier, guardInstinctMoveSpeedMultiplier);
        activeTreatMoveSpeedMultiplier = Mathf.Max(1f, activeTreatMoveSpeedMultiplier);
        activeTreatViewRadiusMultiplier = Mathf.Max(1f, activeTreatViewRadiusMultiplier);
        activeTreatViewAngleOffset = Mathf.Clamp(activeTreatViewAngleOffset, -359f, 359f);

        CacheReferences();
        CacheAnimatorParameters();
    }

    private void Update()
    {
        UpdateTreatTimer();

        if (movementSuspended)
        {
            KeepStopped();
            UpdateAnimationState();
            return;
        }

        if (!IsNavAgentReady())
        {
            UpdateAnimationState();
            return;
        }

        UpdateTargetDetectionFromVision();

        switch (currentState)
        {
            case DetectionDogState.FollowHandler:
                UpdateFollowHandler(false);
                break;

            case DetectionDogState.MoveToPoint:
                UpdateMoveToPoint();
                break;

            case DetectionDogState.GuardArea:
                UpdateGuardArea();
                break;

            case DetectionDogState.Howling:
                UpdateHowling();
                break;

            case DetectionDogState.ReturnToHandler:
                UpdateReturnToHandler();
                break;

            case DetectionDogState.OffLeashSearch:
                UpdateOffLeashSearch();
                break;
        }

        UpdateAnimationState();
    }

    public void Initialize(DogHandler ownerHandler, Transform ownerFollowTarget, LayerMask ownerTargetLayer)
    {
        handler = ownerHandler;
        followTarget = ownerFollowTarget != null
            ? ownerFollowTarget
            : ownerHandler != null
                ? ownerHandler.transform
                : null;

        targetLayer = ownerTargetLayer;
        movementSuspended = false;

        CacheReferences();

        if (animator != null && disableAnimatorRootMotion)
            animator.applyRootMotion = false;

        CacheAnimatorParameters();
        ConfigureVisionSensor();
        SubscribeVisionEvent();

        ApplyMovementSpeed();

        if (currentState == DetectionDogState.FollowHandler)
            UpdateFollowHandler(true);
    }

    public void ApplyStats(AgentStatsSO stats)
    {
        if (stats == null)
            return;

        baseMoveSpeed = stats.detectionDogMoveSpeed;

        dogViewRadius = stats.detectionDogViewRadius;
        dogViewAngle = stats.detectionDogViewAngle;

        followStartDistance = stats.detectionDogFollowStartDistance;
        followStopDistance = stats.detectionDogFollowStopDistance;
        arrivalDistance = stats.detectionDogArrivalDistance;

        guardScanTurnSpeed = stats.detectionDogGuardScanTurnSpeed;
        howlingDuration = stats.detectionDogHowlingDuration;

        offLeashWaypointReachDistance = stats.offLeashWaypointReachDistance;
        fallbackOffLeashSearchRadius = stats.offLeashFallbackSearchRadius;
        offLeashPointSearchTries = stats.offLeashPointSearchTries;

        ApplyMovementSpeed();

        if (navAgent != null)
            navAgent.stoppingDistance = Mathf.Max(0f, arrivalDistance);

        ConfigureVisionSensor();
    }

    public VisionSensor GetVisionSensor()
    {
        CacheReferences();
        return visionSensor;
    }

    public bool DeployTo(Vector3 targetPosition)
    {
        movementSuspended = false;

        if (!IsNavAgentReady())
        {
            Debug.LogWarning($"[{name}] 탐지견 배치 실패: NavMeshAgent 상태가 올바르지 않습니다.");
            return false;
        }

        if (!TryResolveReachablePoint(targetPosition, out Vector3 resolvedPosition))
        {
            Debug.LogWarning($"[{name}] 탐지견 배치 실패: 이동 가능한 위치를 찾지 못했습니다. Raw={targetPosition}");
            return false;
        }

        StopHowlingRoutine();
        StopOffLeash();

        hasDeployDestination = true;
        deployDestination = resolvedPosition;

        currentState = DetectionDogState.MoveToPoint;
        SetDestination(resolvedPosition);

        if (debugLog)
            Debug.Log($"[{name}] 탐지견 배치 시작: {resolvedPosition}");

        return true;
    }

    public bool StartOffLeash(float duration)
    {
        if (duration <= 0f)
        {
            Debug.LogWarning($"[{name}] 오프리쉬 실패: 지속 시간이 0 이하입니다.");
            return false;
        }

        movementSuspended = false;

        if (!IsNavAgentReady())
        {
            Debug.LogWarning($"[{name}] 오프리쉬 실패: NavMeshAgent 상태가 올바르지 않습니다.");
            return false;
        }

        StopHowlingRoutine();

        currentState = DetectionDogState.OffLeashSearch;
        offLeashEndTime = Time.time + duration;

        hasDeployDestination = false;
        hasOffLeashDestination = false;
        nextOffLeashRepathTime = -999f;

        bool started = TryMoveToNextOffLeashPoint(true);

        if (!started)
        {
            ReturnToHandler();
            return false;
        }

        if (debugLog)
            Debug.Log($"[{name}] 오프리쉬 시작. Duration={duration:0.#}");

        return true;
    }

    public void ApplyTreat(
        float duration,
        float moveSpeedMultiplier,
        float viewRadiusMultiplier,
        float viewAngleOffset
    )
    {
        if (duration <= 0f)
            return;

        activeTreatMoveSpeedMultiplier = Mathf.Max(1f, moveSpeedMultiplier);
        activeTreatViewRadiusMultiplier = Mathf.Max(1f, viewRadiusMultiplier);
        activeTreatViewAngleOffset = Mathf.Clamp(viewAngleOffset, -359f, 359f);

        isTreatActive = true;
        treatEndTime = Time.time + duration;

        ApplyMovementSpeed();
        ApplyTreatVisionModifiers();
        TriggerEatAnimation();

        if (debugLog)
            Debug.Log($"[{name}] 간식 적용. Duration={duration:0.#}");
    }

    public void SetGuardInstinctMoveSpeedMultiplier(float multiplier)
    {
        guardInstinctMoveSpeedMultiplier = Mathf.Max(MinMoveSpeedMultiplier, multiplier);
        ApplyMovementSpeed();
    }

    public void ReturnToHandler()
    {
        movementSuspended = false;

        StopOffLeash();

        hasDeployDestination = false;
        hasOffLeashDestination = false;

        if (followTarget == null)
        {
            currentState = DetectionDogState.FollowHandler;
            StopPath(false);
            return;
        }

        currentState = DetectionDogState.ReturnToHandler;

        if (IsNavAgentReady() && TryResolveReachablePoint(followTarget.position, out Vector3 resolvedPosition))
            SetDestination(resolvedPosition);
    }

    public void StopAllDogActions(bool resetPath)
    {
        StopHowlingRoutine();
        StopOffLeash();
        RemoveTreatModifiers();

        hasDeployDestination = false;
        hasOffLeashDestination = false;
        currentState = DetectionDogState.FollowHandler;

        movementSuspended = resetPath;

        if (resetPath)
            StopPath(true);
    }

    private void UpdateFollowHandler(bool forceRepath)
    {
        if (followTarget == null)
        {
            StopPath(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, followTarget.position);

        if (distance <= followStopDistance)
        {
            StopPath(false);
            return;
        }

        if (!forceRepath && distance < followStartDistance)
            return;

        if (!forceRepath && Time.time < nextFollowRepathTime)
            return;

        nextFollowRepathTime = Time.time + followRepathInterval;

        if (!TryResolveReachablePoint(followTarget.position, out Vector3 resolvedPosition))
            return;

        SetDestination(resolvedPosition);
    }

    private void UpdateMoveToPoint()
    {
        if (!hasDeployDestination)
        {
            EnterGuardArea();
            return;
        }

        if (!HasArrived(arrivalDistance))
            return;

        EnterGuardArea();
    }

    private void EnterGuardArea()
    {
        currentState = DetectionDogState.GuardArea;
        StopPath(false);

        if (debugLog)
            Debug.Log($"[{name}] 탐지견이 배치 지점에 도착하여 감시를 시작합니다.");
    }

    private void UpdateGuardArea()
    {
        StopPath(false);

        if (!rotateWhileGuarding)
            return;

        if (guardScanTurnSpeed <= 0f)
            return;

        transform.Rotate(Vector3.up, guardScanTurnSpeed * Time.deltaTime, Space.World);
    }

    private void UpdateHowling()
    {
        if (stopWhileHowling)
            KeepStopped();
    }

    private void UpdateReturnToHandler()
    {
        if (followTarget == null)
        {
            currentState = DetectionDogState.FollowHandler;
            StopPath(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, followTarget.position);

        if (distance <= followStopDistance)
        {
            currentState = DetectionDogState.FollowHandler;
            StopPath(false);
            return;
        }

        if (Time.time < nextFollowRepathTime)
            return;

        nextFollowRepathTime = Time.time + followRepathInterval;

        if (!TryResolveReachablePoint(followTarget.position, out Vector3 resolvedPosition))
            return;

        SetDestination(resolvedPosition);
    }

    private void UpdateOffLeashSearch()
    {
        if (Time.time >= offLeashEndTime)
        {
            ReturnToHandler();
            return;
        }

        if (HasArrived(offLeashWaypointReachDistance))
        {
            TryMoveToNextOffLeashPoint(false);
            return;
        }

        if (Time.time < nextOffLeashRepathTime)
            return;

        if (hasOffLeashDestination && !navAgent.pathPending && !navAgent.hasPath)
            TryMoveToNextOffLeashPoint(false);
    }

    private bool TryMoveToNextOffLeashPoint(bool force)
    {
        if (!force && Time.time < nextOffLeashRepathTime)
            return false;

        nextOffLeashRepathTime = Time.time + offLeashRepathInterval;

        if (!TryFindOffLeashDestination(out Vector3 destination))
        {
            Debug.LogWarning($"[{name}] 오프리쉬 목적지를 찾지 못했습니다.");
            return false;
        }

        hasOffLeashDestination = true;
        offLeashDestination = destination;
        SetDestination(destination);

        if (debugLog)
            Debug.Log($"[{name}] 오프리쉬 다음 목적지: {destination}");

        return true;
    }

    private bool TryFindOffLeashDestination(out Vector3 destination)
    {
        destination = transform.position;

        if (useWholeNavMeshForOffLeash && TryFindDestinationFromWholeNavMesh(out destination))
            return true;

        return TryFindFallbackRandomDestination(out destination);
    }

    private bool TryFindDestinationFromWholeNavMesh(out Vector3 destination)
    {
        destination = transform.position;

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            return false;

        for (int i = 0; i < offLeashPointSearchTries; i++)
        {
            Vector3 rawPoint = triangulation.vertices[Random.Range(0, triangulation.vertices.Length)];

            Vector2 jitter = Random.insideUnitCircle * navMeshSampleRadius;
            rawPoint.x += jitter.x;
            rawPoint.z += jitter.y;

            if (!TryResolveReachablePoint(rawPoint, out Vector3 resolvedPoint))
                continue;

            destination = resolvedPoint;
            return true;
        }

        return false;
    }

    private bool TryFindFallbackRandomDestination(out Vector3 destination)
    {
        destination = transform.position;

        Vector3 center = followTarget != null ? followTarget.position : transform.position;

        for (int i = 0; i < offLeashPointSearchTries; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * fallbackOffLeashSearchRadius;
            Vector3 rawPoint = new Vector3(
                center.x + randomCircle.x,
                center.y,
                center.z + randomCircle.y
            );

            if (!TryResolveReachablePoint(rawPoint, out Vector3 resolvedPoint))
                continue;

            destination = resolvedPoint;
            return true;
        }

        return false;
    }

    private void UpdateTargetDetectionFromVision()
    {
        if (!CanReportTargetInCurrentState())
            return;

        if (visionSensor == null)
            return;

        if (!visionSensor.IsSeeingTarget)
            return;

        Transform seenTarget = visionSensor.CurrentSeenTarget;

        if (seenTarget == null)
            return;

        StartHowling(seenTarget.position);
    }

    private bool CanReportTargetInCurrentState()
    {
        return currentState == DetectionDogState.MoveToPoint ||
               currentState == DetectionDogState.GuardArea ||
               currentState == DetectionDogState.OffLeashSearch;
    }

    private void HandleVisionChanged(VisionSensor sensor, bool isSeeingTarget, Transform target)
    {
        if (!isSeeingTarget)
            return;

        if (target == null)
            return;

        if (!CanReportTargetInCurrentState())
            return;

        StartHowling(target.position);
    }

    private void StartHowling(Vector3 targetPosition)
    {
        if (currentState == DetectionDogState.Howling)
            return;

        StopOffLeash();

        hasDeployDestination = false;
        hasOffLeashDestination = false;

        currentState = DetectionDogState.Howling;

        if (stopWhileHowling)
            KeepStopped();

        TriggerHowlAnimation();

        StopHowlingRoutine();
        howlingRoutine = StartCoroutine(HowlingRoutine(targetPosition));

        if (debugLog)
            Debug.Log($"[{name}] 타겟 발견. 하울링 시작. 위치={targetPosition}");
    }

    private IEnumerator HowlingRoutine(Vector3 targetPosition)
    {
        if (howlingDuration > 0f)
            yield return new WaitForSeconds(howlingDuration);

        if (handler != null)
            handler.NotifyDogFoundTargetPosition(targetPosition);

        howlingRoutine = null;

        if (currentState == DetectionDogState.Howling)
            ReturnToHandler();
    }

    private void StopHowlingRoutine()
    {
        if (howlingRoutine == null)
            return;

        StopCoroutine(howlingRoutine);
        howlingRoutine = null;
    }

    private void StopOffLeash()
    {
        offLeashEndTime = -1f;
        hasOffLeashDestination = false;
        nextOffLeashRepathTime = 0f;
    }

    private void UpdateTreatTimer()
    {
        if (!isTreatActive)
            return;

        if (Time.time < treatEndTime)
            return;

        RemoveTreatModifiers();
    }

    private void ApplyTreatVisionModifiers()
    {
        if (visionSensor == null)
            return;

        visionSensor.SetExternalViewRadiusMultiplier(this, activeTreatViewRadiusMultiplier);
        visionSensor.SetExternalViewAngleOffset(this, activeTreatViewAngleOffset);
    }

    private void RemoveTreatModifiers()
    {
        if (!isTreatActive)
            return;

        isTreatActive = false;
        treatEndTime = -1f;

        activeTreatMoveSpeedMultiplier = 1f;
        activeTreatViewRadiusMultiplier = 1f;
        activeTreatViewAngleOffset = 0f;

        if (visionSensor != null)
        {
            visionSensor.RemoveExternalViewRadiusMultiplier(this);
            visionSensor.RemoveExternalViewAngleOffset(this);
        }

        ApplyMovementSpeed();

        if (debugLog)
            Debug.Log($"[{name}] 간식 효과 종료");
    }

    private void ApplyMovementSpeed()
    {
        if (navAgent == null)
            return;

        float speedMultiplier = guardInstinctMoveSpeedMultiplier;

        if (isTreatActive)
            speedMultiplier *= activeTreatMoveSpeedMultiplier;

        navAgent.speed = baseMoveSpeed * Mathf.Max(MinMoveSpeedMultiplier, speedMultiplier);
    }

    private bool TryResolveReachablePoint(Vector3 rawPosition, out Vector3 resolvedPosition)
    {
        resolvedPosition = rawPosition;

        if (!IsNavAgentReady())
            return false;

        float sampleRadius = Mathf.Max(DefaultNavMeshSampleRadius, navMeshSampleRadius);

        if (!NavMesh.SamplePosition(rawPosition, out NavMeshHit hit, sampleRadius, navAgent.areaMask))
            return false;

        NavMeshPath path = new NavMeshPath();

        if (!navAgent.CalculatePath(hit.position, path))
            return false;

        if (path.status != NavMeshPathStatus.PathComplete)
            return false;

        resolvedPosition = hit.position;
        return true;
    }

    private void SetDestination(Vector3 destination)
    {
        if (!IsNavAgentReady())
            return;

        navAgent.updateRotation = true;
        navAgent.isStopped = false;
        navAgent.SetDestination(destination);
    }

    private void StopPath(bool resetPath)
    {
        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        if (resetPath)
            navAgent.ResetPath();
    }

    private void KeepStopped()
    {
        StopPath(false);
    }

    private bool HasArrived(float distance)
    {
        if (!IsNavAgentReady())
            return false;

        if (navAgent.pathPending)
            return false;

        if (float.IsInfinity(navAgent.remainingDistance))
            return false;

        float finalDistance = Mathf.Max(distance, navAgent.stoppingDistance);

        if (navAgent.remainingDistance > finalDistance)
            return false;

        if (navAgent.hasPath && navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold)
            return false;

        return true;
    }

    private bool IsNavAgentReady()
    {
        return navAgent != null &&
               navAgent.isActiveAndEnabled &&
               navAgent.isOnNavMesh;
    }

    private void CacheReferences()
    {
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (visionSensor == null)
            visionSensor = GetComponentInChildren<VisionSensor>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void ConfigureVisionSensor()
    {
        if (visionSensor == null)
            return;

        visionSensor.owner = handler;
        visionSensor.targetLayer = targetLayer;
        visionSensor.viewRadius = dogViewRadius;
        visionSensor.viewAngle = dogViewAngle;

        if (disableAutoChaseOnDogVision)
            visionSensor.autoChaseOnSight = false;

        visionSensor.ForceEvaluateVision();
    }

    private void SubscribeVisionEvent()
    {
        if (visionSensor == null)
            return;

        visionSensor.OnVisionChanged -= HandleVisionChanged;
        visionSensor.OnVisionChanged += HandleVisionChanged;
    }

    private void UnsubscribeVisionEvent()
    {
        if (visionSensor == null)
            return;

        visionSensor.OnVisionChanged -= HandleVisionChanged;
    }

    private void CacheAnimatorParameters()
    {
        isMovingHash = Animator.StringToHash(isMovingParameter);
        moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
        howlHash = Animator.StringToHash(howlTriggerParameter);
        eatHash = Animator.StringToHash(eatTriggerParameter);

        hasIsMovingParameter = HasAnimatorParameter(isMovingHash, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(moveSpeedHash, AnimatorControllerParameterType.Float);
        hasHowlTriggerParameter = HasAnimatorParameter(howlHash, AnimatorControllerParameterType.Trigger);
        hasEatTriggerParameter = HasAnimatorParameter(eatHash, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType type)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == parameterHash && parameter.type == type)
                return true;
        }

        return false;
    }

    private void UpdateAnimationState()
    {
        if (animator == null)
            return;

        bool isMoving = ResolveIsMoving();

        if (hasIsMovingParameter)
            animator.SetBool(isMovingHash, isMoving);

        if (hasMoveSpeedParameter)
        {
            float normalizedSpeed = 0f;

            if (navAgent != null && navAgent.speed > 0.001f)
                normalizedSpeed = Mathf.Clamp01(navAgent.velocity.magnitude / navAgent.speed);

            if (moveSpeedDampTime > 0f)
                animator.SetFloat(moveSpeedHash, normalizedSpeed, moveSpeedDampTime, Time.deltaTime);
            else
                animator.SetFloat(moveSpeedHash, normalizedSpeed);
        }
    }

    private bool ResolveIsMoving()
    {
        if (movementSuspended)
            return false;

        if (navAgent == null)
            return false;

        if (!navAgent.isActiveAndEnabled)
            return false;

        if (!navAgent.isOnNavMesh)
            return false;

        if (navAgent.isStopped)
            return false;

        if (currentState == DetectionDogState.Howling)
            return false;

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold;

        return hasVelocity;
    }

    private void TriggerHowlAnimation()
    {
        if (animator == null || !hasHowlTriggerParameter)
            return;

        animator.ResetTrigger(howlHash);
        animator.SetTrigger(howlHash);
    }

    private void TriggerEatAnimation()
    {
        if (animator == null || !hasEatTriggerParameter)
            return;

        animator.ResetTrigger(eatHash);
        animator.SetTrigger(eatHash);
    }
}