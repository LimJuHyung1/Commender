using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class StopSignal : MonoBehaviour
{
    private const string DefaultRangeIndicatorName = "Quad";
    private const float MinimumRadius = 0.1f;
    private const float MinimumScale = 0.001f;
    private const float AxisEpsilon = 0.0001f;

    [Header("Trigger")]
    [SerializeField] private bool oneShotTrigger = true;
    [SerializeField] private bool destroyAfterTriggered = false;
    [SerializeField] private float destroyDelayAfterTriggered = 0.2f;
    [SerializeField] private bool disableColliderAfterTriggered = false;
    [SerializeField] private GameObject triggerEffectObject;

    [Header("Range Visual")]
    [SerializeField] private Transform rangeIndicatorQuad;
    [SerializeField] private float rangeIndicatorYOffset = 0.03f;
    [SerializeField] private bool hideRangeIndicatorAfterTriggered = false;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private SphereCollider triggerCollider;
    private Rigidbody rigidBody;
    private LayerMask targetLayerMask;

    private float stopDuration = 2f;
    private float configuredRadius = 2f;
    private float configuredLifeTime;
    private bool hasTriggered;
    private bool isConfigured;

    private void Awake()
    {
        CacheComponents();
        SetupTriggerCollider();
        SetupRigidbody();
        SetupTriggerEffect();

        if (triggerCollider != null)
        {
            configuredRadius = Mathf.Max(MinimumRadius, triggerCollider.radius);
            ApplyRangeIndicatorRadius(configuredRadius);
        }
    }

    private void OnValidate()
    {
        destroyDelayAfterTriggered = Mathf.Max(0.05f, destroyDelayAfterTriggered);
        rangeIndicatorYOffset = Mathf.Max(0f, rangeIndicatorYOffset);

        CacheComponents();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            configuredRadius = Mathf.Max(MinimumRadius, triggerCollider.radius);
            ApplyRangeIndicatorRadius(configuredRadius);
        }

        if (rigidBody != null)
        {
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;
        }
    }

    public void Configure(
        float triggerRadius,
        float targetStopDuration,
        float lifeTime,
        LayerMask targetLayer)
    {
        CacheComponents();
        SetupTriggerCollider();
        SetupRigidbody();
        SetupTriggerEffect();

        float safeRadius = Mathf.Max(MinimumRadius, triggerRadius);

        configuredRadius = safeRadius;
        stopDuration = Mathf.Max(0.1f, targetStopDuration);
        configuredLifeTime = Mathf.Max(0f, lifeTime);
        targetLayerMask = targetLayer;
        hasTriggered = false;
        isConfigured = true;

        if (triggerCollider != null)
        {
            triggerCollider.radius = safeRadius;
            triggerCollider.enabled = true;
        }

        ApplyRangeIndicatorRadius(safeRadius);
        ShowRangeIndicator();

        if (configuredLifeTime > 0f)
            Destroy(gameObject, configuredLifeTime);

        StartCoroutine(CheckOverlappingTargetsNextFrame());

        if (debugLog)
        {
            Debug.Log(
                $"[StopSignal] Configured. " +
                $"Radius={safeRadius}, StopDuration={stopDuration}, LifeTime={configuredLifeTime}, " +
                $"QuadWorldSize={GetRangeIndicatorWorldFootprint()}"
            );
        }
    }

    private void CacheComponents()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        if (rigidBody == null)
            rigidBody = GetComponent<Rigidbody>();

        if (rangeIndicatorQuad == null)
            rangeIndicatorQuad = FindChildByName(transform, DefaultRangeIndicatorName);
    }

    private void SetupTriggerCollider()
    {
        if (triggerCollider == null)
            return;

        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
    }

    private void SetupRigidbody()
    {
        if (rigidBody == null)
            return;

        rigidBody.isKinematic = true;
        rigidBody.useGravity = false;
    }

    private void SetupTriggerEffect()
    {
        if (triggerEffectObject != null)
            triggerEffectObject.SetActive(false);
    }

    private void ApplyRangeIndicatorRadius(float radius)
    {
        if (rangeIndicatorQuad == null)
            return;

        float diameter = Mathf.Max(MinimumRadius, radius * 2f);

        int firstAxis = GetMostHorizontalLocalAxis(-1);
        int secondAxis = GetMostHorizontalLocalAxis(firstAxis);

        Vector3 localScale = rangeIndicatorQuad.localScale;
        SetAxisValue(ref localScale, firstAxis, CalculateLocalScaleForWorldDiameter(firstAxis, diameter));
        SetAxisValue(ref localScale, secondAxis, CalculateLocalScaleForWorldDiameter(secondAxis, diameter));
        rangeIndicatorQuad.localScale = localScale;

        Vector3 localPosition = rangeIndicatorQuad.localPosition;
        localPosition.y = rangeIndicatorYOffset;
        rangeIndicatorQuad.localPosition = localPosition;
    }

    private int GetMostHorizontalLocalAxis(int ignoredAxis)
    {
        int bestAxis = 0;
        float bestScore = -1f;

        for (int i = 0; i < 3; i++)
        {
            if (i == ignoredAxis)
                continue;

            float score = GetHorizontalAxisScore(i);

            if (score > bestScore)
            {
                bestScore = score;
                bestAxis = i;
            }
        }

        return bestAxis;
    }

    private float GetHorizontalAxisScore(int axisIndex)
    {
        Vector3 worldDirection = rangeIndicatorQuad.TransformDirection(GetLocalAxisVector(axisIndex));
        worldDirection.y = 0f;
        return worldDirection.sqrMagnitude;
    }

    private float CalculateLocalScaleForWorldDiameter(int axisIndex, float diameter)
    {
        float meshAxisSize = GetMeshAxisSize(axisIndex);
        float worldFootprintPerLocalUnit = GetWorldFootprintPerLocalUnit(axisIndex);
        float denominator = meshAxisSize * worldFootprintPerLocalUnit;

        if (denominator <= AxisEpsilon)
            return Mathf.Max(MinimumScale, diameter);

        float currentScale = GetAxisValue(rangeIndicatorQuad.localScale, axisIndex);
        float sign = currentScale < 0f ? -1f : 1f;
        float scale = diameter / denominator;

        return Mathf.Max(MinimumScale, scale) * sign;
    }

    private float GetMeshAxisSize(int axisIndex)
    {
        MeshFilter meshFilter = rangeIndicatorQuad.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return 1f;

        float size = GetAxisValue(meshFilter.sharedMesh.bounds.size, axisIndex);

        if (size <= AxisEpsilon)
            return 1f;

        return size;
    }

    private float GetWorldFootprintPerLocalUnit(int axisIndex)
    {
        Vector3 localAxis = GetLocalAxisVector(axisIndex);
        Vector3 parentSpaceAxis = rangeIndicatorQuad.localRotation * localAxis;
        Vector3 worldAxis = rangeIndicatorQuad.parent != null
            ? rangeIndicatorQuad.parent.TransformVector(parentSpaceAxis)
            : parentSpaceAxis;

        worldAxis.y = 0f;

        float footprint = worldAxis.magnitude;

        if (footprint <= AxisEpsilon)
            return 1f;

        return footprint;
    }

    private Vector2 GetRangeIndicatorWorldFootprint()
    {
        if (rangeIndicatorQuad == null)
            return Vector2.zero;

        Renderer indicatorRenderer = rangeIndicatorQuad.GetComponentInChildren<Renderer>();

        if (indicatorRenderer == null)
            return Vector2.zero;

        Bounds bounds = indicatorRenderer.bounds;
        return new Vector2(bounds.size.x, bounds.size.z);
    }

    private Vector3 GetLocalAxisVector(int axisIndex)
    {
        switch (axisIndex)
        {
            case 0:
                return Vector3.right;
            case 1:
                return Vector3.up;
            default:
                return Vector3.forward;
        }
    }

    private float GetAxisValue(Vector3 vector, int axisIndex)
    {
        switch (axisIndex)
        {
            case 0:
                return vector.x;
            case 1:
                return vector.y;
            default:
                return vector.z;
        }
    }

    private void SetAxisValue(ref Vector3 vector, int axisIndex, float value)
    {
        switch (axisIndex)
        {
            case 0:
                vector.x = value;
                break;
            case 1:
                vector.y = value;
                break;
            default:
                vector.z = value;
                break;
        }
    }

    private void ShowRangeIndicator()
    {
        if (rangeIndicatorQuad == null)
            return;

        rangeIndicatorQuad.gameObject.SetActive(true);
    }

    private void HideRangeIndicator()
    {
        if (rangeIndicatorQuad == null)
            return;

        rangeIndicatorQuad.gameObject.SetActive(false);
    }

    private IEnumerator CheckOverlappingTargetsNextFrame()
    {
        yield return null;

        if (!isConfigured)
            yield break;

        if (triggerCollider == null)
            yield break;

        if (oneShotTrigger && hasTriggered)
            yield break;

        Vector3 center = transform.TransformPoint(triggerCollider.center);
        float radius = GetWorldTriggerRadius();

        int mask = targetLayerMask.value == 0
            ? Physics.AllLayers
            : targetLayerMask.value;

        Collider[] overlaps = Physics.OverlapSphere(
            center,
            radius,
            mask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (TryTriggerStop(overlaps[i]) && oneShotTrigger)
                yield break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTriggerStop(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryTriggerStop(other);
    }

    private bool TryTriggerStop(Collider other)
    {
        if (!isConfigured)
            return false;

        if (oneShotTrigger && hasTriggered)
            return false;

        if (other == null)
            return false;

        if (!TryGetTargetController(other, out TargetController target))
            return false;

        TargetStopEffectReceiver receiver = target.GetComponent<TargetStopEffectReceiver>();

        if (receiver == null)
            receiver = target.gameObject.AddComponent<TargetStopEffectReceiver>();

        receiver.ApplyStop(stopDuration);

        hasTriggered = true;

        PlayTriggerEffect();

        if (hideRangeIndicatorAfterTriggered)
            HideRangeIndicator();

        if (disableColliderAfterTriggered && triggerCollider != null)
            triggerCollider.enabled = false;

        if (destroyAfterTriggered)
            Destroy(gameObject, destroyDelayAfterTriggered);

        if (debugLog)
        {
            Debug.Log(
                $"[StopSignal] Triggered target stop. " +
                $"Target={target.name}, Duration={stopDuration:0.##}, " +
                $"DestroyAfterTriggered={destroyAfterTriggered}"
            );
        }

        return true;
    }

    private void PlayTriggerEffect()
    {
        if (triggerEffectObject == null)
            return;

        triggerEffectObject.SetActive(false);
        triggerEffectObject.SetActive(true);
    }

    private bool TryGetTargetController(Collider other, out TargetController target)
    {
        target = other.GetComponentInParent<TargetController>();

        if (target == null)
            return false;

        if (targetLayerMask.value == 0)
            return true;

        Transform current = other.transform;

        while (current != null)
        {
            if ((targetLayerMask.value & (1 << current.gameObject.layer)) != 0)
                return true;

            if (current == target.transform)
                break;

            current = current.parent;
        }

        return false;
    }

    private float GetWorldTriggerRadius()
    {
        if (triggerCollider == null)
            return MinimumRadius;

        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(scale.x, scale.y, scale.z);

        return triggerCollider.radius * maxScale;
    }

    private Transform FindChildByName(Transform parent, string targetName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == targetName)
                return child;

            Transform result = FindChildByName(child, targetName);

            if (result != null)
                return result;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        CacheComponents();

        if (triggerCollider == null)
            return;

        Gizmos.color = Color.cyan;
        Vector3 center = transform.TransformPoint(triggerCollider.center);
        Gizmos.DrawWireSphere(center, GetWorldTriggerRadius());
    }
#endif
}

public class TargetStopEffectReceiver : MonoBehaviour
{
    private NavMeshAgent navAgent;
    private Coroutine stopRoutine;

    private bool cachedAgentValues;
    private bool originalIsStopped;
    private float originalSpeed;
    private float originalAcceleration;
    private float originalAngularSpeed;

    private float stopEndTime;

    public void ApplyStop(float duration)
    {
        if (duration <= 0f)
            return;

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
        {
            Debug.LogWarning($"[TargetStopEffectReceiver] NavMeshAgent not found on {name}.");
            return;
        }

        stopEndTime = Mathf.Max(stopEndTime, Time.time + duration);

        if (stopRoutine != null)
            return;

        CacheAgentValues();
        stopRoutine = StartCoroutine(StopRoutine());
    }

    private IEnumerator StopRoutine()
    {
        while (Time.time < stopEndTime)
        {
            ApplyStoppedState();
            yield return null;
        }

        RestoreAgentValues();
        stopRoutine = null;
    }

    private void LateUpdate()
    {
        if (stopRoutine == null)
            return;

        ApplyStoppedState();
    }

    private void CacheAgentValues()
    {
        if (navAgent == null || cachedAgentValues)
            return;

        originalIsStopped = navAgent.isStopped;
        originalSpeed = navAgent.speed;
        originalAcceleration = navAgent.acceleration;
        originalAngularSpeed = navAgent.angularSpeed;

        cachedAgentValues = true;
    }

    private void ApplyStoppedState()
    {
        if (navAgent == null)
            return;

        navAgent.isStopped = true;
        navAgent.speed = 0f;
        navAgent.acceleration = 0f;
        navAgent.angularSpeed = 0f;
        navAgent.velocity = Vector3.zero;
    }

    private void RestoreAgentValues()
    {
        if (navAgent == null || !cachedAgentValues)
            return;

        navAgent.speed = originalSpeed;
        navAgent.acceleration = originalAcceleration;
        navAgent.angularSpeed = originalAngularSpeed;
        navAgent.isStopped = originalIsStopped;

        cachedAgentValues = false;
    }

    private void OnDisable()
    {
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
            stopRoutine = null;
        }

        RestoreAgentValues();
    }
}