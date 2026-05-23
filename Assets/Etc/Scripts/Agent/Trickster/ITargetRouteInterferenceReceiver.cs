using UnityEngine;

public interface ITargetRouteInterferenceReceiver
{
    void ApplyFakeBoxRouteInterference(Vector3 boxPosition, int reducedRouteCandidateCount);
}

public interface ITargetReverseRouteInterferenceReceiver
{
    void ApplyFakeBoxReverseRouteInterference(Vector3 boxPosition);
}