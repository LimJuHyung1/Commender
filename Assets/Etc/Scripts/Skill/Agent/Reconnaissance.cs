using UnityEngine;

public class Reconnaissance : DroneBase
{
    [Header("Recon Skill")]
    [SerializeField] private float flightSpeed = 12f;
    [SerializeField] private float maxFlightDistance = 18f;
    [SerializeField] private float revealHoldDuration = 2.5f;
    [SerializeField] private bool stopWhenTargetFound = true;

    [Header("Obstacle")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float obstacleCheckRadius = 0.2f;

    private Vector3 startCenterPosition;
    private Vector3 flightDirection;
    private float traveledDistance;

    private bool stoppedByTarget;
    private float stopEndTime;

    public bool IsStoppedByTarget => stoppedByTarget;
    public float TraveledDistance => traveledDistance;

    public void Initialize(
        Observer observer,
        Vector3 startCenter,
        Vector3 droneVisualPosition,
        Vector3 direction,
        LayerMask observationTargetLayer,
        float observationRadius,
        float reconMaxDistance,
        float reconFlightSpeed,
        float targetRevealHoldDuration)
    {
        startCenterPosition = startCenter;
        traveledDistance = 0f;
        stoppedByTarget = false;
        stopEndTime = -1f;

        flightDirection = direction;
        flightDirection.y = 0f;

        if (flightDirection.sqrMagnitude <= 0.0001f)
            flightDirection = transform.forward;

        flightDirection.y = 0f;

        if (flightDirection.sqrMagnitude <= 0.0001f)
            flightDirection = Vector3.forward;

        flightDirection.Normalize();

        if (reconMaxDistance > 0f)
            maxFlightDistance = reconMaxDistance;

        if (reconFlightSpeed > 0f)
            flightSpeed = reconFlightSpeed;

        if (targetRevealHoldDuration > 0f)
            revealHoldDuration = targetRevealHoldDuration;

        float safetyDuration = CalculateSafetyDuration();

        InitializeBase(
            observer,
            startCenter,
            droneVisualPosition,
            observationTargetLayer,
            observationRadius,
            safetyDuration
        );

        transform.rotation = Quaternion.LookRotation(flightDirection, Vector3.up);
    }

    protected override void OnSkillUpdate()
    {
        if (stoppedByTarget)
        {
            if (Time.time >= stopEndTime)
                FinishSkill();

            return;
        }

        MoveForward();
    }

    protected override void OnTargetDetected(TargetController target)
    {
        if (!stopWhenTargetFound)
            return;

        if (stoppedByTarget)
            return;

        if (target == null)
            return;

        stoppedByTarget = true;
        stopEndTime = Time.time + revealHoldDuration;

        if (DebugLog)
            Debug.Log($"[ReconDrone] Target found. Recon drone stopped: {target.name}");
    }

    protected override void DrawAdditionalGizmos()
    {
        Vector3 from = Application.isPlaying ? startCenterPosition : transform.position;
        Vector3 direction = Application.isPlaying ? flightDirection : transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        Gizmos.color = Color.green;
        Gizmos.DrawLine(from, from + direction * maxFlightDistance);
    }

    private void MoveForward()
    {
        float moveDistance = flightSpeed * Time.deltaTime;
        float remainingDistance = maxFlightDistance - traveledDistance;

        if (remainingDistance <= 0f)
        {
            FinishSkill();
            return;
        }

        moveDistance = Mathf.Min(moveDistance, remainingDistance);

        if (ShouldStopByObstacle(moveDistance))
        {
            FinishSkill();
            return;
        }

        Vector3 nextCenter = ObservationCenterPosition + flightDirection * moveDistance;

        traveledDistance += moveDistance;

        SetObservationCenter(nextCenter);
        SetDroneVisualPositionFromCenter(nextCenter);

        if (traveledDistance >= maxFlightDistance)
            FinishSkill();
    }

    private bool ShouldStopByObstacle(float moveDistance)
    {
        if (obstacleMask.value == 0)
            return false;

        Vector3 origin = ObservationCenterPosition + Vector3.up * 0.2f;
        float distance = Mathf.Max(0.01f, moveDistance);

        return Physics.SphereCast(
            origin,
            Mathf.Max(0.01f, obstacleCheckRadius),
            flightDirection,
            out _,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private float CalculateSafetyDuration()
    {
        float safeSpeed = Mathf.Max(0.01f, flightSpeed);
        float flightDuration = maxFlightDistance / safeSpeed;

        return flightDuration + revealHoldDuration + 0.5f;
    }
}