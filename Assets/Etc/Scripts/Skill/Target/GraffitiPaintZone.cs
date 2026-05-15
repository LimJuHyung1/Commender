using System.Collections.Generic;
using UnityEngine;

public class GraffitiPaintZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] private float radius = 3.5f;
    [SerializeField] private float duration = 7f;
    [SerializeField] private LayerMask agentLayerMask;
    [SerializeField] private float tickInterval = 0.05f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool autoFindVisualRoot = true;
    [SerializeField] private string visualRootName = "Quad";
    [SerializeField] private bool autoScaleVisualToRadius = true;
    [SerializeField] private float visualScaleMultiplier = 1f;
    [SerializeField] private float visualYOffset = 0.03f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly HashSet<AgentController> blockedAgents = new HashSet<AgentController>();
    private readonly HashSet<AgentController> agentsInsideThisTick = new HashSet<AgentController>();

    private float spawnTime;
    private float nextTickTime;

    private Vector3 cachedBaseVisualLocalScale = Vector3.one;
    private Vector3 cachedBaseVisualLocalPosition = Vector3.zero;
    private Transform cachedScaleSource;
    private bool hasCachedBaseVisualState;

    public void Initialize(
        float radius,
        float duration,
        LayerMask agentLayerMask,
        bool enableDebugLog)
    {
        this.radius = Mathf.Max(0.1f, radius);
        this.duration = Mathf.Max(0.1f, duration);
        this.agentLayerMask = agentLayerMask;
        this.enableDebugLog = enableDebugLog;

        spawnTime = Time.time;
        nextTickTime = Time.time;

        ResolveVisualRoot();
        CacheBaseVisualState();
        ApplyVisualScaleToRadius();

        if (this.enableDebugLog)
            Debug.Log("[GraffitiPaintZone] Initialized as skill command blocker zone.");
    }

    private void Awake()
    {
        spawnTime = Time.time;
        nextTickTime = Time.time;

        ResolveVisualRoot();
        CacheBaseVisualState();
        ApplyVisualScaleToRadius();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        duration = Mathf.Max(0.1f, duration);
        tickInterval = Mathf.Max(0.01f, tickInterval);
        visualScaleMultiplier = Mathf.Max(0.01f, visualScaleMultiplier);
        visualYOffset = Mathf.Max(0f, visualYOffset);

        ResolveVisualRoot();
        CacheBaseVisualState();
        ApplyVisualScaleToRadius();
    }

    private void Update()
    {
        if (Time.time - spawnTime >= duration)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < nextTickTime)
            return;

        nextTickTime = Time.time + tickInterval;
        TickAgentsInsideZone();
    }

    private void OnDestroy()
    {
        RemoveBlockFromAllAgents();
    }

    private void TickAgentsInsideZone()
    {
        agentsInsideThisTick.Clear();

        Collider[] colliders;

        if (agentLayerMask.value != 0)
        {
            colliders = Physics.OverlapSphere(
                transform.position,
                radius,
                agentLayerMask
            );
        }
        else
        {
            colliders = Physics.OverlapSphere(
                transform.position,
                radius
            );
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];

            if (currentCollider == null)
                continue;

            AgentController agentController =
                currentCollider.GetComponentInParent<AgentController>();

            if (agentController == null)
                continue;

            ApplySkillCommandBlock(agentController);
            agentsInsideThisTick.Add(agentController);
        }

        RemoveBlockFromAgentsOutsideZone();
    }

    private void ApplySkillCommandBlock(AgentController agentController)
    {
        if (agentController == null)
            return;

        if (blockedAgents.Contains(agentController))
            return;

        blockedAgents.Add(agentController);
        agentController.AddSkillCommandBlocker(this);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[GraffitiPaintZone] Skill command blocked. AgentID: {agentController.AgentID}"
            );
        }
    }

    private void RemoveBlockFromAgentsOutsideZone()
    {
        List<AgentController> removeTargets = new List<AgentController>();

        foreach (AgentController agentController in blockedAgents)
        {
            if (agentController == null)
            {
                removeTargets.Add(agentController);
                continue;
            }

            if (!agentsInsideThisTick.Contains(agentController))
                removeTargets.Add(agentController);
        }

        for (int i = 0; i < removeTargets.Count; i++)
        {
            RemoveBlockFromAgent(removeTargets[i]);
        }
    }

    private void RemoveBlockFromAllAgents()
    {
        List<AgentController> removeTargets = new List<AgentController>();

        foreach (AgentController agentController in blockedAgents)
        {
            removeTargets.Add(agentController);
        }

        for (int i = 0; i < removeTargets.Count; i++)
        {
            RemoveBlockFromAgent(removeTargets[i]);
        }

        blockedAgents.Clear();
    }

    private void RemoveBlockFromAgent(AgentController agentController)
    {
        if (agentController != null)
        {
            agentController.RemoveSkillCommandBlocker(this);

            if (enableDebugLog)
            {
                Debug.Log(
                    $"[GraffitiPaintZone] Skill command block removed. AgentID: {agentController.AgentID}"
                );
            }

            blockedAgents.Remove(agentController);
        }
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
            return;

        if (!autoFindVisualRoot)
            return;

        Transform found = transform.Find(visualRootName);

        if (found == null)
            found = transform.Find("Quad");

        if (found == null && transform.childCount > 0)
            found = transform.GetChild(0);

        if (found != null)
            visualRoot = found;
    }

    private void CacheBaseVisualState()
    {
        if (visualRoot == null)
            return;

        if (hasCachedBaseVisualState && cachedScaleSource == visualRoot)
            return;

        cachedBaseVisualLocalScale = visualRoot.localScale;
        cachedBaseVisualLocalPosition = visualRoot.localPosition;
        cachedScaleSource = visualRoot;
        hasCachedBaseVisualState = true;
    }

    private void ApplyVisualScaleToRadius()
    {
        if (!autoScaleVisualToRadius)
            return;

        if (visualRoot == null)
            return;

        CacheBaseVisualState();

        float baseDiameter = GetBaseVisualDiameter();

        if (baseDiameter <= 0.0001f)
        {
            ApplyFallbackVisualScale();
            return;
        }

        float targetDiameter = radius * 2f * visualScaleMultiplier;
        float uniformScaleMultiplier = targetDiameter / baseDiameter;

        visualRoot.localScale = cachedBaseVisualLocalScale * uniformScaleMultiplier;

        Vector3 localPosition = cachedBaseVisualLocalPosition;
        localPosition.y += visualYOffset;
        visualRoot.localPosition = localPosition;
    }

    private float GetBaseVisualDiameter()
    {
        if (visualRoot == null)
            return 0f;

        MeshFilter meshFilter = visualRoot.GetComponent<MeshFilter>();

        if (meshFilter == null)
            meshFilter = visualRoot.GetComponentInChildren<MeshFilter>(true);

        if (meshFilter == null)
            return 0f;

        if (meshFilter.sharedMesh == null)
            return 0f;

        Vector3 meshSize = meshFilter.sharedMesh.bounds.size;

        float meshDiameter = Mathf.Max(meshSize.x, meshSize.y, meshSize.z);

        if (meshDiameter <= 0.0001f)
            return 0f;

        float scaledDiameter = meshDiameter * Mathf.Max(
            Mathf.Abs(cachedBaseVisualLocalScale.x),
            Mathf.Abs(cachedBaseVisualLocalScale.y),
            Mathf.Abs(cachedBaseVisualLocalScale.z)
        );

        return scaledDiameter;
    }

    private void ApplyFallbackVisualScale()
    {
        float targetDiameter = radius * 2f * visualScaleMultiplier;

        visualRoot.localScale = new Vector3(
            targetDiameter,
            targetDiameter,
            targetDiameter
        );

        Vector3 localPosition = cachedBaseVisualLocalPosition;
        localPosition.y += visualYOffset;
        visualRoot.localPosition = localPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}