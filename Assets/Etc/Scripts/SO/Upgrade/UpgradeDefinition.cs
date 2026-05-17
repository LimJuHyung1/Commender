using UnityEngine;

public enum UpgradeOwnerType
{
    Agent,
    Target
}

public enum CommanderAgentType
{
    None,
    Chaser,
    Observer,
    Engineer,
    Trickster
}

public enum CommanderTargetType
{
    None,
    InformationBroker,
    ConstructionWorker,
    GraffitiArtist
}

public enum UpgradeEffectType
{
    CooldownMultiplier,
    DurationAdd,
    RadiusMultiplier,
    UseCountAdd,
    GaugeCostMultiplier,
    MaxGaugeAdd,
    SpeedMultiplier,
    ViewRadiusMultiplier,
    ViewAngleAdd,
    ValueAdd,
    ValueMultiplier,
    BoolEnable
}

[CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Commander/Upgrade/Upgrade Definition")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string upgradeId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;

    [Header("Target Info")]
    [SerializeField] private UpgradeOwnerType ownerType;
    [SerializeField] private CommanderAgentType agentType = CommanderAgentType.None;
    [SerializeField] private CommanderTargetType targetType = CommanderTargetType.None;
    [SerializeField] private string skillId;

    [Header("Effect Info")]
    [SerializeField] private UpgradeEffectType effectType;
    [SerializeField] private float value;

    [Header("Stage Rule")]
    [SerializeField] private int minStage = 1;

    [Header("Stack Rule")]
    [SerializeField] private bool stackable;
    [SerializeField] private int maxStack = 1;

    public string UpgradeId => upgradeId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    public UpgradeOwnerType OwnerType => ownerType;
    public CommanderAgentType AgentType => agentType;
    public CommanderTargetType TargetType => targetType;
    public string SkillId => skillId;

    public UpgradeEffectType EffectType => effectType;
    public float Value => value;

    public int MinStage => minStage;
    public bool Stackable => stackable;
    public int MaxStack => maxStack;

    public bool IsAgentUpgrade => ownerType == UpgradeOwnerType.Agent;
    public bool IsTargetUpgrade => ownerType == UpgradeOwnerType.Target;

    public bool CanAppearAtStage(int stageNumber)
    {
        return stageNumber >= minStage;
    }

    public bool MatchesAgent(CommanderAgentType type)
    {
        return IsAgentUpgrade && agentType == type;
    }

    public bool MatchesTarget(CommanderTargetType type)
    {
        return IsTargetUpgrade && targetType == type;
    }

    private void OnValidate()
    {
        if (minStage < 1)
        {
            minStage = 1;
        }

        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            upgradeId = name;
        }

        if (!stackable)
        {
            maxStack = 1;
        }

        if (maxStack < 1)
        {
            maxStack = 1;
        }

        if (ownerType == UpgradeOwnerType.Agent)
        {
            targetType = CommanderTargetType.None;
        }
        else
        {
            agentType = CommanderAgentType.None;
        }
    }
}