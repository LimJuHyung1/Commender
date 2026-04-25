using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThreatSettings
{
    public float detectionRadius = 5f;
    public float pursuitMemoryDuration = 4f;
    public float reconDroneWeight = 0.8f;
    public float reconDroneInfluenceRadius = 12f;
    public float hologramInfluenceRadius = 15f;
    public float alarmSensorWeight = 1f;
    public float alarmSensorInfluenceRadius = 12f;
}

[RequireComponent(typeof(SphereCollider))]
public class TargetThreatTracker : MonoBehaviour
{
    [Header("References")]
    public LayerMask agentLayer;
    public GameObject playerRevealMarker;

    [Header("Threat Memory")]
    public float pursuitMemoryDuration = 4f;

    private SphereCollider detectionCollider;
    private readonly List<Transform> nearbyAgents = new List<Transform>();
    private readonly List<RememberedThreat> rememberedThreats = new List<RememberedThreat>();

    private Coroutine smokeRoutine;

    private float defaultDetectionRadius = 5f;
    private float reconDroneWeight = 0.8f;
    private float reconDroneInfluenceRadius = 12f;
    private float hologramInfluenceRadius = 15f;
    private float alarmSensorWeight = 1f;
    private float alarmSensorInfluenceRadius = 12f;

    private int reconRevealCount = 0;

    private class RememberedThreat
    {
        public Transform target;
        public Vector3 lastPosition;
        public float expireTime;
    }

    public bool IsRevealedToPlayer => reconRevealCount > 0;
    public SphereCollider DetectionCollider => detectionCollider;
    public float DefaultDetectionRadius => defaultDetectionRadius;
    public float CurrentDetectionRadius => detectionCollider != null ? detectionCollider.radius : defaultDetectionRadius;

    public float ReconDroneWeight => reconDroneWeight;
    public float ReconDroneInfluenceRadius => reconDroneInfluenceRadius;
    public float HologramInfluenceRadius => hologramInfluenceRadius;
    public float AlarmSensorWeight => alarmSensorWeight;
    public float AlarmSensorInfluenceRadius => alarmSensorInfluenceRadius;

    private void Awake()
    {
        detectionCollider = GetComponent<SphereCollider>();

        if (detectionCollider != null)
        {
            detectionCollider.isTrigger = true;
            defaultDetectionRadius = detectionCollider.radius;
        }

        pursuitMemoryDuration = Mathf.Max(0f, pursuitMemoryDuration);

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
        nearbyAgents.Clear();
        rememberedThreats.Clear();
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
            detectionCollider.radius = Mathf.Max(0f, targetRadius);
            Debug.Log($"[TargetThreatTracker] Smoke debuff applied. Detection radius = {targetRadius}");
        }

        yield return new WaitForSeconds(duration);

        RestoreDetectionRadius();
        smokeRoutine = null;

        Debug.Log("[TargetThreatTracker] Smoke debuff ended. Detection radius restored.");
    }

    public void AddReconReveal()
    {
        reconRevealCount++;
        UpdatePlayerRevealVisual();

        Debug.Log($"[TargetThreatTracker] Recon reveal added. count = {reconRevealCount}");
    }

    public void RemoveReconReveal()
    {
        reconRevealCount = Mathf.Max(0, reconRevealCount - 1);
        UpdatePlayerRevealVisual();

        Debug.Log($"[TargetThreatTracker] Recon reveal removed. count = {reconRevealCount}");
    }

    public bool HasAnyThreat()
    {
        CleanupThreats();

        if (nearbyAgents.Count > 0)
            return true;

        if (rememberedThreats.Count > 0)
            return true;

        if (HasAnyDecoySignalInRange())
            return true;

        if (HasAnyPhantomThreatInRange())
            return true;

        if (HasAnyAlarmSensorInRange())
            return true;

        return false;
    }

    public float GetNearestThreatDistance(Vector3 position, float fallbackDistance = 999f)
    {
        CleanupThreats();

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

        for (int i = rememberedThreats.Count - 1; i >= 0; i--)
        {
            RememberedThreat threat = rememberedThreats[i];
            if (threat == null)
                continue;

            float distance = Vector3.Distance(position, threat.lastPosition);
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

        if (AlarmSensor.ActiveSensors != null)
        {
            for (int i = 0; i < AlarmSensor.ActiveSensors.Count; i++)
            {
                AlarmSensor sensor = AlarmSensor.ActiveSensors[i];
                if (sensor == null || !sensor.IsActive)
                    continue;

                float distance = sensor.GetDistanceFromZone(position);
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
        CleanupThreats();

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
        CleanupThreats();

        Vector3 combinedFleeDirection = Vector3.zero;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            Vector3 awayFromAgent = transform.position - agent.position;
            awayFromAgent.y = 0f;

            float distance = awayFromAgent.magnitude;
            if (distance <= 0.001f)
                continue;

            combinedFleeDirection += awayFromAgent.normalized / (distance + 0.1f);
        }

        for (int i = rememberedThreats.Count - 1; i >= 0; i--)
        {
            RememberedThreat threat = rememberedThreats[i];
            if (threat == null)
                continue;

            if (threat.target != null && nearbyAgents.Contains(threat.target))
                continue;

            Vector3 awayFromRememberedThreat = transform.position - threat.lastPosition;
            awayFromRememberedThreat.y = 0f;

            float distance = awayFromRememberedThreat.magnitude;
            if (distance <= 0.001f)
                continue;

            float remainingTime = Mathf.Max(0f, threat.expireTime - Time.time);
            float memoryWeight = pursuitMemoryDuration <= 0.01f
                ? 0f
                : Mathf.Clamp01(remainingTime / pursuitMemoryDuration);

            combinedFleeDirection += awayFromRememberedThreat.normalized * memoryWeight / (distance + 0.1f);
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
                awayFromSignal.y = 0f;

                float sqrDistance = awayFromSignal.sqrMagnitude;
                if (sqrDistance > sqrRange)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                if (distance <= 0.001f)
                    continue;

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
                awayFromHologram.y = 0f;

                float sqrDistance = awayFromHologram.sqrMagnitude;
                if (sqrDistance > sqrRange)
                    continue;

                float distance = Mathf.Sqrt(sqrDistance);
                if (distance <= 0.001f)
                    continue;

                combinedFleeDirection += awayFromHologram.normalized * hologram.ThreatWeight / (distance + 0.1f);
            }
        }

        if (AlarmSensor.ActiveSensors != null)
        {
            for (int i = 0; i < AlarmSensor.ActiveSensors.Count; i++)
            {
                AlarmSensor sensor = AlarmSensor.ActiveSensors[i];
                if (sensor == null || !sensor.IsActive)
                    continue;

                bool isInAvoidanceRange = sensor.IsPositionInAvoidanceRange(transform.position);
                bool isInInfluenceRange = Vector3.Distance(transform.position, sensor.Position) <= alarmSensorInfluenceRadius;

                if (!isInAvoidanceRange && !isInInfluenceRange)
                    continue;

                Vector3 awayFromSensor = transform.position - sensor.Position;
                awayFromSensor.y = 0f;

                if (awayFromSensor.sqrMagnitude <= 0.001f)
                    awayFromSensor = -transform.forward;

                awayFromSensor.y = 0f;

                if (awayFromSensor.sqrMagnitude <= 0.001f)
                    continue;

                float zoneDistance = Mathf.Max(0.1f, sensor.GetDistanceFromZone(transform.position));
                float penaltyRatio = Mathf.Clamp01(sensor.GetAvoidancePenalty(transform.position) / Mathf.Max(0.01f, sensor.DangerPenalty));
                float weight = Mathf.Max(0f, alarmSensorWeight) * Mathf.Lerp(0.5f, 1.5f, penaltyRatio);

                combinedFleeDirection += awayFromSensor.normalized * weight / (zoneDistance + 0.1f);
            }
        }

        return combinedFleeDirection;
    }

    public float GetDistanceScoreFromAgents(Vector3 candidate)
    {
        CleanupThreats();

        float score = 0f;

        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];
            if (agent == null)
                continue;

            score += Vector3.Distance(candidate, agent.position);
        }

        for (int i = rememberedThreats.Count - 1; i >= 0; i--)
        {
            RememberedThreat threat = rememberedThreats[i];
            if (threat == null)
                continue;

            score += Vector3.Distance(candidate, threat.lastPosition);
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

            score += Vector3.Distance(candidate, signal.Position);
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

            score += Vector3.Distance(candidate, hologram.Position) * hologram.ThreatWeight;
        }

        return score;
    }

    public float GetDistanceScoreFromAlarmSensors(Vector3 candidate)
    {
        float score = 0f;

        if (AlarmSensor.ActiveSensors == null)
            return score;

        for (int i = 0; i < AlarmSensor.ActiveSensors.Count; i++)
        {
            AlarmSensor sensor = AlarmSensor.ActiveSensors[i];
            if (sensor == null || !sensor.IsActive)
                continue;

            score += sensor.GetDistanceFromZone(candidate) * alarmSensorWeight;
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

    public bool HasAnyAlarmSensorInRange()
    {
        if (AlarmSensor.ActiveSensors == null)
            return false;

        for (int i = 0; i < AlarmSensor.ActiveSensors.Count; i++)
        {
            AlarmSensor sensor = AlarmSensor.ActiveSensors[i];
            if (sensor == null || !sensor.IsActive)
                continue;

            if (sensor.IsPositionInAvoidanceRange(transform.position))
                return true;

            if (Vector3.Distance(transform.position, sensor.Position) <= alarmSensorInfluenceRadius)
                return true;
        }

        return false;
    }

    public bool IsInsideActiveAlarmSensor(Vector3 position)
    {
        if (AlarmSensor.ActiveSensors == null)
            return false;

        for (int i = 0; i < AlarmSensor.ActiveSensors.Count; i++)
        {
            AlarmSensor sensor = AlarmSensor.ActiveSensors[i];
            if (sensor == null || !sensor.IsActive)
                continue;

            if (sensor.IsPositionInside(position))
                return true;
        }

        return false;
    }

    public float GetAlarmSensorPenalty(Vector3 position)
    {
        float penalty = 0f;

        if (AlarmSensor.ActiveSensors == null)
            return penalty;

        for (int i = 0; i < AlarmSensor.ActiveSensors.Count; i++)
        {
            AlarmSensor sensor = AlarmSensor.ActiveSensors[i];
            if (sensor == null || !sensor.IsActive)
                continue;

            float sensorPenalty = sensor.GetAvoidancePenalty(position);

            if (sensorPenalty <= 0f)
            {
                float centerDistance = Vector3.Distance(position, sensor.Position);
                if (centerDistance > alarmSensorInfluenceRadius)
                    continue;

                float ratio = 1f - Mathf.Clamp01(centerDistance / Mathf.Max(0.01f, alarmSensorInfluenceRadius));
                sensorPenalty = ratio * sensor.DangerPenalty * 0.35f;
            }

            penalty += sensorPenalty * alarmSensorWeight;
        }

        return penalty;
    }

    public void ApplySettings(ThreatSettings settings)
    {
        if (settings == null)
            return;

        defaultDetectionRadius = Mathf.Max(0f, settings.detectionRadius);
        pursuitMemoryDuration = Mathf.Max(0f, settings.pursuitMemoryDuration);
        reconDroneWeight = Mathf.Max(0f, settings.reconDroneWeight);
        reconDroneInfluenceRadius = Mathf.Max(0f, settings.reconDroneInfluenceRadius);
        hologramInfluenceRadius = Mathf.Max(0f, settings.hologramInfluenceRadius);
        alarmSensorWeight = Mathf.Max(0f, settings.alarmSensorWeight);
        alarmSensorInfluenceRadius = Mathf.Max(0f, settings.alarmSensorInfluenceRadius);

        if (smokeRoutine == null)
            RestoreDetectionRadius();
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

        CleanupThreats();

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

    private void CleanupThreats()
    {
        for (int i = nearbyAgents.Count - 1; i >= 0; i--)
        {
            Transform agent = nearbyAgents[i];

            if (agent == null)
            {
                nearbyAgents.RemoveAt(i);
                continue;
            }

            RememberThreat(agent);
        }

        for (int i = rememberedThreats.Count - 1; i >= 0; i--)
        {
            RememberedThreat threat = rememberedThreats[i];

            if (threat == null)
            {
                rememberedThreats.RemoveAt(i);
                continue;
            }

            if (threat.target != null)
                threat.lastPosition = threat.target.position;

            if (Time.time > threat.expireTime)
                rememberedThreats.RemoveAt(i);
        }
    }

    private void RememberThreat(Transform threat)
    {
        if (threat == null)
            return;

        RememberedThreat remembered = FindRememberedThreat(threat);

        if (remembered == null)
        {
            remembered = new RememberedThreat();
            rememberedThreats.Add(remembered);
        }

        remembered.target = threat;
        remembered.lastPosition = threat.position;
        remembered.expireTime = Time.time + pursuitMemoryDuration;
    }

    private RememberedThreat FindRememberedThreat(Transform threat)
    {
        for (int i = 0; i < rememberedThreats.Count; i++)
        {
            RememberedThreat remembered = rememberedThreats[i];

            if (remembered != null && remembered.target == threat)
                return remembered;
        }

        return null;
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

        Transform agent = other.transform;

        if (!nearbyAgents.Contains(agent))
            nearbyAgents.Add(agent);

        RememberThreat(agent);
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & agentLayer) == 0)
            return;

        Transform agent = other.transform;

        if (!nearbyAgents.Contains(agent))
            nearbyAgents.Add(agent);

        RememberThreat(agent);
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & agentLayer) == 0)
            return;

        Transform agent = other.transform;

        if (nearbyAgents.Contains(agent))
            nearbyAgents.Remove(agent);

        RememberThreat(agent);
    }
}