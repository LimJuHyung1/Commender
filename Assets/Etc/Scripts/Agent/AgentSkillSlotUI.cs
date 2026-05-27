using UnityEngine;
using UnityEngine.UI;

public enum AgentSkillSlotType
{
    First,
    Second,
    Third
}

[DisallowMultipleComponent]
public class AgentSkillSlotUI : MonoBehaviour
{
    [Header("Slot")]
    [SerializeField] private AgentSkillSlotType slotType;

    [Header("Images")]
    [SerializeField] private Image mainImage;
    [SerializeField] private Image fillImage;

    [Header("Click Area")]
    [SerializeField] private RectTransform clickArea;

    public AgentSkillSlotType SlotType => slotType;

    public RectTransform ClickArea
    {
        get
        {
            if (clickArea != null)
                return clickArea;

            return transform as RectTransform;
        }
    }

    public bool IsVisible => gameObject.activeInHierarchy;

    private void Reset()
    {
        AutoAssignMissingReferences();
    }

    private void OnValidate()
    {
        AutoAssignMissingReferences();
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible)
            return;

        gameObject.SetActive(visible);
    }

    public void SetIcon(Sprite sprite)
    {
        if (mainImage != null)
        {
            mainImage.sprite = sprite;
            mainImage.enabled = sprite != null;
        }

        if (fillImage != null)
        {
            fillImage.sprite = sprite;
            fillImage.enabled = sprite != null;
        }
    }

    public void ClearIcon()
    {
        if (mainImage != null)
        {
            mainImage.sprite = null;
            mainImage.enabled = false;
        }

        if (fillImage != null)
        {
            fillImage.sprite = null;
            fillImage.enabled = false;
        }
    }

    public void SetGaugeAmount(float amount)
    {
        if (fillImage == null)
            return;

        fillImage.fillAmount = Mathf.Clamp01(amount);
    }

    public void ConfigureGauge(Image.FillMethod fillMethod, int fillOrigin)
    {
        if (fillImage == null)
            return;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = fillMethod;
        fillImage.fillOrigin = fillOrigin;
    }

    private void AutoAssignMissingReferences()
    {
        if (clickArea == null)
            clickArea = transform as RectTransform;

        Image selfImage = GetComponent<Image>();

        if (mainImage == null)
            mainImage = selfImage;

        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                if (image == null)
                    continue;

                if (image == mainImage)
                    continue;

                fillImage = image;
                break;
            }
        }

        if (fillImage == null)
            fillImage = mainImage;
    }
}