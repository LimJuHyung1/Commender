using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AgentSkillGaugeUI : MonoBehaviour
{
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

    [Header("Skill Resolve")]
    [SerializeField] private int maxBasicSkillCount = AgentSkillLoadoutResolver.DefaultMaxBasicSkillCount;
    [SerializeField] private bool includeUnlockedSkill = true;
    [SerializeField] private int maxVisibleSkillCount = AgentSkillLoadoutResolver.DefaultMaxTotalSkillCount;
    [SerializeField] private bool refreshLoadoutInUpdate = true;
    [SerializeField] private float loadoutRefreshInterval = 0.25f;

    [Header("Optional Skill Database")]
    [SerializeField] private SkillDatabaseSO skillDatabase;

    [Header("Gauge Fill Setting")]
    [SerializeField] private bool setupImageFillSetting = true;
    [SerializeField] private GaugeFillDirection fillDirection = GaugeFillDirection.VerticalBottom;

    [Header("Toggle Skill Gauge")]
    [SerializeField] private float toggleSkillOnFillAmount = 1f;
    [SerializeField] private float toggleSkillOffFillAmount = 0f;

    [Header("Input Handler")]
    [SerializeField] private CommanderUIController commanderUIController;
    [SerializeField] private SkillDescriptionPopupUI skillDescriptionPopupUI;
    [SerializeField]
    private AgentSkillGaugeInputHandler.Settings inputHandlerSettings =
        new AgentSkillGaugeInputHandler.Settings();

    private readonly List<AgentSkillSlotUI> slots = new List<AgentSkillSlotUI>();
    private readonly List<AgentSkillLoadoutResolver.ResolvedSkillSlot> resolvedSkills =
        new List<AgentSkillLoadoutResolver.ResolvedSkillSlot>();

    private readonly List<AgentSkillGaugeInputHandler.SkillBinding> inputBindings =
        new List<AgentSkillGaugeInputHandler.SkillBinding>();

    private readonly List<string> manualSkillKeys = new List<string>();

    private AgentSkillGaugeInputHandler inputHandler;
    private float nextLoadoutRefreshTime;

    public AgentController TargetAgent => targetAgent;
    public int AgentId => agentId;

    public string Skill1Name => GetRuntimeSkillKeyAt(0);
    public string Skill2Name => GetRuntimeSkillKeyAt(1);
    public string Skill3Name => GetRuntimeSkillKeyAt(2);

    public bool CanUseSkill1 => CanUseSkillAt(0);
    public bool CanUseSkill2 => CanUseSkillAt(1);
    public bool CanUseSkill3 => CanUseSkillAt(2);

    private bool HasManualSkillKeys => manualSkillKeys.Count > 0;

    private void Awake()
    {
        CacheSlotsByComponent();
        RebuildSlotList();
        ConfigureSlotGauges();
        TryCacheCommanderUIController();
        TryCacheSkillDescriptionPopupUI();
        EnsureInputHandler();

        RefreshLoadout();
    }

    private void Start()
    {
        if (targetAgent == null && autoBindByAgentId)
            TryCacheTargetAgent();

        TryCacheCommanderUIController();
        TryCacheSkillDescriptionPopupUI();

        RefreshLoadout();
    }

    private void Update()
    {
        if (targetAgent == null && autoBindByAgentId && agentId >= 0)
        {
            TryCacheTargetAgent();

            if (targetAgent != null)
                RefreshLoadout();
        }

        TryCacheCommanderUIController();
        TryCacheSkillDescriptionPopupUI();

        RefreshLoadoutByInterval();
        RefreshGaugeAmountsOnly();

        EnsureInputHandler();
        inputHandler.Tick();
    }

    private void OnValidate()
    {
        maxBasicSkillCount = Mathf.Max(-1, maxBasicSkillCount);
        maxVisibleSkillCount = Mathf.Max(-1, maxVisibleSkillCount);
        loadoutRefreshInterval = Mathf.Max(0.02f, loadoutRefreshInterval);

        toggleSkillOnFillAmount = Mathf.Clamp01(toggleSkillOnFillAmount);
        toggleSkillOffFillAmount = Mathf.Clamp01(toggleSkillOffFillAmount);

        if (inputHandlerSettings != null)
            inputHandlerSettings.Validate();

        CacheSlotsByComponent();
        RebuildSlotList();
        ConfigureSlotGauges();
    }

    private void OnGUI()
    {
        if (inputHandler == null)
            return;

        inputHandler.DrawGaugeInfoLabel();
    }

    public void Bind(AgentController agent)
    {
        ClearManualSkillKeys();

        if (agent == null)
        {
            Unbind();
            return;
        }

        targetAgent = agent;
        agentId = agent.AgentID;

        EnsureInputHandler();
        inputHandler.SetTargetAgent(targetAgent, agentId);

        RefreshLoadout();
    }

    public void Bind(AgentController agent, string firstSkillName, string secondSkillName)
    {
        Bind(agent, firstSkillName, secondSkillName, "");
    }

    public void Bind(
        AgentController agent,
        string firstSkillName,
        string secondSkillName,
        string thirdSkillName)
    {
        if (agent == null)
        {
            Unbind();
            return;
        }

        targetAgent = agent;
        agentId = agent.AgentID;

        SetManualSkillKeys(firstSkillName, secondSkillName, thirdSkillName);

        EnsureInputHandler();
        inputHandler.SetTargetAgent(targetAgent, agentId);

        RefreshLoadout();
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

    public void BindByAgentId(int id, string firstSkillName, string secondSkillName)
    {
        BindByAgentId(id, firstSkillName, secondSkillName, "");
    }

    public void BindByAgentId(
        int id,
        string firstSkillName,
        string secondSkillName,
        string thirdSkillName)
    {
        agentId = id;
        targetAgent = null;

        SetManualSkillKeys(firstSkillName, secondSkillName, thirdSkillName);

        if (autoBindByAgentId)
            TryCacheTargetAgent();

        EnsureInputHandler();
        inputHandler.SetTargetAgent(targetAgent, agentId);

        RefreshLoadout();
    }

    public void Unbind()
    {
        targetAgent = null;
        agentId = -1;

        ClearManualSkillKeys();
        resolvedSkills.Clear();
        inputBindings.Clear();

        ClearAllSlots();

        EnsureInputHandler();
        inputHandler.SetTargetAgent(null, -1);
        inputHandler.ClearSkillBindings();
        inputHandler.ClearGaugeInfoLabel();
    }

    public void ClearAgentUI()
    {
        Unbind();
    }

    public void SetSkillNames(string firstSkillName, string secondSkillName)
    {
        SetSkillNames(firstSkillName, secondSkillName, "");
    }

    public void SetSkillNames(
        string firstSkillName,
        string secondSkillName,
        string thirdSkillName)
    {
        SetManualSkillKeys(firstSkillName, secondSkillName, thirdSkillName);
        RefreshLoadout();
    }

    public void Refresh()
    {
        RefreshLoadout();
        RefreshGaugeAmountsOnly();
    }

    public bool CanUseSkill(string skillName)
    {
        if (targetAgent == null)
            return false;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string runtimeSkillKey = skillName;

        if (TryResolveSkillDefinition(skillName, out SkillDefinitionSO skillDefinition))
            runtimeSkillKey = AgentSkillLoadoutResolver.GetRuntimeSkillKey(skillDefinition);

        return targetAgent.CanUseSkillGaugeForSkill(runtimeSkillKey);
    }

    public bool CanUseSkillAt(int index)
    {
        if (targetAgent == null)
            return false;

        if (!TryGetResolvedSkillAt(index, out AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot))
            return false;

        string runtimeSkillKey = skillSlot.RuntimeSkillKey;

        if (string.IsNullOrWhiteSpace(runtimeSkillKey))
            return false;

        return targetAgent.CanUseSkillGaugeForSkill(runtimeSkillKey);
    }

    public bool TryGetResolvedSkillAt(
        int index,
        out AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot)
    {
        skillSlot = null;

        if (index < 0 || index >= resolvedSkills.Count)
            return false;

        skillSlot = resolvedSkills[index];
        return skillSlot != null && skillSlot.HasSkill;
    }

    private void RefreshLoadoutByInterval()
    {
        if (!refreshLoadoutInUpdate)
            return;

        if (Time.unscaledTime < nextLoadoutRefreshTime)
            return;

        nextLoadoutRefreshTime = Time.unscaledTime + loadoutRefreshInterval;
        RefreshLoadout();
    }

    private void RefreshLoadout()
    {
        RebuildSlotList();
        EnsureInputHandler();

        resolvedSkills.Clear();

        if (targetAgent == null)
        {
            ClearAllSlots();
            ConfigureInputHandlerBindings();
            return;
        }

        List<AgentSkillLoadoutResolver.ResolvedSkillSlot> newSkills = ResolveSkills();

        for (int i = 0; i < newSkills.Count; i++)
        {
            AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = newSkills[i];

            if (skillSlot == null || !skillSlot.HasSkill)
                continue;

            if (maxVisibleSkillCount >= 0 && resolvedSkills.Count >= maxVisibleSkillCount)
                break;

            resolvedSkills.Add(skillSlot);
        }

        ApplyResolvedSkillsToSlots();
        ConfigureInputHandlerBindings();
    }

    private List<AgentSkillLoadoutResolver.ResolvedSkillSlot> ResolveSkills()
    {
        if (HasManualSkillKeys)
            return ResolveManualSkills();

        return AgentSkillLoadoutResolver.ResolveVisibleSkills(
            targetAgent,
            UpgradeManager.Instance,
            maxBasicSkillCount,
            includeUnlockedSkill,
            maxVisibleSkillCount
        );
    }

    private List<AgentSkillLoadoutResolver.ResolvedSkillSlot> ResolveManualSkills()
    {
        List<AgentSkillLoadoutResolver.ResolvedSkillSlot> result =
            new List<AgentSkillLoadoutResolver.ResolvedSkillSlot>();

        for (int i = 0; i < manualSkillKeys.Count; i++)
        {
            if (maxVisibleSkillCount >= 0 && result.Count >= maxVisibleSkillCount)
                break;

            string skillKey = manualSkillKeys[i];

            if (string.IsNullOrWhiteSpace(skillKey))
                continue;

            if (!TryResolveSkillDefinition(skillKey, out SkillDefinitionSO skillDefinition))
            {
                Debug.LogWarning(
                    $"[AgentSkillGaugeUI] SkillDefinitionSO를 찾을 수 없습니다. SkillKey: {skillKey}",
                    this
                );

                continue;
            }

            AgentSkillLoadoutResolver.ResolvedSkillSlot resolvedSkill =
                new AgentSkillLoadoutResolver.ResolvedSkillSlot(skillDefinition);

            if (AgentSkillLoadoutResolver.ContainsSkill(result, skillDefinition))
                continue;

            result.Add(resolvedSkill);
        }

        return result;
    }

    private bool TryResolveSkillDefinition(
        string skillKey,
        out SkillDefinitionSO skillDefinition)
    {
        skillDefinition = null;

        if (string.IsNullOrWhiteSpace(skillKey))
            return false;

        string trimmedSkillKey = skillKey.Trim();

        if (targetAgent != null)
        {
            if (targetAgent.TryGetSkillDefinitionByRuntimeKey(trimmedSkillKey, out skillDefinition))
                return true;

            if (targetAgent.TryGetSkillDefinitionById(trimmedSkillKey, out skillDefinition))
                return true;

            if (targetAgent.TryGetSkillDefinitionByCommandKeyword(trimmedSkillKey, out skillDefinition))
                return true;
        }

        AgentDefinitionSO definition = targetAgent != null ? targetAgent.AgentDefinition : null;

        if (definition != null)
        {
            if (definition.TryGetSkillByRuntimeKey(trimmedSkillKey, out skillDefinition))
                return true;

            if (definition.TryGetSkillById(trimmedSkillKey, out skillDefinition))
                return true;

            if (definition.TryGetSkillByCommandKeyword(trimmedSkillKey, out skillDefinition))
                return true;
        }

        if (skillDatabase != null)
        {
            if (skillDatabase.TryGetSkillByRuntimeKey(trimmedSkillKey, out skillDefinition))
                return true;

            if (skillDatabase.TryGetSkillById(trimmedSkillKey, out skillDefinition))
                return true;

            if (skillDatabase.TryGetSkillByCommandKeyword(trimmedSkillKey, out skillDefinition))
                return true;
        }

        return false;
    }

    private void ApplyResolvedSkillsToSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            AgentSkillSlotUI slot = slots[i];

            if (slot == null)
                continue;

            if (i >= resolvedSkills.Count)
            {
                ClearSlot(slot);
                continue;
            }

            AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = resolvedSkills[i];

            if (skillSlot == null || !skillSlot.HasSkill)
            {
                ClearSlot(slot);
                continue;
            }

            ApplySkillToSlot(slot, skillSlot);
        }
    }

    private void ApplySkillToSlot(
        AgentSkillSlotUI slot,
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot)
    {
        if (slot == null)
            return;

        if (skillSlot == null || !skillSlot.HasSkill)
        {
            ClearSlot(slot);
            return;
        }

        slot.SetVisible(true);
        slot.SetIcon(skillSlot.Icon);

        UpdateSkillGaugeSlot(slot, skillSlot);
    }

    private void RefreshGaugeAmountsOnly()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            AgentSkillSlotUI slot = slots[i];

            if (slot == null)
                continue;

            if (i >= resolvedSkills.Count)
            {
                slot.SetGaugeAmount(0f);
                continue;
            }

            AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = resolvedSkills[i];

            if (skillSlot == null || !skillSlot.HasSkill)
            {
                slot.SetGaugeAmount(0f);
                continue;
            }

            UpdateSkillGaugeSlot(slot, skillSlot);
        }
    }

    private void UpdateSkillGaugeSlot(
        AgentSkillSlotUI slot,
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot)
    {
        if (slot == null)
            return;

        if (targetAgent == null)
        {
            slot.SetGaugeAmount(0f);
            return;
        }

        if (skillSlot == null || !skillSlot.HasSkill)
        {
            slot.SetGaugeAmount(0f);
            return;
        }

        if (TryUpdateToggleSkillGaugeSlot(slot, skillSlot))
            return;

        string runtimeSkillKey = skillSlot.RuntimeSkillKey;

        if (string.IsNullOrWhiteSpace(runtimeSkillKey))
        {
            slot.SetGaugeAmount(0f);
            return;
        }

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(runtimeSkillKey);

        if (requiredGauge <= 0f)
        {
            slot.SetGaugeAmount(1f);
            return;
        }

        float amount = targetAgent.GetSkillGaugeNormalizedForSkill(runtimeSkillKey);
        slot.SetGaugeAmount(amount);
    }

    private bool TryUpdateToggleSkillGaugeSlot(
        AgentSkillSlotUI slot,
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot)
    {
        if (slot == null || skillSlot == null)
            return false;

        if (!IsPositionShareSkill(skillSlot))
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

    private bool IsPositionShareSkill(
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot)
    {
        if (skillSlot == null)
            return false;

        return IsSameSkillKey(skillSlot.SkillId, "position_share") ||
               IsSameSkillKey(skillSlot.SkillId, "positionshare") ||
               IsSameSkillKey(skillSlot.RuntimeSkillKey, "position_share") ||
               IsSameSkillKey(skillSlot.RuntimeSkillKey, "positionshare") ||
               IsSameSkillKey(skillSlot.RuntimeSkillKey, "positionshare_on") ||
               IsSameSkillKey(skillSlot.RuntimeSkillKey, "positionshare_off") ||
               IsSameSkillKey(skillSlot.CommandKeyword, "위치 공유");
    }

    private bool IsSameSkillKey(string source, string target)
    {
        return NormalizeSkillKey(source) == NormalizeSkillKey(target);
    }

    private string NormalizeSkillKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private void ConfigureInputHandlerBindings()
    {
        EnsureInputHandler();

        inputBindings.Clear();

        int count = Mathf.Min(slots.Count, resolvedSkills.Count);

        for (int i = 0; i < count; i++)
        {
            AgentSkillSlotUI slot = slots[i];
            AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = resolvedSkills[i];

            if (slot == null || skillSlot == null || !skillSlot.HasSkill)
                continue;

            inputBindings.Add(new AgentSkillGaugeInputHandler.SkillBinding(slot, skillSlot));
        }

        inputHandler.SetTargetAgent(targetAgent, agentId);
        inputHandler.SetCommanderUIController(commanderUIController);
        inputHandler.SetSkillDescriptionPopupUI(skillDescriptionPopupUI);
        inputHandler.SetSkillBindings(inputBindings);
    }

    private void EnsureInputHandler()
    {
        if (inputHandler == null)
        {
            inputHandler = new AgentSkillGaugeInputHandler(inputHandlerSettings);
            inputHandler.SetCanvasCameraProvider(GetCanvasCamera);
        }
        else
        {
            inputHandler.SetSettings(inputHandlerSettings);
        }

        inputHandler.SetTargetAgent(targetAgent, agentId);
        inputHandler.SetCommanderUIController(commanderUIController);
        inputHandler.SetSkillDescriptionPopupUI(skillDescriptionPopupUI);
    }

    private void ClearAllSlots()
    {
        RebuildSlotList();

        for (int i = 0; i < slots.Count; i++)
        {
            ClearSlot(slots[i]);
        }
    }

    private void ClearSlot(AgentSkillSlotUI slot)
    {
        if (slot == null)
            return;

        slot.ClearIcon();
        slot.SetGaugeAmount(0f);
        slot.SetVisible(false);
    }

    private void CacheSlotsByComponent()
    {
        if (!autoFindSlotsByComponent)
            return;

        AgentSkillSlotUI[] foundSlots = GetComponentsInChildren<AgentSkillSlotUI>(true);

        for (int i = 0; i < foundSlots.Length; i++)
        {
            AgentSkillSlotUI slot = foundSlots[i];

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

        AssignMissingSlotsByOrder(foundSlots);
    }

    private void AssignMissingSlotsByOrder(AgentSkillSlotUI[] foundSlots)
    {
        if (foundSlots == null || foundSlots.Length <= 0)
            return;

        int index = 0;

        if (skill1Slot == null)
            skill1Slot = GetSlotByOrder(foundSlots, ref index);

        if (skill2Slot == null)
            skill2Slot = GetSlotByOrder(foundSlots, ref index);

        if (skill3Slot == null)
            skill3Slot = GetSlotByOrder(foundSlots, ref index);
    }

    private AgentSkillSlotUI GetSlotByOrder(
        AgentSkillSlotUI[] foundSlots,
        ref int index)
    {
        while (index < foundSlots.Length)
        {
            AgentSkillSlotUI slot = foundSlots[index];
            index++;

            if (slot == null)
                continue;

            if (slot == skill1Slot || slot == skill2Slot || slot == skill3Slot)
                continue;

            return slot;
        }

        return null;
    }

    private void RebuildSlotList()
    {
        slots.Clear();

        AddSlotIfValid(skill1Slot);
        AddSlotIfValid(skill2Slot);
        AddSlotIfValid(skill3Slot);
    }

    private void AddSlotIfValid(AgentSkillSlotUI slot)
    {
        if (slot == null)
            return;

        if (slots.Contains(slot))
            return;

        slots.Add(slot);
    }

    private void ConfigureSlotGauges()
    {
        if (!setupImageFillSetting)
            return;

        GetFillSetting(out Image.FillMethod fillMethod, out int fillOrigin);

        ConfigureSlotGauge(skill1Slot, fillMethod, fillOrigin);
        ConfigureSlotGauge(skill2Slot, fillMethod, fillOrigin);
        ConfigureSlotGauge(skill3Slot, fillMethod, fillOrigin);
    }

    private void ConfigureSlotGauge(
        AgentSkillSlotUI slot,
        Image.FillMethod fillMethod,
        int fillOrigin)
    {
        if (slot == null)
            return;

        slot.ConfigureGauge(fillMethod, fillOrigin);
    }

    private void GetFillSetting(
        out Image.FillMethod fillMethod,
        out int fillOrigin)
    {
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

            case GaugeFillDirection.VerticalTop:
                fillMethod = Image.FillMethod.Vertical;
                fillOrigin = (int)Image.OriginVertical.Top;
                break;

            case GaugeFillDirection.VerticalBottom:
            default:
                fillMethod = Image.FillMethod.Vertical;
                fillOrigin = (int)Image.OriginVertical.Bottom;
                break;
        }
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
            break;
        }
    }

    private void TryCacheCommanderUIController()
    {
        if (commanderUIController != null)
            return;

        if (inputHandlerSettings == null ||
            !inputHandlerSettings.AutoFindCommanderUIController)
        {
            return;
        }

        commanderUIController = FindFirstObjectByType<CommanderUIController>();
    }

    private void TryCacheSkillDescriptionPopupUI()
    {
        if (skillDescriptionPopupUI != null)
            return;

        if (inputHandlerSettings == null ||
            !inputHandlerSettings.AutoFindSkillDescriptionPopupUI)
        {
            return;
        }

        skillDescriptionPopupUI =
            FindFirstObjectByType<SkillDescriptionPopupUI>(FindObjectsInactive.Include);
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

    private string GetRuntimeSkillKeyAt(int index)
    {
        if (!TryGetResolvedSkillAt(index, out AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot))
            return "";

        return skillSlot.RuntimeSkillKey;
    }

    private void SetManualSkillKeys(
        string firstSkillName,
        string secondSkillName,
        string thirdSkillName)
    {
        manualSkillKeys.Clear();

        AddManualSkillKey(firstSkillName);
        AddManualSkillKey(secondSkillName);
        AddManualSkillKey(thirdSkillName);
    }

    private void AddManualSkillKey(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string trimmedSkillName = skillName.Trim();

        if (manualSkillKeys.Contains(trimmedSkillName))
            return;

        manualSkillKeys.Add(trimmedSkillName);
    }

    private void ClearManualSkillKeys()
    {
        manualSkillKeys.Clear();
    }
}