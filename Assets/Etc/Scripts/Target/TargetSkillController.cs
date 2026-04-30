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

public class TargetSkillController : MonoBehaviour
{
    [Header("Common References")]
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private TargetEscapeMotor escapeMotor;
    [SerializeField] private CommanderUIController commanderUIController;

    [Header("Debug")]
    [SerializeField] private bool writeLog = false;

    protected TargetThreatTracker ThreatTracker => threatTracker;
    protected NavMeshAgent NavAgent => navAgent;
    protected TargetEscapeMotor EscapeMotor => escapeMotor;
    protected CommanderUIController CommanderUIController => commanderUIController;
    protected bool WriteLog => writeLog;

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
    public virtual int RemainingEmergencyEscapeCount => 0;
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
                "상속 구조에서는 ApplySkillUnlocks(int targetLevel)를 사용하는 것이 좋습니다.");
        }
    }

    public virtual void ResetRuntimeState(
        bool destroyActiveBarricades = true,
        bool destroyActiveHologram = true)
    {
    }

    public virtual bool TryUseBarricade(Vector3 escapeDestination)
    {
        return false;
    }

    public virtual bool TryUseHologram()
    {
        return false;
    }

    public virtual bool TryUseSmoke()
    {
        return false;
    }

    public virtual bool TryUseCommunicationJam()
    {
        return false;
    }

    public virtual bool TryUseCommunicationJamOnCommandSubmission()
    {
        return false;
    }

    public virtual bool TryUseEmergencyEscape()
    {
        return false;
    }

    public virtual bool TryAutoEmergencyEscape(float healthRatio)
    {
        return false;
    }

    public virtual bool TryDistortCommandPosition(
        Vector3 originalPosition,
        out Vector3 distortedPosition)
    {
        distortedPosition = originalPosition;
        return false;
    }

    public virtual bool TryUseSkill(TargetSkillType skillType, Vector3 escapeDestination)
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

            case TargetSkillType.CommandDistortion:
                return TryDistortCommandPosition(escapeDestination, out _);

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
        return false;
    }

    public virtual bool TryUseAutoDefensiveSkill(Vector3 escapeDestination)
    {
        return false;
    }

    public virtual void ForceClearCurrentHologram()
    {
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