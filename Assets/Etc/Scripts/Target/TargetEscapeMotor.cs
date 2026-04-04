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
    [Range(-1f, 1f)] public float minPathStartAlignment = -0.05f;

    [Header("Safe Point Search")]
    public float safeSearchRadius = 20f;
    public int safePointSampleCount = 20;
    public float safePointMinDistance = 7f;
    public float navMeshSampleRadius = 4f;
    public float fleeDirectionBias = 2f;

    [Header("Panic Boost")]
    public bool usePanicBoost = false;
    [Range(0f, 1f)] public float panicHealthThresholdRatio = 0.45f;
    public float panicSpeedMultiplier = 1.2f;
    public float panicSearchRadiusMultiplier = 1.2f;
    public int panicSampleCountBonus = 6;
    public float panicFleeDirectionBiasBonus = 0.75f;

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
}

[RequireComponent(typeof(NavMeshAgent))]
public class TargetEscapeMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TargetThreatTracker threatTracker;

    [Header("Flee Movement")]
    [SerializeField] private float repathCooldown = 0.25f;
    [SerializeField] private float fleeMoveSpeed = 15f;
    [SerializeField] private float fleeAngularSpeed = 1080f;
    [SerializeField] private float fleeAcceleration = 45f;
    [SerializeField] private float minNearestThreatDistanceGain = 0.5f;
    [SerializeField][Range(-1f, 1f)] private float minPathStartAlignment = -0.05f;

    [Header("Safe Point Search")]
    [SerializeField] private float safeSearchRadius = 20f;
    [SerializeField] private int safePointSampleCount = 20;
    [SerializeField] private float safePointMinDistance = 7f;
    [SerializeField] private float navMeshSampleRadius = 4f;
    [SerializeField] private float fleeDirectionBias = 2f;

    [Header("Panic Boost")]
    [SerializeField] private bool usePanicBoost = false;
    [SerializeField][Range(0f, 1f)] private float panicHealthThresholdRatio = 0.45f;
    [SerializeField] private float panicSpeedMultiplier = 1.2f;
    [SerializeField] private float panicSearchRadiusMultiplier = 1.2f;
    [SerializeField] private int panicSampleCountBonus = 6;
    [SerializeField] private float panicFleeDirectionBiasBonus = 0.75f;

    [Header("Emergency Escape")]
    [SerializeField] private bool enableEmergencyEscape = true;
    [SerializeField] private int emergencyEscapeCharges = 1;
    [SerializeField] private bool autoUseEmergencyEscape = false;
    [SerializeField][Range(0f, 1f)] private float emergencyEscapeAutoTriggerHealthRatio = 0.35f;
    [SerializeField] private float emergencyEscapeAutoTriggerThreatDistance = 6f;
    [SerializeField] private float emergencyEscapeDuration = 0.8f;
    [SerializeField] private float emergencyEscapeSpeed = 22f;
    [SerializeField] private float emergencyEscapeAcceleration = 30f;
    [SerializeField] private float emergencyEscapeRepathInterval = 0.2f;

    private NavMeshAgent navAgent;

    private Coroutine rootRoutine;
    private Coroutine emergencyEscapeRoutine;

    private float lastRepathTime = -999f;
    private int emergencyEscapeUsedCount = 0;

    private bool isRooted = false;
    private bool isEmergencyEscaping = false;
    private float lastKnownHealthRatio = 1f;

    public bool IsRooted => isRooted;
    public bool IsEmergencyEscaping => isEmergencyEscaping;
    public bool CanBeCaught => !isEmergencyEscaping;
    public bool HasUsedEmergencyEscape => emergencyEscapeUsedCount > 0;
    public int RemainingEmergencyEscapeCount => Mathf.Max(0, emergencyEscapeCharges - emergencyEscapeUsedCount);

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        ApplyNavAgentBaseSettings();
    }

    private void OnDisable()
    {
        if (rootRoutine != null)
        {
            StopCoroutine(rootRoutine);
            rootRoutine = null;
        }

        if (emergencyEscapeRoutine != null)
        {
            StopCoroutine(emergencyEscapeRoutine);
            emergencyEscapeRoutine = null;
        }

        isRooted = false;
        isEmergencyEscaping = false;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }
    }

    public void SetThreatTracker(TargetThreatTracker tracker)
    {
        threatTracker = tracker;
    }

    public void ApplyNavAgentBaseSettings()
    {
        if (navAgent == null)
            return;

        navAgent.speed = fleeMoveSpeed;
        navAgent.autoBraking = false;
        navAgent.acceleration = fleeAcceleration;
        navAgent.angularSpeed = fleeAngularSpeed;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    public void RefreshDynamicMovementSettings(bool hasThreat, float healthRatio)
    {
        if (navAgent == null || isEmergencyEscaping)
            return;

        lastKnownHealthRatio = Mathf.Clamp01(healthRatio);

        float targetSpeed = fleeMoveSpeed;
        float targetAcceleration = fleeAcceleration;

        if (IsPanicBoostActive(hasThreat, lastKnownHealthRatio))
        {
            targetSpeed *= panicSpeedMultiplier;
            targetAcceleration *= panicSpeedMultiplier;
        }

        navAgent.speed = targetSpeed;
        navAgent.acceleration = targetAcceleration;
        navAgent.angularSpeed = fleeAngularSpeed;
    }

    public bool TryAutoEmergencyEscape(float healthRatio)
    {
        lastKnownHealthRatio = Mathf.Clamp01(healthRatio);

        if (!autoUseEmergencyEscape)
            return false;

        if (!enableEmergencyEscape)
            return false;

        if (isRooted || isEmergencyEscaping)
            return false;

        if (RemainingEmergencyEscapeCount <= 0)
            return false;

        if (lastKnownHealthRatio > emergencyEscapeAutoTriggerHealthRatio)
            return false;

        if (threatTracker == null)
            return false;

        float nearestAgentDistance = threatTracker.GetNearestRealAgentDistance();
        if (nearestAgentDistance > emergencyEscapeAutoTriggerThreatDistance)
            return false;

        return TryActivateEmergencyEscape();
    }

    public bool TryActivateEmergencyEscape()
    {
        if (!enableEmergencyEscape)
        {
            Debug.Log("<color=orange>[TargetEscapeMotor]</color> 긴급 회피 비활성화 상태라 사용할 수 없습니다.");
            return false;
        }

        if (RemainingEmergencyEscapeCount <= 0)
        {
            Debug.Log("<color=orange>[TargetEscapeMotor]</color> 긴급 회피 사용 가능 횟수가 없습니다.");
            return false;
        }

        if (isEmergencyEscaping)
        {
            Debug.Log("<color=orange>[TargetEscapeMotor]</color> 현재 이미 긴급 회피 중입니다.");
            return false;
        }

        if (isRooted)
        {
            Debug.Log("<color=orange>[TargetEscapeMotor]</color> 속박 상태라 긴급 회피를 사용할 수 없습니다.");
            return false;
        }

        if (navAgent == null)
        {
            Debug.LogWarning("<color=orange>[TargetEscapeMotor]</color> NavMeshAgent가 없어 긴급 회피를 사용할 수 없습니다.");
            return false;
        }

        emergencyEscapeUsedCount++;

        Debug.Log($"<color=orange>[TargetEscapeMotor]</color> 긴급 회피 발동! 남은 횟수={RemainingEmergencyEscapeCount}");

        if (emergencyEscapeRoutine != null)
            StopCoroutine(emergencyEscapeRoutine);

        emergencyEscapeRoutine = StartCoroutine(EmergencyEscapeRoutine());
        return true;
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
            navAgent.speed = Mathf.Max(navAgent.speed, emergencyEscapeSpeed);
            navAgent.acceleration = Mathf.Max(navAgent.acceleration, emergencyEscapeAcceleration);
        }

        Debug.Log("<color=orange>[TargetEscapeMotor]</color> 긴급 회피 발동! 잠시 포획되지 않습니다.");

        float elapsed = 0f;

        while (elapsed < emergencyEscapeDuration)
        {
            if (navAgent == null)
                break;

            TryFleeFromThreats(true);

            float waitTime = Mathf.Min(emergencyEscapeRepathInterval, emergencyEscapeDuration - elapsed);

            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);
            else
                yield return null;

            elapsed += waitTime;
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

        Debug.Log("<color=orange>[TargetEscapeMotor]</color> 긴급 회피 종료. 다시 포획될 수 있습니다.");
    }

    public void ApplyRoot(float duration)
    {
        if (rootRoutine != null)
            StopCoroutine(rootRoutine);

        rootRoutine = StartCoroutine(RootRoutine(duration));
    }

    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        Debug.Log($"<color=red>[TargetEscapeMotor]</color> 속박 발동! {duration}초 동안 정지합니다.");

        yield return new WaitForSeconds(duration);

        if (navAgent != null)
            navAgent.isStopped = false;

        isRooted = false;
        rootRoutine = null;

        Debug.Log("<color=red>[TargetEscapeMotor]</color> 속박 종료.");

        if (threatTracker != null && threatTracker.HasAnyThreat())
        {
            RefreshDynamicMovementSettings(true, lastKnownHealthRatio);
            TryFleeFromThreats(true);
        }
    }

    public void TryFleeFromThreats(bool forceRepath = false)
    {
        if (navAgent == null || navAgent.isStopped)
            return;

        if (threatTracker == null)
            return;

        bool hasThreat = threatTracker.HasAnyThreat();
        if (!hasThreat)
            return;

        float effectiveRepathCooldown = GetEffectiveRepathCooldown(hasThreat);

        if (!forceRepath && Time.time - lastRepathTime < effectiveRepathCooldown)
            return;

        if (TryFindBestSafePosition(out Vector3 bestPosition))
        {
            navAgent.SetDestination(bestPosition);
            lastRepathTime = Time.time;
            Debug.Log($"[TargetEscapeMotor] 도주 목적지 갱신: {bestPosition}");
        }
    }

    private float GetEffectiveRepathCooldown(bool hasThreat)
    {
        if (IsPanicBoostActive(hasThreat, lastKnownHealthRatio))
            return Mathf.Max(0.05f, repathCooldown * 0.75f);

        return repathCooldown;
    }

    private float GetEffectiveSafeSearchRadius(bool hasThreat)
    {
        float radius = safeSearchRadius;

        if (IsPanicBoostActive(hasThreat, lastKnownHealthRatio))
            radius *= panicSearchRadiusMultiplier;

        return radius;
    }

    private int GetEffectiveSafePointSampleCount(bool hasThreat)
    {
        int count = safePointSampleCount;

        if (IsPanicBoostActive(hasThreat, lastKnownHealthRatio))
            count += panicSampleCountBonus;

        return Mathf.Max(4, count);
    }

    private float GetEffectiveFleeDirectionBias(bool hasThreat)
    {
        float bias = fleeDirectionBias;

        if (IsPanicBoostActive(hasThreat, lastKnownHealthRatio))
            bias += panicFleeDirectionBiasBonus;

        return bias;
    }

    private bool IsPanicBoostActive(bool hasThreat, float healthRatio)
    {
        if (!usePanicBoost)
            return false;

        if (!hasThreat)
            return false;

        return healthRatio <= panicHealthThresholdRatio;
    }

    private bool TryFindBestSafePosition(out Vector3 bestPosition)
    {
        bestPosition = transform.position;

        if (threatTracker == null || navAgent == null)
            return false;

        bool hasThreat = threatTracker.HasAnyThreat();
        float currentSafeSearchRadius = GetEffectiveSafeSearchRadius(hasThreat);
        int currentSampleCount = GetEffectiveSafePointSampleCount(hasThreat);
        float currentFleeDirectionBias = GetEffectiveFleeDirectionBias(hasThreat);

        Vector3 fleeDirection = threatTracker.CalculateCombinedFleeDirection();
        Vector3 flatFleeDirection = fleeDirection.sqrMagnitude > 0.001f
            ? fleeDirection.normalized
            : Vector3.zero;

        float currentNearestThreatDistance =
            threatTracker.GetNearestThreatDistance(transform.position, currentSafeSearchRadius * 2f);

        float bestStrictScore = float.MinValue;
        bool foundStrict = false;
        Vector3 bestFallbackPosition = transform.position;
        float bestFallbackScore = float.MinValue;
        bool foundFallback = false;

        for (int i = 0; i < currentSampleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * currentSafeSearchRadius;
            Vector3 rawCandidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (Vector3.Distance(transform.position, rawCandidate) < safePointMinDistance)
                continue;

            if (!NavMesh.SamplePosition(rawCandidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 candidate = hit.position;

            if (!IsReachable(candidate))
                continue;

            float candidateNearestThreatDistance =
                threatTracker.GetNearestThreatDistance(candidate, currentSafeSearchRadius * 2f);

            float pathStartAlignment = GetPathStartAlignment(candidate, flatFleeDirection);

            float score = EvaluateSafePoint(
                candidate,
                flatFleeDirection,
                currentNearestThreatDistance,
                candidateNearestThreatDistance,
                pathStartAlignment,
                currentFleeDirectionBias
            );

            bool isSaferThanCurrent =
                candidateNearestThreatDistance + 0.01f >= currentNearestThreatDistance + minNearestThreatDistanceGain;

            bool pathStartsAwayFromThreat =
                pathStartAlignment >= minPathStartAlignment;

            if (isSaferThanCurrent && pathStartsAwayFromThreat)
            {
                if (score > bestStrictScore)
                {
                    bestStrictScore = score;
                    bestPosition = candidate;
                    foundStrict = true;
                }
            }
            else
            {
                if (!foundStrict && score > bestFallbackScore)
                {
                    bestFallbackScore = score;
                    bestFallbackPosition = candidate;
                    foundFallback = true;
                }
            }
        }

        if (foundStrict)
            return true;

        if (foundFallback)
        {
            bestPosition = bestFallbackPosition;
            return true;
        }

        return false;
    }

    private bool IsReachable(Vector3 destination)
    {
        if (navAgent == null)
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!navAgent.CalculatePath(destination, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    private float GetPathStartAlignment(Vector3 destination, Vector3 fleeDirection)
    {
        if (navAgent == null)
            return -1f;

        if (fleeDirection == Vector3.zero)
            return 1f;

        NavMeshPath path = new NavMeshPath();
        if (!navAgent.CalculatePath(destination, path))
            return -1f;

        if (path.status != NavMeshPathStatus.PathComplete)
            return -1f;

        if (path.corners == null || path.corners.Length < 2)
            return 1f;

        Vector3 firstStep = path.corners[1] - transform.position;
        firstStep.y = 0f;

        if (firstStep.sqrMagnitude <= 0.001f)
            return 1f;

        return Vector3.Dot(firstStep.normalized, fleeDirection);
    }

    private float EvaluateSafePoint(
        Vector3 candidate,
        Vector3 fleeDirection,
        float currentNearestThreatDistance,
        float candidateNearestThreatDistance,
        float pathStartAlignment,
        float currentFleeDirectionBias)
    {
        if (threatTracker == null)
            return float.MinValue;

        float score = 0f;

        score += threatTracker.GetDistanceScoreFromAgents(candidate) * 3f;
        score += threatTracker.GetDistanceScoreFromDecoys(candidate) * 1.5f;
        score += threatTracker.GetDistanceScoreFromPhantoms(candidate) * 2f;

        float moveDistance = Vector3.Distance(transform.position, candidate);
        score += moveDistance * 0.5f;

        if (fleeDirection != Vector3.zero)
        {
            Vector3 toCandidate = (candidate - transform.position).normalized;
            float alignment = Vector3.Dot(toCandidate, fleeDirection);
            score += alignment * currentFleeDirectionBias;

            if (alignment < 0f)
                score += alignment * 20f;
        }

        float nearestThreatDistanceGain = candidateNearestThreatDistance - currentNearestThreatDistance;
        score += nearestThreatDistanceGain * 5f;

        if (nearestThreatDistanceGain < 0f)
            score += nearestThreatDistanceGain * 25f;

        score += pathStartAlignment * 8f;

        if (pathStartAlignment < 0f)
            score += pathStartAlignment * 30f;

        return score;
    }

    public bool IsCompletelyStopped()
    {
        if (navAgent == null)
            return true;

        bool noPathOrArrived =
            !navAgent.pathPending &&
            (!navAgent.hasPath || navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f);

        bool almostNotMoving = navAgent.velocity.sqrMagnitude <= 0.01f;

        return noPathOrArrived && almostNotMoving;
    }

    public void ResetRuntimeState(bool resetEmergencyEscapeUsage = true)
    {
        if (rootRoutine != null)
        {
            StopCoroutine(rootRoutine);
            rootRoutine = null;
        }

        if (emergencyEscapeRoutine != null)
        {
            StopCoroutine(emergencyEscapeRoutine);
            emergencyEscapeRoutine = null;
        }

        isRooted = false;
        isEmergencyEscaping = false;
        lastRepathTime = -999f;
        lastKnownHealthRatio = 1f;

        if (resetEmergencyEscapeUsage)
            emergencyEscapeUsedCount = 0;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        ApplyNavAgentBaseSettings();
    }

    public void ApplySettings(EscapeSettings settings)
    {
        if (settings == null)
            return;

        repathCooldown = settings.repathCooldown;
        fleeMoveSpeed = settings.fleeMoveSpeed;
        fleeAngularSpeed = settings.fleeAngularSpeed;
        fleeAcceleration = settings.fleeAcceleration;
        minNearestThreatDistanceGain = settings.minNearestThreatDistanceGain;
        minPathStartAlignment = settings.minPathStartAlignment;

        safeSearchRadius = settings.safeSearchRadius;
        safePointSampleCount = settings.safePointSampleCount;
        safePointMinDistance = settings.safePointMinDistance;
        navMeshSampleRadius = settings.navMeshSampleRadius;
        fleeDirectionBias = settings.fleeDirectionBias;

        usePanicBoost = settings.usePanicBoost;
        panicHealthThresholdRatio = settings.panicHealthThresholdRatio;
        panicSpeedMultiplier = settings.panicSpeedMultiplier;
        panicSearchRadiusMultiplier = settings.panicSearchRadiusMultiplier;
        panicSampleCountBonus = settings.panicSampleCountBonus;
        panicFleeDirectionBiasBonus = settings.panicFleeDirectionBiasBonus;

        enableEmergencyEscape = settings.enableEmergencyEscape;
        emergencyEscapeCharges = Mathf.Max(0, settings.emergencyEscapeCharges);
        autoUseEmergencyEscape = settings.autoUseEmergencyEscape;
        emergencyEscapeAutoTriggerHealthRatio = settings.emergencyEscapeAutoTriggerHealthRatio;
        emergencyEscapeAutoTriggerThreatDistance = settings.emergencyEscapeAutoTriggerThreatDistance;
        emergencyEscapeDuration = settings.emergencyEscapeDuration;
        emergencyEscapeSpeed = settings.emergencyEscapeSpeed;
        emergencyEscapeAcceleration = settings.emergencyEscapeAcceleration;
        emergencyEscapeRepathInterval = settings.emergencyEscapeRepathInterval;

        ApplyNavAgentBaseSettings();
    }

}