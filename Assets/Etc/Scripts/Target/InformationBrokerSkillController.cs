using UnityEngine;
using UnityEngine.AI;

public class InformationBrokerSkillController : TargetSkillController
{
    [Header("Communication Jam")]
    [SerializeField] private Vector2 communicationJamCooldownRange = new Vector2(30f, 60f);
    [SerializeField] private float communicationJamRetryDelay = 5f;
    [SerializeField] private bool useRandomCommunicationJam = true;
    [SerializeField] private bool allowOnlyOneCommunicationJamAtOnce = true;

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

    [Header("Command Distortion")]
    [SerializeField] private Vector2 commandDistortionCooldownRange = new Vector2(30f, 60f);
    [SerializeField] private bool commandDistortionReadyOnStart = false;
    [SerializeField] private bool keepCommandDistortionArmedUntilCommand = true;
    [SerializeField] private float commandDistortionArmedDuration = 20f;
    [SerializeField] private bool postponeCommunicationJamWhileCommandDistortionArmed = true;
    [SerializeField] private bool commandDistortionIgnoresSharedCooldown = true;
    [SerializeField] private float commandDistortionRadius = 4f;
    [SerializeField] private float minCommandDistortionDistance = 1.5f;
    [SerializeField] private float commandDistortionNavMeshSampleRadius = 3f;
    [SerializeField] private int commandDistortionSampleCount = 12;

    [Header("Interference Control")]
    [SerializeField] private float interferenceSharedCooldown = 12f;
    [SerializeField] private Vector2 communicationJamPostponeRange = new Vector2(5f, 10f);

    [Header("Command Distortion Debug")]
    [SerializeField] private bool logCommandDistortionCheck = true;

    [Header("Emergency Escape")]
    [SerializeField] private bool autoUseEmergencyEscape = false;

    [Header("Skill Unlock Level")]
    [SerializeField] private int communicationJamUnlockLevel = 1;
    [SerializeField] private int hologramUnlockLevel = 3;
    [SerializeField] private int emergencyEscapeUnlockLevel = 5;
    [SerializeField] private int commandDistortionUnlockLevel = 7;

    [Header("Auto Defensive Skill Control")]
    [SerializeField] private bool autoUseDefensiveSkillOnlyOncePerThreatEncounter = true;
    [SerializeField] private float autoDefensiveSharedCooldown = 10f;
    [SerializeField] private float threatResetDelay = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private const int EmergencyEscapeFixedChargeCount = 1;

    private TargetHologram currentHologram;

    private bool enableCommunicationJamSkill;
    private bool enableHologramSkill;
    private bool enableEmergencyEscapeSkill;
    private bool enableCommandDistortionSkill;

    private int currentHologramMaxUseCount;
    private int remainingHologramUseCount;
    private int currentJammedAgentId = -1;

    private bool commandDistortionArmed;
    private float commandDistortionArmedEndTime = -999f;

    private float nextCommunicationJamReadyTime = -999f;
    private float nextHologramReadyTime = -999f;
    private float nextCommandDistortionReadyTime = -999f;
    private float nextInterferenceReadyTime = -999f;
    private float nextAutoDefensiveReadyTime = -999f;

    private bool wasThreatActiveLastFrame;
    private bool autoDefensiveUsedInCurrentThreatEncounter;
    private float threatLastLostTime = -999f;

    public override TargetHologram CurrentHologram => currentHologram;

    public override bool IsCommunicationJamUnlocked => enableCommunicationJamSkill;
    public override bool IsHologramUnlocked => enableHologramSkill;
    public override bool IsEmergencyEscapeUnlocked => enableEmergencyEscapeSkill;
    public override bool IsCommandDistortionUnlocked => enableCommandDistortionSkill;

    public bool IsCommandDistortionArmed => commandDistortionArmed;

    public float CommandDistortionArmedTimeLeft
    {
        get
        {
            if (!commandDistortionArmed)
                return 0f;

            if (keepCommandDistortionArmedUntilCommand)
                return Mathf.Infinity;

            return Mathf.Max(0f, commandDistortionArmedEndTime - Time.time);
        }
    }

    public override float CommunicationJamRemainingCooldown =>
        Mathf.Max(0f, nextCommunicationJamReadyTime - Time.time);

    public override float HologramRemainingCooldown =>
        Mathf.Max(0f, nextHologramReadyTime - Time.time);

    public override float CommandDistortionRemainingCooldown
    {
        get
        {
            if (commandDistortionArmed)
                return 0f;

            float readyTime = commandDistortionIgnoresSharedCooldown
                ? nextCommandDistortionReadyTime
                : Mathf.Max(nextCommandDistortionReadyTime, nextInterferenceReadyTime);

            return Mathf.Max(0f, readyTime - Time.time);
        }
    }

    public override int EmergencyEscapeCharges =>
        enableEmergencyEscapeSkill ? EmergencyEscapeFixedChargeCount : 0;

    public override bool AutoUseEmergencyEscape => autoUseEmergencyEscape;

    public override int RemainingEmergencyEscapeCount => enableEmergencyEscapeSkill && EscapeMotor != null
        ? EscapeMotor.RemainingEmergencyEscapeCount
        : 0;

    public int CurrentHologramMaxUseCount => currentHologramMaxUseCount;
    public int RemainingHologramUseCount => remainingHologramUseCount;

    protected override void Awake()
    {
        base.Awake();

        ClampValues();
        ApplyEmergencyEscapeSettingsToEscapeMotor(true);
    }

    protected override void OnValidate()
    {
        ClampValues();

        if (Application.isPlaying)
            ApplyEmergencyEscapeSettingsToEscapeMotor(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ForceClearCurrentHologram();
    }

    private void Update()
    {
        UpdateThreatEncounterState();
        UpdateCommunicationJamState();
        UpdateCommandDistortionState();
        TickRandomCommunicationJam();
    }

    private void LateUpdate()
    {
        if (currentHologram == null)
            return;

        if (currentHologram.gameObject == null)
            currentHologram = null;
    }

    public override void ApplySkillUnlocks(int targetLevel)
    {
        enableCommunicationJamSkill = targetLevel >= communicationJamUnlockLevel;
        enableHologramSkill = targetLevel >= hologramUnlockLevel;
        enableEmergencyEscapeSkill = targetLevel >= emergencyEscapeUnlockLevel;
        enableCommandDistortionSkill = targetLevel >= commandDistortionUnlockLevel;

        currentHologramMaxUseCount = GetHologramUseCountByLevel(targetLevel);
        remainingHologramUseCount = currentHologramMaxUseCount;

        ClampValues();
        ApplyEmergencyEscapeSettingsToEscapeMotor(true);
        ResetRuntimeState(true, true);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[InformationBrokerSkillController] 스킬 해금 적용 - " +
                $"Level: {targetLevel}, " +
                $"CommunicationJam: {enableCommunicationJamSkill}, " +
                $"Hologram: {enableHologramSkill}, " +
                $"EmergencyEscape: {enableEmergencyEscapeSkill}, " +
                $"CommandDistortion: {enableCommandDistortionSkill}, " +
                $"EmergencyEscapeCharges: {EmergencyEscapeCharges}, " +
                $"HologramUseCount: {remainingHologramUseCount}");
        }
    }

    public override void ResetRuntimeState(
        bool destroyActiveBarricades = true,
        bool destroyActiveHologram = true)
    {
        nextCommunicationJamReadyTime = -999f;
        nextHologramReadyTime = -999f;
        nextCommandDistortionReadyTime = -999f;
        nextInterferenceReadyTime = -999f;
        nextAutoDefensiveReadyTime = -999f;

        currentJammedAgentId = -1;
        remainingHologramUseCount = currentHologramMaxUseCount;

        commandDistortionArmed = false;
        commandDistortionArmedEndTime = -999f;

        wasThreatActiveLastFrame = false;
        autoDefensiveUsedInCurrentThreatEncounter = false;
        threatLastLostTime = -999f;

        ApplyEmergencyEscapeSettingsToEscapeMotor(true);

        if (destroyActiveHologram)
            ForceClearCurrentHologram();

        ScheduleNextCommunicationJam();
        ScheduleNextCommandDistortionCooldown(commandDistortionReadyOnStart);
    }

    public override bool TryUseEmergencyEscape()
    {
        if (!enableEmergencyEscapeSkill)
            return false;

        if (EscapeMotor == null)
            return false;

        ApplyEmergencyEscapeSettingsToEscapeMotor(false);

        bool activated = EscapeMotor.TryActivateEmergencyEscape();

        if (activated && enableDebugLog)
            Debug.Log("[InformationBrokerSkillController] 긴급 탈출 발동");

        return activated;
    }

    public override bool TryAutoEmergencyEscape(float healthRatio)
    {
        if (!enableEmergencyEscapeSkill)
            return false;

        if (!autoUseEmergencyEscape)
            return false;

        if (EscapeMotor == null)
            return false;

        ApplyEmergencyEscapeSettingsToEscapeMotor(false);

        if (!EscapeMotor.ShouldAutoTriggerEmergencyEscape(healthRatio))
            return false;

        return TryUseEmergencyEscape();
    }

    public override bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        if (!CanUseAutoDefensiveSkill())
            return false;

        bool used = TryUseHologram();

        if (!used)
            return false;

        nextAutoDefensiveReadyTime = Time.time + autoDefensiveSharedCooldown;
        autoDefensiveUsedInCurrentThreatEncounter = true;

        if (enableDebugLog)
            Debug.Log("[InformationBrokerSkillController] 자동 방어 스킬 발동 - 홀로그램");

        return true;
    }

    public override bool TryUseCommunicationJam()
    {
        if (!CanUseCommunicationJam())
            return false;

        if (!TryJamRandomAgentInput(out int jammedAgentId))
        {
            nextCommunicationJamReadyTime = Time.time + communicationJamRetryDelay;
            return false;
        }

        currentJammedAgentId = jammedAgentId;

        nextInterferenceReadyTime = Time.time + interferenceSharedCooldown;
        ScheduleNextCommunicationJam();

        if (enableDebugLog)
        {
            Debug.Log(
                $"[InformationBrokerSkillController] 통신 방해 발동 - " +
                $"AgentID: {jammedAgentId}, " +
                $"다음 통신 방해까지 {CommunicationJamRemainingCooldown:F1}초");
        }

        return true;
    }

    public override bool TryUseCommunicationJamOnCommandSubmission()
    {
        if (enableDebugLog)
        {
            Debug.Log(
                "[InformationBrokerSkillController] 통신 방해는 명령 버튼 클릭으로 발동하지 않습니다. " +
                "무작위 시간에 자동 발동됩니다.");
        }

        return false;
    }

    public override bool TryUseHologram()
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

        TargetHologram spawnedHologram = Instantiate(hologramPrefab, spawnPosition, spawnRotation);
        spawnedHologram.Initialize(transform);

        currentHologram = spawnedHologram;
        remainingHologramUseCount--;
        nextHologramReadyTime = Time.time + hologramCooldown;

        if (enableDebugLog)
        {
            Debug.Log(
                $"[InformationBrokerSkillController] 홀로그램 생성 - " +
                $"Position: {spawnPosition}, " +
                $"RemainingUseCount: {remainingHologramUseCount}");
        }

        return true;
    }

    public override bool TryDistortCommandPosition(
        Vector3 originalPosition,
        out Vector3 distortedPosition)
    {
        distortedPosition = originalPosition;

        if (logCommandDistortionCheck)
        {
            Debug.Log(
                $"[InformationBrokerSkillController] 명령 변조 적용 체크 - " +
                $"Original: {originalPosition}, " +
                $"Unlocked: {enableCommandDistortionSkill}, " +
                $"Armed: {commandDistortionArmed}, " +
                $"ArmedTimeLeft: {GetCommandDistortionArmedTimeLeftText()}, " +
                $"CooldownRemaining: {CommandDistortionRemainingCooldown:F1}, " +
                $"SharedCooldownRemaining: {Mathf.Max(0f, nextInterferenceReadyTime - Time.time):F1}, " +
                $"IgnoreSharedCooldown: {commandDistortionIgnoresSharedCooldown}");
        }

        if (!CanUseCommandDistortion(out string failReason))
        {
            if (logCommandDistortionCheck)
                Debug.Log($"[InformationBrokerSkillController] 명령 변조 실패 - {failReason}");

            return false;
        }

        if (!TryFindDistortedCommandPosition(originalPosition, out distortedPosition))
        {
            if (logCommandDistortionCheck)
            {
                Debug.Log(
                    $"[InformationBrokerSkillController] 명령 변조 실패 - " +
                    $"입력 좌표 주변에서 NavMesh 위치를 찾지 못했습니다. Original: {originalPosition}");
            }

            return false;
        }

        commandDistortionArmed = false;
        commandDistortionArmedEndTime = -999f;

        ScheduleNextCommandDistortionCooldown(false);

        nextInterferenceReadyTime = Time.time + interferenceSharedCooldown;

        if (nextCommunicationJamReadyTime < nextInterferenceReadyTime)
            PostponeCommunicationJamAfterSharedCooldown();

        if (enableDebugLog || logCommandDistortionCheck)
        {
            Debug.Log(
                $"[InformationBrokerSkillController] 명령 변조 적용 성공 - " +
                $"Original: {originalPosition}, " +
                $"Distorted: {distortedPosition}, " +
                $"Distance: {Vector3.Distance(originalPosition, distortedPosition):F2}, " +
                $"NextCooldown: {CommandDistortionRemainingCooldown:F1}초");
        }

        return true;
    }

    public override void ForceClearCurrentHologram()
    {
        if (currentHologram == null)
            return;

        Destroy(currentHologram.gameObject);
        currentHologram = null;
    }

    protected override void ResolveReferences()
    {
        base.ResolveReferences();

        if (hologramSpawnPoint == null)
            hologramSpawnPoint = transform;
    }

    private void TickRandomCommunicationJam()
    {
        if (!useRandomCommunicationJam)
            return;

        if (!enableCommunicationJamSkill)
            return;

        if (Time.time < nextCommunicationJamReadyTime)
            return;

        if (Time.time < nextInterferenceReadyTime)
        {
            PostponeCommunicationJamAfterSharedCooldown();
            return;
        }

        if (postponeCommunicationJamWhileCommandDistortionArmed && commandDistortionArmed)
        {
            PostponeCommunicationJamAfterSharedCooldown();

            if (enableDebugLog)
            {
                Debug.Log(
                    "[InformationBrokerSkillController] 통신 방해 연기 - " +
                    "명령 변조가 준비 중이므로 통신 방해를 뒤로 미룹니다.");
            }

            return;
        }

        bool used = TryUseCommunicationJam();

        if (!used && Time.time >= nextCommunicationJamReadyTime)
            nextCommunicationJamReadyTime = Time.time + communicationJamRetryDelay;
    }

    private void UpdateCommandDistortionState()
    {
        if (!enableCommandDistortionSkill)
            return;

        if (commandDistortionArmed)
        {
            if (!keepCommandDistortionArmedUntilCommand &&
                Time.time > commandDistortionArmedEndTime)
            {
                commandDistortionArmed = false;
                commandDistortionArmedEndTime = -999f;

                ScheduleNextCommandDistortionCooldown(false);

                if (enableDebugLog)
                    Debug.Log("[InformationBrokerSkillController] 명령 변조 준비 상태 만료");
            }

            return;
        }

        if (Time.time < nextCommandDistortionReadyTime)
            return;

        if (!commandDistortionIgnoresSharedCooldown && Time.time < nextInterferenceReadyTime)
            return;

        ArmCommandDistortion("쿨타임 종료");
    }

    private void ArmCommandDistortion(string reason)
    {
        if (!enableCommandDistortionSkill)
            return;

        commandDistortionArmed = true;

        if (keepCommandDistortionArmedUntilCommand)
            commandDistortionArmedEndTime = float.PositiveInfinity;
        else
            commandDistortionArmedEndTime = Time.time + commandDistortionArmedDuration;

        nextInterferenceReadyTime = Time.time + interferenceSharedCooldown;

        if (nextCommunicationJamReadyTime < nextInterferenceReadyTime)
            PostponeCommunicationJamAfterSharedCooldown();

        if (enableDebugLog)
        {
            string timeText = keepCommandDistortionArmedUntilCommand
                ? "다음 좌표 명령까지 유지"
                : $"{commandDistortionArmedDuration:F1}초 유지";

            Debug.Log(
                $"[InformationBrokerSkillController] 명령 변조 준비 완료 - " +
                $"Reason: {reason}, 유지 시간: {timeText}");
        }
    }

    private void ScheduleNextCommunicationJam()
    {
        if (!enableCommunicationJamSkill || !useRandomCommunicationJam)
            return;

        float cooldown = Random.Range(
            communicationJamCooldownRange.x,
            communicationJamCooldownRange.y
        );

        nextCommunicationJamReadyTime = Time.time + cooldown;
    }

    private void ScheduleNextCommandDistortionCooldown(bool readyImmediately)
    {
        if (!enableCommandDistortionSkill)
            return;

        if (readyImmediately)
        {
            nextCommandDistortionReadyTime = Time.time;
            return;
        }

        float cooldown = Random.Range(
            commandDistortionCooldownRange.x,
            commandDistortionCooldownRange.y
        );

        nextCommandDistortionReadyTime = Time.time + cooldown;
    }

    private void PostponeCommunicationJamAfterSharedCooldown()
    {
        float delay = Random.Range(
            communicationJamPostponeRange.x,
            communicationJamPostponeRange.y
        );

        nextCommunicationJamReadyTime = nextInterferenceReadyTime + delay;

        if (enableDebugLog)
        {
            Debug.Log(
                $"[InformationBrokerSkillController] 통신 방해 연기 - " +
                $"다른 교란 스킬과 겹치지 않도록 {nextCommunicationJamReadyTime - Time.time:F1}초 뒤로 조정");
        }
    }

    private void UpdateCommunicationJamState()
    {
        if (currentJammedAgentId < 0)
            return;

        if (CommanderUIController == null)
        {
            currentJammedAgentId = -1;
            return;
        }

        if (!CommanderUIController.IsAgentInputJammed(currentJammedAgentId))
            currentJammedAgentId = -1;
    }

    private void ApplyEmergencyEscapeSettingsToEscapeMotor(bool resetUsage)
    {
        if (EscapeMotor == null)
            return;

        EscapeMotor.ConfigureEmergencyEscape(
            enableEmergencyEscapeSkill,
            EmergencyEscapeCharges,
            autoUseEmergencyEscape,
            resetUsage);
    }

    private bool CanUseCommunicationJam()
    {
        if (!enableCommunicationJamSkill)
            return false;

        if (CommanderUIController == null)
        {
            if (enableDebugLog)
                Debug.Log("[InformationBrokerSkillController] 통신 방해 실패 - CommanderUIController 참조가 없습니다.");

            return false;
        }

        if (Time.time < nextCommunicationJamReadyTime)
            return false;

        if (Time.time < nextInterferenceReadyTime)
            return false;

        if (postponeCommunicationJamWhileCommandDistortionArmed && commandDistortionArmed)
            return false;

        if (allowOnlyOneCommunicationJamAtOnce &&
            currentJammedAgentId >= 0 &&
            CommanderUIController.IsAgentInputJammed(currentJammedAgentId))
        {
            return false;
        }

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

        if (autoUseDefensiveSkillOnlyOncePerThreatEncounter && autoDefensiveUsedInCurrentThreatEncounter)
            return false;

        return true;
    }

    private bool CanUseHologram()
    {
        if (!enableHologramSkill)
            return false;

        if (hologramPrefab == null)
            return false;

        if (remainingHologramUseCount <= 0)
            return false;

        if (Time.time < nextHologramReadyTime)
            return false;

        return true;
    }

    private bool CanUseCommandDistortion(out string failReason)
    {
        failReason = "";

        if (!enableCommandDistortionSkill)
        {
            failReason = "CommandDistortion이 아직 해금되지 않았습니다.";
            return false;
        }

        if (!commandDistortionArmed)
        {
            bool sharedCooldownReady =
                commandDistortionIgnoresSharedCooldown ||
                Time.time >= nextInterferenceReadyTime;

            if (Time.time >= nextCommandDistortionReadyTime && sharedCooldownReady)
            {
                ArmCommandDistortion("명령 입력 시점에 쿨타임 종료 확인");
            }
            else
            {
                failReason =
                    $"아직 명령 변조가 준비되지 않았습니다. " +
                    $"남은 쿨타임: {CommandDistortionRemainingCooldown:F1}초";

                return false;
            }
        }

        if (!keepCommandDistortionArmedUntilCommand &&
            Time.time > commandDistortionArmedEndTime)
        {
            commandDistortionArmed = false;
            commandDistortionArmedEndTime = -999f;

            ScheduleNextCommandDistortionCooldown(false);

            failReason = "명령 변조 준비 시간이 만료되었습니다.";
            return false;
        }

        return true;
    }

    private bool TryJamRandomAgentInput(out int jammedAgentId)
    {
        jammedAgentId = -1;

        if (CommanderUIController == null)
        {
            if (enableDebugLog)
                Debug.Log("[InformationBrokerSkillController] 통신 방해 실패 - CommanderUIController가 없습니다.");

            return false;
        }

        return CommanderUIController.TryJamRandomAvailableAgentInputUntilCommandButton(
            out jammedAgentId);
    }

    private Vector3 GetHologramSpawnPosition()
    {
        Vector3 basePosition = hologramSpawnPoint != null
            ? hologramSpawnPoint.position
            : transform.position;

        Vector3 finalPosition = basePosition + hologramSpawnOffset;

        if (useForwardOffset)
            finalPosition += transform.forward * hologramForwardDistance;

        return finalPosition;
    }

    private bool TryFindDistortedCommandPosition(
        Vector3 originalPosition,
        out Vector3 distortedPosition)
    {
        distortedPosition = originalPosition;

        float maxRadius = Mathf.Max(minCommandDistortionDistance, commandDistortionRadius);

        for (int i = 0; i < commandDistortionSampleCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(minCommandDistortionDistance, maxRadius);

            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 rawPosition = originalPosition + direction * distance;

            if (!NavMesh.SamplePosition(
                    rawPosition,
                    out NavMeshHit hit,
                    commandDistortionNavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            float finalDistance = Vector3.Distance(originalPosition, hit.position);

            if (finalDistance < minCommandDistortionDistance)
                continue;

            distortedPosition = hit.position;
            return true;
        }

        return false;
    }

    private int GetHologramUseCountByLevel(int targetLevel)
    {
        if (targetLevel < hologramUnlockLevel)
            return 0;

        if (targetLevel >= 9)
            return 3;

        if (targetLevel >= 6)
            return 2;

        return 1;
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

    private string GetCommandDistortionArmedTimeLeftText()
    {
        if (!commandDistortionArmed)
            return "0.0";

        if (keepCommandDistortionArmedUntilCommand)
            return "UntilCommand";

        return Mathf.Max(0f, commandDistortionArmedEndTime - Time.time).ToString("F1");
    }

    private void ClampValues()
    {
        communicationJamCooldownRange.x = Mathf.Max(1f, communicationJamCooldownRange.x);
        communicationJamCooldownRange.y = Mathf.Max(
            communicationJamCooldownRange.x,
            communicationJamCooldownRange.y
        );
        communicationJamRetryDelay = Mathf.Max(1f, communicationJamRetryDelay);

        hologramCooldown = Mathf.Max(0f, hologramCooldown);
        hologramForwardDistance = Mathf.Max(0f, hologramForwardDistance);

        commandDistortionCooldownRange.x = Mathf.Max(1f, commandDistortionCooldownRange.x);
        commandDistortionCooldownRange.y = Mathf.Max(
            commandDistortionCooldownRange.x,
            commandDistortionCooldownRange.y
        );
        commandDistortionArmedDuration = Mathf.Max(1f, commandDistortionArmedDuration);
        commandDistortionRadius = Mathf.Max(0.1f, commandDistortionRadius);
        minCommandDistortionDistance = Mathf.Clamp(
            minCommandDistortionDistance,
            0f,
            commandDistortionRadius
        );
        commandDistortionNavMeshSampleRadius = Mathf.Max(0.1f, commandDistortionNavMeshSampleRadius);
        commandDistortionSampleCount = Mathf.Clamp(commandDistortionSampleCount, 1, 32);

        interferenceSharedCooldown = Mathf.Max(0f, interferenceSharedCooldown);
        communicationJamPostponeRange.x = Mathf.Max(0f, communicationJamPostponeRange.x);
        communicationJamPostponeRange.y = Mathf.Max(
            communicationJamPostponeRange.x,
            communicationJamPostponeRange.y
        );

        communicationJamUnlockLevel = Mathf.Max(1, communicationJamUnlockLevel);
        hologramUnlockLevel = Mathf.Max(1, hologramUnlockLevel);
        emergencyEscapeUnlockLevel = Mathf.Max(1, emergencyEscapeUnlockLevel);
        commandDistortionUnlockLevel = Mathf.Max(1, commandDistortionUnlockLevel);

        autoDefensiveSharedCooldown = Mathf.Max(0f, autoDefensiveSharedCooldown);
        threatResetDelay = Mathf.Max(0f, threatResetDelay);
    }
}