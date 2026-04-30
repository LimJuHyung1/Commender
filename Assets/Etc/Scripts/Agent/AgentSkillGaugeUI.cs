using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AgentSkillGaugeUI : MonoBehaviour
{
    private const string SkillDash = "dash";
    private const string SkillSmoke = "smoke";
    private const string SkillFlare = "flare";
    private const string SkillPositionShare = "positionshare";
    private const string SkillBarricade = "barricade";
    private const string SkillSlowTrap = "slowtrap";
    private const string SkillNoisemaker = "noisemaker";
    private const string SkillHologram = "hologram";

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

    [Header("Gauge Info Label")]
    [SerializeField] private bool showGaugeInfoOnSkillClick = true;
    [SerializeField] private float gaugeInfoLabelDuration = 1.5f;
    [SerializeField] private Vector2 gaugeInfoLabelOffset = new Vector2(16f, -24f);
    [SerializeField] private Vector2 gaugeInfoLabelSize = new Vector2(180f, 32f);

    private bool hasCachedGaugeImages = false;

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
        EnsureGaugeImages();
        Refresh();
    }

    private void Start()
    {
        if (targetAgent == null && autoBindByAgentId)
            TryCacheTargetAgent();

        Refresh();
    }

    private void Update()
    {
        if (targetAgent == null && autoBindByAgentId && agentId >= 0)
            TryCacheTargetAgent();

        Refresh();
        HandleSkillGaugeInfoClick();
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

        return targetAgent.CanUseSkillGaugeForSkill(skillName);
    }

    private void HandleSkillGaugeInfoClick()
    {
        if (!showGaugeInfoOnSkillClick)
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.leftButton.wasReleasedThisFrame)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (IsScreenPointInside(skill1ClickArea, mousePosition))
        {
            ShowGaugeInfoLabel(skill1Name, mousePosition);
            return;
        }

        if (IsScreenPointInside(skill2ClickArea, mousePosition))
            ShowGaugeInfoLabel(skill2Name, mousePosition);
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

        float requiredGauge = targetAgent.GetSkillGaugeMaxForSkill(skillName);

        if (requiredGauge <= 0f)
            return $"{GetSkillDisplayName(skillName)}: 게이지 필요 없음";

        float currentGauge = Mathf.Min(targetAgent.SkillGauge, requiredGauge);

        return $"{GetSkillDisplayName(skillName)}: {currentGauge:0.#} / {requiredGauge:0.#}";
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
        string uiName = gameObject.name.ToLower();

        if (uiName.Contains("pursuer") || uiName.Contains("추격"))
        {
            agentId = 0;
            skill1Name = SkillDash;
            skill2Name = SkillSmoke;
            return;
        }

        if (uiName.Contains("scout") || uiName.Contains("수색"))
        {
            agentId = 1;
            skill1Name = SkillFlare;
            skill2Name = SkillPositionShare;
            return;
        }

        if (uiName.Contains("engineer") || uiName.Contains("공병"))
        {
            agentId = 2;
            skill1Name = SkillBarricade;
            skill2Name = SkillSlowTrap;
            return;
        }

        if (uiName.Contains("disruptor") || uiName.Contains("distruptor") || uiName.Contains("교란"))
        {
            agentId = 3;
            skill1Name = SkillNoisemaker;
            skill2Name = SkillHologram;
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
            case AgentRole.Pursuer:
                skill1Name = SkillDash;
                skill2Name = SkillSmoke;
                break;

            case AgentRole.Scout:
                skill1Name = SkillFlare;
                skill2Name = SkillPositionShare;
                break;

            case AgentRole.Engineer:
                skill1Name = SkillBarricade;
                skill2Name = SkillSlowTrap;
                break;

            case AgentRole.Disruptor:
                skill1Name = SkillNoisemaker;
                skill2Name = SkillHologram;
                break;
        }
    }

    private void SetupSkillNamesByAgentId(int id)
    {
        switch (id)
        {
            case 0:
                skill1Name = SkillDash;
                skill2Name = SkillSmoke;
                break;

            case 1:
                skill1Name = SkillFlare;
                skill2Name = SkillPositionShare;
                break;

            case 2:
                skill1Name = SkillBarricade;
                skill2Name = SkillSlowTrap;
                break;

            case 3:
                skill1Name = SkillNoisemaker;
                skill2Name = SkillHologram;
                break;
        }
    }

    private void TryCacheTargetAgent()
    {
        if (agentId < 0)
            return;

        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (agent.AgentID == agentId)
            {
                targetAgent = agent;
                return;
            }
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

        float requiredGauge = targetAgent.GetSkillGaugeMaxForSkill(skillName);

        if (requiredGauge <= 0f)
        {
            SetGaugeAmount(image, 1f);
            return;
        }

        float amount = targetAgent.GetSkillGaugeNormalizedForSkill(skillName);
        SetGaugeAmount(image, amount);
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

        return skillName.Trim().ToLower();
    }

    private string GetSkillDisplayName(string skillName)
    {
        switch (NormalizeSkillName(skillName))
        {
            case SkillDash:
                return "대쉬";

            case SkillSmoke:
                return "연막";

            case SkillFlare:
                return "조명탄";

            case SkillPositionShare:
                return "위치 공유";

            case SkillBarricade:
                return "바리케이드";

            case SkillSlowTrap:
                return "감속 함정";

            case SkillNoisemaker:
                return "소란 장치";

            case SkillHologram:
                return "홀로그램";

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