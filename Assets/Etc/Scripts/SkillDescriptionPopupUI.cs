using System.Collections;
using Michsky.UI.MTP;
using UnityEngine;
using UnityEngine.InputSystem;

public class SkillDescriptionPopupUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StyleManager styleManager;
    [SerializeField] private RectTransform popupRectTransform;
    [SerializeField] private Canvas rootCanvas;

    [Header("Text Item IDs")]
    [SerializeField] private string mainTextItemId = "Main Text";
    [SerializeField] private string subTextItemId = "Sub Text";

    [Header("Display")]
    [SerializeField] private bool showUsageText = true;
    [SerializeField] private string usagePrefix = "»ç¿ë¹ý: ";

    [Header("Usage Text Color")]
    [SerializeField] private bool useColoredUsageText = true;
    [SerializeField] private string usageLabelColor = "#BDBDBD";
    [SerializeField] private string coordinateTextColor = "#7DD3FC";
    [SerializeField] private string skillKeywordColor = "#FFD166";

    [Header("Position")]
    [SerializeField] private bool useMousePosition = false;
    [SerializeField] private Vector2 screenOffset = new Vector2(24f, -24f);

    [Header("Hide")]
    [SerializeField] private bool hideOnOutsideClick = true;

    [Header("Animation")]
    [SerializeField] private float fallbackOutAnimationDuration = 0.25f;

    private int openedFrame = -1;
    private Coroutine hideRoutine;

    private const string CoordinateDoubleToken = "{COORDINATE_DOUBLE_TOKEN}";
    private const string CoordinateToken = "{COORDINATE_TOKEN}";
    private const string SkillKeywordToken = "{SKILL_KEYWORD_TOKEN}";

    private void Awake()
    {
        CacheReferences();

        if (styleManager != null)
        {
            styleManager.playOnEnable = false;
            styleManager.playOutAnimation = false;
        }

        gameObject.SetActive(false);
    }

    private void Update()
    {
        HandleOutsideClickHide();
    }

    public void Show(SkillDefinitionSO skillDefinition, Vector2 screenPosition)
    {
        if (skillDefinition == null)
        {
            Hide();
            return;
        }

        string skillName = string.IsNullOrWhiteSpace(skillDefinition.DisplayName)
            ? skillDefinition.SkillId
            : skillDefinition.DisplayName;

        string bodyText = BuildSkillBodyText(skillDefinition);

        Show(skillName, bodyText, screenPosition);
    }

    public void Show(string skillName, string description, Vector2 screenPosition)
    {
        CacheReferences();
        StopHideRoutineIfRunning();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SetText(subTextItemId, skillName);
        SetText(mainTextItemId, description);

        if (useMousePosition)
            SetPopupPosition(screenPosition);

        openedFrame = Time.frameCount;

        if (styleManager != null)
            styleManager.PlayIn();
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (hideRoutine != null)
            return;

        if (styleManager == null)
        {
            gameObject.SetActive(false);
            return;
        }

        hideRoutine = StartCoroutine(HideAfterOutAnimation());
    }

    private string BuildSkillBodyText(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return "";

        string description = string.IsNullOrWhiteSpace(skillDefinition.Description)
            ? ""
            : skillDefinition.Description.Trim();

        if (!showUsageText)
            return description;

        string usageText = BuildUsageText(skillDefinition);

        if (string.IsNullOrWhiteSpace(usageText))
            return description;

        if (string.IsNullOrWhiteSpace(description))
            return usageText;

        return description + "\n" + usageText;
    }

    private string BuildUsageText(SkillDefinitionSO skillDefinition)
    {
        if (skillDefinition == null)
            return "";

        if (string.IsNullOrWhiteSpace(skillDefinition.UsageText))
            return "";

        string usageText = skillDefinition.UsageText.Trim();

        if (!useColoredUsageText)
            return usagePrefix + usageText;

        return BuildColoredUsageText(skillDefinition, usageText);
    }

    private string BuildColoredUsageText(SkillDefinitionSO skillDefinition, string usageText)
    {
        if (skillDefinition == null)
            return usageText;

        string result = usageText;

        string commandKeyword = string.IsNullOrWhiteSpace(skillDefinition.CommandKeyword)
            ? skillDefinition.DisplayName
            : skillDefinition.CommandKeyword;

        commandKeyword = string.IsNullOrWhiteSpace(commandKeyword)
            ? ""
            : commandKeyword.Trim();

        if (!string.IsNullOrWhiteSpace(commandKeyword))
            result = result.Replace(commandKeyword, SkillKeywordToken);

        result = result.Replace("ÁÂÇ¥ 2°³", CoordinateDoubleToken);
        result = result.Replace("ÁÂÇ¥", CoordinateToken);

        result = result.Replace(
            CoordinateDoubleToken,
            WrapColor("ÁÂÇ¥ 2°³", coordinateTextColor)
        );

        result = result.Replace(
            CoordinateToken,
            WrapColor("ÁÂÇ¥", coordinateTextColor)
        );

        if (!string.IsNullOrWhiteSpace(commandKeyword))
        {
            result = result.Replace(
                SkillKeywordToken,
                WrapColor(commandKeyword, skillKeywordColor)
            );
        }

        return WrapColor(usagePrefix, usageLabelColor) + result;
    }

    private string WrapColor(string text, string colorCode)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        if (string.IsNullOrWhiteSpace(colorCode))
            return text;

        string safeColorCode = colorCode.Trim();

        if (!safeColorCode.StartsWith("#"))
            safeColorCode = "#" + safeColorCode;

        return $"<color={safeColorCode}>{text}</color>";
    }

    private void HandleOutsideClickHide()
    {
        if (!hideOnOutsideClick)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        if (Time.frameCount == openedFrame)
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        bool clicked =
            mouse.leftButton.wasPressedThisFrame ||
            mouse.rightButton.wasPressedThisFrame;

        if (!clicked)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (IsPointerInsidePopup(mousePosition))
            return;

        Hide();
    }

    private IEnumerator HideAfterOutAnimation()
    {
        styleManager.PlayOut();

        float waitTime = GetOutAnimationDuration();

        if (waitTime > 0f)
        {
            if (styleManager.UseUnscaledTime)
                yield return new WaitForSecondsRealtime(waitTime);
            else
                yield return new WaitForSeconds(waitTime);
        }
        else
        {
            yield return null;
        }

        hideRoutine = null;
        gameObject.SetActive(false);
    }

    private float GetOutAnimationDuration()
    {
        if (styleManager == null || styleManager.outAnim == null)
            return fallbackOutAnimationDuration;

        float animationSpeed = Mathf.Max(0.01f, styleManager.AnimationSpeed);

        return Mathf.Max(0f, styleManager.outAnim.length / animationSpeed);
    }

    private void StopHideRoutineIfRunning()
    {
        if (hideRoutine == null)
            return;

        StopCoroutine(hideRoutine);
        hideRoutine = null;
    }

    private void CacheReferences()
    {
        if (styleManager == null)
            styleManager = GetComponent<StyleManager>();

        if (popupRectTransform == null)
            popupRectTransform = transform as RectTransform;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    private void SetText(string itemId, string text)
    {
        if (styleManager == null || styleManager.textItems == null)
            return;

        for (int i = 0; i < styleManager.textItems.Count; i++)
        {
            TextItem textItem = styleManager.textItems[i];

            if (textItem == null)
                continue;

            if (textItem.itemID != itemId)
                continue;

            textItem.text = text ?? "";

            if (textItem.textObject == null)
                textItem.textObject = textItem.GetComponent<TMPro.TextMeshProUGUI>();

            textItem.UpdateText();
            return;
        }
    }

    private void SetPopupPosition(Vector2 screenPosition)
    {
        if (popupRectTransform == null || rootCanvas == null)
            return;

        RectTransform canvasRectTransform = rootCanvas.transform as RectTransform;

        if (canvasRectTransform == null)
            return;

        Vector2 targetScreenPosition = screenPosition + screenOffset;

        Camera canvasCamera = null;

        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            targetScreenPosition,
            canvasCamera,
            out Vector2 localPoint))
        {
            popupRectTransform.anchoredPosition = localPoint;
        }
    }

    private bool IsPointerInsidePopup(Vector2 screenPosition)
    {
        if (popupRectTransform == null)
            return false;

        Camera canvasCamera = null;

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;

        return RectTransformUtility.RectangleContainsScreenPoint(
            popupRectTransform,
            screenPosition,
            canvasCamera
        );
    }
}