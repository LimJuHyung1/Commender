using System;
using UnityEngine;

public class VisionSensor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AgentController owner;
    [SerializeField] private Transform eyePoint;

    [Header("Target Search")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private int maxTargets = 4;

    [Header("Vision Settings")]
    [SerializeField] private float viewRadius = 7.5f;
    [SerializeField, Range(1f, 360f)] private float viewAngle = 60f;
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private float targetEyeHeight = 0.5f;
    [SerializeField] private bool useHorizontalOnly = true;

    [Header("Behaviour")]
    [SerializeField] private bool autoChaseOnSight = true;
    [SerializeField] private bool debugLog = false;

    private bool isSeeingTarget = false;
    private bool wallSightEnabled = false;
    private float checkTimer = 0f;

    private Transform currentSeenTarget;
    private Collider[] targetResults;
    private readonly RaycastHit[] rayHits = new RaycastHit[1];

    public bool IsSeeingTarget => isSeeingTarget;
    public bool IsWallSightEnabled => wallSightEnabled;
    public Transform CurrentSeenTarget => currentSeenTarget;
    public Vector3 LastSeenPosition { get; private set; }
    public float CurrentViewRadius => viewRadius;
    public float CurrentViewAngle => viewAngle;

    public event Action<VisionSensor, bool, Transform> OnVisionChanged;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<AgentController>();

        if (eyePoint == null)
            eyePoint = transform;

        if (maxTargets < 1)
            maxTargets = 1;

        targetResults = new Collider[maxTargets];

        if (owner == null)
            Debug.LogWarning($"[{name}] VisionSensor could not find AgentController in parent.");

        if (owner != null && owner.Stats != null)
            ApplyStats(owner.Stats);
    }

    private void Update()
    {
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

        viewRadius = stats.viewRadius;
        viewAngle = stats.viewAngle;

        if (debugLog)
        {
            Debug.Log($"[{name}] Vision stats applied. radius={viewRadius}, angle={viewAngle}");
        }

        ForceEvaluateVision();
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

    private void EvaluateVision()
    {
        if (owner == null)
        {
            SetSeeingState(false, null);
            return;
        }

        Transform bestTarget = FindBestVisibleTarget();
        SetSeeingState(bestTarget != null, bestTarget);
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
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0)
            return null;

        Transform bestTarget = null;
        float bestSqrDistance = float.MaxValue;

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

            Transform candidate = col.transform;
            Vector3 targetPoint = candidate.position + Vector3.up * targetEyeHeight;
            Vector3 toTarget = targetPoint - origin;

            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance > bestSqrDistance)
                continue;

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

            bestSqrDistance = sqrDistance;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private void SetSeeingState(bool canSee, Transform seenTarget)
    {
        if (canSee && seenTarget != null)
        {
            LastSeenPosition = seenTarget.position;
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
                Debug.Log($"[{name}] Target lost.");

            if (autoChaseOnSight && owner != null && previousTarget != null)
                owner.ClearChaseTarget(previousTarget);
        }

        OnVisionChanged?.Invoke(this, isSeeingTarget, currentSeenTarget);
    }

    private void OnDrawGizmosSelected()
    {
        Transform drawPoint = eyePoint != null ? eyePoint : transform;

        Gizmos.color = wallSightEnabled ? Color.magenta : Color.yellow;
        Gizmos.DrawWireSphere(drawPoint.position, viewRadius);

        Vector3 leftDir = GetDirectionFromAngle(-viewAngle * 0.5f, drawPoint);
        Vector3 rightDir = GetDirectionFromAngle(viewAngle * 0.5f, drawPoint);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(drawPoint.position, drawPoint.position + leftDir * viewRadius);
        Gizmos.DrawLine(drawPoint.position, drawPoint.position + rightDir * viewRadius);

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