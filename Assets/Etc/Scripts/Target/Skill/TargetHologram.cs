using System.Collections.Generic;
using UnityEngine;

public class TargetHologram : HologramBase
{
    [Header("Disappear")]
    [SerializeField] private LayerMask agentLayer;
    [SerializeField] private float ignoreOwnerTouchTime = 0.15f;

    private static readonly List<TargetHologram> activeHolograms = new List<TargetHologram>();

    private float spawnedTime;

    public static IReadOnlyList<TargetHologram> ActiveHolograms => activeHolograms;

    protected override Color GizmoColor => Color.cyan;

    protected override void OnEnable()
    {
        base.OnEnable();
        spawnedTime = Time.time;
    }

    protected override void RegisterInstance()
    {
        if (!activeHolograms.Contains(this))
            activeHolograms.Add(this);
    }

    protected override void UnregisterInstance()
    {
        activeHolograms.Remove(this);
    }

    protected override void OnHologramTriggerEnter(Collider other)
    {
        if (OwnerRoot != null &&
            Time.time - spawnedTime <= ignoreOwnerTouchTime &&
            other.transform.root == OwnerRoot.root)
            return;

        bool touchedByAgentLayer = ((1 << other.gameObject.layer) & agentLayer.value) != 0;
        bool touchedByAgentComponent = other.GetComponentInParent<AgentController>() != null;

        if (!touchedByAgentLayer && !touchedByAgentComponent)
            return;

        DestroySelf();
    }
}