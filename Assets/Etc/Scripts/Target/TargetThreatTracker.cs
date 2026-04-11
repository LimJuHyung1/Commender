using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThreatSettings
{
    public float detectionRadius = 5f;
    public float reconDroneWeight = 0.8f;
    public float reconDroneInfluenceRadius = 12f;
    public float hologramInfluenceRadius = 15f;
}

[RequireComponent(typeof(SphereCollider))]
public class TargetThreatTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask agentLayer;
    [SerializeField] private GameObject playerRevealMarker;

    private SphereCollider detectionCollider;
    private readonly List<Transform> nearbyAgents = new List<Transform>();

    private Coroutine smokeRoutine;

    private float defaultDetectionRadius = 5f;
    private float reconDroneWeight = 0.8f;
    private float reconDroneInfluenceRadius = 12f;
    private float hologramInfluenceRadius = 15f;

    private int reconRevealCount = 0;

    public bool IsRevealedToPlayer => reconRevealCount > 0;
    public SphereCollider DetectionCollider => detectionCollider;
    public float DefaultDetectionRadius => defaultDetectionRadius;
    public float CurrentDetectionRadius => detectionCollider != null ? detectionCollider.radius : defaultDetectionRadius;

    public float ReconDroneWeight => reconDroneWeight;
    public float ReconDroneInfluenceRadius => reconDroneInfluenceRadius;
    public float HologramInfluenceRadius => hologramInfluenceRadius;

    private void Awake()
    {
        detectionCollider = GetComponent<SphereCollider>();

        if (detectionCollider != null)
        {
            detectionCollider.isTrigger = true;
            defaultDetectionRadius = detectionCollider.radius;
        }

        UpdatePlayerRevealVisual();
    }

    private void OnDisable()
    {
        if (smokeRoutine != null)
        {
            StopCoroutine(smokeRoutine);
            smokeRoutine = null;
        }

        RestoreDetectionRadius();
        CleanupNearbyAgents();
        UpdatePlayerRevealVisual();
    }

    public void SetDetectionRadius(float radius)
    {
        defaultDetectionRadius = Mathf.Max(0f, radius);

        if (detectionCollider != null)
        {
            detectionCollider.isTrigger = true;
            detectionCollider.radius = defaultDetectionRadius;
        }
    }

    public void RestoreDetectionRadius()
    {
        if (detectionCollider == null)
            return;

        detectionCollider.isTrigger = true;
        detectionCollider.radius = defaultDetectionRadius;
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
            Debug.Log($"<color=gray>[TargetThreatTracker]</color> 연막탄 적중! 감지 범위를 {targetRadius}로 변경합니다.");
            detectionCollider.radius = Mathf.Max(0f, targetRadius);
        }

        yield return new WaitForSeconds(duration);

        RestoreDetectionRadius();
        smokeRoutine = null;

        Debug.Log("<color=gray>[TargetThreatTracker]</color> 연막 효과 종료. 감지 범위 복구.");
    }

    public void AddReconReveal()
    {
        reconRevealCount++;
        UpdatePlayerRevealVisual();

        Debug.Log($"<color=yellow>[TargetThreatTracker]</color> 플레이어 정찰 노출 시작. count = {reconRevealCount}");
    }

    public void RemoveReconReveal()
    {
        reconRevealCount = Mathf.Max(0, reconRevealCount - 1);
        UpdatePlayerRevealVisual();

        Debug.Log($"<color=yellow>[TargetThreatTracker]</color> 플레이어 정찰 노출 해제. count = {reconRevealCount}");
    }

    public bool HasAnyThreat()
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

    public float GetNearestThreatDistance(Vector3 position, float fallbackDistance = 999f)
    {
        CleanupNearbyAgents();

        float nearest = float.MaxValue;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            float distance = Vector3.Distance(position, agent.position);
            if (distance < nearest)
                nearest = distance;
        }

        if (Noisemaker.ActiveNoisemakers != null)
        {
            for (int i = 0; i < Noisemaker.ActiveNoisemakers.Count; i++)
            {
                Noisemaker signal = Noisemaker.ActiveNoisemakers[i];
                if (signal == null)
                    continue;

                float distance = Vector3.Distance(position, signal.Position);
                if (distance < nearest)
                    nearest = distance;
            }
        }

        if (AgentHologram.ActiveHolograms != null)
        {
            for (int i = 0; i < AgentHologram.ActiveHolograms.Count; i++)
            {
                AgentHologram hologram = AgentHologram.ActiveHolograms[i];
                if (hologram == null)
                    continue;

                float distance = Vector3.Distance(position, hologram.Position);
                if (distance < nearest)
                    nearest = distance;
            }
        }

        if (nearest == float.MaxValue)
            return fallbackDistance;

        return nearest;
    }

    public float GetNearestRealAgentDistance(float fallbackDistance = 9999f)
    {
        CleanupNearbyAgents();

        float nearest = float.MaxValue;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            float distance = Vector3.Distance(transform.position, agent.position);
            if (distance < nearest)
                nearest = distance;
        }

        if (nearest == float.MaxValue)
            return fallbackDistance;

        return nearest;
    }

    public Vector3 CalculateCombinedFleeDirection()
    {
        CleanupNearbyAgents();

        Vector3 combinedFleeDirection = Vector3.zero;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            Vector3 awayFromAgent = transform.position - agent.position;
            float distance = awayFromAgent.magnitude;
            combinedFleeDirection += awayFromAgent.normalized / (distance + 0.1f);
        }

        if (Noisemaker.ActiveNoisemakers != null)
        {
            float sqrRange = reconDroneInfluenceRadius * reconDroneInfluenceRadius;

            for (int i = 0; i < Noisemaker.ActiveNoisemakers.Count; i++)
            {
                Noisemaker signal = Noisemaker.ActiveNoisemakers[i];
                if (signal == null)
                    continue;

                Vector3 awayFromSignal = transform.position - signal.Position;
                float sqrDistance = awayFromSignal.sqrMagnitude;

                if (sqrDistance > sqrRange)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                combinedFleeDirection += awayFromSignal.normalized * reconDroneWeight / (distance + 0.1f);
            }
        }

        if (AgentHologram.ActiveHolograms != null)
        {
            float sqrRange = hologramInfluenceRadius * hologramInfluenceRadius;

            for (int i = 0; i < AgentHologram.ActiveHolograms.Count; i++)
            {
                AgentHologram hologram = AgentHologram.ActiveHolograms[i];
                if (hologram == null)
                    continue;

                Vector3 awayFromHologram = transform.position - hologram.Position;
                float sqrDistance = awayFromHologram.sqrMagnitude;

                if (sqrDistance > sqrRange)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                combinedFleeDirection += awayFromHologram.normalized * hologram.ThreatWeight / (distance + 0.1f);
            }
        }

        return combinedFleeDirection;
    }

    public float GetDistanceScoreFromAgents(Vector3 candidate)
    {
        CleanupNearbyAgents();

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

    public float GetDistanceScoreFromDecoys(Vector3 candidate)
    {
        float score = 0f;

        if (Noisemaker.ActiveNoisemakers == null)
            return score;

        for (int i = 0; i < Noisemaker.ActiveNoisemakers.Count; i++)
        {
            Noisemaker signal = Noisemaker.ActiveNoisemakers[i];
            if (signal == null)
                continue;

            float distance = Vector3.Distance(candidate, signal.Position);
            score += distance;
        }

        return score;
    }

    public float GetDistanceScoreFromPhantoms(Vector3 candidate)
    {
        float score = 0f;

        if (AgentHologram.ActiveHolograms == null)
            return score;

        for (int i = 0; i < AgentHologram.ActiveHolograms.Count; i++)
        {
            AgentHologram hologram = AgentHologram.ActiveHolograms[i];
            if (hologram == null)
                continue;

            float distance = Vector3.Distance(candidate, hologram.Position);
            score += distance * hologram.ThreatWeight;
        }

        return score;
    }

    public bool HasAnyDecoySignalInRange()
    {
        if (Noisemaker.ActiveNoisemakers == null)
            return false;

        float sqrRange = reconDroneInfluenceRadius * reconDroneInfluenceRadius;

        for (int i = 0; i < Noisemaker.ActiveNoisemakers.Count; i++)
        {
            Noisemaker signal = Noisemaker.ActiveNoisemakers[i];
            if (signal == null)
                continue;

            if ((signal.Position - transform.position).sqrMagnitude <= sqrRange)
                return true;
        }

        return false;
    }

    public bool HasAnyPhantomThreatInRange()
    {
        if (AgentHologram.ActiveHolograms == null)
            return false;

        float sqrRange = hologramInfluenceRadius * hologramInfluenceRadius;

        for (int i = 0; i < AgentHologram.ActiveHolograms.Count; i++)
        {
            AgentHologram hologram = AgentHologram.ActiveHolograms[i];
            if (hologram == null)
                continue;

            if ((hologram.Position - transform.position).sqrMagnitude <= sqrRange)
                return true;
        }

        return false;
    }

    public void ApplySettings(ThreatSettings settings)
    {
        if (settings == null)
            return;

        defaultDetectionRadius = Mathf.Max(0f, settings.detectionRadius);
        reconDroneWeight = Mathf.Max(0f, settings.reconDroneWeight);
        reconDroneInfluenceRadius = Mathf.Max(0f, settings.reconDroneInfluenceRadius);
        hologramInfluenceRadius = Mathf.Max(0f, settings.hologramInfluenceRadius);

        if (smokeRoutine == null)
            RestoreDetectionRadius();
    }

    private void CleanupNearbyAgents()
    {
        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            if (nearbyAgents[i] == null)
                nearbyAgents.RemoveAt(i);
        }
    }

    public bool TryGetNearestRealAgentBehind(
    Vector3 escapeForward,
    float maxDistance,
    float behindDotThreshold,
    out Transform nearestAgent,
    out float nearestDistance)
    {
        nearestAgent = null;
        nearestDistance = maxDistance;

        CleanupNearbyAgents();

        escapeForward.y = 0f;
        if (escapeForward.sqrMagnitude <= 0.0001f)
            return false;

        escapeForward.Normalize();

        float maxDistanceSqr = maxDistance * maxDistance;
        float bestSqrDistance = float.MaxValue;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            Vector3 toAgent = agent.position - transform.position;
            toAgent.y = 0f;

            float sqrDistance = toAgent.sqrMagnitude;
            if (sqrDistance <= 0.0001f || sqrDistance > maxDistanceSqr)
                continue;

            float dot = Vector3.Dot(escapeForward, toAgent.normalized);

            // dot가 음수일수록 타겟의 뒤쪽
            if (dot > behindDotThreshold)
                continue;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearestAgent = agent;
            }
        }

        if (nearestAgent == null)
            return false;

        nearestDistance = Mathf.Sqrt(bestSqrDistance);
        return true;
    }


    private void UpdatePlayerRevealVisual()
    {
        if (playerRevealMarker != null)
            playerRevealMarker.SetActive(IsRevealedToPlayer);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & agentLayer) == 0)
            return;

        if (!nearbyAgents.Contains(other.transform))
            nearbyAgents.Add(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & agentLayer) == 0)
            return;

        if (nearbyAgents.Contains(other.transform))
            nearbyAgents.Remove(other.transform);
    }
}