using System.Collections.Generic;
using UnityEngine;

public class DecoySignal : MonoBehaviour
{
    [Header("Decoy Signal Settings")]
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private bool destroyAfterLifetime = true;
    [SerializeField] private bool showDebugGizmo = true;
    [SerializeField] private float debugRadius = 0.5f;

    private static readonly List<DecoySignal> activeSignals = new List<DecoySignal>();

    public static IReadOnlyList<DecoySignal> ActiveSignals => activeSignals;

    public Vector3 Position => transform.position;

    private void OnEnable()
    {
        if (!activeSignals.Contains(this))
            activeSignals.Add(this);

        if (destroyAfterLifetime && lifetime > 0f)
            Invoke(nameof(DestroySelf), lifetime);
    }

    private void OnDisable()
    {
        activeSignals.Remove(this);
        CancelInvoke();
    }

    private void OnDestroy()
    {
        activeSignals.Remove(this);
        CancelInvoke();
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmo)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, debugRadius);
    }
}