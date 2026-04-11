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

    [Header("Animation")]
    [SerializeField] protected Animator animator;
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private bool useMoveSpeedParameter = true;
    [SerializeField] private float movingThreshold = 0.05f;

    private float lookAroundAngle = 70f;
    private float lookAroundTurnSpeed = 240f;
    private float lookAroundPauseSeconds = 0.08f;

    // AgentController.cs 에 추가

    [SerializeField] protected ChatServiceOpenAI commandChatService;
    [SerializeField, TextArea(6, 20)] private string commandSystemPromptOverride;

    public ChatServiceOpenAI CommandChatService => commandChatService;
    public string CommandSystemPromptOverride => commandSystemPromptOverride;

    [Header("State")]
    protected NavMeshAgent navAgent;
    protected Transform currentTarget;
    protected bool isManualMoving = false;

    [SerializeField] private float chaseRepathInterval = 0.15f;
    [SerializeField] private float chaseDestinationThreshold = 0.25f;

    private float chaseRepathTimer = 0f;
    private Vector3 lastChaseDestination;
    private bool hasLastChaseDestination = false;

    private int isMovingParameterHash;
    private int moveSpeedParameterHash;

    private Coroutine lookAroundRoutine;
    private bool isLookingAround = false;
    private bool previousNavUpdateRotation = true;

    public int AgentID => agentID;
    public Transform CurrentTarget => currentTarget;
    public bool IsManualMoving => isManualMoving;
    public bool IsChasing => currentTarget != null;
    public bool IsLookingAround => isLookingAround;
    public LayerMask TargetLayer => targetLayer;
    public AgentStatsSO Stats => stats;

    public abstract void ExecuteSkill(string skillName, Vector3 targetPos);

    protected virtual void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

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

        CacheAnimatorParameterHashes();
        ApplyCommonStats();
        UpdateAnimationState(true);
    }

    protected virtual void OnDisable()
    {
        StopLookAroundInternal(false);
    }

    protected virtual void OnValidate()
    {
        CacheAnimatorParameterHashes();
    }

    protected virtual void ApplyCommonStats()
    {
        if (stats == null)
        {
            Debug.LogWarning($"[Agent {agentID}] AgentStatsSO�� ������� �ʾҽ��ϴ�.");
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

        if (isLookingAround)
        {
            UpdateAnimationState();
            return;
        }

        if (isManualMoving)
        {
            CheckDestinationReached();
            UpdateAnimationState();
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

        UpdateAnimationState();
    }

    protected virtual void CheckDestinationReached()
    {
        if (navAgent.pathPending)
            return;

        if (navAgent.remainingDistance <= navAgent.stoppingDistance)
        {
            if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude <= 0.01f)
            {
                isManualMoving = false;
                navAgent.ResetPath();
                UpdateAnimationState(true);
                Debug.Log($"[Agent {AgentID}] Reached manual destination. Switched to idle state.");
            }
        }
    }

    public void SetAgentID(int id)
    {
        agentID = id;
    }

    public virtual void MoveTo(Vector3 destination)
    {
        Debug.Log($"[Agent {AgentID}] MoveTo ȣ���. destination={destination}, currentTarget={(currentTarget != null ? currentTarget.name : "null")}");

        if (navAgent == null)
            return;

        if (isLookingAround)
            StopLookAroundInternal(false);

        if (currentTarget != null)
        {
            Debug.Log($"[Agent {AgentID}] ���� �߰� ���̹Ƿ� MoveTo�� �����մϴ�.");
            return;
        }

        currentTarget = null;
        isManualMoving = true;
        hasLastChaseDestination = false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, 2.0f, NavMesh.AllAreas))
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(hit.position);
            UpdateAnimationState(true);
            Debug.Log($"[Agent {AgentID}] Manual move started: {hit.position}");
        }
        else
        {
            isManualMoving = false;
            UpdateAnimationState(true);
            Debug.LogWarning($"[Agent {AgentID}] Failed to find valid NavMesh position near {destination}");
        }
    }

    public bool TryStartLookAround()
    {
        if (navAgent == null)
            return false;

        if (currentTarget != null)
        {
            Debug.LogWarning($"[Agent {AgentID}] ���� �߰� ���̹Ƿ� �ֺ� �ѷ����⸦ ������ �� �����ϴ�.");
            return false;
        }

        if (isManualMoving)
        {
            Debug.LogWarning($"[Agent {AgentID}] ���� �̵� ���̹Ƿ� �ֺ� �ѷ����⸦ ������ �� �����ϴ�.");
            return false;
        }

        StopLookAroundInternal(false);
        lookAroundRoutine = StartCoroutine(LookAroundCoroutine());
        return true;
    }

    private IEnumerator LookAroundCoroutine()
    {
        isLookingAround = true;

        if (navAgent != null)
        {
            navAgent.ResetPath();
            navAgent.isStopped = true;
            previousNavUpdateRotation = navAgent.updateRotation;
            navAgent.updateRotation = false;
        }

        UpdateAnimationState(true);
        Debug.Log($"[Agent {AgentID}] �ֺ� �ѷ����� ����");

        float startY = transform.eulerAngles.y;

        yield return RotateToYaw(startY - lookAroundAngle);

        if (lookAroundPauseSeconds > 0f)
            yield return new WaitForSeconds(lookAroundPauseSeconds);

        yield return RotateToYaw(startY + lookAroundAngle);

        if (lookAroundPauseSeconds > 0f)
            yield return new WaitForSeconds(lookAroundPauseSeconds);

        yield return RotateToYaw(startY);

        FinishLookAround();
        Debug.Log($"[Agent {AgentID}] �ֺ� �ѷ����� ����");
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

        if (logMessage)
            Debug.Log($"[Agent {AgentID}] �ֺ� �ѷ����⸦ �ߴ��߽��ϴ�.");
    }

    public virtual void SetChaseTarget(Transform target)
    {
        if (navAgent == null || target == null)
            return;

        if (isLookingAround)
            StopLookAroundInternal(false);

        currentTarget = target;
        isManualMoving = false;
        navAgent.isStopped = false;

        hasLastChaseDestination = false;
        chaseRepathTimer = 0f;

        navAgent.SetDestination(currentTarget.position);
        UpdateAnimationState(true);

        Debug.Log($"[Agent {AgentID}] Chase target assigned: {target.name}");
    }

    public virtual void ClearChaseTarget(Transform target)
    {
        if (target == null)
            return;

        if (currentTarget != target)
            return;

        currentTarget = null;

        if (!isManualMoving && navAgent != null)
        {
            navAgent.ResetPath();
        }

        UpdateAnimationState(true);
        Debug.Log($"[Agent {AgentID}] Chase target cleared: {target.name}");
    }

    public virtual void StopChase()
    {
        currentTarget = null;

        if (!isManualMoving && navAgent != null)
        {
            navAgent.ResetPath();
        }

        UpdateAnimationState(true);
        Debug.Log($"[Agent {AgentID}] Chase stopped.");
    }

    public virtual void ReapplyStats()
    {
        ApplyCommonStats();
    }

    protected virtual void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        float speed = navAgent.velocity.magnitude;
        bool isMovingNow = !navAgent.isStopped &&
                           (navAgent.pathPending || navAgent.hasPath || isManualMoving || currentTarget != null) &&
                           speed > movingThreshold;

        animator.SetBool(isMovingParameterHash, isMovingNow);

        if (useMoveSpeedParameter)
        {
            float normalizedSpeed = 0f;

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