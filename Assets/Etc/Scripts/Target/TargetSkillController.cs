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
    public int emergencyEscapeCharges = 1;
    public bool autoUseEmergencyEscape = true;

    [Header("Skill Toggle")]
    [SerializeField] private bool enableBarricadeSkill = true;
    [SerializeField] private bool enableHologramSkill = true;
    [SerializeField] private bool enableSmokeSkill = true;
    [SerializeField] private bool enableCommunicationJamSkill = true;
    [SerializeField] private bool enableEmergencyEscapeSkill = false;

    [Header("Auto Defensive Skill Control")]
    [SerializeField] private bool autoUseDefensiveSkillOnlyOncePerThreatEncounter = true;
    [SerializeField] private float autoDefensiveSharedCooldown = 10f;
    [SerializeField] private float threatResetDelay = 1f;

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

    private float nextAutoDefensiveReadyTime = -999f;
    private bool wasThreatActiveLastFrame;
    private bool autoDefensiveUsedInCurrentThreatEncounter;
    private float threatLastLostTime = -999f;

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
        ApplyEmergencyEscapeSettingsToEscapeMotor();
    }

    private void OnValidate()
    {
        ClampValues();

        if (Application.isPlaying)
            ApplyEmergencyEscapeSettingsToEscapeMotor();
    }

    private void Update()
    {
        UpdateThreatEncounterState();
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

        nextAutoDefensiveReadyTime = -999f;
        wasThreatActiveLastFrame = false;
        autoDefensiveUsedInCurrentThreatEncounter = false;
        threatLastLostTime = -999f;

        ApplyEmergencyEscapeSettingsToEscapeMotor();

        if (destroyActiveBarricades)
            DestroyAllActiveBarricades();

        if (destroyActiveHologram)
            ForceClearCurrentHologram();
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

        Vector3 behindDirection = transform.position - nearestAgent.position;
        behindDirection.y = 0f;

        if (behindDirection.sqrMagnitude < 0.0001f)
            behindDirection = -escapeForward;

        behindDirection.Normalize();

        Vector3 desiredPosition = transform.position + behindDirection * spawnBehindDistance;

        if (!TryResolveBarricadePosition(desiredPosition, out Vector3 finalPosition))
            return false;

        if (hasLastPlacementPosition)
        {
            Vector3 flatDelta = finalPosition - lastPlacementPosition;
            flatDelta.y = 0f;

            if (flatDelta.magnitude < minDistanceBetweenPlacements)
                return false;
        }

        GameObject spawnedBarricade = Instantiate(barricadePrefab, finalPosition, Quaternion.identity);
        RegisterBarricade(spawnedBarricade);

        lastPlacementPosition = finalPosition;
        hasLastPlacementPosition = true;
        nextBarricadeReadyTime = Time.time + barricadeCooldown;

        if (writeLog)
            Debug.Log($"[TargetSkillController] 바리케이드 설치: {finalPosition}");

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

            ForceClearCurrentHologram();
        }

        Vector3 spawnPosition = GetHologramSpawnPosition();
        Quaternion spawnRotation = copyCurrentRotation ? transform.rotation : Quaternion.identity;

        TargetHologram spawned = Instantiate(hologramPrefab, spawnPosition, spawnRotation);
        spawned.Initialize(transform);

        currentHologram = spawned;
        nextHologramReadyTime = Time.time + hologramCooldown;

        if (writeLog)
            Debug.Log($"[TargetSkillController] 홀로그램 생성: {spawnPosition}");

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
            Debug.Log($"[TargetSkillController] 연막탄 생성: {spawnPosition}");

        return true;
    }

    public bool TryUseCommunicationJam()
    {
        return TryUseCommunicationJamOnCommandSubmission();
    }

    public bool TryUseCommunicationJamOnCommandSubmission()
    {
        if (!CanUseCommunicationJam())
            return false;

        if (!TryJamRandomAgentInput(out int jammedAgentId))
            return false;

        nextCommunicationJamReadyTime = Time.time + communicationJamCooldown;

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] 통신 방해 발동: AgentID {jammedAgentId}, 지속시간 {communicationJamDuration}초");
        }

        return true;
    }

    public bool TryUseEmergencyEscape()
    {
        if (!enableEmergencyEscapeSkill)
            return false;

        if (escapeMotor == null)
            return false;

        ApplyEmergencyEscapeSettingsToEscapeMotor();

        bool activated = escapeMotor.TryActivateEmergencyEscape();

        if (activated && writeLog)
            Debug.Log("[TargetSkillController] 긴급 탈출 발동 요청 성공");

        return activated;
    }

    public bool TryAutoEmergencyEscape(float healthRatio)
    {
        if (!enableEmergencyEscapeSkill)
            return false;

        if (escapeMotor == null)
            return false;

        ApplyEmergencyEscapeSettingsToEscapeMotor();

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

            return TryUseHologram();
        }

        if (TryUseSmoke())
            return true;

        if (TryUseHologram())
            return true;

        return TryUseBarricade(escapeDestination);
    }

    public bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        if (!CanUseAutoDefensiveSkill())
            return false;

        bool used = TryUseDefensiveSkill(escapeDestination, true);
        if (!used)
            return false;

        nextAutoDefensiveReadyTime = Time.time + autoDefensiveSharedCooldown;
        autoDefensiveUsedInCurrentThreatEncounter = true;

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] 자동 방어 스킬 발동: sharedCooldown={autoDefensiveSharedCooldown:0.##}, currentThreatLocked={autoUseDefensiveSkillOnlyOncePerThreatEncounter}");
        }

        return true;
    }

    private bool CanUseAutoDefensiveSkill()
    {
        if (Time.time < nextAutoDefensiveReadyTime)
            return false;

        if (!autoUseDefensiveSkillOnlyOncePerThreatEncounter)
            return true;

        return !autoDefensiveUsedInCurrentThreatEncounter;
    }

    private void UpdateThreatEncounterState()
    {
        if (threatTracker == null)
            return;

        bool hasThreat = threatTracker.HasAnyThreat();

        if (hasThreat)
        {
            bool shouldResetEncounter =
                !wasThreatActiveLastFrame &&
                Time.time - threatLastLostTime >= threatResetDelay;

            if (shouldResetEncounter)
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

    private bool TryJamRandomAgentInput(out int jammedAgentId)
    {
        jammedAgentId = -1;

        if (commanderUIController == null)
            return false;

        return commanderUIController.TryJamRandomAvailableAgentInput(
            communicationJamDuration,
            out jammedAgentId);
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

    private void RegisterBarricade(GameObject spawnedBarricade)
    {
        if (spawnedBarricade == null)
            return;

        activeBarricades.Enqueue(spawnedBarricade);

        while (activeBarricades.Count > maxActiveBarricades)
        {
            GameObject oldest = activeBarricades.Dequeue();
            if (oldest != null)
                Destroy(oldest);
        }
    }

    private void CleanupDestroyedBarricades()
    {
        if (activeBarricades.Count == 0)
            return;

        int originalCount = activeBarricades.Count;
        for (int i = 0; i < originalCount; i++)
        {
            GameObject barricade = activeBarricades.Dequeue();
            if (barricade != null)
                activeBarricades.Enqueue(barricade);
        }
    }

    private void DestroyAllActiveBarricades()
    {
        while (activeBarricades.Count > 0)
        {
            GameObject barricade = activeBarricades.Dequeue();
            if (barricade != null)
                Destroy(barricade);
        }
    }

    private bool TryResolveBarricadePosition(Vector3 desiredPosition, out Vector3 finalPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            finalPosition = hit.position;
            return true;
        }

        finalPosition = desiredPosition;
        return false;
    }

    private Vector3 GetHologramSpawnPosition()
    {
        Transform origin = hologramSpawnPoint != null ? hologramSpawnPoint : transform;
        Vector3 position = origin.position + hologramSpawnOffset;

        if (useForwardOffset)
            position += transform.forward * hologramForwardDistance;

        return position;
    }

    private Vector3 GetSmokeSpawnPosition()
    {
        Transform origin = smokeSpawnPoint != null ? smokeSpawnPoint : transform;
        Vector3 position = origin.position + smokeSpawnOffset;

        if (useSmokeForwardOffset)
            position += transform.forward * smokeForwardDistance;

        return position;
    }

    private void ApplyEmergencyEscapeSettingsToEscapeMotor()
    {
        if (escapeMotor == null)
            return;

        escapeMotor.ConfigureEmergencyEscape(
            enableEmergencyEscapeSkill,
            emergencyEscapeCharges,
            enableEmergencyEscapeSkill && autoUseEmergencyEscape
        );
    }

    private void ClampValues()
    {
        barricadeCooldown = Mathf.Max(0f, barricadeCooldown);
        minDestinationDistance = Mathf.Max(0f, minDestinationDistance);
        maxThreatDistance = Mathf.Max(0f, maxThreatDistance);
        minThreatDistance = Mathf.Max(0f, minThreatDistance);
        minDistanceBetweenPlacements = Mathf.Max(0f, minDistanceBetweenPlacements);
        spawnBehindDistance = Mathf.Max(0f, spawnBehindDistance);
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        maxActiveBarricades = Mathf.Max(1, maxActiveBarricades);

        hologramForwardDistance = Mathf.Max(0f, hologramForwardDistance);
        hologramCooldown = Mathf.Max(0f, hologramCooldown);

        smokeForwardDistance = Mathf.Max(0f, smokeForwardDistance);
        smokeCooldown = Mathf.Max(0f, smokeCooldown);

        communicationJamCooldown = Mathf.Max(0f, communicationJamCooldown);
        communicationJamDuration = Mathf.Max(0f, communicationJamDuration);

        emergencyEscapeCharges = Mathf.Max(0, emergencyEscapeCharges);

        autoDefensiveSharedCooldown = Mathf.Max(0f, autoDefensiveSharedCooldown);
        threatResetDelay = Mathf.Max(0f, threatResetDelay);
    }
}