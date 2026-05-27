using UnityEngine;

public class Drone : DroneBase
{
    [Header("Drone Skill")]
    [SerializeField] private bool followTargetAfterDetection = false;

    private TargetController trackedTarget;

    public bool IsFollowingTarget => trackedTarget != null;

    public void Initialize(
        Observer observer,
        Vector3 observationCenter,
        Vector3 droneVisualPosition,
        LayerMask observationTargetLayer,
        float observationRadius,
        float observationDuration,
        bool enableTargetFollowAfterDetection = false)
    {
        trackedTarget = null;
        followTargetAfterDetection = enableTargetFollowAfterDetection;

        InitializeBase(
            observer,
            observationCenter,
            droneVisualPosition,
            observationTargetLayer,
            observationRadius,
            observationDuration
        );
    }

    protected override void OnSkillUpdate()
    {
        UpdateTrackedTargetFollow();
    }

    protected override void OnSkillStopped()
    {
        trackedTarget = null;
    }

    protected override void OnTargetDetected(TargetController target)
    {
        TryStartTargetFollow(target);
    }

    private void UpdateTrackedTargetFollow()
    {
        if (!followTargetAfterDetection)
            return;

        if (trackedTarget == null || !trackedTarget.gameObject.activeInHierarchy)
        {
            trackedTarget = null;
            return;
        }

        Vector3 centerPosition = trackedTarget.transform.position;

        SetObservationCenter(centerPosition);
        SetDroneVisualPositionFromCenter(centerPosition);
    }

    private void TryStartTargetFollow(TargetController target)
    {
        if (!followTargetAfterDetection)
            return;

        if (trackedTarget != null)
            return;

        if (target == null)
            return;

        trackedTarget = target;

        if (DebugLog)
            Debug.Log($"[Drone] Target tracking started: {target.name}");
    }
}