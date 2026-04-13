using UnityEngine;

public class TargetSmoke : SmokeBase
{
    [Header("Target Filter")]
    [SerializeField] private LayerMask agentLayer;

    protected override bool CanAffect(Collider other)
    {
        AgentController agent = other.GetComponentInParent<AgentController>();
        if (agent == null)
            return false;

        return ((1 << agent.gameObject.layer) & agentLayer) != 0;
    }

    protected override MonoBehaviour FindReceiver(Collider other)
    {
        AgentController agent = other.GetComponentInParent<AgentController>();
        if (agent == null)
            return null;

        return agent.GetComponentInChildren<VisionSensor>(true);
    }
}