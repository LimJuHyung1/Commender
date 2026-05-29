using UnityEngine;

public class Reconnaissance : DroneBase
{
    [Header("Recon Skill")]
    [SerializeField] private float flightSpeed = 12f;

    [Header("Curved Flight")]
    [SerializeField] private bool curvedFlightEnabled = false;
    [SerializeField] private float curvedFlightAmplitude = 1.25f;
    [SerializeField] private float curvedFlightWaveLength = 5f;
    [SerializeField] private float curvedFlightMaxStepDistance = 0.25f;

    [Header("Wall Collision")]
    [SerializeField] private LayerMask wallLayerMask;
    [SerializeField] private float wallCollisionRadius = 0.25f;
    [SerializeField] private float wallCollisionYOffset = 0f;
    [SerializeField] private float wallCollisionIgnoreSeconds = 0.1f;

    private Vector3 startCenterPosition;
    private Vector3 flightDirection;
    private Vector3 flightRight;

    private float forwardDistance;
    private float traveledDistance;
    private float wallCollisionEnableTime;

    public float TraveledDistance => traveledDistance;
    public bool CurvedFlightEnabled => curvedFlightEnabled;

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
        forwardDistance = 0f;
        traveledDistance = 0f;
        wallCollisionEnableTime = Time.time + wallCollisionIgnoreSeconds;

        flightDirection = direction;
        flightDirection.y = 0f;

        if (flightDirection.sqrMagnitude <= 0.0001f)
            flightDirection = transform.forward;

        flightDirection.y = 0f;

        if (flightDirection.sqrMagnitude <= 0.0001f)
            flightDirection = Vector3.forward;

        flightDirection.Normalize();

        flightRight = Vector3.Cross(Vector3.up, flightDirection);

        if (flightRight.sqrMagnitude <= 0.0001f)
            flightRight = Vector3.right;

        flightRight.Normalize();

        if (reconFlightSpeed > 0f)
            flightSpeed = reconFlightSpeed;

        InitializeBase(
            observer,
            startCenter,
            droneVisualPosition,
            observationTargetLayer,
            observationRadius,
            0f
        );

        transform.rotation = Quaternion.LookRotation(flightDirection, Vector3.up);
    }

    public void SetCurvedFlightEnabled(bool enabled, float amplitude, float waveLength)
    {
        curvedFlightEnabled = enabled;

        if (amplitude > 0f)
            curvedFlightAmplitude = amplitude;

        if (waveLength > 0f)
            curvedFlightWaveLength = waveLength;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        flightSpeed = Mathf.Max(0.01f, flightSpeed);

        curvedFlightAmplitude = Mathf.Max(0f, curvedFlightAmplitude);
        curvedFlightWaveLength = Mathf.Max(0.01f, curvedFlightWaveLength);
        curvedFlightMaxStepDistance = Mathf.Max(0.05f, curvedFlightMaxStepDistance);

        wallCollisionRadius = Mathf.Max(0.01f, wallCollisionRadius);
        wallCollisionIgnoreSeconds = Mathf.Max(0f, wallCollisionIgnoreSeconds);
    }

    protected override void OnSkillUpdate()
    {
        MoveForward();
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

        if (curvedFlightEnabled)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction);

            if (right.sqrMagnitude <= 0.0001f)
                right = Vector3.right;

            right.Normalize();

            Vector3 previous = from;

            for (int i = 1; i <= 24; i++)
            {
                float distance = i * 0.5f;
                float offset = Mathf.Sin((distance / curvedFlightWaveLength) * Mathf.PI * 2f) * curvedFlightAmplitude;
                Vector3 next = from + direction * distance + right * offset;

                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
        else
        {
            Gizmos.DrawLine(from, from + direction * 5f);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetWallCollisionCheckPosition(transform.position), wallCollisionRadius);
    }

    private void MoveForward()
    {
        float moveDistance = flightSpeed * Time.deltaTime;

        if (moveDistance <= 0f)
            return;

        if (!curvedFlightEnabled)
        {
            MoveStep(moveDistance);
            return;
        }

        float remainingDistance = moveDistance;

        while (remainingDistance > 0f)
        {
            float stepDistance = Mathf.Min(remainingDistance, curvedFlightMaxStepDistance);

            if (!MoveStep(stepDistance))
                return;

            remainingDistance -= stepDistance;
        }
    }

    private bool MoveStep(float forwardMoveDistance)
    {
        Vector3 currentCenter = ObservationCenterPosition;
        float nextForwardDistance = forwardDistance + forwardMoveDistance;

        Vector3 nextCenter = curvedFlightEnabled
            ? GetCurvedCenterPosition(nextForwardDistance)
            : currentCenter + flightDirection * forwardMoveDistance;

        if (ShouldStopByWall(currentCenter, nextCenter))
        {
            if (DebugLog)
                Debug.Log("[Reconnaissance] Wall layer touched. Reconnaissance destroyed.");

            FinishSkill();
            return false;
        }

        forwardDistance = nextForwardDistance;
        traveledDistance += forwardMoveDistance;

        SetObservationCenter(nextCenter);
        SetDroneVisualPositionFromCenter(nextCenter);
        UpdateRotationByMovement(currentCenter, nextCenter);

        return true;
    }

    private Vector3 GetCurvedCenterPosition(float distance)
    {
        float offset = Mathf.Sin((distance / curvedFlightWaveLength) * Mathf.PI * 2f) * curvedFlightAmplitude;

        return startCenterPosition
               + flightDirection * distance
               + flightRight * offset;
    }

    private void UpdateRotationByMovement(Vector3 currentCenter, Vector3 nextCenter)
    {
        Vector3 moveDirection = nextCenter - currentCenter;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
    }

    private bool ShouldStopByWall(Vector3 currentCenter, Vector3 nextCenter)
    {
        if (wallLayerMask.value == 0)
            return false;

        if (Time.time < wallCollisionEnableTime)
            return false;

        Vector3 currentVisualPosition = GetDroneVisualPosition(currentCenter);
        Vector3 nextVisualPosition = GetDroneVisualPosition(nextCenter);

        Vector3 currentCheckPosition = GetWallCollisionCheckPosition(currentVisualPosition);
        Vector3 nextCheckPosition = GetWallCollisionCheckPosition(nextVisualPosition);

        Vector3 castDirection = nextCheckPosition - currentCheckPosition;
        float castDistance = castDirection.magnitude;

        if (castDistance <= 0.0001f)
        {
            return Physics.CheckSphere(
                nextCheckPosition,
                wallCollisionRadius,
                wallLayerMask,
                QueryTriggerInteraction.Ignore
            );
        }

        bool hitDuringMove = Physics.SphereCast(
            currentCheckPosition,
            wallCollisionRadius,
            castDirection.normalized,
            out _,
            castDistance,
            wallLayerMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitDuringMove)
            return true;

        return Physics.CheckSphere(
            nextCheckPosition,
            wallCollisionRadius,
            wallLayerMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private Vector3 GetWallCollisionCheckPosition(Vector3 visualPosition)
    {
        return visualPosition + Vector3.up * wallCollisionYOffset;
    }
}