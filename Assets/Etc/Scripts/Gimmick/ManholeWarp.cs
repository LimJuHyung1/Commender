using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class ManholeWarp : MonoBehaviour
{
    [Header("Warp Point")]
    [SerializeField] private Transform exitPoint;

    [Header("Collider")]
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private bool forceTriggerCollider = true;

    [Header("Visual")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private GameObject usedVisual;
    [SerializeField] private ParticleSystem useEffect;

    [Header("Cover Launch Effect")]
    [SerializeField] private Transform coverVisual;
    [SerializeField] private GameObject coverLaunchPrefab;
    [SerializeField] private Transform coverLaunchPoint;
    [SerializeField] private bool hideCoverVisualWhenUsed = true;
    [SerializeField] private bool useCoverVisualAsFallbackPrefab = true;
    [SerializeField] private bool createCoverFromOwnMeshWhenEmpty = true;

    [Header("Cover Launch Motion")]
    [SerializeField] private float launchHeight = 2.0f;
    [SerializeField] private Vector3 launchSideOffset = new Vector3(0.35f, 0f, 0.35f);
    [SerializeField] private Vector3 launchRotation = new Vector3(360f, 180f, 360f);
    [SerializeField] private float moveDuration = 0.45f;
    [SerializeField] private float rotateDuration = 0.8f;
    [SerializeField] private float shrinkDuration = 0.35f;
    [SerializeField] private Ease moveEase = Ease.OutBack;
    [SerializeField] private Ease rotateEase = Ease.OutCubic;
    [SerializeField] private Ease shrinkEase = Ease.InBack;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    private MeshRenderer ownMeshRenderer;
    private MeshFilter ownMeshFilter;
    private bool isUsed;

    public bool IsUsed => isUsed;
    public bool IsAvailable => !isUsed && isActiveAndEnabled;
    public Vector3 ExitPosition => exitPoint != null ? exitPoint.position : transform.position;

    private void Awake()
    {
        CacheComponents();
        SetupTriggerCollider();
        RefreshVisual();
    }

    private void OnEnable()
    {
        ManholeWarpManager.Register(this);
        RefreshVisual();
    }

    private void OnDisable()
    {
        ManholeWarpManager.Unregister(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isUsed)
            return;

        if (!other.TryGetComponent(out TargetManholeWarpController warpController))
            warpController = other.GetComponentInParent<TargetManholeWarpController>();

        if (warpController == null)
            return;

        if (showDebugLog)
            Debug.Log($"[ManholeWarp] {name} detected target: {other.name}", this);

        warpController.TryWarpFrom(this);
    }

    public void MarkUsed()
    {
        MarkUsed(true);
    }

    public void MarkUsed(bool playCoverLaunch)
    {
        if (isUsed)
            return;

        isUsed = true;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (useEffect != null)
            useEffect.Play();

        if (playCoverLaunch)
            PlayCoverLaunchEffect();

        RefreshVisual();

        if (showDebugLog)
            Debug.Log($"[ManholeWarp] {name} marked as used. PlayCoverLaunch: {playCoverLaunch}", this);
    }

    public void ResetUsage()
    {
        isUsed = false;

        if (triggerCollider != null)
            triggerCollider.enabled = true;

        if (coverVisual != null)
            coverVisual.gameObject.SetActive(true);

        if (ownMeshRenderer != null)
            ownMeshRenderer.enabled = true;

        RefreshVisual();

        if (showDebugLog)
            Debug.Log($"[ManholeWarp] {name} reset usage.", this);
    }

    public bool TryGetExitPosition(out Vector3 position)
    {
        position = ExitPosition;
        return true;
    }

    private void CacheComponents()
    {
        if (triggerCollider == null)
            triggerCollider = GetPreferredCollider();

        ownMeshRenderer = GetComponent<MeshRenderer>();
        ownMeshFilter = GetComponent<MeshFilter>();
    }

    private Collider GetPreferredCollider()
    {
        Collider[] colliders = GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
                continue;

            if (CanUseAsTrigger(col))
                return col;
        }

        return colliders.Length > 0 ? colliders[0] : null;
    }

    private void SetupTriggerCollider()
    {
        if (triggerCollider == null)
        {
            Debug.LogWarning(
                $"[ManholeWarp] {name}에 Collider가 없습니다. 맨홀 워프 감지를 위해 BoxCollider, SphereCollider 또는 Convex MeshCollider가 필요합니다.",
                this
            );
            return;
        }

        if (!CanUseAsTrigger(triggerCollider))
        {
            Debug.LogWarning(
                $"[ManholeWarp] {name}의 Trigger Collider로 Concave MeshCollider가 설정되어 있습니다. " +
                "MeshCollider의 Convex를 켜거나, 같은 오브젝트에 SphereCollider/BoxCollider를 추가한 뒤 Trigger Collider에 연결하세요.",
                this
            );
            return;
        }

        if (forceTriggerCollider)
            triggerCollider.isTrigger = true;
    }

    private bool CanUseAsTrigger(Collider col)
    {
        if (col == null)
            return false;

        if (col is MeshCollider meshCollider)
            return meshCollider.convex;

        return true;
    }

    private void RefreshVisual()
    {
        if (activeVisual != null)
            activeVisual.SetActive(!isUsed);

        if (usedVisual != null)
            usedVisual.SetActive(isUsed);

        if (!isUsed)
            return;

        if (!hideCoverVisualWhenUsed)
            return;

        if (coverVisual != null)
        {
            coverVisual.gameObject.SetActive(false);
            return;
        }

        if (ownMeshRenderer != null)
            ownMeshRenderer.enabled = false;
    }

    private void PlayCoverLaunchEffect()
    {
        GameObject launchedCover = CreateLaunchedCover();

        if (launchedCover == null)
        {
            if (showDebugLog)
                Debug.LogWarning($"[ManholeWarp] {name} Cover Launch 생성 실패.", this);

            return;
        }

        Transform launchedTransform = launchedCover.transform;

        Vector3 startPosition = GetCoverLaunchStartPosition();
        Vector3 endPosition = startPosition + Vector3.up * launchHeight + launchSideOffset;
        Vector3 originalScale = launchedTransform.localScale;

        if (originalScale == Vector3.zero)
            originalScale = Vector3.one;

        launchedTransform.position = startPosition;
        launchedTransform.rotation = GetCoverLaunchStartRotation();
        launchedTransform.localScale = originalScale;

        Sequence sequence = DOTween.Sequence();
        sequence.SetLink(launchedCover);

        sequence.Append(
            launchedTransform.DOMove(endPosition, moveDuration)
                .SetEase(moveEase)
        );

        sequence.Join(
            launchedTransform.DORotate(launchRotation, rotateDuration, RotateMode.FastBeyond360)
                .SetEase(rotateEase)
        );

        sequence.Append(
            launchedTransform.DOScale(Vector3.zero, shrinkDuration)
                .SetEase(shrinkEase)
        );

        sequence.OnComplete(() =>
        {
            if (launchedCover != null)
                Destroy(launchedCover);
        });

        if (showDebugLog)
        {
            Debug.Log(
                $"[ManholeWarp] {name} Cover Launch 시작 / Start: {startPosition}, End: {endPosition}",
                launchedCover
            );
        }
    }

    private GameObject CreateLaunchedCover()
    {
        if (coverLaunchPrefab != null)
            return Instantiate(coverLaunchPrefab, GetCoverLaunchStartPosition(), GetCoverLaunchStartRotation());

        if (useCoverVisualAsFallbackPrefab && coverVisual != null)
            return Instantiate(coverVisual.gameObject, GetCoverLaunchStartPosition(), GetCoverLaunchStartRotation());

        if (createCoverFromOwnMeshWhenEmpty)
            return CreateLaunchedCoverFromOwnMesh();

        Debug.LogWarning(
            $"[ManholeWarp] {name}에 Cover Launch 대상이 없습니다. Cover Launch Prefab, Cover Visual 또는 자기 자신의 Mesh가 필요합니다.",
            this
        );

        return null;
    }

    private GameObject CreateLaunchedCoverFromOwnMesh()
    {
        if (ownMeshFilter == null || ownMeshFilter.sharedMesh == null || ownMeshRenderer == null)
        {
            Debug.LogWarning(
                $"[ManholeWarp] {name}에서 복제할 MeshFilter 또는 MeshRenderer를 찾지 못했습니다.",
                this
            );

            return null;
        }

        GameObject coverObject = new GameObject($"{name}_LaunchedCover");
        coverObject.transform.position = GetCoverLaunchStartPosition();
        coverObject.transform.rotation = GetCoverLaunchStartRotation();
        coverObject.transform.localScale = transform.lossyScale;

        MeshFilter meshFilter = coverObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = ownMeshFilter.sharedMesh;

        MeshRenderer meshRenderer = coverObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = ownMeshRenderer.sharedMaterials;

        return coverObject;
    }

    private Vector3 GetCoverLaunchStartPosition()
    {
        if (coverLaunchPoint != null)
            return coverLaunchPoint.position;

        if (coverVisual != null)
            return coverVisual.position;

        return transform.position;
    }

    private Quaternion GetCoverLaunchStartRotation()
    {
        if (coverLaunchPoint != null)
            return coverLaunchPoint.rotation;

        if (coverVisual != null)
            return coverVisual.rotation;

        return transform.rotation;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (triggerCollider == null)
            triggerCollider = GetPreferredCollider();

        if (triggerCollider != null && CanUseAsTrigger(triggerCollider) && forceTriggerCollider)
            triggerCollider.isTrigger = true;

        if (exitPoint == null)
            exitPoint = transform;

        if (coverLaunchPoint == null && coverVisual != null)
            coverLaunchPoint = coverVisual;

        if (launchHeight < 0f)
            launchHeight = 0f;

        if (moveDuration < 0.01f)
            moveDuration = 0.01f;

        if (rotateDuration < 0.01f)
            rotateDuration = 0.01f;

        if (shrinkDuration < 0.01f)
            shrinkDuration = 0.01f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 exitPosition = exitPoint != null ? exitPoint.position : transform.position;
        Vector3 launchPosition = coverLaunchPoint != null ? coverLaunchPoint.position : transform.position;
        Vector3 launchEndPosition = launchPosition + Vector3.up * launchHeight + launchSideOffset;

        Gizmos.color = isUsed ? Color.gray : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(exitPosition, 0.25f);
        Gizmos.DrawLine(transform.position, exitPosition);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(launchPosition, 0.2f);
        Gizmos.DrawLine(launchPosition, launchEndPosition);
        Gizmos.DrawWireSphere(launchEndPosition, 0.25f);
    }
#endif
}