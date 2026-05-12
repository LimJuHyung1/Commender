using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GraffitiArtist : TargetSkillController
{
    [Header("Obstacle Leap")]
    [SerializeField] private float obstacleLeapCooldown = 8f;
    [SerializeField] private float obstacleLeapDistance = 5f;
    [SerializeField] private float obstacleLeapDuration = 0.35f;
    [SerializeField] private float obstacleLeapArcHeight = 1.2f;
    [SerializeField] private float obstacleLeapNavMeshSampleRadius = 3f;
    [SerializeField] private float obstacleLeapMinThreatDistance = 7f;
    [SerializeField] private bool blockLeapWhenEscapeSkillBlocked = true;

    [Header("Spray Paint Zone")]
    [SerializeField] private GameObject sprayPaintZonePrefab;
    [SerializeField] private float sprayPaintCooldown = 12f;
    [SerializeField] private float sprayPaintRetryDelay = 3f;
    [SerializeField] private float sprayPaintDuration = 7f;
    [SerializeField] private float sprayPaintRadius = 3.5f;
    [SerializeField][Range(0.1f, 1f)] private float sprayPaintMoveSpeedMultiplier = 0.55f;
    [SerializeField] private float sprayPaintSpawnBackDistance = 1.3f;
    [SerializeField] private float sprayPaintNavMeshSampleRadius = 3f;
    [SerializeField] private LayerMask agentLayerMask;
    [SerializeField] private bool allowOnlyOneActiveSprayPaintZone = true;

    [Header("Paint Smoke")]
    [SerializeField] private GameObject paintSmokePrefab;
    [SerializeField] private Transform paintSmokeSpawnPoint;
    [SerializeField] private Vector3 paintSmokeSpawnOffset = Vector3.zero;
    [SerializeField] private float paintSmokeCooldown = 14f;
    [SerializeField] private float paintSmokeDuration = 5f;
    [SerializeField] private float paintSmokeDetectionRadius = 1.2f;
    [SerializeField] private bool autoDestroyPaintSmoke = true;
    [SerializeField] private float paintSmokeDestroyDelay = 5f;

    [Header("Wall Run Passive")]
    [SerializeField] private LayerMask wallLayerMask;
    [SerializeField] private float wallCheckDistance = 1.2f;
    [SerializeField] private float wallCheckSphereRadius = 0.2f;
    [SerializeField] private float wallCheckHeightOffset = 0.6f;
    [SerializeField] private float wallRunSpeedMultiplier = 1.2f;
    [SerializeField] private float wallRunAccelerationMultiplier = 1.15f;
    [SerializeField] private bool requireThreatForWallRun = true;

    [Header("Skill Unlock Level")]
    [SerializeField] private int obstacleLeapUnlockLevel = 1;
    [SerializeField] private int sprayPaintUnlockLevel = 3;
    [SerializeField] private int paintSmokeUnlockLevel = 5;
    [SerializeField] private int wallRunUnlockLevel = 7;

    [Header("Auto Defensive Skill")]
    [SerializeField] private bool autoUseOnlyOncePerThreatEncounter = true;
    [SerializeField] private float autoDefensiveSharedCooldown = 6f;
    [SerializeField] private float threatResetDelay = 1f;

    [Header("Animation Trigger Names")]
    [SerializeField] private string obstacleLeapTriggerName = "SkillObstacleLeap";
    [SerializeField] private string sprayPaintTriggerName = "SkillSprayPaint";
    [SerializeField] private string paintSmokeTriggerName = "SkillPaintSmoke";
    [SerializeField] private string wallRunBoolName = "IsWallRunning";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<GraffitiPaintZone> activeSprayPaintZones = new List<GraffitiPaintZone>();

    private Animator targetAnimator;

    private bool enableObstacleLeapSkill;
    private bool enableSprayPaintSkill;
    private bool enablePaintSmokeSkill;
    private bool enableWallRunPassive;

    private bool wasThreatActiveLastFrame;
    private bool autoDefensiveUsedInCurrentThreatEncounter;
    private bool isLeaping;
    private bool isWallRunning;

    private float threatLastLostTime = -999f;
    private float nextObstacleLeapReadyTime = -999f;
    private float nextSprayPaintReadyTime = -999f;
    private float nextPaintSmokeReadyTime = -999f;
    private float nextAutoDefensiveReadyTime = -999f;

    public bool IsObstacleLeapUnlocked => enableObstacleLeapSkill;
    public bool IsSprayPaintUnlocked => enableSprayPaintSkill;
    public bool IsWallRunUnlocked => enableWallRunPassive;

    public override bool IsSmokeUnlocked => enablePaintSmokeSkill;

    public float ObstacleLeapRemainingCooldown =>
        Mathf.Max(0f, nextObstacleLeapReadyTime - Time.time);

    public float SprayPaintRemainingCooldown =>
        Mathf.Max(0f, nextSprayPaintReadyTime - Time.time);

    public override float SmokeRemainingCooldown =>
        Mathf.Max(0f, nextPaintSmokeReadyTime - Time.time);

    protected override void Awake()
    {
        base.Awake();

        ResolveLocalReferences();
        ClampValues();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ClampValues();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        StopAllCoroutines();
        DestroyActiveSprayPaintZones();
        isLeaping = false;
        SetWallRunAnimation(false);
    }

    private void Update()
    {
        if (IsTargetUnableToUseSkills())
            return;

        UpdateThreatEncounterState();
        CleanupSprayPaintZoneList();
    }

    private void LateUpdate()
    {
        TickWallRunPassive();
    }

    public override void ApplySkillUnlocks(int targetLevel)
    {
        enableObstacleLeapSkill = targetLevel >= obstacleLeapUnlockLevel;
        enableSprayPaintSkill = targetLevel >= sprayPaintUnlockLevel;
        enablePaintSmokeSkill = targetLevel >= paintSmokeUnlockLevel;
        enableWallRunPassive = targetLevel >= wallRunUnlockLevel;

        ResetRuntimeState(true, true);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[GraffitiArtist] Skill unlock applied. " +
                $"Level: {targetLevel}, " +
                $"ObstacleLeap: {enableObstacleLeapSkill}, " +
                $"SprayPaint: {enableSprayPaintSkill}, " +
                $"PaintSmoke: {enablePaintSmokeSkill}, " +
                $"WallRun: {enableWallRunPassive}"
            );
        }
    }

    public override void ResetRuntimeState(
        bool destroyActiveBarricades = true,
        bool destroyActiveHologram = true)
    {
        base.ResetRuntimeState(destroyActiveBarricades, destroyActiveHologram);

        StopAllCoroutines();

        DestroyActiveSprayPaintZones();

        wasThreatActiveLastFrame = false;
        autoDefensiveUsedInCurrentThreatEncounter = false;
        isLeaping = false;
        isWallRunning = false;

        threatLastLostTime = -999f;
        nextObstacleLeapReadyTime = -999f;
        nextSprayPaintReadyTime = -999f;
        nextPaintSmokeReadyTime = -999f;
        nextAutoDefensiveReadyTime = -999f;

        SetWallRunAnimation(false);
    }

    public override bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        if (IsTargetUnableToUseSkills())
            return false;

        if (!CanUseAutoDefensiveSkill())
            return false;

        bool used = false;

        if (CanUseObstacleLeapByThreatDistance())
            used = TryUseObstacleLeap(escapeDestination);

        if (!used)
            used = TryCreateSprayPaintZone();

        if (!used)
            used = TryUseSmoke();

        if (!used)
            return false;

        nextAutoDefensiveReadyTime = Time.time + autoDefensiveSharedCooldown;
        autoDefensiveUsedInCurrentThreatEncounter = true;

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Auto defensive skill used.");

        return true;
    }

    public bool TryUseObstacleLeap(Vector3 escapeDestination)
    {
        if (!CanUseObstacleLeap())
            return false;

        if (!TryFindLeapDestination(escapeDestination, out Vector3 leapDestination))
            return false;

        StartCoroutine(ObstacleLeapRoutine(leapDestination));

        nextObstacleLeapReadyTime = Time.time + obstacleLeapCooldown;
        PlayTargetTrigger(obstacleLeapTriggerName);

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Obstacle leap used.");

        return true;
    }

    public bool TryCreateSprayPaintZone()
    {
        if (!CanUseSprayPaint())
            return false;

        Vector3 spawnPosition = GetSprayPaintSpawnPosition();

        if (!NavMesh.SamplePosition(
                spawnPosition,
                out NavMeshHit hit,
                sprayPaintNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            nextSprayPaintReadyTime = Time.time + sprayPaintRetryDelay;
            return false;
        }

        GameObject zoneObject;

        if (sprayPaintZonePrefab != null)
        {
            zoneObject = Instantiate(
                sprayPaintZonePrefab,
                hit.position,
                Quaternion.identity
            );
        }
        else
        {
            zoneObject = new GameObject("GraffitiPaintZone");
            zoneObject.transform.position = hit.position;
        }

        GraffitiPaintZone paintZone = zoneObject.GetComponent<GraffitiPaintZone>();

        if (paintZone == null)
            paintZone = zoneObject.AddComponent<GraffitiPaintZone>();

        paintZone.Initialize(
            sprayPaintRadius,
            sprayPaintDuration,
            sprayPaintMoveSpeedMultiplier,
            agentLayerMask,
            enableDebugLog
        );

        activeSprayPaintZones.Add(paintZone);

        nextSprayPaintReadyTime = Time.time + sprayPaintCooldown;
        PlayTargetTrigger(sprayPaintTriggerName);

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Spray paint zone created.");

        return true;
    }

    public override bool TryUseSmoke()
    {
        if (!CanUseTargetSkill(TargetSkillType.Smoke))
            return false;

        if (!CanUsePaintSmoke())
            return false;

        SpawnPaintSmokeEffect();

        if (ThreatTracker != null)
            ThreatTracker.ApplySmokeDebuff(paintSmokeDetectionRadius, paintSmokeDuration);

        nextPaintSmokeReadyTime = Time.time + paintSmokeCooldown;
        PlayTargetTrigger(paintSmokeTriggerName);

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Paint smoke used.");

        return true;
    }

    private IEnumerator ObstacleLeapRoutine(Vector3 leapDestination)
    {
        isLeaping = true;

        StopTargetNavigation();

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < obstacleLeapDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, obstacleLeapDuration));

            Vector3 nextPosition = Vector3.Lerp(startPosition, leapDestination, t);
            nextPosition += Vector3.up * Mathf.Sin(t * Mathf.PI) * obstacleLeapArcHeight;

            WarpTarget(nextPosition);

            yield return null;
        }

        WarpTarget(leapDestination);
        StopTargetNavigation();

        isLeaping = false;

        if (EscapeMotor != null)
            EscapeMotor.TryFleeFromThreats(true);
    }

    private bool CanUseAutoDefensiveSkill()
    {
        if (Time.time < nextAutoDefensiveReadyTime)
            return false;

        if (ThreatTracker == null)
            return false;

        if (!ThreatTracker.HasAnyThreat())
            return false;

        if (autoUseOnlyOncePerThreatEncounter &&
            autoDefensiveUsedInCurrentThreatEncounter)
        {
            return false;
        }

        return true;
    }

    private bool CanUseObstacleLeap()
    {
        if (!enableObstacleLeapSkill)
            return false;

        if (isLeaping)
            return false;

        if (blockLeapWhenEscapeSkillBlocked && IsEscapeSkillBlocked)
            return false;

        if (Time.time < nextObstacleLeapReadyTime)
            return false;

        if (NavAgent == null)
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

        return nearestThreatDistance <= obstacleLeapMinThreatDistance;
    }

    private bool CanUseSprayPaint()
    {
        if (!enableSprayPaintSkill)
            return false;

        if (allowOnlyOneActiveSprayPaintZone && activeSprayPaintZones.Count > 0)
            return false;

        if (Time.time < nextSprayPaintReadyTime)
            return false;

        return true;
    }

    private bool CanUsePaintSmoke()
    {
        if (!enablePaintSmokeSkill)
            return false;

        if (Time.time < nextPaintSmokeReadyTime)
            return false;

        return true;
    }

    private bool TryFindLeapDestination(
        Vector3 escapeDestination,
        out Vector3 leapDestination)
    {
        leapDestination = transform.position;

        Vector3 direction = escapeDestination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f && ThreatTracker != null)
            direction = ThreatTracker.CalculateCombinedFleeDirection();

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;

        direction.Normalize();

        Vector3 desiredPosition = transform.position + direction * obstacleLeapDistance;

        if (!NavMesh.SamplePosition(
                desiredPosition,
                out NavMeshHit hit,
                obstacleLeapNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        leapDestination = hit.position;
        return true;
    }

    private Vector3 GetSprayPaintSpawnPosition()
    {
        Vector3 backDirection = -transform.forward;
        backDirection.y = 0f;

        if (backDirection.sqrMagnitude <= 0.001f)
            backDirection = Vector3.back;

        backDirection.Normalize();

        return transform.position + backDirection * sprayPaintSpawnBackDistance;
    }

    private void SpawnPaintSmokeEffect()
    {
        if (paintSmokePrefab == null)
            return;

        Vector3 spawnPosition = GetPaintSmokeSpawnPosition();
        Quaternion spawnRotation = transform.rotation;

        GameObject smokeObject = Instantiate(
            paintSmokePrefab,
            spawnPosition,
            spawnRotation
        );

        if (autoDestroyPaintSmoke)
            Destroy(smokeObject, paintSmokeDestroyDelay);
    }

    private Vector3 GetPaintSmokeSpawnPosition()
    {
        if (paintSmokeSpawnPoint != null)
            return paintSmokeSpawnPoint.position + paintSmokeSpawnOffset;

        return transform.position + paintSmokeSpawnOffset;
    }

    private void TickWallRunPassive()
    {
        bool shouldWallRun = CanUseWallRunPassive();

        if (shouldWallRun && NavAgent != null)
        {
            NavAgent.speed *= wallRunSpeedMultiplier;
            NavAgent.acceleration *= wallRunAccelerationMultiplier;
        }

        if (isWallRunning != shouldWallRun)
        {
            isWallRunning = shouldWallRun;
            SetWallRunAnimation(isWallRunning);

            if (enableDebugLog)
                Debug.Log($"[GraffitiArtist] Wall run state changed: {isWallRunning}");
        }
    }

    private bool CanUseWallRunPassive()
    {
        if (!enableWallRunPassive)
            return false;

        if (isLeaping)
            return false;

        if (TargetController == null)
            return false;

        if (requireThreatForWallRun && !TargetController.HasActiveThreat)
            return false;

        if (wallLayerMask.value == 0)
            return false;

        return IsNearWall();
    }

    private bool IsNearWall()
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeightOffset;

        if (Physics.SphereCast(
                origin,
                wallCheckSphereRadius,
                transform.forward,
                out _,
                wallCheckDistance,
                wallLayerMask))
        {
            return true;
        }

        if (Physics.SphereCast(
                origin,
                wallCheckSphereRadius,
                -transform.forward,
                out _,
                wallCheckDistance,
                wallLayerMask))
        {
            return true;
        }

        if (Physics.SphereCast(
                origin,
                wallCheckSphereRadius,
                transform.right,
                out _,
                wallCheckDistance,
                wallLayerMask))
        {
            return true;
        }

        if (Physics.SphereCast(
                origin,
                wallCheckSphereRadius,
                -transform.right,
                out _,
                wallCheckDistance,
                wallLayerMask))
        {
            return true;
        }

        return false;
    }

    private void UpdateThreatEncounterState()
    {
        bool hasThreat = ThreatTracker != null && ThreatTracker.HasAnyThreat();

        if (hasThreat)
        {
            if (!wasThreatActiveLastFrame)
                autoDefensiveUsedInCurrentThreatEncounter = false;
        }
        else
        {
            if (wasThreatActiveLastFrame)
                threatLastLostTime = Time.time;

            if (Time.time - threatLastLostTime >= threatResetDelay)
                autoDefensiveUsedInCurrentThreatEncounter = false;
        }

        wasThreatActiveLastFrame = hasThreat;
    }

    private void StopTargetNavigation()
    {
        if (NavAgent == null)
            return;

        if (!NavAgent.enabled)
            return;

        if (!NavAgent.isActiveAndEnabled)
            return;

        if (!NavAgent.isOnNavMesh)
            return;

        NavAgent.isStopped = false;
        NavAgent.ResetPath();
        NavAgent.velocity = Vector3.zero;
    }

    private void WarpTarget(Vector3 position)
    {
        if (NavAgent != null &&
            NavAgent.enabled &&
            NavAgent.isActiveAndEnabled &&
            NavAgent.isOnNavMesh)
        {
            NavAgent.Warp(position);
        }
        else
        {
            transform.position = position;
        }
    }

    private void DestroyActiveSprayPaintZones()
    {
        for (int i = activeSprayPaintZones.Count - 1; i >= 0; i--)
        {
            GraffitiPaintZone paintZone = activeSprayPaintZones[i];

            if (paintZone != null)
                Destroy(paintZone.gameObject);
        }

        activeSprayPaintZones.Clear();
    }

    private void CleanupSprayPaintZoneList()
    {
        for (int i = activeSprayPaintZones.Count - 1; i >= 0; i--)
        {
            if (activeSprayPaintZones[i] == null)
                activeSprayPaintZones.RemoveAt(i);
        }
    }

    private void ResolveLocalReferences()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();
    }

    private void PlayTargetTrigger(string triggerName)
    {
        if (targetAnimator == null)
            return;

        if (!HasAnimatorParameter(
                targetAnimator,
                triggerName,
                AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        targetAnimator.SetTrigger(triggerName);
    }

    private void SetWallRunAnimation(bool value)
    {
        if (targetAnimator == null)
            return;

        if (!HasAnimatorParameter(
                targetAnimator,
                wallRunBoolName,
                AnimatorControllerParameterType.Bool))
        {
            return;
        }

        targetAnimator.SetBool(wallRunBoolName, value);
    }

    private bool HasAnimatorParameter(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        if (string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type != parameterType)
                continue;

            if (parameter.name == parameterName)
                return true;
        }

        return false;
    }

    private void ClampValues()
    {
        obstacleLeapCooldown = Mathf.Max(0.1f, obstacleLeapCooldown);
        obstacleLeapDistance = Mathf.Max(0.1f, obstacleLeapDistance);
        obstacleLeapDuration = Mathf.Max(0.05f, obstacleLeapDuration);
        obstacleLeapArcHeight = Mathf.Max(0f, obstacleLeapArcHeight);
        obstacleLeapNavMeshSampleRadius = Mathf.Max(0.1f, obstacleLeapNavMeshSampleRadius);
        obstacleLeapMinThreatDistance = Mathf.Max(0.1f, obstacleLeapMinThreatDistance);

        sprayPaintCooldown = Mathf.Max(0.1f, sprayPaintCooldown);
        sprayPaintRetryDelay = Mathf.Max(0.1f, sprayPaintRetryDelay);
        sprayPaintDuration = Mathf.Max(0.1f, sprayPaintDuration);
        sprayPaintRadius = Mathf.Max(0.1f, sprayPaintRadius);
        sprayPaintMoveSpeedMultiplier = Mathf.Clamp(sprayPaintMoveSpeedMultiplier, 0.1f, 1f);
        sprayPaintSpawnBackDistance = Mathf.Max(0f, sprayPaintSpawnBackDistance);
        sprayPaintNavMeshSampleRadius = Mathf.Max(0.1f, sprayPaintNavMeshSampleRadius);

        paintSmokeCooldown = Mathf.Max(0.1f, paintSmokeCooldown);
        paintSmokeDuration = Mathf.Max(0.1f, paintSmokeDuration);
        paintSmokeDetectionRadius = Mathf.Max(0f, paintSmokeDetectionRadius);
        paintSmokeDestroyDelay = Mathf.Max(0.1f, paintSmokeDestroyDelay);

        wallCheckDistance = Mathf.Max(0.1f, wallCheckDistance);
        wallCheckSphereRadius = Mathf.Max(0.01f, wallCheckSphereRadius);
        wallCheckHeightOffset = Mathf.Max(0f, wallCheckHeightOffset);
        wallRunSpeedMultiplier = Mathf.Max(1f, wallRunSpeedMultiplier);
        wallRunAccelerationMultiplier = Mathf.Max(1f, wallRunAccelerationMultiplier);

        obstacleLeapUnlockLevel = Mathf.Max(1, obstacleLeapUnlockLevel);
        sprayPaintUnlockLevel = Mathf.Max(1, sprayPaintUnlockLevel);
        paintSmokeUnlockLevel = Mathf.Max(1, paintSmokeUnlockLevel);
        wallRunUnlockLevel = Mathf.Max(1, wallRunUnlockLevel);

        autoDefensiveSharedCooldown = Mathf.Max(0.1f, autoDefensiveSharedCooldown);
        threatResetDelay = Mathf.Max(0f, threatResetDelay);
    }
}

public class GraffitiPaintZone : MonoBehaviour
{
    [SerializeField] private float radius = 3.5f;
    [SerializeField] private float duration = 7f;
    [SerializeField][Range(0.1f, 1f)] private float moveSpeedMultiplier = 0.55f;
    [SerializeField] private LayerMask agentLayerMask;
    [SerializeField] private float tickInterval = 0.05f;
    [SerializeField] private bool enableDebugLog = true;

    private readonly Dictionary<NavMeshAgent, float> originalAgentSpeeds = new Dictionary<NavMeshAgent, float>();
    private readonly HashSet<NavMeshAgent> agentsInsideThisTick = new HashSet<NavMeshAgent>();

    private float spawnTime;
    private float nextTickTime;

    public void Initialize(
        float radius,
        float duration,
        float moveSpeedMultiplier,
        LayerMask agentLayerMask,
        bool enableDebugLog)
    {
        this.radius = Mathf.Max(0.1f, radius);
        this.duration = Mathf.Max(0.1f, duration);
        this.moveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier, 0.1f, 1f);
        this.agentLayerMask = agentLayerMask;
        this.enableDebugLog = enableDebugLog;

        spawnTime = Time.time;
        nextTickTime = Time.time;

        if (this.enableDebugLog)
            Debug.Log("[GraffitiPaintZone] Initialized.");
    }

    private void Awake()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (Time.time - spawnTime >= duration)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < nextTickTime)
            return;

        nextTickTime = Time.time + tickInterval;

        TickAgentsInsideZone();
    }

    private void OnDestroy()
    {
        RestoreAllAgents();
    }

    private void TickAgentsInsideZone()
    {
        agentsInsideThisTick.Clear();

        Collider[] colliders;

        if (agentLayerMask.value != 0)
        {
            colliders = Physics.OverlapSphere(
                transform.position,
                radius,
                agentLayerMask
            );
        }
        else
        {
            colliders = Physics.OverlapSphere(
                transform.position,
                radius
            );
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];

            if (currentCollider == null)
                continue;

            AgentController agentController =
                currentCollider.GetComponentInParent<AgentController>();

            if (agentController == null)
                continue;

            NavMeshAgent navAgent = agentController.GetComponent<NavMeshAgent>();

            if (navAgent == null)
                continue;

            ApplySlow(navAgent);
            agentsInsideThisTick.Add(navAgent);
        }

        RestoreAgentsOutsideZone();
    }

    private void ApplySlow(NavMeshAgent navAgent)
    {
        if (navAgent == null)
            return;

        if (!originalAgentSpeeds.ContainsKey(navAgent))
            originalAgentSpeeds.Add(navAgent, navAgent.speed);

        float originalSpeed = originalAgentSpeeds[navAgent];
        float slowedSpeed = originalSpeed * moveSpeedMultiplier;

        navAgent.speed = Mathf.Min(navAgent.speed, slowedSpeed);
    }

    private void RestoreAgentsOutsideZone()
    {
        List<NavMeshAgent> restoreTargets = new List<NavMeshAgent>();

        foreach (KeyValuePair<NavMeshAgent, float> pair in originalAgentSpeeds)
        {
            NavMeshAgent navAgent = pair.Key;

            if (navAgent == null)
            {
                restoreTargets.Add(navAgent);
                continue;
            }

            if (!agentsInsideThisTick.Contains(navAgent))
                restoreTargets.Add(navAgent);
        }

        for (int i = 0; i < restoreTargets.Count; i++)
        {
            RestoreAgent(restoreTargets[i]);
        }
    }

    private void RestoreAllAgents()
    {
        List<NavMeshAgent> restoreTargets = new List<NavMeshAgent>();

        foreach (KeyValuePair<NavMeshAgent, float> pair in originalAgentSpeeds)
        {
            restoreTargets.Add(pair.Key);
        }

        for (int i = 0; i < restoreTargets.Count; i++)
        {
            RestoreAgent(restoreTargets[i]);
        }
    }

    private void RestoreAgent(NavMeshAgent navAgent)
    {
        if (navAgent != null && originalAgentSpeeds.TryGetValue(navAgent, out float originalSpeed))
            navAgent.speed = originalSpeed;

        originalAgentSpeeds.Remove(navAgent);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}