using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class EscapeSettings
{
    [Header("Flee Movement")]
    public float repathCooldown = 0.25f;
    public float fleeMoveSpeed = 15f;
    public float fleeAngularSpeed = 1080f;
    public float fleeAcceleration = 45f;
    public float minNearestThreatDistanceGain = 0.5f;
    [Range(-1f, 1f)] public float minPathStartAlignment = 0.1f;

    [Header("Safe Point Search")]
    public float safeSearchRadius = 20f;
    public int safePointSampleCount = 5;
    public float safePointMinDistance = 7f;
    public float navMeshSampleRadius = 4f;
    public float fleeDirectionBias = 2f;
    [Range(0f, 1f)] public float directionalSampleRatio = 1f;
    public float directionalSampleSpreadAngle = 70f;

    [Header("Dead End Avoidance")]
    public float minEdgeDistanceFromWall = 0f;
    public float edgeDistanceWeight = 0f;
    public float opennessProbeDistance = 0f;
    public int opennessProbeCount = 0;
    [Range(0f, 1f)] public float minOpennessRatio = 0f;
    public float opennessWeight = 0f;

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
        repathCooldown = Mathf.Max(0.01f, repathCooldown);
        fleeMoveSpeed = Mathf.Max(0f, fleeMoveSpeed);
        fleeAngularSpeed = Mathf.Max(0f, fleeAngularSpeed);
        fleeAcceleration = Mathf.Max(0f, fleeAcceleration);
        minNearestThreatDistanceGain = Mathf.Max(0f, minNearestThreatDistanceGain);
        minPathStartAlignment = Mathf.Clamp(minPathStartAlignment, -1f, 1f);

        safeSearchRadius = Mathf.Max(0.5f, safeSearchRadius);
        safePointSampleCount = Mathf.Clamp(safePointSampleCount, 3, 7);
        safePointMinDistance = Mathf.Max(0f, safePointMinDistance);
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        fleeDirectionBias = Mathf.Max(0f, fleeDirectionBias);
        directionalSampleRatio = Mathf.Clamp01(directionalSampleRatio);
        directionalSampleSpreadAngle = Mathf.Max(0f, directionalSampleSpreadAngle);

        minEdgeDistanceFromWall = Mathf.Max(0f, minEdgeDistanceFromWall);
        edgeDistanceWeight = Mathf.Max(0f, edgeDistanceWeight);
        opennessProbeDistance = Mathf.Max(0f, opennessProbeDistance);
        opennessProbeCount = Mathf.Max(0, opennessProbeCount);
        minOpennessRatio = Mathf.Clamp01(minOpennessRatio);
        opennessWeight = Mathf.Max(0f, opennessWeight);

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
    private const float MinFallbackRepathCooldown = 0.05f;

    [Header("References")]
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private TargetSkillController skillController;
    [SerializeField] private EscapeSettings settings = new EscapeSettings();

    private NavMeshAgent navAgent;
    private NavMeshPath reusablePath;

    private Coroutine rootRoutine;
    private Coroutine slowRoutine;
    private Coroutine emergencyEscapeRoutine;

    private float lastRepathTime = -999f;
    private float lastKnownHealthRatio = 1f;
    private float activeSlowMultiplier = 1f;
    private int emergencyEscapeUsedCount;

    private bool isRooted;
    private bool isSlowed;
    private bool isEmergencyEscaping;

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

        if (reusablePath == null)
            reusablePath = new NavMeshPath();

        settings.ClampValues();
        ApplyNavAgentBaseSettings();
    }

    private void OnValidate()
    {
        if (settings == null)
            settings = new EscapeSettings();

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

        Debug.Log($"[TargetEscapeMotor] ��� ȸ�� �ߵ�. ���� Ƚ��={RemainingEmergencyEscapeCount}");
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

        bool hasThreat = threatTracker.HasAnyThreat();
        if (!hasThreat)
            return;

        float cooldown = GetEffectiveRepathCooldown(hasThreat);
        if (!forceRepath && Time.time - lastRepathTime < cooldown)
            return;

        if (!TryFindSimpleFleePosition(out Vector3 bestPosition))
            return;

        if (!navAgent.SetDestination(bestPosition))
            return;

        if (skillController != null)
            skillController.TryUseAutoDefensiveSkill(bestPosition);

        lastRepathTime = Time.time;
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
        }

        Debug.Log("[TargetEscapeMotor] ��� ȸ�� ����");

        float elapsed = 0f;
        while (elapsed < settings.emergencyEscapeDuration)
        {
            if (navAgent == null)
                break;

            TryFleeFromThreats(true);

            float step = Mathf.Min(settings.emergencyEscapeRepathInterval, settings.emergencyEscapeDuration - elapsed);
            yield return step > 0f ? new WaitForSeconds(step) : null;
            elapsed += step;
        }

        if (navAgent != null)
        {
            navAgent.speed = originalSpeed;
            navAgent.acceleration = originalAcceleration;
        }

        isEmergencyEscaping = false;
        emergencyEscapeRoutine = null;

        bool hasThreat = threatTracker != null && threatTracker.HasAnyThreat();
        RefreshDynamicMovementSettings(hasThreat, lastKnownHealthRatio);

        Debug.Log("[TargetEscapeMotor] ��� ȸ�� ����");
    }

    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;
        ResetAgentPath(true);

        Debug.Log($"[TargetEscapeMotor] �ӹ� �ߵ�. {duration}�� ���� ����");

        yield return new WaitForSeconds(duration);

        if (navAgent != null)
            navAgent.isStopped = false;

        isRooted = false;
        rootRoutine = null;

        Debug.Log("[TargetEscapeMotor] �ӹ� ����");

        if (threatTracker != null && threatTracker.HasAnyThreat())
        {
            RefreshDynamicMovementSettings(true, lastKnownHealthRatio);
            TryFleeFromThreats(true);
        }
    }

    private IEnumerator SlowRoutine(float duration)
    {
        duration = Mathf.Max(0.01f, duration);

        Debug.Log($"[TargetEscapeMotor] ���� �ߵ�. {duration:0.##}�� ���� �̵� �ӵ� x{activeSlowMultiplier:0.##}");

        yield return new WaitForSeconds(duration);

        isSlowed = false;
        activeSlowMultiplier = 1f;
        slowRoutine = null;

        Debug.Log("[TargetEscapeMotor] ���� ����");

        bool hasThreat = threatTracker != null && threatTracker.HasAnyThreat();
        RefreshDynamicMovementSettings(hasThreat, lastKnownHealthRatio);

        if (hasThreat)
            TryFleeFromThreats(true);
    }

    private bool CanUseEmergencyEscape(bool writeLog)
    {
        if (!settings.enableEmergencyEscape)
            return FailEmergencyEscape(writeLog, "��� ȸ�� ��Ȱ��ȭ �����Դϴ�.");

        if (RemainingEmergencyEscapeCount <= 0)
            return FailEmergencyEscape(writeLog, "��� ȸ�� ��� ���� Ƚ���� �����ϴ�.");

        if (isEmergencyEscaping)
            return FailEmergencyEscape(writeLog, "�̹� ��� ȸ�� ���Դϴ�.");

        if (isRooted)
            return FailEmergencyEscape(writeLog, "�ӹ� ���¿����� ����� �� �����ϴ�.");

        if (navAgent == null)
        {
            if (writeLog)
                Debug.LogWarning("[TargetEscapeMotor] NavMeshAgent�� ���� ��� ȸ�Ǹ� ����� �� �����ϴ�.");
            return false;
        }

        return true;
    }

    private bool FailEmergencyEscape(bool writeLog, string message)
    {
        if (writeLog)
            Debug.Log($"[TargetEscapeMotor] {message}");

        return false;
    }

    private bool CanFlee()
    {
        return navAgent != null && !navAgent.isStopped && threatTracker != null;
    }

    private float GetEffectiveRepathCooldown(bool hasThreat)
    {
        if (IsPanicBoostActive(hasThreat))
            return Mathf.Max(MinFallbackRepathCooldown, settings.repathCooldown * 0.75f);

        return settings.repathCooldown;
    }

    private float GetEffectiveSafeSearchRadius(bool hasThreat)
    {
        if (IsPanicBoostActive(hasThreat))
            return settings.safeSearchRadius * settings.panicSearchRadiusMultiplier;

        return settings.safeSearchRadius;
    }

    private bool IsPanicBoostActive(bool hasThreat)
    {
        return settings.usePanicBoost && hasThreat && lastKnownHealthRatio <= settings.panicHealthThresholdRatio;
    }

    private bool TryFindSimpleFleePosition(out Vector3 bestPosition)
    {
        bestPosition = transform.position;

        if (!CanFlee() || threatTracker == null)
            return false;

        bool hasThreat = threatTracker.HasAnyThreat();
        if (!hasThreat)
            return false;

        Vector3 fleeDirection = threatTracker.CalculateCombinedFleeDirection();
        fleeDirection.y = 0f;

        if (fleeDirection.sqrMagnitude <= MinMoveThreshold)
            fleeDirection = -transform.forward;

        if (fleeDirection.sqrMagnitude <= MinMoveThreshold)
            fleeDirection = Vector3.back;

        fleeDirection.Normalize();

        float searchRadius = GetEffectiveSafeSearchRadius(hasThreat);
        float currentThreatDistance = threatTracker.GetNearestThreatDistance(transform.position, searchRadius * 2f);

        float[] angleOffsets = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 110f, -110f };
        float[] distanceScales = { 1f, 0.8f, 0.6f, 0.4f };

        bool foundSaferCandidate = false;
        bool foundFallbackCandidate = false;

        float bestSaferScore = float.MinValue;
        float bestFallbackScore = float.MinValue;
        Vector3 fallbackPosition = transform.position;

        for (int d = 0; d < distanceScales.Length; d++)
        {
            float distance = Mathf.Max(settings.safePointMinDistance, searchRadius * distanceScales[d]);

            for (int i = 0; i < angleOffsets.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, angleOffsets[i], 0f) * fleeDirection;
                direction.y = 0f;

                if (direction.sqrMagnitude <= MinMoveThreshold)
                    continue;

                direction.Normalize();

                Vector3 rawCandidate = transform.position + direction * distance;

                if (!NavMesh.SamplePosition(rawCandidate, out NavMeshHit hit, settings.navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                Vector3 candidate = hit.position;

                if (!TryCalculatePath(candidate))
                    continue;

                float pathStartAlignment = GetPathStartAlignment(reusablePath, fleeDirection);
                float candidateThreatDistance = threatTracker.GetNearestThreatDistance(candidate, searchRadius * 2f);
                float threatDistanceGain = candidateThreatDistance - currentThreatDistance;
                float moveDistance = Vector3.Distance(transform.position, candidate);

                float score =
                    threatDistanceGain * 10f +
                    pathStartAlignment * 2f +
                    moveDistance * 0.2f;

                if (threatDistanceGain > 0.05f)
                {
                    if (score > bestSaferScore)
                    {
                        bestSaferScore = score;
                        bestPosition = candidate;
                        foundSaferCandidate = true;
                    }
                }
                else
                {
                    if (pathStartAlignment > -0.35f && score > bestFallbackScore)
                    {
                        bestFallbackScore = score;
                        fallbackPosition = candidate;
                        foundFallbackCandidate = true;
                    }
                }
            }
        }

        if (foundSaferCandidate)
            return true;

        if (foundFallbackCandidate)
        {
            bestPosition = fallbackPosition;
            return true;
        }

        return false;
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

    private float GetPathStartAlignment(NavMeshPath path, Vector3 fleeDirection)
    {
        if (fleeDirection == Vector3.zero || path == null || path.corners == null || path.corners.Length < 2)
            return 1f;

        Vector3 firstStep = path.corners[1] - transform.position;
        firstStep.y = 0f;

        if (firstStep.sqrMagnitude <= MinMoveThreshold)
            return 1f;

        return Vector3.Dot(firstStep.normalized, fleeDirection.normalized);
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
        ResetAgentPath();
    }

    private void ResetAgentPath(bool stopAgent = false)
    {
        if (navAgent == null)
            return;

        navAgent.isStopped = stopAgent;
        navAgent.ResetPath();
    }
}