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
        {
            TryCacheTargetAgent();
        }

        if (commanderUIController == null && autoFindCommanderUIController)
        {
            TryCacheCommanderUIController();
        }

        Refresh();
    }

    private void Update()
    {
        if (targetAgent == null && autoBindByAgentId && agentId >= 0)
        {
            TryCacheTargetAgent();
        }

        if (commanderUIController == null && autoFindCommanderUIController)
        {
            TryCacheCommanderUIController();
        }

        Refresh();
        HandleSkillGaugeInfoClick();
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
        {
            TryCacheTargetAgent();
        }

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

        UpdateSkillGaugeSlot(skill1Slot, skill1Name);
        UpdateSkillGaugeSlot(skill2Slot, skill2Name);
        RefreshThirdSkillSlot();
    }

    public bool CanUseSkill(string skillName)
    {
        if (targetAgent == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsPositionShareSkill(normalizedSkillName))
        {
            return targetAgent is Observer;
        }

        return targetAgent.CanUseSkillGaugeForSkill(normalizedSkillName);
    }

    private void CacheSlotsByComponent()
    {
        if (!autoFindSlotsByComponent)
        {
            return;
        }

        AgentSkillSlotUI[] slots = GetComponentsInChildren<AgentSkillSlotUI>(true);

        for (int i = 0; i < slots.Length; i++)
        {
            AgentSkillSlotUI slot = slots[i];

            if (slot == null)
            {
                continue;
            }

            switch (slot.SlotType)
            {
                case AgentSkillSlotType.First:
                    if (skill1Slot == null)
                    {
                        skill1Slot = slot;
                    }
                    break;

                case AgentSkillSlotType.Second:
                    if (skill2Slot == null)
                    {
                        skill2Slot = slot;
                    }
                    break;

                case AgentSkillSlotType.Third:
                    if (skill3Slot == null)
                    {
                        skill3Slot = slot;
                    }
                    break;
            }
        }
    }

    private void ConfigureSlotGauges()
    {
        if (!setupImageFillSetting)
        {
            return;
        }

        Image.FillMethod fillMethod = Image.FillMethod.Vertical;
        int fillOrigin = (int)Image.OriginVertical.Bottom;

        switch (fillDirection)
        {
            case GaugeFillDirection.HorizontalLeft:
                fillMethod = Image.FillMethod.Horizontal;
                fillOrigin = (int)Image.OriginHorizontal.Left;
                break;

            case GaugeFillDirection.HorizontalRight:
                fillMethod = Image.FillMethod.Horizontal;
                fillOrigin = (int)Image.OriginHorizontal.Right;
                break;

            case GaugeFillDirection.VerticalBottom:
                fillMethod = Image.FillMethod.Vertical;
                fillOrigin = (int)Image.OriginVertical.Bottom;
                break;

            case GaugeFillDirection.VerticalTop:
                fillMethod = Image.FillMethod.Vertical;
                fillOrigin = (int)Image.OriginVertical.Top;
                break;
        }

        if (skill1Slot != null)
        {
            skill1Slot.ConfigureGauge(fillMethod, fillOrigin);
        }

        if (skill2Slot != null)
        {
            skill2Slot.ConfigureGauge(fillMethod, fillOrigin);
        }

        if (skill3Slot != null)
        {
            skill3Slot.ConfigureGauge(fillMethod, fillOrigin);
        }
    }

    private void RefreshThirdSkillSlot()
    {
        if (skill3Slot == null)
        {
            return;
        }

        UpgradeDefinition unlockedUpgrade = GetUnlockedThirdSkillUpgrade();

        if (unlockedUpgrade == null)
        {
            skill3Name = "";
            skill3Slot.SetGaugeAmount(0f);
            skill3Slot.ClearIcon();
            skill3Slot.SetVisible(false);
            return;
        }

        string unlockedSkillName = GetSkillNameFromUpgrade(unlockedUpgrade);

        if (string.IsNullOrWhiteSpace(unlockedSkillName))
        {
            skill3Name = "";
            skill3Slot.SetGaugeAmount(0f);
            skill3Slot.ClearIcon();
            skill3Slot.SetVisible(false);
            return;
        }

        skill3Name = NormalizeSkillName(unlockedSkillName);

        skill3Slot.SetVisible(true);
        skill3Slot.SetIcon(unlockedUpgrade.Icon);
        UpdateSkillGaugeSlot(skill3Slot, skill3Name);
    }

    private UpgradeDefinition GetUnlockedThirdSkillUpgrade()
    {
        if (IsChaserAgent())
        {
            return GetUnlockedChaserThirdSkillUpgrade();
        }

        if (IsObserverAgent())
        {
            return GetUnlockedObserverThirdSkillUpgrade();
        }

        if (IsEngineerAgent())
        {
            return GetUnlockedEngineerThirdSkillUpgrade();
        }

        if (IsTricksterAgent())
        {
            return GetUnlockedTricksterThirdSkillUpgrade();
        }

        return null;
    }

    private UpgradeDefinition GetUnlockedChaserThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            return null;
        }

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
        {
            return null;
        }

        if (upgradeManager.HasAgentUpgrade(ChaserUnlockPatrol))
        {
            return upgradeDatabase.GetUpgradeOrNull(ChaserUnlockPatrol);
        }

        if (upgradeManager.HasAgentUpgrade(ChaserUnlockTrackingInstinct))
        {
            return upgradeDatabase.GetUpgradeOrNull(ChaserUnlockTrackingInstinct);
        }

        return null;
    }

    private UpgradeDefinition GetUnlockedObserverThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            return null;
        }

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
        {
            return null;
        }

        if (upgradeManager.HasAgentUpgrade(ObserverUnlockReconnaissance))
        {
            return upgradeDatabase.GetUpgradeOrNull(ObserverUnlockReconnaissance);
        }

        if (upgradeManager.HasAgentUpgrade(ObserverUnlockObservationSupport))
        {
            return upgradeDatabase.GetUpgradeOrNull(ObserverUnlockObservationSupport);
        }

        return null;
    }

    private UpgradeDefinition GetUnlockedEngineerThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            return null;
        }

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
        {
            return null;
        }

        if (upgradeManager.HasAgentUpgrade(EngineerUnlockDemolition))
        {
            return upgradeDatabase.GetUpgradeOrNull(EngineerUnlockDemolition);
        }

        if (upgradeManager.HasAgentUpgrade(EngineerUnlockSafeZone))
        {
            return upgradeDatabase.GetUpgradeOrNull(EngineerUnlockSafeZone);
        }

        return null;
    }

    private UpgradeDefinition GetUnlockedTricksterThirdSkillUpgrade()
    {
        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            return null;
        }

        UpgradeDatabase upgradeDatabase = upgradeManager.UpgradeDatabase;

        if (upgradeDatabase == null)
        {
            return null;
        }

        if (upgradeManager.HasAgentUpgrade(TricksterUnlockVanishing))
        {
            return upgradeDatabase.GetUpgradeOrNull(TricksterUnlockVanishing);
        }

        if (upgradeManager.HasAgentUpgrade(TricksterUnlockMisdirection))
        {
            return upgradeDatabase.GetUpgradeOrNull(TricksterUnlockMisdirection);
        }

        return null;
    }

    private string GetSkillNameFromUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(upgrade.SkillId))
        {
            return NormalizeSkillName(upgrade.SkillId);
        }

        if (upgrade.UpgradeId == ChaserUnlockPatrol)
        {
            return SkillPatrol;
        }

        if (upgrade.UpgradeId == ChaserUnlockTrackingInstinct)
        {
            return SkillTrackingInstinct;
        }

        if (upgrade.UpgradeId == ObserverUnlockReconnaissance)
        {
            return SkillReconnaissance;
        }

        if (upgrade.UpgradeId == ObserverUnlockObservationSupport)
        {
            return SkillObservationSupport;
        }

        if (upgrade.UpgradeId == EngineerUnlockDemolition)
        {
            return SkillDemolition;
        }

        if (upgrade.UpgradeId == EngineerUnlockSafeZone)
        {
            return SkillSafeZone;
        }

        if (upgrade.UpgradeId == TricksterUnlockVanishing)
        {
            return SkillVanishing;
        }

        if (upgrade.UpgradeId == TricksterUnlockMisdirection)
        {
            return SkillMisdirection;
        }

        return "";
    }

    private bool IsChaserAgent()
    {
        if (targetAgent != null)
        {
            if (targetAgent.Stats != null)
            {
                return targetAgent.Stats.role == AgentRole.Chaser;
            }

            return targetAgent.AgentID == 0;
        }

        return agentId == 0;
    }

    private bool IsObserverAgent()
    {
        if (targetAgent != null)
        {
            if (targetAgent.Stats != null)
            {
                return targetAgent.Stats.role == AgentRole.Observer;
            }

            return targetAgent.AgentID == 1;
        }

        return agentId == 1;
    }

    private bool IsEngineerAgent()
    {
        if (targetAgent != null)
        {
            if (targetAgent.Stats != null)
            {
                return targetAgent.Stats.role == AgentRole.Engineer;
            }

            return targetAgent.AgentID == 2;
        }

        return agentId == 2;
    }

    private bool IsTricksterAgent()
    {
        if (targetAgent != null)
        {
            if (targetAgent.Stats != null)
            {
                return targetAgent.Stats.role == AgentRole.Trickster;
            }

            return targetAgent.AgentID == 3;
        }

        return agentId == 3;
    }

    private void HandleSkillGaugeInfoClick()
    {
        if (!showGaugeInfoOnSkillClick &&
            !pasteSkillNameOnFunctionKeyClick &&
            !pasteSkillNameOnDoubleClick)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        if (!mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

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
    private bool IsSlotClicked(AgentSkillSlotUI slot, Vector2 screenPosition)
    {
        if (slot == null)
        {
            return false;
        }

        RectTransform clickArea = slot.ClickArea;

        if (clickArea == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            clickArea,
            screenPosition,
            GetCanvasCamera()
        );
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
        {
            return;
        }

        if (showGaugeInfoOnSkillClick)
        {
            ShowGaugeInfoLabel(skillName, mousePosition);
        }
    }

    private bool IsSkillIconDoubleClick(AgentSkillSlotUI clickedSlot)
    {
        if (!pasteSkillNameOnDoubleClick)
        {
            return false;
        }

        if (clickedSlot == null)
        {
            return false;
        }

        if (lastClickedSkillSlot != clickedSlot)
        {
            return false;
        }

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
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
        {
            return false;
        }

        if (commanderUIController == null && autoFindCommanderUIController)
        {
            TryCacheCommanderUIController();
        }

        if (commanderUIController == null)
        {
            return false;
        }

        int clickedAgentId = targetAgent != null ? targetAgent.AgentID : agentId;

        if (clickedAgentId < 0)
        {
            return false;
        }

        string displayName = GetSkillDisplayName(normalizedSkillName);

        return commanderUIController.TryPasteSkillDisplayNameToAgentInput(
            clickedAgentId,
            displayName
        );
    }

    private bool TryPasteSkillNameToFunctionKeyInput(string skillName)
    {
        if (!pasteSkillNameOnFunctionKeyClick)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
        {
            return false;
        }

        if (!TryGetHeldFunctionKeyInputIndex(out int inputIndex))
        {
            return false;
        }

        if (commanderUIController == null && autoFindCommanderUIController)
        {
            TryCacheCommanderUIController();
        }

        if (commanderUIController == null)
        {
            return false;
        }

        int clickedAgentId = targetAgent != null ? targetAgent.AgentID : agentId;
        string displayName = GetSkillDisplayName(normalizedSkillName);

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
        {
            return false;
        }

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

    private void TryCacheCommanderUIController()
    {
        if (commanderUIController != null)
        {
            return;
        }

        commanderUIController = FindFirstObjectByType<CommanderUIController>();
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return Camera.main;
    }

    private void TryCacheTargetAgent()
    {
        if (agentId < 0)
        {
            return;
        }

        AgentController[] foundAgents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < foundAgents.Length; i++)
        {
            AgentController agent = foundAgents[i];

            if (agent == null)
            {
                continue;
            }

            if (agent.AgentID != agentId)
            {
                continue;
            }

            targetAgent = agent;

            if (string.IsNullOrWhiteSpace(skill1Name) &&
                string.IsNullOrWhiteSpace(skill2Name))
            {
                SetupSkillNamesByAgent(targetAgent);
            }

            return;
        }
    }

    private void SetupSkillNamesByAgent(AgentController agent)
    {
        if (agent == null)
        {
            return;
        }

        if (agent.Stats != null)
        {
            SetupSkillNamesByRole(agent.Stats.role);
            return;
        }

        SetupSkillNamesByAgentId(agent.AgentID);
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
        {
            return;
        }

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
        {
            return;
        }

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
        {
            return false;
        }

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
        {
            return;
        }

        slot.SetGaugeAmount(amount);
    }

    private bool IsAutoActivatedSkill(string skillName)
    {
        return NormalizeSkillName(skillName) == SkillJokerCard;
    }

    private string NormalizeSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return "";
        }

        string skill = skillName.Trim().ToLower();

        if (IsAccessControlSkill(skill))
        {
            return SkillAccessControl;
        }

        if (IsEscapeBlockSkill(skill))
        {
            return SkillEscapeBlock;
        }

        if (IsPatrolSkill(skill))
        {
            return SkillPatrol;
        }

        if (IsTrackingInstinctSkill(skill))
        {
            return SkillTrackingInstinct;
        }

        if (IsReconnaissanceSkill(skill))
        {
            return SkillReconnaissance;
        }

        if (IsObservationSupportSkill(skill))
        {
            return SkillObservationSupport;
        }

        if (IsPositionShareSkill(skill))
        {
            return SkillPositionShare;
        }

        if (IsLegacySlowTrapSkill(skill))
        {
            return SkillStopSignal;
        }

        if (IsLegacyNoisemakerSkill(skill))
        {
            return SkillFakeBox;
        }

        if (IsLegacyHologramSkill(skill))
        {
            return SkillJokerCard;
        }

        if (IsFakeBoxSkill(skill))
        {
            return SkillFakeBox;
        }

        if (IsJokerCardSkill(skill))
        {
            return SkillJokerCard;
        }

        if (IsStopSignalSkill(skill))
        {
            return SkillStopSignal;
        }

        if (IsDemolitionSkill(skill))
        {
            return SkillDemolition;
        }

        if (IsSafeZoneSkill(skill))
        {
            return SkillSafeZone;
        }

        if (IsDroneSkill(skill))
        {
            return SkillDrone;
        }

        if (IsVanishingSkill(skill))
        {
            return SkillVanishing;
        }

        if (IsMisdirectionSkill(skill))
        {
            return SkillMisdirection;
        }

        return skill;
    }

    private bool IsAccessControlSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

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
        {
            return false;
        }

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
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillPatrol ||
               skill == "chaser_patrol" ||
               skill.Contains("patrol") ||
               skill.Contains("순찰");
    }

    private bool IsTrackingInstinctSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

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
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillDrone ||
               skill.Contains("drone") ||
               skill.Contains("uav") ||
               skill.Contains("드론");
    }

    private bool IsReconnaissanceSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillReconnaissance ||
               skill == "recon" ||
               skill == "scout" ||
               skill.Contains("reconnaissance") ||
               skill.Contains("recon") ||
               skill.Contains("scout") ||
               skill.Contains("정찰");
    }

    private bool IsObservationSupportSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillObservationSupport ||
               skill == "observation_support" ||
               skill == "vision_support" ||
               skill.Contains("observation support") ||
               skill.Contains("vision support") ||
               skill.Contains("관측지원") ||
               skill.Contains("관측 지원") ||
               skill.Contains("시야지원") ||
               skill.Contains("시야 지원");
    }

    private bool IsPositionShareSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillPositionShare ||
               skill == SkillPositionShareOn ||
               skill == SkillPositionShareOff ||
               skill == "share_position" ||
               skill == "position_share" ||
               skill.Contains("position share") ||
               skill.Contains("target position share") ||
               skill.Contains("share position") ||
               skill.Contains("위치공유") ||
               skill.Contains("위치 공유");
    }

    private bool IsStopSignalSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillStopSignal ||
               skill == "stop_signal" ||
               skill.Contains("stop signal") ||
               skill.Contains("stop sign") ||
               skill.Contains("정지신호") ||
               skill.Contains("정지 신호");
    }

    private bool IsDemolitionSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillDemolition ||
               skill.Contains("demolish") ||
               skill.Contains("철거");
    }

    private bool IsSafeZoneSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillSafeZone ||
               skill == "safe_zone" ||
               skill.Contains("safe zone") ||
               skill.Contains("안전구역") ||
               skill.Contains("안전 구역");
    }

    private bool IsVanishingSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillVanishing ||
               skill.Contains("vanishing") ||
               skill.Contains("vanish") ||
               skill.Contains("배니싱");
    }

    private bool IsMisdirectionSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillMisdirection ||
               skill.Contains("misdirection") ||
               skill.Contains("mis direction") ||
               skill.Contains("미스디렉션") ||
               skill.Contains("미스 디렉션");
    }

    private bool IsLegacySlowTrapSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillSlowTrap ||
               skill.Contains("slow trap") ||
               skill.Contains("snaretrap") ||
               skill.Contains("감속함정") ||
               skill.Contains("감속 함정") ||
               skill.Contains("구속함정") ||
               skill.Contains("구속 함정");
    }

    private bool IsLegacyNoisemakerSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == LegacySkillNoisemaker ||
               skill.Contains("noise") ||
               skill.Contains("소란장치") ||
               skill.Contains("소란 장치") ||
               skill.Contains("소음장치") ||
               skill.Contains("소음 장치");
    }

    private bool IsLegacyHologramSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == LegacySkillHologram ||
               skill.Contains("hologram") ||
               skill.Contains("홀로그램");
    }

    private bool IsFakeBoxSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillFakeBox ||
               skill == "fake_box" ||
               skill.Contains("fake box") ||
               skill.Contains("magicbox") ||
               skill.Contains("magic box") ||
               skill.Contains("페이크박스") ||
               skill.Contains("페이크 박스") ||
               skill.Contains("마술상자") ||
               skill.Contains("마술 상자") ||
               skill.Contains("가짜상자") ||
               skill.Contains("가짜 상자");
    }

    private bool IsJokerCardSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        string skill = skillName.Trim().ToLower();

        return skill == SkillJokerCard ||
               skill == "joker_card" ||
               skill.Contains("joker card") ||
               skill.Contains("조커카드") ||
               skill.Contains("조커 카드");
    }

    private string GetSkillDisplayName(string skillName)
    {
        switch (NormalizeSkillName(skillName))
        {
            case SkillDash:
                return "대시";

            case SkillSmoke:
                return "연막";

            case SkillAccessControl:
                return "출입 통제";

            case SkillEscapeBlock:
                return "도주 제지";

            case SkillPatrol:
                return "순찰";

            case SkillTrackingInstinct:
                return "추적 본능";

            case SkillDrone:
                return "드론";

            case SkillReconnaissance:
                return "정찰";

            case SkillObservationSupport:
                return "관측 지원";

            case SkillPositionShare:
                return "위치 공유";

            case SkillBarricade:
                return "바리케이드";

            case SkillStopSignal:
                return "정지 신호";

            case SkillDemolition:
                return "철거";

            case SkillSafeZone:
                return "안전 구역";

            case SkillFakeBox:
                return "페이크 박스";

            case SkillJokerCard:
                return "조커 카드";

            case SkillVanishing:
                return "배니싱";

            case SkillMisdirection:
                return "미스디렉션";

            default:
                return skillName;
        }
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
        {
            return "에이전트 연결 없음";
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return "스킬 정보 없음";
        }

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsPositionShareSkill(normalizedSkillName))
        {
            Observer observer = targetAgent as Observer;

            if (observer == null)
            {
                return $"{GetSkillDisplayName(normalizedSkillName)}: 사용 불가";
            }

            string stateText = observer.IsTargetPositionShareEnabled ? "켜짐" : "꺼짐";
            return $"{GetSkillDisplayName(normalizedSkillName)}: {stateText}";
        }

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(normalizedSkillName);

        if (requiredGauge <= 0f)
        {
            return $"{GetSkillDisplayName(normalizedSkillName)}: 게이지 필요 없음";
        }

        float currentGauge = targetAgent.GetSkillGaugeCurrentForSkill(normalizedSkillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
        {
            return $"{GetSkillDisplayName(normalizedSkillName)}: {currentGauge:0.#} / {requiredGauge:0.#} 자동 발동";
        }

        return $"{GetSkillDisplayName(normalizedSkillName)}: {currentGauge:0.#} / {requiredGauge:0.#}";
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
        {
            return;
        }

        if (gaugeInfoLabelStyle == null)
        {
            gaugeInfoLabelStyle = new GUIStyle(GUI.skin.box);
            gaugeInfoLabelStyle.fontSize = 15;
            gaugeInfoLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        GUI.Box(GetGaugeInfoLabelRect(), gaugeInfoLabelText, gaugeInfoLabelStyle);
    }
}