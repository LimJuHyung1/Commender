using System.Collections.Generic;
using UnityEngine;

public class Noisemaker : MonoBehaviour
{
    [Header("Noisemaker Settings")]
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private bool destroyAfterLifetime = true;
    [SerializeField] private bool showDebugGizmo = true;
    [SerializeField] private float debugRadius = 0.5f;

    private static readonly List<Noisemaker> activeNoisemakers = new List<Noisemaker>();

    public static IReadOnlyList<Noisemaker> ActiveNoisemakers => activeNoisemakers;

    public Vector3 Position => transform.position;

    private void OnEnable()
    {
        if (!activeNoisemakers.Contains(this))
            activeNoisemakers.Add(this);

        if (destroyAfterLifetime && lifetime > 0f)
            Invoke(nameof(DestroySelf), lifetime);
    }

    private void OnDisable()
    {
        activeNoisemakers.Remove(this);
        CancelInvoke();
    }

    private void OnDestroy()
    {
        activeNoisemakers.Remove(this);
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