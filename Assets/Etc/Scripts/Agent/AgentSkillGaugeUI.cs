using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AgentSkillGaugeUI : MonoBehaviour
{
    private const string SkillDash = "dash";
    private const string SkillSmoke = "smoke";

    private const string SkillAccessControl = "accesscontrol";
    private const string SkillEscapeBlock = "escapeblock";
    private const string SkillPatrol = "patrol";
    private const string SkillTrackingInstinct = "trackinginstinct";

    private const string SkillDrone = "drone";
    private const string SkillReconnaissance = "reconnaissance";
    private const string SkillObservationSupport = "observationsupport";
    private const string SkillPositionShare = "positionshare";
    private const string SkillPositionShareOn = "positionshare_on";
    private const string SkillPositionShareOff = "positionshare_off";

    private const string SkillBarricade = "barricade";
    private const string SkillStopSignal = "stopsignal";
    private const string SkillSlowTrap = "slowtrap";
    private const string SkillDemolition = "demolition";
    private const string SkillSafeZone = "safezone";

    private const string SkillFakeBox = "fakebox";
    private const string SkillJokerCard = "jokercard";
    private const string SkillVanishing = "vanishing";
    private const string SkillMisdirection = "misdirection";

    private const string LegacySkillNoisemaker = "noisemaker";
    private const string LegacySkillHologram = "hologram";

    private const string ChaserUnlockPatrol = "chaser_unlock_patrol";
    private const string ChaserUnlockTrackingInstinct = "chaser_unlock_tracking_instinct";

    private const string ObserverUnlockReconnaissance = "observer_unlock_reconnaissance";
    private const string ObserverUnlockObservationSupport = "observer_unlock_observation_support";

    private const string EngineerUnlockDemolition = "engineer_unlock_demolition";
    private const string EngineerUnlockSafeZone = "engineer_unlock_safe_zone";

    private const string TricksterUnlockVanishing = "trickster_unlock_vanishing";
    private const string TricksterUnlockMisdirection = "trickster_unlock_misdirection";

    private enum GaugeFillDirection
    {
        HorizontalLeft,
        HorizontalRight,
        VerticalBottom,
        VerticalTop
    }

    [Header("Target")]
    [SerializeField] private AgentController targetAgent;
    [SerializeField] private int agentId = -1;
    [SerializeField] private bool autoBindByAgentId = true;

    [Header("Skill Slots")]
    [SerializeField] private AgentSkillSlotUI skill1Slot;
    [SerializeField] private AgentSkillSlotUI skill2Slot;
    [SerializeField] private AgentSkillSlotUI skill3Slot;
    [SerializeField] private bool autoFindSlotsByComponent = true;

    [Header("Skill Names")]
    [SerializeField] private string skill1Name = "";
    [SerializeField] private string skill2Name = "";
    [SerializeField] private string skill3Name = "";

    [Header("Gauge Fill Setting")]
    [SerializeField] private bool setupImageFillSetting = true;
    [SerializeField] private GaugeFillDirection fillDirection = GaugeFillDirection.VerticalBottom;

    [Header("Toggle Skill Gauge")]
    [SerializeField] private float toggleSkillOnFillAmount = 1f;
    [SerializeField] private float toggleSkillOffFillAmount = 0f;

    [Header("Gauge Info Label")]
    [SerializeField] private bool showGaugeInfoOnSkillClick = true;
    [SerializeField] private float gaugeInfoLabelDuration = 1.5f;
    [SerializeField] private Vector2 gaugeInfoLabelOffset = new Vector2(16f, -24f);
    [SerializeField] private Vector2 gaugeInfoLabelSize = new Vector2(180f, 32f);

    [Header("Skill Name Paste")]
    [SerializeField] private CommanderUIController commanderUIController;
    [SerializeField] private bool pasteSkillNameOnFunctionKeyClick = true;
    [SerializeField] private bool pasteOnlyWhenFunctionKeyInputMatchesClickedAgent = true;
    [SerializeField] private bool pasteSkillNameOnDoubleClick = true;
    [SerializeField] private float doubleClickMaxInterval = 0.3f;
    [SerializeField] private bool showGaugeInfoAfterSkillPaste = false;
    [SerializeField] private bool autoFindCommanderUIController = true;

    [Header("Skill Description Popup")]
    [SerializeField] private SkillDescriptionPopupUI skillDescriptionPopupUI;
    [SerializeField] private SkillDatabaseSO skillDatabase;
    [SerializeField] private bool showSkillDescriptionOnRightClick = true;
    [SerializeField] private bool autoFindSkillDescriptionPopupUI = true;

    private AgentSkillSlotUI lastClickedSkillSlot;
    private float lastSkillClickTime = -999f;

    private string gaugeInfoLabelText = "";
    private Vector2 gaugeInfoLabelScreenPosition;
    private float gaugeInfoLabelEndTime = -1f;
    private GUIStyle gaugeInfoLabelStyle;

    public AgentController TargetAgent => targetAgent;
    public int AgentId => agentId;

    public string Skill1Name => skill1Name;
    public string Skill2Name => skill2Name;
    public string Skill3Name => skill3Name;

    public bool CanUseSkill1 => CanUseSkill(skill1Name);
    public bool CanUseSkill2 => CanUseSkill(skill2Name);
    public bool CanUseSkill3 => CanUseSkill(skill3Name);

    private void Awake()
    {
        CacheSlotsByComponent();
        TryCacheCommanderUIController();
        ConfigureSlotGauges();

        if (skill3Slot != null)
        {
            skill3Slot.ClearIcon();
            skill3Slot.SetGaugeAmount(0f);
            skill3Slot.SetVisible(false);
        }

        Refresh();
    }

    private void Start()
    {
        if (targetAgent == null && autoBindByAgentId)
            TryCacheTargetAgent();

        if (commanderUIController == null && autoFindCommanderUIController)
            TryCacheCommanderUIController();

        Refresh();
    }

    private void Update()
    {
        if (targetAgent == null && autoBindByAgentId && agentId >= 0)
            TryCacheTargetAgent();

        if (commanderUIController == null && autoFindCommanderUIController)
            TryCacheCommanderUIController();

        Refresh();
        HandleSkillGaugeInfoClick();
        HandleSkillDescriptionRightClick();
    }

    private void OnValidate()
    {
        gaugeInfoLabelDuration = Mathf.Max(0f, gaugeInfoLabelDuration);
        gaugeInfoLabelSize.x = Mathf.Max(1f, gaugeInfoLabelSize.x);
        gaugeInfoLabelSize.y = Mathf.Max(1f, gaugeInfoLabelSize.y);

        doubleClickMaxInterval = Mathf.Max(0.05f, doubleClickMaxInterval);

        toggleSkillOnFillAmount = Mathf.Clamp01(toggleSkillOnFillAmount);
        toggleSkillOffFillAmount = Mathf.Clamp01(toggleSkillOffFillAmount);

        skill1Name = NormalizeSkillName(skill1Name);
        skill2Name = NormalizeSkillName(skill2Name);
        skill3Name = NormalizeSkillName(skill3Name);

        CacheSlotsByComponent();
        ConfigureSlotGauges();
    }

    public void Bind(AgentController agent)
    {
        if (agent == null)
        {
            Unbind();
            return;
        }

        SetupSkillNamesByAgent(agent);
        Bind(agent, skill1Name, skill2Name, skill3Name);
    }

    public void Bind(AgentController agent, string firstSkillName, string secondSkillName)
    {
        Bind(agent, firstSkillName, secondSkillName, "");
    }

    public void Bind(AgentController agent, string firstSkillName, string secondSkillName, string thirdSkillName)
    {
        targetAgent = agent;
        agentId = agent != null ? agent.AgentID : -1;

        skill1Name = NormalizeSkillName(firstSkillName);
        skill2Name = NormalizeSkillName(secondSkillName);
        skill3Name = NormalizeSkillName(thirdSkillName);

        if (targetAgent != null &&
            string.IsNullOrWhiteSpace(skill1Name) &&
            string.IsNullOrWhiteSpace(skill2Name) &&
            string.IsNullOrWhiteSpace(skill3Name))
        {
            SetupSkillNamesByAgent(targetAgent);
        }

        CacheSlotsByComponent();
        ConfigureSlotGauges();
        Refresh();
    }

    public void BindAgentController(AgentController agent)
    {
        Bind(agent);
    }

    public void BindAgentObject(GameObject agentObject)
    {
        if (agentObject == null)
        {
            Unbind();
            return;
        }

        AgentController agent = agentObject.GetComponent<AgentController>();

        if (agent == null)
            agent = agentObject.GetComponentInChildren<AgentController>(true);

        Bind(agent);
    }

    public void ClearAgentUI()
    {
        Unbind();
    }

    public void BindByAgentId(int id, string firstSkillName, string secondSkillName)
    {
        BindByAgentId(id, firstSkillName, secondSkillName, "");
    }

    public void BindByAgentId(int id, string firstSkillName, string secondSkillName, string thirdSkillName)
    {
        agentId = id;
        targetAgent = null;

        skill1Name = NormalizeSkillName(firstSkillName);
        skill2Name = NormalizeSkillName(secondSkillName);
        skill3Name = NormalizeSkillName(thirdSkillName);

        if (autoBindByAgentId)
            TryCacheTargetAgent();

        if (targetAgent != null &&
            string.IsNullOrWhiteSpace(skill1Name) &&
            string.IsNullOrWhiteSpace(skill2Name) &&
            string.IsNullOrWhiteSpace(skill3Name))
        {
            SetupSkillNamesByAgent(targetAgent);
        }

        CacheSlotsByComponent();
        ConfigureSlotGauges();
        Refresh();
    }

    public void Unbind()
    {
        targetAgent = null;
        agentId = -1;

        skill1Name = "";
        skill2Name = "";
        skill3Name = "";

        SetSlotGaugeAmount(skill1Slot, 0f);
        SetSlotGaugeAmount(skill2Slot, 0f);
        SetSlotGaugeAmount(skill3Slot, 0f);

        ClearSkillIcon(skill1Slot);
        ClearSkillIcon(skill2Slot);

        if (skill3Slot != null)
        {
            skill3Slot.ClearIcon();
            skill3Slot.SetVisible(false);
        }
    }

    public void SetSkillNames(string firstSkillName, string secondSkillName)
    {
        SetSkillNames(firstSkillName, secondSkillName, "");
    }

    public void SetSkillNames(string firstSkillName, string secondSkillName, string thirdSkillName)
    {
        skill1Name = NormalizeSkillName(firstSkillName);
        skill2Name = NormalizeSkillName(secondSkillName);
        skill3Name = NormalizeSkillName(thirdSkillName);

        Refresh();
    }

    public void Refresh()
    {
        CacheSlotsByComponent();

        if (targetAgent == null)
        {
            SetSlotGaugeAmount(skill1Slot, 0f);
            SetSlotGaugeAmount(skill2Slot, 0f);
            SetSlotGaugeAmount(skill3Slot, 0f);

            ClearSkillIcon(skill1Slot);
            ClearSkillIcon(skill2Slot);

            if (skill3Slot != null)
            {
                skill3Slot.ClearIcon();
                skill3Slot.SetVisible(false);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(skill1Name) && string.IsNullOrWhiteSpace(skill2Name))
        {
            SetupSkillNamesByAgent(targetAgent);
        }

        RefreshBasicSkillIcons();

        UpdateSkillGaugeSlot(skill1Slot, skill1Name);
        UpdateSkillGaugeSlot(skill2Slot, skill2Name);
        RefreshThirdSkillSlot();
    }

    private void RefreshBasicSkillIcons()
    {
        RefreshBasicSkillIcon(skill1Slot, 0, skill1Name);
        RefreshBasicSkillIcon(skill2Slot, 1, skill2Name);
    }

    private void RefreshBasicSkillIcon(AgentSkillSlotUI slot, int basicSkillIndex, string skillName)
    {
        if (slot == null)
            return;

        slot.SetVisible(true);

        SkillDefinitionSO skillDefinition = GetBasicSkillDefinitionByIndex(basicSkillIndex);

        if (skillDefinition != null)
        {
            if (skillDefinition.Icon != null)
            {
                slot.SetIcon(skillDefinition.Icon);
                return;
            }

            Debug.LogWarning(
                $"[AgentSkillGaugeUI] {targetAgent.name}의 BasicSkills[{basicSkillIndex}]는 존재하지만 Icon이 비어 있습니다. " +
                $"SkillSO={skillDefinition.name}, SkillName={skillName}"
            );

            return;
        }

        if (TryGetSkillDefinitionForPopup(skillName, out skillDefinition))
        {
            if (skillDefinition != null && skillDefinition.Icon != null)
            {
                slot.SetIcon(skillDefinition.Icon);
                return;
            }

            Debug.LogWarning(
                $"[AgentSkillGaugeUI] SkillDefinitionSO는 찾았지만 Icon이 비어 있습니다. " +
                $"Agent={targetAgent.name}, SkillName={skillName}"
            );

            return;
        }

        Debug.LogWarning(
            $"[AgentSkillGaugeUI] SkillDefinitionSO를 찾지 못했습니다. " +
            $"Agent={targetAgent.name}, Index={basicSkillIndex}, SkillName={skillName}"
        );
    }

    private SkillDefinitionSO GetBasicSkillDefinitionByIndex(int index)
    {
        if (targetAgent == null)
            return null;

        AgentDefinitionSO definition = targetAgent.AgentDefinition;

        if (definition == null)
            return null;

        IReadOnlyList<SkillDefinitionSO> basicSkills = definition.BasicSkills;

        if (basicSkills == null)
            return null;

        if (index < 0 || index >= basicSkills.Count)
            return null;

        return basicSkills[index];
    }

    private void ClearSkillIcon(AgentSkillSlotUI slot)
    {
        if (slot == null)
            return;

        slot.ClearIcon();
    }

    public bool CanUseSkill(string skillName)
    {
        if (targetAgent == null)
            return false;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsPositionShareSkill(normalizedSkillName))
        {
            if (TargetAgentHasSkill(normalizedSkillName))
                return true;

            return targetAgent is Observer;
        }

        return targetAgent.CanUseSkillGaugeForSkill(normalizedSkillName);
    }

    private bool TargetAgentHasSkill(string skillName)
    {
        if (targetAgent == null)
            return false;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (targetAgent.TryGetSkillDefinitionByRuntimeKey(normalizedSkillName, out _))
            return true;

        if (targetAgent.TryGetSkillDefinitionById(normalizedSkillName, out _))
            return true;

        if (targetAgent.TryGetSkillDefinitionByCommandKeyword(skillName, out _))
            return true;

        return TryGetSkillDefinitionFromAgentDefinition(normalizedSkillName, out _);
    }

    private void CacheSlotsByComponent()
    {
        if (!autoFindSlotsByComponent)
            return;

        AgentSkillSlotUI[] slots = GetComponentsInChildren<AgentSkillSlotUI>(true);

        for (int i = 0; i < slots.Length; i++)
        {
            AgentSkillSlotUI slot = slots[i];

            if (slot == null)
                continue;

            switch (slot.SlotType)
            {
                case AgentSkillSlotType.First:
                    if (skill1Slot == null)
                        skill1Slot = slot;
                    break;

                case AgentSkillSlotType.Second:
                    if (skill2Slot == null)
                        skill2Slot = slot;
                    break;

                case AgentSkillSlotType.Third:
                    if (skill3Slot == null)
                        skill3Slot = slot;
                    break;
            }
        }
    }

    private void ConfigureSlotGauges()
    {
        if (!setupImageFillSetting)
            return;

        Image.FillMethod fillMethod = Image.FillMethod.Vertical;
        int fillOrigin = 0;

        switch (fillDirection)
        {
            case GaugeFillDirection.HorizontalLeft:
                fillMethod = Image.FillMethod.Horizontal;
                fillOrigin = 1;
                break;

            case GaugeFillDirection.HorizontalRight:
                fillMethod = Image.FillMethod.Horizontal;
                fillOrigin = 0;
                break;

            case GaugeFillDirection.VerticalBottom:
                fillMethod = Image.FillMethod.Vertical;
                fillOrigin = 0;
                break;

            case GaugeFillDirection.VerticalTop:
                fillMethod = Image.FillMethod.Vertical;
                fillOrigin = 1;
                break;
        }

        ConfigureSlotGauge(skill1Slot, fillMethod, fillOrigin);
        ConfigureSlotGauge(skill2Slot, fillMethod, fillOrigin);
        ConfigureSlotGauge(skill3Slot, fillMethod, fillOrigin);
    }

    private void ConfigureSlotGauge(AgentSkillSlotUI slot, Image.FillMethod fillMethod, int fillOrigin)
    {
        if (slot == null)
            return;

        slot.ConfigureGauge(fillMethod, fillOrigin);
    }

    private void TryCacheTargetAgent()
    {
        if (agentId < 0)
            return;

        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (agent.AgentID != agentId)
                continue;

            targetAgent = agent;
            SetupSkillNamesByAgent(targetAgent);
            break;
        }
    }

    private void TryCacheCommanderUIController()
    {
        if (!autoFindCommanderUIController)
            return;

        if (commanderUIController != null)
            return;

        commanderUIController = FindFirstObjectByType<CommanderUIController>();
    }

    private void SetupSkillNamesByAgent(AgentController agent)
    {
        if (agent == null)
            return;

        if (TrySetupSkillNamesByAgentDefinition(agent))
            return;

        if (agent.Stats != null)
        {
            SetupSkillNamesByRole(agent.Stats.role);
            return;
        }

        if (agent is Chaser)
        {
            SetupSkillNamesByRole(AgentRole.Chaser);
            return;
        }

        if (agent is Observer)
        {
            SetupSkillNamesByRole(AgentRole.Observer);
            return;
        }

        if (agent is Engineer)
        {
            SetupSkillNamesByRole(AgentRole.Engineer);
            return;
        }

        if (agent is Trickster)
        {
            SetupSkillNamesByRole(AgentRole.Trickster);
            return;
        }

        SetupSkillNamesByAgentId(agent.AgentID);
    }

    private bool TrySetupSkillNamesByAgentDefinition(AgentController agent)
    {
        if (agent == null)
            return false;

        AgentDefinitionSO definition = agent.AgentDefinition;

        if (definition == null)
            return false;

        IReadOnlyList<SkillDefinitionSO> basicSkills = definition.BasicSkills;

        if (basicSkills == null || basicSkills.Count <= 0)
            return false;

        SkillDefinitionSO firstSkill = GetSkillAt(basicSkills, 0);
        SkillDefinitionSO secondSkill = GetSkillAt(basicSkills, 1);

        skill1Name = NormalizeSkillName(GetRuntimeSkillNameFromDefinition(firstSkill));
        skill2Name = NormalizeSkillName(GetRuntimeSkillNameFromDefinition(secondSkill));
        skill3Name = "";

        return !string.IsNullOrWhiteSpace(skill1Name) ||
               !string.IsNullOrWhiteSpace(skill2Name);
    }

    private SkillDefinitionSO GetSkillAt(IReadOnlyList<SkillDefinitionSO> skills, int index)
    {
        if (skills == null)
            return null;

        if (index < 0 || index >= skills.Count)
            return null;

        return skills[index];
    }

    private string GetRuntimeSkillNameFromDefinition(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skillDefinition.RuntimeSkillKey))
            return skillDefinition.RuntimeSkillKey;

        if (!string.IsNullOrWhiteSpace(skillDefinition.SkillId))
            return skillDefinition.SkillId;

        if (!string.IsNullOrWhiteSpace(skillDefinition.CommandKeyword))
            return skillDefinition.CommandKeyword;

        if (!string.IsNullOrWhiteSpace(skillDefinition.DisplayName))
            return skillDefinition.DisplayName;

        return "";
    }

    private void SetupSkillNamesByRole(AgentRole role)
    {
        switch (role)
        {
            case AgentRole.Chaser:
                skill1Name = SkillAccessControl;
                skill2Name = SkillEscapeBlock;
                skill3Name = "";
                break;

            case AgentRole.Observer:
                skill1Name = SkillDrone;
                skill2Name = SkillPositionShare;
                skill3Name = "";
                break;

            case AgentRole.Engineer:
                skill1Name = SkillBarricade;
                skill2Name = SkillStopSignal;
                skill3Name = "";
                break;

            case AgentRole.Trickster:
                skill1Name = SkillFakeBox;
                skill2Name = SkillJokerCard;
                skill3Name = "";
                break;
        }
    }

    private void SetupSkillNamesByAgentId(int id)
    {
        switch (id)
        {
            case 0:
                skill1Name = SkillAccessControl;
                skill2Name = SkillEscapeBlock;
                skill3Name = "";
                break;

            case 1:
                skill1Name = SkillDrone;
                skill2Name = SkillPositionShare;
                skill3Name = "";
                break;

            case 2:
                skill1Name = SkillBarricade;
                skill2Name = SkillStopSignal;
                skill3Name = "";
                break;

            case 3:
                skill1Name = SkillFakeBox;
                skill2Name = SkillJokerCard;
                skill3Name = "";
                break;
        }
    }

    private void UpdateSkillGaugeSlot(AgentSkillSlotUI slot, string skillName)
    {
        if (slot == null)
            return;

        if (targetAgent == null)
        {
            slot.SetGaugeAmount(0f);
            return;
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            slot.SetGaugeAmount(0f);
            return;
        }

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (TryUpdateToggleSkillGaugeSlot(slot, normalizedSkillName))
            return;

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(normalizedSkillName);

        if (requiredGauge <= 0f)
        {
            slot.SetGaugeAmount(1f);
            return;
        }

        float amount = targetAgent.GetSkillGaugeNormalizedForSkill(normalizedSkillName);
        slot.SetGaugeAmount(amount);
    }

    private bool TryUpdateToggleSkillGaugeSlot(AgentSkillSlotUI slot, string normalizedSkillName)
    {
        if (!IsPositionShareSkill(normalizedSkillName))
            return false;

        Observer observer = targetAgent as Observer;

        if (observer == null)
        {
            slot.SetGaugeAmount(toggleSkillOffFillAmount);
            return true;
        }

        float amount = observer.IsTargetPositionShareEnabled
            ? toggleSkillOnFillAmount
            : toggleSkillOffFillAmount;

        slot.SetGaugeAmount(amount);
        return true;
    }

    private void SetSlotGaugeAmount(AgentSkillSlotUI slot, float amount)
    {
        if (slot == null)
            return;

        slot.SetGaugeAmount(amount);
    }

    private void RefreshThirdSkillSlot()
    {
        if (skill3Slot == null)
            return;

        if (ShouldUseAgentDefinitionUnlockableSkills())
        {
            RefreshThirdSkillSlotByAgentDefinition();
            return;
        }

        UpgradeDefinition unlockedUpgrade = GetUnlockedThirdSkillUpgrade();

        if (unlockedUpgrade == null)
        {
            ClearThirdSkillSlot();
            return;
        }

        string unlockedSkillName = GetSkillNameFromUpgrade(unlockedUpgrade);

        if (string.IsNullOrWhiteSpace(unlockedSkillName))
        {
            ClearThirdSkillSlot();
            return;
        }

        skill3Name = NormalizeSkillName(unlockedSkillName);

        skill3Slot.SetVisible(true);

        if (unlockedUpgrade.Icon != null)
            skill3Slot.SetIcon(unlockedUpgrade.Icon);

        UpdateSkillGaugeSlot(skill3Slot, skill3Name);
    }

    private bool ShouldUseAgentDefinitionUnlockableSkills()
    {
        if (targetAgent == null)
            return false;

        AgentDefinitionSO definition = targetAgent.AgentDefinition;

        if (definition == null)
            return false;

        IReadOnlyList<SkillDefinitionSO> unlockableSkills = definition.UnlockableSkills;

        return unlockableSkills != null && unlockableSkills.Count > 0;
    }

    private void RefreshThirdSkillSlotByAgentDefinition()
    {
        if (!TryGetUnlockedSkillFromAgentDefinition(
                out SkillDefinitionSO unlockedSkill,
                out UpgradeDefinition unlockedUpgrade))
        {
            ClearThirdSkillSlot();
            return;
        }

        string unlockedSkillName = GetRuntimeSkillNameFromDefinition(unlockedSkill);

        if (string.IsNullOrWhiteSpace(unlockedSkillName))
        {
            ClearThirdSkillSlot();
            return;
        }

        skill3Name = NormalizeSkillName(unlockedSkillName);

        skill3Slot.SetVisible(true);

        Sprite icon = GetThirdSkillIcon(unlockedSkill, unlockedUpgrade);

        if (icon != null)
            skill3Slot.SetIcon(icon);

        UpdateSkillGaugeSlot(skill3Slot, skill3Name);
    }

    private bool TryGetUnlockedSkillFromAgentDefinition(
        out SkillDefinitionSO unlockedSkill,
        out UpgradeDefinition unlockedUpgrade)
    {
        unlockedSkill = null;
        unlockedUpgrade = null;

        if (targetAgent == null)
            return false;

        AgentDefinitionSO definition = targetAgent.AgentDefinition;

        if (definition == null)
            return false;

        IReadOnlyList<SkillDefinitionSO> unlockableSkills = definition.UnlockableSkills;

        if (unlockableSkills == null || unlockableSkills.Count <= 0)
            return false;

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return false;

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        for (int i = 0; i < unlockableSkills.Count; i++)
        {
            SkillDefinitionSO skill = unlockableSkills[i];

            if (skill == null)
                continue;

            if (!skill.HasUnlockUpgradeId)
                continue;

            string unlockUpgradeId = skill.UnlockUpgradeId;

            if (!upgradeManager.HasAgentUpgrade(unlockUpgradeId))
                continue;

            unlockedSkill = skill;

            if (upgradeDatabase != null)
                unlockedUpgrade = upgradeDatabase.GetUpgradeOrNull(unlockUpgradeId);

            return true;
        }

        return false;
    }

    private Sprite GetThirdSkillIcon(SkillDefinitionSO skillDefinition, UpgradeDefinition upgradeDefinition)
    {
        if (skillDefinition != null && skillDefinition.Icon != null)
            return skillDefinition.Icon;

        if (upgradeDefinition != null && upgradeDefinition.Icon != null)
            return upgradeDefinition.Icon;

        return null;
    }

    private void ClearThirdSkillSlot()
    {
        skill3Name = "";

        if (skill3Slot == null)
            return;

        skill3Slot.SetGaugeAmount(0f);
        skill3Slot.ClearIcon();
        skill3Slot.SetVisible(false);
    }

    private UpgradeDefinition GetUnlockedThirdSkillUpgrade()
    {
        if (IsChaserAgent())
            return GetUnlockedChaserThirdSkillUpgrade();

        if (IsObserverAgent())
            return GetUnlockedObserverThirdSkillUpgrade();

        if (IsEngineerAgent())
            return GetUnlockedEngineerThirdSkillUpgrade();

        if (IsTricksterAgent())
            return GetUnlockedTricksterThirdSkillUpgrade();

        return null;
    }

    private UpgradeDefinition GetUnlockedChaserThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return null;

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
            return null;

        if (upgradeManager.HasAgentUpgrade(ChaserUnlockPatrol))
            return upgradeDatabase.GetUpgradeOrNull(ChaserUnlockPatrol);

        if (upgradeManager.HasAgentUpgrade(ChaserUnlockTrackingInstinct))
            return upgradeDatabase.GetUpgradeOrNull(ChaserUnlockTrackingInstinct);

        return null;
    }

    private UpgradeDefinition GetUnlockedObserverThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return null;

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
            return null;

        if (upgradeManager.HasAgentUpgrade(ObserverUnlockReconnaissance))
            return upgradeDatabase.GetUpgradeOrNull(ObserverUnlockReconnaissance);

        if (upgradeManager.HasAgentUpgrade(ObserverUnlockObservationSupport))
            return upgradeDatabase.GetUpgradeOrNull(ObserverUnlockObservationSupport);

        return null;
    }

    private UpgradeDefinition GetUnlockedEngineerThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return null;

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
            return null;

        if (upgradeManager.HasAgentUpgrade(EngineerUnlockDemolition))
            return upgradeDatabase.GetUpgradeOrNull(EngineerUnlockDemolition);

        if (upgradeManager.HasAgentUpgrade(EngineerUnlockSafeZone))
            return upgradeDatabase.GetUpgradeOrNull(EngineerUnlockSafeZone);

        return null;
    }

    private UpgradeDefinition GetUnlockedTricksterThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return null;

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
            return null;

        if (upgradeManager.HasAgentUpgrade(TricksterUnlockVanishing))
            return upgradeDatabase.GetUpgradeOrNull(TricksterUnlockVanishing);

        if (upgradeManager.HasAgentUpgrade(TricksterUnlockMisdirection))
            return upgradeDatabase.GetUpgradeOrNull(TricksterUnlockMisdirection);

        return null;
    }

    private string GetSkillNameFromUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
            return "";

        string upgradeId = upgrade.UpgradeId;

        if (string.IsNullOrWhiteSpace(upgradeId))
            return "";

        switch (upgradeId)
        {
            case ChaserUnlockPatrol:
                return SkillPatrol;

            case ChaserUnlockTrackingInstinct:
                return SkillTrackingInstinct;

            case ObserverUnlockReconnaissance:
                return SkillReconnaissance;

            case ObserverUnlockObservationSupport:
                return SkillObservationSupport;

            case EngineerUnlockDemolition:
                return SkillDemolition;

            case EngineerUnlockSafeZone:
                return SkillSafeZone;

            case TricksterUnlockVanishing:
                return SkillVanishing;

            case TricksterUnlockMisdirection:
                return SkillMisdirection;
        }

        return upgradeId;
    }

    private bool IsChaserAgent()
    {
        if (targetAgent is Chaser)
            return true;

        return targetAgent != null &&
               targetAgent.Stats != null &&
               targetAgent.Stats.role == AgentRole.Chaser;
    }

    private bool IsObserverAgent()
    {
        if (targetAgent is Observer)
            return true;

        return targetAgent != null &&
               targetAgent.Stats != null &&
               targetAgent.Stats.role == AgentRole.Observer;
    }

    private bool IsEngineerAgent()
    {
        if (targetAgent is Engineer)
            return true;

        return targetAgent != null &&
               targetAgent.Stats != null &&
               targetAgent.Stats.role == AgentRole.Engineer;
    }

    private bool IsTricksterAgent()
    {
        if (targetAgent is Trickster)
            return true;

        return targetAgent != null &&
               targetAgent.Stats != null &&
               targetAgent.Stats.role == AgentRole.Trickster;
    }

    private void HandleSkillGaugeInfoClick()
    {
        if (!showGaugeInfoOnSkillClick && !pasteSkillNameOnFunctionKeyClick && !pasteSkillNameOnDoubleClick)
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.leftButton.wasReleasedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (IsSlotClicked(skill1Slot, mousePosition))
        {
            HandleSkillIconClick(skill1Slot, skill1Name, mousePosition);
            return;
        }

        if (IsSlotClicked(skill2Slot, mousePosition))
        {
            HandleSkillIconClick(skill2Slot, skill2Name, mousePosition);
            return;
        }

        if (skill3Slot != null && skill3Slot.IsVisible && IsSlotClicked(skill3Slot, mousePosition))
        {
            HandleSkillIconClick(skill3Slot, skill3Name, mousePosition);
        }
    }

    private void HandleSkillDescriptionRightClick()
    {
        if (!showSkillDescriptionOnRightClick)
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.rightButton.wasReleasedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (IsSlotClicked(skill1Slot, mousePosition))
        {
            ShowSkillDescriptionPopup(skill1Name, mousePosition);
            return;
        }

        if (IsSlotClicked(skill2Slot, mousePosition))
        {
            ShowSkillDescriptionPopup(skill2Name, mousePosition);
            return;
        }

        if (skill3Slot != null && skill3Slot.IsVisible && IsSlotClicked(skill3Slot, mousePosition))
        {
            ShowSkillDescriptionPopup(skill3Name, mousePosition);
        }
    }

    private bool IsSlotClicked(AgentSkillSlotUI slot, Vector2 screenPosition)
    {
        if (slot == null)
            return false;

        RectTransform clickArea = slot.ClickArea;

        if (clickArea == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            clickArea,
            screenPosition,
            GetCanvasCamera()
        );
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private void HandleSkillIconClick(AgentSkillSlotUI clickedSlot, string skillName, Vector2 mousePosition)
    {
        bool pasted = false;

        if (IsSkillIconDoubleClick(clickedSlot))
        {
            pasted = TryPasteSkillNameToOwnInput(skillName);
            ResetLastSkillIconClick();
        }
        else
        {
            RegisterSkillIconClick(clickedSlot);
            pasted = TryPasteSkillNameToFunctionKeyInput(skillName);
        }

        if (pasted && !showGaugeInfoAfterSkillPaste)
            return;

        if (showGaugeInfoOnSkillClick)
            ShowGaugeInfoLabel(skillName, mousePosition);
    }

    private bool IsSkillIconDoubleClick(AgentSkillSlotUI clickedSlot)
    {
        if (!pasteSkillNameOnDoubleClick)
            return false;

        if (clickedSlot == null)
            return false;

        if (lastClickedSkillSlot != clickedSlot)
            return false;

        float elapsedTime = Time.unscaledTime - lastSkillClickTime;
        return elapsedTime <= doubleClickMaxInterval;
    }

    private void RegisterSkillIconClick(AgentSkillSlotUI clickedSlot)
    {
        lastClickedSkillSlot = clickedSlot;
        lastSkillClickTime = Time.unscaledTime;
    }

    private void ResetLastSkillIconClick()
    {
        lastClickedSkillSlot = null;
        lastSkillClickTime = -999f;
    }

    private bool TryPasteSkillNameToOwnInput(string skillName)
    {
        if (!pasteSkillNameOnDoubleClick)
            return false;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
            return false;

        if (commanderUIController == null && autoFindCommanderUIController)
            TryCacheCommanderUIController();

        if (commanderUIController == null)
            return false;

        int clickedAgentId = targetAgent != null ? targetAgent.AgentID : agentId;

        if (clickedAgentId < 0)
            return false;

        string displayName = GetSkillDisplayNameForUI(normalizedSkillName);

        return commanderUIController.TryPasteSkillDisplayNameToAgentInput(
            clickedAgentId,
            displayName
        );
    }

    private bool TryPasteSkillNameToFunctionKeyInput(string skillName)
    {
        if (!pasteSkillNameOnFunctionKeyClick)
            return false;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
            return false;

        if (!TryGetHeldFunctionKeyInputIndex(out int inputIndex))
            return false;

        if (commanderUIController == null && autoFindCommanderUIController)
            TryCacheCommanderUIController();

        if (commanderUIController == null)
            return false;

        int clickedAgentId = targetAgent != null ? targetAgent.AgentID : agentId;

        if (clickedAgentId < 0)
            return false;

        string displayName = GetSkillDisplayNameForUI(normalizedSkillName);

        return commanderUIController.TryPasteSkillDisplayNameToInputIndex(
            inputIndex,
            clickedAgentId,
            displayName,
            pasteOnlyWhenFunctionKeyInputMatchesClickedAgent
        );
    }

    private bool TryGetHeldFunctionKeyInputIndex(out int inputIndex)
    {
        inputIndex = -1;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return false;

        if (keyboard.f1Key.isPressed)
        {
            inputIndex = 0;
            return true;
        }

        if (keyboard.f2Key.isPressed)
        {
            inputIndex = 1;
            return true;
        }

        if (keyboard.f3Key.isPressed)
        {
            inputIndex = 2;
            return true;
        }

        if (keyboard.f4Key.isPressed)
        {
            inputIndex = 3;
            return true;
        }

        return false;
    }

    private void ShowSkillDescriptionPopup(string skillName, Vector2 mousePosition)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (skillDescriptionPopupUI == null && autoFindSkillDescriptionPopupUI)
            skillDescriptionPopupUI = FindFirstObjectByType<SkillDescriptionPopupUI>(FindObjectsInactive.Include);

        if (skillDescriptionPopupUI == null)
            return;

        if (TryGetSkillDefinitionForPopup(skillName, out SkillDefinitionSO skillDefinition))
        {
            skillDescriptionPopupUI.Show(skillDefinition, mousePosition);
            return;
        }

        Debug.LogWarning($"[AgentSkillGaugeUI] SkillDefinitionSO를 찾을 수 없습니다. SkillName: {skillName}");

        skillDescriptionPopupUI.Show(
            skillName,
            "스킬 설명 데이터를 찾을 수 없습니다.",
            mousePosition
        );
    }

    private bool TryGetSkillDefinitionForPopup(string skillName, out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (TryGetSkillDefinitionFromAgentDefinition(normalizedSkillName, out skillDefinition))
            return true;

        if (targetAgent != null)
        {
            if (targetAgent.TryGetSkillDefinitionByRuntimeKey(normalizedSkillName, out skillDefinition))
                return true;

            if (targetAgent.TryGetSkillDefinitionById(normalizedSkillName, out skillDefinition))
                return true;

            if (targetAgent.TryGetSkillDefinitionByCommandKeyword(skillName, out skillDefinition))
                return true;
        }

        if (skillDatabase == null)
            return false;

        if (skillDatabase.TryGetSkillByRuntimeKey(normalizedSkillName, out skillDefinition))
            return true;

        if (skillDatabase.TryGetSkillById(normalizedSkillName, out skillDefinition))
            return true;

        if (skillDatabase.TryGetSkillByCommandKeyword(skillName, out skillDefinition))
            return true;

        return false;
    }

    private bool TryGetSkillDefinitionFromAgentDefinition(string skillName, out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (targetAgent == null)
            return false;

        AgentDefinitionSO definition = targetAgent.AgentDefinition;

        if (definition == null)
            return false;

        string normalizedSkillName = NormalizeSkillKeyForMatch(skillName);

        if (TryGetSkillDefinitionFromList(definition.BasicSkills, normalizedSkillName, out skillDefinition))
            return true;

        if (TryGetSkillDefinitionFromList(definition.UnlockableSkills, normalizedSkillName, out skillDefinition))
            return true;

        return false;
    }

    private bool TryGetSkillDefinitionFromList(
        IReadOnlyList<SkillDefinitionSO> skills,
        string normalizedSkillName,
        out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinitionSO skill = skills[i];

            if (skill == null)
                continue;

            if (SkillMatchesNormalizedName(skill, normalizedSkillName))
            {
                skillDefinition = skill;
                return true;
            }
        }

        return false;
    }

    private bool SkillMatchesNormalizedName(SkillDefinitionSO skill, string normalizedSkillName)
    {
        if (skill == null)
            return false;

        if (string.IsNullOrWhiteSpace(normalizedSkillName))
            return false;

        if (NormalizeSkillKeyForMatch(skill.RuntimeSkillKey) == normalizedSkillName)
            return true;

        if (NormalizeSkillKeyForMatch(skill.SkillId) == normalizedSkillName)
            return true;

        if (NormalizeSkillKeyForMatch(skill.CommandKeyword) == normalizedSkillName)
            return true;

        if (NormalizeSkillKeyForMatch(skill.DisplayName) == normalizedSkillName)
            return true;

        IReadOnlyList<string> aliases = skill.RuntimeSkillAliases;

        if (aliases != null)
        {
            for (int i = 0; i < aliases.Count; i++)
            {
                if (NormalizeSkillKeyForMatch(aliases[i]) == normalizedSkillName)
                    return true;
            }
        }

        return false;
    }

    private string NormalizeSkillKeyForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private bool IsAutoActivatedSkill(string skillName)
    {
        return NormalizeSkillName(skillName) == SkillJokerCard;
    }

    private string NormalizeSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "";

        string skill = skillName.Trim().ToLower();

        if (IsAccessControlSkill(skill))
            return SkillAccessControl;

        if (IsEscapeBlockSkill(skill))
            return SkillEscapeBlock;

        if (IsPatrolSkill(skill))
            return SkillPatrol;

        if (IsTrackingInstinctSkill(skill))
            return SkillTrackingInstinct;

        if (IsReconnaissanceSkill(skill))
            return SkillReconnaissance;

        if (IsObservationSupportSkill(skill))
            return SkillObservationSupport;

        if (IsPositionShareSkill(skill))
            return SkillPositionShare;

        if (IsLegacySlowTrapSkill(skill))
            return SkillStopSignal;

        if (IsLegacyNoisemakerSkill(skill))
            return SkillFakeBox;

        if (IsLegacyHologramSkill(skill))
            return SkillJokerCard;

        if (IsFakeBoxSkill(skill))
            return SkillFakeBox;

        if (IsJokerCardSkill(skill))
            return SkillJokerCard;

        if (IsStopSignalSkill(skill))
            return SkillStopSignal;

        if (IsDemolitionSkill(skill))
            return SkillDemolition;

        if (IsSafeZoneSkill(skill))
            return SkillSafeZone;

        if (IsDroneSkill(skill))
            return SkillDrone;

        if (IsVanishingSkill(skill))
            return SkillVanishing;

        if (IsMisdirectionSkill(skill))
            return SkillMisdirection;

        return NormalizeSkillKeyForMatch(skill);
    }

    private bool IsAccessControlSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillAccessControl ||
               skill == "access_control" ||
               skill.Contains("access control") ||
               skill.Contains("control zone") ||
               skill.Contains("출입통제") ||
               skill.Contains("출입 통제") ||
               skill.Contains("통제구역") ||
               skill.Contains("통제 구역");
    }

    private bool IsEscapeBlockSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillEscapeBlock ||
               skill == "escape_block" ||
               skill.Contains("escape block") ||
               skill.Contains("도주제지") ||
               skill.Contains("도주 제지");
    }

    private bool IsPatrolSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillPatrol ||
               skill.Contains("patrol") ||
               skill.Contains("순찰");
    }

    private bool IsTrackingInstinctSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillTrackingInstinct ||
               skill == "tracking_instinct" ||
               skill.Contains("tracking instinct") ||
               skill.Contains("추적본능") ||
               skill.Contains("추적 본능");
    }

    private bool IsDroneSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillDrone ||
               skill.Contains("drone") ||
               skill.Contains("드론");
    }

    private bool IsReconnaissanceSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillReconnaissance ||
               skill.Contains("reconnaissance") ||
               skill.Contains("정찰");
    }

    private bool IsObservationSupportSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillObservationSupport ||
               skill == "observation_support" ||
               skill.Contains("observation support") ||
               skill.Contains("관측지원") ||
               skill.Contains("관측 지원");
    }

    private bool IsPositionShareSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillPositionShare ||
               skill == SkillPositionShareOn ||
               skill == SkillPositionShareOff ||
               skill == "position_share" ||
               skill.Contains("position share") ||
               skill.Contains("위치공유") ||
               skill.Contains("위치 공유");
    }

    private bool IsBarricadeSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillBarricade ||
               skill.Contains("barricade") ||
               skill.Contains("바리케이드");
    }

    private bool IsStopSignalSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillStopSignal ||
               skill == "stop_signal" ||
               skill.Contains("stop signal") ||
               skill.Contains("정지신호") ||
               skill.Contains("정지 신호");
    }

    private bool IsLegacySlowTrapSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillSlowTrap ||
               skill.Contains("slow trap") ||
               skill.Contains("감속함정") ||
               skill.Contains("감속 함정");
    }

    private bool IsDemolitionSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillDemolition ||
               skill.Contains("demolition") ||
               skill.Contains("철거");
    }

    private bool IsSafeZoneSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillSafeZone ||
               skill == "safe_zone" ||
               skill.Contains("safe zone") ||
               skill.Contains("안전구역") ||
               skill.Contains("안전 구역");
    }

    private bool IsFakeBoxSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillFakeBox ||
               skill == "fake_box" ||
               skill.Contains("fake box") ||
               skill.Contains("페이크박스") ||
               skill.Contains("페이크 박스");
    }

    private bool IsJokerCardSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillJokerCard ||
               skill == "joker_card" ||
               skill.Contains("joker card") ||
               skill.Contains("조커카드") ||
               skill.Contains("조커 카드");
    }

    private bool IsVanishingSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillVanishing ||
               skill.Contains("vanishing") ||
               skill.Contains("배니싱");
    }

    private bool IsMisdirectionSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillMisdirection ||
               skill.Contains("misdirection") ||
               skill.Contains("미스디렉션");
    }

    private bool IsLegacyNoisemakerSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == LegacySkillNoisemaker ||
               skill.Contains("noise maker") ||
               skill.Contains("noisemaker");
    }

    private bool IsLegacyHologramSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == LegacySkillHologram ||
               skill.Contains("hologram") ||
               skill.Contains("홀로그램");
    }

    private string GetSkillDisplayNameForUI(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "";

        if (TryGetSkillDefinitionForPopup(skillName, out SkillDefinitionSO skillDefinition))
        {
            if (!string.IsNullOrWhiteSpace(skillDefinition.DisplayName))
                return skillDefinition.DisplayName;
        }

        return skillName;
    }

    private void ShowGaugeInfoLabel(string skillName, Vector2 mousePosition)
    {
        gaugeInfoLabelText = GetGaugeInfoText(skillName);
        gaugeInfoLabelScreenPosition = mousePosition + gaugeInfoLabelOffset;
        gaugeInfoLabelEndTime = Time.unscaledTime + gaugeInfoLabelDuration;

        Debug.Log($"[AgentSkillGaugeUI] {gaugeInfoLabelText}");
    }

    private string GetGaugeInfoText(string skillName)
    {
        if (targetAgent == null)
            return "에이전트 연결 없음";

        if (string.IsNullOrWhiteSpace(skillName))
            return "스킬 정보 없음";

        string normalizedSkillName = NormalizeSkillName(skillName);
        string displayName = GetSkillDisplayNameForUI(normalizedSkillName);

        if (IsPositionShareSkill(normalizedSkillName))
        {
            Observer observer = targetAgent as Observer;

            if (observer == null)
                return $"{displayName}: 사용 불가";

            string stateText = observer.IsTargetPositionShareEnabled ? "켜짐" : "꺼짐";
            return $"{displayName}: {stateText}";
        }

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(normalizedSkillName);

        if (requiredGauge <= 0f)
            return $"{displayName}: 게이지 필요 없음";

        float currentGauge = targetAgent.GetSkillGaugeCurrentForSkill(normalizedSkillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
            return $"{displayName}: {currentGauge:0.#} / {requiredGauge:0.#} 자동 발동";

        return $"{displayName}: {currentGauge:0.#} / {requiredGauge:0.#}";
    }

    private bool IsGaugeInfoLabelVisible()
    {
        return Time.unscaledTime <= gaugeInfoLabelEndTime;
    }

    private Rect GetGaugeInfoLabelRect()
    {
        Vector2 guiPosition = new Vector2(
            gaugeInfoLabelScreenPosition.x,
            Screen.height - gaugeInfoLabelScreenPosition.y
        );

        return new Rect(
            guiPosition.x,
            guiPosition.y,
            gaugeInfoLabelSize.x,
            gaugeInfoLabelSize.y
        );
    }

    private void OnGUI()
    {
        if (!IsGaugeInfoLabelVisible())
            return;

        if (gaugeInfoLabelStyle == null)
        {
            gaugeInfoLabelStyle = new GUIStyle(GUI.skin.box);
            gaugeInfoLabelStyle.fontSize = 15;
            gaugeInfoLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        GUI.Box(GetGaugeInfoLabelRect(), gaugeInfoLabelText, gaugeInfoLabelStyle);
    }
}