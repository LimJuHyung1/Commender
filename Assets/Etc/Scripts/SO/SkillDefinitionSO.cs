using System.Collections.Generic;
using UnityEngine;

public enum SkillActivationType
{
    Command,
    Toggle,
    Passive,
    Auto
}

public enum SkillAvailabilityType
{
    Basic,
    Unlockable
}

[CreateAssetMenu(fileName = "SkillDefinition", menuName = "Commander/Skill/Skill Definition")]
public class SkillDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string skillId;
    [SerializeField] private string runtimeSkillKey;
    [SerializeField] private AgentRole ownerRole = AgentRole.Chaser;
    [SerializeField] private SkillAvailabilityType availabilityType = SkillAvailabilityType.Basic;
    [SerializeField] private int displayOrder;

    [Header("Display")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 5)] private string description;
    [SerializeField, TextArea(1, 3)] private string usageText;
    [SerializeField] private Sprite icon;

    [Header("Command")]
    [SerializeField] private SkillActivationType activationType = SkillActivationType.Command;
    [SerializeField] private string commandKeyword;
    [SerializeField] private List<string> alternativeCommandKeywords = new List<string>();

    [Header("Runtime Alias")]
    [SerializeField] private List<string> runtimeSkillAliases = new List<string>();

    [Header("Unlock")]
    [SerializeField] private string unlockUpgradeId;

    public string SkillId => skillId;
    public string RuntimeSkillKey => runtimeSkillKey;
    public AgentRole OwnerRole => ownerRole;
    public SkillAvailabilityType AvailabilityType => availabilityType;
    public int DisplayOrder => displayOrder;

    public string DisplayName => displayName;
    public string Description => description;
    public string UsageText => usageText;
    public Sprite Icon => icon;

    public SkillActivationType ActivationType => activationType;
    public string UnlockUpgradeId => unlockUpgradeId;

    public IReadOnlyList<string> AlternativeCommandKeywords => alternativeCommandKeywords;
    public IReadOnlyList<string> RuntimeSkillAliases => runtimeSkillAliases;

    public bool IsBasicSkill => availabilityType == SkillAvailabilityType.Basic;
    public bool IsUnlockableSkill => availabilityType == SkillAvailabilityType.Unlockable;

    public bool IsCommandSkill => activationType == SkillActivationType.Command;
    public bool IsToggleSkill => activationType == SkillActivationType.Toggle;
    public bool IsPassiveSkill => activationType == SkillActivationType.Passive;
    public bool IsAutoActivatedSkill => activationType == SkillActivationType.Auto;

    public string CommandKeyword
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(commandKeyword))
                return commandKeyword;

            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return skillId;
        }
    }

    public bool CanPasteToInput
    {
        get
        {
            if (activationType != SkillActivationType.Command &&
                activationType != SkillActivationType.Toggle)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(CommandKeyword);
        }
    }

    public bool HasUnlockUpgradeId
    {
        get
        {
            return !string.IsNullOrWhiteSpace(unlockUpgradeId);
        }
    }

    public bool MatchesSkillId(string targetSkillId)
    {
        if (string.IsNullOrWhiteSpace(targetSkillId))
            return false;

        return NormalizeId(skillId) == NormalizeId(targetSkillId);
    }

    public bool MatchesRuntimeSkillKey(string targetRuntimeSkillKey)
    {
        if (string.IsNullOrWhiteSpace(targetRuntimeSkillKey))
            return false;

        string normalizedTargetKey = NormalizeRuntimeKey(targetRuntimeSkillKey);

        if (NormalizeRuntimeKey(runtimeSkillKey) == normalizedTargetKey)
            return true;

        if (runtimeSkillAliases == null)
            return false;

        for (int i = 0; i < runtimeSkillAliases.Count; i++)
        {
            if (NormalizeRuntimeKey(runtimeSkillAliases[i]) == normalizedTargetKey)
                return true;
        }

        return false;
    }

    public bool MatchesOwnerRole(AgentRole targetRole)
    {
        return ownerRole == targetRole;
    }

    public bool MatchesUnlockUpgradeId(string targetUpgradeId)
    {
        if (string.IsNullOrWhiteSpace(targetUpgradeId))
            return false;

        return NormalizeId(unlockUpgradeId) == NormalizeId(targetUpgradeId);
    }

    public bool HasCommandKeyword(string targetKeyword)
    {
        if (string.IsNullOrWhiteSpace(targetKeyword))
            return false;

        string normalizedTargetKeyword = NormalizeKeyword(targetKeyword);

        if (NormalizeKeyword(CommandKeyword) == normalizedTargetKeyword)
            return true;

        if (alternativeCommandKeywords == null)
            return false;

        for (int i = 0; i < alternativeCommandKeywords.Count; i++)
        {
            if (NormalizeKeyword(alternativeCommandKeywords[i]) == normalizedTargetKeyword)
                return true;
        }

        return false;
    }

    public static string NormalizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeRuntimeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeKeyword(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant();
    }

    private void OnValidate()
    {
        ValidateBasicInfo();
        ValidateCommandInfo();
        ValidateUnlockInfo();
        ValidateStringList(alternativeCommandKeywords, false);
        ValidateStringList(runtimeSkillAliases, true);
    }

    private void ValidateBasicInfo()
    {
        if (string.IsNullOrWhiteSpace(skillId))
            skillId = name;

        skillId = NormalizeId(skillId);

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;

        displayName = displayName.Trim();

        if (string.IsNullOrWhiteSpace(runtimeSkillKey))
            runtimeSkillKey = CreateDefaultRuntimeKey(skillId);

        runtimeSkillKey = NormalizeRuntimeKey(runtimeSkillKey);

        if (displayOrder < 0)
            displayOrder = 0;
    }

    private void ValidateCommandInfo()
    {
        if (string.IsNullOrWhiteSpace(commandKeyword))
        {
            commandKeyword = displayName;
        }
        else
        {
            commandKeyword = commandKeyword.Trim();
        }
    }

    private void ValidateUnlockInfo()
    {
        if (availabilityType == SkillAvailabilityType.Basic)
        {
            unlockUpgradeId = "";
            return;
        }

        unlockUpgradeId = NormalizeId(unlockUpgradeId);
    }

    private void ValidateStringList(List<string> values, bool normalizeAsRuntimeKey)
    {
        if (values == null)
            return;

        for (int i = values.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                values.RemoveAt(i);
                continue;
            }

            values[i] = normalizeAsRuntimeKey
                ? NormalizeRuntimeKey(values[i])
                : values[i].Trim();
        }

        RemoveDuplicateValues(values, normalizeAsRuntimeKey);
    }

    private void RemoveDuplicateValues(List<string> values, bool normalizeAsRuntimeKey)
    {
        if (values == null || values.Count <= 1)
            return;

        HashSet<string> uniqueValues = new HashSet<string>();

        for (int i = values.Count - 1; i >= 0; i--)
        {
            string normalizedValue = normalizeAsRuntimeKey
                ? NormalizeRuntimeKey(values[i])
                : NormalizeKeyword(values[i]);

            if (uniqueValues.Add(normalizedValue))
                continue;

            values.RemoveAt(i);
        }
    }

    private static string CreateDefaultRuntimeKey(string sourceSkillId)
    {
        if (string.IsNullOrWhiteSpace(sourceSkillId))
            return "";

        string normalizedId = NormalizeId(sourceSkillId);

        return normalizedId
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }
}