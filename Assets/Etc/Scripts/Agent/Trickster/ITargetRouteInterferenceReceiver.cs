using UnityEngine;

public interface ITargetRouteInterferenceReceiver
{
    void ApplyFakeBoxRouteInterference(Vector3 boxPosition, int reducedRouteCandidateCount);
}