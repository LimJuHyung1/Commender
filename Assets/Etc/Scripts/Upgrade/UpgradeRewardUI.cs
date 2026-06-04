using System;
using System.Collections.Generic;
using System.Reflection;
using Michsky.UI.MTP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeRewardUI : MonoBehaviour
{
    [Serializable]
    private class UpgradeCardView
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button button;

        [Header("Legacy Text Fallback")]
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private TMP_Text skillDescriptionTmpText;
        [SerializeField] private Text relationFallbackText;
        [SerializeField] private TMP_Text relationFallbackTmpText;

        [Header("MTP Text")]
        [SerializeField] private StyleManager skillNameStyleManager;
        [SerializeField] private string skillNameTextItemId = "Main Text";

        [SerializeField] private StyleManager skillRelationStyleManager;
        [SerializeField] private string skillRelationTextItemId = "Main Text";

        [Header("Icon")]
        [SerializeField] private Image iconImage;

        private UpgradeDefinition currentUpgrade;

        public bool HasRoot => root != null;

        public void AutoBindFromRoot(GameObject cardRoot, bool addButtonIfMissing)
        {
            if (cardRoot != null)
                root = cardRoot;

            if (root == null)
                return;

            BindButton(addButtonIfMissing);
            BindMtpTextTargets();
            BindLegacyTexts();
            BindIconImage();
        }

        public void Bind(
            UpgradeDefinition upgrade,
            SkillDatabaseSO skillDatabase,
            Action<UpgradeDefinition> onClicked)
        {
            currentUpgrade = upgrade;

            if (root != null)
                root.SetActive(upgrade != null);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = upgrade != null;
            }

            if (upgrade == null)
            {
                ClearTexts();
                ClearIcon();
                return;
            }

            SetSkillNameText(BuildColoredUpgradeNameText(upgrade));
            SetSkillRelationText(BuildRelationAndDescriptionText(upgrade, skillDatabase));
            SetSkillDescriptionText(string.Empty);
            ApplyIcon(upgrade.Icon);

            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    onClicked?.Invoke(currentUpgrade);
                });
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        public void Clear()
        {
            currentUpgrade = null;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            ClearTexts();
            ClearIcon();
        }

        private void BindButton(bool addButtonIfMissing)
        {
            if (button != null)
                return;

            button = root.GetComponent<Button>();

            if (button == null)
                button = root.GetComponentInChildren<Button>(true);

            if (button == null && addButtonIfMissing)
            {
                button = root.AddComponent<Button>();

                Graphic graphic = root.GetComponent<Graphic>();

                if (graphic == null)
                {
                    Image image = root.AddComponent<Image>();
                    image.color = new Color(1f, 1f, 1f, 0f);
                    graphic = image;
                }

                button.targetGraphic = graphic;
            }
        }

        private void BindMtpTextTargets()
        {
            if (root == null)
                return;

            StyleManager[] styleManagers = root.GetComponentsInChildren<StyleManager>(true);

            if (skillNameStyleManager == null)
                skillNameStyleManager = FindStyleManagerByObjectName(styleManagers, "MTP-5");

            if (skillRelationStyleManager == null)
                skillRelationStyleManager = FindStyleManagerByObjectName(styleManagers, "MTP-9");

            if (skillRelationStyleManager == null)
                skillRelationStyleManager = FindStyleManagerByObjectName(styleManagers, "Relation");

            if (skillRelationStyleManager == null)
                skillRelationStyleManager = FindStyleManagerByObjectName(styleManagers, "Target");

            if (skillRelationStyleManager == skillNameStyleManager)
                skillRelationStyleManager = null;
        }

        private void BindLegacyTexts()
        {
            if (root == null)
                return;

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);

            if (skillDescriptionText == null)
            {
                skillDescriptionText = FindTextByName(texts, "SkillDescription");

                if (skillDescriptionText == null)
                    skillDescriptionText = FindTextByName(texts, "Description");
            }

            if (skillDescriptionTmpText == null)
            {
                skillDescriptionTmpText = FindTmpTextByName(tmpTexts, "SkillDescription");

                if (skillDescriptionTmpText == null)
                    skillDescriptionTmpText = FindTmpTextByName(tmpTexts, "Description");
            }

            if (relationFallbackText == null)
            {
                relationFallbackText = FindTextByName(texts, "SkillRelation");

                if (relationFallbackText == null)
                    relationFallbackText = FindTextByName(texts, "Relation");

                if (relationFallbackText == null)
                    relationFallbackText = FindTextByName(texts, "TargetSkill");
            }

            if (relationFallbackTmpText == null)
            {
                relationFallbackTmpText = FindTmpTextByName(tmpTexts, "SkillRelation");

                if (relationFallbackTmpText == null)
                    relationFallbackTmpText = FindTmpTextByName(tmpTexts, "Relation");

                if (relationFallbackTmpText == null)
                    relationFallbackTmpText = FindTmpTextByName(tmpTexts, "TargetSkill");
            }
        }

        private void BindIconImage()
        {
            if (iconImage != null)
                return;

            iconImage = FindImageByName(root.transform, "Icon");

            if (iconImage != null)
                return;

            Image[] images = root.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                if (image == null)
                    continue;

                if (image.gameObject == root)
                    continue;

                if (image.GetComponentInParent<Button>() != null && image.gameObject != root)
                {
                    iconImage = image;
                    return;
                }
            }
        }

        private void SetSkillNameText(string text)
        {
            if (skillNameStyleManager != null)
            {
                SetMtpTextAndPlay(skillNameStyleManager, skillNameTextItemId, text);
                return;
            }
        }

        private void SetSkillRelationText(string text)
        {
            if (skillRelationStyleManager != null)
            {
                SetMtpTextAndPlay(skillRelationStyleManager, skillRelationTextItemId, text);
                ClearRelationFallbackText();
                return;
            }

            if (relationFallbackText != null)
            {
                relationFallbackText.supportRichText = true;
                relationFallbackText.text = text ?? string.Empty;
            }

            if (relationFallbackTmpText != null)
            {
                relationFallbackTmpText.richText = true;
                relationFallbackTmpText.text = text ?? string.Empty;
            }
        }

        private void SetSkillDescriptionText(string text)
        {
            if (skillDescriptionText != null)
                skillDescriptionText.text = text ?? string.Empty;

            if (skillDescriptionTmpText != null)
                skillDescriptionTmpText.text = text ?? string.Empty;
        }

        private void ClearTexts()
        {
            SetMtpTextOnly(skillNameStyleManager, skillNameTextItemId, string.Empty);
            SetMtpTextOnly(skillRelationStyleManager, skillRelationTextItemId, string.Empty);

            if (skillDescriptionText != null)
                skillDescriptionText.text = string.Empty;

            if (skillDescriptionTmpText != null)
                skillDescriptionTmpText.text = string.Empty;

            ClearRelationFallbackText();
        }

        private void ClearRelationFallbackText()
        {
            if (relationFallbackText != null)
                relationFallbackText.text = string.Empty;

            if (relationFallbackTmpText != null)
                relationFallbackTmpText.text = string.Empty;
        }

        private static void SetMtpTextAndPlay(StyleManager styleManager, string itemId, string text)
        {
            if (styleManager == null)
                return;

            ConfigureMtpForPausedUI(styleManager);
            SetMtpTextOnly(styleManager, itemId, text);
            styleManager.PlayIn();
        }

        private static void ConfigureMtpForPausedUI(StyleManager styleManager)
        {
            if (styleManager == null)
                return;

            styleManager.UseUnscaledTime = true;
            styleManager.playOnEnable = false;
            styleManager.playOutAnimation = false;
            styleManager.disableOnOut = false;
        }

        private static void SetMtpTextOnly(StyleManager styleManager, string itemId, string text)
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

                textItem.text = text ?? string.Empty;

                if (textItem.textObject == null)
                    textItem.textObject = textItem.GetComponent<TextMeshProUGUI>();

                if (textItem.textObject != null)
                {
                    textItem.textObject.richText = true;
                    // textItem.textObject.enableWordWrapping = true;
                    textItem.textObject.overflowMode = TextOverflowModes.Overflow;
                }

                textItem.UpdateText();
                return;
            }
        }

        private void ApplyIcon(Sprite icon)
        {
            if (iconImage == null)
            {
                if (root != null)
                    Debug.LogWarning($"[{root.name}] Icon Image를 찾지 못했습니다. Card 자식에 Icon 이름의 Image 오브젝트가 있는지 확인하세요.");

                return;
            }

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;

            Color color = iconImage.color;
            color.a = icon != null ? 1f : 0f;
            iconImage.color = color;

            if (iconImage.gameObject != null)
                iconImage.gameObject.SetActive(icon != null);
        }

        private void ClearIcon()
        {
            if (iconImage == null)
                return;

            iconImage.sprite = null;
            iconImage.enabled = false;

            Color color = iconImage.color;
            color.a = 0f;
            iconImage.color = color;
        }

        private static string BuildColoredUpgradeNameText(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
                return string.Empty;

            string displayName = string.IsNullOrWhiteSpace(upgrade.DisplayName)
                ? upgrade.UpgradeId
                : upgrade.DisplayName;

            string colorHex = GetAgentColorHex(upgrade);

            if (string.IsNullOrWhiteSpace(colorHex))
                return displayName;

            return WrapColor(displayName, colorHex);
        }

        private static string BuildRelationAndDescriptionText(
            UpgradeDefinition upgrade,
            SkillDatabaseSO skillDatabase)
        {
            if (upgrade == null)
                return string.Empty;

            string relationText = BuildRelationText(upgrade, skillDatabase);
            string descriptionText = BuildDescriptionText(upgrade);

            if (string.IsNullOrWhiteSpace(relationText))
                return descriptionText;

            if (string.IsNullOrWhiteSpace(descriptionText))
                return relationText;

            return relationText + "\n" + descriptionText;
        }

        private static string BuildRelationText(UpgradeDefinition upgrade, SkillDatabaseSO skillDatabase)
        {
            if (upgrade == null)
                return string.Empty;

            if (IsUnlockUpgrade(upgrade))
                return "신규 스킬\n";

            string skillName = GetTargetSkillDisplayName(upgrade, skillDatabase);
            string coloredSkillName = BuildColoredTargetSkillNameText(upgrade, skillName);

            if (!string.IsNullOrWhiteSpace(coloredSkillName))
                return $"{coloredSkillName} 강화\n";

            if (upgrade.IsTargetUpgrade)
                return "타겟 강화\n";

            return "스킬 강화\n";
        }

        private static string BuildColoredTargetSkillNameText(UpgradeDefinition upgrade, string skillName)
        {
            if (upgrade == null)
                return skillName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(skillName))
                return string.Empty;

            string colorHex = GetAgentColorHex(upgrade);

            if (string.IsNullOrWhiteSpace(colorHex))
                return skillName;

            return WrapColor(skillName, colorHex);
        }

        private static string BuildDescriptionText(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(upgrade.Description))
                return string.Empty;

            return upgrade.Description.Trim();
        }

        private static bool IsUnlockUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
                return false;

            if (TryGetUpgradeCategory(upgrade, out string category))
            {
                if (category == "UnlockSkill")
                    return true;
            }

            string upgradeId = upgrade.UpgradeId;

            if (string.IsNullOrWhiteSpace(upgradeId))
                return false;

            return upgradeId.IndexOf("_unlock_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTargetSkillDisplayName(
            UpgradeDefinition upgrade,
            SkillDatabaseSO skillDatabase)
        {
            string skillId = GetPrimarySkillId(upgrade);

            if (string.IsNullOrWhiteSpace(skillId))
                return string.Empty;

            if (skillDatabase != null)
            {
                if (skillDatabase.TryGetSkillById(skillId, out SkillDefinitionSO skillDefinition))
                    return skillDefinition.DisplayName;

                if (skillDatabase.TryGetSkillByRuntimeKey(skillId, out skillDefinition))
                    return skillDefinition.DisplayName;
            }

            return GetFallbackSkillDisplayName(skillId);
        }

        private static string GetPrimarySkillId(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
                return string.Empty;

            if (IsUnlockUpgrade(upgrade))
            {
                string unlockSkillId = TryGetStringPropertyOrField(upgrade, "UnlockSkillId", "unlockSkillId");

                if (!string.IsNullOrWhiteSpace(unlockSkillId))
                    return unlockSkillId;
            }

            string targetSkillId = TryGetStringPropertyOrField(upgrade, "TargetSkillId", "targetSkillId");

            if (!string.IsNullOrWhiteSpace(targetSkillId))
                return targetSkillId;

            if (!string.IsNullOrWhiteSpace(upgrade.SkillId))
                return upgrade.SkillId;

            return ExtractSkillIdFromUpgradeId(upgrade.UpgradeId);
        }

        private static string TryGetStringPropertyOrField(
            object target,
            string propertyName,
            string fieldName)
        {
            if (target == null)
                return string.Empty;

            Type type = target.GetType();

            PropertyInfo propertyInfo = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (propertyInfo != null && propertyInfo.PropertyType == typeof(string))
            {
                object value = propertyInfo.GetValue(target, null);

                if (value is string propertyValue)
                    return propertyValue;
            }

            FieldInfo fieldInfo = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (fieldInfo != null && fieldInfo.FieldType == typeof(string))
            {
                object value = fieldInfo.GetValue(target);

                if (value is string fieldValue)
                    return fieldValue;
            }

            return string.Empty;
        }

        private static bool TryGetUpgradeCategory(UpgradeDefinition upgrade, out string category)
        {
            category = string.Empty;

            if (upgrade == null)
                return false;

            Type type = upgrade.GetType();

            PropertyInfo propertyInfo = type.GetProperty(
                "UpgradeCategory",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (propertyInfo != null)
            {
                object value = propertyInfo.GetValue(upgrade, null);

                if (value != null)
                {
                    category = value.ToString();
                    return !string.IsNullOrWhiteSpace(category);
                }
            }

            FieldInfo fieldInfo = type.GetField(
                "upgradeCategory",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (fieldInfo != null)
            {
                object value = fieldInfo.GetValue(upgrade);

                if (value != null)
                {
                    category = value.ToString();
                    return !string.IsNullOrWhiteSpace(category);
                }
            }

            return false;
        }

        private static string GetAgentColorHex(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
                return string.Empty;

            string upgradeId = upgrade.UpgradeId ?? string.Empty;
            string lowerUpgradeId = upgradeId.ToLowerInvariant();

            if (lowerUpgradeId.StartsWith("chaser_"))
                return "#008B8B";

            if (lowerUpgradeId.StartsWith("observer_"))
                return "#8FBC8F";

            if (lowerUpgradeId.StartsWith("engineer_"))
                return "#FFA500";

            if (lowerUpgradeId.StartsWith("trickster_"))
                return "#48D1CC";

            string skillId = GetPrimarySkillId(upgrade);
            string normalizedSkillId = NormalizeSkillId(skillId);

            switch (normalizedSkillId)
            {
                case "access_control":
                case "accesscontrol":
                case "escape_block":
                case "escapeblock":
                case "patrol":
                case "tracking_instinct":
                case "trackinginstinct":
                    return "#008B8B";

                case "drone":
                case "position_share":
                case "positionshare":
                case "reconnaissance":
                case "observation_support":
                case "observationsupport":
                    return "#8FBC8F";

                case "barricade":
                case "stop_signal":
                case "stopsignal":
                case "demolition":
                case "safe_zone":
                case "safezone":
                    return "#FFA500";

                case "fake_box":
                case "fakebox":
                case "joker_card":
                case "jokercard":
                case "vanishing":
                case "misdirection":
                    return "#48D1CC";
            }

            return string.Empty;
        }

        private static string WrapColor(string text, string colorHex)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(colorHex))
                return text;

            return $"<color={colorHex}>{text}</color>";
        }

        private static string ExtractSkillIdFromUpgradeId(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
                return string.Empty;

            string[] tokens = upgradeId.Split('_');

            if (tokens.Length < 3)
                return string.Empty;

            if (tokens[1].Equals("unlock", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length >= 3)
                    return string.Join("_", tokens, 2, tokens.Length - 2);

                return string.Empty;
            }

            if (tokens[0].Equals("chaser", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("observer", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("engineer", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("trickster", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length >= 3)
                    return tokens[1] + "_" + tokens[2];
            }

            return string.Empty;
        }

        private static string GetFallbackSkillDisplayName(string skillId)
        {
            switch (NormalizeSkillId(skillId))
            {
                case "access_control":
                case "accesscontrol":
                    return "출입 통제";

                case "escape_block":
                case "escapeblock":
                    return "도주 제지";

                case "patrol":
                    return "순찰";

                case "tracking_instinct":
                case "trackinginstinct":
                    return "추적 본능";

                case "drone":
                    return "드론";

                case "position_share":
                case "positionshare":
                    return "위치 공유";

                case "reconnaissance":
                    return "정찰";

                case "observation_support":
                case "observationsupport":
                    return "관측 지원";

                case "barricade":
                    return "바리케이드";

                case "stop_signal":
                case "stopsignal":
                    return "정지 신호";

                case "demolition":
                    return "철거";

                case "safe_zone":
                case "safezone":
                    return "안전 구역";

                case "fake_box":
                case "fakebox":
                    return "페이크 박스";

                case "joker_card":
                case "jokercard":
                    return "조커 카드";

                case "vanishing":
                    return "배니싱";

                case "misdirection":
                    return "미스디렉션";
            }

            return skillId;
        }

        private static string NormalizeSkillId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        private static StyleManager FindStyleManagerByObjectName(StyleManager[] styleManagers, string keyword)
        {
            if (styleManagers == null || string.IsNullOrWhiteSpace(keyword))
                return null;

            for (int i = 0; i < styleManagers.Length; i++)
            {
                StyleManager styleManager = styleManagers[i];

                if (styleManager == null)
                    continue;

                if (styleManager.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return styleManager;
            }

            return null;
        }

        private static Text FindTextByName(Text[] texts, string keyword)
        {
            if (texts == null || string.IsNullOrWhiteSpace(keyword))
                return null;

            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];

                if (text == null)
                    continue;

                if (text.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return text;
            }

            return null;
        }

        private static TMP_Text FindTmpTextByName(TMP_Text[] texts, string keyword)
        {
            if (texts == null || string.IsNullOrWhiteSpace(keyword))
                return null;

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];

                if (text == null)
                    continue;

                if (text.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return text;
            }

            return null;
        }

        private static Image FindImageByName(Transform parent, string objectName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child == null)
                    continue;

                if (string.Equals(child.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    Image image = child.GetComponent<Image>();

                    if (image != null)
                        return image;
                }

                Image childResult = FindImageByName(child, objectName);

                if (childResult != null)
                    return childResult;
            }

            return null;
        }
    }

    [Header("Root")]
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text")]
    [SerializeField] private Text titleText;
    [SerializeField] private TMP_Text titleTmpText;
    [SerializeField] private string titleMessage = "강화하거나 새로 배울 스킬을 선택하세요";

    [Header("Database")]
    [SerializeField] private SkillDatabaseSO skillDatabase;

    [Header("Cards")]
    [SerializeField] private Transform cardParent;
    [SerializeField] private List<UpgradeCardView> cardViews = new List<UpgradeCardView>();

    [Header("Options")]
    [SerializeField] private bool autoCollectCards = true;
    [SerializeField] private bool addButtonIfMissing = true;
    [SerializeField] private bool pauseGameWhileOpen = true;

    [Header("Hide While Open")]
    [SerializeField] private Canvas[] canvasesToDisableWhileOpen;
    [SerializeField] private GameObject[] objectsToDisableWhileOpen;

    private Action<UpgradeDefinition> onUpgradeSelected;
    private float previousTimeScale = 1f;
    private bool isOpen;

    private bool[] disabledCanvasPreviousStates;
    private bool[] disabledObjectPreviousStates;

    private void Awake()
    {
        EnsureBindings();
        HideImmediate();
    }

    public void ShowChoices(IReadOnlyList<UpgradeDefinition> choices, Action<UpgradeDefinition> onSelected)
    {
        EnsureBindings();

        onUpgradeSelected = onSelected;

        if (choices == null || choices.Count <= 0)
        {
            HideImmediate();
            onUpgradeSelected?.Invoke(null);
            return;
        }

        OpenRoot();
        SetTitle(titleMessage);

        for (int i = 0; i < cardViews.Count; i++)
        {
            if (i < choices.Count)
                cardViews[i].Bind(choices[i], skillDatabase, HandleCardClicked);
            else
                cardViews[i].Bind(null, skillDatabase, null);
        }
    }

    public void Hide()
    {
        CloseRoot();
    }

    public void HideImmediate()
    {
        bool wasOpen = isOpen;
        isOpen = false;

        if (wasOpen)
        {
            RestoreCanvasesDisabledWhileOpen();
            RestoreObjectsDisabledWhileOpen();

            if (pauseGameWhileOpen)
                Time.timeScale = previousTimeScale;
        }

        if (rewardRoot != null)
            rewardRoot.SetActive(false);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        for (int i = 0; i < cardViews.Count; i++)
            cardViews[i].Clear();
    }

    private void HandleCardClicked(UpgradeDefinition selectedUpgrade)
    {
        if (!isOpen)
            return;

        SetCardsInteractable(false);

        if (UpgradeManager.Instance != null && selectedUpgrade != null)
            UpgradeManager.Instance.SelectAgentUpgrade(selectedUpgrade);

        CloseRoot();

        onUpgradeSelected?.Invoke(selectedUpgrade);
        onUpgradeSelected = null;
    }

    private void OpenRoot()
    {
        isOpen = true;

        if (pauseGameWhileOpen)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (rewardRoot != null)
            rewardRoot.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        DisableCanvasesWhileOpen();
        DisableObjectsWhileOpen();
    }

    private void CloseRoot()
    {
        isOpen = false;

        RestoreCanvasesDisabledWhileOpen();
        RestoreObjectsDisabledWhileOpen();

        if (pauseGameWhileOpen)
            Time.timeScale = previousTimeScale;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (rewardRoot != null)
            rewardRoot.SetActive(false);

        SetCardsInteractable(false);
    }

    private void SetTitle(string text)
    {
        if (titleText != null)
            titleText.text = text ?? string.Empty;

        if (titleTmpText != null)
            titleTmpText.text = text ?? string.Empty;
    }

    private void SetCardsInteractable(bool interactable)
    {
        for (int i = 0; i < cardViews.Count; i++)
            cardViews[i].SetInteractable(interactable);
    }

    private void DisableCanvasesWhileOpen()
    {
        if (canvasesToDisableWhileOpen == null || canvasesToDisableWhileOpen.Length == 0)
            return;

        disabledCanvasPreviousStates = new bool[canvasesToDisableWhileOpen.Length];

        for (int i = 0; i < canvasesToDisableWhileOpen.Length; i++)
        {
            Canvas targetCanvas = canvasesToDisableWhileOpen[i];

            if (targetCanvas == null)
                continue;

            GameObject targetObject = targetCanvas.gameObject;
            disabledCanvasPreviousStates[i] = targetObject.activeSelf;

            if (ShouldSkipDisableTarget(targetObject))
                continue;

            targetObject.SetActive(false);
        }
    }

    private void RestoreCanvasesDisabledWhileOpen()
    {
        if (canvasesToDisableWhileOpen == null || disabledCanvasPreviousStates == null)
            return;

        int count = Mathf.Min(canvasesToDisableWhileOpen.Length, disabledCanvasPreviousStates.Length);

        for (int i = 0; i < count; i++)
        {
            Canvas targetCanvas = canvasesToDisableWhileOpen[i];

            if (targetCanvas == null)
                continue;

            GameObject targetObject = targetCanvas.gameObject;

            if (ShouldSkipDisableTarget(targetObject))
                continue;

            targetObject.SetActive(disabledCanvasPreviousStates[i]);
        }

        disabledCanvasPreviousStates = null;
    }

    private void DisableObjectsWhileOpen()
    {
        if (objectsToDisableWhileOpen == null || objectsToDisableWhileOpen.Length == 0)
            return;

        disabledObjectPreviousStates = new bool[objectsToDisableWhileOpen.Length];

        for (int i = 0; i < objectsToDisableWhileOpen.Length; i++)
        {
            GameObject target = objectsToDisableWhileOpen[i];

            if (target == null)
                continue;

            disabledObjectPreviousStates[i] = target.activeSelf;

            if (ShouldSkipDisableTarget(target))
                continue;

            target.SetActive(false);
        }
    }

    private void RestoreObjectsDisabledWhileOpen()
    {
        if (objectsToDisableWhileOpen == null || disabledObjectPreviousStates == null)
            return;

        int count = Mathf.Min(objectsToDisableWhileOpen.Length, disabledObjectPreviousStates.Length);

        for (int i = 0; i < count; i++)
        {
            GameObject target = objectsToDisableWhileOpen[i];

            if (target == null)
                continue;

            if (ShouldSkipDisableTarget(target))
                continue;

            target.SetActive(disabledObjectPreviousStates[i]);
        }

        disabledObjectPreviousStates = null;
    }

    private bool ShouldSkipDisableTarget(GameObject target)
    {
        if (target == null || rewardRoot == null)
            return false;

        Transform targetTransform = target.transform;
        Transform rewardTransform = rewardRoot.transform;

        if (target == rewardRoot)
        {
            Debug.LogWarning("[UpgradeRewardUI] RewardRoot 자체는 비활성화 대상으로 사용할 수 없습니다.", this);
            return true;
        }

        if (rewardTransform.IsChildOf(targetTransform))
        {
            Debug.LogWarning(
                $"[UpgradeRewardUI] '{target.name}'은 강화 선택 UI의 부모입니다. 이 오브젝트를 끄면 강화 선택 UI도 함께 사라지므로 비활성화하지 않습니다.",
                this
            );

            return true;
        }

        if (targetTransform.IsChildOf(rewardTransform))
        {
            Debug.LogWarning(
                $"[UpgradeRewardUI] '{target.name}'은 강화 선택 UI의 자식입니다. 강화 선택 UI 내부 오브젝트는 비활성화 대상으로 사용하지 않는 것이 안전합니다.",
                this
            );

            return true;
        }

        return false;
    }

    private void EnsureBindings()
    {
        if (rewardRoot == null)
            rewardRoot = gameObject;

        if (canvasGroup == null)
        {
            canvasGroup = rewardRoot.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = rewardRoot.AddComponent<CanvasGroup>();
        }

        if (cardParent == null)
        {
            Transform panel = transform.Find("UpgradeRewardPanel");

            if (panel != null)
                cardParent = panel;
        }

        if (cardParent == null && HasCardChild(transform))
            cardParent = transform;

        if (cardParent != null)
        {
            if (titleText == null && titleTmpText == null)
            {
                Transform title = cardParent.Find("TitleText");

                if (title != null)
                {
                    titleText = title.GetComponent<Text>();
                    titleTmpText = title.GetComponent<TMP_Text>();
                }
            }
        }

        if (autoCollectCards)
            AutoCollectCardViews();
        else
            AutoBindExistingCardViews();
    }

    private bool HasCardChild(Transform parent)
    {
        if (parent == null)
            return false;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child != null && child.name.StartsWith("Card", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void AutoCollectCardViews()
    {
        if (cardParent == null)
            return;

        cardViews.Clear();

        for (int i = 0; i < cardParent.childCount; i++)
        {
            Transform child = cardParent.GetChild(i);

            if (child == null)
                continue;

            if (!IsCardRoot(child))
                continue;

            UpgradeCardView cardView = new UpgradeCardView();
            cardView.AutoBindFromRoot(child.gameObject, addButtonIfMissing);
            cardViews.Add(cardView);
        }
    }

    private bool IsCardRoot(Transform target)
    {
        if (target == null)
            return false;

        if (target.name.StartsWith("Card", StringComparison.OrdinalIgnoreCase))
            return true;

        if (target.Find("MTP-5") != null)
            return true;

        return false;
    }

    private void AutoBindExistingCardViews()
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            if (cardViews[i] == null)
                continue;

            if (cardViews[i].HasRoot)
                cardViews[i].AutoBindFromRoot(null, addButtonIfMissing);
        }
    }
}