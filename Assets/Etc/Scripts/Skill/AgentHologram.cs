using System.Collections.Generic;
using UnityEngine;

public class AgentHologram : HologramBase
{
    [Header("Threat")]
    [SerializeField] private float threatWeight = 1.5f;

    private static readonly List<AgentHologram> activeHolograms = new List<AgentHologram>();

    public static IReadOnlyList<AgentHologram> ActiveHolograms => activeHolograms;
    public float ThreatWeight => threatWeight;

    protected override Color GizmoColor => Color.magenta;

    protected override void RegisterInstance()
    {
        if (!activeHolograms.Contains(this))
            activeHolograms.Add(this);
    }

    protected override void UnregisterInstance()
    {
        activeHolograms.Remove(this);
    }
}