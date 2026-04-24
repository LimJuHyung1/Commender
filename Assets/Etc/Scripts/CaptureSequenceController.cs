using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CaptureSequenceController : MonoBehaviour
{
    private struct CameraShot
    {
        public Vector3 position;
        public Quaternion rotation;
        public float cameraSize;
        public float moveTime;
        public float holdTime;

        public CameraShot(Vector3 position, Quaternion rotation, float cameraSize, float moveTime, float holdTime)
        {
            this.position = position;
            this.rotation = rotation;
            this.cameraSize = cameraSize;
            this.moveTime = moveTime;
            this.holdTime = holdTime;
        }
    }

    private const float CaptureWorldTimeScale = 0.08f;

    private const float OverviewMoveTime = 0.45f;
    private const float OverviewHoldTime = 0.75f;

    private const float TargetMoveTime = 0.45f;
    private const float TargetHoldTime = 0.95f;

    private const float ResultMoveTime = 0.45f;
    private const float ResultHoldTime = 0.35f;

    private const float OverviewCameraSize = 5.8f;
    private const float TargetCameraSize = 2.7f;
    private const float ResultCameraSize = 6.4f;

    private const float CameraCollisionRadius = 0.2f;
    private const float CameraWallPadding = 0.45f;
    private const float MinimumCameraDistance = 1.2f;

    private Camera mainCamera;
    private AgentCameraFollow cameraFollow;
    private bool isPlaying;

    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool timeScaleChanged;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        RestoreWorldTimeScale();
    }

    public IEnumerator PlayCaptureSequence(AgentController catchingAgent, GameObject capturedTarget)
    {
        if (isPlaying)
            yield break;

        isPlaying = true;

        ResolveReferences();

        if (mainCamera == null)
        {
            Debug.LogWarning("[CaptureSequenceController] 사용할 카메라를 찾지 못했습니다.");
            isPlaying = false;
            yield break;
        }

        if (capturedTarget == null)
        {
            Debug.LogWarning("[CaptureSequenceController] capturedTarget이 없습니다.");
            isPlaying = false;
            yield break;
        }

        if (catchingAgent == null)
            catchingAgent = FindNearestAgent(capturedTarget.transform.position);

        ApplyWorldSlowMotion();
        StopAllNavMeshAgents();

        if (catchingAgent != null)
            FaceTarget(catchingAgent.transform, capturedTarget.transform.position);

        Vector3 targetLookDirection = catchingAgent != null
            ? catchingAgent.transform.position
            : mainCamera.transform.position;

        FaceTarget(capturedTarget.transform, targetLookDirection);

        if (cameraFollow != null)
            cameraFollow.enabled = false;

        CameraShot[] shots = BuildCameraShots(catchingAgent, capturedTarget.transform);

        for (int i = 0; i < shots.Length; i++)
        {
            yield return MoveCameraToShot(shots[i]);
        }

        RestoreWorldTimeScale();

        isPlaying = false;
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
    }

    private void ApplyWorldSlowMotion()
    {
        if (timeScaleChanged)
            return;

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = CaptureWorldTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime * CaptureWorldTimeScale;

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

    private CameraShot[] BuildCameraShots(AgentController catchingAgent, Transform capturedTarget)
    {
        Vector3 targetPoint = GetFocusPoint(capturedTarget);

        Vector3 agentPoint = catchingAgent != null
            ? GetFocusPoint(catchingAgent.transform)
            : targetPoint - capturedTarget.forward * 2f;

        Vector3 centerPoint = (agentPoint + targetPoint) * 0.5f;
        Vector3 direction = GetFlatDirection(agentPoint, targetPoint, capturedTarget.forward);
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;

        Vector3 overviewLook = centerPoint + Vector3.up * 0.8f;
        Vector3 overviewPosition =
            centerPoint
            - direction * 5.8f
            + right * 1.3f
            + Vector3.up * 6.6f;

        Vector3 targetLook = targetPoint + Vector3.up * 0.85f;
        Vector3 targetPosition =
            targetPoint
            + direction * 2.7f
            - right * 0.65f
            + Vector3.up * 1.9f;

        Vector3 resultLook = centerPoint + Vector3.up * 0.8f;
        Vector3 resultPosition =
            centerPoint
            - direction * 6.4f
            + Vector3.up * 6.0f;

        return new CameraShot[]
        {
            CreateShot(overviewPosition, overviewLook, OverviewCameraSize, OverviewMoveTime, OverviewHoldTime),
            CreateShot(targetPosition, targetLook, TargetCameraSize, TargetMoveTime, TargetHoldTime),
            CreateShot(resultPosition, resultLook, ResultCameraSize, ResultMoveTime, ResultHoldTime)
        };
    }

    private CameraShot CreateShot(Vector3 desiredPosition, Vector3 lookPoint, float cameraSize, float moveTime, float holdTime)
    {
        Vector3 resolvedPosition = ResolveCameraPosition(desiredPosition, lookPoint);
        Quaternion rotation = GetLookRotation(resolvedPosition, lookPoint);

        return new CameraShot(
            resolvedPosition,
            rotation,
            cameraSize,
            moveTime,
            holdTime
        );
    }

    private IEnumerator MoveCameraToShot(CameraShot shot)
    {
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;
        float startCameraSize = GetCameraSize();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, shot.moveTime);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            mainCamera.transform.position = Vector3.Lerp(startPosition, shot.position, smoothT);
            mainCamera.transform.rotation = Quaternion.Slerp(startRotation, shot.rotation, smoothT);
            SetCameraSize(Mathf.Lerp(startCameraSize, shot.cameraSize, smoothT));

            yield return null;
        }

        mainCamera.transform.position = shot.position;
        mainCamera.transform.rotation = shot.rotation;
        SetCameraSize(shot.cameraSize);

        if (shot.holdTime > 0f)
            yield return new WaitForSecondsRealtime(shot.holdTime);
    }

    private Vector3 ResolveCameraPosition(Vector3 desiredPosition, Vector3 lookPoint)
    {
        int wallMask = LayerMask.GetMask("Wall");

        if (wallMask == 0)
            return desiredPosition;

        Vector3 direction = desiredPosition - lookPoint;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return desiredPosition;

        Vector3 normalizedDirection = direction / distance;

        bool blocked = Physics.SphereCast(
            lookPoint,
            CameraCollisionRadius,
            normalizedDirection,
            out RaycastHit hit,
            distance,
            wallMask,
            QueryTriggerInteraction.Ignore
        );

        if (!blocked)
            return desiredPosition;

        float adjustedDistance = Mathf.Max(MinimumCameraDistance, hit.distance - CameraWallPadding);
        return lookPoint + normalizedDirection * adjustedDistance;
    }

    private Vector3 GetFocusPoint(Transform root)
    {
        if (root == null)
            return Vector3.zero;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        if (colliders != null && colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds.center;
        }

        return root.position + Vector3.up;
    }

    private Vector3 GetFlatDirection(Vector3 from, Vector3 to, Vector3 fallbackForward)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = fallbackForward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return direction.normalized;
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

    private void StopAllNavMeshAgents()
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];

            if (agent == null)
                continue;

            if (!agent.enabled)
                continue;

            if (!agent.isOnNavMesh)
                continue;

            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void FaceTarget(Transform actor, Vector3 targetPosition)
    {
        if (actor == null)
            return;

        Vector3 direction = targetPosition - actor.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        actor.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private AgentController FindNearestAgent(Vector3 targetPosition)
    {
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);

        AgentController nearestAgent = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] == null)
                continue;

            float distance = Vector3.SqrMagnitude(agents[i].transform.position - targetPosition);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestAgent = agents[i];
            }
        }

        return nearestAgent;
    }
}