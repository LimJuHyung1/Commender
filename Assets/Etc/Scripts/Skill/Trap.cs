using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Trap : MonoBehaviour
{
    [Header("Trap Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField][Range(0.05f, 1f)] private float slowMultiplier = 0.5f;
    [SerializeField] private float slowDuration = 3f;
    [SerializeField] private bool destroyAfterTrigger = true;
    [SerializeField] private bool singleUse = true;
    [SerializeField] private bool ignoreTriggerColliders = true;

    private BoxCollider triggerCollider;
    private bool hasTriggered = false;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        slowMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
        slowDuration = Mathf.Max(0.01f, slowDuration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (singleUse && hasTriggered)
            return;

        if (other == null)
            return;

        if (ignoreTriggerColliders && other.isTrigger)
            return;

        TargetController target = other.GetComponentInParent<TargetController>();
        if (target == null)
            return;

        if (!IsAllowedTargetLayer(other.gameObject.layer, target.gameObject.layer))
            return;

        hasTriggered = true;
        target.ApplySlow(slowMultiplier, slowDuration);

        Debug.Log($"[Trap] Target slowed to x{slowMultiplier:0.##} for {slowDuration:0.##} seconds: {target.name}");

        if (destroyAfterTrigger)
            Destroy(gameObject);
    }

    private bool IsAllowedTargetLayer(int hitLayer, int targetRootLayer)
    {
        return IsInTargetLayer(hitLayer) || IsInTargetLayer(targetRootLayer);
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