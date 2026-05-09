using UnityEngine;

public enum SkillCameraFocusMode
{
    None,
    UserOnly,
    ObjectOnly,
    UserAndObject,
    StrongTargetEvent
}

public sealed class SkillCameraRequest
{
    public SkillCameraFocusMode Mode { get; private set; }
    public Transform User { get; private set; }
    public Transform ObjectTarget { get; private set; }

    public SkillCameraRequest(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget = null)
    {
        Mode = mode;
        User = user;
        ObjectTarget = objectTarget;
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
}