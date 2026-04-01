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

    [Header("State")]
    protected NavMeshAgent navAgent;
    protected Transform currentTarget;
    protected bool isManualMoving = false;

    [SerializeField] private float chaseRepathInterval = 0.15f;
    [SerializeField] private float chaseDestinationThreshold = 0.25f;

    private float chaseRepathTimer = 0f;
    private Vector3 lastChaseDestination;
    private bool hasLastChaseDestination = false;

    public int AgentID => agentID;
    public Transform CurrentTarget => currentTarget;
    public bool IsManualMoving => isManualMoving;
    public bool IsChasing => currentTarget != null;
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

        ApplyCommonStats();
    }

    protected virtual void ApplyCommonStats()
    {
        if (stats == null)
        {
            Debug.LogWarning($"[Agent {agentID}] AgentStatsSO가 연결되지 않았습니다.");
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

        if (isManualMoving)
        {
            CheckDestinationReached();
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
                navAgent.SetDestination(targetPos);
                lastChaseDestination = targetPos;
                hasLastChaseDestination = true;
                chaseRepathTimer = chaseRepathInterval;
            }
        }
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
        Debug.Log($"[Agent {AgentID}] MoveTo 호출됨. destination={destination}, currentTarget={(currentTarget != null ? currentTarget.name : "null")}");

        if (navAgent == null)
            return;

        // 추격 중이면 수동 이동 명령을 무시한다.
        if (currentTarget != null)
        {
            Debug.Log($"[Agent {AgentID}] 현재 추격 중이므로 MoveTo를 무시합니다.");
            return;
        }

        currentTarget = null;
        isManualMoving = true;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, 2.0f, NavMesh.AllAreas))
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(hit.position);
            Debug.Log($"[Agent {AgentID}] Manual move started: {hit.position}");
        }
        else
        {
            Debug.LogWarning($"[Agent {AgentID}] Failed to find valid NavMesh position near {destination}");
        }
    }

    public virtual void SetChaseTarget(Transform target)
    {
        if (navAgent == null || target == null)
            return;

        currentTarget = target;
        isManualMoving = false;
        navAgent.isStopped = false;

        hasLastChaseDestination = false;
        chaseRepathTimer = 0f;

        navAgent.SetDestination(currentTarget.position);

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

        Debug.Log($"[Agent {AgentID}] Chase target cleared: {target.name}");
    }

    public virtual void StopChase()
    {
        currentTarget = null;

        if (!isManualMoving && navAgent != null)
        {
            navAgent.ResetPath();
        }

        Debug.Log($"[Agent {AgentID}] Chase stopped.");
    }

    public virtual void ReapplyStats()
    {
        ApplyCommonStats();
    }
}