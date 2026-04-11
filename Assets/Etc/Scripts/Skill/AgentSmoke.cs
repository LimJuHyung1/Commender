using UnityEngine;

public class AgentSmoke : SmokeBase
{
    [Header("Target Filter")]
    [SerializeField] private LayerMask targetLayer;

    protected override bool CanAffect(Collider other)
    {
        return ((1 << other.gameObject.layer) & targetLayer) != 0;
    }

    protected override MonoBehaviour FindReceiver(Collider other)
    {
        return other.GetComponentInParent<TargetController>();
    }
}