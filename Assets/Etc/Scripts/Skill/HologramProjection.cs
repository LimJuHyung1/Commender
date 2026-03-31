using System.Collections.Generic;
using UnityEngine;

public class HologramProjection : MonoBehaviour
{
    [Header("Hologram Settings")]
    [SerializeField] private bool showDebugGizmo = true;
    [SerializeField] private float debugRadius = 0.75f;
    [SerializeField] private float threatWeight = 1.5f;

    private static readonly List<HologramProjection> activeHolograms = new List<HologramProjection>();

    public static IReadOnlyList<HologramProjection> ActiveHolograms => activeHolograms;

    public Vector3 Position => transform.position;
    public float ThreatWeight => threatWeight;

    private void OnEnable()
    {
        if (!activeHolograms.Contains(this))
            activeHolograms.Add(this);
    }

    private void OnDisable()
    {
        activeHolograms.Remove(this);
    }

    private void OnDestroy()
    {
        activeHolograms.Remove(this);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmo)
            return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, debugRadius);
    }
}