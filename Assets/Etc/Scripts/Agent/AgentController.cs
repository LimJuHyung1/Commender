using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class AgentController : MonoBehaviour
{
    [Header("Agent Common Settings")]
    [SerializeField] protected int agentID;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected AgentStatsSO stats;

    [Header("Optional References")]
    [SerializeField] protected VisionSensor visionSensor;
    [SerializeField] protected Light spotLight;
    [SerializeField] private AgentStateController stateIconController;

    [Header("Animation")]
    [SerializeField] protected Animator animator;
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private bool useMoveSpeedParameter = true;
    [SerializeField] private float movingThreshold = 0.05f;

    [Header("Look Around")]
    [SerializeField] private float lookAroundAngle = 70f;
    [SerializeField] private float lookAroundTurnSpeed = 240f;
    [SerializeField] private float lookAroundPauseSeconds = 0.08f;

    [Header("Command")]
    [SerializeField] protected ChatServiceOpenAI commandChatService;
    [SerializeField, TextArea(6, 20)] private string commandSystemPromptOverride;

    [Header("Chase")]
    [SerializeField] private float chaseRepathInterval = 0.15f;
    [SerializeField] private float chaseDestinationThreshold = 0.25f;

    [Header("Skill Gauge")]
    [SerializeField] private float skillGaugeChargePerMeter = 1f;

    private const float DefaultSkillGaugeMax = 100f;
    private const float SkillGaugeFullEpsilon = 0.001f;

    private const float SharedTargetPositionMemoryDuration = 3f;
    private const float SharedTargetRepathInterval = 0.15f;
    private const float SharedTargetDestinationThreshold = 0.25f;

    protected NavMeshAgent navAgent;
    protected Transform currentTarget;
    protected bool isManualMoving = false;

    private float skillGauge = 0f;
    private Vector3 lastSkillGaugePosition;
    private float skillGaugeChargeBlockedUntil = -1f;

    private float chaseRepathTimer = 0f;
    private Vector3 lastChaseDestination;
    private bool hasLastChaseDestination = false;

    private bool hasSharedTargetPosition = false;
    private bool isFollowingSharedTargetPosition = false;
    private Vector3 sharedTargetPosition;
    private Vector3 lastSharedTargetDestination;
    private bool hasLastSharedTargetDestination = false;
    private float sharedTargetPositionExpireTime = -1f;
    private float sharedTargetRepathTimer = 0f;
    private AgentController sharedTargetReporter;

    private int isMovingParameterHash;
    private int moveSpeedParameterHash;

    private Coroutine lookAroundRoutine;
    private bool isLookingAround = false;
    private bool previousNavUpdateRotation = true;

    public ChatServiceOpenAI CommandChatService => commandChatService;
    public string CommandSystemPromptOverride => commandSystemPromptOverride;

    public int AgentID => agentID;
    public Transform CurrentTarget => currentTarget;
    public bool IsManualMoving => isManualMoving;
    public bool IsChasing => currentTarget != null;
    public bool IsLookingAround => isLookingAround;
    public LayerMask TargetLayer => targetLayer;
    public AgentStatsSO Stats => stats;
    public bool IsSmokeDebuffed => visionSensor != null && visionSensor.IsSmokeDebuffed;

    public float SkillGauge => skillGauge;
    public float SkillGaugeCapacity => GetCurrentSkillGaugeCapacity();
    public float SkillGaugeNormalized => SkillGaugeCapacity <= 0f ? 1f : Mathf.Clamp01(skillGauge / SkillGaugeCapacity);

    public bool HasSharedTargetPosition => hasSharedTargetPosition;
    public Vector3 SharedTargetPosition => sharedTargetPosition;
    public AgentController SharedTargetReporter => sharedTargetReporter;
    public bool IsFollowingSharedTargetPosition => isFollowingSharedTargetPosition;

    public abstract void ExecuteSkill(string skillName, Vector3 targetPos);

    protected virtual void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        lastSkillGaugePosition = transform.position;

        if (visionSensor == null)
            visionSensor = GetComponentInChildren<VisionSensor>(true);

        if (spotLight == null)
        {
            Light[] lights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Spot)
                {
                    spotLight = lights[i];
                    break;
                }
            }
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (commandChatService == null)
            commandChatService = GetComponentInChildren<ChatServiceOpenAI>(true);

        if (stateIconController == null)
            stateIconController = GetComponentInChildren<AgentStateController>(true);

        CacheAnimatorParameterHashes();
        ApplyCommonStats();
        UpdateAnimationState(true);
        UpdateStateIcon();
    }

    protected virtual void OnDisable()
    {
        lastSkillGaugePosition = transform.position;

        StopLookAroundInternal(false);
        ClearSharedTargetPosition();
        UpdateStateIcon();
    }

    protected virtual void OnValidate()
    {
        CacheAnimatorParameterHashes();
    }

    protected virtual void ApplyCommonStats()
    {
        if (stats == null)
        {
            Debug.LogWarning($"[Agent {agentID}] AgentStatsSO가 설정되지 않았습니다.");
            return;
        }

        ApplyNavAgentStats();
        ApplyVisionStats();
        ApplySpotLightStats();
    }

    protected virtual void ApplyNavAgentStats()
    {
        if (navAgent == null)
            return;

        navAgent.speed = stats.moveSpeed;
        navAgent.acceleration = stats.acceleration;
        navAgent.stoppingDistance = stats.stoppingDistance;
        navAgent.angularSpeed = stats.angularSpeed;
    }

    protected virtual void ApplyVisionStats()
    {
        if (visionSensor == null || stats == null)
            return;

        visionSensor.ApplyStats(stats);
    }

    protected virtual void ApplySpotLightStats()
    {
        if (spotLight == null || stats == null)
            return;

        spotLight.enabled = stats.useSpotLight;
        spotLight.color = stats.spotLightColor;
        spotLight.intensity = stats.spotLightIntensity;
        spotLight.range = stats.spotLightRange;
        spotLight.innerSpotAngle = stats.spotLightInnerAngle;
        spotLight.spotAngle = stats.spotLightOuterAngle;
    }

    protected virtual void Update()
    {
        if (navAgent == null)
            return;

        UpdateSkillGaugeCharge();

        if (isLookingAround)
        {
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        if (isManualMoving)
        {
            CheckDestinationReached();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        if (currentTarget != null)
        {
            chaseRepathTimer -= Time.deltaTime;

            Vector3 targetPos = currentTarget.position;
            bool shouldRepath = !hasLastChaseDestination ||
                                Vector3.Distance(lastChaseDestination, targetPos) >= chaseDestinationThreshold;

            if (chaseRepathTimer <= 0f && shouldRepath)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(targetPos);
                lastChaseDestination = targetPos;
                hasLastChaseDestination = true;
                chaseRepathTimer = chaseRepathInterval;
            }
        }
        else
        {
            UpdateSharedTargetPositionMovement();
        }

        UpdateAnimationState();
        UpdateStateIcon();
    }

    private void UpdateSkillGaugeCharge()
    {
        Vector3 currentPosition = transform.position;
        float capacity = GetCurrentSkillGaugeCapacity();

        if (capacity <= 0f)
        {
            skillGauge = 0f;
            lastSkillGaugePosition = currentPosition;
            return;
        }

        if (IsSkillGaugeChargeBlocked())
        {
            lastSkillGaugePosition = currentPosition;
            return;
        }

        if (skillGauge >= capacity - SkillGaugeFullEpsilon)
        {
            skillGauge = capacity;
            lastSkillGaugePosition = currentPosition;
            return;
        }

        float movedDistance = Vector3.Distance(lastSkillGaugePosition, currentPosition);
        lastSkillGaugePosition = currentPosition;

        if (movedDistance <= 0.001f)
            return;

        if (!ShouldChargeSkillGauge())
            return;

        skillGauge = Mathf.Min(
            capacity,
            skillGauge + movedDistance * skillGaugeChargePerMeter
        );
    }

    private bool ShouldChargeSkillGauge()
    {
        if (navAgent == null)
            return false;

        if (navAgent.isStopped)
            return false;

        if (navAgent.velocity.sqrMagnitude <= movingThreshold * movingThreshold)
            return false;

        if (isManualMoving)
            return true;

        if (currentTarget != null)
            return true;

        if (isFollowingSharedTargetPosition)
            return true;

        return navAgent.hasPath && !navAgent.pathPending;
    }

    private bool IsSkillGaugeChargeBlocked()
    {
        return Time.time < skillGaugeChargeBlockedUntil;
    }

    protected void BlockSkillGaugeCharge(float seconds)
    {
        if (seconds <= 0f)
            return;

        skillGaugeChargeBlockedUntil = Mathf.Max(
            skillGaugeChargeBlockedUntil,
            Time.time + seconds
        );

        lastSkillGaugePosition = transform.position;
    }

    private float GetCurrentSkillGaugeCapacity()
    {
        if (stats == null)
            return DefaultSkillGaugeMax;

        return Mathf.Max(0f, stats.GetLargestSkillGaugeMax());
    }

    public float GetSkillGaugeMaxForSkill(string skillName)
    {
        if (stats == null)
            return DefaultSkillGaugeMax;

        return Mathf.Max(0f, stats.GetSkillGaugeMax(skillName));
    }

    public float GetSkillGaugeNormalizedForSkill(string skillName)
    {
        float requiredGauge = GetSkillGaugeMaxForSkill(skillName);

        if (requiredGauge <= 0f)
            return 1f;

        return Mathf.Clamp01(skillGauge / requiredGauge);
    }

    public bool CanUseSkillGaugeForSkill(string skillName, bool showWarning = false)
    {
        float requiredGauge = GetSkillGaugeMaxForSkill(skillName);

        if (requiredGauge <= 0f)
            return true;

        bool canUse = skillGauge >= requiredGauge - SkillGaugeFullEpsilon;

        if (!canUse && showWarning)
        {
            Debug.LogWarning(
                $"[Agent {AgentID}] '{skillName}' 스킬을 사용할 수 없습니다. " +
                $"현재 게이지: {skillGauge:0.#} / 필요 게이지: {requiredGauge:0.#}"
            );
        }

        return canUse;
    }

    protected bool TryConsumeSkillGaugeForSkill(string skillName, float chargeBlockSeconds = 0f)
    {
        float requiredGauge = GetSkillGaugeMaxForSkill(skillName);

        if (requiredGauge <= 0f)
            return true;

        if (!CanUseSkillGaugeForSkill(skillName, true))
            return false;

        skillGauge = 0f;
        lastSkillGaugePosition = transform.position;

        if (chargeBlockSeconds > 0f)
            BlockSkillGaugeCharge(chargeBlockSeconds);

        Debug.Log($"[Agent {AgentID}] '{skillName}' 스킬 사용. 스킬 게이지를 소모했습니다.");
        return true;
    }

    public void ResetSkillGauge()
    {
        skillGauge = 0f;
        lastSkillGaugePosition = transform.position;
        skillGaugeChargeBlockedUntil = -1f;
    }

    public void FillSkillGauge()
    {
        skillGauge = GetCurrentSkillGaugeCapacity();
        lastSkillGaugePosition = transform.position;
    }

    protected virtual void CheckDestinationReached()
    {
        if (navAgent.pathPending)
            return;

        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid ||
            navAgent.pathStatus == NavMeshPathStatus.PathPartial)
        {
            isManualMoving = false;
            navAgent.ResetPath();
            UpdateAnimationState(true);
            UpdateStateIcon();
            Debug.LogWarning($"[Agent {AgentID}] Manual move failed. Path is not complete.");
            return;
        }

        if (!navAgent.hasPath)
            return;

        if (navAgent.remainingDistance <= navAgent.stoppingDistance)
        {
            if (navAgent.velocity.sqrMagnitude <= 0.01f)
            {
                float realDistance = Vector3.Distance(transform.position, navAgent.destination);

                if (realDistance <= navAgent.stoppingDistance + 0.15f)
                {
                    isManualMoving = false;
                    navAgent.ResetPath();
                    UpdateAnimationState(true);
                    UpdateStateIcon();
                    Debug.Log($"[Agent {AgentID}] Reached manual destination. Switched to idle state.");
                }
            }
        }
    }

    public void SetAgentID(int id)
    {
        agentID = id;
    }

    public virtual void MoveTo(Vector3 destination)
    {
        Debug.Log($"[Agent {AgentID}] MoveTo 호출. destination={destination}, currentTarget={(currentTarget != null ? currentTarget.name : "null")}");

        if (navAgent == null)
            return;

        if (isLookingAround)
            StopLookAroundInternal(false);

        if (currentTarget != null)
        {
            Debug.Log($"[Agent {AgentID}] 현재 추적 중이므로 MoveTo를 무시합니다.");
            return;
        }

        currentTarget = null;
        isManualMoving = false;
        isFollowingSharedTargetPosition = false;
        hasLastChaseDestination = false;
        hasLastSharedTargetDestination = false;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(destination, out hit, 2.0f, navAgent.areaMask))
        {
            UpdateAnimationState(true);
            UpdateStateIcon();
            Debug.LogWarning($"[Agent {AgentID}] Failed to find valid NavMesh position near {destination}");
            return;
        }

        NavMeshPath path = new NavMeshPath();
        bool pathFound = navAgent.CalculatePath(hit.position, path);

        if (!pathFound || path.status != NavMeshPathStatus.PathComplete)
        {
            navAgent.ResetPath();
            UpdateAnimationState(true);
            UpdateStateIcon();
            Debug.LogWarning($"[Agent {AgentID}] 도달 불가 목적지입니다. destination={destination}, sampled={hit.position}, pathStatus={path.status}");
            return;
        }

        navAgent.isStopped = false;
        navAgent.SetDestination(hit.position);
        isManualMoving = true;
        UpdateAnimationState(true);
        UpdateStateIcon();

        Debug.Log($"[Agent {AgentID}] Manual move started: {hit.position}");
    }

    public bool TryStartLookAround()
    {
        if (navAgent == null)
            return false;

        if (currentTarget != null)
        {
            Debug.LogWarning($"[Agent {AgentID}] 현재 추적 중이므로 주변 둘러보기를 시작할 수 없습니다.");
            return false;
        }

        if (isManualMoving)
        {
            Debug.LogWarning($"[Agent {AgentID}] 현재 이동 중이므로 주변 둘러보기를 시작할 수 없습니다.");
            return false;
        }

        StopLookAroundInternal(false);
        lookAroundRoutine = StartCoroutine(LookAroundCoroutine());
        return true;
    }

    private IEnumerator LookAroundCoroutine()
    {
        isLookingAround = true;
        isFollowingSharedTargetPosition = false;

        if (navAgent != null)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
            previousNavUpdateRotation = navAgent.updateRotation;
            navAgent.updateRotation = false;
        }

        UpdateAnimationState(true);
        UpdateStateIcon();
        Debug.Log($"[Agent {AgentID}] 주변 둘러보기 시작");

        float startY = transform.eulerAngles.y;

        yield return RotateToYaw(startY - lookAroundAngle);

        if (lookAroundPauseSeconds > 0f)
            yield return new WaitForSeconds(lookAroundPauseSeconds);

        yield return RotateToYaw(startY + lookAroundAngle);

        if (lookAroundPauseSeconds > 0f)
            yield return new WaitForSeconds(lookAroundPauseSeconds);

        yield return RotateToYaw(startY);

        FinishLookAround();
        Debug.Log($"[Agent {AgentID}] 주변 둘러보기 종료");
    }

    private IEnumerator RotateToYaw(float targetYaw)
    {
        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);

        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.5f)
        {
            if (!isLookingAround)
                yield break;

            if (currentTarget != null || isManualMoving)
            {
                StopLookAroundInternal(false);
                yield break;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                lookAroundTurnSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void FinishLookAround()
    {
        if (navAgent != null)
        {
            navAgent.updateRotation = previousNavUpdateRotation;
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        isLookingAround = false;
        lookAroundRoutine = null;
        UpdateAnimationState(true);
        UpdateStateIcon();
    }

    private void StopLookAroundInternal(bool logMessage)
    {
        if (lookAroundRoutine != null)
        {
            StopCoroutine(lookAroundRoutine);
            lookAroundRoutine = null;
        }

        if (!isLookingAround)
            return;

        if (navAgent != null)
        {
            navAgent.updateRotation = previousNavUpdateRotation;
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        isLookingAround = false;
        UpdateAnimationState(true);
        UpdateStateIcon();

        if (logMessage)
            Debug.Log($"[Agent {AgentID}] 주변 둘러보기를 중단했습니다.");
    }

    public virtual void SetChaseTarget(Transform target)
    {
        if (navAgent == null || target == null)
            return;

        if (isLookingAround)
            StopLookAroundInternal(false);

        currentTarget = target;
        isManualMoving = false;
        isFollowingSharedTargetPosition = false;
        navAgent.isStopped = false;

        hasLastChaseDestination = false;
        hasLastSharedTargetDestination = false;
        chaseRepathTimer = 0f;

        navAgent.SetDestination(currentTarget.position);
        UpdateAnimationState(true);
        UpdateStateIcon();

        Debug.Log($"[Agent {AgentID}] Chase target assigned: {target.name}");
    }

    public virtual void ClearChaseTarget(Transform target)
    {
        if (target == null)
            return;

        if (currentTarget != target)
            return;

        currentTarget = null;
        hasLastChaseDestination = false;

        if (!isManualMoving && navAgent != null)
            navAgent.ResetPath();

        UpdateAnimationState(true);
        UpdateStateIcon();
        Debug.Log($"[Agent {AgentID}] Chase target cleared: {target.name}");
    }

    public virtual void StopChase()
    {
        currentTarget = null;
        hasLastChaseDestination = false;

        if (!isManualMoving && navAgent != null)
            navAgent.ResetPath();

        UpdateAnimationState(true);
        UpdateStateIcon();
        Debug.Log($"[Agent {AgentID}] Chase stopped.");
    }

    public virtual void ReceiveSharedTargetPosition(Vector3 position, AgentController reporter)
    {
        if (!isActiveAndEnabled)
            return;

        if (reporter == null)
            return;

        if (currentTarget != null)
            return;

        hasSharedTargetPosition = true;
        sharedTargetPosition = position;
        sharedTargetReporter = reporter;
        sharedTargetPositionExpireTime = Time.time + SharedTargetPositionMemoryDuration;
    }

    public virtual void ClearSharedTargetPosition(AgentController reporter = null)
    {
        if (reporter != null && sharedTargetReporter != null && sharedTargetReporter != reporter)
            return;

        hasSharedTargetPosition = false;
        isFollowingSharedTargetPosition = false;
        sharedTargetReporter = null;
        sharedTargetPositionExpireTime = -1f;
        sharedTargetRepathTimer = 0f;
        hasLastSharedTargetDestination = false;

        if (currentTarget == null && !isManualMoving && !isLookingAround && navAgent != null)
            navAgent.ResetPath();

        UpdateAnimationState(true);
        UpdateStateIcon();
    }

    private void UpdateSharedTargetPositionMovement()
    {
        if (!hasSharedTargetPosition)
        {
            isFollowingSharedTargetPosition = false;
            return;
        }

        if (Time.time > sharedTargetPositionExpireTime)
        {
            ClearSharedTargetPosition(sharedTargetReporter);
            return;
        }

        if (navAgent == null)
            return;

        if (currentTarget != null || isManualMoving || isLookingAround)
            return;

        sharedTargetRepathTimer -= Time.deltaTime;

        bool shouldRepath = !hasLastSharedTargetDestination ||
                            Vector3.Distance(lastSharedTargetDestination, sharedTargetPosition) >= SharedTargetDestinationThreshold;

        if (sharedTargetRepathTimer > 0f && !shouldRepath)
            return;

        if (!NavMesh.SamplePosition(sharedTargetPosition, out NavMeshHit hit, 2.0f, navAgent.areaMask))
            return;

        NavMeshPath path = new NavMeshPath();
        bool pathFound = navAgent.CalculatePath(hit.position, path);

        if (!pathFound || path.status != NavMeshPathStatus.PathComplete)
            return;

        navAgent.isStopped = false;
        navAgent.SetDestination(hit.position);

        lastSharedTargetDestination = sharedTargetPosition;
        hasLastSharedTargetDestination = true;
        isFollowingSharedTargetPosition = true;
        sharedTargetRepathTimer = SharedTargetRepathInterval;
    }

    public virtual void ReapplyStats()
    {
        ApplyCommonStats();
        UpdateStateIcon();
    }

    protected virtual void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        float speed = navAgent.velocity.magnitude;
        bool isMovingNow = !navAgent.isStopped &&
                           (
                               navAgent.pathPending ||
                               navAgent.hasPath ||
                               isManualMoving ||
                               currentTarget != null ||
                               isFollowingSharedTargetPosition
                           ) &&
                           speed > movingThreshold;

        animator.SetBool(isMovingParameterHash, isMovingNow);

        if (useMoveSpeedParameter)
        {
            float normalizedSpeed;

            if (stats != null && stats.moveSpeed > 0.01f)
                normalizedSpeed = Mathf.Clamp01(speed / stats.moveSpeed);
            else
                normalizedSpeed = Mathf.Clamp01(speed);

            if (immediate)
                animator.SetFloat(moveSpeedParameterHash, normalizedSpeed);
            else
                animator.SetFloat(moveSpeedParameterHash, normalizedSpeed, 0.1f, Time.deltaTime);
        }
    }

    protected virtual void UpdateStateIcon()
    {
        if (stateIconController == null)
            return;

        if (IsSmokeDebuffed)
        {
            stateIconController.SetState(AgentStateController.AgentAwarenessState.BlindedBySmoke);
            return;
        }

        if (currentTarget != null)
        {
            stateIconController.SetState(AgentStateController.AgentAwarenessState.ChasingTarget);
            return;
        }

        stateIconController.ClearState();
    }

    private void CacheAnimatorParameterHashes()
    {
        isMovingParameterHash = string.IsNullOrWhiteSpace(isMovingParameter)
            ? Animator.StringToHash("IsMoving")
            : Animator.StringToHash(isMovingParameter);

        moveSpeedParameterHash = string.IsNullOrWhiteSpace(moveSpeedParameter)
            ? Animator.StringToHash("MoveSpeed")
            : Animator.StringToHash(moveSpeedParameter);
    }
}