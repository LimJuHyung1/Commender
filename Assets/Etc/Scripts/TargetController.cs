using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using CodeMonkey.HealthSystemCM;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(SphereCollider))]
public class TargetController : MonoBehaviour, IGetHealthSystem
{
    [Header("회피 설정")]
    [SerializeField] private LayerMask agentLayer;
    [SerializeField] private float repathCooldown = 1.0f;

    [Header("안전 지점 탐색")]
    [SerializeField] private float safeSearchRadius = 20f;
    [SerializeField] private int safePointSampleCount = 20;
    [SerializeField] private float safePointMinDistance = 7f;
    [SerializeField] private float navMeshSampleRadius = 4f;
    [SerializeField] private float fleeDirectionBias = 2f;

    [Header("체력 설정")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float startHealth = 100f;
    [SerializeField] private float fleeHealthDrainPerSecond = 12f;

    [Header("정지 회복 설정")]
    [SerializeField] private float recoveryDelayAfterStop = 10f;
    [SerializeField] private float recoverHealthPerSecond = 1f;
    [SerializeField] private float recoveryAmountTotal = 20f;
    [SerializeField] private float recoveryDuration = 2f;
    private bool isRecoveringAfterStop = false;
    private float recoveryStartHealth = 0f;

    [Header("교란 위협 설정")]
    [SerializeField] private float decoySignalWeight = 0.8f;
    [SerializeField] private float decoySignalInfluenceRadius = 12f;
    [SerializeField] private float phantomThreatInfluenceRadius = 15f;

    [Header("긴급 회피 스킬")]
    [SerializeField] private bool enableEmergencyEscape = true;
    [SerializeField] private float emergencyEscapeDuration = 0.8f;
    [SerializeField] private float emergencyEscapeSpeed = 22f;
    [SerializeField] private float emergencyEscapeAcceleration = 30f;
    [SerializeField] private float emergencyEscapeRepathInterval = 0.2f;

    [Header("플레이어 정찰 표시")]
    [SerializeField] private GameObject playerRevealMarker;

    private float normalDetectionRadius = 5f;
    private SphereCollider detectionCollider;
    private NavMeshAgent navAgent;
    private readonly List<Transform> nearbyAgents = new List<Transform>();

    private int reconRevealCount = 0;
    private bool isRooted = false;

    private Coroutine smokeRoutine;
    private Coroutine rootRoutine;
    private Coroutine emergencyEscapeRoutine;

    private HealthSystem healthSystem;

    private float lastRepathTime = -999f;
    private float stoppedRecoveryTimer = 0f;

    private bool hasUsedEmergencyEscape = false;
    private bool isEmergencyEscaping = false;

    public bool IsRevealedToPlayer => reconRevealCount > 0;
    public bool IsRooted => isRooted;
    public bool HasUsedEmergencyEscape => hasUsedEmergencyEscape;
    public bool IsEmergencyEscaping => isEmergencyEscaping;
    public bool CanBeCaught => !isEmergencyEscaping;
    public float CurrentHealth => healthSystem != null ? healthSystem.GetHealth() : 0f;
    public float MaxHealth => healthSystem != null ? healthSystem.GetHealthMax() : maxHealth;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        detectionCollider = GetComponent<SphereCollider>();

        if (navAgent != null)
        {
            navAgent.speed = 15f;
            navAgent.autoBraking = false;
        }

        if (detectionCollider != null)
        {
            detectionCollider.isTrigger = true;
            normalDetectionRadius = detectionCollider.radius;
        }

        CreateHealthSystem();
        UpdatePlayerRevealVisual();
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDead -= HealthSystem_OnDead;
        }
    }

    private void Update()
    {
        if (isRooted)
            return;

        if (healthSystem == null || healthSystem.IsDead())
            return;

        bool hasThreat = HasAnyThreat();

        if (hasThreat)
        {
            stoppedRecoveryTimer = 0f;

            float damageAmount = fleeHealthDrainPerSecond * Time.deltaTime;
            healthSystem.Damage(damageAmount);

            TryFleeFromThreats();
        }
        else
        {
            HandleStoppedRecovery();
        }
    }

    private void CreateHealthSystem()
    {
        healthSystem = new HealthSystem(maxHealth);

        float clampedStartHealth = Mathf.Clamp(startHealth, 0f, maxHealth);
        healthSystem.SetHealth(clampedStartHealth);

        healthSystem.OnDead += HealthSystem_OnDead;
    }

    private void HealthSystem_OnDead(object sender, EventArgs e)
    {
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        Debug.Log("<color=red>[Target]</color> 체력이 0이 되어 더 이상 이동하지 않습니다.");
    }

    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }

    public void Damage(float amount)
    {
        if (healthSystem == null || healthSystem.IsDead())
            return;

        healthSystem.Damage(amount);
    }

    public void Heal(float amount)
    {
        if (healthSystem == null || healthSystem.IsDead())
            return;

        healthSystem.Heal(amount);
    }

    public void HealComplete()
    {
        if (healthSystem == null)
            return;

        healthSystem.HealComplete();

        if (navAgent != null && navAgent.isStopped)
        {
            navAgent.isStopped = false;
        }
    }

    public bool TryActivateEmergencyEscape()
    {
        if (!enableEmergencyEscape)
        {
            Debug.Log("<color=orange>[Target]</color> 긴급 회피 비활성화 상태라 사용할 수 없습니다.");
            return false;
        }

        if (hasUsedEmergencyEscape)
        {
            Debug.Log("<color=orange>[Target]</color> 긴급 회피는 이미 사용했습니다.");
            return false;
        }

        if (isEmergencyEscaping)
        {
            Debug.Log("<color=orange>[Target]</color> 현재 이미 긴급 회피 중입니다.");
            return false;
        }

        if (isRooted)
        {
            Debug.Log("<color=orange>[Target]</color> 속박 상태라 긴급 회피를 사용할 수 없습니다.");
            return false;
        }

        if (navAgent == null)
        {
            Debug.LogWarning("<color=orange>[Target]</color> NavMeshAgent가 없어 긴급 회피를 사용할 수 없습니다.");
            return false;
        }

        if (healthSystem == null || healthSystem.IsDead())
        {
            Debug.Log("<color=orange>[Target]</color> 사망 상태라 긴급 회피를 사용할 수 없습니다.");
            return false;
        }

        hasUsedEmergencyEscape = true;

        Debug.Log($"<color=orange>[Target]</color> 긴급 회피 발동! duration={emergencyEscapeDuration}, speed={emergencyEscapeSpeed}");

        if (emergencyEscapeRoutine != null)
            StopCoroutine(emergencyEscapeRoutine);

        emergencyEscapeRoutine = StartCoroutine(EmergencyEscapeRoutine());
        return true;
    }

    private IEnumerator EmergencyEscapeRoutine()
    {
        isEmergencyEscaping = true;
        stoppedRecoveryTimer = 0f;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
        }

        float originalSpeed = navAgent != null ? navAgent.speed : 0f;
        float originalAcceleration = navAgent != null ? navAgent.acceleration : 0f;

        if (navAgent != null)
        {
            navAgent.speed = Mathf.Max(originalSpeed, emergencyEscapeSpeed);
            navAgent.acceleration = Mathf.Max(originalAcceleration, emergencyEscapeAcceleration);
        }

        Debug.Log("<color=orange>[Target]</color> 긴급 회피 발동! 잠시 포획되지 않습니다.");

        float elapsed = 0f;

        while (elapsed < emergencyEscapeDuration)
        {
            if (navAgent == null || healthSystem == null || healthSystem.IsDead())
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

        Debug.Log("<color=orange>[Target]</color> 긴급 회피 종료. 다시 포획될 수 있습니다.");
    }

    private void HandleStoppedRecovery()
    {
        if (navAgent == null || healthSystem == null || healthSystem.IsDead())
            return;

        if (!IsCompletelyStopped())
        {
            stoppedRecoveryTimer = 0f;
            isRecoveringAfterStop = false;
            return;
        }

        stoppedRecoveryTimer += Time.deltaTime;

        if (stoppedRecoveryTimer < recoveryDelayAfterStop)
            return;

        if (!isRecoveringAfterStop)
        {
            isRecoveringAfterStop = true;
            recoveryStartHealth = healthSystem.GetHealth();
        }

        float targetRecoveryHealth = Mathf.Min(recoveryStartHealth + recoveryAmountTotal, healthSystem.GetHealthMax());

        if (healthSystem.GetHealth() >= targetRecoveryHealth)
            return;

        float recoverPerSecond = recoveryAmountTotal / recoveryDuration;
        float recoverAmount = recoverPerSecond * Time.deltaTime;

        float nextHealth = Mathf.Min(healthSystem.GetHealth() + recoverAmount, targetRecoveryHealth);
        healthSystem.SetHealth(nextHealth);
    }

    private bool IsCompletelyStopped()
    {
        if (navAgent == null)
            return true;

        bool noPathOrArrived =
            !navAgent.pathPending &&
            (!navAgent.hasPath || navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f);

        bool almostNotMoving = navAgent.velocity.sqrMagnitude <= 0.01f;

        return noPathOrArrived && almostNotMoving;
    }

    public void ApplySmokeDebuff(float targetRadius, float duration)
    {
        if (smokeRoutine != null)
            StopCoroutine(smokeRoutine);

        smokeRoutine = StartCoroutine(SmokeDebuffRoutine(targetRadius, duration));
    }

    private IEnumerator SmokeDebuffRoutine(float targetRadius, float duration)
    {
        if (detectionCollider != null)
        {
            Debug.Log($"<color=gray>[Target]</color> 연막탄 적중! 감지 범위를 {targetRadius}로 변경합니다.");

            detectionCollider.radius = targetRadius;

            yield return new WaitForSeconds(duration);

            detectionCollider.radius = normalDetectionRadius;
            Debug.Log("<color=gray>[Target]</color> 연막 효과 종료. 감지 범위 복구.");
        }

        smokeRoutine = null;
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
        stoppedRecoveryTimer = 0f;

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        Debug.Log($"<color=red>[Target]</color> 속박 발동! {duration}초 동안 정지합니다.");

        yield return new WaitForSeconds(duration);

        if (navAgent != null && (healthSystem == null || !healthSystem.IsDead()))
            navAgent.isStopped = false;

        isRooted = false;
        rootRoutine = null;

        Debug.Log("<color=red>[Target]</color> 속박 종료.");

        if (HasAnyThreat() && healthSystem != null && !healthSystem.IsDead())
        {
            TryFleeFromThreats(true);
        }
    }

    public void AddReconReveal()
    {
        reconRevealCount++;
        UpdatePlayerRevealVisual();

        Debug.Log($"<color=yellow>[Target]</color> 플레이어 정찰 노출 시작. count = {reconRevealCount}");
    }

    public void RemoveReconReveal()
    {
        reconRevealCount = Mathf.Max(0, reconRevealCount - 1);
        UpdatePlayerRevealVisual();

        Debug.Log($"<color=yellow>[Target]</color> 플레이어 정찰 노출 해제. count = {reconRevealCount}");
    }

    private void UpdatePlayerRevealVisual()
    {
        if (playerRevealMarker != null)
        {
            playerRevealMarker.SetActive(IsRevealedToPlayer);
        }
    }

    private bool HasAnyThreat()
    {
        CleanupNearbyAgents();

        if (nearbyAgents.Count > 0)
            return true;

        if (HasAnyDecoySignalInRange())
            return true;

        if (HasAnyPhantomThreatInRange())
            return true;

        return false;
    }

    private bool HasAnyDecoySignalInRange()
    {
        if (DecoySignal.ActiveSignals == null)
            return false;

        float sqrRange = decoySignalInfluenceRadius * decoySignalInfluenceRadius;

        for (int i = 0; i < DecoySignal.ActiveSignals.Count; i++)
        {
            DecoySignal signal = DecoySignal.ActiveSignals[i];
            if (signal == null)
                continue;

            if ((signal.Position - transform.position).sqrMagnitude <= sqrRange)
                return true;
        }

        return false;
    }

    private bool HasAnyPhantomThreatInRange()
    {
        if (PhantomThreat.ActiveThreats == null)
            return false;

        float sqrRange = phantomThreatInfluenceRadius * phantomThreatInfluenceRadius;

        for (int i = 0; i < PhantomThreat.ActiveThreats.Count; i++)
        {
            PhantomThreat threat = PhantomThreat.ActiveThreats[i];
            if (threat == null)
                continue;

            if ((threat.Position - transform.position).sqrMagnitude <= sqrRange)
                return true;
        }

        return false;
    }

    private void TryFleeFromThreats(bool forceRepath = false)
    {
        if (navAgent == null || navAgent.isStopped)
            return;

        if (!forceRepath && Time.time - lastRepathTime < repathCooldown)
            return;

        if (TryFindBestSafePosition(out Vector3 bestPosition))
        {
            navAgent.SetDestination(bestPosition);
            lastRepathTime = Time.time;
        }
    }

    private bool TryFindBestSafePosition(out Vector3 bestPosition)
    {
        bestPosition = transform.position;

        Vector3 fleeDirection = CalculateCombinedFleeDirection();
        Vector3 flatFleeDirection = fleeDirection.sqrMagnitude > 0.001f ? fleeDirection.normalized : Vector3.zero;

        float bestScore = float.MinValue;
        bool found = false;

        for (int i = 0; i < safePointSampleCount; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * safeSearchRadius;
            Vector3 rawCandidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (Vector3.Distance(transform.position, rawCandidate) < safePointMinDistance)
                continue;

            if (!NavMesh.SamplePosition(rawCandidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 candidate = hit.position;

            if (!IsReachable(candidate))
                continue;

            float score = EvaluateSafePoint(candidate, flatFleeDirection);

            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = candidate;
                found = true;
            }
        }

        return found;
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

    private float EvaluateSafePoint(Vector3 candidate, Vector3 fleeDirection)
    {
        float score = 0f;

        score += GetDistanceScoreFromAgents(candidate) * 3f;
        score += GetDistanceScoreFromDecoys(candidate) * 1.5f;
        score += GetDistanceScoreFromPhantoms(candidate) * 2f;

        float moveDistance = Vector3.Distance(transform.position, candidate);
        score += moveDistance * 0.5f;

        if (fleeDirection != Vector3.zero)
        {
            Vector3 toCandidate = (candidate - transform.position).normalized;
            float alignment = Vector3.Dot(toCandidate, fleeDirection);
            score += alignment * fleeDirectionBias;
        }

        return score;
    }

    private float GetDistanceScoreFromAgents(Vector3 candidate)
    {
        float score = 0f;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            float distance = Vector3.Distance(candidate, agent.position);
            score += distance;
        }

        return score;
    }

    private float GetDistanceScoreFromDecoys(Vector3 candidate)
    {
        float score = 0f;

        if (DecoySignal.ActiveSignals == null)
            return score;

        for (int i = 0; i < DecoySignal.ActiveSignals.Count; i++)
        {
            DecoySignal signal = DecoySignal.ActiveSignals[i];
            if (signal == null)
                continue;

            float distance = Vector3.Distance(candidate, signal.Position);
            score += distance;
        }

        return score;
    }

    private float GetDistanceScoreFromPhantoms(Vector3 candidate)
    {
        float score = 0f;

        if (PhantomThreat.ActiveThreats == null)
            return score;

        for (int i = 0; i < PhantomThreat.ActiveThreats.Count; i++)
        {
            PhantomThreat threat = PhantomThreat.ActiveThreats[i];
            if (threat == null)
                continue;

            float distance = Vector3.Distance(candidate, threat.Position);
            score += distance * threat.ThreatWeight;
        }

        return score;
    }

    private Vector3 CalculateCombinedFleeDirection()
    {
        Vector3 combinedFleeDirection = Vector3.zero;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];

            if (agent == null)
            {
                nearbyAgents.RemoveAt(i);
                continue;
            }

            Vector3 awayFromAgent = transform.position - agent.position;
            float distance = awayFromAgent.magnitude;
            combinedFleeDirection += awayFromAgent.normalized / (distance + 0.1f);
        }

        if (DecoySignal.ActiveSignals != null)
        {
            float sqrRange = decoySignalInfluenceRadius * decoySignalInfluenceRadius;

            for (int i = 0; i < DecoySignal.ActiveSignals.Count; i++)
            {
                DecoySignal signal = DecoySignal.ActiveSignals[i];
                if (signal == null)
                    continue;

                Vector3 awayFromSignal = transform.position - signal.Position;
                float sqrDistance = awayFromSignal.sqrMagnitude;

                if (sqrDistance > sqrRange)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                combinedFleeDirection += awayFromSignal.normalized * decoySignalWeight / (distance + 0.1f);
            }
        }

        if (PhantomThreat.ActiveThreats != null)
        {
            float sqrRange = phantomThreatInfluenceRadius * phantomThreatInfluenceRadius;

            for (int i = 0; i < PhantomThreat.ActiveThreats.Count; i++)
            {
                PhantomThreat threat = PhantomThreat.ActiveThreats[i];
                if (threat == null)
                    continue;

                Vector3 awayFromThreat = transform.position - threat.Position;
                float sqrDistance = awayFromThreat.sqrMagnitude;

                if (sqrDistance > sqrRange)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                combinedFleeDirection += awayFromThreat.normalized * threat.ThreatWeight / (distance + 0.1f);
            }
        }

        return combinedFleeDirection;
    }

    private void CleanupNearbyAgents()
    {
        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            if (nearbyAgents[i] == null)
                nearbyAgents.RemoveAt(i);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & agentLayer) != 0)
        {
            if (!nearbyAgents.Contains(other.transform))
            {
                nearbyAgents.Add(other.transform);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & agentLayer) != 0)
        {
            if (nearbyAgents.Contains(other.transform))
            {
                nearbyAgents.Remove(other.transform);
            }
        }
    }
}