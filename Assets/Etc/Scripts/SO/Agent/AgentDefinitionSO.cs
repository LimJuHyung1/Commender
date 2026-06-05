using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AgentDefinition", menuName = "Commander/Agent/Agent Definition")]
public class AgentDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string agentId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 5)] private string description;

    [Header("Display")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Color themeColor = Color.white;

    [Header("Runtime")]
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private AgentStatsSO stats;

    [Header("Skills")]
    [SerializeField] private List<SkillDefinitionSO> basicSkills = new List<SkillDefinitionSO>();
    [SerializeField] private List<SkillDefinitionSO> unlockableSkills = new List<SkillDefinitionSO>();

    public string AgentId => agentId;
    public string DisplayName => displayName;
    public string Description => description;

    public Sprite Icon => icon;
    public Color ThemeColor => themeColor;

    public GameObject AgentPrefab => agentPrefab;
    public AgentStatsSO Stats => stats;

    public IReadOnlyList<SkillDefinitionSO> BasicSkills => basicSkills;
    public IReadOnlyList<SkillDefinitionSO> UnlockableSkills => unlockableSkills;

    public bool HasValidId => !string.IsNullOrWhiteSpace(agentId);
    public bool HasStats => stats != null;
    public bool HasPrefab => agentPrefab != null;

    public string GetSafeDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        if (!string.IsNullOrWhiteSpace(agentId))
            return agentId;

        return name;
    }

    public List<SkillDefinitionSO> GetAllSkills()
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        AddSkillsToList(result, basicSkills);
        AddSkillsToList(result, unlockableSkills);

        return result;
    }

    public SkillDefinitionSO GetBasicSkillAt(int index)
    {
        if (basicSkills == null)
            return null;

        if (index < 0 || index >= basicSkills.Count)
            return null;

        return basicSkills[index];
    }

    public SkillDefinitionSO GetUnlockableSkillAt(int index)
    {
        if (unlockableSkills == null)
            return null;

        if (index < 0 || index >= unlockableSkills.Count)
            return null;

        return unlockableSkills[index];
    }

    public bool TryGetSkillById(string skillId, out SkillDefinitionSO skill)
    {
        skill = null;

        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        string normalizedTargetId = NormalizeId(skillId);

        List<SkillDefinitionSO> skills = GetAllSkills();

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinitionSO currentSkill = skills[i];

            if (currentSkill == null)
                continue;

            if (NormalizeId(currentSkill.SkillId) == normalizedTargetId)
            {
                skill = currentSkill;
                return true;
            }
        }

        return false;
    }

    public bool TryGetSkillByRuntimeKey(string runtimeSkillKey, out SkillDefinitionSO skill)
    {
        skill = null;

        if (string.IsNullOrWhiteSpace(runtimeSkillKey))
            return false;

        string normalizedTargetKey = NormalizeRuntimeKey(runtimeSkillKey);

        List<SkillDefinitionSO> skills = GetAllSkills();

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinitionSO currentSkill = skills[i];

            if (currentSkill == null)
                continue;

            if (NormalizeRuntimeKey(currentSkill.RuntimeSkillKey) == normalizedTargetKey)
            {
                skill = currentSkill;
                return true;
            }

            IReadOnlyList<string> aliases = currentSkill.RuntimeSkillAliases;

            if (aliases == null)
                continue;

            for (int j = 0; j < aliases.Count; j++)
            {
                if (NormalizeRuntimeKey(aliases[j]) == normalizedTargetKey)
                {
                    skill = currentSkill;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryGetSkillByCommandKeyword(string commandKeyword, out SkillDefinitionSO skill)
    {
        skill = null;

        if (string.IsNullOrWhiteSpace(commandKeyword))
            return false;

        string normalizedTargetKeyword = NormalizeKeyword(commandKeyword);

        List<SkillDefinitionSO> skills = GetAllSkills();

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinitionSO currentSkill = skills[i];

            if (currentSkill == null)
                continue;

            if (NormalizeKeyword(currentSkill.CommandKeyword) == normalizedTargetKeyword)
            {
                skill = currentSkill;
                return true;
            }

            IReadOnlyList<string> keywords = currentSkill.AlternativeCommandKeywords;

            if (keywords == null)
                continue;

            for (int j = 0; j < keywords.Count; j++)
            {
                if (NormalizeKeyword(keywords[j]) == normalizedTargetKeyword)
                {
                    skill = currentSkill;
                    return true;
                }
            }
        }

        return false;
    }

    public bool HasSkillId(string skillId)
    {
        return TryGetSkillById(skillId, out _);
    }

    public bool HasRuntimeSkillKey(string runtimeSkillKey)
    {
        return TryGetSkillByRuntimeKey(runtimeSkillKey, out _);
    }

    public bool HasCommandKeyword(string commandKeyword)
    {
        return TryGetSkillByCommandKeyword(commandKeyword, out _);
    }

    public string GetPrimaryRuntimeSkillKey(int basicSkillIndex)
    {
        SkillDefinitionSO skill = GetBasicSkillAt(basicSkillIndex);

        if (skill == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skill.RuntimeSkillKey))
            return skill.RuntimeSkillKey;

        return skill.SkillId;
    }

    private static void AddSkillsToList(List<SkillDefinitionSO> target, List<SkillDefinitionSO> source)
    {
        if (target == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            SkillDefinitionSO skill = source[i];

            if (skill == null)
                continue;

            if (target.Contains(skill))
                continue;

            target.Add(skill);
        }
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
        ValidateIdentity();
    }

    private void ValidateIdentity()
    {
        if (string.IsNullOrWhiteSpace(agentId))
            agentId = name;

        agentId = NormalizeId(agentId);

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;

        displayName = displayName.Trim();

        if (description == null)
            description = "";
    }

    private void ValidateSkillList(List<SkillDefinitionSO> skills)
    {
        if (skills == null)
            return;

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            if (skills[i] == null)
                skills.RemoveAt(i);
        }
    }

    private void RemoveDuplicateSkills()
    {
        RemoveDuplicateSkillsInList(basicSkills);
        RemoveDuplicateSkillsInList(unlockableSkills);
        RemoveSkillsAlreadyIncludedInBasicList();
    }

    private void RemoveDuplicateSkillsInList(List<SkillDefinitionSO> skills)
    {
        if (skills == null)
            return;

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            SkillDefinitionSO currentSkill = skills[i];

            if (currentSkill == null)
            {
                skills.RemoveAt(i);
                continue;
            }

            for (int j = 0; j < i; j++)
            {
                if (skills[j] == currentSkill)
                {
                    skills.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void RemoveSkillsAlreadyIncludedInBasicList()
    {
        if (basicSkills == null || unlockableSkills == null)
            return;

        for (int i = unlockableSkills.Count - 1; i >= 0; i--)
        {
            SkillDefinitionSO unlockableSkill = unlockableSkills[i];

            if (unlockableSkill == null)
            {
                unlockableSkills.RemoveAt(i);
                continue;
            }

            if (basicSkills.Contains(unlockableSkill))
                unlockableSkills.RemoveAt(i);
        }
    }
}