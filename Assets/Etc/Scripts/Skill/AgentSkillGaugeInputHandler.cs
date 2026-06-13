using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class AgentSkillGaugeInputHandler
{
    [Serializable]
    public sealed class Settings
    {
        [Header("Gauge Info Label")]
        [SerializeField] private bool showGaugeInfoOnSkillClick = true;
        [SerializeField] private float gaugeInfoLabelDuration = 1.5f;
        [SerializeField] private Vector2 gaugeInfoLabelOffset = new Vector2(16f, -24f);
        [SerializeField] private Vector2 gaugeInfoLabelSize = new Vector2(360f, 64f);

        [Header("Skill Name Paste")]
        [SerializeField] private bool pasteSkillNameOnFunctionKeyClick = true;
        [SerializeField] private bool pasteOnlyWhenFunctionKeyInputMatchesClickedAgent = true;
        [SerializeField] private bool pasteSkillNameOnDoubleClick = true;
        [SerializeField] private float doubleClickMaxInterval = 0.3f;
        [SerializeField] private bool showGaugeInfoAfterSkillPaste;

        [Header("Skill Description Popup")]
        [SerializeField] private bool showSkillDescriptionOnRightClick = true;

        [Header("Auto Find References")]
        [SerializeField] private bool autoFindCommanderUIController = true;
        [SerializeField] private bool autoFindSkillDescriptionPopupUI = true;

        public bool ShowGaugeInfoOnSkillClick
        {
            get => showGaugeInfoOnSkillClick;
            set => showGaugeInfoOnSkillClick = value;
        }

        public float GaugeInfoLabelDuration
        {
            get => gaugeInfoLabelDuration;
            set => gaugeInfoLabelDuration = Mathf.Max(0f, value);
        }

        public Vector2 GaugeInfoLabelOffset
        {
            get => gaugeInfoLabelOffset;
            set => gaugeInfoLabelOffset = value;
        }

        public Vector2 GaugeInfoLabelSize
        {
            get => gaugeInfoLabelSize;
            set
            {
                gaugeInfoLabelSize = value;
                gaugeInfoLabelSize.x = Mathf.Max(1f, gaugeInfoLabelSize.x);
                gaugeInfoLabelSize.y = Mathf.Max(1f, gaugeInfoLabelSize.y);
            }
        }

        public bool PasteSkillNameOnFunctionKeyClick
        {
            get => pasteSkillNameOnFunctionKeyClick;
            set => pasteSkillNameOnFunctionKeyClick = value;
        }

        public bool PasteOnlyWhenFunctionKeyInputMatchesClickedAgent
        {
            get => pasteOnlyWhenFunctionKeyInputMatchesClickedAgent;
            set => pasteOnlyWhenFunctionKeyInputMatchesClickedAgent = value;
        }

        public bool PasteSkillNameOnDoubleClick
        {
            get => pasteSkillNameOnDoubleClick;
            set => pasteSkillNameOnDoubleClick = value;
        }

        public float DoubleClickMaxInterval
        {
            get => doubleClickMaxInterval;
            set => doubleClickMaxInterval = Mathf.Max(0.05f, value);
        }

        public bool ShowGaugeInfoAfterSkillPaste
        {
            get => showGaugeInfoAfterSkillPaste;
            set => showGaugeInfoAfterSkillPaste = value;
        }

        public bool ShowSkillDescriptionOnRightClick
        {
            get => showSkillDescriptionOnRightClick;
            set => showSkillDescriptionOnRightClick = value;
        }

        public bool AutoFindCommanderUIController
        {
            get => autoFindCommanderUIController;
            set => autoFindCommanderUIController = value;
        }

        public bool AutoFindSkillDescriptionPopupUI
        {
            get => autoFindSkillDescriptionPopupUI;
            set => autoFindSkillDescriptionPopupUI = value;
        }

        public void Validate()
        {
            GaugeInfoLabelDuration = gaugeInfoLabelDuration;
            GaugeInfoLabelSize = gaugeInfoLabelSize;
            DoubleClickMaxInterval = doubleClickMaxInterval;
        }
    }

    public sealed class SkillBinding
    {
        public AgentSkillSlotUI Slot { get; }
        public AgentSkillLoadoutResolver.ResolvedSkillSlot SkillSlot { get; }

        public SkillDefinitionSO SkillDefinition
        {
            get
            {
                return SkillSlot != null ? SkillSlot.SkillDefinition : null;
            }
        }

        public bool HasValidSkill
        {
            get
            {
                return Slot != null && SkillSlot != null && SkillSlot.HasSkill;
            }
        }

        public SkillBinding(
            AgentSkillSlotUI slot,
            AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot)
        {
            Slot = slot;
            SkillSlot = skillSlot;
        }
    }

    private readonly List<SkillBinding> skillBindings = new List<SkillBinding>();

    private Settings settings;

    private AgentController targetAgent;
    private int agentId = -1;

    private CommanderUIController commanderUIController;
    private SkillDescriptionPopupUI skillDescriptionPopupUI;

    private Camera canvasCamera;
    private Func<Camera> canvasCameraProvider;

    private AgentSkillSlotUI lastClickedSkillSlot;
    private float lastSkillClickTime = -999f;

    private string gaugeInfoLabelText = "";
    private Vector2 gaugeInfoLabelScreenPosition;
    private float gaugeInfoLabelEndTime = -1f;
    private GUIStyle gaugeInfoLabelStyle;

    public Settings HandlerSettings => settings;

    public bool HasGaugeInfoLabelVisible
    {
        get
        {
            return IsGaugeInfoLabelVisible();
        }
    }

    public string CurrentGaugeInfoLabelText => gaugeInfoLabelText;

    public AgentSkillGaugeInputHandler()
        : this(new Settings())
    {
    }

    public AgentSkillGaugeInputHandler(Settings settings)
    {
        SetSettings(settings);
    }

    public void SetSettings(Settings newSettings)
    {
        settings = newSettings ?? new Settings();
        settings.Validate();
    }

    public void SetTargetAgent(AgentController agent)
    {
        int nextAgentId = agent != null ? agent.AgentID : -1;
        bool changed = targetAgent != agent || agentId != nextAgentId;

        targetAgent = agent;
        agentId = nextAgentId;

        if (!changed)
            return;

        ResetLastSkillIconClick();
        ClearGaugeInfoLabel();
    }

    public void SetTargetAgent(AgentController agent, int fallbackAgentId)
    {
        int nextAgentId = agent != null ? agent.AgentID : fallbackAgentId;
        bool changed = targetAgent != agent || agentId != nextAgentId;

        targetAgent = agent;
        agentId = nextAgentId;

        if (!changed)
            return;

        ResetLastSkillIconClick();
        ClearGaugeInfoLabel();
    }

    public void SetAgentId(int id)
    {
        agentId = id;
    }

    public void SetCommanderUIController(CommanderUIController controller)
    {
        commanderUIController = controller;
    }

    public void SetSkillDescriptionPopupUI(SkillDescriptionPopupUI popupUI)
    {
        skillDescriptionPopupUI = popupUI;
    }

    public void SetCanvasCamera(Camera camera)
    {
        canvasCamera = camera;
    }

    public void SetCanvasCameraProvider(Func<Camera> provider)
    {
        canvasCameraProvider = provider;
    }

    public void ClearSkillBindings()
    {
        skillBindings.Clear();
        ResetLastSkillIconClick();
    }

    public void SetSkillBindings(IReadOnlyList<SkillBinding> bindings)
    {
        skillBindings.Clear();

        if (bindings == null)
            return;

        for (int i = 0; i < bindings.Count; i++)
        {
            SkillBinding binding = bindings[i];

            if (binding == null)
                continue;

            if (!binding.HasValidSkill)
                continue;

            skillBindings.Add(binding);
        }

        // ResetLastSkillIconClick();
    }

    public void SetSkillBindings(
        IReadOnlyList<AgentSkillSlotUI> slots,
        IReadOnlyList<AgentSkillLoadoutResolver.ResolvedSkillSlot> resolvedSkills)
    {
        skillBindings.Clear();

        if (slots == null || resolvedSkills == null)
            return;

        int count = Mathf.Min(slots.Count, resolvedSkills.Count);

        for (int i = 0; i < count; i++)
        {
            AgentSkillSlotUI slot = slots[i];
            AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = resolvedSkills[i];

            if (slot == null || skillSlot == null || !skillSlot.HasSkill)
                continue;

            skillBindings.Add(new SkillBinding(slot, skillSlot));
        }

        // ResetLastSkillIconClick();
    }

    public void Tick()
    {
        if (settings == null)
            SetSettings(null);

        HandleSkillGaugeInfoClick();
        HandleSkillDescriptionRightClick();
    }

    public void ClearGaugeInfoLabel()
    {
        gaugeInfoLabelText = "";
        gaugeInfoLabelEndTime = -1f;
    }

    public void DrawGaugeInfoLabel()
    {
        if (!IsGaugeInfoLabelVisible())
            return;

        if (gaugeInfoLabelStyle == null)
        {
            gaugeInfoLabelStyle = new GUIStyle(GUI.skin.box);
            gaugeInfoLabelStyle.fontSize = 14;
            gaugeInfoLabelStyle.alignment = TextAnchor.MiddleLeft;
            gaugeInfoLabelStyle.wordWrap = true;
            gaugeInfoLabelStyle.padding = new RectOffset(10, 10, 6, 6);
        }

        GUI.Box(GetGaugeInfoLabelRect(), gaugeInfoLabelText, gaugeInfoLabelStyle);
    }

    private void HandleSkillGaugeInfoClick()
    {
        if (!settings.ShowGaugeInfoOnSkillClick &&
            !settings.PasteSkillNameOnFunctionKeyClick &&
            !settings.PasteSkillNameOnDoubleClick)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.leftButton.wasReleasedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (!TryGetClickedSkillBinding(mousePosition, out SkillBinding clickedBinding))
            return;

        HandleSkillIconLeftClick(clickedBinding, mousePosition);
    }

    private void HandleSkillDescriptionRightClick()
    {
        if (!settings.ShowSkillDescriptionOnRightClick)
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.rightButton.wasReleasedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (!TryGetClickedSkillBinding(mousePosition, out SkillBinding clickedBinding))
            return;

        ShowSkillDescriptionPopup(clickedBinding, mousePosition);
    }

    private bool TryGetClickedSkillBinding(
        Vector2 mousePosition,
        out SkillBinding clickedBinding)
    {
        clickedBinding = null;

        for (int i = 0; i < skillBindings.Count; i++)
        {
            SkillBinding binding = skillBindings[i];

            if (binding == null || !binding.HasValidSkill)
                continue;

            if (!binding.Slot.IsVisible)
                continue;

            if (!IsSlotClicked(binding.Slot, mousePosition))
                continue;

            clickedBinding = binding;
            return true;
        }

        return false;
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
        if (canvasCameraProvider != null)
            return canvasCameraProvider();

        return canvasCamera;
    }

    private void HandleSkillIconLeftClick(
        SkillBinding clickedBinding,
        Vector2 mousePosition)
    {
        if (clickedBinding == null || !clickedBinding.HasValidSkill)
            return;

        bool pasted = false;

        if (IsSkillIconDoubleClick(clickedBinding.Slot))
        {
            pasted = TryPasteSkillNameToOwnInput(clickedBinding);
            ResetLastSkillIconClick();
        }
        else
        {
            RegisterSkillIconClick(clickedBinding.Slot);
            pasted = TryPasteSkillNameToFunctionKeyInput(clickedBinding);
        }

        if (pasted && !settings.ShowGaugeInfoAfterSkillPaste)
            return;

        if (settings.ShowGaugeInfoOnSkillClick)
            ShowGaugeInfoLabel(clickedBinding, mousePosition);
    }

    private bool IsSkillIconDoubleClick(AgentSkillSlotUI clickedSlot)
    {
        if (!settings.PasteSkillNameOnDoubleClick)
            return false;

        if (clickedSlot == null)
            return false;

        if (lastClickedSkillSlot != clickedSlot)
            return false;

        float elapsedTime = Time.unscaledTime - lastSkillClickTime;
        return elapsedTime <= settings.DoubleClickMaxInterval;
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

    private bool TryPasteSkillNameToOwnInput(SkillBinding binding)
    {
        if (!settings.PasteSkillNameOnDoubleClick)
            return false;

        if (!CanPasteSkillToInput(binding))
            return false;

        TryCacheCommanderUIControllerIfNeeded();

        if (commanderUIController == null)
            return false;

        int clickedAgentId = GetClickedAgentId();

        if (clickedAgentId < 0)
            return false;

        string pasteText = GetSkillPasteText(binding);

        if (string.IsNullOrWhiteSpace(pasteText))
            return false;

        return commanderUIController.TryPasteSkillDisplayNameToAgentInput(
            clickedAgentId,
            pasteText
        );
    }

    private bool TryPasteSkillNameToFunctionKeyInput(SkillBinding binding)
    {
        if (!settings.PasteSkillNameOnFunctionKeyClick)
            return false;

        if (!CanPasteSkillToInput(binding))
            return false;

        if (!TryGetHeldFunctionKeyInputIndex(out int inputIndex))
            return false;

        TryCacheCommanderUIControllerIfNeeded();

        if (commanderUIController == null)
            return false;

        int clickedAgentId = GetClickedAgentId();

        if (clickedAgentId < 0)
            return false;

        string pasteText = GetSkillPasteText(binding);

        if (string.IsNullOrWhiteSpace(pasteText))
            return false;

        return commanderUIController.TryPasteSkillDisplayNameToInputIndex(
            inputIndex,
            clickedAgentId,
            pasteText,
            settings.PasteOnlyWhenFunctionKeyInputMatchesClickedAgent
        );
    }

    private bool CanPasteSkillToInput(SkillBinding binding)
    {
        if (binding == null || !binding.HasValidSkill)
            return false;

        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = binding.SkillSlot;

        if (skillSlot == null)
            return false;

        return skillSlot.CanPasteToInput;
    }

    private string GetSkillPasteText(SkillBinding binding)
    {
        if (binding == null || binding.SkillSlot == null)
            return "";

        if (!string.IsNullOrWhiteSpace(binding.SkillSlot.DisplayName))
            return binding.SkillSlot.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(binding.SkillSlot.CommandKeyword))
            return binding.SkillSlot.CommandKeyword.Trim();

        if (!string.IsNullOrWhiteSpace(binding.SkillSlot.SkillId))
            return binding.SkillSlot.SkillId.Trim();

        return "";
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

    private void ShowSkillDescriptionPopup(
        SkillBinding binding,
        Vector2 mousePosition)
    {
        if (binding == null || !binding.HasValidSkill)
            return;

        TryCacheSkillDescriptionPopupUIIfNeeded();

        if (skillDescriptionPopupUI == null)
            return;

        SkillDefinitionSO skillDefinition = binding.SkillDefinition;

        if (skillDefinition == null)
            return;

        skillDescriptionPopupUI.Show(skillDefinition, mousePosition);
    }

    private void ShowGaugeInfoLabel(
        SkillBinding binding,
        Vector2 mousePosition)
    {
        gaugeInfoLabelText = GetGaugeInfoText(binding);
        gaugeInfoLabelScreenPosition = mousePosition + settings.GaugeInfoLabelOffset;
        gaugeInfoLabelEndTime = Time.unscaledTime + settings.GaugeInfoLabelDuration;

        Debug.Log($"[AgentSkillGaugeInputHandler] {gaugeInfoLabelText}");
    }

    private string GetGaugeInfoText(SkillBinding binding)
    {
        if (targetAgent == null)
            return "에이전트 연결 없음";

        if (binding == null || !binding.HasValidSkill)
            return "스킬 정보 없음";

        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot = binding.SkillSlot;

        if (skillSlot == null || skillSlot.SkillDefinition == null)
            return "스킬 정보 없음";

        string displayName = skillSlot.DisplayName;
        string runtimeSkillKey = skillSlot.RuntimeSkillKey;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "스킬";

        if (TryGetSpecialGaugeInfoText(skillSlot, out string specialText))
            return specialText;

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(runtimeSkillKey);

        if (requiredGauge <= 0f)
            return $"{displayName}: 게이지 필요 없음";

        float currentGauge = targetAgent.GetSkillGaugeCurrentForSkill(runtimeSkillKey);

        SkillDefinitionSO skillDefinition = skillSlot.SkillDefinition;

        if (skillDefinition.IsAutoActivatedSkill)
            return $"{displayName}: {currentGauge:0.#} / {requiredGauge:0.#} 자동 발동";

        if (skillDefinition.IsPassiveSkill)
            return $"{displayName}: 패시브";

        if (skillDefinition.IsToggleSkill)
            return $"{displayName}: {currentGauge:0.#} / {requiredGauge:0.#} 토글";

        return $"{displayName}: {currentGauge:0.#} / {requiredGauge:0.#}";
    }

    private bool TryGetSpecialGaugeInfoText(
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot,
        out string text)
    {
        text = "";

        if (skillSlot == null || skillSlot.SkillDefinition == null)
            return false;

        if (TryGetPositionShareGaugeInfoText(skillSlot, out text))
            return true;

        if (TryGetDogHandlerGaugeInfoText(skillSlot, out text))
            return true;

        return false;
    }

    private bool TryGetPositionShareGaugeInfoText(
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot,
        out string text)
    {
        text = "";

        if (!IsSkill(skillSlot, "position_share", "positionshare", "positionshare_on", "positionshare_off"))
            return false;

        string displayName = skillSlot.DisplayName;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "위치 공유";

        Observer observer = targetAgent as Observer;

        if (observer == null)
        {
            text = $"{displayName}: 사용 불가";
            return true;
        }

        string stateText = observer.IsTargetPositionShareEnabled ? "켜짐" : "꺼짐";
        text = $"{displayName}: {stateText}";
        return true;
    }

    private bool TryGetDogHandlerGaugeInfoText(
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot,
        out string text)
    {
        text = "";

        DogHandler dogHandler = targetAgent as DogHandler;

        if (IsSkill(skillSlot, "dog_deploy", "dogdeploy"))
        {
            text = "탐지견 배치\n게이지 필요 없음 / 좌표 + 배치로 사용";
            return true;
        }

        if (IsSkill(skillSlot, "guard_instinct", "guardinstinct"))
        {
            if (dogHandler == null)
            {
                text = "경계 본능\n패시브 / 탐지견이 타겟 발견 시 스택 증가";
                return true;
            }

            text =
                "경계 본능\n" +
                $"스택 {dogHandler.GuardInstinctStack}/{dogHandler.GuardInstinctMaxStack} / " +
                $"이동속도 x{dogHandler.GuardInstinctMoveSpeedMultiplier:0.##}";

            return true;
        }

        if (IsSkill(skillSlot, "treat", "dog_treat", "dogtreat"))
        {
            float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(skillSlot.RuntimeSkillKey);
            float currentGauge = targetAgent.GetSkillGaugeCurrentForSkill(skillSlot.RuntimeSkillKey);

            if (requiredGauge <= 0f)
            {
                text = "간식\n탐지견 이동속도, 시야 거리, 시야각 강화";
                return true;
            }

            text =
                "간식\n" +
                $"{currentGauge:0.#}/{requiredGauge:0.#} / 20초 동안 탐지견 강화";

            return true;
        }

        if (IsSkill(skillSlot, "off_leash", "offleash"))
        {
            float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(skillSlot.RuntimeSkillKey);
            float currentGauge = targetAgent.GetSkillGaugeCurrentForSkill(skillSlot.RuntimeSkillKey);

            if (requiredGauge <= 0f)
            {
                text = "오프리쉬\n탐지견이 맵 전체를 자유 수색";
                return true;
            }

            text =
                "오프리쉬\n" +
                $"{currentGauge:0.#}/{requiredGauge:0.#} / 맵 전체 자유 수색";

            return true;
        }

        return false;
    }

    private bool IsSkill(
        AgentSkillLoadoutResolver.ResolvedSkillSlot skillSlot,
        params string[] compareKeys)
    {
        if (skillSlot == null || compareKeys == null)
            return false;

        string skillId = NormalizeSkillKey(skillSlot.SkillId);
        string runtimeSkillKey = NormalizeSkillKey(skillSlot.RuntimeSkillKey);
        string displayName = NormalizeSkillKey(skillSlot.DisplayName);
        string commandKeyword = NormalizeSkillKey(skillSlot.CommandKeyword);

        SkillDefinitionSO skillDefinition = skillSlot.SkillDefinition;

        for (int i = 0; i < compareKeys.Length; i++)
        {
            string compareKey = NormalizeSkillKey(compareKeys[i]);

            if (string.IsNullOrWhiteSpace(compareKey))
                continue;

            if (skillId == compareKey)
                return true;

            if (runtimeSkillKey == compareKey)
                return true;

            if (displayName == compareKey)
                return true;

            if (commandKeyword == compareKey)
                return true;

            if (skillDefinition != null)
            {
                if (skillDefinition.MatchesSkillId(compareKeys[i]))
                    return true;

                if (skillDefinition.MatchesRuntimeSkillKey(compareKeys[i]))
                    return true;

                if (skillDefinition.HasCommandKeyword(compareKeys[i]))
                    return true;
            }
        }

        return false;
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

    private int GetClickedAgentId()
    {
        if (targetAgent != null)
            return targetAgent.AgentID;

        return agentId;
    }

    private void TryCacheCommanderUIControllerIfNeeded()
    {
        if (commanderUIController != null)
            return;

        if (!settings.AutoFindCommanderUIController)
            return;

        commanderUIController = UnityEngine.Object.FindFirstObjectByType<CommanderUIController>();
    }

    private void TryCacheSkillDescriptionPopupUIIfNeeded()
    {
        if (skillDescriptionPopupUI != null)
            return;

        if (!settings.AutoFindSkillDescriptionPopupUI)
            return;

        skillDescriptionPopupUI =
            UnityEngine.Object.FindFirstObjectByType<SkillDescriptionPopupUI>(
                FindObjectsInactive.Include
            );
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
            settings.GaugeInfoLabelSize.x,
            settings.GaugeInfoLabelSize.y
        );
    }
}