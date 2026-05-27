using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private float animationStopDelay = 0.18f;
    [SerializeField] private float animationStopDistanceBuffer = 0.15f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;

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

    private const string SkillGaugeDefaultKey = "default";
    private const string SkillAccessControlKey = "accesscontrol";
    private const string SkillDroneKey = "drone";
    private const string SkillBarricadeKey = "barricade";
    private const string SkillStopSignalKey = "stopsignal";
    private const string SkillFakeBoxKey = "fakebox";
    private const string SkillJokerCardKey = "jokercard";
    private const string SkillNoisemakerKey = "noisemaker";
    private const string SkillHologramKey = "hologram";
    private const string SkillDashKey = "dash";
    private const string SkillSmokeKey = "smoke";

    protected NavMeshAgent navAgent;
    protected Transform currentTarget;
    protected bool isManualMoving = false;

    private readonly Dictionary<string, float> skillGaugeByKey = new Dictionary<string, float>();
    private readonly HashSet<object> skillCommandBlockers = new HashSet<object>();

    private Vector3 lastSkillGaugePosition;
    private float skillGaugeChargeBlockedUntil = -1f;

    private float chaseRepathTimer = 0f;
    private Vector3 lastChaseDestination;
    private bool hasLastChaseDestination;

    private bool hasSharedTargetPosition;
    private bool isFollowingSharedTargetPosition;
    private Vector3 sharedTargetPosition;
    private Vector3 lastSharedTargetDestination;
    private bool hasLastSharedTargetDestination;
    private float sharedTargetPositionExpireTime = -1f;
    private float sharedTargetRepathTimer;
    private AgentController sharedTargetReporter;
    private float sharedTargetMoveSpeedMultiplier = 1f;
    private bool sharedTargetMoveSpeedApplied;

    private int isMovingParameterHash;
    private int moveSpeedParameterHash;

    private bool cachedAnimationIsMoving;
    private float lastAnimationMovingTime = -999f;

    private Coroutine lookAroundRoutine;
    private bool isLookingAround;
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
    public VisionSensor VisionSensor => visionSensor;
    public bool IsSkillCommandBlocked => skillCommandBlockers.Count > 0;

    protected virtual bool ShouldIgnoreDebuffStateIcon => false;
    protected virtual float SkillGaugeChargeMultiplier => 1f;

    protected virtual bool ShouldBlockSharedTargetMovement => false;

    public float SkillGauge => GetLargestCurrentSkillGauge();
    public float SkillGaugeCapacity => GetCurrentSkillGaugeCapacity();
    public float SkillGaugeNormalized => SkillGaugeCapacity <= 0f ? 1f : Mathf.Clamp01(SkillGauge / SkillGaugeCapacity);

    public bool HasSharedTargetPosition => hasSharedTargetPosition;
    public Vector3 SharedTargetPosition => sharedTargetPosition;
    public AgentController SharedTargetReporter => sharedTargetReporter;
    public bool IsFollowingSharedTargetPosition => isFollowingSharedTargetPosition;

    public abstract void ExecuteSkill(string skillName, Vector3 targetPos);

    public virtual void AddSkillCommandBlocker(object source)
    {
        if (source == null)
            return;

        bool added = skillCommandBlockers.Add(source);

        if (!added)
            return;

        UpdateStateIcon();

        Debug.Log($"[Agent {AgentID}] 스킬 명령 차단 상태가 적용되었습니다.");
    }

    public virtual void RemoveSkillCommandBlocker(object source)
    {
        if (source == null)
            return;

        bool removed = skillCommandBlockers.Remove(source);

        if (!removed)
            return;

        UpdateStateIcon();

        Debug.Log($"[Agent {AgentID}] 스킬 명령 차단 상태가 해제되었습니다.");
    }

    public virtual void ClearSkillCommandBlockers(bool updateIcon = true)
    {
        if (skillCommandBlockers.Count <= 0)
            return;

        skillCommandBlockers.Clear();

        if (updateIcon)
            UpdateStateIcon();
    }

    public virtual bool CanReceivePlayerSkillCommand(bool showWarning = false)
    {
        if (!IsSkillCommandBlocked)
            return true;

        if (showWarning)
        {
            Debug.LogWarning(
                $"[Agent {AgentID}] 그래피티 교란 구역 안에 있어 스킬 명령을 받을 수 없습니다."
            );
        }

        return false;
    }

    protected void RequestSkillCamera(SkillCameraFocusMode mode)
    {
        SkillCameraEventBus.Request(
            mode,
            transform
        );
    }

    protected void RequestSkillCamera(SkillCameraFocusMode mode, Transform objectTarget)
    {
        SkillCameraEventBus.Request(
            mode,
            transform,
            objectTarget
        );
    }

    protected void RequestUserSkillCamera()
    {
        RequestSkillCamera(SkillCameraFocusMode.UserOnly);
    }

    protected void RequestInstalledObjectCamera(Transform objectTarget)
    {
        if (objectTarget == null)
            return;

        SkillCameraEventBus.Request(
            SkillCameraFocusMode.ObjectOnly,
            null,
            objectTarget
        );
    }

    protected void RequestStrongSkillCamera()
    {
        RequestSkillCamera(SkillCameraFocusMode.StrongTargetEvent);
    }

    protected void RequestFollowUserSkillCamera()
    {
        SkillCameraEventBus.Request(
            SkillCameraFocusMode.FollowUser,
            transform
        );
    }

    protected virtual void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        lastSkillGaugePosition = transform.position;

        if (visionSensor == null)
            visionSensor = GetComponentInChildren<VisionSensor>(true);

        if (spotLight == null)
            spotLight = FindSpotLightInChildren();

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
        cachedAnimationIsMoving = false;
        lastAnimationMovingTime = -999f;

        StopLookAroundInternal(false);
        ClearSharedTargetPosition();
        ClearSkillCommandBlockers(false);
        UpdateStateIcon();
    }

    protected virtual void OnValidate()
    {
        movingThreshold = Mathf.Max(0f, movingThreshold);
        animationStopDelay = Mathf.Max(0f, animationStopDelay);
        animationStopDistanceBuffer = Mathf.Max(0f, animationStopDistanceBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);
        skillGaugeChargePerMeter = Mathf.Max(0f, skillGaugeChargePerMeter);

        lookAroundAngle = Mathf.Max(0f, lookAroundAngle);
        lookAroundTurnSpeed = Mathf.Max(0f, lookAroundTurnSpeed);
        lookAroundPauseSeconds = Mathf.Max(0f, lookAroundPauseSeconds);

        chaseRepathInterval = Mathf.Max(0.01f, chaseRepathInterval);
        chaseDestinationThreshold = Mathf.Max(0f, chaseDestinationThreshold);

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
        if (navAgent == null || stats == null)
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
            UpdateChaseMovement();
        }
        else
        {
            UpdateSharedTargetPositionMovement();
        }

        UpdateAnimationState();
        UpdateStateIcon();
    }

    private Light FindSpotLightInChildren()
    {
        Light[] lights = GetComponentsInChildren<Light>(true);

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Spot)
                return lights[i];
        }

        return null;
    }

    private void UpdateChaseMovement()
    {
        if (currentTarget == null || navAgent == null)
            return;

        chaseRepathTimer -= Time.deltaTime;

        Vector3 targetPos = currentTarget.position;
        bool shouldRepath = !hasLastChaseDestination ||
                            Vector3.Distance(lastChaseDestination, targetPos) >= chaseDestinationThreshold;

        if (chaseRepathTimer > 0f && !shouldRepath)
            return;

        navAgent.isStopped = false;
        navAgent.SetDestination(targetPos);

        lastChaseDestination = targetPos;
        hasLastChaseDestination = true;
        chaseRepathTimer = chaseRepathInterval;
    }

    private void UpdateSkillGaugeCharge()
    {
        Vector3 currentPosition = transform.position;
        float movedDistance = Vector3.Distance(lastSkillGaugePosition, currentPosition);

        if (IsSkillGaugeChargeBlocked())
        {
            lastSkillGaugePosition = currentPosition;
            return;
        }

        lastSkillGaugePosition = currentPosition;

        if (movedDistance <= 0.001f)
            return;

        OnAgentMoved(movedDistance);

        if (!ShouldChargeSkillGauge())
            return;

        float chargeMultiplier = Mathf.Max(0f, SkillGaugeChargeMultiplier);
        float chargeAmount = movedDistance * skillGaugeChargePerMeter * chargeMultiplier;
        string[] gaugeKeys = GetCurrentAgentGaugeKeys();

        for (int i = 0; i < gaugeKeys.Length; i++)
        {
            string key = gaugeKeys[i];
            float capacity = GetSkillGaugeMaxForSkill(key);

            if (capacity <= 0f)
                continue;

            float currentGauge = GetSkillGaugeValue(key);

            if (currentGauge >= capacity - SkillGaugeFullEpsilon)
            {
                SetSkillGaugeValue(key, capacity);
                continue;
            }

            SetSkillGaugeValue(key, Mathf.Min(capacity, currentGauge + chargeAmount));
        }
    }

    private bool ShouldChargeSkillGauge()
    {
        if (navAgent == null)
            return false;

        if (navAgent.isStopped)
            return false;

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > movingThreshold * movingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > movingThreshold * movingThreshold;

        if (!hasVelocity)
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

        skillGaugeChargeBlockedUntil = Mathf.Max(skillGaugeChargeBlockedUntil, Time.time + seconds);
        lastSkillGaugePosition = transform.position;
    }

    protected virtual void OnAgentMoved(float movedDistance)
    {
    }

    private float GetCurrentSkillGaugeCapacity()
    {
        string[] gaugeKeys = GetCurrentAgentGaugeKeys();
        float largestCapacity = 0f;

        for (int i = 0; i < gaugeKeys.Length; i++)
        {
            float requiredGauge = GetSkillGaugeRequiredForSkill(gaugeKeys[i]);

            if (requiredGauge > largestCapacity)
                largestCapacity = requiredGauge;
        }

        if (largestCapacity <= 0f)
            return stats != null ? Mathf.Max(0f, stats.GetLargestSkillGaugeMax()) : DefaultSkillGaugeMax;

        return largestCapacity;
    }

    public virtual float GetSkillGaugeMaxForSkill(string skillName)
    {
        if (stats == null)
            return DefaultSkillGaugeMax;

        string key = GetSkillGaugeKey(skillName);
        return Mathf.Max(0f, stats.GetSkillGaugeMax(key));
    }

    public virtual float GetSkillGaugeRequiredForSkill(string skillName)
    {
        return GetSkillGaugeMaxForSkill(skillName);
    }

    public virtual float GetSkillGaugeCurrentForSkill(string skillName)
    {
        float requiredGauge = GetSkillGaugeRequiredForSkill(skillName);

        if (requiredGauge <= 0f)
            return 0f;

        string key = GetSkillGaugeKey(skillName);
        return Mathf.Clamp(GetSkillGaugeValue(key), 0f, requiredGauge);
    }

    public virtual float GetSkillGaugeNormalizedForSkill(string skillName)
    {
        float requiredGauge = GetSkillGaugeRequiredForSkill(skillName);

        if (requiredGauge <= 0f)
            return 1f;

        return Mathf.Clamp01(GetSkillGaugeCurrentForSkill(skillName) / requiredGauge);
    }

    public virtual bool CanUseSkillGaugeForSkill(string skillName, bool showWarning = false)
    {
        float requiredGauge = GetSkillGaugeRequiredForSkill(skillName);

        if (requiredGauge <= 0f)
            return true;

        string key = GetSkillGaugeKey(skillName);
        float rawCurrentGauge = GetSkillGaugeValue(key);
        bool canUse = rawCurrentGauge >= requiredGauge - SkillGaugeFullEpsilon;

        if (!canUse && showWarning)
        {
            Debug.LogWarning(
                $"[Agent {AgentID}] '{skillName}' 스킬을 사용할 수 없습니다. " +
                $"현재 게이지: {rawCurrentGauge:0.#} / 필요 게이지: {requiredGauge:0.#}"
            );
        }

        return canUse;
    }

    protected virtual bool TryConsumeSkillGaugeForSkill(string skillName, float chargeBlockSeconds = 0f)
    {
        float requiredGauge = GetSkillGaugeRequiredForSkill(skillName);

        if (requiredGauge <= 0f)
            return true;

        if (!CanUseSkillGaugeForSkill(skillName, true))
            return false;

        string key = GetSkillGaugeKey(skillName);
        float currentGauge = GetSkillGaugeValue(key);
        float nextGauge = Mathf.Max(0f, currentGauge - requiredGauge);

        SetSkillGaugeValue(key, nextGauge);
        lastSkillGaugePosition = transform.position;

        if (chargeBlockSeconds > 0f)
            BlockSkillGaugeCharge(chargeBlockSeconds);

        Debug.Log(
            $"[Agent {AgentID}] '{skillName}' 스킬 사용. " +
            $"소모 게이지: {requiredGauge:0.#}, 남은 게이지: {nextGauge:0.#}"
        );

        return true;
    }

    public virtual void ResetSkillGauge()
    {
        skillGaugeByKey.Clear();
        lastSkillGaugePosition = transform.position;
        skillGaugeChargeBlockedUntil = -1f;
    }

    public virtual void FillSkillGauge()
    {
        string[] gaugeKeys = GetCurrentAgentGaugeKeys();

        for (int i = 0; i < gaugeKeys.Length; i++)
        {
            string key = gaugeKeys[i];
            float capacity = GetSkillGaugeMaxForSkill(key);

            if (capacity <= 0f)
                continue;

            SetSkillGaugeValue(key, capacity);
        }

        lastSkillGaugePosition = transform.position;
    }

    private float GetSkillGaugeValue(string key)
    {
        key = GetSkillGaugeKey(key);

        if (!skillGaugeByKey.TryGetValue(key, out float value))
            return 0f;

        return Mathf.Max(0f, value);
    }

    private void SetSkillGaugeValue(string key, float value)
    {
        key = GetSkillGaugeKey(key);
        skillGaugeByKey[key] = Mathf.Max(0f, value);
    }

    private float GetLargestCurrentSkillGauge()
    {
        string[] gaugeKeys = GetCurrentAgentGaugeKeys();
        float largestGauge = 0f;

        for (int i = 0; i < gaugeKeys.Length; i++)
        {
            float currentGauge = GetSkillGaugeCurrentForSkill(gaugeKeys[i]);

            if (currentGauge > largestGauge)
                largestGauge = currentGauge;
        }

        return largestGauge;
    }

    private string GetSkillGaugeKey(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return SkillGaugeDefaultKey;

        string skill = skillName.Trim().ToLower();

        if (IsAccessControlSkill(skill))
            return SkillAccessControlKey;

        if (IsDroneSkill(skill))
            return SkillDroneKey;

        if (IsBarricadeSkill(skill))
            return SkillBarricadeKey;

        if (IsStopSignalSkill(skill))
            return SkillStopSignalKey;

        if (IsFakeBoxSkill(skill))
            return SkillFakeBoxKey;

        if (IsJokerCardSkill(skill))
            return SkillJokerCardKey;

        if (IsNoisemakerSkill(skill))
            return SkillNoisemakerKey;

        if (IsHologramSkill(skill))
            return SkillHologramKey;

        if (IsDashSkill(skill))
            return SkillDashKey;

        if (IsSmokeSkill(skill))
            return SkillSmokeKey;

        return skill;
    }

    protected virtual string[] GetCurrentAgentGaugeKeys()
    {
        if (stats == null)
        {
            return new[]
            {
                SkillGaugeDefaultKey
            };
        }

        switch (stats.role)
        {
            case AgentRole.Chaser:
                return new[]
                {
                    SkillAccessControlKey
                };

            case AgentRole.Observer:
                return new[]
                {
                    SkillDroneKey
                };

            case AgentRole.Engineer:
                return new[]
                {
                    SkillBarricadeKey,
                    SkillStopSignalKey
                };

            case AgentRole.Trickster:
                return new[]
                {
                    SkillFakeBoxKey,
                    SkillJokerCardKey
                };

            default:
                return new[]
                {
                    SkillAccessControlKey,
                    SkillDroneKey,
                    SkillBarricadeKey,
                    SkillStopSignalKey,
                    SkillFakeBoxKey,
                    SkillJokerCardKey,
                    SkillNoisemakerKey,
                    SkillHologramKey,
                    SkillDashKey,
                    SkillSmokeKey
                };
        }
    }

    private bool IsAccessControlSkill(string skill)
    {
        return skill.Contains("accesscontrol") ||
               skill.Contains("access control") ||
               skill.Contains("controlzone") ||
               skill.Contains("control zone") ||
               skill.Contains("restricted zone") ||
               skill.Contains("출입통제") ||
               skill.Contains("출입 통제") ||
               skill.Contains("통제구역") ||
               skill.Contains("통제 구역") ||
               skill.Contains("금지구역") ||
               skill.Contains("금지 구역");
    }

    private bool IsDroneSkill(string skill)
    {
        return skill.Contains("drone") ||
               skill.Contains("uav") ||
               skill.Contains("드론");
    }

    private bool IsBarricadeSkill(string skill)
    {
        return skill.Contains("barricade") ||
               skill.Contains("바리케이드") ||
               skill.Contains("봉쇄") ||
               skill.Contains("장애물");
    }

    private bool IsStopSignalSkill(string skill)
    {
        return skill.Contains("stopsignal") ||
               skill.Contains("stop signal") ||
               skill.Contains("stop sign") ||
               skill.Contains("slowtrap") ||
               skill.Contains("slow trap") ||
               skill.Contains("snaretrap") ||
               skill.Contains("정지신호") ||
               skill.Contains("정지 신호") ||
               skill.Contains("정지표지") ||
               skill.Contains("정지 표지") ||
               skill.Contains("정지장치") ||
               skill.Contains("정지 장치") ||
               skill.Contains("신호설치") ||
               skill.Contains("신호 설치") ||
               skill.Contains("통제신호") ||
               skill.Contains("통제 신호") ||
               skill.Contains("감속함정") ||
               skill.Contains("감속 함정") ||
               skill.Contains("구속함정") ||
               skill.Contains("구속 함정");
    }

    private bool IsFakeBoxSkill(string skill)
    {
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
        return skill.Contains("jokercard") ||
               skill.Contains("joker card") ||
               skill.Contains("조커카드") ||
               skill.Contains("조커 카드");
    }

    private bool IsNoisemakerSkill(string skill)
    {
        return skill.Contains("noisemaker") ||
               skill.Contains("noise") ||
               skill.Contains("소란장치") ||
               skill.Contains("소란 장치") ||
               skill.Contains("소음") ||
               skill.Contains("소란");
    }

    private bool IsHologramSkill(string skill)
    {
        return skill.Contains("hologram") ||
               skill.Contains("홀로그램");
    }

    private bool IsDashSkill(string skill)
    {
        return skill.Contains("dash") ||
               skill.Contains("대시");
    }

    private bool IsSmokeSkill(string skill)
    {
        return skill.Contains("smoke") ||
               skill.Contains("연막");
    }

    protected virtual void CheckDestinationReached()
    {
        if (navAgent == null)
            return;

        if (navAgent.pathPending)
            return;

        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid ||
            navAgent.pathStatus == NavMeshPathStatus.PathPartial)
        {
            CompleteManualMove(false);
            Debug.LogWarning($"[Agent {AgentID}] Manual move failed. Path is not complete.");
            return;
        }

        if (!navAgent.hasPath)
        {
            if (GetPlanarDistanceToNavDestination() <= navAgent.stoppingDistance + 0.2f)
                CompleteManualMove(true);

            return;
        }

        if (float.IsInfinity(navAgent.remainingDistance))
            return;

        if (navAgent.remainingDistance > navAgent.stoppingDistance + 0.15f)
            return;

        if (navAgent.velocity.sqrMagnitude > 0.01f)
            return;

        CompleteManualMove(true);
    }

    private void CompleteManualMove(bool reached)
    {
        isManualMoving = false;

        if (navAgent != null)
            navAgent.ResetPath();

        UpdateAnimationState(true);
        UpdateStateIcon();

        if (reached)
            Debug.Log($"[Agent {AgentID}] Reached manual destination. Switched to idle state.");
    }

    private float GetPlanarDistanceToNavDestination()
    {
        if (navAgent == null)
            return 0f;

        Vector3 current = transform.position;
        Vector3 destination = navAgent.destination;

        current.y = 0f;
        destination.y = 0f;

        return Vector3.Distance(current, destination);
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

        RestoreSharedTargetMoveSpeed();

        currentTarget = null;
        isManualMoving = false;
        isFollowingSharedTargetPosition = false;
        hasLastChaseDestination = false;
        hasLastSharedTargetDestination = false;

        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2.0f, navAgent.areaMask))
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

        RestoreSharedTargetMoveSpeed();

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

    protected void StopLookAroundFromDerived(bool logMessage)
    {
        StopLookAroundInternal(logMessage);
    }

    public virtual void SetChaseTarget(Transform target)
    {
        if (navAgent == null || target == null)
            return;

        if (isLookingAround)
            StopLookAroundInternal(false);

        RestoreSharedTargetMoveSpeed();

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

    public virtual void StopAllMovementForStageResult()
    {
        if (isLookingAround)
            StopLookAroundInternal(false);

        RestoreSharedTargetMoveSpeed();

        currentTarget = null;
        isManualMoving = false;

        hasLastChaseDestination = false;
        chaseRepathTimer = 0f;

        hasSharedTargetPosition = false;
        isFollowingSharedTargetPosition = false;
        sharedTargetReporter = null;
        sharedTargetPositionExpireTime = -1f;
        sharedTargetRepathTimer = 0f;
        sharedTargetMoveSpeedMultiplier = 1f;
        hasLastSharedTargetDestination = false;

        cachedAnimationIsMoving = false;
        lastAnimationMovingTime = -999f;

        ClearSkillCommandBlockers(false);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        UpdateAnimationState(true);
        UpdateStateIcon();
    }

    public virtual void PlayVictoryPose()
    {
        StopAllMovementForStageResult();
    }

    public virtual void PlayDefeatPose()
    {
        StopAllMovementForStageResult();
    }

    public virtual void ClearResultAnimationLock()
    {
    }

    public virtual void ReceiveSharedTargetPosition(Vector3 position, AgentController reporter)
    {
        ReceiveSharedTargetPosition(position, reporter, 1f);
    }

    public virtual void ReceiveSharedTargetPosition(
        Vector3 position,
        AgentController reporter,
        float moveSpeedMultiplier)
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
        sharedTargetMoveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
    }

    public virtual void ClearSharedTargetPosition(AgentController reporter = null)
    {
        if (reporter != null && sharedTargetReporter != null && sharedTargetReporter != reporter)
            return;

        RestoreSharedTargetMoveSpeed();

        hasSharedTargetPosition = false;
        isFollowingSharedTargetPosition = false;
        sharedTargetReporter = null;
        sharedTargetPositionExpireTime = -1f;
        sharedTargetRepathTimer = 0f;
        sharedTargetMoveSpeedMultiplier = 1f;
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
            RestoreSharedTargetMoveSpeed();
            return;
        }

        if (Time.time > sharedTargetPositionExpireTime)
        {
            ClearSharedTargetPosition(sharedTargetReporter);
            return;
        }

        if (navAgent == null)
            return;

        if (currentTarget != null || isManualMoving || isLookingAround || ShouldBlockSharedTargetMovement)
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

        ApplySharedTargetMoveSpeed();

        navAgent.isStopped = false;
        navAgent.SetDestination(hit.position);

        lastSharedTargetDestination = sharedTargetPosition;
        hasLastSharedTargetDestination = true;
        isFollowingSharedTargetPosition = true;
        sharedTargetRepathTimer = SharedTargetRepathInterval;
    }

    private void ApplySharedTargetMoveSpeed()
    {
        if (navAgent == null || stats == null)
            return;

        if (sharedTargetMoveSpeedMultiplier <= 1.0001f)
        {
            RestoreSharedTargetMoveSpeed();
            return;
        }

        navAgent.speed = stats.moveSpeed * sharedTargetMoveSpeedMultiplier;
        sharedTargetMoveSpeedApplied = true;
    }

    private void RestoreSharedTargetMoveSpeed()
    {
        if (!sharedTargetMoveSpeedApplied)
            return;

        ApplyNavAgentStats();
        sharedTargetMoveSpeedApplied = false;
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

        bool isMovingNow = ResolveAnimationIsMoving();

        animator.SetBool(isMovingParameterHash, isMovingNow);

        if (!useMoveSpeedParameter)
            return;

        float speed = Mathf.Max(
            navAgent.velocity.magnitude,
            navAgent.desiredVelocity.magnitude
        );

        float normalizedSpeed;

        if (!isMovingNow)
        {
            normalizedSpeed = 0f;
        }
        else if (stats != null && stats.moveSpeed > 0.01f)
        {
            normalizedSpeed = Mathf.Clamp01(speed / stats.moveSpeed);
            normalizedSpeed = Mathf.Max(normalizedSpeed, minimumMovingNormalizedSpeed);
        }
        else
        {
            normalizedSpeed = Mathf.Clamp01(speed);
            normalizedSpeed = Mathf.Max(normalizedSpeed, minimumMovingNormalizedSpeed);
        }

        if (immediate)
            animator.SetFloat(moveSpeedParameterHash, normalizedSpeed);
        else
            animator.SetFloat(moveSpeedParameterHash, normalizedSpeed, 0.1f, Time.deltaTime);
    }

    private bool ResolveAnimationIsMoving()
    {
        if (navAgent == null)
            return false;

        if (navAgent.isStopped)
        {
            cachedAnimationIsMoving = false;
            return false;
        }

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            currentTarget != null ||
            isFollowingSharedTargetPosition ||
            HasActivePathForAnimation();

        bool hasActualVelocity =
            navAgent.velocity.sqrMagnitude > movingThreshold * movingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > movingThreshold * movingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForAnimation();

        bool shouldMove = hasMovementIntent && (hasActualVelocity || hasNotReachedDestination);

        if (shouldMove)
        {
            cachedAnimationIsMoving = true;
            lastAnimationMovingTime = Time.time;
            return true;
        }

        if (cachedAnimationIsMoving && Time.time - lastAnimationMovingTime <= animationStopDelay)
            return true;

        cachedAnimationIsMoving = false;
        return false;
    }

    private bool HasActivePathForAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForAnimation();
    }

    private bool HasReachedDestinationForAnimation()
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
        return navAgent.remainingDistance <= stopDistance + animationStopDistanceBuffer;
    }

    protected virtual void UpdateStateIcon()
    {
        if (stateIconController == null)
            return;

        bool ignoreDebuffStateIcon = ShouldIgnoreDebuffStateIcon;

        if (!ignoreDebuffStateIcon && IsSmokeDebuffed)
        {
            stateIconController.SetState(AgentStateController.AgentAwarenessState.BlindedBySmoke);
            return;
        }

        if (!ignoreDebuffStateIcon && IsSkillCommandBlocked)
        {
            stateIconController.SetState(AgentStateController.AgentAwarenessState.SkillCommandBlocked);
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

    public virtual void PlayHitReaction(Vector3 hitSourcePosition)
    {
    }
}