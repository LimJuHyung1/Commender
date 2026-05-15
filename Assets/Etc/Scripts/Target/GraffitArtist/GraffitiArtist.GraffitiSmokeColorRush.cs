using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public partial class GraffitiArtist
{
    public bool TryCreateGraffitiZone()
    {
        if (!CanUseGraffiti())
            return false;

        graffitiCastRoutine = StartCoroutine(GraffitiCastRoutine());
        nextGraffitiReadyTime = Time.time + graffitiCooldown;

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Graffiti cast started.");

        return true;
    }

    public bool TryUseColorRush()
    {
        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Color Rush is passive. It cannot be used manually.");

        return false;
    }

    public override bool TryUseSmoke()
    {
        if (!CanUseTargetSkill(TargetSkillType.Smoke))
            return false;

        if (!CanUseSpraySmoke())
            return false;

        SpawnSpraySmokeEffect();

        remainingSpraySmokeUseCount--;
        nextSpraySmokeReadyTime = Time.time + spraySmokeCooldown;

        if (enableDebugLog)
        {
            Debug.Log(
                $"[GraffitiArtist] Spray smoke used. Remaining: {remainingSpraySmokeUseCount}"
            );
        }

        return true;
    }

    public void OnGraffitiSkillAnimationFinished()
    {
        if (!isGraffitiCasting)
            return;

        if (ShouldCancelGraffitiCast())
        {
            CancelGraffitiCastByThreat(true);
            return;
        }

        if (graffitiCastRoutine != null)
        {
            StopCoroutine(graffitiCastRoutine);
            graffitiCastRoutine = null;
        }

        bool created = CreateGraffitiZoneAfterAnimation();

        if (!created)
            nextGraffitiReadyTime = Time.time + graffitiRetryDelay;

        CompleteGraffitiCast(created);
    }

    private IEnumerator GraffitiCastRoutine()
    {
        isGraffitiCasting = true;

        PlayTargetTrigger(graffitiTriggerName);
        BeginGraffitiMovementLock();

        float elapsed = 0f;

        while (isGraffitiCasting)
        {
            if (ShouldCancelGraffitiCast())
            {
                CancelGraffitiCastByThreat(false);
                yield break;
            }

            KeepGraffitiMovementLocked();

            if (elapsed >= graffitiAnimationEventTimeout)
            {
                CancelGraffitiCastByTimeout();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool CreateGraffitiZoneAfterAnimation()
    {
        if (!TryFindGraffitiSpawnPosition(out Vector3 spawnPosition))
            return false;

        GameObject zoneObject;

        if (graffitiZonePrefab != null)
        {
            zoneObject = Instantiate(
                graffitiZonePrefab,
                spawnPosition,
                Quaternion.identity
            );
        }
        else
        {
            zoneObject = new GameObject("GraffitiZone");
            zoneObject.transform.position = spawnPosition;
        }

        GraffitiPaintZone graffitiZone = zoneObject.GetComponent<GraffitiPaintZone>();

        if (graffitiZone == null)
            graffitiZone = zoneObject.AddComponent<GraffitiPaintZone>();

        graffitiZone.Initialize(
            currentGraffitiRadius,
            graffitiDuration,
            agentLayerMask,
            enableDebugLog
        );

        activeGraffitiZones.Add(graffitiZone);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[GraffitiArtist] Graffiti zone created. " +
                $"Radius: {currentGraffitiRadius}, " +
                $"Position: {spawnPosition}"
            );
        }

        return true;
    }

    private bool TryFindGraffitiSpawnPosition(out Vector3 spawnPosition)
    {
        if (useRandomGraffitiSpawnPosition)
            return TryFindRandomGraffitiSpawnPosition(out spawnPosition);

        spawnPosition = transform.position;

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                graffitiNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
            return true;
        }

        return false;
    }

    private bool TryFindRandomGraffitiSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = transform.position;

        for (int i = 0; i < graffitiRandomSpawnSampleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle;

            if (randomCircle.sqrMagnitude <= 0.001f)
                randomCircle = Vector2.right;

            Vector2 direction = randomCircle.normalized;

            float distance = Random.Range(
                currentGraffitiSpawnMinDistance,
                currentGraffitiSpawnMaxDistance
            );

            Vector3 candidatePosition = transform.position + new Vector3(
                direction.x,
                0f,
                direction.y
            ) * distance;

            if (!NavMesh.SamplePosition(
                    candidatePosition,
                    out NavMeshHit hit,
                    graffitiNavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            spawnPosition = hit.position;
            return true;
        }

        return false;
    }

    private void ApplyGraffitiDifficultyScaling(int targetLevel)
    {
        currentGraffitiDifficultyLevel = Mathf.Clamp(
            targetLevel,
            1,
            maxGraffitiDifficultyLevel
        );

        float difficulty01 = 0f;

        if (scaleGraffitiByStageDifficulty && maxGraffitiDifficultyLevel > 1)
        {
            difficulty01 = Mathf.InverseLerp(
                1f,
                maxGraffitiDifficultyLevel,
                currentGraffitiDifficultyLevel
            );

            if (graffitiDifficultyCurve != null)
                difficulty01 = graffitiDifficultyCurve.Evaluate(difficulty01);

            difficulty01 = Mathf.Clamp01(difficulty01);
        }

        currentGraffitiRadius = Mathf.Lerp(
            graffitiRadius,
            maxGraffitiRadius,
            difficulty01
        );

        currentGraffitiSpawnMinDistance = Mathf.Lerp(
            graffitiRandomSpawnMinDistance,
            maxGraffitiSpawnMinDistance,
            difficulty01
        );

        currentGraffitiSpawnMaxDistance = Mathf.Lerp(
            graffitiRandomSpawnMaxDistance,
            maxGraffitiSpawnMaxDistance,
            difficulty01
        );

        if (currentGraffitiSpawnMaxDistance < currentGraffitiSpawnMinDistance)
        {
            float temp = currentGraffitiSpawnMinDistance;
            currentGraffitiSpawnMinDistance = currentGraffitiSpawnMaxDistance;
            currentGraffitiSpawnMaxDistance = temp;
        }
    }

    private bool ShouldCancelGraffitiCast()
    {
        if (!isGraffitiCasting)
            return true;

        if (IsTargetUnableToUseSkills())
            return true;

        if (HasActiveThreat())
            return true;

        return false;
    }

    private void CancelGraffitiCastByThreat(bool stopRoutine)
    {
        if (!isGraffitiCasting)
            return;

        if (stopRoutine && graffitiCastRoutine != null)
            StopCoroutine(graffitiCastRoutine);

        isGraffitiCasting = false;
        graffitiCastRoutine = null;

        nextGraffitiReadyTime = Time.time + graffitiCancelledRetryDelay;

        ResetTargetTrigger(graffitiTriggerName);
        CancelGraffitiAnimationIfNeeded();
        EndGraffitiMovementLock(true);

        if (EscapeMotor != null)
            EscapeMotor.TryFleeFromThreats(true);

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Graffiti cast cancelled because threat appeared.");
    }

    private void CancelGraffitiCastByTimeout()
    {
        if (!isGraffitiCasting)
            return;

        isGraffitiCasting = false;
        graffitiCastRoutine = null;

        nextGraffitiReadyTime = Time.time + graffitiRetryDelay;

        EndGraffitiMovementLock(false);

        if (enableDebugLog)
        {
            Debug.LogWarning(
                "[GraffitiArtist] Graffiti cast timed out. " +
                "Check Animation Event timing or increase timeout."
            );
        }
    }

    private void CompleteGraffitiCast(bool created)
    {
        isGraffitiCasting = false;
        graffitiCastRoutine = null;

        EndGraffitiMovementLock(false);

        if (!created && enableDebugLog)
        {
            Debug.Log(
                "[GraffitiArtist] Graffiti cast finished, but spawn position was not found."
            );
        }
    }

    private void ForceEndGraffitiCastState(bool forceResume)
    {
        isGraffitiCasting = false;
        graffitiCastRoutine = null;

        EndGraffitiMovementLock(forceResume);
    }

    private void BeginGraffitiMovementLock()
    {
        graffitiMovementLockActive = true;

        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        if (CanUseNavAgent())
        {
            savedGraffitiAgentStopState = NavAgent.isStopped;
            hasSavedGraffitiAgentStopState = true;

            NavAgent.isStopped = true;
            NavAgent.ResetPath();
            NavAgent.velocity = Vector3.zero;
        }
        else
        {
            hasSavedGraffitiAgentStopState = false;
        }
    }

    private void KeepGraffitiMovementLocked()
    {
        if (!graffitiMovementLockActive)
            return;

        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        if (!CanUseNavAgent())
            return;

        NavAgent.isStopped = true;
        NavAgent.ResetPath();
        NavAgent.velocity = Vector3.zero;
    }

    private void EndGraffitiMovementLock(bool forceResume)
    {
        if (!graffitiMovementLockActive)
            return;

        graffitiMovementLockActive = false;

        if (!CanUseNavAgent())
            return;

        NavAgent.velocity = Vector3.zero;

        if (forceResume)
        {
            NavAgent.isStopped = false;
            return;
        }

        if (hasSavedGraffitiAgentStopState)
            NavAgent.isStopped = savedGraffitiAgentStopState;
        else
            NavAgent.isStopped = false;
    }

    private bool ShouldUseSpraySmoke()
    {
        return ShouldUseSpraySmoke(GetNearestThreatDistance());
    }

    private bool ShouldUseSpraySmoke(float nearestThreatDistance)
    {
        if (!CanUseSpraySmoke())
            return false;

        if (float.IsInfinity(nearestThreatDistance))
            return false;

        return nearestThreatDistance <= spraySmokeActivationDistance;
    }

    private float GetNearestThreatDistance()
    {
        if (ThreatTracker == null)
            return float.PositiveInfinity;

        return ThreatTracker.GetNearestRealAgentDistance();
    }

    private bool CanUseGraffiti()
    {
        if (!enableGraffitiSkill)
            return false;

        if (isGraffitiCasting)
            return false;

        if (HasActiveThreat())
            return false;

        if (allowOnlyOneActiveGraffitiZone && activeGraffitiZones.Count > 0)
            return false;

        if (Time.time < nextGraffitiReadyTime)
            return false;

        return true;
    }

    private bool CanUseSpraySmoke()
    {
        if (!enableSpraySmokeSkill)
            return false;

        if (remainingSpraySmokeUseCount <= 0)
            return false;

        if (Time.time < nextSpraySmokeReadyTime)
            return false;

        return true;
    }

    private void SpawnSpraySmokeEffect()
    {
        if (spraySmokePrefab == null)
            return;

        Vector3 spawnPosition = GetSpraySmokeSpawnPosition();
        Quaternion spawnRotation = transform.rotation;

        Instantiate(
            spraySmokePrefab,
            spawnPosition,
            spawnRotation
        );
    }

    private Vector3 GetSpraySmokeSpawnPosition()
    {
        if (spraySmokeSpawnPoint != null)
            return spraySmokeSpawnPoint.position + spraySmokeSpawnOffset;

        return transform.position + spraySmokeSpawnOffset;
    }

    private void UpdateColorRushPassiveState()
    {
        if (enableColorRushSkill)
        {
            EnableColorRushPassive();
            return;
        }

        DisableColorRushPassive();
    }

    private void EnableColorRushPassive()
    {
        if (isColorRushPassiveActive)
            return;

        isColorRushPassiveActive = true;
        hasLastTrailSpawnPosition = false;

        ResolveColorRushTrailPool();

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Color Rush passive enabled.");
    }

    private void DisableColorRushPassive()
    {
        if (!isColorRushPassiveActive && !isColorRushSpeedMultiplierApplied)
            return;

        RemoveColorRushSpeedMultiplier();

        isColorRushPassiveActive = false;
        hasLastTrailSpawnPosition = false;

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Color Rush passive disabled.");
    }

    private void UpdateColorRushPassive()
    {
        if (!enableColorRushSkill)
        {
            DisableColorRushPassive();
            return;
        }

        if (!isColorRushPassiveActive)
            EnableColorRushPassive();

        UpdateColorRushSpeedMultiplierState();
        TrySpawnColorRushTrailByDistance();
    }

    private void UpdateColorRushSpeedMultiplierState()
    {
        if (HasActiveThreat())
        {
            ApplyColorRushSpeedMultiplier();
            return;
        }

        RemoveColorRushSpeedMultiplier();
    }

    private void ApplyColorRushSpeedMultiplier()
    {
        if (EscapeMotor == null)
            return;

        EscapeMotor.SetExternalSpeedMultiplier(this, colorRushPassiveSpeedMultiplier);
        isColorRushSpeedMultiplierApplied = true;
    }

    private void RemoveColorRushSpeedMultiplier()
    {
        if (EscapeMotor != null && isColorRushSpeedMultiplierApplied)
            EscapeMotor.RemoveExternalSpeedMultiplier(this);

        isColorRushSpeedMultiplierApplied = false;
    }

    private void TrySpawnColorRushTrailByDistance()
    {
        if (colorRushTrailPrefab == null)
            return;

        if (isGraffitiCasting)
        {
            hasLastTrailSpawnPosition = false;
            return;
        }

        if (!IsTargetMovingForColorRushTrail())
        {
            hasLastTrailSpawnPosition = false;
            return;
        }

        Vector3 currentPosition = transform.position;

        if (!hasLastTrailSpawnPosition)
        {
            lastTrailSpawnPosition = currentPosition;
            hasLastTrailSpawnPosition = true;
            return;
        }

        float movedDistance = Vector3.Distance(lastTrailSpawnPosition, currentPosition);

        if (movedDistance < colorRushTrailSpawnDistance)
            return;

        SpawnColorRushTrailAtCurrentPosition();

        lastTrailSpawnPosition = currentPosition;
    }

    private bool IsTargetMovingForColorRushTrail()
    {
        if (NavAgent == null)
            return true;

        if (!NavAgent.enabled)
            return true;

        if (!NavAgent.isActiveAndEnabled)
            return true;

        if (!NavAgent.isOnNavMesh)
            return true;

        float minSpeedSqr = colorRushTrailMinMoveSpeed * colorRushTrailMinMoveSpeed;

        if (NavAgent.velocity.sqrMagnitude > minSpeedSqr)
            return true;

        if (NavAgent.desiredVelocity.sqrMagnitude > minSpeedSqr)
            return true;

        return false;
    }

    private void SpawnColorRushTrailAtCurrentPosition()
    {
        ResolveColorRushTrailPool();

        if (colorRushTrailPool == null)
            return;

        Vector3 spawnPosition = transform.position;

        if (NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit hit,
                colorRushTrailNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }

        spawnPosition += Vector3.up * colorRushTrailYOffset;

        colorRushTrailPool.SpawnTrail(
            spawnPosition,
            Quaternion.identity,
            colorRushTrailLifetime
        );
    }

    private void ResolveColorRushTrailPool()
    {
        if (colorRushTrailPrefab == null)
            return;

        if (colorRushTrailPool == null && autoCreateColorRushTrailPool)
        {
            GameObject poolObject = new GameObject("ColorRushTrailPool");
            poolObject.transform.SetParent(null);

            colorRushTrailPool = poolObject.AddComponent<ColorRushTrailPool>();
        }

        if (colorRushTrailPool == null)
            return;

        if (colorRushTrailPoolInitialized)
            return;

        colorRushTrailPool.Initialize(
            colorRushTrailPrefab,
            colorRushTrailPoolSize,
            colorRushTrailParent
        );

        colorRushTrailPoolInitialized = true;
    }

    private void TryUseSafeStateGraffiti()
    {
        if (!autoCreateGraffitiWhenSafe)
            return;

        if (!enableGraffitiSkill)
            return;

        if (isGraffitiCasting)
            return;

        if (HasActiveThreat())
            return;

        if (Time.time - runtimeStartTime < safeGraffitiInitialDelay)
            return;

        float safeStateStartTime = threatLastLostTime < 0f
            ? runtimeStartTime
            : threatLastLostTime;

        if (Time.time - safeStateStartTime < safeGraffitiMinNoThreatTime)
            return;

        TryCreateGraffitiZone();
    }
}