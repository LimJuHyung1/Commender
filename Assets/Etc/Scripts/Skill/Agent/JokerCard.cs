using DG.Tweening;
using UnityEngine;

public class JokerCard : MonoBehaviour
{
    private const string CardBillboardRootName = "CardBillboardRoot";
    private const string CardMotionRootName = "CardMotionRoot";
    private const string CardFlipRootName = "CardFlipRoot";
    private const string FrontCardName = "FrontCard";
    private const string BackCardName = "BackCard";

    private const float FlipStartY = 180f;
    private const float FlipEndY = 360f;

    [Header("References")]
    [SerializeField] private Transform cardBillboardRoot;
    [SerializeField] private Transform cardMotionRoot;
    [SerializeField] private Transform cardFlipRoot;
    [SerializeField] private SpriteRenderer frontRenderer;
    [SerializeField] private SpriteRenderer backRenderer;
    [SerializeField] private Camera targetCamera;

    [Header("Auto Reference")]
    [SerializeField] private bool autoCacheReferences = true;

    [Header("Effect Settings")]
    [SerializeField] private Vector3 startScale = new Vector3(0.15f, 0.15f, 0.15f);
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private float appearDuration = 0.7f;
    [SerializeField] private float holdDuration = 0.35f;
    [SerializeField] private float disappearDuration = 0.2f;
    [SerializeField] private float zRotateAmount = 360f;
    [SerializeField] private bool invertBillboardFacing;

    private Sequence sequence;
    private Vector3 initialMotionLocalPosition;
    private bool isPlaying;
    private float effectAlpha;

    private void Awake()
    {
        AutoCacheReferences();

        if (targetCamera == null)
            targetCamera = Camera.main;

        CacheInitialMotionPosition();
        SetupCardSideRotation();
        HideVisualState();
    }

    private void OnValidate()
    {
        appearDuration = Mathf.Max(0.01f, appearDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        disappearDuration = Mathf.Max(0.01f, disappearDuration);

        AutoCacheReferences();
        CacheInitialMotionPosition();
    }

    private void LateUpdate()
    {
        if (!isPlaying)
            return;

        UpdateBillboardRotation();
        ApplyVisibleSideAlpha();
    }

    private void OnDisable()
    {
        KillAllTweens();

        isPlaying = false;
        effectAlpha = 0f;

        SetRendererAlpha(frontRenderer, 0f);
        SetRendererAlpha(backRenderer, 0f);
    }

    public void Play()
    {
        AutoCacheReferences();

        if (!HasValidReferences())
        {
            Debug.LogWarning("[JokerCard] Required references are missing.");
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        KillAllTweens();

        ForceHierarchyActive();
        EnableRenderers();
        ResetTransformForPlay();

        isPlaying = true;
        effectAlpha = 0f;

        UpdateBillboardRotation();
        ApplyVisibleSideAlpha();

        sequence = DOTween.Sequence();

        sequence.Append(
            DOTween.To(
                () => effectAlpha,
                value =>
                {
                    effectAlpha = value;
                    ApplyVisibleSideAlpha();
                },
                1f,
                appearDuration * 0.25f
            ).SetTarget(this)
        );

        sequence.Join(
            cardMotionRoot
                .DOScale(endScale, appearDuration)
                .SetEase(Ease.OutBack)
        );

        sequence.Join(
            cardMotionRoot
                .DOLocalRotate(
                    new Vector3(0f, 0f, zRotateAmount),
                    appearDuration,
                    RotateMode.FastBeyond360
                )
                .SetEase(Ease.OutCubic)
        );

        sequence.Join(
            cardFlipRoot
                .DOLocalRotate(
                    new Vector3(0f, FlipEndY, 0f),
                    appearDuration,
                    RotateMode.FastBeyond360
                )
                .SetEase(Ease.OutCubic)
                .OnUpdate(ApplyVisibleSideAlpha)
        );

        sequence.AppendCallback(() =>
        {
            cardMotionRoot.localRotation = Quaternion.identity;
            cardMotionRoot.localScale = endScale;
            cardFlipRoot.localRotation = Quaternion.identity;

            effectAlpha = 1f;
            ApplyFrontOnlyAlpha();
        });

        sequence.AppendInterval(holdDuration);

        sequence.Append(
            DOTween.To(
                () => effectAlpha,
                value =>
                {
                    effectAlpha = value;
                    ApplyFrontOnlyAlpha();
                },
                0f,
                disappearDuration
            ).SetTarget(this)
        );

        sequence.Join(
            cardMotionRoot
                .DOScale(startScale, disappearDuration)
                .SetEase(Ease.InBack)
        );

        sequence.OnComplete(() =>
        {
            sequence = null;
            isPlaying = false;
            HideVisualState();
        });
    }

    public void StopImmediate()
    {
        KillAllTweens();

        isPlaying = false;
        HideVisualState();
    }

    private void AutoCacheReferences()
    {
        if (!autoCacheReferences)
            return;

        if (cardBillboardRoot == null)
            cardBillboardRoot = FindChildRecursive(transform, CardBillboardRootName);

        if (cardMotionRoot == null)
            cardMotionRoot = FindChildRecursive(transform, CardMotionRootName);

        if (cardFlipRoot == null)
            cardFlipRoot = FindChildRecursive(transform, CardFlipRootName);

        if (frontRenderer == null)
            frontRenderer = FindSpriteRendererByObjectName(FrontCardName);

        if (backRenderer == null)
            backRenderer = FindSpriteRendererByObjectName(BackCardName);
    }

    private void CacheInitialMotionPosition()
    {
        if (cardMotionRoot == null)
            return;

        initialMotionLocalPosition = cardMotionRoot.localPosition;
    }

    private void ForceHierarchyActive()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (cardBillboardRoot != null && !cardBillboardRoot.gameObject.activeSelf)
            cardBillboardRoot.gameObject.SetActive(true);

        if (cardMotionRoot != null && !cardMotionRoot.gameObject.activeSelf)
            cardMotionRoot.gameObject.SetActive(true);

        if (cardFlipRoot != null && !cardFlipRoot.gameObject.activeSelf)
            cardFlipRoot.gameObject.SetActive(true);

        if (frontRenderer != null && !frontRenderer.gameObject.activeSelf)
            frontRenderer.gameObject.SetActive(true);

        if (backRenderer != null && !backRenderer.gameObject.activeSelf)
            backRenderer.gameObject.SetActive(true);
    }

    private void EnableRenderers()
    {
        if (frontRenderer != null)
            frontRenderer.enabled = true;

        if (backRenderer != null)
            backRenderer.enabled = true;
    }

    private void ResetTransformForPlay()
    {
        if (cardMotionRoot != null)
        {
            cardMotionRoot.localPosition = initialMotionLocalPosition;
            cardMotionRoot.localRotation = Quaternion.identity;
            cardMotionRoot.localScale = startScale;
        }

        if (cardFlipRoot != null)
            cardFlipRoot.localRotation = Quaternion.Euler(0f, FlipStartY, 0f);
    }

    private void HideVisualState()
    {
        effectAlpha = 0f;

        ForceHierarchyActive();
        EnableRenderers();

        if (cardMotionRoot != null)
        {
            cardMotionRoot.localPosition = initialMotionLocalPosition;
            cardMotionRoot.localRotation = Quaternion.identity;
            cardMotionRoot.localScale = startScale;
        }

        if (cardFlipRoot != null)
            cardFlipRoot.localRotation = Quaternion.Euler(0f, FlipStartY, 0f);

        SetRendererAlpha(frontRenderer, 0f);
        SetRendererAlpha(backRenderer, 0f);
    }

    private void UpdateBillboardRotation()
    {
        if (cardBillboardRoot == null || targetCamera == null)
            return;

        Vector3 forward = targetCamera.transform.forward;

        if (invertBillboardFacing)
            forward = -forward;

        cardBillboardRoot.rotation = Quaternion.LookRotation(
            forward,
            targetCamera.transform.up
        );
    }

    private void ApplyVisibleSideAlpha()
    {
        if (cardFlipRoot == null || frontRenderer == null || backRenderer == null)
            return;

        float yAngle = NormalizeAngle(cardFlipRoot.localEulerAngles.y);
        bool showBack = yAngle > 90f && yAngle < 270f;

        SetRendererAlpha(frontRenderer, showBack ? 0f : effectAlpha);
        SetRendererAlpha(backRenderer, showBack ? effectAlpha : 0f);
    }

    private void ApplyFrontOnlyAlpha()
    {
        if (frontRenderer == null || backRenderer == null)
            return;

        SetRendererAlpha(frontRenderer, effectAlpha);
        SetRendererAlpha(backRenderer, 0f);
    }

    private void SetupCardSideRotation()
    {
        if (frontRenderer != null)
            frontRenderer.transform.localRotation = Quaternion.identity;

        if (backRenderer != null)
            backRenderer.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private void KillAllTweens()
    {
        if (sequence != null)
        {
            sequence.Kill(false);
            sequence = null;
        }

        if (cardMotionRoot != null)
            DOTween.Kill(cardMotionRoot, false);

        if (cardFlipRoot != null)
            DOTween.Kill(cardFlipRoot, false);

        DOTween.Kill(this, false);
    }

    private bool HasValidReferences()
    {
        return cardBillboardRoot != null
            && cardMotionRoot != null
            && cardFlipRoot != null
            && frontRenderer != null
            && backRenderer != null;
    }

    private SpriteRenderer FindSpriteRendererByObjectName(string objectName)
    {
        Transform found = FindChildRecursive(transform, objectName);

        if (found == null)
            return null;

        return found.GetComponent<SpriteRenderer>();
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (string.Equals(
                child.name,
                childName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
                return found;
        }

        return null;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
            angle += 360f;

        return angle;
    }

    private void SetRendererAlpha(SpriteRenderer targetRenderer, float alpha)
    {
        if (targetRenderer == null)
            return;

        Color color = targetRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        targetRenderer.color = color;
    }
}