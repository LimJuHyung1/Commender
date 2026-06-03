using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Commander/Skill/Skill Database")]
public class SkillDatabaseSO : ScriptableObject
{
    [Header("Agent Skill Sets")]
    [SerializeField] private List<AgentSkillSetSO> agentSkillSets = new List<AgentSkillSetSO>();

    [Header("Additional Skills")]
    [SerializeField] private List<SkillDefinitionSO> additionalSkills = new List<SkillDefinitionSO>();

    private readonly List<SkillDefinitionSO> cachedAllSkills = new List<SkillDefinitionSO>();
    private readonly Dictionary<AgentRole, AgentSkillSetSO> skillSetByRole = new Dictionary<AgentRole, AgentSkillSetSO>();
    private readonly Dictionary<string, SkillDefinitionSO> skillByNormalizedId = new Dictionary<string, SkillDefinitionSO>();
    private readonly Dictionary<string, SkillDefinitionSO> skillByNormalizedRuntimeKey = new Dictionary<string, SkillDefinitionSO>();

    private bool cacheBuilt;

    public IReadOnlyList<AgentSkillSetSO> AgentSkillSets => agentSkillSets;
    public IReadOnlyList<SkillDefinitionSO> AdditionalSkills => additionalSkills;

    public bool TryGetSkillSet(AgentRole role, out AgentSkillSetSO skillSet)
    {
        EnsureCache();

        return skillSetByRole.TryGetValue(role, out skillSet);
    }

    public AgentSkillSetSO GetSkillSetOrNull(AgentRole role)
    {
        return TryGetSkillSet(role, out AgentSkillSetSO skillSet) ? skillSet : null;
    }

    public bool TryGetSkillById(string skillId, out SkillDefinitionSO skillDefinition)
    {
        EnsureCache();

        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        string normalizedId = SkillDefinitionSO.NormalizeId(skillId);

        if (string.IsNullOrWhiteSpace(normalizedId))
            return false;

        return skillByNormalizedId.TryGetValue(normalizedId, out skillDefinition);
    }

    public SkillDefinitionSO GetSkillByIdOrNull(string skillId)
    {
        return TryGetSkillById(skillId, out SkillDefinitionSO skillDefinition) ? skillDefinition : null;
    }

    public bool TryGetSkillByRuntimeKey(string runtimeSkillKey, out SkillDefinitionSO skillDefinition)
    {
        EnsureCache();

        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(runtimeSkillKey))
            return false;

        string normalizedRuntimeKey = SkillDefinitionSO.NormalizeRuntimeKey(runtimeSkillKey);

        if (string.IsNullOrWhiteSpace(normalizedRuntimeKey))
            return false;

        if (skillByNormalizedRuntimeKey.TryGetValue(normalizedRuntimeKey, out skillDefinition))
            return true;

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            SkillDefinitionSO currentSkill = cachedAllSkills[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesRuntimeSkillKey(runtimeSkillKey))
                continue;

            skillDefinition = currentSkill;
            return true;
        }

        return false;
    }

    public SkillDefinitionSO GetSkillByRuntimeKeyOrNull(string runtimeSkillKey)
    {
        return TryGetSkillByRuntimeKey(runtimeSkillKey, out SkillDefinitionSO skillDefinition) ? skillDefinition : null;
    }

    public bool TryGetSkillByCommandKeyword(string commandKeyword, out SkillDefinitionSO skillDefinition)
    {
        EnsureCache();

        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(commandKeyword))
            return false;

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            SkillDefinitionSO currentSkill = cachedAllSkills[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.HasCommandKeyword(commandKeyword))
                continue;

            skillDefinition = currentSkill;
            return true;
        }

        return false;
    }

    public bool TryGetSkillByCommandKeyword(
        AgentRole ownerRole,
        string commandKeyword,
        out SkillDefinitionSO skillDefinition)
    {
        EnsureCache();

        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(commandKeyword))
            return false;

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            SkillDefinitionSO currentSkill = cachedAllSkills[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesOwnerRole(ownerRole))
                continue;

            if (!currentSkill.HasCommandKeyword(commandKeyword))
                continue;

            skillDefinition = currentSkill;
            return true;
        }

        return false;
    }

    public SkillDefinitionSO GetSkillByCommandKeywordOrNull(string commandKeyword)
    {
        return TryGetSkillByCommandKeyword(commandKeyword, out SkillDefinitionSO skillDefinition)
            ? skillDefinition
            : null;
    }

    public SkillDefinitionSO GetSkillByCommandKeywordOrNull(AgentRole ownerRole, string commandKeyword)
    {
        return TryGetSkillByCommandKeyword(ownerRole, commandKeyword, out SkillDefinitionSO skillDefinition)
            ? skillDefinition
            : null;
    }

    public List<SkillDefinitionSO> GetAllSkills()
    {
        EnsureCache();

        return new List<SkillDefinitionSO>(cachedAllSkills);
    }

    public void GetAllSkillsNonAlloc(List<SkillDefinitionSO> result)
    {
        if (result == null)
            return;

        EnsureCache();

        result.Clear();

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            if (cachedAllSkills[i] == null)
                continue;

            result.Add(cachedAllSkills[i]);
        }
    }

    public List<SkillDefinitionSO> GetSkillsByRole(AgentRole role)
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        GetSkillsByRoleNonAlloc(role, result);

        return result;
    }

    public void GetSkillsByRoleNonAlloc(AgentRole role, List<SkillDefinitionSO> result)
    {
        if (result == null)
            return;

        EnsureCache();

        result.Clear();

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            SkillDefinitionSO currentSkill = cachedAllSkills[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesOwnerRole(role))
                continue;

            result.Add(currentSkill);
        }
    }

    public List<SkillDefinitionSO> GetBasicSkillsByRole(AgentRole role)
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        GetBasicSkillsByRoleNonAlloc(role, result);

        return result;
    }

    public void GetBasicSkillsByRoleNonAlloc(AgentRole role, List<SkillDefinitionSO> result)
    {
        if (result == null)
            return;

        EnsureCache();

        result.Clear();

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            SkillDefinitionSO currentSkill = cachedAllSkills[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesOwnerRole(role))
                continue;

            if (!currentSkill.IsBasicSkill)
                continue;

            result.Add(currentSkill);
        }
    }

    public List<SkillDefinitionSO> GetUnlockableSkillsByRole(AgentRole role)
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        GetUnlockableSkillsByRoleNonAlloc(role, result);

        return result;
    }

    public void GetUnlockableSkillsByRoleNonAlloc(AgentRole role, List<SkillDefinitionSO> result)
    {
        if (result == null)
            return;

        EnsureCache();

        result.Clear();

        for (int i = 0; i < cachedAllSkills.Count; i++)
        {
            SkillDefinitionSO currentSkill = cachedAllSkills[i];

            if (currentSkill == null)
                continue;

            if (!currentSkill.MatchesOwnerRole(role))
                continue;

            if (!currentSkill.IsUnlockableSkill)
                continue;

            result.Add(currentSkill);
        }
    }

    public bool HasSkill(string skillId)
    {
        return TryGetSkillById(skillId, out _);
    }

    public bool HasRuntimeSkillKey(string runtimeSkillKey)
    {
        return TryGetSkillByRuntimeKey(runtimeSkillKey, out _);
    }

    public bool IsSkillOwnedByRole(string skillId, AgentRole role)
    {
        if (!TryGetSkillById(skillId, out SkillDefinitionSO skillDefinition))
            return false;

        return skillDefinition.MatchesOwnerRole(role);
    }

    public void RebuildCache()
    {
        BuildCache();
    }

    private void EnsureCache()
    {
        if (cacheBuilt)
            return;

        BuildCache();
    }

    private void BuildCache()
    {
        cacheBuilt = false;

        cachedAllSkills.Clear();
        skillSetByRole.Clear();
        skillByNormalizedId.Clear();
        skillByNormalizedRuntimeKey.Clear();

        RegisterAgentSkillSets();
        RegisterAdditionalSkills();

        SortCachedAllSkills();

        cacheBuilt = true;
    }

    private void RegisterAgentSkillSets()
    {
        if (agentSkillSets == null)
            return;

        for (int i = 0; i < agentSkillSets.Count; i++)
        {
            AgentSkillSetSO skillSet = agentSkillSets[i];

            if (skillSet == null)
                continue;

            RegisterSkillSet(skillSet);
            RegisterSkillsFromSkillSet(skillSet);
        }
    }

    private void RegisterSkillSet(AgentSkillSetSO skillSet)
    {
        if (skillSet == null)
            return;

        AgentRole ownerRole = skillSet.OwnerRole;

        if (skillSetByRole.ContainsKey(ownerRole))
        {
            Debug.LogWarning(
                $"[SkillDatabaseSO] OwnerRole이 중복된 AgentSkillSetSO가 있습니다. Role: {ownerRole}",
                this
            );

            return;
        }

        skillSetByRole.Add(ownerRole, skillSet);
    }

    private void RegisterSkillsFromSkillSet(AgentSkillSetSO skillSet)
    {
        if (skillSet == null)
            return;

        IReadOnlyList<SkillDefinitionSO> skills = skillSet.SkillDefinitions;

        if (skills == null)
            return;

        for (int i = 0; i < skills.Count; i++)
        {
            RegisterSkill(skills[i], skillSet.OwnerRole);
        }
    }

    private void RegisterAdditionalSkills()
    {
        if (additionalSkills == null)
            return;

        for (int i = 0; i < additionalSkills.Count; i++)
        {
            RegisterSkill(additionalSkills[i], null);
        }
    }

    private void RegisterSkill(SkillDefinitionSO skillDefinition, AgentRole? expectedOwnerRole)
    {
        if (skillDefinition == null)
            return;

        if (expectedOwnerRole.HasValue &&
            !skillDefinition.MatchesOwnerRole(expectedOwnerRole.Value))
        {
            Debug.LogWarning(
                $"[SkillDatabaseSO] 스킬 '{skillDefinition.DisplayName}'의 OwnerRole이 SkillSet의 OwnerRole과 다릅니다. " +
                $"SkillSet: {expectedOwnerRole.Value}, Skill: {skillDefinition.OwnerRole}",
                this
            );

            return;
        }

        RegisterSkillToAllSkillList(skillDefinition);
        RegisterSkillId(skillDefinition);
        RegisterRuntimeSkillKey(skillDefinition);
    }

    private void RegisterSkillToAllSkillList(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return;

        if (cachedAllSkills.Contains(skillDefinition))
            return;

        cachedAllSkills.Add(skillDefinition);
    }

    private void RegisterSkillId(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return;

        string normalizedSkillId = SkillDefinitionSO.NormalizeId(skillDefinition.SkillId);

        if (string.IsNullOrWhiteSpace(normalizedSkillId))
            return;

        if (skillByNormalizedId.TryGetValue(normalizedSkillId, out SkillDefinitionSO existingSkill))
        {
            if (existingSkill == skillDefinition)
                return;

            Debug.LogWarning(
                $"[SkillDatabaseSO] SkillId가 중복되었습니다. " +
                $"SkillId: {skillDefinition.SkillId}, Existing: {existingSkill.DisplayName}, Duplicate: {skillDefinition.DisplayName}",
                this
            );

            return;
        }

        skillByNormalizedId.Add(normalizedSkillId, skillDefinition);
    }

    private void RegisterRuntimeSkillKey(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return;

        string normalizedRuntimeKey = SkillDefinitionSO.NormalizeRuntimeKey(skillDefinition.RuntimeSkillKey);

        if (string.IsNullOrWhiteSpace(normalizedRuntimeKey))
            return;

        if (skillByNormalizedRuntimeKey.TryGetValue(normalizedRuntimeKey, out SkillDefinitionSO existingSkill))
        {
            if (existingSkill == skillDefinition)
                return;

            Debug.LogWarning(
                $"[SkillDatabaseSO] RuntimeSkillKey가 중복되었습니다. " +
                $"RuntimeSkillKey: {skillDefinition.RuntimeSkillKey}, Existing: {existingSkill.DisplayName}, Duplicate: {skillDefinition.DisplayName}",
                this
            );

            return;
        }

        skillByNormalizedRuntimeKey.Add(normalizedRuntimeKey, skillDefinition);
    }

    private void SortCachedAllSkills()
    {
        if (cachedAllSkills.Count <= 1)
            return;

        cachedAllSkills.Sort(CompareSkill);
    }

    private int CompareSkill(SkillDefinitionSO left, SkillDefinitionSO right)
    {
        if (left == null && right == null)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int roleCompare = left.OwnerRole.CompareTo(right.OwnerRole);

        if (roleCompare != 0)
            return roleCompare;

        int orderCompare = left.DisplayOrder.CompareTo(right.DisplayOrder);

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal);
    }

    private void OnValidate()
    {
        cacheBuilt = false;

        RemoveNullSkillSets();
        RemoveNullAdditionalSkills();
        ValidateDuplicateSkillSets();
        ValidateDuplicateAdditionalSkills();
    }

    private void RemoveNullSkillSets()
    {
        if (agentSkillSets == null)
            return;

        for (int i = agentSkillSets.Count - 1; i >= 0; i--)
        {
            if (agentSkillSets[i] != null)
                continue;

            agentSkillSets.RemoveAt(i);
        }
    }

    private void RemoveNullAdditionalSkills()
    {
        if (additionalSkills == null)
            return;

        for (int i = additionalSkills.Count - 1; i >= 0; i--)
        {
            if (additionalSkills[i] != null)
                continue;

            additionalSkills.RemoveAt(i);
        }
    }

    private void ValidateDuplicateSkillSets()
    {
        if (agentSkillSets == null)
            return;

        HashSet<AgentRole> registeredRoles = new HashSet<AgentRole>();

        for (int i = 0; i < agentSkillSets.Count; i++)
        {
            AgentSkillSetSO skillSet = agentSkillSets[i];

            if (skillSet == null)
                continue;

            if (registeredRoles.Add(skillSet.OwnerRole))
                continue;

            Debug.LogWarning(
                $"[SkillDatabaseSO] AgentSkillSetSO의 OwnerRole이 중복되었습니다. Role: {skillSet.OwnerRole}",
                this
            );
        }
    }

    private void ValidateDuplicateAdditionalSkills()
    {
        if (additionalSkills == null)
            return;

        HashSet<string> registeredSkillIds = new HashSet<string>();

        for (int i = additionalSkills.Count - 1; i >= 0; i--)
        {
            SkillDefinitionSO skillDefinition = additionalSkills[i];

            if (skillDefinition == null)
                continue;

            string normalizedSkillId = SkillDefinitionSO.NormalizeId(skillDefinition.SkillId);

            if (string.IsNullOrWhiteSpace(normalizedSkillId))
                continue;

            if (registeredSkillIds.Add(normalizedSkillId))
                continue;

            Debug.LogWarning(
                $"[SkillDatabaseSO] Additional Skills에 중복된 SkillId가 있습니다. SkillId: {skillDefinition.SkillId}",
                this
            );

            additionalSkills.RemoveAt(i);
        }
    }
}