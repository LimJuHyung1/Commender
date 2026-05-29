using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    private const string ChaserUnlockPatrol = "chaser_unlock_patrol";
    private const string ChaserUnlockTrackingInstinct = "chaser_unlock_tracking_instinct";

    private const string ChaserPatrolPressureTracking = "chaser_patrol_pressure_tracking";
    private const string ChaserPatrolHighSpeed = "chaser_patrol_high_speed";

    private const string ChaserTrackingInstinctMaxStack10 = "chaser_tracking_instinct_max_stack_10";
    private const string ChaserTrackingInstinctInstinctiveCharge = "chaser_tracking_instinct_instinctive_charge";

    private const string ObserverUnlockReconnaissance = "observer_unlock_reconnaissance";
    private const string ObserverUnlockObservationSupport = "observer_unlock_observation_support";

    private const string ObserverReconnaissanceUpgradeModule = "observer_reconnaissance_upgrade_module";
    private const string ObserverReconnaissanceSkilledPilot = "observer_reconnaissance_skilled_pilot";

    private const string ObserverObservationSupportHawkeye = "observer_observation_support_hawkeye";
    private const string ObserverObservationSupportEfficientObservation = "observer_observation_support_efficient_observation";

    private const string EngineerUnlockDemolition = "engineer_unlock_demolition";
    private const string EngineerUnlockSafeZone = "engineer_unlock_safe_zone";

    private const string EngineerDemolitionWideArea = "engineer_demolition_wide_area";
    private const string EngineerDemolitionMulti = "engineer_demolition_multi";

    private const string EngineerSafeZoneExpanded = "engineer_safe_zone_expanded";
    private const string EngineerSafeZoneEmergencyCharge = "engineer_safe_zone_emergency_charge";

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

    public bool HasAgentUpgrade(string upgradeId)
    {
        return ContainsUpgrade(selectedAgentUpgradeIds, upgradeId);
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

    private void RemoveUnavailableUpgrades(
        List<UpgradeDefinition> candidates,
        List<string> alreadySelectedUpgradeIds)
    {
        if (candidates == null)
        {
            return;
        }

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

        if (!CanAddChaserNewSkillUpgrade(upgrade, alreadySelectedUpgradeIds))
        {
            return false;
        }

        if (!CanAddObserverNewSkillUpgrade(upgrade, alreadySelectedUpgradeIds))
        {
            return false;
        }

        if (!CanAddEngineerNewSkillUpgrade(upgrade, alreadySelectedUpgradeIds))
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

    private bool CanAddChaserNewSkillUpgrade(
        UpgradeDefinition upgrade,
        List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
        {
            return false;
        }

        string upgradeId = upgrade.UpgradeId;

        bool hasPatrol = ContainsUpgrade(alreadySelectedUpgradeIds, ChaserUnlockPatrol);
        bool hasTrackingInstinct = ContainsUpgrade(alreadySelectedUpgradeIds, ChaserUnlockTrackingInstinct);

        if (upgradeId == ChaserUnlockPatrol && hasTrackingInstinct)
        {
            return false;
        }

        if (upgradeId == ChaserUnlockTrackingInstinct && hasPatrol)
        {
            return false;
        }

        if (IsPatrolUpgrade(upgradeId) && !hasPatrol)
        {
            return false;
        }

        if (IsTrackingInstinctUpgrade(upgradeId) && !hasTrackingInstinct)
        {
            return false;
        }

        if (IsPatrolUpgrade(upgradeId) && hasTrackingInstinct)
        {
            return false;
        }

        if (IsTrackingInstinctUpgrade(upgradeId) && hasPatrol)
        {
            return false;
        }

        return true;
    }

    private bool CanAddObserverNewSkillUpgrade(
        UpgradeDefinition upgrade,
        List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
        {
            return false;
        }

        string upgradeId = upgrade.UpgradeId;

        bool hasReconnaissance = ContainsUpgrade(
            alreadySelectedUpgradeIds,
            ObserverUnlockReconnaissance
        );

        bool hasObservationSupport = ContainsUpgrade(
            alreadySelectedUpgradeIds,
            ObserverUnlockObservationSupport
        );

        if (upgradeId == ObserverUnlockReconnaissance && hasObservationSupport)
        {
            return false;
        }

        if (upgradeId == ObserverUnlockObservationSupport && hasReconnaissance)
        {
            return false;
        }

        if (IsReconnaissanceUpgrade(upgradeId) && !hasReconnaissance)
        {
            return false;
        }

        if (IsObservationSupportUpgrade(upgradeId) && !hasObservationSupport)
        {
            return false;
        }

        if (IsReconnaissanceUpgrade(upgradeId) && hasObservationSupport)
        {
            return false;
        }

        if (IsObservationSupportUpgrade(upgradeId) && hasReconnaissance)
        {
            return false;
        }

        return true;
    }

    private bool CanAddEngineerNewSkillUpgrade(
    UpgradeDefinition upgrade,
    List<string> alreadySelectedUpgradeIds)
    {
        if (upgrade == null)
        {
            return false;
        }

        string upgradeId = upgrade.UpgradeId;

        bool hasDemolition = ContainsUpgrade(alreadySelectedUpgradeIds, EngineerUnlockDemolition);
        bool hasSafeZone = ContainsUpgrade(alreadySelectedUpgradeIds, EngineerUnlockSafeZone);

        if (upgradeId == EngineerUnlockDemolition && hasSafeZone)
        {
            return false;
        }

        if (upgradeId == EngineerUnlockSafeZone && hasDemolition)
        {
            return false;
        }

        if (IsDemolitionUpgrade(upgradeId) && !hasDemolition)
        {
            return false;
        }

        if (IsSafeZoneUpgrade(upgradeId) && !hasSafeZone)
        {
            return false;
        }

        return true;
    }

    private bool IsPatrolUpgrade(string upgradeId)
    {
        return upgradeId == ChaserPatrolPressureTracking ||
               upgradeId == ChaserPatrolHighSpeed;
    }

    private bool IsTrackingInstinctUpgrade(string upgradeId)
    {
        return upgradeId == ChaserTrackingInstinctMaxStack10 ||
               upgradeId == ChaserTrackingInstinctInstinctiveCharge;
    }

    private bool IsReconnaissanceUpgrade(string upgradeId)
    {
        return upgradeId == ObserverReconnaissanceUpgradeModule ||
               upgradeId == ObserverReconnaissanceSkilledPilot;
    }

    private bool IsObservationSupportUpgrade(string upgradeId)
    {
        return upgradeId == ObserverObservationSupportHawkeye ||
               upgradeId == ObserverObservationSupportEfficientObservation;
    }

    private bool IsDemolitionUpgrade(string upgradeId)
    {
        return upgradeId == EngineerDemolitionWideArea ||
               upgradeId == EngineerDemolitionMulti;
    }

    private bool IsSafeZoneUpgrade(string upgradeId)
    {
        return upgradeId == EngineerSafeZoneExpanded ||
               upgradeId == EngineerSafeZoneEmergencyCharge;
    }

    public string GetUnlockedEngineerThirdSkillName()
    {
        if (HasAgentUpgrade(EngineerUnlockDemolition))
        {
            return "demolition";
        }

        if (HasAgentUpgrade(EngineerUnlockSafeZone))
        {
            return "safezone";
        }

        return "";
    }

    public UpgradeDefinition GetUnlockedEngineerThirdSkillUpgrade()
    {
        if (upgradeDatabase == null)
        {
            return null;
        }

        if (HasAgentUpgrade(EngineerUnlockDemolition))
        {
            return upgradeDatabase.GetUpgradeOrNull(EngineerUnlockDemolition);
        }

        if (HasAgentUpgrade(EngineerUnlockSafeZone))
        {
            return upgradeDatabase.GetUpgradeOrNull(EngineerUnlockSafeZone);
        }

        return null;
    }

    public Sprite GetUnlockedEngineerThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedEngineerThirdSkillUpgrade();

        if (upgrade == null)
        {
            return null;
        }

        return upgrade.Icon;
    }

    private bool ContainsUpgrade(List<string> upgradeIds, string upgradeId)
    {
        if (upgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
        {
            return false;
        }

        for (int i = 0; i < upgradeIds.Count; i++)
        {
            if (upgradeIds[i] == upgradeId)
            {
                return true;
            }
        }

        return false;
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

    public string GetUnlockedChaserThirdSkillName()
    {
        if (HasAgentUpgrade(ChaserUnlockPatrol))
        {
            return "patrol";
        }

        if (HasAgentUpgrade(ChaserUnlockTrackingInstinct))
        {
            return "trackinginstinct";
        }

        return "";
    }

    public UpgradeDefinition GetUnlockedChaserThirdSkillUpgrade()
    {
        if (upgradeDatabase == null)
        {
            return null;
        }

        if (HasAgentUpgrade(ChaserUnlockPatrol))
        {
            return upgradeDatabase.GetUpgradeOrNull(ChaserUnlockPatrol);
        }

        if (HasAgentUpgrade(ChaserUnlockTrackingInstinct))
        {
            return upgradeDatabase.GetUpgradeOrNull(ChaserUnlockTrackingInstinct);
        }

        return null;
    }

    public Sprite GetUnlockedChaserThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedChaserThirdSkillUpgrade();

        if (upgrade == null)
        {
            return null;
        }

        return upgrade.Icon;
    }

    public string GetUnlockedObserverThirdSkillName()
    {
        if (HasAgentUpgrade(ObserverUnlockReconnaissance))
        {
            return "reconnaissance";
        }

        if (HasAgentUpgrade(ObserverUnlockObservationSupport))
        {
            return "observationsupport";
        }

        return "";
    }

    public UpgradeDefinition GetUnlockedObserverThirdSkillUpgrade()
    {
        if (upgradeDatabase == null)
        {
            return null;
        }

        if (HasAgentUpgrade(ObserverUnlockReconnaissance))
        {
            return upgradeDatabase.GetUpgradeOrNull(ObserverUnlockReconnaissance);
        }

        if (HasAgentUpgrade(ObserverUnlockObservationSupport))
        {
            return upgradeDatabase.GetUpgradeOrNull(ObserverUnlockObservationSupport);
        }

        return null;
    }

    public Sprite GetUnlockedObserverThirdSkillIcon()
    {
        UpgradeDefinition upgrade = GetUnlockedObserverThirdSkillUpgrade();

        if (upgrade == null)
        {
            return null;
        }

        return upgrade.Icon;
    }
}