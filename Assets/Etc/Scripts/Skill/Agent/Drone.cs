using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class Drone : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float radius = 7f;
    [SerializeField] private float duration = 20f;

    [Header("Observation Area Visual")]
    [SerializeField] private bool showObservationArea = true;
    [SerializeField] private GameObject observationAreaPrefab;
    [SerializeField] private Material observationAreaMaterial;
    [SerializeField] private float observationAreaYOffset = 0.05f;

    [Header("Advanced")]
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private int maxColliders = 32;
    [SerializeField] private float ownerNotifyInterval = 0.15f;
    [SerializeField] private bool autoDestroyWhenFinished = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool drawGizmos = true;

    private Observer owner;
    private SphereCollider zoneCollider;

    private Vector3 observationCenterPosition;

    private float finishTime;
    private float checkTimer;
    private float notifyTimer;
    private bool initialized;

    private Collider[] hitBuffer;

    private GameObject observationAreaObject;
    private Renderer observationAreaRenderer;
    private Material runtimeObservationAreaMaterial;

    private readonly HashSet<TargetController> revealedTargets = new HashSet<TargetController>();
    private readonly HashSet<TargetController> currentTargets = new HashSet<TargetController>();
    private readonly List<TargetController> targetsToRemove = new List<TargetController>();

    public Observer Owner => owner;
    public float Radius => radius;
    public float Duration => duration;
    public Vector3 ObservationCenterPosition => observationCenterPosition;

    private void Awake()
    {
        CacheCollider();
        ApplyColliderSettings();
        PrepareHitBuffer();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0f, radius);
        duration = Mathf.Max(0f, duration);
        observationAreaYOffset = Mathf.Max(0f, observationAreaYOffset);
        checkInterval = Mathf.Max(0.02f, checkInterval);
        ownerNotifyInterval = Mathf.Max(0.02f, ownerNotifyInterval);
        maxColliders = Mathf.Max(1, maxColliders);

        CacheCollider();
        ApplyColliderSettings();
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (duration > 0f && Time.time >= finishTime)
        {
            if (autoDestroyWhenFinished)
            {
                Destroy(gameObject);
                return;
            }

            ClearAllReveals();
            HideObservationArea();
            enabled = false;
            return;
        }

        UpdateObservationAreaVisual();
        UpdateDetection();
        UpdateOwnerNotify();
    }

    private void OnDisable()
    {
        ClearAllReveals();
        HideObservationArea();
    }

    private void OnDestroy()
    {
        ClearAllReveals();

        if (observationAreaObject != null)
            Destroy(observationAreaObject);

        if (runtimeObservationAreaMaterial != null)
            Destroy(runtimeObservationAreaMaterial);
    }

    public void Initialize(
        Observer observer,
        Vector3 observationCenter,
        Vector3 droneVisualPosition,
        LayerMask observationTargetLayer,
        float observationRadius,
        float observationDuration)
    {
        owner = observer;

        observationCenterPosition = observationCenter;
        transform.position = droneVisualPosition;

        targetLayer = observationTargetLayer;
        radius = Mathf.Max(0f, observationRadius);
        duration = Mathf.Max(0f, observationDuration);

        finishTime = Time.time + duration;
        checkTimer = 0f;
        notifyTimer = 0f;
        initialized = true;

        CacheCollider();
        ApplyColliderSettings();
        PrepareHitBuffer();
        SetupObservationAreaVisual();
        DetectTargets();

        if (debugLog)
        {
            Debug.Log(
                $"[Drone] Initialized. " +
                $"Drone Position: {transform.position}, " +
                $"Observation Center: {observationCenterPosition}, " +
                $"Radius: {radius}, Duration: {duration}"
            );
        }
    }

    private void CacheCollider()
    {
        if (zoneCollider != null)
            return;

        zoneCollider = GetComponent<SphereCollider>();
    }

    private void ApplyColliderSettings()
    {
        if (zoneCollider == null)
            return;

        zoneCollider.isTrigger = true;
        zoneCollider.radius = radius;

        if (initialized)
            zoneCollider.center = transform.InverseTransformPoint(observationCenterPosition);
        else
            zoneCollider.center = Vector3.zero;
    }

    private void PrepareHitBuffer()
    {
        if (hitBuffer != null && hitBuffer.Length == maxColliders)
            return;

        hitBuffer = new Collider[maxColliders];
    }

    private void SetupObservationAreaVisual()
    {
        if (!showObservationArea)
            return;

        if (observationAreaObject == null)
            CreateObservationAreaObject();

        if (observationAreaObject == null)
            return;

        observationAreaObject.SetActive(true);
        UpdateObservationAreaVisual();
    }

    private void CreateObservationAreaObject()
    {
        if (observationAreaPrefab != null)
        {
            observationAreaObject = Instantiate(
                observationAreaPrefab,
                GetObservationAreaPosition(),
                Quaternion.Euler(90f, 0f, 0f)
            );

            observationAreaObject.name = "DroneObservationArea";
        }
        else
        {
            observationAreaObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            observationAreaObject.name = "DroneObservationArea";

            Collider areaCollider = observationAreaObject.GetComponent<Collider>();

            if (areaCollider != null)
                Destroy(areaCollider);
        }

        observationAreaObject.transform.SetParent(null);

        observationAreaRenderer = observationAreaObject.GetComponentInChildren<Renderer>();

        if (observationAreaRenderer == null)
        {
            Debug.LogWarning("[Drone] Observation area renderer is missing.");
            return;
        }

        Material material = ResolveObservationAreaMaterial();

        if (material != null)
            observationAreaRenderer.sharedMaterial = material;
    }

    private Material ResolveObservationAreaMaterial()
    {
        if (observationAreaMaterial != null)
            return observationAreaMaterial;

        Shader shader = Shader.Find("Commander/DroneObservationArea");

        if (shader == null)
        {
            Debug.LogWarning("[Drone] Shader not found: Commander/DroneObservationArea");
            return null;
        }

        runtimeObservationAreaMaterial = new Material(shader);
        runtimeObservationAreaMaterial.name = "Runtime_DroneObservationArea";

        return runtimeObservationAreaMaterial;
    }

    private void UpdateObservationAreaVisual()
    {
        if (!showObservationArea)
            return;

        if (observationAreaObject == null)
            return;

        observationAreaObject.transform.position = GetObservationAreaPosition();
        observationAreaObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        observationAreaObject.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
    }

    private Vector3 GetObservationAreaPosition()
    {
        return new Vector3(
            observationCenterPosition.x,
            observationCenterPosition.y + observationAreaYOffset,
            observationCenterPosition.z
        );
    }

    private void HideObservationArea()
    {
        if (observationAreaObject == null)
            return;

        observationAreaObject.SetActive(false);
    }

    private void UpdateDetection()
    {
        checkTimer -= Time.deltaTime;

        if (checkTimer > 0f)
            return;

        checkTimer = checkInterval;
        DetectTargets();
    }

    private void DetectTargets()
    {
        currentTargets.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(
            observationCenterPosition,
            radius,
            hitBuffer,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];

            if (hit == null)
                continue;

            if (!IsColliderInsideVisibleObservationArea(hit))
                continue;

            TargetController target = hit.GetComponentInParent<TargetController>();

            if (target == null)
                continue;

            currentTargets.Add(target);

            if (revealedTargets.Add(target))
            {
                target.AddReconReveal();

                if (debugLog)
                {
                    float horizontalDistance = GetHorizontalDistanceToObservationCenter(target.transform.position);

                    Debug.Log(
                        $"[Drone] Target revealed: {target.name}, " +
                        $"Horizontal Distance: {horizontalDistance}, Radius: {radius}"
                    );
                }
            }

            NotifyOwner(target);
        }

        targetsToRemove.Clear();

        foreach (TargetController target in revealedTargets)
        {
            if (target == null)
            {
                targetsToRemove.Add(target);
                continue;
            }

            if (!currentTargets.Contains(target))
                targetsToRemove.Add(target);
        }

        for (int i = 0; i < targetsToRemove.Count; i++)
        {
            TargetController target = targetsToRemove[i];

            revealedTargets.Remove(target);

            if (target != null)
            {
                target.RemoveReconReveal();

                if (debugLog)
                    Debug.Log($"[Drone] Target reveal removed: {target.name}");
            }
        }

        targetsToRemove.Clear();
    }

    private bool IsColliderInsideVisibleObservationArea(Collider targetCollider)
    {
        if (targetCollider == null)
            return false;

        Vector3 closestPoint = targetCollider.ClosestPoint(observationCenterPosition);

        Vector2 centerXZ = new Vector2(
            observationCenterPosition.x,
            observationCenterPosition.z
        );

        Vector2 closestXZ = new Vector2(
            closestPoint.x,
            closestPoint.z
        );

        float sqrDistance = (closestXZ - centerXZ).sqrMagnitude;
        return sqrDistance <= radius * radius;
    }

    private float GetHorizontalDistanceToObservationCenter(Vector3 worldPosition)
    {
        Vector2 centerXZ = new Vector2(
            observationCenterPosition.x,
            observationCenterPosition.z
        );

        Vector2 positionXZ = new Vector2(
            worldPosition.x,
            worldPosition.z
        );

        return Vector2.Distance(centerXZ, positionXZ);
    }

    private void UpdateOwnerNotify()
    {
        if (owner == null)
            return;

        notifyTimer -= Time.deltaTime;

        if (notifyTimer > 0f)
            return;

        notifyTimer = ownerNotifyInterval;

        foreach (TargetController target in revealedTargets)
        {
            if (target == null)
                continue;

            NotifyOwner(target);
        }
    }

    private void NotifyOwner(TargetController target)
    {
        if (owner == null)
            return;

        if (target == null)
            return;

        owner.NotifyDroneTargetObserved(target.transform);
    }

    private void ClearAllReveals()
    {
        if (revealedTargets.Count == 0)
            return;

        targetsToRemove.Clear();

        foreach (TargetController target in revealedTargets)
        {
            if (target != null)
                targetsToRemove.Add(target);
        }

        for (int i = 0; i < targetsToRemove.Count; i++)
        {
            targetsToRemove[i].RemoveReconReveal();
        }

        revealedTargets.Clear();
        currentTargets.Clear();
        targetsToRemove.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Vector3 center = initialized ? observationCenterPosition : transform.position;

        Gizmos.color = Color.cyan;
        DrawFlatCircleGizmo(center, radius, 64);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, center);
    }

    private void DrawFlatCircleGizmo(Vector3 center, float circleRadius, int segmentCount)
    {
        if (circleRadius <= 0f)
            return;

        Vector3 previousPoint = center + new Vector3(circleRadius, 0f, 0f);

        for (int i = 1; i <= segmentCount; i++)
        {
            float angle = i / (float)segmentCount * Mathf.PI * 2f;

            Vector3 currentPoint = center + new Vector3(
                Mathf.Cos(angle) * circleRadius,
                0f,
                Mathf.Sin(angle) * circleRadius
            );

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}