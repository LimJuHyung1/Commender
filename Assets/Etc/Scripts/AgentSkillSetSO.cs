using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AgentSkillSet", menuName = "Commander/Skill/Agent Skill Set")]
public class AgentSkillSetSO : ScriptableObject
{
    [Header("Agent")]
    [SerializeField] private AgentRole ownerRole = AgentRole.Chaser;

    [Header("Skills")]
    [SerializeField] private List<SkillDefinitionSO> skillDefinitions = new List<SkillDefinitionSO>();

    public AgentRole OwnerRole => ownerRole;
    public IReadOnlyList<SkillDefinitionSO> SkillDefinitions => skillDefinitions;

    public int SkillCount
    {
        get
        {
            if (skillDefinitions == null)
                return 0;

            return skillDefinitions.Count;
        }
    }

    public bool HasAnySkill
    {
        get
        {
            return SkillCount > 0;
        }
    }

    public bool TryGetSkillById(string skillId, out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        if (skillDefinitions == null)
            return false;

        for (int i = 0; i < skillDefinitions.Count; i++)
        {
            SkillDefinitionSO currentSkill = skillDefinitions[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesSkillId(skillId))
                continue;

            skillDefinition = currentSkill;
            return true;
        }

        return false;
    }

    public bool TryGetSkillByRuntimeKey(string runtimeSkillKey, out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(runtimeSkillKey))
            return false;

        if (skillDefinitions == null)
            return false;

        for (int i = 0; i < skillDefinitions.Count; i++)
        {
            SkillDefinitionSO currentSkill = skillDefinitions[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesRuntimeSkillKey(runtimeSkillKey))
                continue;

            skillDefinition = currentSkill;
            return true;
        }

        return false;
    }

    public bool TryGetSkillByCommandKeyword(string commandKeyword, out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(commandKeyword))
            return false;

        if (skillDefinitions == null)
            return false;

        for (int i = 0; i < skillDefinitions.Count; i++)
        {
            SkillDefinitionSO currentSkill = skillDefinitions[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.HasCommandKeyword(commandKeyword))
                continue;

            skillDefinition = currentSkill;
            return true;
        }

        return false;
    }

    public bool ContainsSkill(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return false;

        if (skillDefinitions == null)
            return false;

        return skillDefinitions.Contains(skillDefinition);
    }

    public List<SkillDefinitionSO> GetAllSkills()
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        AddSkillsToList(result, SkillFilterType.All);

        return result;
    }

    public List<SkillDefinitionSO> GetBasicSkills()
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        AddSkillsToList(result, SkillFilterType.BasicOnly);

        return result;
    }

    public List<SkillDefinitionSO> GetUnlockableSkills()
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        AddSkillsToList(result, SkillFilterType.UnlockableOnly);

        return result;
    }

    public void GetAllSkillsNonAlloc(List<SkillDefinitionSO> result)
    {
        AddSkillsToList(result, SkillFilterType.All);
    }

    public void GetBasicSkillsNonAlloc(List<SkillDefinitionSO> result)
    {
        AddSkillsToList(result, SkillFilterType.BasicOnly);
    }

    public void GetUnlockableSkillsNonAlloc(List<SkillDefinitionSO> result)
    {
        AddSkillsToList(result, SkillFilterType.UnlockableOnly);
    }

    private void AddSkillsToList(List<SkillDefinitionSO> result, SkillFilterType filterType)
    {
        if (result == null)
            return;

        result.Clear();

        if (skillDefinitions == null)
            return;

        for (int i = 0; i < skillDefinitions.Count; i++)
        {
            SkillDefinitionSO currentSkill = skillDefinitions[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesOwnerRole(ownerRole))
                continue;

            if (filterType == SkillFilterType.BasicOnly && !currentSkill.IsBasicSkill)
                continue;

            if (filterType == SkillFilterType.UnlockableOnly && !currentSkill.IsUnlockableSkill)
                continue;

            result.Add(currentSkill);
        }
    }

    private void OnValidate()
    {
        RemoveNullSkills();
        RemoveDuplicateSkills();
        ValidateOwnerRole();
        SortSkillsByDisplayOrder();
    }

    private void RemoveNullSkills()
    {
        if (skillDefinitions == null)
            return;

        for (int i = skillDefinitions.Count - 1; i >= 0; i--)
        {
            if (skillDefinitions[i] != null)
                continue;

            skillDefinitions.RemoveAt(i);
        }
    }

    private void RemoveDuplicateSkills()
    {
        if (skillDefinitions == null)
            return;

        HashSet<string> skillIds = new HashSet<string>();

        for (int i = skillDefinitions.Count - 1; i >= 0; i--)
        {
            SkillDefinitionSO currentSkill = skillDefinitions[i];

            if (currentSkill == null)
                continue;

            string normalizedSkillId = SkillDefinitionSO.NormalizeId(currentSkill.SkillId);

            if (string.IsNullOrWhiteSpace(normalizedSkillId))
                continue;

            if (skillIds.Add(normalizedSkillId))
                continue;

            skillDefinitions.RemoveAt(i);
        }
    }

    private void ValidateOwnerRole()
    {
        if (skillDefinitions == null)
            return;

        for (int i = 0; i < skillDefinitions.Count; i++)
        {
            SkillDefinitionSO currentSkill = skillDefinitions[i];

            if (currentSkill == null)
                continue;

            if (currentSkill.OwnerRole == ownerRole)
                continue;

            Debug.LogWarning(
                $"[AgentSkillSetSO] {name}에 등록된 스킬 '{currentSkill.DisplayName}'의 OwnerRole이 SkillSet의 OwnerRole과 다릅니다. " +
                $"SkillSet: {ownerRole}, Skill: {currentSkill.OwnerRole}",
                this
            );
        }
    }

    private void SortSkillsByDisplayOrder()
    {
        if (skillDefinitions == null || skillDefinitions.Count <= 1)
            return;

        skillDefinitions.Sort(CompareSkillDisplayOrder);
    }

    private int CompareSkillDisplayOrder(SkillDefinitionSO left, SkillDefinitionSO right)
    {
        if (left == null && right == null)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int orderCompare = left.DisplayOrder.CompareTo(right.DisplayOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal);
    }

    private enum SkillFilterType
    {
        All,
        BasicOnly,
        UnlockableOnly
    }
}