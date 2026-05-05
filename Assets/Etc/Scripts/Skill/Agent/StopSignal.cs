using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class StopSignal : MonoBehaviour
{
    private const string DefaultRangeIndicatorName = "Quad";

    [Header("Trigger")]
    [SerializeField] private bool destroyAfterTriggered = true;
    [SerializeField] private float destroyDelayAfterTriggered = 0.2f;
    [SerializeField] private GameObject triggerEffectObject;

    [Header("Range Visual")]
    [SerializeField] private Transform rangeIndicatorQuad;
    [SerializeField] private float rangeIndicatorYOffset = 0.03f;
    [SerializeField] private bool hideRangeIndicatorAfterTriggered = true;

    private SphereCollider triggerCollider;
    private Rigidbody rigidBody;
    private LayerMask targetLayerMask;

    private float stopDuration = 2f;
    private bool hasTriggered;

    private void Awake()
    {
        CacheComponents();
        SetupTriggerCollider();
        SetupRigidbody();
        SetupTriggerEffect();
        ApplyRangeIndicatorRadius(triggerCollider.radius);
    }

    private void OnValidate()
    {
        CacheComponents();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            ApplyRangeIndicatorRadius(triggerCollider.radius);
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

        float safeRadius = Mathf.Max(0.1f, triggerRadius);

        triggerCollider.radius = safeRadius;

        stopDuration = Mathf.Max(0.1f, targetStopDuration);
        targetLayerMask = targetLayer;
        hasTriggered = false;

        ApplyRangeIndicatorRadius(safeRadius);

        if (rangeIndicatorQuad != null)
            rangeIndicatorQuad.gameObject.SetActive(true);

        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);

        StartCoroutine(CheckOverlappingTargetsNextFrame());
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

        float diameter = Mathf.Max(0.1f, radius * 2f);

        Vector3 localScale = rangeIndicatorQuad.localScale;
        localScale.x = diameter;
        localScale.y = diameter;
        rangeIndicatorQuad.localScale = localScale;

        Vector3 localPosition = rangeIndicatorQuad.localPosition;
        localPosition.y = rangeIndicatorYOffset;
        rangeIndicatorQuad.localPosition = localPosition;
    }

    private IEnumerator CheckOverlappingTargetsNextFrame()
    {
        yield return null;

        if (hasTriggered || triggerCollider == null)
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
            if (TryTriggerStop(overlaps[i]))
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
        if (hasTriggered)
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

        if (triggerEffectObject != null)
            triggerEffectObject.SetActive(true);

        if (hideRangeIndicatorAfterTriggered && rangeIndicatorQuad != null)
            rangeIndicatorQuad.gameObject.SetActive(false);

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        Debug.Log($"[StopSignal] Triggered target stop. Target: {target.name}, Duration: {stopDuration:0.##}");

        if (destroyAfterTriggered)
            Destroy(gameObject, Mathf.Max(0.05f, destroyDelayAfterTriggered));

        return true;
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
            return 0.1f;

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