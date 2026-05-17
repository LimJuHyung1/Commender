using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AgentSkillGaugeUI : MonoBehaviour
{
    private const string SkillDash = "dash";
    private const string SkillSmoke = "smoke";

    private const string SkillAccessControl = "accesscontrol";
    private const string SkillEscapeBlock = "escapeblock";

    private const string SkillDrone = "drone";
    private const string SkillPositionShare = "positionshare";
    private const string SkillPositionShareOn = "positionshare_on";
    private const string SkillPositionShareOff = "positionshare_off";

    private const string SkillBarricade = "barricade";
    private const string SkillStopSignal = "stopsignal";
    private const string SkillSlowTrap = "slowtrap";

    private const string SkillFakeBox = "fakebox";
    private const string SkillJokerCard = "jokercard";

    private const string LegacySkillNoisemaker = "noisemaker";
    private const string LegacySkillHologram = "hologram";

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

    [Header("Skill Names")]
    [SerializeField] private string skill1Name = "";
    [SerializeField] private string skill2Name = "";

    [Header("Gauge Images")]
    [SerializeField] private Image skill1GaugeImage;
    [SerializeField] private Image skill2GaugeImage;
    [SerializeField] private bool autoFindGaugeImages = true;

    [Header("Click Area")]
    [SerializeField] private RectTransform skill1ClickArea;
    [SerializeField] private RectTransform skill2ClickArea;

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
    [SerializeField] private bool showGaugeInfoAfterSkillPaste = false;
    [SerializeField] private bool autoFindCommanderUIController = true;

    private bool hasCachedGaugeImages;

    private string gaugeInfoLabelText = "";
    private Vector2 gaugeInfoLabelScreenPosition;
    private float gaugeInfoLabelEndTime = -1f;
    private GUIStyle gaugeInfoLabelStyle;

    public AgentController TargetAgent => targetAgent;
    public int AgentId => agentId;
    public string Skill1Name => skill1Name;
    public string Skill2Name => skill2Name;

    public bool CanUseSkill1 => CanUseSkill(skill1Name);
    public bool CanUseSkill2 => CanUseSkill(skill2Name);

    private void Awake()
    {
        TrySetupByUIName();
        TryCacheCommanderUIController();
        EnsureGaugeImages();
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
    }

    private void OnValidate()
    {
        gaugeInfoLabelDuration = Mathf.Max(0f, gaugeInfoLabelDuration);
        gaugeInfoLabelSize.x = Mathf.Max(1f, gaugeInfoLabelSize.x);
        gaugeInfoLabelSize.y = Mathf.Max(1f, gaugeInfoLabelSize.y);

        toggleSkillOnFillAmount = Mathf.Clamp01(toggleSkillOnFillAmount);
        toggleSkillOffFillAmount = Mathf.Clamp01(toggleSkillOffFillAmount);

        skill1Name = NormalizeSkillName(skill1Name);
        skill2Name = NormalizeSkillName(skill2Name);

        hasCachedGaugeImages = false;
    }

    public void Bind(AgentController agent)
    {
        if (agent == null)
        {
            Unbind();
            return;
        }

        SetupSkillNamesByAgent(agent);
        Bind(agent, skill1Name, skill2Name);
    }

    public void Bind(AgentController agent, string firstSkillName, string secondSkillName)
    {
        targetAgent = agent;
        agentId = agent != null ? agent.AgentID : -1;

        skill1Name = NormalizeSkillName(firstSkillName);
        skill2Name = NormalizeSkillName(secondSkillName);

        if (targetAgent != null &&
            string.IsNullOrWhiteSpace(skill1Name) &&
            string.IsNullOrWhiteSpace(skill2Name))
        {
            SetupSkillNamesByAgent(targetAgent);
        }

        EnsureGaugeImages();
        Refresh();
    }

    public void BindByAgentId(int id, string firstSkillName, string secondSkillName)
    {
        agentId = id;
        skill1Name = NormalizeSkillName(firstSkillName);
        skill2Name = NormalizeSkillName(secondSkillName);

        targetAgent = null;

        if (autoBindByAgentId)
            TryCacheTargetAgent();

        if (targetAgent != null &&
            string.IsNullOrWhiteSpace(skill1Name) &&
            string.IsNullOrWhiteSpace(skill2Name))
        {
            SetupSkillNamesByAgent(targetAgent);
        }

        EnsureGaugeImages();
        Refresh();
    }

    public void Unbind()
    {
        targetAgent = null;
        agentId = -1;
        skill1Name = "";
        skill2Name = "";

        Refresh();
    }

    public void SetSkillNames(string firstSkillName, string secondSkillName)
    {
        skill1Name = NormalizeSkillName(firstSkillName);
        skill2Name = NormalizeSkillName(secondSkillName);

        Refresh();
    }

    public void Refresh()
    {
        EnsureGaugeImages();

        if (targetAgent == null)
        {
            SetGaugeAmount(skill1GaugeImage, 0f);
            SetGaugeAmount(skill2GaugeImage, 0f);
            return;
        }

        UpdateSkillGaugeImage(skill1GaugeImage, skill1Name);
        UpdateSkillGaugeImage(skill2GaugeImage, skill2Name);
    }

    public bool CanUseSkill(string skillName)
    {
        if (targetAgent == null)
            return false;

        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (IsPositionShareSkill(normalizedSkillName))
            return targetAgent is Observer;

        return targetAgent.CanUseSkillGaugeForSkill(normalizedSkillName);
    }

    private void HandleSkillGaugeInfoClick()
    {
        if (!showGaugeInfoOnSkillClick && !pasteSkillNameOnFunctionKeyClick)
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.leftButton.wasReleasedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (IsScreenPointInside(skill1ClickArea, mousePosition))
        {
            HandleSkillIconClick(skill1Name, mousePosition);
            return;
        }

        if (IsScreenPointInside(skill2ClickArea, mousePosition))
            HandleSkillIconClick(skill2Name, mousePosition);
    }

    private void HandleSkillIconClick(string skillName, Vector2 mousePosition)
    {
        bool pasted = TryPasteSkillNameToFunctionKeyInput(skillName);

        if (pasted && !showGaugeInfoAfterSkillPaste)
            return;

        if (showGaugeInfoOnSkillClick)
            ShowGaugeInfoLabel(skillName, mousePosition);
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
        string displayName = GetSkillDisplayName(normalizedSkillName);

        return commanderUIController.TryPasteSkillDisplayNameToInputIndex(
            inputIndex,
            clickedAgentId,
            displayName,
            pasteOnlyWhenFunctionKeyInputMatchesClickedAgent
        );
    }

    private bool IsAutoActivatedSkill(string skillName)
    {
        return NormalizeSkillName(skillName) == SkillJokerCard;
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

    private void TryCacheCommanderUIController()
    {
        if (commanderUIController != null)
            return;

        commanderUIController = FindFirstObjectByType<CommanderUIController>();
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

        if (IsPositionShareSkill(normalizedSkillName))
        {
            Observer observer = targetAgent as Observer;

            if (observer == null)
                return $"{GetSkillDisplayName(normalizedSkillName)}: 사용 불가";

            string stateText = observer.IsTargetPositionShareEnabled ? "켜짐" : "꺼짐";
            return $"{GetSkillDisplayName(normalizedSkillName)}: {stateText}";
        }

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(normalizedSkillName);

        if (requiredGauge <= 0f)
            return $"{GetSkillDisplayName(normalizedSkillName)}: 게이지 필요 없음";

        float currentGauge = targetAgent.GetSkillGaugeCurrentForSkill(normalizedSkillName);

        if (IsAutoActivatedSkill(normalizedSkillName))
            return $"{GetSkillDisplayName(normalizedSkillName)}: {currentGauge:0.#} / {requiredGauge:0.#} 자동 발동";

        return $"{GetSkillDisplayName(normalizedSkillName)}: {currentGauge:0.#} / {requiredGauge:0.#}";
    }

    private bool IsScreenPointInside(RectTransform rectTransform, Vector2 screenPosition)
    {
        if (rectTransform == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
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

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private void EnsureGaugeImages()
    {
        if (hasCachedGaugeImages)
            return;

        if (autoFindGaugeImages)
            CacheGaugeImages();

        SetupGaugeImage(skill1GaugeImage);
        SetupGaugeImage(skill2GaugeImage);

        hasCachedGaugeImages = true;
    }

    private void CacheGaugeImages()
    {
        FindSkillRoots(out Transform skill1Root, out Transform skill2Root);

        if (skill1ClickArea == null && skill1Root != null)
            skill1ClickArea = skill1Root as RectTransform;

        if (skill2ClickArea == null && skill2Root != null)
            skill2ClickArea = skill2Root as RectTransform;

        if (skill1GaugeImage == null)
            skill1GaugeImage = FindGaugeImage(skill1Root);

        if (skill2GaugeImage == null)
            skill2GaugeImage = FindGaugeImage(skill2Root);
    }

    private void FindSkillRoots(out Transform skill1Root, out Transform skill2Root)
    {
        skill1Root = null;
        skill2Root = null;

        int foundCount = 0;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (!child.name.StartsWith("Skill", StringComparison.OrdinalIgnoreCase))
                continue;

            if (foundCount == 0)
                skill1Root = child;
            else if (foundCount == 1)
                skill2Root = child;

            foundCount++;

            if (foundCount >= 2)
                break;
        }

        if (skill1Root == null)
            skill1Root = FindChildRecursive(transform, "Skill1");

        if (skill2Root == null)
            skill2Root = FindChildRecursive(transform, "Skill2");
    }

    private Image FindGaugeImage(Transform skillRoot)
    {
        if (skillRoot == null)
            return null;

        Transform imageTransform = FindChildRecursive(skillRoot, "Image");

        if (imageTransform != null)
        {
            Image image = imageTransform.GetComponent<Image>();

            if (image != null)
                return image;
        }

        Transform fillTransform = FindChildRecursive(skillRoot, "Fill");

        if (fillTransform != null)
        {
            Image image = fillTransform.GetComponent<Image>();

            if (image != null)
                return image;
        }

        return skillRoot.GetComponentInChildren<Image>(true);
    }

    private void SetupGaugeImage(Image image)
    {
        if (image == null)
            return;

        if (!setupImageFillSetting)
            return;

        image.type = Image.Type.Filled;

        switch (fillDirection)
        {
            case GaugeFillDirection.HorizontalLeft:
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                break;

            case GaugeFillDirection.HorizontalRight:
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Right;
                break;

            case GaugeFillDirection.VerticalBottom:
                image.fillMethod = Image.FillMethod.Vertical;
                image.fillOrigin = (int)Image.OriginVertical.Bottom;
                break;

            case GaugeFillDirection.VerticalTop:
                image.fillMethod = Image.FillMethod.Vertical;
                image.fillOrigin = (int)Image.OriginVertical.Top;
                break;
        }

        image.fillAmount = 0f;
    }

    private void TrySetupByUIName()
    {
        skill1Name = NormalizeSkillName(skill1Name);
        skill2Name = NormalizeSkillName(skill2Name);

        if (!string.IsNullOrWhiteSpace(skill1Name) ||
            !string.IsNullOrWhiteSpace(skill2Name))
        {
            return;
        }

        string uiName = gameObject.name.ToLower();

        if (uiName.Contains("pursuer") ||
            uiName.Contains("chaser") ||
            uiName.Contains("추격") ||
            uiName.Contains("체이서"))
        {
            agentId = 0;
            skill1Name = SkillAccessControl;
            skill2Name = SkillEscapeBlock;
            return;
        }

        if (uiName.Contains("scout") ||
            uiName.Contains("observer") ||
            uiName.Contains("수색") ||
            uiName.Contains("옵저버"))
        {
            agentId = 1;
            skill1Name = SkillDrone;
            skill2Name = SkillPositionShare;
            return;
        }

        if (uiName.Contains("engineer") ||
            uiName.Contains("공병") ||
            uiName.Contains("엔지니어") ||
            uiName.Contains("안전관리자") ||
            uiName.Contains("안전 관리자"))
        {
            agentId = 2;
            skill1Name = SkillBarricade;
            skill2Name = SkillStopSignal;
            return;
        }

        if (uiName.Contains("disruptor") ||
            uiName.Contains("distruptor") ||
            uiName.Contains("trickster") ||
            uiName.Contains("magician") ||
            uiName.Contains("교란") ||
            uiName.Contains("트릭스터") ||
            uiName.Contains("마술사") ||
            uiName.Contains("마술"))
        {
            agentId = 3;
            skill1Name = SkillFakeBox;
            skill2Name = SkillJokerCard;
        }
    }

    private void SetupSkillNamesByAgent(AgentController agent)
    {
        if (agent == null)
            return;

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
                break;

            case AgentRole.Observer:
                skill1Name = SkillDrone;
                skill2Name = SkillPositionShare;
                break;

            case AgentRole.Engineer:
                skill1Name = SkillBarricade;
                skill2Name = SkillStopSignal;
                break;

            case AgentRole.Trickster:
                skill1Name = SkillFakeBox;
                skill2Name = SkillJokerCard;
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
                break;

            case 1:
                skill1Name = SkillDrone;
                skill2Name = SkillPositionShare;
                break;

            case 2:
                skill1Name = SkillBarricade;
                skill2Name = SkillStopSignal;
                break;

            case 3:
                skill1Name = SkillFakeBox;
                skill2Name = SkillJokerCard;
                break;
        }
    }

    private void TryCacheTargetAgent()
    {
        if (agentId < 0)
            return;

        AgentController[] foundAgents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < foundAgents.Length; i++)
        {
            AgentController agent = foundAgents[i];

            if (agent == null)
                continue;

            if (agent.AgentID != agentId)
                continue;

            targetAgent = agent;

            if (string.IsNullOrWhiteSpace(skill1Name) &&
                string.IsNullOrWhiteSpace(skill2Name))
            {
                SetupSkillNamesByAgent(targetAgent);
            }

            return;
        }
    }

    private void UpdateSkillGaugeImage(Image image, string skillName)
    {
        if (image == null)
            return;

        if (targetAgent == null)
        {
            SetGaugeAmount(image, 0f);
            return;
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            SetGaugeAmount(image, 0f);
            return;
        }

        string normalizedSkillName = NormalizeSkillName(skillName);

        if (TryUpdateToggleSkillGaugeImage(image, normalizedSkillName))
            return;

        float requiredGauge = targetAgent.GetSkillGaugeRequiredForSkill(normalizedSkillName);

        if (requiredGauge <= 0f)
        {
            SetGaugeAmount(image, 1f);
            return;
        }

        float amount = targetAgent.GetSkillGaugeNormalizedForSkill(normalizedSkillName);
        SetGaugeAmount(image, amount);
    }

    private bool TryUpdateToggleSkillGaugeImage(Image image, string normalizedSkillName)
    {
        if (!IsPositionShareSkill(normalizedSkillName))
            return false;

        Observer observer = targetAgent as Observer;

        if (observer == null)
        {
            SetGaugeAmount(image, toggleSkillOffFillAmount);
            return true;
        }

        float amount = observer.IsTargetPositionShareEnabled
            ? toggleSkillOnFillAmount
            : toggleSkillOffFillAmount;

        SetGaugeAmount(image, amount);
        return true;
    }

    private void SetGaugeAmount(Image image, float amount)
    {
        if (image == null)
            return;

        image.fillAmount = Mathf.Clamp01(amount);
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

        if (IsDroneSkill(skill))
            return SkillDrone;

        return skill;
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

    private bool IsDroneSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillDrone ||
               skill.Contains("drone") ||
               skill.Contains("uav") ||
               skill.Contains("드론");
    }

    private bool IsPositionShareSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

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
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == SkillStopSignal ||
               skill == "stop_signal" ||
               skill.Contains("stop signal") ||
               skill.Contains("stop sign") ||
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
               skill.Contains("snaretrap") ||
               skill.Contains("감속함정") ||
               skill.Contains("감속 함정") ||
               skill.Contains("구속함정") ||
               skill.Contains("구속 함정");
    }

    private bool IsLegacyNoisemakerSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

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
            return false;

        string skill = skillName.Trim().ToLower();

        return skill == LegacySkillHologram ||
               skill.Contains("hologram") ||
               skill.Contains("홀로그램");
    }

    private bool IsFakeBoxSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

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
            return false;

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

            case SkillDrone:
                return "드론";

            case SkillPositionShare:
                return "위치 공유";

            case SkillBarricade:
                return "바리케이드";

            case SkillStopSignal:
                return "정지 신호";

            case SkillFakeBox:
                return "페이크 박스";

            case SkillJokerCard:
                return "조커 카드";

            default:
                return skillName;
        }
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == targetName)
                return child;

            Transform found = FindChildRecursive(child, targetName);

            if (found != null)
                return found;
        }

        return null;
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