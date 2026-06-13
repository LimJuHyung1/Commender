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
    Trickster,
    Profiler,
    DogHandler
}

public enum CommanderTargetType
{
    None,
    InformationBroker,
    ConstructionWorker,
    GraffitiArtist
}

public enum UpgradeCategory
{
    BasicSkillUpgrade,
    UnlockSkill,
    NewSkillUpgrade,
    TargetUpgrade,
    Other
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

    [Header("Category")]
    [SerializeField] private UpgradeCategory upgradeCategory = UpgradeCategory.BasicSkillUpgrade;

    [Header("Owner Info")]
    [SerializeField] private UpgradeOwnerType ownerType;

    [Tooltip("새 에이전트 구조에서 사용하는 에이전트 ID입니다. 예: security_officer, drone_pilot, safety_manager, magician")]
    [SerializeField] private string agentId;

    [SerializeField] private CommanderTargetType targetType = CommanderTargetType.None;

    [Header("Legacy Agent Info")]
    [Tooltip("기존 4직군 구조와의 호환용 필드입니다. 새 에이전트 구조에서는 Agent Id를 우선 사용하세요.")]
    [SerializeField] private CommanderAgentType agentType = CommanderAgentType.None;

    [Header("Skill Info")]
    [Tooltip("강화 대상 스킬 ID입니다. 예: access_control, escape_block, patrol")]
    [SerializeField] private string skillId;

    [Tooltip("신규 스킬 해금 강화일 때 해금되는 스킬 ID입니다. 예: patrol, tracking_instinct")]
    [SerializeField] private string unlockSkillId;

    [Header("Effect Info")]
    [SerializeField] private UpgradeEffectType effectType;
    [SerializeField] private float value;

    [Tooltip("효과를 더 구체적으로 구분하기 위한 문자열 키입니다. 예: AccessControlSpeedBonusMultiplier")]
    [SerializeField] private string effectKey;

    [Tooltip("보조 수치가 필요한 강화에 사용합니다. 예: 체력 소모 배율, 추가 배율")]
    [SerializeField] private float secondaryValue;

    [Tooltip("지속 시간이 필요한 강화에 사용합니다. 예: 3초, 4초")]
    [SerializeField] private float durationValue;

    [Tooltip("횟수나 개수 정보가 필요한 강화에 사용합니다. 예: 스테이지당 1회")]
    [SerializeField] private int countValue;

    [SerializeField, TextArea(1, 3)] private string effectSummary;

    [Header("Stage Rule")]
    [SerializeField] private int minStage = 1;

    [Header("Stack Rule")]
    [SerializeField] private bool stackable;
    [SerializeField] private int maxStack = 1;

    [Header("Skill Description UI")]
    [SerializeField] private bool showInSkillDescription = true;

    public string UpgradeId => upgradeId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    public UpgradeCategory UpgradeCategory => upgradeCategory;

    public UpgradeOwnerType OwnerType => ownerType;

    public string AgentId => agentId;
    public CommanderAgentType AgentType => agentType;
    public CommanderTargetType TargetType => targetType;

    public string SkillId => skillId;
    public string TargetSkillId => skillId;
    public string UnlockSkillId => unlockSkillId;

    public UpgradeEffectType EffectType => effectType;
    public float Value => value;
    public string EffectKey => GetEffectKey();
    public float SecondaryValue => secondaryValue;
    public float DurationValue => durationValue;
    public int CountValue => countValue;
    public string EffectSummary => GetEffectSummary();

    public int MinStage => minStage;
    public bool Stackable => stackable;
    public int MaxStack => maxStack;

    public bool ShowInSkillDescription => showInSkillDescription;

    public bool IsAgentUpgrade => ownerType == UpgradeOwnerType.Agent;
    public bool IsTargetUpgrade => ownerType == UpgradeOwnerType.Target;

    public bool IsBasicSkillUpgrade => upgradeCategory == UpgradeCategory.BasicSkillUpgrade;
    public bool IsUnlockSkillUpgrade => upgradeCategory == UpgradeCategory.UnlockSkill;
    public bool IsNewSkillUpgrade => upgradeCategory == UpgradeCategory.NewSkillUpgrade;
    public bool IsTargetSkillUpgrade => upgradeCategory == UpgradeCategory.TargetUpgrade;

    public bool HasAgentId
    {
        get
        {
            return !string.IsNullOrWhiteSpace(agentId);
        }
    }

    public bool HasTargetSkillId
    {
        get
        {
            return !string.IsNullOrWhiteSpace(skillId);
        }
    }

    public bool HasUnlockSkillId
    {
        get
        {
            return !string.IsNullOrWhiteSpace(unlockSkillId);
        }
    }

    public bool HasEffectKey
    {
        get
        {
            return !string.IsNullOrWhiteSpace(effectKey);
        }
    }

    public bool HasEffectSummary
    {
        get
        {
            return !string.IsNullOrWhiteSpace(effectSummary);
        }
    }

    public bool CanAppearAtStage(int stageNumber)
    {
        return stageNumber >= minStage;
    }

    public bool MatchesAgentDefinition(AgentDefinitionSO agentDefinition)
    {
        if (agentDefinition == null)
            return false;

        return MatchesAgentId(agentDefinition.AgentId);
    }

    public bool MatchesAgentId(string targetAgentId)
    {
        if (!IsAgentUpgrade)
            return false;

        if (string.IsNullOrWhiteSpace(targetAgentId))
            return false;

        if (!string.IsNullOrWhiteSpace(agentId))
            return NormalizeAgentId(agentId) == NormalizeAgentId(targetAgentId);

        CommanderAgentType legacyType = ConvertAgentIdToLegacyAgentType(targetAgentId);

        if (legacyType == CommanderAgentType.None)
            return false;

        return MatchesAgent(legacyType);
    }

    public bool MatchesAgent(CommanderAgentType type)
    {
        return IsAgentUpgrade && agentType == type;
    }

    public bool MatchesTarget(CommanderTargetType type)
    {
        return IsTargetUpgrade && targetType == type;
    }

    public bool MatchesSkillId(string targetSkillId)
    {
        return MatchesTargetSkillId(targetSkillId);
    }

    public bool MatchesTargetSkillId(string targetSkillId)
    {
        if (string.IsNullOrWhiteSpace(targetSkillId))
            return false;

        return NormalizeSkillId(skillId) == NormalizeSkillId(targetSkillId);
    }

    public bool MatchesUnlockSkillId(string targetUnlockSkillId)
    {
        if (string.IsNullOrWhiteSpace(targetUnlockSkillId))
            return false;

        return NormalizeSkillId(unlockSkillId) == NormalizeSkillId(targetUnlockSkillId);
    }

    public bool IsRelatedToSkill(string targetSkillId)
    {
        if (string.IsNullOrWhiteSpace(targetSkillId))
            return false;

        return MatchesTargetSkillId(targetSkillId) || MatchesUnlockSkillId(targetSkillId);
    }

    public bool MatchesEffectKey(string targetEffectKey)
    {
        if (string.IsNullOrWhiteSpace(targetEffectKey))
            return false;

        return NormalizeEffectKey(effectKey) == NormalizeEffectKey(targetEffectKey);
    }

    public bool IsRelatedToAgentDefinition(AgentDefinitionSO agentDefinition)
    {
        if (agentDefinition == null)
            return false;

        if (!IsAgentUpgrade)
            return false;

        if (MatchesAgentDefinition(agentDefinition))
            return true;

        if (agentDefinition.HasSkillId(skillId))
            return true;

        if (agentDefinition.HasSkillId(unlockSkillId))
            return true;

        return false;
    }

    public bool IsRelatedToAgentSkillSet(AgentDefinitionSO agentDefinition)
    {
        return IsRelatedToAgentDefinition(agentDefinition);
    }

    public static string NormalizeAgentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
    }

    public static string NormalizeSkillId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeEffectKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim();
    }

    public static CommanderAgentType ConvertAgentIdToLegacyAgentType(string targetAgentId)
    {
        string normalizedAgentId = NormalizeAgentId(targetAgentId);

        switch (normalizedAgentId)
        {
            case "security_officer":
            case "chaser":
                return CommanderAgentType.Chaser;

            case "drone_pilot":
            case "observer":
                return CommanderAgentType.Observer;

            case "safety_manager":
            case "engineer":
                return CommanderAgentType.Engineer;

            case "magician":
            case "trickster":
                return CommanderAgentType.Trickster;

            case "profiler":
                return CommanderAgentType.Profiler;

            case "dog_handler":
            case "doghandler":
            case "detection_dog_handler":
                return CommanderAgentType.DogHandler;
        }

        return CommanderAgentType.None;
    }

    public static string ConvertLegacyAgentTypeToAgentId(CommanderAgentType legacyAgentType)
    {
        switch (legacyAgentType)
        {
            case CommanderAgentType.Chaser:
                return "security_officer";

            case CommanderAgentType.Observer:
                return "drone_pilot";

            case CommanderAgentType.Engineer:
                return "safety_manager";

            case CommanderAgentType.Trickster:
                return "magician";

            case CommanderAgentType.Profiler:
                return "profiler";

            case CommanderAgentType.DogHandler:
                return "dog_handler";
        }

        return "";
    }

    private string GetEffectKey()
    {
        if (!string.IsNullOrWhiteSpace(effectKey))
            return effectKey.Trim();

        return effectType.ToString();
    }

    private string GetEffectSummary()
    {
        if (!string.IsNullOrWhiteSpace(effectSummary))
            return effectSummary.Trim();

        return "";
    }

    private void OnValidate()
    {
        ValidateBasicInfo();
        ValidateOwnerInfo();
        ValidateSkillInfo();
        ValidateEffectInfo();
        ValidateStageRule();
        ValidateStackRule();
        ValidateCategory();
    }

    private void ValidateBasicInfo()
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            upgradeId = name;

        upgradeId = upgradeId.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;

        displayName = displayName.Trim();

        if (!string.IsNullOrWhiteSpace(description))
            description = description.Trim();
    }

    private void ValidateOwnerInfo()
    {
        agentId = NormalizeAgentId(agentId);

        if (ownerType == UpgradeOwnerType.Agent)
        {
            targetType = CommanderTargetType.None;

            if (string.IsNullOrWhiteSpace(agentId) && agentType != CommanderAgentType.None)
                agentId = ConvertLegacyAgentTypeToAgentId(agentType);

            return;
        }

        agentId = "";
        agentType = CommanderAgentType.None;
    }

    private void ValidateSkillInfo()
    {
        skillId = NormalizeSkillId(skillId);
        unlockSkillId = NormalizeSkillId(unlockSkillId);
    }

    private void ValidateEffectInfo()
    {
        if (!string.IsNullOrWhiteSpace(effectKey))
            effectKey = effectKey.Trim();

        if (!string.IsNullOrWhiteSpace(effectSummary))
            effectSummary = effectSummary.Trim();

        if (durationValue < 0f)
            durationValue = 0f;

        if (countValue < 0)
            countValue = 0;
    }

    private void ValidateStageRule()
    {
        if (minStage < 1)
            minStage = 1;
    }

    private void ValidateStackRule()
    {
        if (!stackable)
            maxStack = 1;

        if (maxStack < 1)
            maxStack = 1;
    }

    private void ValidateCategory()
    {
        if (upgradeCategory == UpgradeCategory.UnlockSkill)
        {
            if (string.IsNullOrWhiteSpace(unlockSkillId))
            {
                Debug.LogWarning(
                    $"[UpgradeDefinition] 신규 스킬 해금 강화 '{displayName}'에 UnlockSkillId가 비어 있습니다.",
                    this
                );
            }

            return;
        }

        if (upgradeCategory == UpgradeCategory.BasicSkillUpgrade ||
            upgradeCategory == UpgradeCategory.NewSkillUpgrade)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                Debug.LogWarning(
                    $"[UpgradeDefinition] 스킬 강화 '{displayName}'에 Target SkillId가 비어 있습니다.",
                    this
                );
            }
        }
    }
}