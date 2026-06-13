using System.Collections.Generic;
using UnityEngine;

public static class AgentSkillLoadoutResolver
{
    public const int DefaultMaxBasicSkillCount = 2;
    public const int DefaultMaxTotalSkillCount = 3;

    public sealed class ResolvedSkillSlot
    {
        public SkillDefinitionSO SkillDefinition { get; }
        public UpgradeDefinition UnlockUpgradeDefinition { get; }

        public bool HasSkill => SkillDefinition != null;

        public bool IsBasicSkill
        {
            get
            {
                return SkillDefinition != null && SkillDefinition.IsBasicSkill;
            }
        }

        public bool IsUnlockableSkill
        {
            get
            {
                return SkillDefinition != null && SkillDefinition.IsUnlockableSkill;
            }
        }

        public bool IsUnlockedSkill
        {
            get
            {
                return IsUnlockableSkill && UnlockUpgradeDefinition != null;
            }
        }

        public string SkillId
        {
            get
            {
                return SkillDefinition != null ? SkillDefinition.SkillId : "";
            }
        }

        public string RuntimeSkillKey
        {
            get
            {
                return GetRuntimeSkillKey(SkillDefinition);
            }
        }

        public string DisplayName
        {
            get
            {
                return GetDisplayName(SkillDefinition);
            }
        }

        public string CommandKeyword
        {
            get
            {
                return SkillDefinition != null ? SkillDefinition.CommandKeyword : "";
            }
        }

        public Sprite Icon
        {
            get
            {
                return GetIcon(SkillDefinition, UnlockUpgradeDefinition);
            }
        }

        public bool CanPasteToInput
        {
            get
            {
                return SkillDefinition != null && SkillDefinition.CanPasteToInput;
            }
        }

        public SkillActivationType ActivationType
        {
            get
            {
                return SkillDefinition != null
                    ? SkillDefinition.ActivationType
                    : SkillActivationType.Command;
            }
        }

        public ResolvedSkillSlot(
            SkillDefinitionSO skillDefinition,
            UpgradeDefinition unlockUpgradeDefinition = null)
        {
            SkillDefinition = skillDefinition;
            UnlockUpgradeDefinition = unlockUpgradeDefinition;
        }
    }

    public static List<ResolvedSkillSlot> ResolveVisibleSkills(AgentController agent)
    {
        return ResolveVisibleSkills(
            agent,
            UpgradeManager.Instance,
            DefaultMaxBasicSkillCount,
            true,
            DefaultMaxTotalSkillCount
        );
    }

    public static List<ResolvedSkillSlot> ResolveVisibleSkills(
        AgentController agent,
        int maxBasicSkillCount,
        bool includeUnlockedSkill,
        int maxTotalSkillCount)
    {
        return ResolveVisibleSkills(
            agent,
            UpgradeManager.Instance,
            maxBasicSkillCount,
            includeUnlockedSkill,
            maxTotalSkillCount
        );
    }

    public static List<ResolvedSkillSlot> ResolveVisibleSkills(
        AgentController agent,
        UpgradeManager upgradeManager,
        int maxBasicSkillCount = DefaultMaxBasicSkillCount,
        bool includeUnlockedSkill = true,
        int maxTotalSkillCount = DefaultMaxTotalSkillCount)
    {
        if (agent == null)
            return new List<ResolvedSkillSlot>();

        return ResolveVisibleSkills(
            agent.AgentDefinition,
            upgradeManager,
            maxBasicSkillCount,
            includeUnlockedSkill,
            maxTotalSkillCount
        );
    }

    public static List<ResolvedSkillSlot> ResolveVisibleSkills(
        AgentDefinitionSO agentDefinition,
        UpgradeManager upgradeManager,
        int maxBasicSkillCount = DefaultMaxBasicSkillCount,
        bool includeUnlockedSkill = true,
        int maxTotalSkillCount = DefaultMaxTotalSkillCount)
    {
        List<ResolvedSkillSlot> result = new List<ResolvedSkillSlot>();

        if (agentDefinition == null)
            return result;

        if (maxTotalSkillCount == 0)
            return result;

        AddBasicSkills(
            result,
            agentDefinition.BasicSkills,
            maxBasicSkillCount,
            maxTotalSkillCount
        );

        if (includeUnlockedSkill)
        {
            TryAddUnlockedSkill(
                result,
                agentDefinition,
                upgradeManager,
                maxTotalSkillCount
            );
        }

        return result;
    }

    public static bool TryResolveVisibleSkillAt(
        AgentController agent,
        int index,
        out ResolvedSkillSlot resolvedSkillSlot)
    {
        List<ResolvedSkillSlot> skills = ResolveVisibleSkills(agent);
        return TryGetSkillAt(skills, index, out resolvedSkillSlot);
    }

    public static bool TryResolveVisibleSkillAt(
        AgentController agent,
        int index,
        int maxBasicSkillCount,
        bool includeUnlockedSkill,
        int maxTotalSkillCount,
        out ResolvedSkillSlot resolvedSkillSlot)
    {
        List<ResolvedSkillSlot> skills = ResolveVisibleSkills(
            agent,
            maxBasicSkillCount,
            includeUnlockedSkill,
            maxTotalSkillCount
        );

        return TryGetSkillAt(skills, index, out resolvedSkillSlot);
    }

    public static bool TryGetUnlockedSkill(
        AgentController agent,
        out ResolvedSkillSlot resolvedSkillSlot)
    {
        resolvedSkillSlot = null;

        if (agent == null)
            return false;

        return TryGetUnlockedSkill(
            agent.AgentDefinition,
            UpgradeManager.Instance,
            out resolvedSkillSlot
        );
    }

    public static bool TryGetUnlockedSkill(
        AgentDefinitionSO agentDefinition,
        UpgradeManager upgradeManager,
        out ResolvedSkillSlot resolvedSkillSlot)
    {
        resolvedSkillSlot = null;

        if (agentDefinition == null)
            return false;

        if (upgradeManager == null)
            return false;

        if (!upgradeManager.TryGetUnlockedSkillFromAgentDefinition(
                agentDefinition,
                out SkillDefinitionSO unlockedSkill,
                out UpgradeDefinition unlockedUpgrade))
        {
            return false;
        }

        if (unlockedSkill == null)
            return false;

        resolvedSkillSlot = new ResolvedSkillSlot(unlockedSkill, unlockedUpgrade);
        return true;
    }

    public static string GetRuntimeSkillKey(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skillDefinition.RuntimeSkillKey))
            return skillDefinition.RuntimeSkillKey.Trim();

        if (!string.IsNullOrWhiteSpace(skillDefinition.SkillId))
            return skillDefinition.SkillId.Trim();

        if (!string.IsNullOrWhiteSpace(skillDefinition.CommandKeyword))
            return skillDefinition.CommandKeyword.Trim();

        if (!string.IsNullOrWhiteSpace(skillDefinition.DisplayName))
            return skillDefinition.DisplayName.Trim();

        return skillDefinition.name;
    }

    public static string GetDisplayName(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skillDefinition.DisplayName))
            return skillDefinition.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(skillDefinition.SkillId))
            return skillDefinition.SkillId.Trim();

        return skillDefinition.name;
    }

    public static Sprite GetIcon(
        SkillDefinitionSO skillDefinition,
        UpgradeDefinition unlockUpgradeDefinition)
    {
        if (skillDefinition != null && skillDefinition.Icon != null)
            return skillDefinition.Icon;

        if (unlockUpgradeDefinition != null && unlockUpgradeDefinition.Icon != null)
            return unlockUpgradeDefinition.Icon;

        return null;
    }

    public static bool ContainsSkill(
        IReadOnlyList<ResolvedSkillSlot> resolvedSkills,
        SkillDefinitionSO skillDefinition)
    {
        if (resolvedSkills == null || skillDefinition == null)
            return false;

        for (int i = 0; i < resolvedSkills.Count; i++)
        {
            ResolvedSkillSlot resolvedSkill = resolvedSkills[i];

            if (resolvedSkill == null)
                continue;

            if (IsSameSkill(resolvedSkill.SkillDefinition, skillDefinition))
                return true;
        }

        return false;
    }

    public static bool IsSameSkill(
        SkillDefinitionSO firstSkill,
        SkillDefinitionSO secondSkill)
    {
        if (firstSkill == null || secondSkill == null)
            return false;

        if (firstSkill == secondSkill)
            return true;

        if (!string.IsNullOrWhiteSpace(firstSkill.SkillId) &&
            !string.IsNullOrWhiteSpace(secondSkill.SkillId))
        {
            if (SkillDefinitionSO.NormalizeId(firstSkill.SkillId) ==
                SkillDefinitionSO.NormalizeId(secondSkill.SkillId))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(firstSkill.RuntimeSkillKey) &&
            secondSkill.MatchesRuntimeSkillKey(firstSkill.RuntimeSkillKey))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(secondSkill.RuntimeSkillKey) &&
            firstSkill.MatchesRuntimeSkillKey(secondSkill.RuntimeSkillKey))
        {
            return true;
        }

        return false;
    }

    private static void AddBasicSkills(
        List<ResolvedSkillSlot> result,
        IReadOnlyList<SkillDefinitionSO> basicSkills,
        int maxBasicSkillCount,
        int maxTotalSkillCount)
    {
        if (result == null || basicSkills == null)
            return;

        int addedBasicSkillCount = 0;

        for (int i = 0; i < basicSkills.Count; i++)
        {
            if (IsSlotLimitReached(result, maxTotalSkillCount))
                return;

            if (maxBasicSkillCount >= 0 && addedBasicSkillCount >= maxBasicSkillCount)
                return;

            SkillDefinitionSO skill = basicSkills[i];

            if (skill == null)
                continue;

            bool added = TryAddSkill(
                result,
                new ResolvedSkillSlot(skill),
                maxTotalSkillCount
            );

            if (added)
                addedBasicSkillCount++;
        }
    }

    private static bool TryAddUnlockedSkill(
        List<ResolvedSkillSlot> result,
        AgentDefinitionSO agentDefinition,
        UpgradeManager upgradeManager,
        int maxTotalSkillCount)
    {
        if (result == null || agentDefinition == null)
            return false;

        if (IsSlotLimitReached(result, maxTotalSkillCount))
            return false;

        if (!TryGetUnlockedSkill(
                agentDefinition,
                upgradeManager,
                out ResolvedSkillSlot unlockedSkillSlot))
        {
            return false;
        }

        return TryAddSkill(result, unlockedSkillSlot, maxTotalSkillCount);
    }

    private static bool TryAddSkill(
        List<ResolvedSkillSlot> result,
        ResolvedSkillSlot resolvedSkillSlot,
        int maxTotalSkillCount)
    {
        if (result == null || resolvedSkillSlot == null)
            return false;

        if (!resolvedSkillSlot.HasSkill)
            return false;

        if (IsSlotLimitReached(result, maxTotalSkillCount))
            return false;

        if (ContainsSkill(result, resolvedSkillSlot.SkillDefinition))
            return false;

        result.Add(resolvedSkillSlot);
        return true;
    }

    private static bool TryGetSkillAt(
        IReadOnlyList<ResolvedSkillSlot> skills,
        int index,
        out ResolvedSkillSlot resolvedSkillSlot)
    {
        resolvedSkillSlot = null;

        if (skills == null)
            return false;

        if (index < 0 || index >= skills.Count)
            return false;

        resolvedSkillSlot = skills[index];
        return resolvedSkillSlot != null && resolvedSkillSlot.HasSkill;
    }

    private static bool IsSlotLimitReached(
        IReadOnlyList<ResolvedSkillSlot> result,
        int maxTotalSkillCount)
    {
        if (result == null)
            return true;

        if (maxTotalSkillCount < 0)
            return false;

        return result.Count >= maxTotalSkillCount;
    }
}