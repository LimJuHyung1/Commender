using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeRewardUI : MonoBehaviour
{
    [Serializable]
    private class UpgradeCardView
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button button;
        [SerializeField] private Text skillNameText;
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private Image iconImage;

        private UpgradeDefinition currentUpgrade;

        public GameObject Root => root;
        public bool HasRoot => root != null;

        public void AutoBindFromRoot(GameObject cardRoot, bool addButtonIfMissing)
        {
            if (cardRoot != null)
            {
                root = cardRoot;
            }

            if (root == null)
            {
                return;
            }

            BindButton(addButtonIfMissing);
            BindTexts();
            BindIconImage();
        }

        public void Bind(UpgradeDefinition upgrade, Action<UpgradeDefinition> onClicked)
        {
            currentUpgrade = upgrade;

            if (root != null)
            {
                root.SetActive(upgrade != null);
            }

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

            if (skillNameText != null)
            {
                skillNameText.text = upgrade.DisplayName;
            }

            if (skillDescriptionText != null)
            {
                skillDescriptionText.text = BuildDescriptionText(upgrade);
            }

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
            {
                button.interactable = interactable;
            }
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
            {
                return;
            }

            button = root.GetComponent<Button>();

            if (button == null)
            {
                button = root.GetComponentInChildren<Button>(true);
            }

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

        private void BindTexts()
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);

            if (skillNameText == null)
            {
                skillNameText = FindTextByName(texts, "SkillName");

                if (skillNameText == null)
                {
                    skillNameText = FindTextByName(texts, "Name");
                }

                if (skillNameText == null && texts.Length > 0)
                {
                    skillNameText = texts[0];
                }
            }

            if (skillDescriptionText == null)
            {
                skillDescriptionText = FindTextByName(texts, "SkillDescription");

                if (skillDescriptionText == null)
                {
                    skillDescriptionText = FindTextByName(texts, "Description");
                }

                if (skillDescriptionText == null && texts.Length > 1)
                {
                    skillDescriptionText = texts[1];
                }
            }
        }

        private void BindIconImage()
        {
            if (iconImage != null)
            {
                return;
            }

            iconImage = FindImageByName(root.transform, "Icon");

            if (iconImage != null)
            {
                return;
            }

            Image[] images = root.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                if (image == null)
                {
                    continue;
                }

                if (image.gameObject == root)
                {
                    continue;
                }

                if (image.GetComponentInParent<Button>() != null && image.gameObject != root)
                {
                    iconImage = image;
                    return;
                }
            }
        }

        private void ApplyIcon(Sprite icon)
        {
            if (iconImage == null)
            {
                Debug.LogWarning($"[{root.name}] Icon Image를 찾지 못했습니다. Card 자식에 Icon이라는 이름의 Image 오브젝트가 있는지 확인하세요.");
                return;
            }

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;

            Color color = iconImage.color;
            color.a = icon != null ? 1f : 0f;
            iconImage.color = color;

            if (iconImage.gameObject != null)
            {
                iconImage.gameObject.SetActive(icon != null);
            }
        }

        private void ClearIcon()
        {
            if (iconImage == null)
            {
                return;
            }

            iconImage.sprite = null;
            iconImage.enabled = false;

            Color color = iconImage.color;
            color.a = 0f;
            iconImage.color = color;
        }

        private void ClearTexts()
        {
            if (skillNameText != null)
            {
                skillNameText.text = string.Empty;
            }

            if (skillDescriptionText != null)
            {
                skillDescriptionText.text = string.Empty;
            }
        }

        private static Text FindTextByName(Text[] texts, string keyword)
        {
            if (texts == null || string.IsNullOrWhiteSpace(keyword))
            {
                return null;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                {
                    continue;
                }

                if (texts[i].name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return texts[i];
                }
            }

            return null;
        }

        private static Image FindImageByName(Transform parent, string objectName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child == null)
                {
                    continue;
                }

                if (string.Equals(child.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    Image image = child.GetComponent<Image>();

                    if (image != null)
                    {
                        return image;
                    }
                }

                Image childResult = FindImageByName(child, objectName);

                if (childResult != null)
                {
                    return childResult;
                }
            }

            return null;
        }

        private static string BuildDescriptionText(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return string.Empty;
            }

            string skillName = GetSkillName(upgrade.SkillId);
            string effectText = BuildEffectText(upgrade);

            if (string.IsNullOrWhiteSpace(skillName))
            {
                return $"{upgrade.Description}\n{effectText}";
            }

            return $"{skillName}\n{upgrade.Description}\n{effectText}";
        }

        private static string BuildEffectText(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return string.Empty;
            }

            switch (upgrade.EffectType)
            {
                case UpgradeEffectType.CooldownMultiplier:
                    return $"쿨타임 {ToPercentDecrease(upgrade.Value)} 감소";

                case UpgradeEffectType.DurationAdd:
                    return $"지속시간 +{upgrade.Value:0.#}초";

                case UpgradeEffectType.RadiusMultiplier:
                    return $"범위 {ToPercentIncrease(upgrade.Value)} 증가";

                case UpgradeEffectType.UseCountAdd:
                    return $"사용 횟수 +{upgrade.Value:0}회";

                case UpgradeEffectType.GaugeCostMultiplier:
                    return $"게이지 소모량 {ToPercentDecrease(upgrade.Value)} 감소";

                case UpgradeEffectType.MaxGaugeAdd:
                    return $"최대 게이지 +{upgrade.Value:0.#}";

                case UpgradeEffectType.SpeedMultiplier:
                    return $"이동속도 {ToPercentIncrease(upgrade.Value)} 증가";

                case UpgradeEffectType.ViewRadiusMultiplier:
                    return $"시야 거리 {ToPercentIncrease(upgrade.Value)} 증가";

                case UpgradeEffectType.ViewAngleAdd:
                    return $"시야각 +{upgrade.Value:0.#}도";

                case UpgradeEffectType.ValueAdd:
                    return $"수치 +{upgrade.Value:0.#}";

                case UpgradeEffectType.ValueMultiplier:
                    return $"효과 {ToPercentIncrease(upgrade.Value)} 증가";

                case UpgradeEffectType.BoolEnable:
                    return "특수 효과 활성화";

                default:
                    return string.Empty;
            }
        }

        private static string ToPercentIncrease(float multiplier)
        {
            float percent = (multiplier - 1f) * 100f;
            return $"+{percent:0.#}%";
        }

        private static string ToPercentDecrease(float multiplier)
        {
            float percent = (1f - multiplier) * 100f;
            return $"{percent:0.#}%";
        }

        private static string GetSkillName(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return string.Empty;
            }

            switch (skillId)
            {
                case "access_control":
                    return "출입 통제";

                case "escape_block":
                    return "도주 제지";

                case "drone":
                    return "드론";

                case "share_position":
                    return "위치 공유";

                case "barricade":
                    return "바리케이드";

                case "stop_signal":
                    return "정지 신호";

                case "fake_box":
                    return "페이크 박스";

                case "joker_card":
                    return "조커 카드";

                default:
                    return skillId;
            }
        }
    }

    [Header("Root")]
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text guideText;

    [Header("Cards")]
    [SerializeField] private Transform cardParent;
    [SerializeField] private List<UpgradeCardView> cardViews = new();

    [Header("Options")]
    [SerializeField] private bool autoCollectCards = true;
    [SerializeField] private bool addButtonIfMissing = true;
    [SerializeField] private bool pauseGameWhileOpen = true;

    private Action<UpgradeDefinition> onUpgradeSelected;
    private float previousTimeScale = 1f;
    private bool isOpen;

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

        if (titleText != null)
        {
            titleText.text = "강화 선택";
        }

        if (guideText != null)
        {
            guideText.text = "다음 스테이지에 적용할 강화를 하나 선택하세요.";
        }

        for (int i = 0; i < cardViews.Count; i++)
        {
            if (i < choices.Count)
            {
                cardViews[i].Bind(choices[i], HandleCardClicked);
            }
            else
            {
                cardViews[i].Bind(null, null);
            }
        }
    }

    public void Hide()
    {
        CloseRoot();
    }

    public void HideImmediate()
    {
        isOpen = false;

        if (rewardRoot != null)
        {
            rewardRoot.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        for (int i = 0; i < cardViews.Count; i++)
        {
            cardViews[i].Clear();
        }
    }

    private void HandleCardClicked(UpgradeDefinition selectedUpgrade)
    {
        if (!isOpen)
        {
            return;
        }

        SetCardsInteractable(false);

        if (UpgradeManager.Instance != null && selectedUpgrade != null)
        {
            UpgradeManager.Instance.SelectAgentUpgrade(selectedUpgrade);
        }

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
        {
            rewardRoot.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void CloseRoot()
    {
        isOpen = false;

        if (pauseGameWhileOpen)
        {
            Time.timeScale = previousTimeScale;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (rewardRoot != null)
        {
            rewardRoot.SetActive(false);
        }

        SetCardsInteractable(false);
    }

    private void SetCardsInteractable(bool interactable)
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            cardViews[i].SetInteractable(interactable);
        }
    }

    private void EnsureBindings()
    {
        if (rewardRoot == null)
        {
            rewardRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = rewardRoot.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = rewardRoot.AddComponent<CanvasGroup>();
            }
        }

        if (cardParent == null)
        {
            Transform panel = transform.Find("UpgradeRewardPanel");

            if (panel != null)
            {
                cardParent = panel;
            }
        }

        if (cardParent != null)
        {
            if (titleText == null)
            {
                Transform title = cardParent.Find("TitleText");

                if (title != null)
                {
                    titleText = title.GetComponent<Text>();
                }
            }

            if (guideText == null)
            {
                Transform guide = cardParent.Find("GuardText");

                if (guide == null)
                {
                    guide = cardParent.Find("GuideText");
                }

                if (guide != null)
                {
                    guideText = guide.GetComponent<Text>();
                }
            }
        }

        if (autoCollectCards)
        {
            AutoCollectCardViews();
        }
        else
        {
            AutoBindExistingCardViews();
        }
    }

    private void AutoCollectCardViews()
    {
        if (cardParent == null)
        {
            return;
        }

        cardViews.Clear();

        for (int i = 0; i < cardParent.childCount; i++)
        {
            Transform child = cardParent.GetChild(i);

            if (child == null)
            {
                continue;
            }

            if (!IsCardRoot(child))
            {
                continue;
            }

            UpgradeCardView cardView = new UpgradeCardView();
            cardView.AutoBindFromRoot(child.gameObject, addButtonIfMissing);
            cardViews.Add(cardView);
        }
    }

    private bool IsCardRoot(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.name.StartsWith("Card", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (target.Find("SkillName") != null && target.Find("SkillDescription") != null)
        {
            return true;
        }

        return false;
    }

    private void AutoBindExistingCardViews()
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            if (cardViews[i] == null)
            {
                continue;
            }

            if (cardViews[i].HasRoot)
            {
                cardViews[i].AutoBindFromRoot(null, addButtonIfMissing);
            }
        }
    }
}