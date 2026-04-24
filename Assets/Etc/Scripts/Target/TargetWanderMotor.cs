using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TargetWanderMotor : MonoBehaviour
{
    private const float MinMoveThreshold = 0.001f;

    [Header("References")]
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private TargetEscapeMotor escapeMotor;
    [SerializeField] private TargetController targetController;

    [Header("Safe Wander")]
    [SerializeField] private bool enableSafeWander = true;
    [SerializeField] private float safeTimeBeforeWander = 3f;
    [SerializeField] private float wanderRepathInterval = 3f;
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float wanderMinDistance = 2.5f;
    [SerializeField] private int wanderSampleCount = 12;
    [SerializeField] private float navMeshSampleRadius = 3f;

    [Header("Movement")]
    [SerializeField] private float wanderMoveSpeed = 3.5f;
    [SerializeField] private float wanderAcceleration = 12f;
    [SerializeField] private float wanderAngularSpeed = 360f;
    [SerializeField] private float arriveDistance = 0.4f;

    [Header("Wall Avoidance")]
    [SerializeField] private LayerMask wallLayerMask;
    [SerializeField] private float minEdgeDistanceFromWall = 1.2f;
    [SerializeField] private float edgeDistanceWeight = 3f;
    [SerializeField] private float opennessProbeDistance = 2.5f;
    [SerializeField] private int opennessProbeCount = 6;
    [SerializeField] private float wallCheckHeightOffset = 0.6f;
    [SerializeField] private float wallCheckSphereRadius = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float minOpennessRatio = 0.35f;
    [SerializeField] private float opennessWeight = 4f;

    private NavMeshAgent navAgent;
    private NavMeshPath reusablePath;

    private float safeTimer = 0f;
    private float lastWanderTime = -999f;
    private bool isWandering = false;

    public bool IsWandering => isWandering;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (targetController == null)
            targetController = GetComponent<TargetController>();

        reusablePath = new NavMeshPath();
    }

    private void OnDisable()
    {
        StopWandering(true);
    }

    public void TickSafeWander(bool hasThreat)
    {
        if (!enableSafeWander)
        {
            StopWandering(false);
            return;
        }

        if (!CanWander(hasThreat))
        {
            StopWandering(hasThreat);
            return;
        }

        safeTimer += Time.deltaTime;

        if (safeTimer < safeTimeBeforeWander)
            return;

        ApplyWanderMovementSettings();

        if (navAgent.pathPending)
            return;

        if (IsMovingToValidWanderDestination())
            return;

        if (Time.time - lastWanderTime < wanderRepathInterval)
            return;

        if (!TryFindWanderPosition(out Vector3 wanderPosition))
            return;

        if (!navAgent.SetDestination(wanderPosition))
            return;

        isWandering = true;
        lastWanderTime = Time.time;
    }

    public void StopWandering(bool clearPath)
    {
        bool wasWandering = isWandering;

        safeTimer = 0f;
        isWandering = false;

        if (navAgent != null &&
            clearPath &&
            wasWandering &&
            navAgent.isActiveAndEnabled &&
            navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }

        if (escapeMotor != null)
            escapeMotor.ApplyNavAgentBaseSettings();
    }

    private bool CanWander(bool hasThreat)
    {
        if (navAgent == null)
            return false;

        if (!navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return false;

        if (hasThreat)
            return false;

        if (navAgent.isStopped)
            return false;

        if (targetController != null && targetController.MaxHealth > 0.01f && targetController.CurrentHealth <= 0.01f)
            return false;

        if (escapeMotor != null)
        {
            if (escapeMotor.IsRooted)
                return false;

            if (escapeMotor.IsEmergencyEscaping)
                return false;
        }

        return true;
    }

    private void ApplyWanderMovementSettings()
    {
        navAgent.speed = wanderMoveSpeed;
        navAgent.acceleration = wanderAcceleration;
        navAgent.angularSpeed = wanderAngularSpeed;
        navAgent.autoBraking = true;
    }

    private bool IsMovingToValidWanderDestination()
    {
        if (!navAgent.hasPath)
            return false;

        if (navAgent.remainingDistance <= navAgent.stoppingDistance + arriveDistance)
            return false;

        if (navAgent.velocity.sqrMagnitude <= 0.01f && !navAgent.pathPending)
            return false;

        return true;
    }

    private bool TryFindWanderPosition(out Vector3 bestPosition)
    {
        bestPosition = transform.position;

        float bestScore = float.MinValue;
        bool found = false;

        for (int i = 0; i < wanderSampleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float distance = Random.Range(wanderMinDistance, wanderRadius);

            Vector3 rawPosition = transform.position + new Vector3(
                randomCircle.x * distance,
                0f,
                randomCircle.y * distance
            );

            if (!NavMesh.SamplePosition(rawPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 candidate = hit.position;

            float candidateDistance = Vector3.Distance(transform.position, candidate);
            if (candidateDistance < wanderMinDistance)
                continue;

            if (!TryCalculatePath(candidate))
                continue;

            float edgeScore = GetEdgeDistanceScore(candidate, out bool tooCloseToWall);
            if (tooCloseToWall)
                continue;

            Vector3 moveDirection = candidate - transform.position;
            moveDirection.y = 0f;

            if (moveDirection.sqrMagnitude <= MinMoveThreshold)
                continue;

            moveDirection.Normalize();

            float opennessScore = GetOpennessScore(candidate, moveDirection, out bool tooClosed);
            if (tooClosed)
                continue;

            float score =
                candidateDistance +
                edgeScore * edgeDistanceWeight +
                opennessScore * opennessWeight +
                Random.value;

            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool TryCalculatePath(Vector3 destination)
    {
        if (navAgent == null)
            return false;

        if (reusablePath == null)
            reusablePath = new NavMeshPath();

        if (!navAgent.CalculatePath(destination, reusablePath))
            return false;

        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    private float GetEdgeDistanceScore(Vector3 candidate, out bool tooCloseToWall)
    {
        tooCloseToWall = false;

        if (minEdgeDistanceFromWall <= 0f)
            return 1f;

        if (!NavMesh.FindClosestEdge(candidate, out NavMeshHit edgeHit, NavMesh.AllAreas))
            return 1f;

        float edgeDistance = Vector3.Distance(candidate, edgeHit.position);

        if (edgeDistance < minEdgeDistanceFromWall)
            tooCloseToWall = true;

        return Mathf.InverseLerp(0f, minEdgeDistanceFromWall + 3f, edgeDistance);
    }

    private float GetOpennessScore(Vector3 candidate, Vector3 moveDirection, out bool tooClosed)
    {
        tooClosed = false;

        if (opennessProbeCount <= 0 || opennessProbeDistance <= 0f)
            return 1f;

        Vector3 origin = candidate + Vector3.up * wallCheckHeightOffset;

        int clearCount = 0;
        int totalCount = opennessProbeCount;

        for (int i = 0; i < totalCount; i++)
        {
            float angle = totalCount == 1
                ? 0f
                : Mathf.Lerp(-70f, 70f, (float)i / (totalCount - 1));

            Vector3 probeDirection = Quaternion.Euler(0f, angle, 0f) * moveDirection;
            probeDirection.y = 0f;

            if (probeDirection.sqrMagnitude <= MinMoveThreshold)
                continue;

            probeDirection.Normalize();

            bool blocked = Physics.SphereCast(
                origin,
                wallCheckSphereRadius,
                probeDirection,
                out _,
                opennessProbeDistance,
                wallLayerMask,
                QueryTriggerInteraction.Ignore
            );

            if (!blocked)
                clearCount++;
        }

        float opennessRatio = totalCount > 0 ? (float)clearCount / totalCount : 1f;

        if (opennessRatio < minOpennessRatio)
            tooClosed = true;

        return opennessRatio;
    }

    public void BeginSafeDelay(bool clearCurrentPath)
    {
        safeTimer = 0f;
        lastWanderTime = -999f;
        isWandering = false;

        if (navAgent != null &&
            clearCurrentPath &&
            navAgent.isActiveAndEnabled &&
            navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.isStopped = false;
        }

        if (escapeMotor != null)
            escapeMotor.ApplyNavAgentBaseSettings();
    }
}