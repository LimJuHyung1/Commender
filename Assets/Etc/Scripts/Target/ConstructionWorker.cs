using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ConstructionWorker : TargetSkillController
{
    private enum LocomotionAnimState
    {
        Idle = 0,
        Walk = 1,
        Run = 2
    }

    [Header("Barricade")]
    [SerializeField] private Barricade barricadePrefab;
    [SerializeField] private float barricadeCooldown = 8f;
    [SerializeField] private float barricadeBehindDistance = 2.5f;
    [SerializeField] private float barricadeNavMeshSampleRadius = 3f;

    [Header("Material Throw")]
    [SerializeField] private string throwableTag = "Throwable";
    [SerializeField] private float throwableSearchRadius = 5f;
    [SerializeField] private float throwTargetSearchRadius = 15f;
    [SerializeField] private Vector2 materialThrowCooldownRange = new Vector2(12f, 20f);
    [SerializeField] private float materialThrowRetryDelay = 3f;
    [SerializeField] private float throwDuration = 0.45f;
    [SerializeField] private float throwHitHeight = 1f;
    [SerializeField] private bool destroyThrowableAfterHit = false;
    [SerializeField] private bool useMaterialThrowCamera = true;

    [Header("Dust Zone")]
    [SerializeField] private GameObject dustZonePrefab;
    [SerializeField] private Vector2 dustCooldownRange = new Vector2(25f, 40f);
    [SerializeField] private float dustRetryDelay = 5f;
    [SerializeField] private float dustDuration = 10f;
    [SerializeField] private float dustRadius = 4f;
    [SerializeField][Range(0.1f, 1f)] private float dustMoveSpeedMultiplier = 0.5f;
    [SerializeField] private float dustSpawnForwardOffset = 0.5f;
    [SerializeField] private float dustNavMeshSampleRadius = 3f;
    [SerializeField] private bool allowOnlyOneActiveDustZone = true;
    [SerializeField] private bool useDustVerticalAnimation = true;
    [SerializeField] private float dustHiddenY = -1f;
    [SerializeField] private float dustVisibleY = 0f;
    [SerializeField] private float dustAppearDuration = 3f;
    [SerializeField] private float dustDisappearDuration = 3f;

    [Header("Dig Escape")]
    [SerializeField] private string targetSpawnPointsObjectName = "TargetSpawnPoints";
    [SerializeField] private Transform targetSpawnPointsParent;
    [SerializeField] private float digEscapeMinDistanceFromCurrent = 6f;
    [SerializeField] private float digEscapeNavMeshSampleRadius = 4f;
    [SerializeField] private float digEscapeAnimationWaitTime = 0.8f;
    [SerializeField] private float digEscapeVanishDelay = 0.25f;
    [SerializeField] private float digEscapeReappearDelay = 0.15f;
    [SerializeField] private bool blockDigEscapeWhenThreatActive = true;
    [SerializeField] private bool hideTargetDuringDigEscape = true;

    [Header("Skill Unlock Level")]
    [SerializeField] private int barricadeUnlockLevel = 1;
    [SerializeField] private int barricadeSecondUseLevel = 4;
    [SerializeField] private int barricadeThirdUseLevel = 7;
    [SerializeField] private int materialThrowUnlockLevel = 7;
    [SerializeField] private int dustZoneUnlockLevel = 4;
    [SerializeField] private int digEscapeUnlockLevel = 6;

    [Header("Auto Defensive Skill")]
    [SerializeField] private bool autoUseBarricadeOnlyOncePerThreatEncounter = true;
    [SerializeField] private float autoDefensiveSharedCooldown = 8f;
    [SerializeField] private float threatResetDelay = 1f;

    [Header("Animation")]
    [SerializeField] private bool updateLocomotionAnimation = true;
    [SerializeField] private float walkSpeedThreshold = 0.05f;
    [SerializeField] private float runSpeedThreshold = 2.8f;
    [SerializeField] private float moveSpeedDampTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private const float MaterialThrowAnimationLockTime = 0.6f;
    private const float DustZoneAnimationLockTime = 0.7f;
    private const float RevealedAnimationLockTime = 0.8f;

    private static readonly int TargetAnimStateHash = Animator.StringToHash("TargetAnimState");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsSkillLockedHash = Animator.StringToHash("IsSkillLocked");

    private static readonly int SkillMaterialThrowHash = Animator.StringToHash("SkillMaterialThrow");
    private static readonly int SkillDustZoneHash = Animator.StringToHash("SkillDustZone");
    private static readonly int RevealedByAgentSkillHash = Animator.StringToHash("RevealedByAgentSkill");

    private static readonly int IsExhaustedHash = Animator.StringToHash("IsExhausted");
    private static readonly int ExhaustedHash = Animator.StringToHash("Exhausted");
    private static readonly int TimeOverHash = Animator.StringToHash("TimeOver");
    private static readonly int CapturedHash = Animator.StringToHash("Captured");

    private readonly List<GameObject> activeBarricades = new List<GameObject>();
    private readonly List<DustZone> activeDustZones = new List<DustZone>();
    private readonly Dictionary<int, AnimatorControllerParameterType> animatorParameterTypes =
        new Dictionary<int, AnimatorControllerParameterType>();

    private Animator targetAnimator;
    private TargetVisibilityController visibilityController;
    private Coroutine skillAnimationLockRoutine;
    private Renderer[] targetRenderers;
    private Canvas[] targetCanvases;

    private bool enableBarricadeSkill;
    private bool enableMaterialThrowSkill;
    private bool enableDustZoneSkill;
    private bool enableDigEscapeSkill;

    private bool wasThreatActiveLastFrame;
    private bool autoDefensiveUsedInCurrentThreatEncounter;
    private bool isThrowingObject;
    private bool isDigEscaping;
    private bool isSkillAnimationLocked;
    private bool isTerminalAnimationPlaying;

    private int currentBarricadeMaxUseCount;
    private int remainingBarricadeUseCount;
    private int remainingDigEscapeUseCount;

    private float threatLastLostTime = -999f;
    private float nextBarricadeReadyTime = -999f;
    private float nextMaterialThrowReadyTime = -999f;
    private float nextDustReadyTime = -999f;
    private float nextAutoDefensiveReadyTime = -999f;

    public override bool IsBarricadeUnlocked => enableBarricadeSkill;

    public bool IsMaterialThrowUnlocked => enableMaterialThrowSkill;
    public bool IsDustZoneUnlocked => enableDustZoneSkill;
    public bool IsDigEscapeUnlocked => enableDigEscapeSkill;

    public int BarricadeMaxUseCount => currentBarricadeMaxUseCount;
    public int BarricadeRemainingUseCount => remainingBarricadeUseCount;
    public int DigEscapeRemainingUseCount => remainingDigEscapeUseCount;

    public override float BarricadeRemainingCooldown =>
        Mathf.Max(0f, nextBarricadeReadyTime - Time.time);

    public float MaterialThrowRemainingCooldown =>
        Mathf.Max(0f, nextMaterialThrowReadyTime - Time.time);

    public float DustZoneRemainingCooldown =>
        Mathf.Max(0f, nextDustReadyTime - Time.time);

    public float DigEscapeRemainingCooldown => 0f;

    protected override void Awake()
    {
        base.Awake();

        ResolveLocalReferences();
        CacheAnimatorParameters();
        ResetAnimationState();
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
        skillAnimationLockRoutine = null;

        ResetAnimationState();

        isThrowingObject = false;
        isDigEscaping = false;
    }

    private void Update()
    {
        UpdateAnimationLocomotion();

        if (IsTargetUnableToUseSkills())
            return;

        UpdateThreatEncounterState();

        if (isDigEscaping)
            return;

        TickMaterialThrow();
        TickDustZone();
    }

    public override void ApplySkillUnlocks(int targetLevel)
    {
        enableBarricadeSkill = targetLevel >= barricadeUnlockLevel;
        enableMaterialThrowSkill = targetLevel >= materialThrowUnlockLevel;
        enableDustZoneSkill = targetLevel >= dustZoneUnlockLevel;
        enableDigEscapeSkill = targetLevel >= digEscapeUnlockLevel;

        currentBarricadeMaxUseCount = enableBarricadeSkill
            ? GetBarricadeMaxUseCount(targetLevel)
            : 0;

        ResetRuntimeState(true, true);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[ConstructionWorker] Skill unlock applied - " +
                $"Level: {targetLevel}, " +
                $"Barricade: {enableBarricadeSkill}, " +
                $"BarricadeUse: {remainingBarricadeUseCount}/{currentBarricadeMaxUseCount}, " +
                $"MaterialThrow: {enableMaterialThrowSkill}, " +
                $"DustZone: {enableDustZoneSkill}, " +
                $"DigEscape: {enableDigEscapeSkill}, " +
                $"DigEscapeUse: {remainingDigEscapeUseCount}"
            );
        }
    }

    public override void ResetRuntimeState(
        bool destroyActiveBarricades = true,
        bool destroyActiveHologram = true)
    {
        base.ResetRuntimeState(destroyActiveBarricades, destroyActiveHologram);

        StopAllCoroutines();
        skillAnimationLockRoutine = null;
        ResetAnimationState();

        if (destroyActiveBarricades)
            DestroyActiveBarricades();

        DestroyActiveDustZones();

        wasThreatActiveLastFrame = false;
        autoDefensiveUsedInCurrentThreatEncounter = false;
        isThrowingObject = false;
        isDigEscaping = false;

        remainingBarricadeUseCount = enableBarricadeSkill
            ? currentBarricadeMaxUseCount
            : 0;

        remainingDigEscapeUseCount = enableDigEscapeSkill ? 1 : 0;

        threatLastLostTime = -999f;
        nextBarricadeReadyTime = -999f;
        nextAutoDefensiveReadyTime = -999f;

        ScheduleNextMaterialThrow(false);
        ScheduleNextDustZone(false);

        RefreshTargetVisibility();
    }

    public override bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        if (IsTargetUnableToUseSkills())
            return false;

        if (!CanUseAutoDefensiveSkill())
            return false;

        bool used = TryUseBarricade(escapeDestination);

        if (!used)
            return false;

        nextAutoDefensiveReadyTime = Time.time + autoDefensiveSharedCooldown;
        autoDefensiveUsedInCurrentThreatEncounter = true;

        if (enableDebugLog)
            Debug.Log("[ConstructionWorker] Auto defensive skill used - Barricade");

        return true;
    }

    public override bool TryUseBarricade(Vector3 escapeDestination)
    {
        if (!CanUseTargetSkill(TargetSkillType.Barricade))
            return false;

        if (!CanUseBarricade())
            return false;

        Vector3 desiredGroundPosition = GetBarricadeSpawnPosition(escapeDestination);
        Quaternion spawnRotation = GetBarricadeSpawnRotation(escapeDestination);

        if (!NavMesh.SamplePosition(
                desiredGroundPosition,
                out NavMeshHit hit,
                barricadeNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        Barricade barricade = Instantiate(
            barricadePrefab,
            hit.position,
            spawnRotation
        );

        barricade.Deploy(hit.position, spawnRotation);

        activeBarricades.Add(barricade.gameObject);
        remainingBarricadeUseCount--;

        nextBarricadeReadyTime = Time.time + barricadeCooldown;

        if (enableDebugLog)
        {
            Debug.Log(
                $"[ConstructionWorker] Barricade created - " +
                $"Remaining: {remainingBarricadeUseCount}/{currentBarricadeMaxUseCount}"
            );
        }

        return true;
    }

    private void TickMaterialThrow()
    {
        if (!enableMaterialThrowSkill)
            return;

        if (isThrowingObject)
            return;

        if (!IsTargetSafeForUtilitySkill())
            return;

        if (Time.time < nextMaterialThrowReadyTime)
            return;

        bool used = TryUseMaterialThrow();

        if (!used)
            nextMaterialThrowReadyTime = Time.time + materialThrowRetryDelay;
    }

    private bool TryUseMaterialThrow()
    {
        if (!TryFindNearestThrowable(out GameObject throwableObject))
            return false;

        if (!TryFindThrowTargetAgent(out AgentController targetAgent))
            return false;

        RequestMaterialThrowCamera(throwableObject.transform);

        StartCoroutine(ThrowObjectRoutine(throwableObject, targetAgent));

        ScheduleNextMaterialThrow(false);
        PlaySkillAnimation(SkillMaterialThrowHash, MaterialThrowAnimationLockTime);

        if (enableDebugLog)
            Debug.Log($"[ConstructionWorker] Material throw used - Object: {throwableObject.name}");

        return true;
    }

    private void RequestMaterialThrowCamera(Transform throwableTransform)
    {
        if (!useMaterialThrowCamera)
            return;

        if (throwableTransform == null)
            return;

        SkillCameraEventBus.Request(
            SkillCameraFocusMode.FollowObject,
            null,
            throwableTransform
        );
    }

    private IEnumerator ThrowObjectRoutine(GameObject throwableObject, AgentController targetAgent)
    {
        isThrowingObject = true;

        if (throwableObject == null || targetAgent == null)
        {
            isThrowingObject = false;
            yield break;
        }

        Transform throwableTransform = throwableObject.transform;

        Rigidbody throwableRigidbody = throwableObject.GetComponent<Rigidbody>();
        Collider[] throwableColliders = throwableObject.GetComponentsInChildren<Collider>();

        bool hadRigidbody = throwableRigidbody != null;
        bool originalKinematic = false;

        if (hadRigidbody)
        {
            originalKinematic = throwableRigidbody.isKinematic;
            throwableRigidbody.linearVelocity = Vector3.zero;
            throwableRigidbody.angularVelocity = Vector3.zero;
            throwableRigidbody.isKinematic = true;
        }

        SetCollidersEnabled(throwableColliders, false);

        Vector3 startPosition = throwableTransform.position;
        Vector3 targetPosition = targetAgent.transform.position + Vector3.up * throwHitHeight;

        float elapsed = 0f;

        while (elapsed < throwDuration)
        {
            if (throwableObject == null)
                break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, throwDuration));
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, t);

            throwableTransform.position = nextPosition;
            throwableTransform.Rotate(Vector3.right, 720f * Time.deltaTime, Space.Self);

            yield return null;
        }

        if (targetAgent != null)
            ApplyObjectHitToAgent(targetAgent);

        if (throwableObject != null)
        {
            if (destroyThrowableAfterHit)
            {
                Destroy(throwableObject);
            }
            else
            {
                SetCollidersEnabled(throwableColliders, true);

                if (hadRigidbody && throwableRigidbody != null)
                    throwableRigidbody.isKinematic = originalKinematic;
            }
        }

        isThrowingObject = false;
    }

    private void TickDustZone()
    {
        if (!enableDustZoneSkill)
            return;

        if (!IsTargetSafeForUtilitySkill())
            return;

        CleanupDustZoneList();

        if (allowOnlyOneActiveDustZone && activeDustZones.Count > 0)
            return;

        if (Time.time < nextDustReadyTime)
            return;

        bool used = TryCreateDustZone();

        if (!used)
            nextDustReadyTime = Time.time + dustRetryDelay;
    }

    private bool TryCreateDustZone()
    {
        Vector3 desiredPosition = transform.position + transform.forward * dustSpawnForwardOffset;

        if (!NavMesh.SamplePosition(
                desiredPosition,
                out NavMeshHit hit,
                dustNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        Vector3 spawnPosition = hit.position;

        if (useDustVerticalAnimation)
            spawnPosition.y = dustHiddenY;
        else
            spawnPosition.y = dustVisibleY;

        GameObject zoneObject;

        if (dustZonePrefab != null)
        {
            zoneObject = Instantiate(
                dustZonePrefab,
                spawnPosition,
                Quaternion.identity
            );
        }
        else
        {
            zoneObject = new GameObject("ConstructionDustZone");
            zoneObject.transform.position = spawnPosition;
        }

        DustZone dustZone = zoneObject.GetComponent<DustZone>();

        if (dustZone == null)
            dustZone = zoneObject.AddComponent<DustZone>();

        dustZone.Initialize(
            dustRadius,
            dustDuration,
            dustMoveSpeedMultiplier,
            enableDebugLog,
            useDustVerticalAnimation,
            dustHiddenY,
            dustVisibleY,
            dustAppearDuration,
            dustDisappearDuration
        );

        activeDustZones.Add(dustZone);

        ScheduleNextDustZone(false);
        PlaySkillAnimation(SkillDustZoneHash, DustZoneAnimationLockTime);

        if (enableDebugLog)
            Debug.Log("[ConstructionWorker] Dust zone created");

        return true;
    }

    public bool TryUseDigEscapeByAgentReveal()
    {
        if (IsTargetUnableToUseSkills())
            return false;

        return TryUseDigEscape();
    }

    public void OnTargetPositionRevealedByAgentSkill()
    {
        if (IsTargetUnableToUseSkills())
            return;

        bool usedDigEscape = TryUseDigEscapeByAgentReveal();

        if (!usedDigEscape)
            PlayRevealedByAgentSkillAnimation();
    }

    private bool TryUseDigEscape()
    {
        if (!enableDigEscapeSkill)
            return false;

        if (isDigEscaping)
            return false;

        if (remainingDigEscapeUseCount <= 0)
            return false;

        if (blockDigEscapeWhenThreatActive &&
            TargetController != null &&
            TargetController.HasActiveThreat)
        {
            return false;
        }

        if (blockDigEscapeWhenThreatActive &&
            ThreatTracker != null &&
            ThreatTracker.HasAnyThreat())
        {
            return false;
        }

        if (!TryFindDigEscapePosition(out Vector3 escapePosition))
            return false;

        remainingDigEscapeUseCount--;

        StartCoroutine(DigEscapeRoutine(escapePosition));

        if (enableDebugLog)
        {
            Debug.Log(
                $"[ConstructionWorker] Dig escape used - " +
                $"Remaining: {remainingDigEscapeUseCount}"
            );
        }

        return true;
    }

    private IEnumerator DigEscapeRoutine(Vector3 escapePosition)
    {
        isDigEscaping = true;

        StopTargetNavigation();

        float totalLockTime =
            digEscapeAnimationWaitTime +
            digEscapeVanishDelay +
            digEscapeReappearDelay;

        PlaySkillAnimation(
            RevealedByAgentSkillHash,
            Mathf.Max(RevealedAnimationLockTime, totalLockTime)
        );

        if (digEscapeAnimationWaitTime > 0f)
            yield return new WaitForSeconds(digEscapeAnimationWaitTime);

        StopTargetNavigation();

        if (hideTargetDuringDigEscape)
            SetTargetVisuals(false);

        if (digEscapeVanishDelay > 0f)
            yield return new WaitForSeconds(digEscapeVanishDelay);

        WarpTarget(escapePosition);
        StopTargetNavigation();

        if (digEscapeReappearDelay > 0f)
            yield return new WaitForSeconds(digEscapeReappearDelay);

        RestoreVisibilityAfterTemporaryHide();

        isDigEscaping = false;
    }

    private bool CanUseBarricade()
    {
        if (!enableBarricadeSkill)
            return false;

        if (barricadePrefab == null)
            return false;

        if (remainingBarricadeUseCount <= 0)
            return false;

        if (Time.time < nextBarricadeReadyTime)
            return false;

        return true;
    }

    private bool CanUseAutoDefensiveSkill()
    {
        if (Time.time < nextAutoDefensiveReadyTime)
            return false;

        if (ThreatTracker == null)
            return false;

        if (!ThreatTracker.HasAnyThreat())
            return false;

        if (autoUseBarricadeOnlyOncePerThreatEncounter &&
            autoDefensiveUsedInCurrentThreatEncounter)
        {
            return false;
        }

        return true;
    }

    private bool IsTargetSafeForUtilitySkill()
    {
        if (TargetController != null && TargetController.HasActiveThreat)
            return false;

        if (ThreatTracker != null && ThreatTracker.HasAnyThreat())
            return false;

        return true;
    }

    private int GetBarricadeMaxUseCount(int targetLevel)
    {
        if (targetLevel >= barricadeThirdUseLevel)
            return 3;

        if (targetLevel >= barricadeSecondUseLevel)
            return 2;

        if (targetLevel >= barricadeUnlockLevel)
            return 1;

        return 0;
    }

    private Vector3 GetBarricadeSpawnPosition(Vector3 escapeDestination)
    {
        Vector3 escapeDirection = escapeDestination - transform.position;
        escapeDirection.y = 0f;

        if (escapeDirection.sqrMagnitude <= 0.001f)
            escapeDirection = transform.forward;

        escapeDirection.Normalize();

        return transform.position - escapeDirection * barricadeBehindDistance;
    }

    private Quaternion GetBarricadeSpawnRotation(Vector3 escapeDestination)
    {
        Vector3 escapeDirection = escapeDestination - transform.position;
        escapeDirection.y = 0f;

        if (escapeDirection.sqrMagnitude <= 0.001f)
            escapeDirection = transform.forward;

        escapeDirection.Normalize();

        return Quaternion.LookRotation(escapeDirection, Vector3.up);
    }

    private bool TryFindNearestThrowable(out GameObject throwableObject)
    {
        throwableObject = null;

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            throwableSearchRadius
        );

        float nearestSqrDistance = float.MaxValue;
        HashSet<GameObject> checkedObjects = new HashSet<GameObject>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];

            if (currentCollider == null)
                continue;

            GameObject candidate = FindTaggedParentOrSelf(currentCollider.transform, throwableTag);

            if (candidate == null)
                continue;

            if (!candidate.activeInHierarchy)
                continue;

            if (checkedObjects.Contains(candidate))
                continue;

            checkedObjects.Add(candidate);

            float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            throwableObject = candidate;
        }

        return throwableObject != null;
    }

    private GameObject FindTaggedParentOrSelf(Transform startTransform, string targetTag)
    {
        if (startTransform == null)
            return null;

        Transform current = startTransform;

        while (current != null)
        {
            if (HasTag(current.gameObject, targetTag))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    private bool HasTag(GameObject targetObject, string targetTag)
    {
        if (targetObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(targetTag))
            return false;

        try
        {
            return targetObject.CompareTag(targetTag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private bool TryFindThrowTargetAgent(out AgentController targetAgent)
    {
        targetAgent = null;

        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float nearestSqrDistance = float.MaxValue;
        float maxSqrDistance = throwTargetSearchRadius * throwTargetSearchRadius;

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!agent.gameObject.activeInHierarchy)
                continue;

            float sqrDistance = (agent.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance > maxSqrDistance)
                continue;

            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            targetAgent = agent;
        }

        return targetAgent != null;
    }

    private void ApplyObjectHitToAgent(AgentController targetAgent)
    {
        if (targetAgent == null)
            return;

        targetAgent.PlayHitReaction(transform.position);

        if (enableDebugLog)
            Debug.Log($"[ConstructionWorker] Agent hit - Agent: {targetAgent.name}");
    }

    private bool TryFindDigEscapePosition(out Vector3 escapePosition)
    {
        escapePosition = transform.position;

        ResolveTargetSpawnPointsParent();

        if (targetSpawnPointsParent == null)
            return false;

        List<Transform> candidatePoints = new List<Transform>();

        for (int i = 0; i < targetSpawnPointsParent.childCount; i++)
        {
            Transform child = targetSpawnPointsParent.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            float distance = Vector3.Distance(transform.position, child.position);

            if (distance < digEscapeMinDistanceFromCurrent)
                continue;

            candidatePoints.Add(child);
        }

        if (candidatePoints.Count <= 0)
        {
            for (int i = 0; i < targetSpawnPointsParent.childCount; i++)
            {
                Transform child = targetSpawnPointsParent.GetChild(i);

                if (child != null && child.gameObject.activeInHierarchy)
                    candidatePoints.Add(child);
            }
        }

        if (candidatePoints.Count <= 0)
            return false;

        Transform selectedPoint = candidatePoints[Random.Range(0, candidatePoints.Count)];

        if (!NavMesh.SamplePosition(
                selectedPoint.position,
                out NavMeshHit hit,
                digEscapeNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        escapePosition = hit.position;
        return true;
    }

    private void ResolveTargetSpawnPointsParent()
    {
        if (targetSpawnPointsParent != null)
            return;

        Transform spawnPointsFromMap = FindTargetSpawnPointsFromMap();

        if (spawnPointsFromMap != null)
        {
            targetSpawnPointsParent = spawnPointsFromMap;
            return;
        }

        GameObject spawnPointsObject = GameObject.Find(targetSpawnPointsObjectName);

        if (spawnPointsObject != null)
            targetSpawnPointsParent = spawnPointsObject.transform;
    }

    private Transform FindTargetSpawnPointsFromMap()
    {
        GameObject mapObject = GameObject.Find("map");

        if (mapObject == null)
            mapObject = GameObject.Find("Map");

        if (mapObject == null)
            return null;

        return FindChildRecursive(mapObject.transform, targetSpawnPointsObjectName);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child == null)
                continue;

            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);

            if (result != null)
                return result;
        }

        return null;
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

    private void ScheduleNextMaterialThrow(bool readyImmediately)
    {
        if (!enableMaterialThrowSkill)
            return;

        if (readyImmediately)
        {
            nextMaterialThrowReadyTime = Time.time;
            return;
        }

        nextMaterialThrowReadyTime = Time.time + Random.Range(
            materialThrowCooldownRange.x,
            materialThrowCooldownRange.y
        );
    }

    private void ScheduleNextDustZone(bool readyImmediately)
    {
        if (!enableDustZoneSkill)
            return;

        if (readyImmediately)
        {
            nextDustReadyTime = Time.time;
            return;
        }

        nextDustReadyTime = Time.time + Random.Range(
            dustCooldownRange.x,
            dustCooldownRange.y
        );
    }

    private void DestroyActiveBarricades()
    {
        for (int i = activeBarricades.Count - 1; i >= 0; i--)
        {
            GameObject barricade = activeBarricades[i];

            if (barricade != null)
                Destroy(barricade);
        }

        activeBarricades.Clear();
    }

    private void DestroyActiveDustZones()
    {
        for (int i = activeDustZones.Count - 1; i >= 0; i--)
        {
            DustZone dustZone = activeDustZones[i];

            if (dustZone != null)
                Destroy(dustZone.gameObject);
        }

        activeDustZones.Clear();
    }

    private void CleanupDustZoneList()
    {
        for (int i = activeDustZones.Count - 1; i >= 0; i--)
        {
            if (activeDustZones[i] == null)
                activeDustZones.RemoveAt(i);
        }
    }

    private void SetCollidersEnabled(Collider[] colliders, bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];

            if (currentCollider == null)
                continue;

            currentCollider.enabled = enabled;
        }
    }

    public void PlayRevealedByAgentSkillAnimation()
    {
        PlaySkillAnimation(RevealedByAgentSkillHash, RevealedAnimationLockTime);
    }

    public void PlayExhaustedAnimation()
    {
        PlayTerminalAnimation(ExhaustedHash, true);
    }

    public void PlayTimeOverAnimation()
    {
        PlayTerminalAnimation(TimeOverHash, false);
    }

    public void PlayCapturedAnimation()
    {
        PlayTerminalAnimation(CapturedHash, false);
    }

    private void UpdateAnimationLocomotion()
    {
        if (!updateLocomotionAnimation)
            return;

        if (targetAnimator == null)
            return;

        if (isTerminalAnimationPlaying)
            return;

        if (isSkillAnimationLocked)
            return;

        float currentSpeed = GetCurrentTargetMoveSpeed();

        SetAnimatorFloat(MoveSpeedHash, currentSpeed, moveSpeedDampTime);
        SetAnimatorInt(TargetAnimStateHash, (int)GetLocomotionAnimState(currentSpeed));
    }

    private float GetCurrentTargetMoveSpeed()
    {
        if (NavAgent == null)
            return 0f;

        if (!NavAgent.enabled)
            return 0f;

        if (!NavAgent.isActiveAndEnabled)
            return 0f;

        if (!NavAgent.isOnNavMesh)
            return 0f;

        Vector3 velocity = NavAgent.velocity;
        velocity.y = 0f;

        return velocity.magnitude;
    }

    private LocomotionAnimState GetLocomotionAnimState(float moveSpeed)
    {
        if (moveSpeed <= walkSpeedThreshold)
            return LocomotionAnimState.Idle;

        if (moveSpeed < runSpeedThreshold)
            return LocomotionAnimState.Walk;

        return LocomotionAnimState.Run;
    }

    private void PlaySkillAnimation(int triggerHash, float lockDuration)
    {
        if (isTerminalAnimationPlaying)
            return;

        bool played = PlayAnimatorTrigger(triggerHash);

        if (!played)
            return;

        LockSkillAnimation(lockDuration);
    }

    private void PlayTerminalAnimation(int triggerHash, bool exhausted)
    {
        isTerminalAnimationPlaying = true;
        isSkillAnimationLocked = true;

        StopSkillAnimationLockRoutine();

        SetAnimatorBool(IsSkillLockedHash, true);
        SetAnimatorBool(IsExhaustedHash, exhausted);
        SetAnimatorFloat(MoveSpeedHash, 0f, 0f);
        SetAnimatorInt(TargetAnimStateHash, (int)LocomotionAnimState.Idle);

        PlayAnimatorTrigger(triggerHash);
    }

    private void ResetAnimationState()
    {
        isSkillAnimationLocked = false;
        isTerminalAnimationPlaying = false;

        StopSkillAnimationLockRoutine();

        SetAnimatorBool(IsSkillLockedHash, false);
        SetAnimatorBool(IsExhaustedHash, false);
        SetAnimatorFloat(MoveSpeedHash, 0f, 0f);
        SetAnimatorInt(TargetAnimStateHash, (int)LocomotionAnimState.Idle);

        ResetAnimatorTrigger(SkillMaterialThrowHash);
        ResetAnimatorTrigger(SkillDustZoneHash);
        ResetAnimatorTrigger(RevealedByAgentSkillHash);
        ResetAnimatorTrigger(ExhaustedHash);
        ResetAnimatorTrigger(TimeOverHash);
        ResetAnimatorTrigger(CapturedHash);
    }

    private void LockSkillAnimation(float duration)
    {
        if (duration <= 0f)
            return;

        StopSkillAnimationLockRoutine();
        skillAnimationLockRoutine = StartCoroutine(SkillAnimationLockRoutine(duration));
    }

    private IEnumerator SkillAnimationLockRoutine(float duration)
    {
        isSkillAnimationLocked = true;
        SetAnimatorBool(IsSkillLockedHash, true);

        yield return new WaitForSeconds(duration);

        if (!isTerminalAnimationPlaying)
        {
            isSkillAnimationLocked = false;
            SetAnimatorBool(IsSkillLockedHash, false);
        }

        skillAnimationLockRoutine = null;
    }

    private void StopSkillAnimationLockRoutine()
    {
        if (skillAnimationLockRoutine == null)
            return;

        StopCoroutine(skillAnimationLockRoutine);
        skillAnimationLockRoutine = null;
    }

    private bool PlayAnimatorTrigger(int triggerHash)
    {
        if (!HasAnimatorParameter(triggerHash, AnimatorControllerParameterType.Trigger))
            return false;

        targetAnimator.ResetTrigger(triggerHash);
        targetAnimator.SetTrigger(triggerHash);
        return true;
    }

    private void ResetAnimatorTrigger(int triggerHash)
    {
        if (!HasAnimatorParameter(triggerHash, AnimatorControllerParameterType.Trigger))
            return;

        targetAnimator.ResetTrigger(triggerHash);
    }

    private void SetAnimatorBool(int parameterHash, bool value)
    {
        if (!HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            return;

        targetAnimator.SetBool(parameterHash, value);
    }

    private void SetAnimatorInt(int parameterHash, int value)
    {
        if (!HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Int))
            return;

        targetAnimator.SetInteger(parameterHash, value);
    }

    private void SetAnimatorFloat(int parameterHash, float value, float dampTime)
    {
        if (!HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Float))
            return;

        if (dampTime > 0f)
        {
            targetAnimator.SetFloat(parameterHash, value, dampTime, Time.deltaTime);
            return;
        }

        targetAnimator.SetFloat(parameterHash, value);
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null)
            return false;

        if (!animatorParameterTypes.TryGetValue(parameterHash, out AnimatorControllerParameterType cachedType))
            return false;

        return cachedType == parameterType;
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterTypes.Clear();

        if (targetAnimator == null)
            return;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter == null)
                continue;

            animatorParameterTypes[parameter.nameHash] = parameter.type;
        }
    }

    private void SetTargetVisuals(bool visible)
    {
        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];

                if (targetRenderer == null)
                    continue;

                targetRenderer.enabled = visible;
            }
        }

        if (targetCanvases != null)
        {
            for (int i = 0; i < targetCanvases.Length; i++)
            {
                Canvas targetCanvas = targetCanvases[i];

                if (targetCanvas == null)
                    continue;

                targetCanvas.enabled = visible;
            }
        }
    }

    private void RefreshTargetVisibility()
    {
        if (visibilityController == null)
            return;

        visibilityController.ResetRuntimeState();
    }

    private void RestoreVisibilityAfterTemporaryHide()
    {
        if (visibilityController != null)
        {
            visibilityController.ResetRuntimeState();
            return;
        }

        SetTargetVisuals(true);
    }

    private void ResolveLocalReferences()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();

        if (visibilityController == null)
            visibilityController = GetComponent<TargetVisibilityController>();

        if (visibilityController == null)
            visibilityController = GetComponentInChildren<TargetVisibilityController>(true);

        targetRenderers = GetComponentsInChildren<Renderer>(true);
        targetCanvases = GetComponentsInChildren<Canvas>(true);

        ResolveTargetSpawnPointsParent();
    }

    private void ClampValues()
    {
        barricadeCooldown = Mathf.Max(0.1f, barricadeCooldown);
        barricadeBehindDistance = Mathf.Max(0.1f, barricadeBehindDistance);
        barricadeNavMeshSampleRadius = Mathf.Max(0.1f, barricadeNavMeshSampleRadius);

        throwableSearchRadius = Mathf.Max(0.1f, throwableSearchRadius);
        throwTargetSearchRadius = Mathf.Max(0.1f, throwTargetSearchRadius);
        materialThrowCooldownRange.x = Mathf.Max(0.1f, materialThrowCooldownRange.x);
        materialThrowCooldownRange.y = Mathf.Max(
            materialThrowCooldownRange.x,
            materialThrowCooldownRange.y
        );
        materialThrowRetryDelay = Mathf.Max(0.1f, materialThrowRetryDelay);
        throwDuration = Mathf.Max(0.05f, throwDuration);
        throwHitHeight = Mathf.Max(0f, throwHitHeight);

        dustCooldownRange.x = Mathf.Max(0.1f, dustCooldownRange.x);
        dustCooldownRange.y = Mathf.Max(dustCooldownRange.x, dustCooldownRange.y);
        dustRetryDelay = Mathf.Max(0.1f, dustRetryDelay);
        dustDuration = Mathf.Max(0.1f, dustDuration);
        dustRadius = Mathf.Max(0.1f, dustRadius);
        dustMoveSpeedMultiplier = Mathf.Clamp(dustMoveSpeedMultiplier, 0.1f, 1f);
        dustSpawnForwardOffset = Mathf.Max(0f, dustSpawnForwardOffset);
        dustNavMeshSampleRadius = Mathf.Max(0.1f, dustNavMeshSampleRadius);
        dustAppearDuration = Mathf.Max(0f, dustAppearDuration);
        dustDisappearDuration = Mathf.Max(0f, dustDisappearDuration);

        digEscapeMinDistanceFromCurrent = Mathf.Max(0f, digEscapeMinDistanceFromCurrent);
        digEscapeNavMeshSampleRadius = Mathf.Max(0.1f, digEscapeNavMeshSampleRadius);
        digEscapeAnimationWaitTime = Mathf.Max(0f, digEscapeAnimationWaitTime);
        digEscapeVanishDelay = Mathf.Max(0f, digEscapeVanishDelay);
        digEscapeReappearDelay = Mathf.Max(0f, digEscapeReappearDelay);

        barricadeUnlockLevel = Mathf.Max(1, barricadeUnlockLevel);
        barricadeSecondUseLevel = Mathf.Max(barricadeUnlockLevel, barricadeSecondUseLevel);
        barricadeThirdUseLevel = Mathf.Max(barricadeSecondUseLevel, barricadeThirdUseLevel);

        materialThrowUnlockLevel = Mathf.Max(1, materialThrowUnlockLevel);
        dustZoneUnlockLevel = Mathf.Max(1, dustZoneUnlockLevel);
        digEscapeUnlockLevel = Mathf.Max(1, digEscapeUnlockLevel);

        autoDefensiveSharedCooldown = Mathf.Max(0.1f, autoDefensiveSharedCooldown);
        threatResetDelay = Mathf.Max(0f, threatResetDelay);

        walkSpeedThreshold = Mathf.Max(0f, walkSpeedThreshold);
        runSpeedThreshold = Mathf.Max(walkSpeedThreshold + 0.01f, runSpeedThreshold);
        moveSpeedDampTime = Mathf.Max(0f, moveSpeedDampTime);
    }
}