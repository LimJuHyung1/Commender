using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(LineRenderer))]
public class AccessControlZone : MonoBehaviour
{
    [Header("Scan")]
    [SerializeField] private float scanInterval = 0.1f;
    [SerializeField] private int maxTargetHits = 16;

    [Header("Line Visual")]
    [SerializeField] private float lineHeight = 0.03f;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private int circleSegments = 80;

    [Header("Area Visual")]
    [SerializeField] private Transform zoneVisual;
    [SerializeField] private Renderer zoneVisualRenderer;
    [SerializeField] private Material zoneVisualMaterial;
    [SerializeField] private float visualHeight = 0.015f;
    [SerializeField] private bool scaleVisualToRadius = true;

    private Chaser owner;
    private LayerMask targetLayer;

    private float radius;
    private float endTime;

    private float targetSpeedMultiplier;
    private float targetAngularSpeedMultiplier;

    private float chaserSpeedMultiplier;
    private float chaserAngularSpeedMultiplier;

    private string modifierKey;

    private LineRenderer lineRenderer;
    private Collider[] targetHits;

    private float scanTimer;

    private readonly List<NavAgentStatModifierReceiver> affectedReceivers = new List<NavAgentStatModifierReceiver>();

    public void Initialize(
        Chaser owner,
        Vector3 center,
        float radius,
        float duration,
        LayerMask targetLayer,
        float targetSpeedMultiplier,
        float targetAngularSpeedMultiplier,
        float chaserSpeedMultiplier,
        float chaserAngularSpeedMultiplier,
        Material lineMaterial,
        Color lineColor)
    {
        this.owner = owner;
        this.radius = Mathf.Max(0.1f, radius);
        this.targetLayer = targetLayer;

        this.targetSpeedMultiplier = Mathf.Max(0.01f, targetSpeedMultiplier);
        this.targetAngularSpeedMultiplier = Mathf.Max(0.01f, targetAngularSpeedMultiplier);
        this.chaserSpeedMultiplier = Mathf.Max(0.01f, chaserSpeedMultiplier);
        this.chaserAngularSpeedMultiplier = Mathf.Max(0.01f, chaserAngularSpeedMultiplier);

        transform.position = center;
        endTime = Time.time + Mathf.Max(0.1f, duration);

        modifierKey = $"ChaserAccessControl_{GetInstanceID()}";
        targetHits = new Collider[Mathf.Max(1, maxTargetHits)];

        lineRenderer = GetComponent<LineRenderer>();

        SetupLineRenderer(lineMaterial, lineColor);
        SetupZoneVisual();
        DrawCircle();
    }

    private void Update()
    {
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
            return;
        }

        ApplyChaserBuffIfInside();

        scanTimer -= Time.deltaTime;

        if (scanTimer > 0f)
            return;

        scanTimer = scanInterval;
        ApplyTargetDebuffs();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < affectedReceivers.Count; i++)
        {
            if (affectedReceivers[i] != null)
                affectedReceivers[i].RemoveModifier(modifierKey);
        }

        affectedReceivers.Clear();
    }

    private void ApplyChaserBuffIfInside()
    {
        if (owner == null)
            return;

        float sqrDistance = (owner.transform.position - transform.position).sqrMagnitude;

        if (sqrDistance > radius * radius)
            return;

        NavAgentStatModifierReceiver receiver = GetOrAddModifierReceiver(owner.transform);

        if (receiver == null)
            return;

        ApplyModifier(
            receiver,
            chaserSpeedMultiplier,
            chaserAngularSpeedMultiplier,
            1f
        );
    }

    private void ApplyTargetDebuffs()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            targetHits,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = targetHits[i];

            if (hit == null)
                continue;

            Transform targetRoot = ResolveTargetRoot(hit);

            if (targetRoot == null)
                continue;

            NavAgentStatModifierReceiver receiver = GetOrAddModifierReceiver(targetRoot);

            if (receiver == null)
                continue;

            ApplyModifier(
                receiver,
                targetSpeedMultiplier,
                targetAngularSpeedMultiplier,
                1f
            );
        }
    }

    private void ApplyModifier(
        NavAgentStatModifierReceiver receiver,
        float speedMultiplier,
        float angularSpeedMultiplier,
        float accelerationMultiplier)
    {
        if (receiver == null)
            return;

        receiver.ApplyTimedModifier(
            modifierKey,
            speedMultiplier,
            angularSpeedMultiplier,
            accelerationMultiplier,
            scanInterval + 0.15f
        );

        if (!affectedReceivers.Contains(receiver))
            affectedReceivers.Add(receiver);
    }

    private Transform ResolveTargetRoot(Collider hit)
    {
        if (hit == null)
            return null;

        TargetController targetController = hit.GetComponentInParent<TargetController>();

        if (targetController != null)
            return targetController.transform;

        return hit.transform.root;
    }

    private NavAgentStatModifierReceiver GetOrAddModifierReceiver(Transform root)
    {
        if (root == null)
            return null;

        NavMeshAgent navMeshAgent = root.GetComponent<NavMeshAgent>();

        if (navMeshAgent == null)
            navMeshAgent = root.GetComponentInChildren<NavMeshAgent>();

        if (navMeshAgent == null)
            return null;

        NavAgentStatModifierReceiver receiver = navMeshAgent.GetComponent<NavAgentStatModifierReceiver>();

        if (receiver == null)
            receiver = navMeshAgent.gameObject.AddComponent<NavAgentStatModifierReceiver>();

        return receiver;
    }

    private void SetupLineRenderer(Material lineMaterial, Color lineColor)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.positionCount = circleSegments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        if (lineMaterial != null)
            lineRenderer.material = lineMaterial;
        else
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void SetupZoneVisual()
    {
        if (zoneVisual == null)
            return;

        zoneVisual.localPosition = new Vector3(0f, visualHeight, 0f);

        if (scaleVisualToRadius)
        {
            float diameter = radius * 2f;
            zoneVisual.localScale = new Vector3(diameter, diameter, 1f);
        }

        if (zoneVisualRenderer == null)
            zoneVisualRenderer = zoneVisual.GetComponent<Renderer>();

        if (zoneVisualRenderer == null)
            return;

        if (zoneVisualMaterial != null)
            zoneVisualRenderer.material = zoneVisualMaterial;
    }

    private void DrawCircle()
    {
        if (lineRenderer == null)
            return;

        Vector3 center = transform.position + Vector3.up * lineHeight;

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;

            Vector3 point = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            lineRenderer.SetPosition(i, point);
        }
    }
}