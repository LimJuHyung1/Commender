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

    [Header("Target Info")]
    [SerializeField] private UpgradeOwnerType ownerType;
    [SerializeField] private CommanderAgentType agentType = CommanderAgentType.None;
    [SerializeField] private CommanderTargetType targetType = CommanderTargetType.None;

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
        ValidateTargetInfo();
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

    private void ValidateTargetInfo()
    {
        skillId = NormalizeSkillId(skillId);
        unlockSkillId = NormalizeSkillId(unlockSkillId);

        if (ownerType == UpgradeOwnerType.Agent)
        {
            targetType = CommanderTargetType.None;
        }
        else
        {
            agentType = CommanderAgentType.None;
        }
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