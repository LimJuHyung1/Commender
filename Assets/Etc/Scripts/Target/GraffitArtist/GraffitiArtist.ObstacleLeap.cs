using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public partial class GraffitiArtist
{
    public bool TryUseObstacleLeap(Vector3 escapeDestination)
    {
        if (!CanUseObstacleLeap())
            return false;

        if (!TryFindObstacleLeapDestination(
                escapeDestination,
                out Vector3 leapDestination,
                out Vector3 obstaclePeakPosition))
        {
            return false;
        }

        StartCoroutine(ObstacleLeapRoutine(leapDestination, obstaclePeakPosition));

        nextObstacleLeapReadyTime = Time.time + obstacleLeapCooldown;
        PlayTargetTrigger(obstacleLeapTriggerName);

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Obstacle leap used.");

        return true;
    }

    private IEnumerator ObstacleLeapRoutine(
        Vector3 leapDestination,
        Vector3 obstaclePeakPosition)
    {
        isLeaping = true;

        BeginObstacleLeapPresentation();

        Vector3 startPosition = transform.position;
        BeginObstacleLeapMovementMode();

        float obstacleT = CalculateObstacleTOnLeapPath(
            startPosition,
            leapDestination,
            obstaclePeakPosition
        );

        float groundYAtObstacle = Mathf.Lerp(startPosition.y, leapDestination.y, obstacleT);
        float requiredClearanceY = obstaclePeakPosition.y + obstacleLeapArcHeight;
        float obstacleArcValue = Mathf.Sin(obstacleT * Mathf.PI);

        float arcHeight = Mathf.Max(
            obstacleLeapArcHeight,
            (requiredClearanceY - groundYAtObstacle) / Mathf.Max(0.05f, obstacleArcValue)
        );

        float elapsed = 0f;

        while (elapsed < obstacleLeapDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, obstacleLeapDuration));

            Vector3 nextPosition = Vector3.Lerp(startPosition, leapDestination, t);
            float groundY = Mathf.Lerp(startPosition.y, leapDestination.y, t);
            float arcValue = Mathf.Sin(t * Mathf.PI);

            nextPosition.y = groundY + arcValue * arcHeight;

            MoveTargetDuringObstacleLeap(nextPosition);

            yield return null;
        }

        EndObstacleLeapMovementMode(leapDestination, true);

        if (obstacleLeapLandingDelay > 0f)
            yield return new WaitForSeconds(obstacleLeapLandingDelay);

        EndObstacleLeapPresentation();

        isLeaping = false;

        if (EscapeMotor != null)
            EscapeMotor.TryFleeFromThreats(true);
    }

    private void BeginObstacleLeapPresentation()
    {
        if (forceShowDuringObstacleLeap)
        {
            if (targetVisibilityController == null)
                targetVisibilityController = GetComponent<TargetVisibilityController>();

            if (targetVisibilityController != null)
            {
                targetVisibilityController.ForceShow();
                obstacleLeapForceVisibleActive = true;
            }
        }

        PlayObstacleLeapSkillCamera();
    }

    private void PlayObstacleLeapSkillCamera()
    {
        if (!useObstacleLeapSkillCamera)
            return;

        if (obstacleLeapCameraMode == SkillCameraFocusMode.None)
            return;

        if (skillCameraDirector == null)
            skillCameraDirector = FindFirstObjectByType<SkillCameraDirector>();

        if (forcePlayObstacleLeapSkillCamera && skillCameraDirector != null)
        {
            skillCameraDirector.StartCoroutine(
                skillCameraDirector.ForcePlaySkillCameraAndWait(
                    obstacleLeapCameraMode,
                    transform
                )
            );

            return;
        }

        SkillCameraEventBus.Request(
            obstacleLeapCameraMode,
            transform
        );
    }

    private void EndObstacleLeapPresentation()
    {
        if (!obstacleLeapForceVisibleActive)
            return;

        obstacleLeapForceVisibleActive = false;

        if (targetVisibilityController == null)
            targetVisibilityController = GetComponent<TargetVisibilityController>();

        if (targetVisibilityController != null)
            targetVisibilityController.ClearForceVisible();
    }

    private void BeginObstacleLeapMovementMode()
    {
        obstacleLeapMovementModeActive = true;

        if (!CanUseNavAgent())
        {
            hasSavedObstacleLeapAgentState = false;
            return;
        }

        savedObstacleLeapAgentStopped = NavAgent.isStopped;
        savedObstacleLeapUpdatePosition = NavAgent.updatePosition;
        savedObstacleLeapUpdateRotation = NavAgent.updateRotation;
        hasSavedObstacleLeapAgentState = true;

        NavAgent.isStopped = true;
        NavAgent.ResetPath();
        NavAgent.velocity = Vector3.zero;
        NavAgent.updatePosition = false;
        NavAgent.updateRotation = false;
    }

    private void MoveTargetDuringObstacleLeap(Vector3 position)
    {
        transform.position = position;
    }

    private float CalculateObstacleTOnLeapPath(
        Vector3 startPosition,
        Vector3 leapDestination,
        Vector3 obstaclePeakPosition)
    {
        Vector3 startFlat = new Vector3(startPosition.x, 0f, startPosition.z);
        Vector3 destinationFlat = new Vector3(leapDestination.x, 0f, leapDestination.z);
        Vector3 obstacleFlat = new Vector3(obstaclePeakPosition.x, 0f, obstaclePeakPosition.z);

        Vector3 path = destinationFlat - startFlat;

        if (path.sqrMagnitude <= 0.001f)
            return 0.5f;

        float t = Vector3.Dot(obstacleFlat - startFlat, path) / path.sqrMagnitude;
        return Mathf.Clamp(t, 0.15f, 0.85f);
    }

    private void EndObstacleLeapMovementMode(Vector3 finalPosition, bool resumeAfterLeap)
    {
        if (!obstacleLeapMovementModeActive)
            return;

        obstacleLeapMovementModeActive = false;

        if (CanUseNavAgent())
        {
            NavAgent.Warp(finalPosition);

            if (hasSavedObstacleLeapAgentState)
            {
                NavAgent.updatePosition = savedObstacleLeapUpdatePosition;
                NavAgent.updateRotation = savedObstacleLeapUpdateRotation;
                NavAgent.isStopped = resumeAfterLeap ? false : savedObstacleLeapAgentStopped;
            }
            else
            {
                NavAgent.updatePosition = true;
                NavAgent.updateRotation = true;
                NavAgent.isStopped = !resumeAfterLeap;
            }

            NavAgent.velocity = Vector3.zero;
        }
        else
        {
            transform.position = finalPosition;
        }
    }

    private bool ShouldUseObstacleLeap()
    {
        if (Time.time < nextObstacleLeapProbeReadyTime)
            return false;

        return CanUseObstacleLeapByThreatDistance();
    }

    private bool CanUseObstacleLeap()
    {
        if (!enableObstacleLeapSkill)
            return false;

        if (isLeaping)
            return false;

        if (isGraffitiCasting)
            return false;

        if (blockLeapWhenEscapeSkillBlocked && IsEscapeSkillBlocked)
            return false;

        if (Time.time < nextObstacleLeapReadyTime)
            return false;

        if (NavAgent == null)
            return false;

        if (obstacleLeapObjectLayerMask.value == 0)
            return false;

        return true;
    }

    private bool CanUseObstacleLeapByThreatDistance()
    {
        if (!CanUseObstacleLeap())
            return false;

        if (ThreatTracker == null)
            return true;

        float nearestThreatDistance = ThreatTracker.GetNearestRealAgentDistance();

        if (nearestThreatDistance < obstacleLeapActivationMinDistance)
            return false;

        if (nearestThreatDistance > obstacleLeapActivationMaxDistance)
            return false;

        return true;
    }

    private bool TryFindObstacleLeapDestination(
        Vector3 escapeDestination,
        out Vector3 leapDestination,
        out Vector3 obstaclePeakPosition)
    {
        leapDestination = transform.position;
        obstaclePeakPosition = transform.position + Vector3.up;

        Vector3 preferredDirection = CalculateObstacleLeapPreferredDirection(escapeDestination);

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            obstacleLeapSearchRadius,
            obstacleLeapObjectLayerMask,
            QueryTriggerInteraction.Ignore
        );

        if (colliders == null || colliders.Length == 0)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;
        Vector3 bestLanding = transform.position;
        Vector3 bestPeak = transform.position + Vector3.up;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider obstacleCollider = colliders[i];

            if (!IsValidObstacleLeapCollider(obstacleCollider))
                continue;

            if (!TryFindLandingOverObstacle(
                    obstacleCollider,
                    preferredDirection,
                    out Vector3 candidateLanding,
                    out Vector3 candidatePeak,
                    out float candidateScore))
            {
                continue;
            }

            if (candidateScore <= bestScore)
                continue;

            bestScore = candidateScore;
            bestLanding = candidateLanding;
            bestPeak = candidatePeak;
            found = true;
        }

        if (!found)
            return false;

        leapDestination = bestLanding;
        obstaclePeakPosition = bestPeak;
        return true;
    }

    private Vector3 CalculateObstacleLeapPreferredDirection(Vector3 escapeDestination)
    {
        Vector3 direction = escapeDestination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f && ThreatTracker != null)
            direction = ThreatTracker.CalculateCombinedFleeDirection();

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;

        direction.Normalize();
        return direction;
    }

    private bool IsValidObstacleLeapCollider(Collider obstacleCollider)
    {
        if (obstacleCollider == null)
            return false;

        if (obstacleCollider.transform == transform)
            return false;

        if (obstacleCollider.transform.IsChildOf(transform))
            return false;

        Bounds bounds = obstacleCollider.bounds;
        float objectHeight = bounds.size.y;

        if (objectHeight < obstacleLeapMinObjectHeight)
            return false;

        if (objectHeight > obstacleLeapMaxObjectHeight)
            return false;

        Vector3 obstacleDirection = bounds.center - transform.position;
        obstacleDirection.y = 0f;

        if (obstacleDirection.sqrMagnitude > obstacleLeapSearchRadius * obstacleLeapSearchRadius)
            return false;

        return true;
    }

    private bool TryFindLandingOverObstacle(
        Collider obstacleCollider,
        Vector3 preferredDirection,
        out Vector3 landingPosition,
        out Vector3 obstaclePeakPosition,
        out float score)
    {
        landingPosition = transform.position;
        obstaclePeakPosition = transform.position + Vector3.up;
        score = float.NegativeInfinity;

        Bounds bounds = obstacleCollider.bounds;

        Vector3 obstacleCenter = bounds.center;
        Vector3 obstacleCenterFlat = obstacleCenter;
        obstacleCenterFlat.y = transform.position.y;

        Vector3 directionToObstacle = obstacleCenterFlat - transform.position;
        directionToObstacle.y = 0f;

        if (directionToObstacle.sqrMagnitude <= 0.001f)
            directionToObstacle = preferredDirection;

        directionToObstacle.Normalize();

        float directionDot = Vector3.Dot(preferredDirection, directionToObstacle);

        if (directionDot < obstacleLeapMinDirectionDot)
            return false;

        Vector3 obstacleExtents = bounds.extents;

        float projectedObstacleExtent =
            Mathf.Abs(directionToObstacle.x) * obstacleExtents.x +
            Mathf.Abs(directionToObstacle.z) * obstacleExtents.z;

        Vector3 baseLandingPosition =
            obstacleCenterFlat +
            directionToObstacle * (projectedObstacleExtent + obstacleLeapLandingDistance);

        Vector3 sideDirection = new Vector3(
            -directionToObstacle.z,
            0f,
            directionToObstacle.x
        );

        int sampleCount = Mathf.Max(1, obstacleLeapLandingSampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            float sideOffset = GetAlternatingSideOffset(i) * obstacleLeapLandingSideOffset;

            Vector3 candidatePosition =
                baseLandingPosition +
                sideDirection * sideOffset;

            if (!NavMesh.SamplePosition(
                    candidatePosition,
                    out NavMeshHit navHit,
                    obstacleLeapNavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            if (!IsObstacleLeapLandingClear(navHit.position))
                continue;

            landingPosition = navHit.position;

            obstaclePeakPosition = bounds.center;
            obstaclePeakPosition.y = bounds.max.y;

            float distanceToObstacle = Vector3.Distance(
                transform.position,
                obstacleCenterFlat
            );

            score =
                directionDot * 10f -
                distanceToObstacle * 0.35f -
                Mathf.Abs(sideOffset) * 0.25f;

            return true;
        }

        return false;
    }

    private float GetAlternatingSideOffset(int index)
    {
        if (index == 0)
            return 0f;

        int pairIndex = (index + 1) / 2;
        float sign = index % 2 == 1 ? 1f : -1f;

        return pairIndex * sign;
    }

    private bool IsObstacleLeapLandingClear(Vector3 landingPosition)
    {
        if (!checkObstacleLeapLandingClearance)
            return true;

        Vector3 checkPosition =
            landingPosition +
            Vector3.up * obstacleLeapLandingClearanceHeight;

        bool blocked = Physics.CheckSphere(
            checkPosition,
            obstacleLeapLandingClearanceRadius,
            obstacleLeapObjectLayerMask,
            QueryTriggerInteraction.Ignore
        );

        return !blocked;
    }
}