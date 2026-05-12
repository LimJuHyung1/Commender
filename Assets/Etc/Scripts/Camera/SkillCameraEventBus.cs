using UnityEngine;

public enum SkillCameraFocusMode
{
    None,
    UserOnly,
    FollowUser,
    ObjectOnly,
    FollowObject,
    UserAndObject,
    StrongTargetEvent
}

public sealed class SkillCameraRequest
{
    public SkillCameraFocusMode Mode { get; private set; }
    public Transform User { get; private set; }
    public Transform ObjectTarget { get; private set; }
    public bool HasFocusPoint { get; private set; }
    public Vector3 FocusPoint { get; private set; }

    public SkillCameraRequest(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget = null)
    {
        Mode = mode;
        User = user;
        ObjectTarget = objectTarget;
        HasFocusPoint = false;
        FocusPoint = Vector3.zero;
    }

    public SkillCameraRequest(
        SkillCameraFocusMode mode,
        Transform user,
        Vector3 focusPoint)
    {
        Mode = mode;
        User = user;
        ObjectTarget = null;
        HasFocusPoint = true;
        FocusPoint = focusPoint;
    }
}

public static class SkillCameraEventBus
{
    public delegate void SkillCameraRequestedHandler(SkillCameraRequest request);

    public static event SkillCameraRequestedHandler OnSkillCameraRequested;

    public static void Request(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget = null)
    {
        if (mode == SkillCameraFocusMode.None)
            return;

        if (user == null && objectTarget == null)
            return;

        SkillCameraRequest request = new SkillCameraRequest(
            mode,
            user,
            objectTarget
        );

        OnSkillCameraRequested?.Invoke(request);
    }

    public static void RequestAtPosition(
        SkillCameraFocusMode mode,
        Transform user,
        Vector3 focusPoint)
    {
        if (mode == SkillCameraFocusMode.None)
            return;

        SkillCameraRequest request = new SkillCameraRequest(
            mode,
            user,
            focusPoint
        );

        OnSkillCameraRequested?.Invoke(request);
    }
}