using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    [Header("Reward Settings")]
    [SerializeField] private int rewardChoiceCount = 3;

    [Header("Target Milestone Settings")]
    [SerializeField] private int[] targetUpgradeMilestoneStages = { 4, 7, 10 };

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages;

    private readonly List<string> selectedAgentUpgradeIds = new();
    private readonly Dictionary<CommanderTargetType, List<string>> targetUpgradeIdsByType = new();
    private readonly HashSet<int> appliedTargetMilestoneStages = new();

    public UpgradeDatabase UpgradeDatabase => upgradeDatabase;
    public IReadOnlyList<string> SelectedAgentUpgradeIds => selectedAgentUpgradeIds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetRun()
    {
        selectedAgentUpgradeIds.Clear();
        targetUpgradeIdsByType.Clear();
        appliedTargetMilestoneStages.Clear();

        if (logDebugMessages)
        {
            Debug.Log("UpgradeManager run data reset.");
        }
    }

    public List<UpgradeDefinition> BuildAgentRewardChoices(int stageNumber)
    {
        return BuildAgentRewardChoices(stageNumber, rewardChoiceCount);
    }

    public List<UpgradeDefinition> BuildAgentRewardChoices(int stageNumber, int choiceCount)
    {
        List<UpgradeDefinition> result = new();

        if (upgradeDatabase == null)
        {
            Debug.LogWarning("UpgradeDatabase is not assigned.", this);
            return result;
        }

        List<UpgradeDefinition> candidates = upgradeDatabase.GetAgentUpgradeCandidates(stageNumber);
        RemoveUnavailableUpgrades(candidates, selectedAgentUpgradeIds);
        Shuffle(candidates);

        int count = Mathf.Min(choiceCount, candidates.Count);

        for (int i = 0; i < count; i++)
        {
            result.Add(candidates[i]);
        }

        return result;
    }

    public bool SelectAgentUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
        {
            return false;
        }

        if (!upgrade.IsAgentUpgrade)
        {
            Debug.LogWarning($"Upgrade is not an agent upgrade: {upgrade.UpgradeId}", this);
            return false;
        }

        if (!CanAddUpgrade(upgrade, selectedAgentUpgradeIds))
        {
            Debug.LogWarning($"Cannot add agent upgrade: {upgrade.UpgradeId}", this);
            return false;
        }

        selectedAgentUpgradeIds.Add(upgrade.UpgradeId);

        if (logDebugMessages)
        {
            Debug.Log($"Agent upgrade selected: {upgrade.DisplayName}");
        }

        return true;
    }

    public bool SelectAgentUpgrade(string upgradeId)
    {
        if (upgradeDatabase == null)
        {
            Debug.LogWarning("UpgradeDatabase is not assigned.", this);
            return false;
        }

        if (!upgradeDatabase.TryGetUpgrade(upgradeId, out UpgradeDefinition upgrade))
        {
            Debug.LogWarning($"Upgrade not found: {upgradeId}", this);
            return false;
        }

        return SelectAgentUpgrade(upgrade);
    }

    public void TryApplyTargetMilestoneUpgrades(int stageNumber)
    {
        if (!IsTargetMilestoneStage(stageNumber))
        {
            return;
        }

        if (appliedTargetMilestoneStages.Contains(stageNumber))
        {
            return;
        }

        appliedTargetMilestoneStages.Add(stageNumber);

        AddRandomTargetUpgrade(stageNumber, CommanderTargetType.InformationBroker);
        AddRandomTargetUpgrade(stageNumber, CommanderTargetType.ConstructionWorker);
        AddRandomTargetUpgrade(stageNumber, CommanderTargetType.GraffitiArtist);

        if (logDebugMessages)
        {
            Debug.Log($"Target milestone upgrades applied at stage {stageNumber}.");
        }
    }

    public List<UpgradeDefinition> GetSelectedAgentUpgrades()
    {
        if (upgradeDatabase == null)
        {
            return new List<UpgradeDefinition>();
        }

        return upgradeDatabase.GetUpgradesByIds(selectedAgentUpgradeIds);
    }

    public List<UpgradeDefinition> GetTargetUpgrades(CommanderTargetType targetType)
    {
        if (upgradeDatabase == null)
        {
            return new List<UpgradeDefinition>();
        }

        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> upgradeIds))
        {
            return new List<UpgradeDefinition>();
        }

        return upgradeDatabase.GetUpgradesByIds(upgradeIds);
    }

    public IReadOnlyList<string> GetTargetUpgradeIds(CommanderTargetType targetType)
    {
        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> upgradeIds))
        {
            return new List<string>();
        }

        return upgradeIds;
    }

    public int GetAgentUpgradeStackCount(string upgradeId)
    {
        return CountUpgrade(selectedAgentUpgradeIds, upgradeId);
    }

    public int GetTargetUpgradeStackCount(CommanderTargetType targetType, string upgradeId)
    {
        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> upgradeIds))
        {
            return 0;
        }

        return CountUpgrade(upgradeIds, upgradeId);
    }

    public bool HasAppliedTargetMilestone(int stageNumber)
    {
        return appliedTargetMilestoneStages.Contains(stageNumber);
    }

    private void AddRandomTargetUpgrade(int stageNumber, CommanderTargetType targetType)
    {
        if (upgradeDatabase == null)
        {
            Debug.LogWarning("UpgradeDatabase is not assigned.", this);
            return;
        }

        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> currentUpgradeIds))
        {
            currentUpgradeIds = new List<string>();
            targetUpgradeIdsByType.Add(targetType, currentUpgradeIds);
        }

        List<UpgradeDefinition> candidates = upgradeDatabase.GetTargetUpgradeCandidates(stageNumber, targetType);
        RemoveUnavailableUpgrades(candidates, currentUpgradeIds);

        if (candidates.Count <= 0)
        {
            if (logDebugMessages)
            {
                Debug.Log($"No target upgrade candidates for {targetType} at stage {stageNumber}.");
            }

            return;
        }

        int index = Random.Range(0, candidates.Count);
        UpgradeDefinition selectedUpgrade = candidates[index];

        currentUpgradeIds.Add(selectedUpgrade.UpgradeId);

        if (logDebugMessages)
        {
            Debug.Log($"Target upgrade selected: {targetType} / {selectedUpgrade.DisplayName}");
        }
    }

    private bool IsTargetMilestoneStage(int stageNumber)
    {
        if (targetUpgradeMilestoneStages == null)
        {
            return false;
        }

        for (int i = 0; i < targetUpgradeMilestoneStages.Length; i++)
        {
            if (targetUpgradeMilestoneStages[i] == stageNumber)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveUnavailableUpgrades(List<UpgradeDefinition> candidates, List<string> alreadySelectedUpgradeIds)
    {
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            UpgradeDefinition upgrade = candidates[i];

            if (upgrade == null)
            {
                candidates.RemoveAt(i);
                continue;
            }

            if (!CanAddUpgrade(upgrade, alreadySelectedUpgradeIds))
            {
                candidates.RemoveAt(i);
            }
        }
    }

    private bool CanAddUpgrade(UpgradeDefinition upgrade, List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
        {
            return false;
        }

        int currentStack = CountUpgrade(alreadySelectedUpgradeIds, upgrade.UpgradeId);

        if (!upgrade.Stackable && currentStack > 0)
        {
            return false;
        }

        if (upgrade.Stackable && currentStack >= upgrade.MaxStack)
        {
            return false;
        }

        return true;
    }

    private int CountUpgrade(List<string> upgradeIds, string upgradeId)
    {
        if (upgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < upgradeIds.Count; i++)
        {
            if (upgradeIds[i] == upgradeId)
            {
                count++;
            }
        }

        return count;
    }

    private void Shuffle(List<UpgradeDefinition> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            UpgradeDefinition temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}