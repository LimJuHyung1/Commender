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
    CommandDistortion,
    EmergencyEscape
}

public class TargetSkillController : MonoBehaviour, ITargetEscapeSkillBlockReceiver
{
    [Header("Common References")]
    [SerializeField] private TargetController targetController;
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private TargetEscapeMotor escapeMotor;
    [SerializeField] private CommanderUIController commanderUIController;

    [Header("Escape Skill Block")]
    [SerializeField] private bool blockHologram = true;
    [SerializeField] private bool blockEmergencyEscape = true;
    [SerializeField] private bool blockSmoke = false;
    [SerializeField] private bool blockBarricade = false;
    [SerializeField] private bool blockCommunicationJam = false;
    [SerializeField] private bool blockCommandDistortion = false;

    [Header("Debug")]
    [SerializeField] private bool writeLog = false;

    private readonly HashSet<Component> escapeSkillBlockSources = new HashSet<Component>();
    private readonly HashSet<Component> targetSkillBlockSources = new HashSet<Component>();

    protected TargetController TargetController => targetController;
    protected TargetThreatTracker ThreatTracker => threatTracker;
    protected NavMeshAgent NavAgent => navAgent;
    protected TargetEscapeMotor EscapeMotor => escapeMotor;
    protected CommanderUIController CommanderUIController => commanderUIController;
    protected bool WriteLog => writeLog;

    public bool IsEscapeSkillBlocked => escapeSkillBlockSources.Count > 0;
    public bool IsTargetSkillBlocked => targetSkillBlockSources.Count > 0;

    public virtual TargetHologram CurrentHologram => null;

    public virtual bool IsBarricadeUnlocked => false;
    public virtual bool IsHologramUnlocked => false;
    public virtual bool IsSmokeUnlocked => false;
    public virtual bool IsCommunicationJamUnlocked => false;
    public virtual bool IsCommandDistortionUnlocked => false;
    public virtual bool IsEmergencyEscapeUnlocked => false;

    public virtual float BarricadeRemainingCooldown => 0f;
    public virtual float HologramRemainingCooldown => 0f;
    public virtual float SmokeRemainingCooldown => 0f;
    public virtual float CommunicationJamRemainingCooldown => 0f;
    public virtual float CommandDistortionRemainingCooldown => 0f;

    public virtual int EmergencyEscapeCharges => 0;

    public virtual int RemainingEmergencyEscapeCount
    {
        get
        {
            if (!IsEmergencyEscapeUnlocked)
                return 0;

            if (escapeMotor == null)
                return 0;

            return escapeMotor.RemainingEmergencyEscapeCount;
        }
    }

    public virtual bool AutoUseEmergencyEscape => false;

    protected virtual void Awake()
    {
        ResolveReferences();
    }

    protected virtual void OnValidate()
    {
    }

    protected virtual void OnDisable()
    {
        ClearEscapeSkillBlockSources();
        ClearTargetSkillBlockSources();
    }

    public void SetEscapeSkillBlocked(Component source, bool blocked)
    {
        if (source == null)
            return;

        bool changed;

        if (blocked)
            changed = escapeSkillBlockSources.Add(source);
        else
            changed = escapeSkillBlockSources.Remove(source);

        if (!changed)
            return;

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] 도주 스킬 차단 상태 변경: {IsEscapeSkillBlocked}, " +
                $"Source={source.name}, Target={name}"
            );
        }
    }

    public void ClearEscapeSkillBlockSources()
    {
        if (escapeSkillBlockSources.Count <= 0)
            return;

        escapeSkillBlockSources.Clear();

        if (writeLog)
            Debug.Log($"[TargetSkillController] 도주 스킬 차단 상태 초기화: {name}");
    }

    public void SetTargetSkillBlocked(Component source, bool blocked)
    {
        if (source == null)
            return;

        bool changed;

        if (blocked)
            changed = targetSkillBlockSources.Add(source);
        else
            changed = targetSkillBlockSources.Remove(source);

        if (!changed)
            return;

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] 타겟 스킬 차단 상태 변경: {IsTargetSkillBlocked}, " +
                $"Source={source.name}, Target={name}"
            );
        }
    }

    public void ClearTargetSkillBlockSources()
    {
        if (targetSkillBlockSources.Count <= 0)
            return;

        targetSkillBlockSources.Clear();

        if (writeLog)
            Debug.Log($"[TargetSkillController] 타겟 스킬 차단 상태 초기화: {name}");
    }

    public bool CanUseSkill(TargetSkillType skillType)
    {
        return CanUseTargetSkill(skillType);
    }

    protected virtual bool CanUseTargetSkill(TargetSkillType skillType)
    {
        if (skillType == TargetSkillType.None)
            return false;

        if (IsTargetUnableToUseSkills())
        {
            if (writeLog)
            {
                Debug.Log(
                    $"[TargetSkillController] 타겟이 스킬을 사용할 수 없는 상태입니다. " +
                    $"Skill={skillType}, Target={name}"
                );
            }

            return false;
        }

        if (!IsSkillUnlocked(skillType))
        {
            if (writeLog)
            {
                Debug.Log(
                    $"[TargetSkillController] 아직 해금되지 않은 스킬입니다. " +
                    $"Skill={skillType}, Target={name}"
                );
            }

            return false;
        }

        if (!CanUseEscapeSkill(skillType))
            return false;

        return true;
    }

    protected virtual bool IsTargetUnableToUseSkills()
    {
        if (targetController == null)
            return false;

        if (targetController.IsCaught)
            return true;

        if (targetController.IsExhausted)
            return true;

        if (IsTargetSkillBlocked)
            return true;

        return false;
    }

    protected virtual bool IsSkillUnlocked(TargetSkillType skillType)
    {
        switch (skillType)
        {
            case TargetSkillType.Barricade:
                return IsBarricadeUnlocked;

            case TargetSkillType.Hologram:
                return IsHologramUnlocked;

            case TargetSkillType.Smoke:
                return IsSmokeUnlocked;

            case TargetSkillType.CommunicationJam:
                return IsCommunicationJamUnlocked;

            case TargetSkillType.CommandDistortion:
                return IsCommandDistortionUnlocked;

            case TargetSkillType.EmergencyEscape:
                return IsEmergencyEscapeUnlocked;

            default:
                return false;
        }
    }

    protected bool CanUseEscapeSkill(TargetSkillType skillType)
    {
        if (!IsEscapeSkillBlocked)
            return true;

        if (!IsBlockedByEscapePrevention(skillType))
            return true;

        if (writeLog)
        {
            Debug.Log(
                $"[TargetSkillController] 체이서의 도주 제지 효과로 스킬 사용 차단: " +
                $"Skill={skillType}, Target={name}"
            );
        }

        return false;
    }

    protected bool IsBlockedByEscapePrevention(TargetSkillType skillType)
    {
        switch (skillType)
        {
            case TargetSkillType.Hologram:
                return blockHologram;

            case TargetSkillType.EmergencyEscape:
                return blockEmergencyEscape;

            case TargetSkillType.Smoke:
                return blockSmoke;

            case TargetSkillType.Barricade:
                return blockBarricade;

            case TargetSkillType.CommunicationJam:
                return blockCommunicationJam;

            case TargetSkillType.CommandDistortion:
                return blockCommandDistortion;

            default:
                return false;
        }
    }

    public virtual void ApplySkillUnlocks(int targetLevel)
    {
    }

    public virtual void ApplySkillUnlocks(
        bool barricade,
        bool hologram,
        bool smoke,
        bool communicationJam,
        bool emergencyEscape,
        int emergencyEscapeChargeCount)
    {
        if (writeLog)
        {
            Debug.LogWarning(
                "[TargetSkillController] 구버전 ApplySkillUnlocks가 호출되었습니다. " +
                "상속 구조에서는 ApplySkillUnlocks(int targetLevel)를 사용하는 것이 좋습니다."
            );
        }
    }

    public virtual void ResetRuntimeState(
        bool destroyActiveBarricades = true,
        bool destroyActiveHologram = true)
    {
        ClearEscapeSkillBlockSources();
        ClearTargetSkillBlockSources();
    }

    public virtual bool TryUseBarricade(Vector3 escapeDestination)
    {
        if (!CanUseTargetSkill(TargetSkillType.Barricade))
            return false;

        return false;
    }

    public virtual bool TryUseHologram()
    {
        if (!CanUseTargetSkill(TargetSkillType.Hologram))
            return false;

        return false;
    }

    public virtual bool TryUseSmoke()
    {
        if (!CanUseTargetSkill(TargetSkillType.Smoke))
            return false;

        return false;
    }

    public virtual bool TryUseCommunicationJam()
    {
        if (!CanUseTargetSkill(TargetSkillType.CommunicationJam))
            return false;

        return false;
    }

    public virtual bool TryUseCommunicationJamOnCommandSubmission()
    {
        return TryUseCommunicationJam();
    }

    public virtual bool TryUseEmergencyEscape()
    {
        if (!CanUseTargetSkill(TargetSkillType.EmergencyEscape))
            return false;

        if (escapeMotor == null)
            return false;

        return escapeMotor.TryActivateEmergencyEscape();
    }

    public virtual bool TryAutoEmergencyEscape(float healthRatio)
    {
        if (!AutoUseEmergencyEscape)
            return false;

        if (!CanUseTargetSkill(TargetSkillType.EmergencyEscape))
            return false;

        if (escapeMotor == null)
            return false;

        if (!escapeMotor.ShouldAutoTriggerEmergencyEscape(healthRatio))
            return false;

        return TryUseEmergencyEscape();
    }

    public virtual bool TryDistortCommandPosition(
        Vector3 originalPosition,
        out Vector3 distortedPosition)
    {
        distortedPosition = originalPosition;

        if (!CanUseTargetSkill(TargetSkillType.CommandDistortion))
            return false;

        return false;
    }

    public virtual bool TryDistortCommandPosition(
        AgentController commandAgent,
        Vector3 originalPosition,
        out Vector3 distortedPosition)
    {
        return TryDistortCommandPosition(originalPosition, out distortedPosition);
    }

    public virtual bool TryUseSkill(TargetSkillType skillType, Vector3 skillPosition)
    {
        return TryUseSkill(skillType, null, skillPosition);
    }

    public virtual bool TryUseSkill(
        TargetSkillType skillType,
        AgentController commandAgent,
        Vector3 skillPosition)
    {
        if (!CanUseTargetSkill(skillType))
            return false;

        switch (skillType)
        {
            case TargetSkillType.Barricade:
                return TryUseBarricade(skillPosition);

            case TargetSkillType.Hologram:
                return TryUseHologram();

            case TargetSkillType.Smoke:
                return TryUseSmoke();

            case TargetSkillType.CommunicationJam:
                return TryUseCommunicationJam();

            case TargetSkillType.CommandDistortion:
                return TryDistortCommandPosition(commandAgent, skillPosition, out _);

            case TargetSkillType.EmergencyEscape:
                return TryUseEmergencyEscape();

            default:
                return false;
        }
    }

    public virtual bool TryUseDefensiveSkill(
        Vector3 escapeDestination,
        bool preferBarricadeFirst = true)
    {
        if (IsTargetUnableToUseSkills())
            return false;

        if (preferBarricadeFirst)
        {
            if (TryUseSkill(TargetSkillType.Barricade, escapeDestination))
                return true;

            if (TryUseSkill(TargetSkillType.Hologram, escapeDestination))
                return true;

            if (TryUseSkill(TargetSkillType.Smoke, escapeDestination))
                return true;
        }
        else
        {
            if (TryUseSkill(TargetSkillType.Hologram, escapeDestination))
                return true;

            if (TryUseSkill(TargetSkillType.Smoke, escapeDestination))
                return true;

            if (TryUseSkill(TargetSkillType.Barricade, escapeDestination))
                return true;
        }

        return false;
    }

    public virtual bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        return TryUseDefensiveSkill(escapeDestination);
    }

    public virtual void ForceClearCurrentHologram()
    {
    }

    public void SetTargetController(TargetController controller)
    {
        targetController = controller;
    }

    public void SetThreatTracker(TargetThreatTracker tracker)
    {
        threatTracker = tracker;
    }

    public void SetEscapeMotor(TargetEscapeMotor motor)
    {
        escapeMotor = motor;
    }

    public void SetCommanderUIController(CommanderUIController uiController)
    {
        commanderUIController = uiController;
    }

    protected virtual void ResolveReferences()
    {
        if (targetController == null)
            targetController = GetComponent<TargetController>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (commanderUIController == null)
            commanderUIController = FindFirstObjectByType<CommanderUIController>();
    }
}