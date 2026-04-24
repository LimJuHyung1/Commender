using System;
using UnityEngine;

public class VisionSensor : MonoBehaviour, ISmokeDebuffReceiver
{
    [Header("References")]
    public AgentController owner;
    public Transform eyePoint;

    [Header("Target Search")]
    public LayerMask targetLayer;
    public LayerMask obstacleMask;
    public int maxTargets = 4;

    [Header("Vision Settings")]
    public float viewRadius = 7.5f;
    [Range(1f, 360f)] public float viewAngle = 60f;
    public float checkInterval = 0.1f;
    public float targetEyeHeight = 0.5f;
    public bool useHorizontalOnly = true;

    [Header("Detection Confirm")]
    public float detectionConfirmDuration = 0.3f;

    [Header("Behaviour")]
    public bool autoChaseOnSight = true;
    public bool debugLog = false;

    [Header("Lose Sight")]
    public float loseSightGraceDuration = 0.15f;

    [Header("Smoke Debuff")]
    public float minimumViewRadius = 0.5f;

    private bool isSeeingTarget = false;
    private bool wallSightEnabled = false;
    private float checkTimer = 0f;

    private Transform currentSeenTarget;
    private Transform pendingLostTarget;
    private float loseSightGraceTimer = 0f;

    private Transform candidateTarget;
    private float candidateStartTime = -1f;

    private Collider[] targetResults;
    private readonly RaycastHit[] rayHits = new RaycastHit[1];

    private float currentViewRadius;
    private float smokeDebuffEndTime = -1f;
    private float smokeDebuffRadius = -1f;

    public bool IsSeeingTarget => isSeeingTarget;
    public bool IsWallSightEnabled => wallSightEnabled;
    public Transform CurrentSeenTarget => currentSeenTarget;
    public Vector3 LastSeenPosition { get; private set; }
    public float CurrentViewRadius => currentViewRadius;
    public float CurrentViewAngle => viewAngle;
    public bool IsSmokeDebuffed => smokeDebuffEndTime > Time.time;
    public bool IsOperational => isActiveAndEnabled && gameObject.activeInHierarchy && owner != null;

    public event Action<VisionSensor, bool, Transform> OnVisionChanged;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<AgentController>();

        if (eyePoint == null)
            eyePoint = transform;

        if (maxTargets < 1)
            maxTargets = 1;

        viewRadius = Mathf.Max(minimumViewRadius, viewRadius);
        currentViewRadius = viewRadius;

        targetResults = new Collider[maxTargets];

        if (owner == null)
            Debug.LogWarning($"[{name}] VisionSensor could not find AgentController in parent.");

        if (owner != null && owner.Stats != null)
            ApplyStats(owner.Stats);
    }

    private void OnDisable()
    {
        ClearVisionStateImmediate(true);
    }

    private void Update()
    {
        UpdateSmokeDebuff();
        UpdateLoseSightGrace();

        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f)
            return;

        checkTimer = checkInterval;
        EvaluateVision();
    }

    public void ApplyStats(AgentStatsSO stats)
    {
        if (stats == null)
            return;

        viewRadius = Mathf.Max(minimumViewRadius, stats.viewRadius);
        viewAngle = stats.viewAngle;

        RefreshCurrentViewRadius();

        if (debugLog)
        {
            Debug.Log($"[{name}] Vision stats applied. baseRadius={viewRadius}, currentRadius={currentViewRadius}, angle={viewAngle}");
        }

        ForceEvaluateVision();
    }

    public void ApplySmokeDebuff(float targetRadius, float duration)
    {
        if (duration <= 0f)
            return;

        float clampedBaseRadius = Mathf.Max(minimumViewRadius, viewRadius);
        float clampedTargetRadius = Mathf.Clamp(targetRadius, minimumViewRadius, clampedBaseRadius);

        if (IsSmokeDebuffed)
            smokeDebuffRadius = Mathf.Min(smokeDebuffRadius, clampedTargetRadius);
        else
            smokeDebuffRadius = clampedTargetRadius;

        smokeDebuffEndTime = Mathf.Max(smokeDebuffEndTime, Time.time + duration);

        RefreshCurrentViewRadius();
        ForceEvaluateVision();

        if (debugLog)
        {
            Debug.Log($"[{name}] Smoke debuff applied. radius={currentViewRadius}, duration={duration:F2}");
        }
    }

    public void ForceEvaluateVision()
    {
        EvaluateVision();
    }

    public void SetWallSightEnabled(bool enabled)
    {
        wallSightEnabled = enabled;

        if (debugLog)
        {
            Debug.Log($"[{name}] WallSight {(wallSightEnabled ? "Enabled" : "Disabled")}");
        }

        EvaluateVision();
    }

    public bool CanDirectlySeeTransform(Transform target)
    {
        if (!IsOperational || target == null)
            return false;

        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;
        Vector3 targetPoint = target.position + Vector3.up * targetEyeHeight;
        Vector3 toTarget = targetPoint - origin;

        float sqrDistance = toTarget.sqrMagnitude;
        float radius = CurrentViewRadius;

        if (sqrDistance > radius * radius)
            return false;

        Vector3 forward = eyePoint != null ? eyePoint.forward : transform.forward;
        if (useHorizontalOnly)
            forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.Normalize();

        Vector3 directionForAngle = toTarget;
        if (useHorizontalOnly)
            directionForAngle.y = 0f;

        if (directionForAngle.sqrMagnitude <= 0.0001f)
            return true;

        float angleToTarget = Vector3.Angle(forward, directionForAngle.normalized);
        if (angleToTarget > CurrentViewAngle * 0.5f)
            return false;

        if (!wallSightEnabled)
        {
            float rayDistance = toTarget.magnitude;
            int wallHitCount = Physics.RaycastNonAlloc(
                origin,
                toTarget.normalized,
                rayHits,
                rayDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );

            if (wallHitCount > 0)
                return false;
        }

        return true;
    }

    private void UpdateSmokeDebuff()
    {
        if (!IsSmokeDebuffed && smokeDebuffEndTime >= 0f)
        {
            smokeDebuffEndTime = -1f;
            smokeDebuffRadius = -1f;
            RefreshCurrentViewRadius();
            ForceEvaluateVision();

            if (debugLog)
            {
                Debug.Log($"[{name}] Smoke debuff ended. radius restored to {currentViewRadius}");
            }
        }
    }

    private void RefreshCurrentViewRadius()
    {
        float baseRadius = Mathf.Max(minimumViewRadius, viewRadius);

        if (IsSmokeDebuffed && smokeDebuffRadius > 0f)
            currentViewRadius = Mathf.Clamp(smokeDebuffRadius, minimumViewRadius, baseRadius);
        else
            currentViewRadius = baseRadius;
    }

    private void UpdateLoseSightGrace()
    {
        if (pendingLostTarget == null)
            return;

        loseSightGraceTimer -= Time.deltaTime;

        if (loseSightGraceTimer > 0f)
            return;

        FinalizeLostTarget();
    }

    private void EvaluateVision()
    {
        if (owner == null)
        {
            UpdateDetectionConfirmation(null);
            return;
        }

        Transform bestTarget = FindBestVisibleTarget();
        UpdateDetectionConfirmation(bestTarget);
    }

    private void UpdateDetectionConfirmation(Transform bestTarget)
    {
        if (bestTarget == null)
        {
            candidateTarget = null;
            candidateStartTime = -1f;

            SetSeeingState(false, null);
            return;
        }

        if (isSeeingTarget && currentSeenTarget == bestTarget)
        {
            candidateTarget = null;
            candidateStartTime = -1f;

            SetSeeingState(true, bestTarget);
            return;
        }

        if (candidateTarget != bestTarget)
        {
            candidateTarget = bestTarget;
            candidateStartTime = Time.time;
        }

        float visibleDuration = Time.time - candidateStartTime;

        if (detectionConfirmDuration <= 0f || visibleDuration >= detectionConfirmDuration)
        {
            Transform confirmedTarget = candidateTarget;

            candidateTarget = null;
            candidateStartTime = -1f;

            SetSeeingState(true, confirmedTarget);
        }
    }

    private Transform FindBestVisibleTarget()
    {
        Vector3 origin = eyePoint.position;
        float radius = CurrentViewRadius;
        float angle = CurrentViewAngle;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            radius,
            targetResults,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        if (hitCount <= 0)
            return null;

        Transform bestRealTarget = null;
        float bestRealSqrDistance = float.MaxValue;

        Transform bestTargetHologram = null;
        float bestTargetHologramSqrDistance = float.MaxValue;

        Vector3 forward = eyePoint.forward;
        if (useHorizontalOnly)
            forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.Normalize();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = targetResults[i];
            if (col == null)
                continue;

            Transform candidate = ResolveTargetRoot(col);
            if (candidate == null)
                continue;

            Vector3 targetPoint = candidate.position + Vector3.up * targetEyeHeight;
            Vector3 toTarget = targetPoint - origin;
            float sqrDistance = toTarget.sqrMagnitude;

            Vector3 directionForAngle = toTarget;
            if (useHorizontalOnly)
                directionForAngle.y = 0f;

            if (directionForAngle.sqrMagnitude <= 0.0001f)
                continue;

            float angleToTarget = Vector3.Angle(forward, directionForAngle.normalized);
            if (angleToTarget > angle * 0.5f)
                continue;

            if (!wallSightEnabled)
            {
                float rayDistance = toTarget.magnitude;
                int wallHitCount = Physics.RaycastNonAlloc(
                    origin,
                    toTarget.normalized,
                    rayHits,
                    rayDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore
                );

                if (wallHitCount > 0)
                    continue;
            }

            TargetHologram targetHologram = candidate.GetComponent<TargetHologram>();
            if (targetHologram != null)
            {
                if (sqrDistance < bestTargetHologramSqrDistance)
                {
                    bestTargetHologramSqrDistance = sqrDistance;
                    bestTargetHologram = candidate;
                }

                continue;
            }

            if (sqrDistance < bestRealSqrDistance)
            {
                bestRealSqrDistance = sqrDistance;
                bestRealTarget = candidate;
            }
        }

        if (bestTargetHologram != null)
            return bestTargetHologram;

        return bestRealTarget;
    }

    private Transform ResolveTargetRoot(Collider col)
    {
        if (col == null)
            return null;

        TargetHologram targetHologram = col.GetComponentInParent<TargetHologram>();
        if (targetHologram != null)
            return targetHologram.transform;

        TargetController targetController = col.GetComponentInParent<TargetController>();
        if (targetController != null)
            return targetController.transform;

        return col.transform.root;
    }

    private void SetSeeingState(bool canSee, Transform seenTarget)
    {
        if (canSee && seenTarget != null)
        {
            LastSeenPosition = seenTarget.position;
            CancelPendingLostClear();
        }

        bool sameTarget = currentSeenTarget == seenTarget;
        if (isSeeingTarget == canSee && sameTarget)
            return;

        Transform previousTarget = currentSeenTarget;

        isSeeingTarget = canSee;
        currentSeenTarget = seenTarget;

        if (isSeeingTarget)
        {
            if (debugLog)
                Debug.Log($"[{name}] Target detected: {seenTarget.name}");

            if (autoChaseOnSight && owner != null)
                owner.SetChaseTarget(seenTarget);
        }
        else
        {
            if (debugLog)
                Debug.Log($"[{name}] Target lost. grace={loseSightGraceDuration:F2}s");

            if (autoChaseOnSight && owner != null && previousTarget != null)
                StartLostSightGrace(previousTarget);
        }

        OnVisionChanged?.Invoke(this, isSeeingTarget, currentSeenTarget);
    }

    private void StartLostSightGrace(Transform target)
    {
        pendingLostTarget = target;

        if (loseSightGraceDuration <= 0f)
        {
            FinalizeLostTarget();
            return;
        }

        loseSightGraceTimer = loseSightGraceDuration;
    }

    private void CancelPendingLostClear()
    {
        pendingLostTarget = null;
        loseSightGraceTimer = 0f;
    }

    private void FinalizeLostTarget()
    {
        if (pendingLostTarget == null)
            return;

        Transform targetToClear = pendingLostTarget;

        pendingLostTarget = null;
        loseSightGraceTimer = 0f;

        if (autoChaseOnSight && owner != null)
            owner.ClearChaseTarget(targetToClear);

        if (debugLog)
            Debug.Log($"[{name}] Chase cleared after grace time.");
    }

    private void ClearVisionStateImmediate(bool clearChaseTarget)
    {
        Transform previousTarget = currentSeenTarget;

        pendingLostTarget = null;
        loseSightGraceTimer = 0f;
        checkTimer = 0f;

        candidateTarget = null;
        candidateStartTime = -1f;

        isSeeingTarget = false;
        currentSeenTarget = null;

        if (clearChaseTarget && autoChaseOnSight && owner != null && previousTarget != null)
            owner.ClearChaseTarget(previousTarget);

        OnVisionChanged?.Invoke(this, false, null);
    }

    private void OnDrawGizmosSelected()
    {
        Transform drawPoint = eyePoint != null ? eyePoint : transform;
        float gizmoRadius = Application.isPlaying ? CurrentViewRadius : Mathf.Max(minimumViewRadius, viewRadius);

        Gizmos.color = wallSightEnabled ? Color.magenta : Color.yellow;
        Gizmos.DrawWireSphere(drawPoint.position, gizmoRadius);

        Vector3 leftDir = GetDirectionFromAngle(-viewAngle * 0.5f, drawPoint);
        Vector3 rightDir = GetDirectionFromAngle(viewAngle * 0.5f, drawPoint);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(drawPoint.position, drawPoint.position + leftDir * gizmoRadius);
        Gizmos.DrawLine(drawPoint.position, drawPoint.position + rightDir * gizmoRadius);

        if (Application.isPlaying && currentSeenTarget != null)
        {
            Gizmos.color = isSeeingTarget ? Color.green : Color.red;
            Gizmos.DrawLine(drawPoint.position, currentSeenTarget.position + Vector3.up * targetEyeHeight);
        }
    }

    private Vector3 GetDirectionFromAngle(float angleOffset, Transform basis)
    {
        Vector3 forward = basis.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        float baseAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float finalAngle = baseAngle + angleOffset;

        float rad = finalAngle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }
}