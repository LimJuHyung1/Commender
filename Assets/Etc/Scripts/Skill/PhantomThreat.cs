using System.Collections.Generic;
using UnityEngine;

public class PhantomThreat : MonoBehaviour
{
    [Header("Phantom Threat Settings")]
    [SerializeField] private bool showDebugGizmo = true;
    [SerializeField] private float debugRadius = 0.75f;
    [SerializeField] private float threatWeight = 1.5f;

    private static readonly List<PhantomThreat> activeThreats = new List<PhantomThreat>();

    public static IReadOnlyList<PhantomThreat> ActiveThreats => activeThreats;

    public Vector3 Position => transform.position;
    public float ThreatWeight => threatWeight;

    private void OnEnable()
    {
        if (!activeThreats.Contains(this))
            activeThreats.Add(this);
    }

    private void OnDisable()
    {
        activeThreats.Remove(this);
    }

    private void OnDestroy()
    {
        activeThreats.Remove(this);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmo)
            return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, debugRadius);
    }
}