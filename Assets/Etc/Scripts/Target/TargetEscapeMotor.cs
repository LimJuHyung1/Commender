using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class EscapeSettings
{
    [Header("Flee Movement")]
    public float repathCooldown = 0.35f;
    public float fleeMoveSpeed = 15f;
    public float fleeAngularSpeed = 1080f;
    public float fleeAcceleration = 45f;
    public float minNearestThreatDistanceGain = 0.5f;
    [Range(-1f, 1f)] public float minPathStartAlignment = -0.25f;

    [Header("Safe Point Search")]
    public float safeSearchRadius = 24f;
    public int safePointSampleCount = 5;
    public float safePointMinDistance = 9f;
    public float navMeshSampleRadius = 4f;
    public float fleeDirectionBias = 2f;
    [Range(0f, 1f)] public float directionalSampleRatio = 1f;
    public float directionalSampleSpreadAngle = 70f;

    [Header("Dead End Avoidance")]
    public float minEdgeDistanceFromWall = 1.5f;
    public float edgeDistanceWeight = 3f;
    public float opennessProbeDistance = 3f;
    public int opennessProbeCount = 5;
    [Range(0f, 1f)] public float minOpennessRatio = 0.3f;
    public float opennessWeight = 5f;

    [Header("Relaxed Fallback")]
    public float relaxedEdgePenaltyMultiplier = 0.35f;
    public float relaxedOpennessPenaltyMultiplier = 0.4f;
    [Range(-1f, 1f)] public float relaxedMinPathStartAlignment = -0.55f;

    [Header("Emergency Local Reposition")]
    public float emergencyLocalSearchRadius = 5f;
    public int emergencyLocalSampleCount = 12;

    [Header("Panic Boost")]
    public bool usePanicBoost = false;
    [Range(0f, 1f)] public float panicHealthThresholdRatio = 0.45f;
    public float panicSpeedMultiplier = 1.2f;
    public float panicSearchRadiusMultiplier = 1.2f;
    public int panicSampleCountBonus = 0;
    public float panicFleeDirectionBiasBonus = 0f;

    [Header("Emergency Escape")]
    public bool enableEmergencyEscape = false;
    public int emergencyEscapeCharges = 0;
    public bool autoUseEmergencyEscape = false;
    [Range(0f, 1f)] public float emergencyEscapeAutoTriggerHealthRatio = 0.35f;
    public float emergencyEscapeAutoTriggerThreatDistance = 6f;
    public float emergencyEscapeDuration = 0.8f;
    public float emergencyEscapeSpeed = 22f;
    public float emergencyEscapeAcceleration = 30f;
    public float emergencyEscapeRepathInterval = 0.2f;

    public EscapeSettings Clone()
    {
        return new EscapeSettings
        {
            repathCooldown = repathCooldown,
            fleeMoveSpeed = fleeMoveSpeed,
            fleeAngularSpeed = fleeAngularSpeed,
            fleeAcceleration = fleeAcceleration,
            minNearestThreatDistanceGain = minNearestThreatDistanceGain,
            minPathStartAlignment = minPathStartAlignment,

            safeSearchRadius = safeSearchRadius,
            safePointSampleCount = safePointSampleCount,
            safePointMinDistance = safePointMinDistance,
            navMeshSampleRadius = navMeshSampleRadius,
            fleeDirectionBias = fleeDirectionBias,
            directionalSampleRatio = directionalSampleRatio,
            directionalSampleSpreadAngle = directionalSampleSpreadAngle,

            minEdgeDistanceFromWall = minEdgeDistanceFromWall,
            edgeDistanceWeight = edgeDistanceWeight,
            opennessProbeDistance = opennessProbeDistance,
            opennessProbeCount = opennessProbeCount,
            minOpennessRatio = minOpennessRatio,
            opennessWeight = opennessWeight,

            relaxedEdgePenaltyMultiplier = relaxedEdgePenaltyMultiplier,
            relaxedOpennessPenaltyMultiplier = relaxedOpennessPenaltyMultiplier,
            relaxedMinPathStartAlignment = relaxedMinPathStartAlignment,

            emergencyLocalSearchRadius = emergencyLocalSearchRadius,
            emergencyLocalSampleCount = emergencyLocalSampleCount,

            usePanicBoost = usePanicBoost,
            panicHealthThresholdRatio = panicHealthThresholdRatio,
            panicSpeedMultiplier = panicSpeedMultiplier,
            panicSearchRadiusMultiplier = panicSearchRadiusMultiplier,
            panicSampleCountBonus = panicSampleCountBonus,
            panicFleeDirectionBiasBonus = panicFleeDirectionBiasBonus,

            enableEmergencyEscape = enableEmergencyEscape,
            emergencyEscapeCharges = emergencyEscapeCharges,
            autoUseEmergencyEscape = autoUseEmergencyEscape,
            emergencyEscapeAutoTriggerHealthRatio = emergencyEscapeAutoTriggerHealthRatio,
            emergencyEscapeAutoTriggerThreatDistance = emergencyEscapeAutoTriggerThreatDistance,
            emergencyEscapeDuration = emergencyEscapeDuration,
            emergencyEscapeSpeed = emergencyEscapeSpeed,
            emergencyEscapeAcceleration = emergencyEscapeAcceleration,
            emergencyEscapeRepathInterval = emergencyEscapeRepathInterval
        };
    }

    public void ClampValues()
    {
        repathCooldown = Mathf.Max(0.05f, repathCooldown);
        fleeMoveSpeed = Mathf.Max(0f, fleeMoveSpeed);
        fleeAngularSpeed = Mathf.Max(0f, fleeAngularSpeed);
        fleeAcceleration = Mathf.Max(0f, fleeAcceleration);
        minNearestThreatDistanceGain = Mathf.Max(0f, minNearestThreatDistanceGain);
        minPathStartAlignment = Mathf.Clamp(minPathStartAlignment, -1f, 1f);

        safeSearchRadius = Mathf.Max(1f, safeSearchRadius);
        safePointSampleCount = Mathf.Clamp(safePointSampleCount, 3, 16);
        safePointMinDistance = Mathf.Max(0.5f, safePointMinDistance);
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        fleeDirectionBias = Mathf.Max(0f, fleeDirectionBias);
        directionalSampleRatio = Mathf.Clamp01(directionalSampleRatio);
        directionalSampleSpreadAngle = Mathf.Max(0f, directionalSampleSpreadAngle);

        minEdgeDistanceFromWall = Mathf.Max(0f, minEdgeDistanceFromWall);
        edgeDistanceWeight = Mathf.Max(0f, edgeDistanceWeight);
        opennessProbeDistance = Mathf.Max(0f, opennessProbeDistance);
        opennessProbeCount = Mathf.Clamp(opennessProbeCount, 0, 12);
        minOpennessRatio = Mathf.Clamp01(minOpennessRatio);
        opennessWeight = Mathf.Max(0f, opennessWeight);

        relaxedEdgePenaltyMultiplier = Mathf.Max(0f, relaxedEdgePenaltyMultiplier);
        relaxedOpennessPenaltyMultiplier = Mathf.Max(0f, relaxedOpennessPenaltyMultiplier);
        relaxedMinPathStartAlignment = Mathf.Clamp(relaxedMinPathStartAlignment, -1f, 1f);

        emergencyLocalSearchRadius = Mathf.Max(1f, emergencyLocalSearchRadius);
        emergencyLocalSampleCount = Mathf.Clamp(emergencyLocalSampleCount, 4, 32);

        panicHealthThresholdRatio = Mathf.Clamp01(panicHealthThresholdRatio);
        panicSpeedMultiplier = Mathf.Max(1f, panicSpeedMultiplier);
        panicSearchRadiusMultiplier = Mathf.Max(1f, panicSearchRadiusMultiplier);
        panicSampleCountBonus = Mathf.Max(0, panicSampleCountBonus);
        panicFleeDirectionBiasBonus = Mathf.Max(0f, panicFleeDirectionBiasBonus);

        emergencyEscapeCharges = Mathf.Max(0, emergencyEscapeCharges);
        emergencyEscapeAutoTriggerHealthRatio = Mathf.Clamp01(emergencyEscapeAutoTriggerHealthRatio);
        emergencyEscapeAutoTriggerThreatDistance = Mathf.Max(0f, emergencyEscapeAutoTriggerThreatDistance);
        emergencyEscapeDuration = Mathf.Max(0.01f, emergencyEscapeDuration);
        emergencyEscapeSpeed = Mathf.Max(0f, emergencyEscapeSpeed);
        emergencyEscapeAcceleration = Mathf.Max(0f, emergencyEscapeAcceleration);
        emergencyEscapeRepathInterval = Mathf.Max(0.01f, emergencyEscapeRepathInterval);
    }
}

[RequireComponent(typeof(NavMeshAgent))]
public class TargetEscapeMotor : MonoBehaviour
{
    private const float MinMoveThreshold = 0.001f;
    private const float ArriveEpsilon = 0.05f;

    private static readonly float[] EscapeAngles =
    {
        0f,
        20f,
        -20f,
        40f,
        -40f,
        65f,
        -65f,
        90f,
        -90f,
        120f,
        -120f,
        150f,
        -150f
    };

    private static readonly float[] EscapeDistanceScales =
    {
        1f,
        0.85f,
        0.7f,
        0.55f
    };

    [Header("References")]
    public TargetThreatTracker threatTracker;
    public TargetSkillController skillController;

    [Header("Escape Settings")]
    public EscapeSettings settings = new EscapeSettings();

    [Header("Path Commitment")]
    public float destinationCommitTime = 1.6f;
    public float repathNearDestinationDistance = 2.5f;
    public float stuckVelocityThreshold = 0.08f;
    public float stuckDurationBeforeRepath = 0.7f;
    public float badPathAlignmentThreshold = -0.45f;

    [Header("Wall Check")]
    public LayerMask wallLayerMask;
    public float wallCheckHeightOffset = 0.6f;
    public float wallCheckSphereRadius = 0.2f;

    private NavMeshAgent navAgent;
    private NavMeshPath reusablePath;

    private Coroutine rootRoutine;
    private Coroutine slowRoutine;
    private Coroutine emergencyEscapeRoutine;

    private Vector3 currentDestination;
    private float currentDestinationSetTime = -999f;
    private float lastRepathTime = -999f;
    private float lastKnownHealthRatio = 1f;
    private float activeSlowMultiplier = 1f;
    private float stuckTimer = 0f;

    private int emergencyEscapeUsedCount;

    private bool hasCurrentDestination;
    private bool isRooted;
    private bool isSlowed;
    private bool isEmergencyEscaping;

    private struct EscapeCandidate
    {
        public Vector3 position;
        public float score;
        public float threatGain;
        public float pathStartAlignment;
        public float pathLength;
    }

    public bool IsRooted => isRooted;
    public bool IsSlowed => isSlowed;
    public bool IsEmergencyEscaping => isEmergencyEscaping;
    public bool CanBeCaught => !isEmergencyEscaping;
    public bool HasUsedEmergencyEscape => emergencyEscapeUsedCount > 0;
    public int RemainingEmergencyEscapeCount => Mathf.Max(0, settings.emergencyEscapeCharges - emergencyEscapeUsedCount);

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (skillController == null)
            skillController = GetComponent<TargetSkillController>();

        reusablePath = new NavMeshPath();

        ClampLocalValues();
        settings.ClampValues();
        ApplyNavAgentBaseSettings();
    }

    private void OnValidate()
    {
        if (settings == null)
            settings = new EscapeSettings();

        ClampLocalValues();
        settings.ClampValues();
    }

    private void OnDisable()
    {
        StopActiveCoroutines();
        ResetMotionState();
    }

    public void SetThreatTracker(TargetThreatTracker tracker)
    {
        threatTracker = tracker;
    }

    public void SetSkillController(TargetSkillController controller)
    {
        skillController = controller;
    }

    public void ApplyNavAgentBaseSettings()
    {
        if (navAgent == null)
            return;

        navAgent.speed = settings.fleeMoveSpeed;
        navAgent.acceleration = settings.fleeAcceleration;
        navAgent.angularSpeed = settings.fleeAngularSpeed;
        navAgent.autoBraking = false;
        navAgent.autoRepath = true;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    public void RefreshDynamicMovementSettings(bool hasThreat, float healthRatio)
    {
        if (navAgent == null || isEmergencyEscaping)
            return;

        lastKnownHealthRatio = Mathf.Clamp01(healthRatio);

        float speed = settings.fleeMoveSpeed;
        float acceleration = settings.fleeAcceleration;

        if (IsPanicBoostActive(hasThreat))
        {
            speed *= settings.panicSpeedMultiplier;
            acceleration *= settings.panicSpeedMultiplier;
        }

        speed *= activeSlowMultiplier;
        acceleration *= activeSlowMultiplier;

        navAgent.speed = speed;
        navAgent.acceleration = acceleration;
        navAgent.angularSpeed = settings.fleeAngularSpeed;
        navAgent.autoBraking = false;
    }

    public void ConfigureEmergencyEscape(bool enabled, int charges, bool autoUse)
    {
        settings.enableEmergencyEscape = enabled;
        settings.emergencyEscapeCharges = enabled ? Mathf.Max(0, charges) : 0;
        settings.autoUseEmergencyEscape = enabled && autoUse;
        settings.ClampValues();

        emergencyEscapeUsedCount = 0;

        if (!enabled && emergencyEscapeRoutine != null)
        {
            StopCoroutine(emergencyEscapeRoutine);
            emergencyEscapeRoutine = null;
            isEmergencyEscaping = false;
            ApplyNavAgentBaseSettings();
        }
    }

    public bool ShouldAutoTriggerEmergencyEscape(float healthRatio)
    {
        lastKnownHealthRatio = Mathf.Clamp01(healthRatio);

        if (!settings.autoUseEmergencyEscape)
            return false;

        if (!CanUseEmergencyEscape(false))
            return false;

        if (lastKnownHealthRatio > settings.emergencyEscapeAutoTriggerHealthRatio)
            return false;

        if (threatTracker == null)
            return false;

        return threatTracker.GetNearestRealAgentDistance() <= settings.emergencyEscapeAutoTriggerThreatDistance;
    }

    public bool TryActivateEmergencyEscape()
    {
        if (!CanUseEmergencyEscape(true))
            return false;

        emergencyEscapeUsedCount++;

        if (emergencyEscapeRoutine != null)
            StopCoroutine(emergencyEscapeRoutine);

        emergencyEscapeRoutine = StartCoroutine(EmergencyEscapeRoutine());
        return true;
    }

    public void ApplyRoot(float duration)
    {
        if (rootRoutine != null)
            StopCoroutine(rootRoutine);

        rootRoutine = StartCoroutine(RootRoutine(duration));
    }

    public void ApplySlow(float multiplier, float duration)
    {
        activeSlowMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        isSlowed = true;

        if (slowRoutine != null)
            StopCoroutine(slowRoutine);

        bool hasThreat = threatTracker != null && threatTracker.HasAnyThreat();
        RefreshDynamicMovementSettings(hasThreat, lastKnownHealthRatio);

        slowRoutine = StartCoroutine(SlowRoutine(duration));
    }

    public void TryFleeFromThreats(bool forceRepath = false)
    {
        if (!CanFlee())
            return;

        if (!threatTracker.HasAnyThreat())
            return;

        if (isRooted)
            return;

        if (navAgent.isStopped)
            navAgent.isStopped = false;

        navAgent.autoBraking = false;

        bool shouldChooseNewDestination = forceRepath || ShouldChooseNewDestination();

        if (!shouldChooseNewDestination)
            return;

        if (!forceRepath && Time.time - lastRepathTime < settings.repathCooldown)
            return;

        if (!TryFindEscapeDestination(out Vector3 escapeDestination))
        {
            if (!TryFindEmergencyLocalReposition(out escapeDestination))
                return;
        }

        if (!navAgent.SetDestination(escapeDestination))
            return;

        currentDestination = escapeDestination;
        hasCurrentDestination = true;
        currentDestinationSetTime = Time.time;
        lastRepathTime = Time.time;
        stuckTimer = 0f;

        if (skillController != null)
            skillController.TryUseAutoDefensiveSkill(escapeDestination);
    }

    public bool IsCompletelyStopped()
    {
        if (navAgent == null)
            return true;

        bool noPathOrArrived =
            !navAgent.pathPending &&
            (!navAgent.hasPath || navAgent.remainingDistance <= navAgent.stoppingDistance + ArriveEpsilon);

        bool almostNotMoving = navAgent.velocity.sqrMagnitude <= 0.01f;
        return noPathOrArrived && almostNotMoving;
    }

    public void ResetRuntimeState(bool resetEmergencyEscapeUsage = true)
    {
        StopActiveCoroutines();

        isRooted = false;
        isSlowed = false;
        isEmergencyEscaping = false;
        activeSlowMultiplier = 1f;
        lastRepathTime = -999f;
        lastKnownHealthRatio = 1f;
        currentDestinationSetTime = -999f;
        stuckTimer = 0f;
        hasCurrentDestination = false;

        if (resetEmergencyEscapeUsage)
            emergencyEscapeUsedCount = 0;

        if (skillController != null)
            skillController.ResetRuntimeState(true, true);

        ResetAgentPath();
        ApplyNavAgentBaseSettings();
    }

    public void ApplySettings(EscapeSettings newSettings)
    {
        if (newSettings == null)
            return;

        settings = newSettings.Clone();
        settings.ClampValues();
        ApplyNavAgentBaseSettings();
    }

    private bool ShouldChooseNewDestination()
    {
        if (navAgent == null)
            return false;

        if (navAgent.pathPending)
            return false;

        if (!navAgent.hasPath)
            return true;

        if (navAgent.pathStatus != NavMeshPathStatus.PathComplete)
            return true;

        if (!hasCurrentDestination)
            return true;

        if (navAgent.remainingDistance <= navAgent.stoppingDistance + repathNearDestinationDistance)
            return true;

        bool isMoving = navAgent.velocity.sqrMagnitude >= stuckVelocityThreshold * stuckVelocityThreshold;

        if (isMoving)
        {
            stuckTimer = 0f;
        }
        else
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckDurationBeforeRepath)
                return true;
        }

        float destinationAge = Time.time - currentDestinationSetTime;

        if (destinationAge < destinationCommitTime)
            return false;

        if (IsCurrentPathClearlyBad())
            return true;

        if (IsCurrentDestinationNowDangerous())
            return true;

        return false;
    }

    private bool IsCurrentPathClearlyBad()
    {
        if (navAgent == null || threatTracker == null)
            return false;

        Vector3 fleeDirection = GetStableFleeDirection();
        Vector3 moveDirection = navAgent.steeringTarget - transform.position;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= MinMoveThreshold)
            moveDirection = currentDestination - transform.position;

        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= MinMoveThreshold)
            return false;

        float alignment = Vector3.Dot(moveDirection.normalized, fleeDirection.normalized);
        return alignment < badPathAlignmentThreshold;
    }

    private bool IsCurrentDestinationNowDangerous()
    {
        if (threatTracker == null)
            return false;

        float searchRadius = GetEffectiveSearchRadius(true);
        float currentThreatDistance = threatTracker.GetNearestThreatDistance(transform.position, searchRadius * 2f);
        float destinationThreatDistance = threatTracker.GetNearestThreatDistance(currentDestination, searchRadius * 2f);

        return destinationThreatDistance + 0.25f < currentThreatDistance;
    }

    private bool TryFindEscapeDestination(out Vector3 bestPosition)
    {
        bestPosition = transform.position;

        if (navAgent == null || threatTracker == null)
            return false;

        Vector3 fleeDirection = GetStableFleeDirection();

        float searchRadius = GetEffectiveSearchRadius(true);
        float currentThreatDistance = threatTracker.GetNearestThreatDistance(transform.position, searchRadius * 2f);

        EscapeCandidate bestStrict = default;
        EscapeCandidate bestRelaxed = default;

        bestStrict.score = float.MinValue;
        bestRelaxed.score = float.MinValue;

        bool foundStrict = false;
        bool foundRelaxed = false;

        for (int d = 0; d < EscapeDistanceScales.Length; d++)
        {
            float distance = Mathf.Max(
                settings.safePointMinDistance,
                searchRadius * EscapeDistanceScales[d]
            );

            for (int a = 0; a < EscapeAngles.Length; a++)
            {
                Vector3 direction = Quaternion.Euler(0f, EscapeAngles[a], 0f) * fleeDirection;
                direction.y = 0f;

                if (direction.sqrMagnitude <= MinMoveThreshold)
                    continue;

                direction.Normalize();

                Vector3 rawCandidate = transform.position + direction * distance;

                if (!TryProjectToNavMesh(rawCandidate, out Vector3 candidate))
                    continue;

                if (!TryCalculateCompletePath(candidate))
                    continue;

                EscapeCandidate evaluated = EvaluateCandidate(
                    candidate,
                    fleeDirection,
                    currentThreatDistance,
                    searchRadius
                );

                bool strictValid =
                    evaluated.threatGain >= -0.1f &&
                    evaluated.pathStartAlignment >= settings.minPathStartAlignment &&
                    IsCandidateOpenEnough(candidate, fleeDirection, true);

                if (strictValid)
                {
                    if (evaluated.score > bestStrict.score)
                    {
                        bestStrict = evaluated;
                        foundStrict = true;
                    }
                }

                bool relaxedValid =
                    evaluated.pathStartAlignment >= settings.relaxedMinPathStartAlignment;

                if (relaxedValid)
                {
                    float relaxedScore = evaluated.score;
                    relaxedScore += evaluated.threatGain * 3f;

                    evaluated.score = relaxedScore;

                    if (evaluated.score > bestRelaxed.score)
                    {
                        bestRelaxed = evaluated;
                        foundRelaxed = true;
                    }
                }
            }
        }

        if (foundStrict)
        {
            bestPosition = bestStrict.position;
            return true;
        }

        if (foundRelaxed)
        {
            bestPosition = bestRelaxed.position;
            return true;
        }

        return false;
    }

    private EscapeCandidate EvaluateCandidate(
        Vector3 candidate,
        Vector3 fleeDirection,
        float currentThreatDistance,
        float searchRadius)
    {
        float candidateThreatDistance = threatTracker.GetNearestThreatDistance(candidate, searchRadius * 2f);
        float threatGain = candidateThreatDistance - currentThreatDistance;

        float directDistance = Vector3.Distance(transform.position, candidate);
        float pathStartAlignment = GetPathStartAlignment(reusablePath, fleeDirection);
        float pathLength = CalculatePathLength(reusablePath);

        float edgeScore = GetEdgeDistanceScore(candidate);
        float opennessScore = GetOpennessScore(candidate, fleeDirection);

        float score = 0f;
        score += threatGain * 12f;
        score += pathStartAlignment * 5f;
        score += directDistance * 0.35f;
        score += edgeScore * settings.edgeDistanceWeight;
        score += opennessScore * settings.opennessWeight;
        score -= pathLength * 0.05f;

        return new EscapeCandidate
        {
            position = candidate,
            score = score,
            threatGain = threatGain,
            pathStartAlignment = pathStartAlignment,
            pathLength = pathLength
        };
    }

    private bool TryFindEmergencyLocalReposition(out Vector3 bestPosition)
    {
        bestPosition = transform.position;

        if (navAgent == null || threatTracker == null)
            return false;

        Vector3 fleeDirection = GetStableFleeDirection();

        float currentThreatDistance = threatTracker.GetNearestThreatDistance(
            transform.position,
            settings.emergencyLocalSearchRadius * 3f
        );

        float bestScore = float.MinValue;
        bool found = false;

        for (int i = 0; i < settings.emergencyLocalSampleCount; i++)
        {
            float angle = 360f / settings.emergencyLocalSampleCount * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * fleeDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinMoveThreshold)
                continue;

            direction.Normalize();

            Vector3 rawCandidate = transform.position + direction * settings.emergencyLocalSearchRadius;

            if (!TryProjectToNavMesh(rawCandidate, out Vector3 candidate))
                continue;

            if (!TryCalculateCompletePath(candidate))
                continue;

            float threatDistance = threatTracker.GetNearestThreatDistance(
                candidate,
                settings.emergencyLocalSearchRadius * 3f
            );

            float threatGain = threatDistance - currentThreatDistance;
            float pathStartAlignment = GetPathStartAlignment(reusablePath, fleeDirection);
            float distance = Vector3.Distance(transform.position, candidate);
            float edgeScore = GetEdgeDistanceScore(candidate);
            float opennessScore = GetOpennessScore(candidate, direction);

            float score = 0f;
            score += threatGain * 8f;
            score += pathStartAlignment * 4f;
            score += distance * 0.25f;
            score += edgeScore * 2f;
            score += opennessScore * 2f;

            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = candidate;
                found = true;
            }
        }

        return found;
    }

    private Vector3 GetStableFleeDirection()
    {
        Vector3 fleeDirection = Vector3.zero;

        if (threatTracker != null)
            fleeDirection = threatTracker.CalculateCombinedFleeDirection();

        fleeDirection.y = 0f;

        if (fleeDirection.sqrMagnitude <= MinMoveThreshold)
            fleeDirection = -transform.forward;

        fleeDirection.y = 0f;

        if (fleeDirection.sqrMagnitude <= MinMoveThreshold)
            fleeDirection = Vector3.back;

        return fleeDirection.normalized;
    }

    private bool TryProjectToNavMesh(Vector3 rawPosition, out Vector3 navMeshPosition)
    {
        navMeshPosition = rawPosition;

        if (NavMesh.SamplePosition(rawPosition, out NavMeshHit hit, settings.navMeshSampleRadius, NavMesh.AllAreas))
        {
            navMeshPosition = hit.position;
            return true;
        }

        return false;
    }

    private bool TryCalculateCompletePath(Vector3 destination)
    {
        if (navAgent == null)
            return false;

        if (reusablePath == null)
            reusablePath = new NavMeshPath();

        if (!navAgent.CalculatePath(destination, reusablePath))
            return false;

        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    private float GetPathStartAlignment(NavMeshPath path, Vector3 fleeDirection)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return 0f;

        Vector3 firstStep = path.corners[1] - path.corners[0];
        firstStep.y = 0f;

        if (firstStep.sqrMagnitude <= MinMoveThreshold)
            return 0f;

        return Vector3.Dot(firstStep.normalized, fleeDirection.normalized);
    }

    private float CalculatePathLength(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return 0f;

        float length = 0f;

        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return length;
    }

    private float GetEdgeDistanceScore(Vector3 candidate)
    {
        if (settings.minEdgeDistanceFromWall <= 0f)
            return 1f;

        if (!NavMesh.FindClosestEdge(candidate, out NavMeshHit edgeHit, NavMesh.AllAreas))
            return 0.5f;

        float edgeDistance = Vector3.Distance(candidate, edgeHit.position);
        return Mathf.InverseLerp(0f, settings.minEdgeDistanceFromWall + 2f, edgeDistance);
    }

    private bool IsCandidateOpenEnough(Vector3 candidate, Vector3 fleeDirection, bool strict)
    {
        float openness = GetOpennessScore(candidate, fleeDirection);

        if (!strict)
            return true;

        return openness >= settings.minOpennessRatio;
    }

    private float GetOpennessScore(Vector3 candidate, Vector3 fleeDirection)
    {
        if (wallLayerMask.value == 0)
            return 1f;

        if (settings.opennessProbeCount <= 0 || settings.opennessProbeDistance <= 0f)
            return 1f;

        Vector3 origin = candidate + Vector3.up * wallCheckHeightOffset;
        int clearCount = 0;

        float halfSpread = Mathf.Max(15f, settings.directionalSampleSpreadAngle * 0.5f);

        for (int i = 0; i < settings.opennessProbeCount; i++)
        {
            float t = settings.opennessProbeCount == 1
                ? 0.5f
                : (float)i / (settings.opennessProbeCount - 1);

            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * fleeDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinMoveThreshold)
                continue;

            direction.Normalize();

            bool blocked = Physics.SphereCast(
                origin,
                wallCheckSphereRadius,
                direction,
                out _,
                settings.opennessProbeDistance,
                wallLayerMask,
                QueryTriggerInteraction.Ignore
            );

            if (!blocked)
                clearCount++;
        }

        return (float)clearCount / settings.opennessProbeCount;
    }

    private float GetEffectiveSearchRadius(bool hasThreat)
    {
        if (IsPanicBoostActive(hasThreat))
            return settings.safeSearchRadius * settings.panicSearchRadiusMultiplier;

        return settings.safeSearchRadius;
    }

    private bool IsPanicBoostActive(bool hasThreat)
    {
        return settings.usePanicBoost &&
               hasThreat &&
               lastKnownHealthRatio <= settings.panicHealthThresholdRatio;
    }

    private bool CanFlee()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return false;

        if (threatTracker == null)
            return false;

        return true;
    }

    private bool CanUseEmergencyEscape(bool writeLog)
    {
        if (!settings.enableEmergencyEscape)
            return FailEmergencyEscape(writeLog, "Emergency Escape is disabled.");

        if (RemainingEmergencyEscapeCount <= 0)
            return FailEmergencyEscape(writeLog, "No Emergency Escape charges left.");

        if (isEmergencyEscaping)
            return FailEmergencyEscape(writeLog, "Emergency Escape is already active.");

        if (isRooted)
            return FailEmergencyEscape(writeLog, "Cannot use Emergency Escape while rooted.");

        if (navAgent == null)
            return FailEmergencyEscape(writeLog, "NavMeshAgent is missing.");

        return true;
    }

    private bool FailEmergencyEscape(bool writeLog, string message)
    {
        if (writeLog)
            Debug.Log($"[TargetEscapeMotor] {message}");

        return false;
    }

    private IEnumerator EmergencyEscapeRoutine()
    {
        isEmergencyEscaping = true;

        if (navAgent != null)
            navAgent.isStopped = false;

        float originalSpeed = navAgent != null ? navAgent.speed : 0f;
        float originalAcceleration = navAgent != null ? navAgent.acceleration : 0f;

        if (navAgent != null)
        {
            navAgent.speed = Mathf.Max(navAgent.speed, settings.emergencyEscapeSpeed);
            navAgent.acceleration = Mathf.Max(navAgent.acceleration, settings.emergencyEscapeAcceleration);
            navAgent.autoBraking = false;
        }

        float elapsed = 0f;

        while (elapsed < settings.emergencyEscapeDuration)
        {
            if (navAgent == null)
                break;

            TryFleeFromThreats(true);

            float step = Mathf.Min(
                settings.emergencyEscapeRepathInterval,
                settings.emergencyEscapeDuration - elapsed
            );

            yield return step > 0f ? new WaitForSeconds(step) : null;
            elapsed += step;
        }

        if (navAgent != null)
        {
            navAgent.speed = originalSpeed;
            navAgent.acceleration = originalAcceleration;
            navAgent.autoBraking = false;
        }

        isEmergencyEscaping = false;
        emergencyEscapeRoutine = null;

        bool hasThreat = threatTracker != null && threatTracker.HasAnyThreat();
        RefreshDynamicMovementSettings(hasThreat, lastKnownHealthRatio);
    }

    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;
        ResetAgentPath(true);

        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        isRooted = false;
        rootRoutine = null;

        if (threatTracker != null && threatTracker.HasAnyThreat())
        {
            RefreshDynamicMovementSettings(true, lastKnownHealthRatio);
            TryFleeFromThreats(true);
        }
    }

    private IEnumerator SlowRoutine(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

        isSlowed = false;
        activeSlowMultiplier = 1f;
        slowRoutine = null;

        bool hasThreat = threatTracker != null && threatTracker.HasAnyThreat();
        RefreshDynamicMovementSettings(hasThreat, lastKnownHealthRatio);

        if (hasThreat)
            TryFleeFromThreats(true);
    }

    private void StopActiveCoroutines()
    {
        if (rootRoutine != null)
        {
            StopCoroutine(rootRoutine);
            rootRoutine = null;
        }

        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }

        if (emergencyEscapeRoutine != null)
        {
            StopCoroutine(emergencyEscapeRoutine);
            emergencyEscapeRoutine = null;
        }
    }

    private void ResetMotionState()
    {
        isRooted = false;
        isSlowed = false;
        isEmergencyEscaping = false;
        activeSlowMultiplier = 1f;
        hasCurrentDestination = false;
        currentDestinationSetTime = -999f;
        stuckTimer = 0f;

        ResetAgentPath();
    }

    private void ResetAgentPath(bool stopAgent = false)
    {
        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = stopAgent;
        navAgent.ResetPath();
    }

    private void ClampLocalValues()
    {
        destinationCommitTime = Mathf.Max(0.1f, destinationCommitTime);
        repathNearDestinationDistance = Mathf.Max(0.1f, repathNearDestinationDistance);
        stuckVelocityThreshold = Mathf.Max(0.01f, stuckVelocityThreshold);
        stuckDurationBeforeRepath = Mathf.Max(0.1f, stuckDurationBeforeRepath);
        badPathAlignmentThreshold = Mathf.Clamp(badPathAlignmentThreshold, -1f, 1f);

        wallCheckHeightOffset = Mathf.Max(0f, wallCheckHeightOffset);
        wallCheckSphereRadius = Mathf.Max(0.01f, wallCheckSphereRadius);
    }
}