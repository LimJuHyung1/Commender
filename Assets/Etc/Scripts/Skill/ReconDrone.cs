using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class ReconDrone : MonoBehaviour
{
    [Header("Recon Drone Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float radius = 4f;

    private SphereCollider zoneCollider;
    private readonly HashSet<TargetController> revealedTargets = new HashSet<TargetController>();

    private void Awake()
    {
        zoneCollider = GetComponent<SphereCollider>();

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
            zoneCollider.radius = radius;
        }
    }

    private void Start()
    {
        RefreshInitialTargets();
    }

    private void OnValidate()
    {
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null)
        {
            sc.isTrigger = true;
            sc.radius = radius;
        }
    }

    private void RefreshInitialTargets()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            radius,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider hit in hits)
        {
            TargetController target = hit.GetComponentInParent<TargetController>();
            if (target == null)
                continue;

            if (revealedTargets.Add(target))
            {
                target.AddReconReveal();
                Debug.Log($"[Recon Drone] Target already inside recon zone: {target.name}");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInTargetLayer(other.gameObject.layer))
            return;

        TargetController target = other.GetComponentInParent<TargetController>();
        if (target == null)
            return;

        if (revealedTargets.Add(target))
        {
            target.AddReconReveal();
            Debug.Log($"[Recon Drone] Target entered recon zone: {target.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsInTargetLayer(other.gameObject.layer))
            return;

        TargetController target = other.GetComponentInParent<TargetController>();
        if (target == null)
            return;

        if (revealedTargets.Remove(target))
        {
            target.RemoveReconReveal();
            Debug.Log($"[Recon Drone] Target exited recon zone: {target.name}");
        }
    }

    private void OnDisable()
    {
        ClearAllReveals();
    }

    private void OnDestroy()
    {
        ClearAllReveals();
    }

    private void ClearAllReveals()
    {
        if (revealedTargets.Count == 0)
            return;

        foreach (TargetController target in revealedTargets)
        {
            if (target != null)
                target.RemoveReconReveal();
        }

        revealedTargets.Clear();
    }

    private bool IsInTargetLayer(int layer)
    {
        return ((1 << layer) & targetLayer) != 0;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}