using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class SnareTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float rootDuration = 1.5f;
    [SerializeField] private bool destroyAfterTrigger = true;
    [SerializeField] private bool singleUse = true;

    private SphereCollider triggerCollider;
    private bool hasTriggered = false;

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (singleUse && hasTriggered)
            return;

        if (!IsInTargetLayer(other.gameObject.layer))
            return;

        TargetController target = other.GetComponentInParent<TargetController>();
        if (target == null)
            return;

        hasTriggered = true;
        target.ApplyRoot(rootDuration);

        Debug.Log($"[SnareTrap] Target rooted for {rootDuration} seconds: {target.name}");

        if (destroyAfterTrigger)
            Destroy(gameObject);
    }

    private bool IsInTargetLayer(int layer)
    {
        return ((1 << layer) & targetLayer) != 0;
    }

    private void OnDrawGizmosSelected()
    {
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(sc.center, sc.radius);
    }
}