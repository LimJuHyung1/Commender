using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum TargetSkillType
{
    None,
    Barricade,
    Hologram,
    Smoke,
    CommunicationJam,
    EmergencyEscape
}

public enum TargetDifficultySkillSet
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public class TargetSkillController : MonoBehaviour
{
    [Header("Common References")]
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private TargetEscapeMotor escapeMotor;
    [SerializeField] private CommanderUIController commanderUIController;

    [Header("Barricade")]
    [SerializeField] private GameObject barricadePrefab;
    [SerializeField] private float barricadeCooldown = 7f;
    [SerializeField] private float minDestinationDistance = 4f;
    [SerializeField] private float maxThreatDistance = 10f;
    [SerializeField] private float minThreatDistance = 1.5f;
    [SerializeField][Range(-1f, 1f)] private float behindDotThreshold = -0.35f;
    [SerializeField] private float minDistanceBetweenPlacements = 3.5f;
    [SerializeField] private float spawnBehindDistance = 2.3f;
    [SerializeField] private float navMeshSampleRadius = 1.5f;
    [SerializeField][Min(1)] private int maxActiveBarricades = 2;

    [Header("Hologram")]
    [SerializeField] private TargetHologram hologramPrefab;
    [SerializeField] private Transform hologramSpawnPoint;
    [SerializeField] private Vector3 hologramSpawnOffset = Vector3.zero;
    [SerializeField] private bool useForwardOffset = false;
    [SerializeField] private float hologramForwardDistance = 2f;
    [SerializeField] private bool copyCurrentRotation = true;
    [SerializeField] private bool allowOnlyOneActiveHologram = true;
    [SerializeField] private bool destroyPreviousHologram = true;
    [SerializeField] private float hologramCooldown = 8f;

    [Header("Smoke")]
    [SerializeField] private TargetSmoke smokePrefab;
    [SerializeField] private Transform smokeSpawnPoint;
    [SerializeField] private Vector3 smokeSpawnOffset = Vector3.zero;
    [SerializeField] private bool useSmokeForwardOffset = false;
    [SerializeField] private float smokeForwardDistance = 1.2f;
    [SerializeField] private bool copySmokeRotation = false;
    [SerializeField] private float smokeCooldown = 10f;

    [Header("Communication Jam")]
    [SerializeField] private float communicationJamCooldown = 12f;
    [SerializeField] private float communicationJamDuration = 15f;

    [Header("Emergency Escape")]
    [SerializeField][Min(0)] private int normalEmergencyEscapeCharges = 1;
    [SerializeField][Min(0)] private int hardEmergencyEscapeCharges = 1;
    [SerializeField] private bool autoUseEmergencyEscapeOnNormal = true;
    [SerializeField] private bool autoUseEmergencyEscapeOnHard = true;

    [Header("Skill Toggle")]
    [SerializeField] private bool enableBarricadeSkill = true;
    [SerializeField] private bool enableHologramSkill = true;
    [SerializeField] private bool enableSmokeSkill = true;
    [SerializeField] private bool enableCommunicationJamSkill = true;
    [SerializeField] private bool enableEmergencyEscapeSkill = false;

    [Header("Debug")]
    [SerializeField] private bool writeLog = true;

    private readonly Queue<GameObject> activeBarricades = new Queue<GameObject>();

    private float nextBarricadeReadyTime = -999f;
    private Vector3 lastPlacementPosition;
    private bool hasLastPlacementPosition;

    private TargetHologram currentHologram;
    private float nextHologramReadyTime = -999f;
    private float nextSmokeReadyTime = -999f;
    private float nextCommunicationJamReadyTime = -999f;

    public TargetHologram CurrentHologram => currentHologram;
    public float BarricadeRemainingCooldown => Mathf.Max(0f, nextBarricadeReadyTime - Time.time);
    public float HologramRemainingCooldown => Mathf.Max(0f, nextHologramReadyTime - Time.time);
    public float SmokeRemainingCooldown => Mathf.Max(0f, nextSmokeReadyTime - Time.time);
    public float CommunicationJamRemainingCooldown => Mathf.Max(0f, nextCommunicationJamReadyTime - Time.time);
    public int RemainingEmergencyEscapeCount => enableEmergencyEscapeSkill && escapeMotor != null
        ? escapeMotor.RemainingEmergencyEscapeCount
        : 0;

    private void Awake()
    {
        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (hologramSpawnPoint == null)
            hologramSpawnPoint = transform;

        if (smokeSpawnPoint == null)
            smokeSpawnPoint = transform;

        if (commanderUIController == null)
            commanderUIController = FindFirstObjectByType<CommanderUIController>();

        ClampValues();
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void LateUpdate()
    {
        if (currentHologram == null)
            return;

        if (currentHologram.gameObject == null)
            currentHologram = null;
    }

    private void OnDisable()
    {
        DestroyAllActiveBarricades();
        ForceClearCurrentHologram();
    }

    public void ResetRuntimeState(bool destroyActiveBarricades = true, bool destroyActiveHologram = true)
    {
        nextBarricadeReadyTime = -999f;
        hasLastPlacementPosition = false;
        nextHologramReadyTime = -999f;
        nextSmokeReadyTime = -999f;
        nextCommunicationJamReadyTime = -999f;

        if (destroyActiveBarricades)
            DestroyAllActiveBarricades();

        if (destroyActiveHologram)
            ForceClearCurrentHologram();
    }

    public void ApplySkillSetByDifficultyIndex(int difficultyIndex)
    {
        if (difficultyIndex <= 0)
        {
            ApplySkillSet(TargetDifficultySkillSet.Easy);
            return;
        }

        if (difficultyIndex == 1)
        {
            ApplySkillSet(TargetDifficultySkillSet.Normal);
            return;
        }

        ApplySkillSet(TargetDifficultySkillSet.Hard);
    }

    public void ApplySkillSet(TargetDifficultySkillSet skillSet)
    {
        bool useBarricade = true;
        bool useHologram = true;
        bool useSmoke = false;
        bool useCommunicationJam = false;
        bool useEmergencyEscape = false;
        int emergencyEscapeCharges = 0;
        bool autoUseEmergencyEscape = false;

        switch (skillSet)
        {
            case TargetDifficultySkillSet.Easy:
                useBarricade = true;
                useHologram = true;
                break;

            case TargetDifficultySkillSet.Normal:
                useBarricade = true;
                useHologram = true;
                useSmoke = true;
                useEmergencyEscape = true;
                emergencyEscapeCharges = normalEmergencyEscapeCharges;
                autoUseEmergencyEscape = autoUseEmergencyEscapeOnNormal;
                break;

            case TargetDifficultySkillSet.Hard:
                useBarricade = true;
                useHologram = true;
                useSmoke = true;
                useEmergencyEscape = true;
                useCommunicationJam = true;
                emergencyEscapeCharges = hardEmergencyEscapeCharges;
                autoUseEmergencyEscape = autoUseEmergencyEscapeOnHard;
                break;
        }

        enableBarricadeSkill = useBarricade;
        enableHologramSkill = useHologram;
        enableSmokeSkill = useSmoke;
        enableCommunicationJamSkill = useCommunicationJam;
        enableEmergencyEscapeSkill = useEmergencyEscape;

        if (escapeMotor != null)
            escapeMotor.ConfigureEmergencyEscape(useEmergencyEscape, emergencyEscapeCharges, autoUseEmergencyEscape);

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] ���̵� ��ų ����: {skillSet} " +
                $"(Barricade={enableBarricadeSkill}, Hologram={enableHologramSkill}, Smoke={enableSmokeSkill}, " +
                $"EmergencyEscape={enableEmergencyEscapeSkill}, CommunicationJam={enableCommunicationJamSkill})"
            );
        }
    }

    public bool TryUseBarricade(Vector3 escapeDestination)
    {
        CleanupDestroyedBarricades();

        if (!CanUseBarricade())
            return false;

        Vector3 toDestination = escapeDestination - transform.position;
        toDestination.y = 0f;

        float destinationDistance = toDestination.magnitude;
        if (destinationDistance < minDestinationDistance)
            return false;

        Vector3 escapeForward = toDestination / destinationDistance;

        if (!threatTracker.TryGetNearestRealAgentBehind(
                escapeForward,
                maxThreatDistance,
                behindDotThreshold,
                out Transform nearestAgent,
                out float nearestDistance))
            return false;

        if (nearestAgent == null)
            return false;

        if (nearestDistance < minThreatDistance)
            return false;

        Vector3 desiredSpawnPosition = transform.position - escapeForward * spawnBehindDistance;

        if (!TryResolveBarricadeSpawnPosition(desiredSpawnPosition, out Vector3 spawnPosition))
            return false;

        if (hasLastPlacementPosition &&
            Vector3.Distance(spawnPosition, lastPlacementPosition) < minDistanceBetweenPlacements)
            return false;

        SpawnBarricade(spawnPosition, escapeForward);

        lastPlacementPosition = spawnPosition;
        hasLastPlacementPosition = true;
        nextBarricadeReadyTime = Time.time + barricadeCooldown;

        return true;
    }

    public bool TryUseHologram()
    {
        if (!CanUseHologram())
            return false;

        if (allowOnlyOneActiveHologram && currentHologram != null)
        {
            if (!destroyPreviousHologram)
                return false;

            Destroy(currentHologram.gameObject);
            currentHologram = null;
        }

        Vector3 spawnPosition = GetHologramSpawnPosition();
        Quaternion spawnRotation = copyCurrentRotation && hologramSpawnPoint != null
            ? hologramSpawnPoint.rotation
            : Quaternion.identity;

        TargetHologram spawned = Instantiate(hologramPrefab, spawnPosition, spawnRotation);
        spawned.Initialize(transform);

        currentHologram = spawned;
        nextHologramReadyTime = Time.time + hologramCooldown;

        if (writeLog)
            Debug.Log($"[TargetSkillController] Ȧ�α׷� ����: {spawnPosition}");

        return true;
    }

    public bool TryUseSmoke()
    {
        if (!CanUseSmoke())
            return false;

        Vector3 spawnPosition = GetSmokeSpawnPosition();
        Quaternion spawnRotation = copySmokeRotation && smokeSpawnPoint != null
            ? smokeSpawnPoint.rotation
            : Quaternion.identity;

        Instantiate(smokePrefab, spawnPosition, spawnRotation);
        nextSmokeReadyTime = Time.time + smokeCooldown;

        if (writeLog)
            Debug.Log($"[TargetSkillController] ����ź ����: {spawnPosition}");

        return true;
    }

    public bool TryUseCommunicationJam()
    {
        if (!CanUseCommunicationJam())
            return false;

        int randomAgentId = Random.Range(0, 4);
        bool jamApplied = commanderUIController.TryJamAgentInput(randomAgentId, communicationJamDuration);

        if (!jamApplied)
            return false;

        nextCommunicationJamReadyTime = Time.time + communicationJamCooldown;

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] ��� ���� �ߵ�: AgentID {randomAgentId}, ���ӽð� {communicationJamDuration}��");
        }

        return true;
    }

    public bool TryUseEmergencyEscape()
    {
        if (!enableEmergencyEscapeSkill)
            return false;

        if (escapeMotor == null)
            return false;

        bool activated = escapeMotor.TryActivateEmergencyEscape();

        if (activated && writeLog)
            Debug.Log("[TargetSkillController] ��� Ż�� �ߵ� ��û ����");

        return activated;
    }

    public bool TryAutoEmergencyEscape(float healthRatio)
    {
        if (!enableEmergencyEscapeSkill)
            return false;

        if (escapeMotor == null)
            return false;

        if (!escapeMotor.ShouldAutoTriggerEmergencyEscape(healthRatio))
            return false;

        return TryUseEmergencyEscape();
    }

    public bool TryUseSkill(TargetSkillType skillType, Vector3 escapeDestination)
    {
        switch (skillType)
        {
            case TargetSkillType.Barricade:
                return TryUseBarricade(escapeDestination);

            case TargetSkillType.Hologram:
                return TryUseHologram();

            case TargetSkillType.Smoke:
                return TryUseSmoke();

            case TargetSkillType.CommunicationJam:
                return TryUseCommunicationJam();

            case TargetSkillType.EmergencyEscape:
                return TryUseEmergencyEscape();

            default:
                return false;
        }
    }

    public bool TryUseDefensiveSkill(Vector3 escapeDestination, bool preferBarricadeFirst = true)
    {
        if (preferBarricadeFirst)
        {
            if (TryUseBarricade(escapeDestination))
                return true;

            if (TryUseSmoke())
                return true;

            if (TryUseHologram())
                return true;

            return TryUseCommunicationJam();
        }

        if (TryUseSmoke())
            return true;

        if (TryUseHologram())
            return true;

        if (TryUseCommunicationJam())
            return true;

        return TryUseBarricade(escapeDestination);
    }

    public bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        return TryUseDefensiveSkill(escapeDestination, true);
    }

    public void ForceClearCurrentHologram()
    {
        if (currentHologram == null)
            return;

        Destroy(currentHologram.gameObject);
        currentHologram = null;
    }

    private bool CanUseBarricade()
    {
        if (!enableBarricadeSkill)
            return false;

        if (barricadePrefab == null)
            return false;

        if (threatTracker == null)
            return false;

        if (Time.time < nextBarricadeReadyTime)
            return false;

        if (navAgent != null && !navAgent.isOnNavMesh)
            return false;

        return true;
    }

    private bool CanUseHologram()
    {
        if (!enableHologramSkill)
            return false;

        if (hologramPrefab == null)
            return false;

        if (Time.time < nextHologramReadyTime)
            return false;

        return true;
    }

    private bool CanUseSmoke()
    {
        if (!enableSmokeSkill)
            return false;

        if (smokePrefab == null)
            return false;

        if (Time.time < nextSmokeReadyTime)
            return false;

        return true;
    }

    private bool CanUseCommunicationJam()
    {
        if (!enableCommunicationJamSkill)
            return false;

        if (commanderUIController == null)
            return false;

        if (Time.time < nextCommunicationJamReadyTime)
            return false;

        return true;
    }

    private bool TryResolveBarricadeSpawnPosition(Vector3 desiredPosition, out Vector3 spawnPosition)
    {
        spawnPosition = desiredPosition;

        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            return false;

        spawnPosition = hit.position;

        Vector3 flatOffset = spawnPosition - transform.position;
        flatOffset.y = 0f;

        if (flatOffset.sqrMagnitude < 0.8f * 0.8f)
            return false;

        return true;
    }

    private void SpawnBarricade(Vector3 spawnPosition, Vector3 escapeForward)
    {
        Quaternion rotation = escapeForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(escapeForward, Vector3.up)
            : Quaternion.identity;

        GameObject instance = Instantiate(barricadePrefab, spawnPosition, rotation);
        activeBarricades.Enqueue(instance);

        TrimActiveBarricades();

        if (writeLog)
            Debug.Log($"[TargetSkillController] �ٸ����̵� ����: {spawnPosition}");
    }

    private Vector3 GetHologramSpawnPosition()
    {
        Transform basis = hologramSpawnPoint != null ? hologramSpawnPoint : transform;
        Vector3 finalPosition = basis.position + hologramSpawnOffset;

        if (useForwardOffset)
        {
            Vector3 forward = basis.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                finalPosition += forward.normalized * hologramForwardDistance;
        }

        return finalPosition;
    }

    private Vector3 GetSmokeSpawnPosition()
    {
        Transform basis = smokeSpawnPoint != null ? smokeSpawnPoint : transform;
        Vector3 finalPosition = basis.position + smokeSpawnOffset;

        if (useSmokeForwardOffset)
        {
            Vector3 forward = basis.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                finalPosition += forward.normalized * smokeForwardDistance;
        }

        return finalPosition;
    }

    private void TrimActiveBarricades()
    {
        CleanupDestroyedBarricades();

        while (activeBarricades.Count > maxActiveBarricades)
        {
            GameObject oldest = activeBarricades.Dequeue();

            if (oldest != null)
                Destroy(oldest);
        }
    }

    private void CleanupDestroyedBarricades()
    {
        int count = activeBarricades.Count;

        for (int i = 0; i < count; i++)
        {
            GameObject obj = activeBarricades.Dequeue();

            if (obj != null)
                activeBarricades.Enqueue(obj);
        }
    }

    private void DestroyAllActiveBarricades()
    {
        while (activeBarricades.Count > 0)
        {
            GameObject obj = activeBarricades.Dequeue();

            if (obj != null)
                Destroy(obj);
        }
    }

    private void ClampValues()
    {
        barricadeCooldown = Mathf.Max(0f, barricadeCooldown);
        minDestinationDistance = Mathf.Max(0.5f, minDestinationDistance);
        maxThreatDistance = Mathf.Max(0.5f, maxThreatDistance);
        minThreatDistance = Mathf.Max(0f, minThreatDistance);
        behindDotThreshold = Mathf.Clamp(behindDotThreshold, -1f, 1f);
        minDistanceBetweenPlacements = Mathf.Max(0f, minDistanceBetweenPlacements);

        spawnBehindDistance = Mathf.Max(0.5f, spawnBehindDistance);
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        maxActiveBarricades = Mathf.Max(1, maxActiveBarricades);

        hologramCooldown = Mathf.Max(0f, hologramCooldown);
        hologramForwardDistance = Mathf.Max(0f, hologramForwardDistance);

        smokeCooldown = Mathf.Max(0f, smokeCooldown);
        smokeForwardDistance = Mathf.Max(0f, smokeForwardDistance);

        communicationJamCooldown = Mathf.Max(0f, communicationJamCooldown);
        communicationJamDuration = Mathf.Max(0.1f, communicationJamDuration);

        normalEmergencyEscapeCharges = Mathf.Max(0, normalEmergencyEscapeCharges);
        hardEmergencyEscapeCharges = Mathf.Max(0, hardEmergencyEscapeCharges);
    }
}