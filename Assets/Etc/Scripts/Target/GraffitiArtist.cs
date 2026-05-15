using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public partial class GraffitiArtist : TargetSkillController
{
    [Header("Obstacle Leap")]
    [SerializeField] private float obstacleLeapCooldown = 18f;
    [SerializeField] private float obstacleLeapDuration = 1f;
    [SerializeField] private float obstacleLeapArcHeight = 1.2f;
    [SerializeField] private float obstacleLeapLandingDelay = 0.2f;
    [SerializeField] private float obstacleLeapNavMeshSampleRadius = 2.5f;
    [SerializeField] private float obstacleLeapActivationMinDistance = 3.5f;

    [FormerlySerializedAs("obstacleLeapMinThreatDistance")]
    [SerializeField] private float obstacleLeapActivationMaxDistance = 8f;

    [SerializeField] private bool blockLeapWhenEscapeSkillBlocked = true;

    [Header("Obstacle Leap Object Search")]
    [SerializeField] private string obstacleLeapLayerName = "Obstacle";
    [SerializeField] private float obstacleLeapSearchRadius = 4.5f;
    [SerializeField] private float obstacleLeapLandingDistance = 2f;
    [SerializeField] private float obstacleLeapLandingSideOffset = 0.75f;
    [SerializeField] private int obstacleLeapLandingSampleCount = 5;
    [SerializeField] private float obstacleLeapMinObjectHeight = 0.2f;
    [SerializeField] private float obstacleLeapMaxObjectHeight = 2.5f;
    [SerializeField] private float obstacleLeapMinDirectionDot = 0.1f;
    [SerializeField] private bool checkObstacleLeapLandingClearance = true;
    [SerializeField] private float obstacleLeapLandingClearanceRadius = 0.35f;
    [SerializeField] private float obstacleLeapLandingClearanceHeight = 0.5f;
    [SerializeField] private float obstacleLeapProbeRetryDelay = 2f;

    [Header("Obstacle Leap Presentation")]
    [SerializeField] private bool forceShowDuringObstacleLeap = true;
    [SerializeField] private bool useObstacleLeapSkillCamera = true;
    [SerializeField] private SkillCameraFocusMode obstacleLeapCameraMode = SkillCameraFocusMode.FollowUser;
    [SerializeField] private TargetVisibilityController targetVisibilityController;

    [Header("Graffiti")]
    [FormerlySerializedAs("sprayPaintZonePrefab")]
    [SerializeField] private GameObject graffitiZonePrefab;

    [FormerlySerializedAs("sprayPaintCooldown")]
    [SerializeField] private float graffitiCooldown = 35f;

    [FormerlySerializedAs("sprayPaintRetryDelay")]
    [SerializeField] private float graffitiRetryDelay = 5f;

    [SerializeField] private float graffitiCancelledRetryDelay = 8f;

    [FormerlySerializedAs("sprayPaintDuration")]
    [SerializeField] private float graffitiDuration = 10f;

    [FormerlySerializedAs("sprayPaintRadius")]
    [SerializeField] private float graffitiRadius = 3.5f;

    [FormerlySerializedAs("sprayPaintNavMeshSampleRadius")]
    [SerializeField] private float graffitiNavMeshSampleRadius = 3f;

    [SerializeField] private LayerMask agentLayerMask;

    [FormerlySerializedAs("allowOnlyOneActiveSprayPaintZone")]
    [SerializeField] private bool allowOnlyOneActiveGraffitiZone = true;

    [Header("Graffiti Animation Event")]
    [SerializeField] private float graffitiAnimationEventTimeout = 10f;
    [SerializeField] private bool cancelGraffitiAnimationOnThreat = true;
    [SerializeField] private string graffitiCancelStateName = "Run";
    [SerializeField] private float graffitiCancelCrossFadeDuration = 0.05f;

    [Header("Graffiti Spawn Position")]
    [SerializeField] private bool useRandomGraffitiSpawnPosition = true;
    [SerializeField] private float graffitiRandomSpawnMinDistance = 2f;
    [SerializeField] private float graffitiRandomSpawnMaxDistance = 7f;
    [SerializeField] private int graffitiRandomSpawnSampleCount = 12;

    [Header("Graffiti Difficulty Scaling")]
    [SerializeField] private bool scaleGraffitiByStageDifficulty = true;
    [SerializeField, Range(1, 10)] private int maxGraffitiDifficultyLevel = 10;
    [SerializeField] private float maxGraffitiRadius = 6f;
    [SerializeField] private float maxGraffitiSpawnMinDistance = 3.5f;
    [SerializeField] private float maxGraffitiSpawnMaxDistance = 11f;

    [SerializeField]
    private AnimationCurve graffitiDifficultyCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Safe Graffiti Timing")]
    [SerializeField] private bool autoCreateGraffitiWhenSafe = true;
    [SerializeField] private float safeGraffitiInitialDelay = 6f;
    [SerializeField] private float safeGraffitiMinNoThreatTime = 8f;

    [Header("Color Rush Passive")]
    [SerializeField] private GameObject colorRushTrailPrefab;
    [SerializeField] private ColorRushTrailPool colorRushTrailPool;
    [SerializeField] private Transform colorRushTrailParent;
    [SerializeField] private bool autoCreateColorRushTrailPool = true;
    [SerializeField] private int colorRushTrailPoolSize = 40;
    [SerializeField] private float colorRushPassiveSpeedMultiplier = 1.25f;
    [SerializeField] private float colorRushTrailSpawnDistance = 1.2f;
    [SerializeField] private float colorRushTrailLifetime = 10f;
    [SerializeField] private float colorRushTrailNavMeshSampleRadius = 1.5f;
    [SerializeField] private float colorRushTrailYOffset = 0.03f;
    [SerializeField] private float colorRushTrailMinMoveSpeed = 0.05f;

    [Header("Spray Smoke")]
    [FormerlySerializedAs("paintSmokePrefab")]
    [SerializeField] private GameObject spraySmokePrefab;

    [FormerlySerializedAs("paintSmokeSpawnPoint")]
    [SerializeField] private Transform spraySmokeSpawnPoint;

    [FormerlySerializedAs("paintSmokeSpawnOffset")]
    [SerializeField] private Vector3 spraySmokeSpawnOffset = Vector3.zero;

    [FormerlySerializedAs("paintSmokeCooldown")]
    [SerializeField] private float spraySmokeCooldown = 14f;

    [SerializeField] private float spraySmokeActivationDistance = 5.5f;
    [SerializeField] private float spraySmokeUrgentDistance = 3.5f;

    [Header("Spray Smoke Uses")]
    [SerializeField] private int spraySmokeMaxUseCount = 5;

    [Header("Skill Unlock Level")]
    [SerializeField] private int spraySmokeUnlockLevel = 1;

    [FormerlySerializedAs("sprayPaintUnlockLevel")]
    [SerializeField] private int graffitiUnlockLevel = 3;

    [SerializeField] private int obstacleLeapUnlockLevel = 5;

    [FormerlySerializedAs("wallRunUnlockLevel")]
    [SerializeField] private int colorRushUnlockLevel = 7;

    [Header("Auto Defensive Skill")]
    [SerializeField] private bool autoUseOnlyOncePerThreatEncounter = false;
    [SerializeField] private float autoDefensiveSharedCooldown = 6f;
    [SerializeField] private float threatResetDelay = 1f;
    [SerializeField] private bool checkDefensiveSkillInUpdate = true;
    [SerializeField] private float defensiveSkillCheckInterval = 0.2f;
    [SerializeField] private float defensiveEscapeDestinationDistance = 8f;

    [Header("Animation Trigger Names")]
    [SerializeField] private string obstacleLeapTriggerName = "SkillObstacleLeap";

    [FormerlySerializedAs("sprayPaintTriggerName")]
    [SerializeField] private string graffitiTriggerName = "SkillGraffiti";

    [Header("Local References")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private TargetWanderMotor wanderMotor;
    [SerializeField] private bool forceDisableAnimatorRootMotion = true;
    [SerializeField] private bool autoSetupAnimationEventRelay = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<GraffitiPaintZone> activeGraffitiZones = new List<GraffitiPaintZone>();

    private Coroutine graffitiCastRoutine;

    private LayerMask obstacleLeapObjectLayerMask;

    private bool enableObstacleLeapSkill;
    private bool enableGraffitiSkill;
    private bool enableColorRushSkill;
    private bool enableSpraySmokeSkill;

    private bool wasThreatActiveLastFrame;
    private bool autoDefensiveUsedInCurrentThreatEncounter;
    private bool isLeaping;

    private bool isGraffitiCasting;
    private bool graffitiMovementLockActive;
    private bool hasSavedGraffitiAgentStopState;
    private bool savedGraffitiAgentStopState;

    private bool obstacleLeapMovementModeActive;
    private bool hasSavedObstacleLeapAgentState;
    private bool savedObstacleLeapAgentStopped;
    private bool savedObstacleLeapUpdatePosition;
    private bool savedObstacleLeapUpdateRotation;
    private bool obstacleLeapForceVisibleActive;

    private bool isColorRushPassiveActive;
    private bool isColorRushSpeedMultiplierApplied;
    private bool hasLastTrailSpawnPosition;
    private bool colorRushTrailPoolInitialized;

    private Vector3 lastTrailSpawnPosition;

    private int currentGraffitiDifficultyLevel = 1;
    private float currentGraffitiRadius;
    private float currentGraffitiSpawnMinDistance;
    private float currentGraffitiSpawnMaxDistance;

    private int remainingSpraySmokeUseCount;

    private float runtimeStartTime = -999f;
    private float threatLastLostTime = -999f;
    private float nextObstacleLeapReadyTime = -999f;
    private float nextObstacleLeapProbeReadyTime = -999f;
    private float nextGraffitiReadyTime = -999f;
    private float nextSpraySmokeReadyTime = -999f;
    private float nextAutoDefensiveReadyTime = -999f;
    private float nextDefensiveSkillCheckTime = -999f;

    public bool IsObstacleLeapUnlocked => enableObstacleLeapSkill;
    public bool IsGraffitiUnlocked => enableGraffitiSkill;
    public bool IsColorRushUnlocked => enableColorRushSkill;
    public bool IsColorRushPassiveActive => isColorRushPassiveActive;
    public int RemainingSpraySmokeUseCount => remainingSpraySmokeUseCount;

    public override bool IsSmokeUnlocked => enableSpraySmokeSkill;

    public float ObstacleLeapRemainingCooldown =>
        Mathf.Max(0f, nextObstacleLeapReadyTime - Time.time);

    public float GraffitiRemainingCooldown =>
        Mathf.Max(0f, nextGraffitiReadyTime - Time.time);

    public float ColorRushRemainingCooldown => 0f;

    public override float SmokeRemainingCooldown =>
        Mathf.Max(0f, nextSpraySmokeReadyTime - Time.time);

    protected override void Awake()
    {
        base.Awake();

        ResolveLocalReferences();
        ResolveObstacleLeapLayerMask();
        ClampValues();
        ApplyGraffitiDifficultyScaling(1);

        remainingSpraySmokeUseCount = spraySmokeMaxUseCount;
        runtimeStartTime = Time.time;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        ResolveObstacleLeapLayerMask();
        ClampValues();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        StopAllCoroutines();
        ForceEndGraffitiCastState(false);
        EndObstacleLeapMovementMode(transform.position, false);
        EndObstacleLeapPresentation();
        DestroyActiveGraffitiZones();
        DisableColorRushPassive();

        isLeaping = false;
    }

    private void Update()
    {
        if (IsTargetUnableToUseSkills())
            return;

        UpdateThreatEncounterState();
        TryUseAutoDefensiveSkillByInterval();
        CleanupGraffitiZoneList();
        TryUseSafeStateGraffiti();
        UpdateColorRushPassive();
    }

    public override void ApplySkillUnlocks(int targetLevel)
    {
        enableSpraySmokeSkill = targetLevel >= spraySmokeUnlockLevel;
        enableGraffitiSkill = targetLevel >= graffitiUnlockLevel;
        enableObstacleLeapSkill = targetLevel >= obstacleLeapUnlockLevel;
        enableColorRushSkill = targetLevel >= colorRushUnlockLevel;

        ApplyGraffitiDifficultyScaling(targetLevel);

        ResetRuntimeState(true, true);
        UpdateColorRushPassiveState();

        if (enableDebugLog)
        {
            Debug.Log(
                $"[GraffitiArtist] Skill unlock applied. " +
                $"Level: {targetLevel}, " +
                $"SpraySmoke: {enableSpraySmokeSkill}, " +
                $"Graffiti: {enableGraffitiSkill}, " +
                $"ObstacleLeap: {enableObstacleLeapSkill}, " +
                $"ColorRushPassive: {enableColorRushSkill}, " +
                $"GraffitiRadius: {currentGraffitiRadius}, " +
                $"GraffitiSpawnRange: {currentGraffitiSpawnMinDistance}~{currentGraffitiSpawnMaxDistance}, " +
                $"SmokeUses: {remainingSpraySmokeUseCount}"
            );
        }
    }

    public override void ResetRuntimeState(
        bool destroyActiveBarricades = true,
        bool destroyActiveHologram = true)
    {
        base.ResetRuntimeState(destroyActiveBarricades, destroyActiveHologram);

        StopAllCoroutines();
        ForceEndGraffitiCastState(false);
        EndObstacleLeapMovementMode(transform.position, false);
        EndObstacleLeapPresentation();
        DestroyActiveGraffitiZones();
        DisableColorRushPassive();

        wasThreatActiveLastFrame = false;
        autoDefensiveUsedInCurrentThreatEncounter = false;
        isLeaping = false;

        remainingSpraySmokeUseCount = spraySmokeMaxUseCount;

        runtimeStartTime = Time.time;
        threatLastLostTime = -999f;
        nextObstacleLeapReadyTime = -999f;
        nextObstacleLeapProbeReadyTime = -999f;
        nextGraffitiReadyTime = -999f;
        nextSpraySmokeReadyTime = -999f;
        nextAutoDefensiveReadyTime = -999f;
        nextDefensiveSkillCheckTime = -999f;

        UpdateColorRushPassiveState();
    }

    private void TryUseAutoDefensiveSkillByInterval()
    {
        if (!checkDefensiveSkillInUpdate)
            return;

        if (Time.time < nextDefensiveSkillCheckTime)
            return;

        nextDefensiveSkillCheckTime = Time.time + defensiveSkillCheckInterval;

        if (!HasActiveThreat())
            return;

        Vector3 escapeDestination = CalculateDefensiveEscapeDestination();

        TryUseAutoDefensiveSkill(escapeDestination);
    }

    private Vector3 CalculateDefensiveEscapeDestination()
    {
        Vector3 fleeDirection = Vector3.zero;

        if (ThreatTracker != null)
            fleeDirection = ThreatTracker.CalculateCombinedFleeDirection();

        fleeDirection.y = 0f;

        if (fleeDirection.sqrMagnitude <= 0.001f)
            fleeDirection = transform.forward;

        fleeDirection.Normalize();

        return transform.position + fleeDirection * defensiveEscapeDestinationDistance;
    }

    public override bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        if (IsTargetUnableToUseSkills())
            return false;

        if (isLeaping)
            return false;

        if (isGraffitiCasting && HasActiveThreat())
            CancelGraffitiCastByThreat(true);

        if (!CanUseAutoDefensiveSkill())
            return false;

        float nearestThreatDistance = GetNearestThreatDistance();
        bool isUrgentThreat = nearestThreatDistance <= spraySmokeUrgentDistance;

        if (isUrgentThreat && ShouldUseSpraySmoke(nearestThreatDistance))
        {
            if (TryUseSmoke())
            {
                CompleteAutoDefensiveSkillUse();
                return true;
            }
        }

        if (ShouldUseObstacleLeap())
        {
            if (TryUseObstacleLeap(escapeDestination))
            {
                CompleteAutoDefensiveSkillUse();
                return true;
            }

            nextObstacleLeapProbeReadyTime = Time.time + obstacleLeapProbeRetryDelay;

            if (enableDebugLog)
            {
                Debug.Log(
                    "[GraffitiArtist] Obstacle leap failed. Spray smoke fallback will be checked."
                );
            }
        }

        if (ShouldUseSpraySmoke(nearestThreatDistance))
        {
            if (TryUseSmoke())
            {
                CompleteAutoDefensiveSkillUse();
                return true;
            }
        }

        return false;
    }

    private void CompleteAutoDefensiveSkillUse()
    {
        nextAutoDefensiveReadyTime = Time.time + autoDefensiveSharedCooldown;
        autoDefensiveUsedInCurrentThreatEncounter = true;

        if (enableDebugLog)
            Debug.Log("[GraffitiArtist] Reactive defensive skill used.");
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

    private void UpdateThreatEncounterState()
    {
        bool hasThreat = HasActiveThreat();

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

    private bool HasActiveThreat()
    {
        return ThreatTracker != null && ThreatTracker.HasAnyThreat();
    }

    private bool CanUseNavAgent()
    {
        if (NavAgent == null)
            return false;

        if (!NavAgent.enabled)
            return false;

        if (!NavAgent.isActiveAndEnabled)
            return false;

        if (!NavAgent.isOnNavMesh)
            return false;

        return true;
    }

    private void DestroyActiveGraffitiZones()
    {
        for (int i = activeGraffitiZones.Count - 1; i >= 0; i--)
        {
            GraffitiPaintZone graffitiZone = activeGraffitiZones[i];

            if (graffitiZone != null)
                Destroy(graffitiZone.gameObject);
        }

        activeGraffitiZones.Clear();
    }

    private void CleanupGraffitiZoneList()
    {
        for (int i = activeGraffitiZones.Count - 1; i >= 0; i--)
        {
            if (activeGraffitiZones[i] == null)
                activeGraffitiZones.RemoveAt(i);
        }
    }

    private void ResolveLocalReferences()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>(true);

        if (wanderMotor == null)
            wanderMotor = GetComponent<TargetWanderMotor>();

        if (targetVisibilityController == null)
            targetVisibilityController = GetComponent<TargetVisibilityController>();

        if (targetAnimator != null && forceDisableAnimatorRootMotion)
            targetAnimator.applyRootMotion = false;

        if (!autoSetupAnimationEventRelay)
            return;

        if (targetAnimator == null)
            return;

        GraffitiArtistAnimationEventRelay relay =
            targetAnimator.GetComponent<GraffitiArtistAnimationEventRelay>();

        if (relay == null)
            relay = targetAnimator.gameObject.AddComponent<GraffitiArtistAnimationEventRelay>();

        relay.SetOwner(this);
    }

    private void ResolveObstacleLeapLayerMask()
    {
        obstacleLeapObjectLayerMask = 0;

        if (string.IsNullOrWhiteSpace(obstacleLeapLayerName))
            return;

        int obstacleLayer = LayerMask.NameToLayer(obstacleLeapLayerName);

        if (obstacleLayer < 0)
            return;

        obstacleLeapObjectLayerMask = 1 << obstacleLayer;
    }

    private void PlayTargetTrigger(string triggerName)
    {
        if (targetAnimator == null)
            return;

        if (string.IsNullOrEmpty(triggerName))
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

    private void ResetTargetTrigger(string triggerName)
    {
        if (targetAnimator == null)
            return;

        if (string.IsNullOrEmpty(triggerName))
            return;

        if (!HasAnimatorParameter(
                targetAnimator,
                triggerName,
                AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        targetAnimator.ResetTrigger(triggerName);
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

    private void CancelGraffitiAnimationIfNeeded()
    {
        if (!cancelGraffitiAnimationOnThreat)
            return;

        if (targetAnimator == null)
            return;

        if (string.IsNullOrEmpty(graffitiCancelStateName))
            return;

        targetAnimator.CrossFade(
            graffitiCancelStateName,
            graffitiCancelCrossFadeDuration,
            0
        );
    }

    private void ClampValues()
    {
        obstacleLeapCooldown = Mathf.Max(0.1f, obstacleLeapCooldown);
        obstacleLeapDuration = Mathf.Max(0.05f, obstacleLeapDuration);
        obstacleLeapArcHeight = Mathf.Max(0f, obstacleLeapArcHeight);
        obstacleLeapLandingDelay = Mathf.Max(0f, obstacleLeapLandingDelay);
        obstacleLeapNavMeshSampleRadius = Mathf.Max(0.1f, obstacleLeapNavMeshSampleRadius);
        obstacleLeapActivationMinDistance = Mathf.Max(0f, obstacleLeapActivationMinDistance);
        obstacleLeapActivationMaxDistance = Mathf.Max(
            obstacleLeapActivationMinDistance,
            obstacleLeapActivationMaxDistance
        );

        obstacleLeapSearchRadius = Mathf.Max(0.1f, obstacleLeapSearchRadius);
        obstacleLeapLandingDistance = Mathf.Max(0.1f, obstacleLeapLandingDistance);
        obstacleLeapLandingSideOffset = Mathf.Max(0f, obstacleLeapLandingSideOffset);
        obstacleLeapLandingSampleCount = Mathf.Max(1, obstacleLeapLandingSampleCount);
        obstacleLeapMinObjectHeight = Mathf.Max(0f, obstacleLeapMinObjectHeight);
        obstacleLeapMaxObjectHeight = Mathf.Max(
            obstacleLeapMinObjectHeight,
            obstacleLeapMaxObjectHeight
        );
        obstacleLeapMinDirectionDot = Mathf.Clamp(obstacleLeapMinDirectionDot, -1f, 1f);
        obstacleLeapLandingClearanceRadius = Mathf.Max(0.05f, obstacleLeapLandingClearanceRadius);
        obstacleLeapLandingClearanceHeight = Mathf.Max(0f, obstacleLeapLandingClearanceHeight);
        obstacleLeapProbeRetryDelay = Mathf.Max(0.1f, obstacleLeapProbeRetryDelay);

        graffitiCooldown = Mathf.Clamp(graffitiCooldown, 30f, 40f);
        graffitiRetryDelay = Mathf.Max(0.1f, graffitiRetryDelay);
        graffitiCancelledRetryDelay = Mathf.Max(0.1f, graffitiCancelledRetryDelay);
        graffitiDuration = Mathf.Max(0.1f, graffitiDuration);
        graffitiRadius = Mathf.Max(0.1f, graffitiRadius);
        graffitiNavMeshSampleRadius = Mathf.Max(0.1f, graffitiNavMeshSampleRadius);
        graffitiAnimationEventTimeout = Mathf.Max(0.5f, graffitiAnimationEventTimeout);
        graffitiCancelCrossFadeDuration = Mathf.Max(0f, graffitiCancelCrossFadeDuration);

        graffitiRandomSpawnMinDistance = Mathf.Max(0f, graffitiRandomSpawnMinDistance);
        graffitiRandomSpawnMaxDistance = Mathf.Max(0.1f, graffitiRandomSpawnMaxDistance);
        graffitiRandomSpawnSampleCount = Mathf.Max(1, graffitiRandomSpawnSampleCount);

        maxGraffitiDifficultyLevel = Mathf.Clamp(maxGraffitiDifficultyLevel, 1, 10);
        maxGraffitiRadius = Mathf.Max(graffitiRadius, maxGraffitiRadius);
        maxGraffitiSpawnMinDistance = Mathf.Max(0f, maxGraffitiSpawnMinDistance);
        maxGraffitiSpawnMaxDistance = Mathf.Max(
            maxGraffitiSpawnMinDistance,
            maxGraffitiSpawnMaxDistance
        );

        safeGraffitiInitialDelay = Mathf.Max(0f, safeGraffitiInitialDelay);
        safeGraffitiMinNoThreatTime = Mathf.Max(0f, safeGraffitiMinNoThreatTime);

        colorRushTrailPoolSize = Mathf.Max(1, colorRushTrailPoolSize);
        colorRushPassiveSpeedMultiplier = Mathf.Max(1f, colorRushPassiveSpeedMultiplier);
        colorRushTrailSpawnDistance = Mathf.Max(0.1f, colorRushTrailSpawnDistance);
        colorRushTrailLifetime = Mathf.Max(0.1f, colorRushTrailLifetime);
        colorRushTrailNavMeshSampleRadius = Mathf.Max(0.1f, colorRushTrailNavMeshSampleRadius);
        colorRushTrailYOffset = Mathf.Max(0f, colorRushTrailYOffset);
        colorRushTrailMinMoveSpeed = Mathf.Max(0f, colorRushTrailMinMoveSpeed);

        spraySmokeCooldown = Mathf.Max(0.1f, spraySmokeCooldown);
        spraySmokeActivationDistance = Mathf.Max(0.1f, spraySmokeActivationDistance);
        spraySmokeUrgentDistance = Mathf.Clamp(
            spraySmokeUrgentDistance,
            0.1f,
            spraySmokeActivationDistance
        );
        spraySmokeMaxUseCount = Mathf.Max(0, spraySmokeMaxUseCount);

        spraySmokeUnlockLevel = Mathf.Max(1, spraySmokeUnlockLevel);
        graffitiUnlockLevel = Mathf.Max(1, graffitiUnlockLevel);
        obstacleLeapUnlockLevel = Mathf.Max(1, obstacleLeapUnlockLevel);
        colorRushUnlockLevel = Mathf.Max(1, colorRushUnlockLevel);

        autoDefensiveSharedCooldown = Mathf.Max(0.1f, autoDefensiveSharedCooldown);
        threatResetDelay = Mathf.Max(0f, threatResetDelay);
        defensiveSkillCheckInterval = Mathf.Max(0.05f, defensiveSkillCheckInterval);
        defensiveEscapeDestinationDistance = Mathf.Max(1f, defensiveEscapeDestinationDistance);
    }
}