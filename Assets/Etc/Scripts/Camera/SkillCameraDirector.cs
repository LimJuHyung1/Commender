using System.Collections;
using UnityEngine;

public class SkillCameraDirector : MonoBehaviour
{
    private struct CameraState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Size;

        public CameraState(Vector3 position, Quaternion rotation, float size)
        {
            Position = position;
            Rotation = rotation;
            Size = size;
        }
    }

    private struct CameraShot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Size;
        public float MoveTime;
        public float HoldTime;

        public CameraShot(
            Vector3 position,
            Quaternion rotation,
            float size,
            float moveTime,
            float holdTime)
        {
            Position = position;
            Rotation = rotation;
            Size = size;
            MoveTime = moveTime;
            HoldTime = holdTime;
        }
    }

    private static SkillCameraDirector activeDirector;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private AgentCameraFollow cameraFollow;
    [SerializeField] private CaptureSequenceController captureSequenceController;
    [SerializeField] private CameraOcclusionFader cameraOcclusionFader;

    [Header("Option")]
    [SerializeField] private bool enableSkillCamera = true;
    [SerializeField] private bool enforceSingleDirector = true;
    [SerializeField] private bool ignoreRequestWhileCaptureSequence = true;
    [SerializeField] private bool dropRequestWhilePlaying = true;
    [SerializeField] private bool showRejectedRequestLog = false;
    [SerializeField] private float globalCooldown = 2f;
    [SerializeField] private int maxAcceptedRequestsPerFrame = 1;

    [Header("Return")]
    [SerializeField] private float returnMoveTime = 0.35f;
    [SerializeField] private bool clearFocusForObjectOnly = true;
    [SerializeField] private bool skipManualReturnForObjectOnly = true;

    [Header("Character Shot")]
    [SerializeField] private float characterBackDistance = 4.2f;
    [SerializeField] private float characterHeight = 3.2f;
    [SerializeField] private float characterSideOffset = 0.6f;
    [SerializeField] private float characterLookHeight = 0.8f;
    [SerializeField] private float characterSkillSize = 4.2f;

    [Header("Object Shot")]
    [SerializeField] private Vector3 topDownSkillOffset = new Vector3(0f, 18f, 0f);
    [SerializeField] private Vector3 topDownSkillEuler = new Vector3(90f, 0f, 90f);
    [SerializeField] private float objectSkillSize = 5.5f;

    [Header("Follow User Shot")]
    [SerializeField] private float followUserMoveTime = 0.2f;
    [SerializeField] private float followUserDuration = 1.2f;
    [SerializeField] private float followUserReturnTime = 0.35f;
    [SerializeField] private float followUserPositionSmooth = 12f;
    [SerializeField] private float followUserRotationSmooth = 12f;
    [SerializeField] private float followUserSize = 4.2f;

    [Header("Follow Object Shot")]
    [SerializeField] private float followObjectMoveTime = 0.18f;
    [SerializeField] private float followObjectDuration = 0.75f;
    [SerializeField] private float followObjectReturnTime = 0.35f;
    [SerializeField] private float followObjectPositionSmooth = 14f;
    [SerializeField] private float followObjectRotationSmooth = 14f;
    [SerializeField] private float followObjectSize = 4.2f;

    [Header("Timing")]
    [SerializeField] private float userMoveTime = 0.2f;
    [SerializeField] private float userHoldTime = 0.3f;
    [SerializeField] private float objectMoveTime = 0.25f;
    [SerializeField] private float objectHoldTime = 0.45f;
    [SerializeField] private float strongMoveTime = 0.22f;
    [SerializeField] private float strongHoldTime = 0.6f;

    [Header("Slow Motion")]
    [SerializeField] private bool useSlowMotion = true;
    [SerializeField] private float normalSkillTimeScale = 0.35f;
    [SerializeField] private float strongSkillTimeScale = 0.18f;
    [SerializeField] private float normalSlowDuration = 0.18f;
    [SerializeField] private float strongSlowDuration = 0.3f;

    [Header("Wall Collision")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float cameraCollisionRadius = 0.2f;
    [SerializeField] private float cameraWallPadding = 0.45f;
    [SerializeField] private float minimumCameraDistance = 1.2f;

    private Coroutine currentRoutine;

    private bool isPlaying;
    private bool wasCameraFollowEnabledBeforePlay;

    private float nextAllowedTime;
    private int lastAcceptedFrame = -1;
    private int acceptedRequestCountThisFrame;

    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool timeScaleChanged;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (!RegisterActiveDirector())
            return;

        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!RegisterActiveDirector())
            return;

        SkillCameraEventBus.OnSkillCameraRequested += HandleSkillCameraRequested;
    }

    private void OnDisable()
    {
        SkillCameraEventBus.OnSkillCameraRequested -= HandleSkillCameraRequested;

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        ClearOcclusionTarget();
        RestoreCameraFollow();
        RestoreWorldTimeScale();

        isPlaying = false;
        wasCameraFollowEnabledBeforePlay = false;

        if (activeDirector == this)
            activeDirector = null;
    }

    private void OnDestroy()
    {
        if (activeDirector == this)
            activeDirector = null;
    }

    private bool RegisterActiveDirector()
    {
        if (!enforceSingleDirector)
            return true;

        if (activeDirector == null)
        {
            activeDirector = this;
            return true;
        }

        if (activeDirector == this)
            return true;

        enabled = false;

        Debug.LogWarning(
            "[SkillCameraDirector] SkillCameraDirector가 씬에 2개 이상 존재합니다. " +
            "중복 컴포넌트를 비활성화했습니다."
        );

        return false;
    }

    private void HandleSkillCameraRequested(SkillCameraRequest request)
    {
        if (request == null)
            return;

        TryPlaySkillCamera(
            request.Mode,
            request.User,
            request.ObjectTarget,
            request.HasFocusPoint,
            request.FocusPoint
        );
    }

    public bool TryPlaySkillCamera(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget = null,
        bool hasExplicitFocusPoint = false,
        Vector3 explicitFocusPoint = default)
    {
        if (!CanPlay(mode, user, objectTarget, hasExplicitFocusPoint))
            return false;

        LockPlayback();

        currentRoutine = StartCoroutine(
            PlaySkillCameraRoutine(
                mode,
                user,
                objectTarget,
                hasExplicitFocusPoint,
                explicitFocusPoint
            )
        );

        return true;
    }

    public IEnumerator PlaySkillCameraAndWait(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget = null,
        bool hasExplicitFocusPoint = false,
        Vector3 explicitFocusPoint = default)
    {
        if (!CanPlay(mode, user, objectTarget, hasExplicitFocusPoint))
            yield break;

        LockPlayback();

        yield return PlaySkillCameraRoutine(
            mode,
            user,
            objectTarget,
            hasExplicitFocusPoint,
            explicitFocusPoint
        );
    }

    public IEnumerator ForcePlaySkillCameraAndWait(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget = null,
        bool hasExplicitFocusPoint = false,
        Vector3 explicitFocusPoint = default)
    {
        ResolveReferences();

        if (!enableSkillCamera)
            yield break;

        if (mode == SkillCameraFocusMode.None)
            yield break;

        if (!IsValidRequest(mode, user, objectTarget, hasExplicitFocusPoint))
            yield break;

        if (mainCamera == null)
            yield break;

        CancelCurrentPlaybackForForce();

        LockPlayback();

        yield return PlaySkillCameraRoutine(
            mode,
            user,
            objectTarget,
            hasExplicitFocusPoint,
            explicitFocusPoint
        );
    }

    private bool CanPlay(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint = false)
    {
        if (!enableSkillCamera)
            return Reject("스킬 카메라 기능이 꺼져 있습니다.");

        if (mode == SkillCameraFocusMode.None)
            return Reject("카메라 모드가 None입니다.");

        if (!IsValidRequest(mode, user, objectTarget, hasExplicitFocusPoint))
            return Reject("카메라 요청 대상이 올바르지 않습니다.");

        if (dropRequestWhilePlaying && isPlaying)
            return Reject("이미 스킬 카메라 연출이 재생 중입니다.");

        if (Time.unscaledTime < nextAllowedTime)
            return Reject("스킬 카메라 쿨타임 중입니다.");

        if (!CanAcceptRequestInThisFrame())
            return Reject("같은 프레임에 너무 많은 카메라 요청이 들어왔습니다.");

        ResolveReferences();

        if (mainCamera == null)
            return Reject("메인 카메라를 찾지 못했습니다.");

        if (ignoreRequestWhileCaptureSequence &&
            captureSequenceController != null &&
            captureSequenceController.IsPlaying)
        {
            return Reject("포획 연출 중이므로 스킬 카메라 요청을 무시했습니다.");
        }

        return true;
    }

    private bool IsValidRequest(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint = false)
    {
        switch (mode)
        {
            case SkillCameraFocusMode.UserOnly:
                return user != null;

            case SkillCameraFocusMode.FollowUser:
                return user != null;

            case SkillCameraFocusMode.ObjectOnly:
                return objectTarget != null || hasExplicitFocusPoint;

            case SkillCameraFocusMode.FollowObject:
                return objectTarget != null;

            case SkillCameraFocusMode.UserAndObject:
                return user != null || objectTarget != null || hasExplicitFocusPoint;

            case SkillCameraFocusMode.StrongTargetEvent:
                return user != null;

            default:
                return false;
        }
    }

    private bool CanAcceptRequestInThisFrame()
    {
        int currentFrame = Time.frameCount;

        if (lastAcceptedFrame != currentFrame)
        {
            lastAcceptedFrame = currentFrame;
            acceptedRequestCountThisFrame = 0;
        }

        int limit = Mathf.Max(1, maxAcceptedRequestsPerFrame);

        if (acceptedRequestCountThisFrame >= limit)
            return false;

        return true;
    }

    private void LockPlayback()
    {
        isPlaying = true;
        nextAllowedTime = Time.unscaledTime + Mathf.Max(0f, globalCooldown);

        int currentFrame = Time.frameCount;

        if (lastAcceptedFrame != currentFrame)
        {
            lastAcceptedFrame = currentFrame;
            acceptedRequestCountThisFrame = 0;
        }

        acceptedRequestCountThisFrame++;
    }

    private bool Reject(string reason)
    {
        if (showRejectedRequestLog)
            Debug.Log($"[SkillCameraDirector] 카메라 요청 무시: {reason}");

        return false;
    }

    private IEnumerator PlaySkillCameraRoutine(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint = false,
        Vector3 explicitFocusPoint = default)
    {
        bool objectOnlyRequest = IsObjectOnlyRequest(
            mode,
            user,
            objectTarget,
            hasExplicitFocusPoint
        );

        if (objectOnlyRequest &&
            clearFocusForObjectOnly &&
            cameraFollow != null &&
            cameraFollow.HasFocusedAgent)
        {
            cameraFollow.ClearFocusAgent();
        }

        CameraState originalState = CaptureCurrentState();

        wasCameraFollowEnabledBeforePlay = cameraFollow != null && cameraFollow.enabled;

        if (cameraFollow != null)
            cameraFollow.enabled = false;

        ApplyOcclusionTarget(
            mode,
            user,
            objectTarget,
            hasExplicitFocusPoint,
            explicitFocusPoint
        );

        float slowScale = GetSlowTimeScale(mode);
        float slowDuration = GetSlowDuration(mode);

        if (useSlowMotion)
            ApplyWorldSlowMotion(slowScale);

        float slowEndTime = Time.unscaledTime + slowDuration;

        if (mode == SkillCameraFocusMode.FollowUser)
        {
            yield return PlayFollowUserShot(user, originalState, slowEndTime);
            FinishPlayback();
            yield break;
        }

        if (mode == SkillCameraFocusMode.FollowObject)
        {
            yield return PlayFollowObjectShot(objectTarget, originalState, slowEndTime);
            FinishPlayback();
            yield break;
        }

        CameraShot[] shots = BuildShots(
            mode,
            user,
            objectTarget,
            hasExplicitFocusPoint,
            explicitFocusPoint
        );

        for (int i = 0; i < shots.Length; i++)
        {
            if (timeScaleChanged && Time.unscaledTime >= slowEndTime)
                RestoreWorldTimeScale();

            yield return MoveCameraToShot(shots[i]);
        }

        RestoreWorldTimeScale();

        if (!objectOnlyRequest || !skipManualReturnForObjectOnly)
        {
            yield return MoveCameraToState(originalState, returnMoveTime);
        }

        FinishPlayback();
    }

    private IEnumerator PlayFollowUserShot(
        Transform target,
        CameraState originalState,
        float slowEndTime)
    {
        if (target == null)
            yield break;

        CameraShot firstShot = CreateCharacterShot(
            target,
            followUserSize,
            followUserMoveTime,
            0f
        );

        yield return MoveCameraToShot(firstShot);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, followUserDuration);

        while (elapsed < duration)
        {
            if (target == null)
                break;

            if (timeScaleChanged && Time.unscaledTime >= slowEndTime)
                RestoreWorldTimeScale();

            elapsed += Time.unscaledDeltaTime;

            CameraShot currentShot = CreateCharacterShot(
                target,
                followUserSize,
                0f,
                0f
            );

            float positionT = Mathf.Clamp01(followUserPositionSmooth * Time.unscaledDeltaTime);
            float rotationT = Mathf.Clamp01(followUserRotationSmooth * Time.unscaledDeltaTime);

            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                currentShot.Position,
                positionT
            );

            mainCamera.transform.rotation = Quaternion.Slerp(
                mainCamera.transform.rotation,
                currentShot.Rotation,
                rotationT
            );

            SetCameraSize(
                Mathf.Lerp(
                    GetCameraSize(),
                    currentShot.Size,
                    positionT
                )
            );

            yield return null;
        }

        RestoreWorldTimeScale();

        yield return MoveCameraToState(
            originalState,
            followUserReturnTime
        );
    }

    private IEnumerator PlayFollowObjectShot(
        Transform target,
        CameraState originalState,
        float slowEndTime)
    {
        if (target == null)
            yield break;

        CameraShot firstShot = CreateTopDownShot(
            target,
            followObjectSize,
            followObjectMoveTime,
            0f
        );

        yield return MoveCameraToShot(firstShot);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, followObjectDuration);

        while (elapsed < duration)
        {
            if (target == null)
                break;

            if (timeScaleChanged && Time.unscaledTime >= slowEndTime)
                RestoreWorldTimeScale();

            elapsed += Time.unscaledDeltaTime;

            CameraShot currentShot = CreateTopDownShot(
                target,
                followObjectSize,
                0f,
                0f
            );

            float positionT = Mathf.Clamp01(followObjectPositionSmooth * Time.unscaledDeltaTime);
            float rotationT = Mathf.Clamp01(followObjectRotationSmooth * Time.unscaledDeltaTime);

            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                currentShot.Position,
                positionT
            );

            mainCamera.transform.rotation = Quaternion.Slerp(
                mainCamera.transform.rotation,
                currentShot.Rotation,
                rotationT
            );

            SetCameraSize(
                Mathf.Lerp(
                    GetCameraSize(),
                    currentShot.Size,
                    positionT
                )
            );

            yield return null;
        }

        RestoreWorldTimeScale();

        yield return MoveCameraToState(
            originalState,
            followObjectReturnTime
        );
    }

    private void FinishPlayback()
    {
        ClearOcclusionTarget();
        RestoreCameraFollow();
        RestoreWorldTimeScale();

        currentRoutine = null;
        isPlaying = false;
        wasCameraFollowEnabledBeforePlay = false;
    }

    private void RestoreCameraFollow()
    {
        if (cameraFollow != null && wasCameraFollowEnabledBeforePlay)
            cameraFollow.enabled = true;
    }

    private void ApplyOcclusionTarget(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint,
        Vector3 explicitFocusPoint)
    {
        if (cameraOcclusionFader == null)
            return;

        if (hasExplicitFocusPoint)
        {
            cameraOcclusionFader.SetManualFocusPoint(explicitFocusPoint);
            return;
        }

        switch (mode)
        {
            case SkillCameraFocusMode.UserOnly:
            case SkillCameraFocusMode.FollowUser:
            case SkillCameraFocusMode.StrongTargetEvent:
                cameraOcclusionFader.SetManualTarget(user);
                break;

            case SkillCameraFocusMode.ObjectOnly:
            case SkillCameraFocusMode.FollowObject:
                cameraOcclusionFader.SetManualTarget(objectTarget);
                break;

            case SkillCameraFocusMode.UserAndObject:
                if (user != null && objectTarget != null)
                {
                    Vector3 centerPoint = (GetFocusPoint(user) + GetFocusPoint(objectTarget)) * 0.5f;
                    cameraOcclusionFader.SetManualFocusPoint(centerPoint);
                }
                else if (objectTarget != null)
                {
                    cameraOcclusionFader.SetManualTarget(objectTarget);
                }
                else if (user != null)
                {
                    cameraOcclusionFader.SetManualTarget(user);
                }
                else
                {
                    cameraOcclusionFader.ClearManualTarget();
                }
                break;

            default:
                cameraOcclusionFader.ClearManualTarget();
                break;
        }
    }

    private void ClearOcclusionTarget()
    {
        if (cameraOcclusionFader == null)
            return;

        cameraOcclusionFader.ClearManualTarget();
    }

    private bool IsObjectOnlyRequest(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint = false)
    {
        if (mode == SkillCameraFocusMode.ObjectOnly &&
            (objectTarget != null || hasExplicitFocusPoint))
        {
            return true;
        }

        if (mode == SkillCameraFocusMode.FollowObject &&
            objectTarget != null)
        {
            return true;
        }

        if (mode == SkillCameraFocusMode.UserAndObject &&
            user == null &&
            (objectTarget != null || hasExplicitFocusPoint))
        {
            return true;
        }

        return false;
    }

    private CameraShot[] BuildShots(
        SkillCameraFocusMode mode,
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint = false,
        Vector3 explicitFocusPoint = default)
    {
        switch (mode)
        {
            case SkillCameraFocusMode.UserOnly:
                return BuildUserOnlyShots(user);

            case SkillCameraFocusMode.ObjectOnly:
                return BuildObjectOnlyShots(
                    objectTarget,
                    hasExplicitFocusPoint,
                    explicitFocusPoint
                );

            case SkillCameraFocusMode.UserAndObject:
                return BuildSinglePriorityShot(
                    user,
                    objectTarget,
                    hasExplicitFocusPoint,
                    explicitFocusPoint
                );

            case SkillCameraFocusMode.StrongTargetEvent:
                return BuildStrongTargetShots(user);

            default:
                return new CameraShot[0];
        }
    }

    private CameraShot[] BuildUserOnlyShots(Transform user)
    {
        if (user == null)
            return new CameraShot[0];

        return new CameraShot[]
        {
            CreateCharacterShot(
                user,
                characterSkillSize,
                userMoveTime,
                userHoldTime
            )
        };
    }

    private CameraShot[] BuildObjectOnlyShots(
        Transform objectTarget,
        bool hasExplicitFocusPoint,
        Vector3 explicitFocusPoint)
    {
        if (hasExplicitFocusPoint)
        {
            return new CameraShot[]
            {
                CreateTopDownShot(
                    explicitFocusPoint,
                    objectSkillSize,
                    objectMoveTime,
                    objectHoldTime
                )
            };
        }

        if (objectTarget == null)
            return new CameraShot[0];

        return new CameraShot[]
        {
            CreateTopDownShot(
                objectTarget,
                objectSkillSize,
                objectMoveTime,
                objectHoldTime
            )
        };
    }

    private CameraShot[] BuildSinglePriorityShot(
        Transform user,
        Transform objectTarget,
        bool hasExplicitFocusPoint,
        Vector3 explicitFocusPoint)
    {
        if (hasExplicitFocusPoint)
        {
            return new CameraShot[]
            {
                CreateTopDownShot(
                    explicitFocusPoint,
                    objectSkillSize,
                    objectMoveTime,
                    objectHoldTime
                )
            };
        }

        if (objectTarget != null)
        {
            return new CameraShot[]
            {
                CreateTopDownShot(
                    objectTarget,
                    objectSkillSize,
                    objectMoveTime,
                    objectHoldTime
                )
            };
        }

        if (user != null)
        {
            return new CameraShot[]
            {
                CreateCharacterShot(
                    user,
                    characterSkillSize,
                    userMoveTime,
                    userHoldTime
                )
            };
        }

        return new CameraShot[0];
    }

    private CameraShot[] BuildStrongTargetShots(Transform target)
    {
        if (target == null)
            return new CameraShot[0];

        return new CameraShot[]
        {
            CreateCharacterShot(
                target,
                characterSkillSize,
                strongMoveTime,
                strongHoldTime
            )
        };
    }

    private CameraShot CreateCharacterShot(
        Transform target,
        float size,
        float moveTime,
        float holdTime)
    {
        Vector3 focusPoint = GetFocusPoint(target);

        Vector3 forward = target.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 lookPoint = focusPoint + Vector3.up * characterLookHeight;

        Vector3 desiredPosition =
            focusPoint
            - forward * characterBackDistance
            + right * characterSideOffset
            + Vector3.up * characterHeight;

        Vector3 resolvedPosition = ResolveCameraPosition(desiredPosition, lookPoint);
        Quaternion rotation = GetLookRotation(resolvedPosition, lookPoint);

        return new CameraShot(
            resolvedPosition,
            rotation,
            size,
            moveTime,
            holdTime
        );
    }

    private CameraShot CreateTopDownShot(
        Transform target,
        float size,
        float moveTime,
        float holdTime)
    {
        Vector3 focusPoint = GetFocusPoint(target);
        Vector3 position = focusPoint + topDownSkillOffset;
        Quaternion rotation = Quaternion.Euler(topDownSkillEuler);

        return new CameraShot(
            position,
            rotation,
            size,
            moveTime,
            holdTime
        );
    }

    private CameraShot CreateTopDownShot(
        Vector3 focusPoint,
        float size,
        float moveTime,
        float holdTime)
    {
        Vector3 position = focusPoint + topDownSkillOffset;
        Quaternion rotation = Quaternion.Euler(topDownSkillEuler);

        return new CameraShot(
            position,
            rotation,
            size,
            moveTime,
            holdTime
        );
    }

    private IEnumerator MoveCameraToShot(CameraShot shot)
    {
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;
        float startSize = GetCameraSize();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, shot.MoveTime);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = SmoothStep(t);

            mainCamera.transform.position =
                Vector3.Lerp(startPosition, shot.Position, smoothT);

            mainCamera.transform.rotation =
                Quaternion.Slerp(startRotation, shot.Rotation, smoothT);

            SetCameraSize(Mathf.Lerp(startSize, shot.Size, smoothT));

            yield return null;
        }

        mainCamera.transform.position = shot.Position;
        mainCamera.transform.rotation = shot.Rotation;
        SetCameraSize(shot.Size);

        if (shot.HoldTime > 0f)
            yield return new WaitForSecondsRealtime(shot.HoldTime);
    }

    private IEnumerator MoveCameraToState(CameraState state, float moveTime)
    {
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;
        float startSize = GetCameraSize();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, moveTime);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = SmoothStep(t);

            mainCamera.transform.position =
                Vector3.Lerp(startPosition, state.Position, smoothT);

            mainCamera.transform.rotation =
                Quaternion.Slerp(startRotation, state.Rotation, smoothT);

            SetCameraSize(Mathf.Lerp(startSize, state.Size, smoothT));

            yield return null;
        }

        mainCamera.transform.position = state.Position;
        mainCamera.transform.rotation = state.Rotation;
        SetCameraSize(state.Size);
    }

    private CameraState CaptureCurrentState()
    {
        return new CameraState(
            mainCamera.transform.position,
            mainCamera.transform.rotation,
            GetCameraSize()
        );
    }

    private void ResolveReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();

        if (cameraFollow == null && mainCamera != null)
            cameraFollow = mainCamera.GetComponent<AgentCameraFollow>();

        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<AgentCameraFollow>();

        if (captureSequenceController == null)
            captureSequenceController = FindFirstObjectByType<CaptureSequenceController>();

        if (cameraOcclusionFader == null && mainCamera != null)
            cameraOcclusionFader = mainCamera.GetComponent<CameraOcclusionFader>();

        if (cameraOcclusionFader == null)
            cameraOcclusionFader = FindFirstObjectByType<CameraOcclusionFader>();
    }

    private void ApplyWorldSlowMotion(float timeScale)
    {
        if (timeScaleChanged)
            return;

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
        Time.fixedDeltaTime = previousFixedDeltaTime * Time.timeScale;

        timeScaleChanged = true;
    }

    private void RestoreWorldTimeScale()
    {
        if (!timeScaleChanged)
            return;

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;

        timeScaleChanged = false;
    }

    private float GetSlowTimeScale(SkillCameraFocusMode mode)
    {
        if (mode == SkillCameraFocusMode.StrongTargetEvent)
            return strongSkillTimeScale;

        return normalSkillTimeScale;
    }

    private float GetSlowDuration(SkillCameraFocusMode mode)
    {
        if (mode == SkillCameraFocusMode.StrongTargetEvent)
            return strongSlowDuration;

        return normalSlowDuration;
    }

    private Vector3 ResolveCameraPosition(Vector3 desiredPosition, Vector3 lookPoint)
    {
        if (wallLayer.value == 0)
            return desiredPosition;

        Vector3 direction = desiredPosition - lookPoint;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return desiredPosition;

        Vector3 normalizedDirection = direction / distance;

        bool blocked = Physics.SphereCast(
            lookPoint,
            cameraCollisionRadius,
            normalizedDirection,
            out RaycastHit hit,
            distance,
            wallLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!blocked)
            return desiredPosition;

        float adjustedDistance = Mathf.Max(
            minimumCameraDistance,
            hit.distance - cameraWallPadding
        );

        return lookPoint + normalizedDirection * adjustedDistance;
    }

    private Vector3 GetFocusPoint(Transform root)
    {
        if (root == null)
            return Vector3.zero;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds bounds = new Bounds(root.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (!renderers[i].enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (hasBounds)
            return bounds.center;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            if (colliders[i].isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        if (hasBounds)
            return bounds.center;

        return root.position + Vector3.up;
    }

    private Quaternion GetLookRotation(Vector3 cameraPosition, Vector3 lookPoint)
    {
        Vector3 direction = lookPoint - cameraPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private float GetCameraSize()
    {
        if (mainCamera.orthographic)
            return mainCamera.orthographicSize;

        return mainCamera.fieldOfView;
    }

    private void SetCameraSize(float size)
    {
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = size;
            return;
        }

        mainCamera.fieldOfView = size;
    }

    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private void CancelCurrentPlaybackForForce()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        ClearOcclusionTarget();
        RestoreCameraFollow();
        RestoreWorldTimeScale();

        isPlaying = false;
        wasCameraFollowEnabledBeforePlay = false;
        nextAllowedTime = 0f;
    }

    public void CancelPlaybackForExternalSequence()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        ClearOcclusionTarget();
        RestoreWorldTimeScale();

        isPlaying = false;
        wasCameraFollowEnabledBeforePlay = false;
        nextAllowedTime = 0f;
    }
}