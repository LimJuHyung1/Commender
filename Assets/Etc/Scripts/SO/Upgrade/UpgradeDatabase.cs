using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeDatabase", menuName = "Commander/Upgrade/Upgrade Database")]
public class UpgradeDatabase : ScriptableObject
{
    [SerializeField] private List<UpgradeDefinition> upgrades = new();

    private Dictionary<string, UpgradeDefinition> upgradeById;

    public IReadOnlyList<UpgradeDefinition> Upgrades => upgrades;

    public bool TryGetUpgrade(string upgradeId, out UpgradeDefinition upgrade)
    {
        EnsureCache();

        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            upgrade = null;
            return false;
        }

        return upgradeById.TryGetValue(upgradeId, out upgrade);
    }

    public UpgradeDefinition GetUpgradeOrNull(string upgradeId)
    {
        return TryGetUpgrade(upgradeId, out UpgradeDefinition upgrade) ? upgrade : null;
    }

    public List<UpgradeDefinition> GetAgentUpgradeCandidates(int stageNumber)
    {
        List<UpgradeDefinition> candidates = new();

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            if (!upgrade.IsAgentUpgrade)
            {
                continue;
            }

            if (!upgrade.CanAppearAtStage(stageNumber))
            {
                continue;
            }

            candidates.Add(upgrade);
        }

        return candidates;
    }

    public List<UpgradeDefinition> GetAgentUpgradeCandidates(int stageNumber, CommanderAgentType agentType)
    {
        List<UpgradeDefinition> candidates = new();

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            if (!upgrade.MatchesAgent(agentType))
            {
                continue;
            }

            if (!upgrade.CanAppearAtStage(stageNumber))
            {
                continue;
            }

            candidates.Add(upgrade);
        }

        return candidates;
    }

    public List<UpgradeDefinition> GetTargetUpgradeCandidates(int stageNumber)
    {
        List<UpgradeDefinition> candidates = new();

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            if (!upgrade.IsTargetUpgrade)
            {
                continue;
            }

            if (!upgrade.CanAppearAtStage(stageNumber))
            {
                continue;
            }

            candidates.Add(upgrade);
        }

        return candidates;
    }

    public List<UpgradeDefinition> GetTargetUpgradeCandidates(int stageNumber, CommanderTargetType targetType)
    {
        List<UpgradeDefinition> candidates = new();

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            if (!upgrade.MatchesTarget(targetType))
            {
                continue;
            }

            if (!upgrade.CanAppearAtStage(stageNumber))
            {
                continue;
            }

            candidates.Add(upgrade);
        }

        return candidates;
    }

    public List<UpgradeDefinition> GetUpgradesByIds(IReadOnlyList<string> upgradeIds)
    {
        EnsureCache();

        List<UpgradeDefinition> result = new();

        if (upgradeIds == null)
        {
            return result;
        }

        for (int i = 0; i < upgradeIds.Count; i++)
        {
            string upgradeId = upgradeIds[i];

            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                continue;
            }

            if (upgradeById.TryGetValue(upgradeId, out UpgradeDefinition upgrade))
            {
                result.Add(upgrade);
            }
        }

        return result;
    }

    public bool ContainsUpgrade(string upgradeId)
    {
        EnsureCache();

        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            return false;
        }

        return upgradeById.ContainsKey(upgradeId);
    }

    private void EnsureCache()
    {
        if (upgradeById != null)
        {
            return;
        }

        BuildCache();
    }

    private void BuildCache()
    {
        upgradeById = new Dictionary<string, UpgradeDefinition>();

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            string upgradeId = upgrade.UpgradeId;

            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                continue;
            }

            if (upgradeById.ContainsKey(upgradeId))
            {
                Debug.LogWarning($"Duplicate upgrade id found: {upgradeId}", this);
                continue;
            }

            upgradeById.Add(upgradeId, upgrade);
        }
    }

    private void OnValidate()
    {
        upgradeById = null;

        HashSet<string> idSet = new();

        for (int i = upgrades.Count - 1; i >= 0; i--)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            string upgradeId = upgrade.UpgradeId;

            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                Debug.LogWarning($"Upgrade at index {i} has an empty id.", this);
                continue;
            }

            if (!idSet.Add(upgradeId))
            {
                Debug.LogWarning($"Duplicate upgrade id in database: {upgradeId}", this);
            }
        }
    }
}