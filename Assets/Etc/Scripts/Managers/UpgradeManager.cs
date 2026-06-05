using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    private const string ChaserUnlockPatrol = "chaser_unlock_patrol";
    private const string ChaserUnlockTrackingInstinct = "chaser_unlock_tracking_instinct";

    private const string ObserverUnlockReconnaissance = "observer_unlock_reconnaissance";
    private const string ObserverUnlockObservationSupport = "observer_unlock_observation_support";

    private const string EngineerUnlockDemolition = "engineer_unlock_demolition";
    private const string EngineerUnlockSafeZone = "engineer_unlock_safe_zone";

    private const string TricksterUnlockVanishing = "trickster_unlock_vanishing";
    private const string TricksterUnlockMisdirection = "trickster_unlock_misdirection";

    [Header("Database")]
    [SerializeField] private UpgradeDatabase upgradeDatabase;

    [Header("Agent Definition Rules")]
    [SerializeField] private List<AgentDefinitionSO> agentDefinitions = new List<AgentDefinitionSO>();

    [Header("Reward Settings")]
    [SerializeField] private int rewardChoiceCount = 3;

    [Header("Target Milestone Settings")]
    [SerializeField] private int[] targetUpgradeMilestoneStages = { 4, 7, 10 };

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages;

    private readonly List<string> selectedAgentUpgradeIds = new List<string>();
    private readonly Dictionary<CommanderTargetType, List<string>> targetUpgradeIdsByType =
        new Dictionary<CommanderTargetType, List<string>>();
    private readonly HashSet<int> appliedTargetMilestoneStages = new HashSet<int>();

    public UpgradeDatabase UpgradeDatabase => upgradeDatabase;
    public IReadOnlyList<string> SelectedAgentUpgradeIds => selectedAgentUpgradeIds;
    public IReadOnlyList<AgentDefinitionSO> AgentDefinitions => agentDefinitions;

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
            Debug.Log("UpgradeManager run data reset.");
    }

    public List<UpgradeDefinition> BuildAgentRewardChoices(int stageNumber)
    {
        return BuildAgentRewardChoices(stageNumber, rewardChoiceCount);
    }

    public List<UpgradeDefinition> BuildAgentRewardChoices(int stageNumber, int choiceCount)
    {
        List<UpgradeDefinition> result = new List<UpgradeDefinition>();

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
            return false;

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
            Debug.Log($"Agent upgrade selected: {upgrade.DisplayName}");

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
            return;

        if (appliedTargetMilestoneStages.Contains(stageNumber))
            return;

        appliedTargetMilestoneStages.Add(stageNumber);

        AddRandomTargetUpgrade(stageNumber, CommanderTargetType.InformationBroker);
        AddRandomTargetUpgrade(stageNumber, CommanderTargetType.ConstructionWorker);
        AddRandomTargetUpgrade(stageNumber, CommanderTargetType.GraffitiArtist);

        if (logDebugMessages)
            Debug.Log($"Target milestone upgrades applied at stage {stageNumber}.");
    }

    public List<UpgradeDefinition> GetSelectedAgentUpgrades()
    {
        if (upgradeDatabase == null)
            return new List<UpgradeDefinition>();

        return upgradeDatabase.GetUpgradesByIds(selectedAgentUpgradeIds);
    }

    public List<UpgradeDefinition> GetTargetUpgrades(CommanderTargetType targetType)
    {
        if (upgradeDatabase == null)
            return new List<UpgradeDefinition>();

        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> upgradeIds))
            return new List<UpgradeDefinition>();

        return upgradeDatabase.GetUpgradesByIds(upgradeIds);
    }

    public IReadOnlyList<string> GetTargetUpgradeIds(CommanderTargetType targetType)
    {
        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> upgradeIds))
            return new List<string>();

        return upgradeIds;
    }

    public int GetAgentUpgradeStackCount(string upgradeId)
    {
        return CountUpgrade(selectedAgentUpgradeIds, upgradeId);
    }

    public int GetTargetUpgradeStackCount(CommanderTargetType targetType, string upgradeId)
    {
        if (!targetUpgradeIdsByType.TryGetValue(targetType, out List<string> upgradeIds))
            return 0;

        return CountUpgrade(upgradeIds, upgradeId);
    }

    public bool HasAppliedTargetMilestone(int stageNumber)
    {
        return appliedTargetMilestoneStages.Contains(stageNumber);
    }

    public bool HasAgentUpgrade(string upgradeId)
    {
        return ContainsUpgrade(selectedAgentUpgradeIds, upgradeId);
    }

    public bool HasUnlockedSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        string normalizedSkillId = NormalizeSkillId(skillId);
        List<UpgradeDefinition> selectedUpgrades = GetSelectedAgentUpgrades();

        for (int i = 0; i < selectedUpgrades.Count; i++)
        {
            UpgradeDefinition upgrade = selectedUpgrades[i];

            if (upgrade == null)
                continue;

            if (!upgrade.IsUnlockSkillUpgrade)
                continue;

            if (NormalizeSkillId(upgrade.UnlockSkillId) == normalizedSkillId)
                return true;
        }

        return false;
    }

    public bool TryGetUnlockedSkillFromAgentDefinition(
        AgentDefinitionSO agentDefinition,
        out SkillDefinitionSO unlockedSkill,
        out UpgradeDefinition unlockedUpgrade)
    {
        unlockedSkill = null;
        unlockedUpgrade = null;

        if (agentDefinition == null)
            return false;

        IReadOnlyList<SkillDefinitionSO> unlockableSkills = agentDefinition.UnlockableSkills;

        if (unlockableSkills == null || unlockableSkills.Count <= 0)
            return false;

        for (int i = 0; i < unlockableSkills.Count; i++)
        {
            SkillDefinitionSO skill = unlockableSkills[i];

            if (skill == null)
                continue;

            if (!skill.HasUnlockUpgradeId)
                continue;

            if (!HasAgentUpgrade(skill.UnlockUpgradeId))
                continue;

            unlockedSkill = skill;

            if (upgradeDatabase != null)
                unlockedUpgrade = upgradeDatabase.GetUpgradeOrNull(skill.UnlockUpgradeId);

            return true;
        }

        return false;
    }

    public UpgradeDefinition GetUnlockedSkillUpgradeFromAgentDefinition(AgentDefinitionSO agentDefinition)
    {
        if (TryGetUnlockedSkillFromAgentDefinition(
                agentDefinition,
                out SkillDefinitionSO _,
                out UpgradeDefinition unlockedUpgrade))
        {
            return unlockedUpgrade;
        }

        return null;
    }

    public Sprite GetUnlockedSkillIconFromAgentDefinition(AgentDefinitionSO agentDefinition)
    {
        if (!TryGetUnlockedSkillFromAgentDefinition(
                agentDefinition,
                out SkillDefinitionSO unlockedSkill,
                out UpgradeDefinition unlockedUpgrade))
        {
            return null;
        }

        if (unlockedSkill != null && unlockedSkill.Icon != null)
            return unlockedSkill.Icon;

        if (unlockedUpgrade != null)
            return unlockedUpgrade.Icon;

        return null;
    }

    public string GetUnlockedSkillRuntimeKeyFromAgentDefinition(AgentDefinitionSO agentDefinition)
    {
        if (!TryGetUnlockedSkillFromAgentDefinition(
                agentDefinition,
                out SkillDefinitionSO unlockedSkill,
                out UpgradeDefinition _))
        {
            return "";
        }

        if (unlockedSkill == null)
            return "";

        if (!string.IsNullOrWhiteSpace(unlockedSkill.RuntimeSkillKey))
            return NormalizeSkillRuntimeKey(unlockedSkill.RuntimeSkillKey);

        if (!string.IsNullOrWhiteSpace(unlockedSkill.SkillId))
            return NormalizeSkillRuntimeKey(unlockedSkill.SkillId);

        if (!string.IsNullOrWhiteSpace(unlockedSkill.CommandKeyword))
            return NormalizeSkillRuntimeKey(unlockedSkill.CommandKeyword);

        return "";
    }

    public string GetUnlockedChaserThirdSkillName()
    {
        return GetLegacyUnlockedSkillName(
            ChaserUnlockPatrol,
            "patrol",
            ChaserUnlockTrackingInstinct,
            "trackinginstinct"
        );
    }

    public UpgradeDefinition GetUnlockedChaserThirdSkillUpgrade()
    {
        return GetLegacyUnlockedSkillUpgrade(ChaserUnlockPatrol, ChaserUnlockTrackingInstinct);
    }

    public Sprite GetUnlockedChaserThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedChaserThirdSkillUpgrade();
        return upgrade != null ? upgrade.Icon : null;
    }

    public string GetUnlockedObserverThirdSkillName()
    {
        return GetLegacyUnlockedSkillName(
            ObserverUnlockReconnaissance,
            "reconnaissance",
            ObserverUnlockObservationSupport,
            "observationsupport"
        );
    }

    public UpgradeDefinition GetUnlockedObserverThirdSkillUpgrade()
    {
        return GetLegacyUnlockedSkillUpgrade(
            ObserverUnlockReconnaissance,
            ObserverUnlockObservationSupport
        );
    }

    public Sprite GetUnlockedObserverThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedObserverThirdSkillUpgrade();
        return upgrade != null ? upgrade.Icon : null;
    }

    public string GetUnlockedEngineerThirdSkillName()
    {
        return GetLegacyUnlockedSkillName(
            EngineerUnlockDemolition,
            "demolition",
            EngineerUnlockSafeZone,
            "safezone"
        );
    }

    public UpgradeDefinition GetUnlockedEngineerThirdSkillUpgrade()
    {
        return GetLegacyUnlockedSkillUpgrade(EngineerUnlockDemolition, EngineerUnlockSafeZone);
    }

    public Sprite GetUnlockedEngineerThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedEngineerThirdSkillUpgrade();
        return upgrade != null ? upgrade.Icon : null;
    }

    public string GetUnlockedTricksterThirdSkillName()
    {
        return GetLegacyUnlockedSkillName(
            TricksterUnlockVanishing,
            "vanishing",
            TricksterUnlockMisdirection,
            "misdirection"
        );
    }

    public UpgradeDefinition GetUnlockedTricksterThirdSkillUpgrade()
    {
        return GetLegacyUnlockedSkillUpgrade(
            TricksterUnlockVanishing,
            TricksterUnlockMisdirection
        );
    }

    public Sprite GetUnlockedTricksterThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedTricksterThirdSkillUpgrade();
        return upgrade != null ? upgrade.Icon : null;
    }

    private string GetLegacyUnlockedSkillName(
        string firstUnlockUpgradeId,
        string firstSkillName,
        string secondUnlockUpgradeId,
        string secondSkillName)
    {
        if (HasAgentUpgrade(firstUnlockUpgradeId))
            return firstSkillName;

        if (HasAgentUpgrade(secondUnlockUpgradeId))
            return secondSkillName;

        return "";
    }

    private UpgradeDefinition GetLegacyUnlockedSkillUpgrade(
        string firstUnlockUpgradeId,
        string secondUnlockUpgradeId)
    {
        if (upgradeDatabase == null)
            return null;

        if (HasAgentUpgrade(firstUnlockUpgradeId))
            return upgradeDatabase.GetUpgradeOrNull(firstUnlockUpgradeId);

        if (HasAgentUpgrade(secondUnlockUpgradeId))
            return upgradeDatabase.GetUpgradeOrNull(secondUnlockUpgradeId);

        return null;
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

        List<UpgradeDefinition> candidates =
            upgradeDatabase.GetTargetUpgradeCandidates(stageNumber, targetType);

        RemoveUnavailableUpgrades(candidates, currentUpgradeIds);

        if (candidates.Count <= 0)
        {
            if (logDebugMessages)
                Debug.Log($"No target upgrade candidates for {targetType} at stage {stageNumber}.");

            return;
        }

        int index = Random.Range(0, candidates.Count);
        UpgradeDefinition selectedUpgrade = candidates[index];

        currentUpgradeIds.Add(selectedUpgrade.UpgradeId);

        if (logDebugMessages)
            Debug.Log($"Target upgrade selected: {targetType} / {selectedUpgrade.DisplayName}");
    }

    private bool IsTargetMilestoneStage(int stageNumber)
    {
        if (targetUpgradeMilestoneStages == null)
            return false;

        for (int i = 0; i < targetUpgradeMilestoneStages.Length; i++)
        {
            if (targetUpgradeMilestoneStages[i] == stageNumber)
                return true;
        }

        return false;
    }

    private void RemoveUnavailableUpgrades(
        List<UpgradeDefinition> candidates,
        List<string> alreadySelectedUpgradeIds)
    {
        if (candidates == null)
            return;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            UpgradeDefinition upgrade = candidates[i];

            if (upgrade == null)
            {
                candidates.RemoveAt(i);
                continue;
            }

            if (!CanAddUpgrade(upgrade, alreadySelectedUpgradeIds))
                candidates.RemoveAt(i);
        }
    }

    private bool CanAddUpgrade(UpgradeDefinition upgrade, List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
            return false;

        if (!CanAddAgentDefinitionBasedUpgrade(upgrade, alreadySelectedUpgradeIds))
            return false;

        int currentStack = CountUpgrade(alreadySelectedUpgradeIds, upgrade.UpgradeId);

        if (!upgrade.Stackable && currentStack > 0)
            return false;

        if (upgrade.Stackable && currentStack >= upgrade.MaxStack)
            return false;

        return true;
    }

    private bool CanAddAgentDefinitionBasedUpgrade(
        UpgradeDefinition upgrade,
        List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
            return false;

        if (!upgrade.IsAgentUpgrade)
            return true;

        if (upgrade.IsUnlockSkillUpgrade)
            return CanAddUnlockSkillUpgrade(upgrade, alreadySelectedUpgradeIds);

        if (upgrade.IsNewSkillUpgrade)
            return CanAddNewSkillUpgrade(upgrade, alreadySelectedUpgradeIds);

        return true;
    }

    private bool CanAddUnlockSkillUpgrade(
        UpgradeDefinition upgrade,
        List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
            return false;

        if (!upgrade.HasUnlockSkillId)
            return true;

        AgentDefinitionSO ownerDefinition =
            FindAgentDefinitionByUnlockableSkillId(upgrade.UnlockSkillId);

        if (ownerDefinition == null)
            return true;

        IReadOnlyList<SkillDefinitionSO> unlockableSkills = ownerDefinition.UnlockableSkills;

        if (unlockableSkills == null || unlockableSkills.Count <= 0)
            return true;

        string candidateUnlockSkillId = NormalizeSkillId(upgrade.UnlockSkillId);

        for (int i = 0; i < unlockableSkills.Count; i++)
        {
            SkillDefinitionSO unlockableSkill = unlockableSkills[i];

            if (unlockableSkill == null)
                continue;

            string existingUnlockSkillId = NormalizeSkillId(unlockableSkill.SkillId);

            if (string.IsNullOrWhiteSpace(existingUnlockSkillId))
                continue;

            if (existingUnlockSkillId == candidateUnlockSkillId)
                continue;

            if (HasSelectedUnlockUpgradeForSkill(existingUnlockSkillId, alreadySelectedUpgradeIds))
                return false;
        }

        return true;
    }

    private bool CanAddNewSkillUpgrade(
        UpgradeDefinition upgrade,
        List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
            return false;

        if (!upgrade.HasTargetSkillId)
            return true;

        string targetSkillId = NormalizeSkillId(upgrade.SkillId);

        AgentDefinitionSO ownerDefinition = FindAgentDefinitionByUnlockableSkillId(targetSkillId);

        if (ownerDefinition == null)
            return true;

        if (!HasSelectedUnlockUpgradeForSkill(targetSkillId, alreadySelectedUpgradeIds))
            return false;

        if (HasSelectedDifferentUnlockableSkillInSameAgent(
                ownerDefinition,
                targetSkillId,
                alreadySelectedUpgradeIds))
        {
            return false;
        }

        return true;
    }

    private bool HasSelectedDifferentUnlockableSkillInSameAgent(
        AgentDefinitionSO agentDefinition,
        string targetSkillId,
        List<string> alreadySelectedUpgradeIds)
    {
        if (agentDefinition == null)
            return false;

        IReadOnlyList<SkillDefinitionSO> unlockableSkills = agentDefinition.UnlockableSkills;

        if (unlockableSkills == null || unlockableSkills.Count <= 0)
            return false;

        string normalizedTargetSkillId = NormalizeSkillId(targetSkillId);

        for (int i = 0; i < unlockableSkills.Count; i++)
        {
            SkillDefinitionSO skill = unlockableSkills[i];

            if (skill == null)
                continue;

            string unlockableSkillId = NormalizeSkillId(skill.SkillId);

            if (string.IsNullOrWhiteSpace(unlockableSkillId))
                continue;

            if (unlockableSkillId == normalizedTargetSkillId)
                continue;

            if (HasSelectedUnlockUpgradeForSkill(unlockableSkillId, alreadySelectedUpgradeIds))
                return true;
        }

        return false;
    }

    private bool HasSelectedUnlockUpgradeForSkill(
        string skillId,
        List<string> alreadySelectedUpgradeIds)
    {
        if (upgradeDatabase == null)
            return false;

        if (alreadySelectedUpgradeIds == null)
            return false;

        string normalizedSkillId = NormalizeSkillId(skillId);

        if (string.IsNullOrWhiteSpace(normalizedSkillId))
            return false;

        for (int i = 0; i < alreadySelectedUpgradeIds.Count; i++)
        {
            string upgradeId = alreadySelectedUpgradeIds[i];

            if (string.IsNullOrWhiteSpace(upgradeId))
                continue;

            if (!upgradeDatabase.TryGetUpgrade(upgradeId, out UpgradeDefinition selectedUpgrade))
                continue;

            if (selectedUpgrade == null)
                continue;

            if (!selectedUpgrade.IsUnlockSkillUpgrade)
                continue;

            if (NormalizeSkillId(selectedUpgrade.UnlockSkillId) == normalizedSkillId)
                return true;
        }

        return false;
    }

    private AgentDefinitionSO FindAgentDefinitionByUnlockableSkillId(string skillId)
    {
        if (agentDefinitions == null)
            return null;

        string normalizedSkillId = NormalizeSkillId(skillId);

        if (string.IsNullOrWhiteSpace(normalizedSkillId))
            return null;

        for (int i = 0; i < agentDefinitions.Count; i++)
        {
            AgentDefinitionSO definition = agentDefinitions[i];

            if (definition == null)
                continue;

            IReadOnlyList<SkillDefinitionSO> unlockableSkills = definition.UnlockableSkills;

            if (unlockableSkills == null)
                continue;

            for (int j = 0; j < unlockableSkills.Count; j++)
            {
                SkillDefinitionSO skill = unlockableSkills[j];

                if (skill == null)
                    continue;

                if (NormalizeSkillId(skill.SkillId) == normalizedSkillId)
                    return definition;
            }
        }

        return null;
    }

    private bool ContainsUpgrade(List<string> upgradeIds, string upgradeId)
    {
        if (upgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
            return false;

        for (int i = 0; i < upgradeIds.Count; i++)
        {
            if (upgradeIds[i] == upgradeId)
                return true;
        }

        return false;
    }

    private int CountUpgrade(List<string> upgradeIds, string upgradeId)
    {
        if (upgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
            return 0;

        int count = 0;

        for (int i = 0; i < upgradeIds.Count; i++)
        {
            if (upgradeIds[i] == upgradeId)
                count++;
        }

        return count;
    }

    private string NormalizeSkillId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant();
    }

    private string NormalizeSkillRuntimeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private void Shuffle(List<UpgradeDefinition> list)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            UpgradeDefinition temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}