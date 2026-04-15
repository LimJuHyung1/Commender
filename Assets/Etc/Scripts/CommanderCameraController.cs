using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CommanderCameraController : MonoBehaviour
{
    private enum TopDownViewMode
    {
        FitToGroundBounds,
        FixedView
    }

    [Header("Ground Reference")]
    [SerializeField] private Transform groundRoot;
    [SerializeField] private LayerMask groundLayer;

    [Header("Common Camera Settings")]
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Top View Settings")]
    [SerializeField] private TopDownViewMode topDownViewMode = TopDownViewMode.FixedView;
    [SerializeField] private Vector3 topDownLocalOffset = new Vector3(0f, 30f, 0f);
    [SerializeField] private Vector3 topDownEuler = new Vector3(90f, 0f, 90f);
    [SerializeField] private float fixedTopDownOrthoSize = 18f;
    [SerializeField] private float fitTopDownHeight = 30f;
    [SerializeField] private float fitPadding = 1f;

    [Header("Top View Pan")]
    [SerializeField] private float dragPanSensitivity = 1f;

    [Header("Focused Agent View")]
    [SerializeField] private string focusAnchorName = "CameraFocusPoint";
    [SerializeField] private Vector3 focusTargetOffset = Vector3.zero;
    [SerializeField] private Vector3 focusedViewEuler = new Vector3(45f, -45f, 0f);
    [SerializeField] private Vector3 focusedLocalOffset = new Vector3(0f, 0f, -7f);
    [SerializeField] private float focusedOrthoSize = 6f;

    [Header("Focused Agent Orbit")]
    [SerializeField] private bool resetOrbitOnFocusChanged = true;
    [SerializeField] private float orbitSensitivity = 0.2f;
    [SerializeField] private float minOrbitPitch = 15f;
    [SerializeField] private float maxOrbitPitch = 80f;

    [Header("Mouse Wheel Zoom")]
    [SerializeField] private float wheelZoomSensitivity = 5f;
    [SerializeField] private float minTopDownOrthoSize = 8f;
    [SerializeField] private float maxTopDownOrthoSize = 30f;
    [SerializeField] private float minFocusedOrthoSize = 3f;
    [SerializeField] private float maxFocusedOrthoSize = 12f;
    [SerializeField] private float minFitZoomOffset = -12f;
    [SerializeField] private float maxFitZoomOffset = 12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Camera cam;

    private Bounds groundBounds;
    private bool hasGroundBounds;

    private Transform focusedAgent;
    private Transform cachedFocusAnchor;
    private Renderer[] cachedFocusRenderers;
    private Collider[] cachedFocusColliders;

    private Vector3 topDownPanOffset = Vector3.zero;

    private float focusedOrbitYaw;
    private float focusedOrbitPitch;
    private float focusedOrbitRoll;

    private float currentTopDownOrthoSize;
    private float currentFocusedOrthoSize;
    private float currentFitZoomOffset;

    private Quaternion cachedTopDownRotation;
    private Vector3 cachedPanRightOnGround;
    private Vector3 cachedPanUpOnGround;

    public Camera ControlledCamera => cam;
    public bool HasFocusedAgent => focusedAgent != null;
    public Transform FocusedAgent => focusedAgent;
    public bool HasGroundBounds => hasGroundBounds;
    public Bounds GroundBounds => groundBounds;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        RebuildRotationCaches();
        ResetFocusedOrbitAngles();
        ResetZoomStates();
        RefreshGroundBounds();
    }

    private void OnValidate()
    {
        if (maxOrbitPitch < minOrbitPitch)
            maxOrbitPitch = minOrbitPitch;

        if (maxTopDownOrthoSize < minTopDownOrthoSize)
            maxTopDownOrthoSize = minTopDownOrthoSize;

        if (maxFocusedOrthoSize < minFocusedOrthoSize)
            maxFocusedOrthoSize = minFocusedOrthoSize;

        if (maxFitZoomOffset < minFitZoomOffset)
            maxFitZoomOffset = minFitZoomOffset;

        RebuildRotationCaches();
    }

    private void LateUpdate()
    {
        if (focusedAgent != null)
        {
            UpdateFocusedAgentView();
            return;
        }

        UpdateTopDownView();
    }

    public void FocusAgent(Transform agentTransform)
    {
        bool focusChanged = focusedAgent != agentTransform;

        focusedAgent = agentTransform;
        CacheFocusedAgentData(agentTransform);

        if (focusChanged || resetOrbitOnFocusChanged)
            ResetFocusedOrbitAngles();
    }

    public void ClearFocusAgent()
    {
        focusedAgent = null;
        ClearFocusedAgentCache();
    }

    public void ResetTopDownPan()
    {
        topDownPanOffset = Vector3.zero;
    }

    public void ResetZoom()
    {
        ResetZoomStates();
    }

    public void ResetOrbit()
    {
        ResetFocusedOrbitAngles();
    }

    public void SetGroundRoot(Transform newGroundRoot)
    {
        groundRoot = newGroundRoot;
        ResetTopDownPan();
        RefreshGroundBounds();
    }

    public void RefreshGroundBounds()
    {
        if (groundRoot == null)
        {
            hasGroundBounds = false;
            Debug.LogWarning("[CommanderCameraController] groundRoot가 연결되지 않았습니다.");
            return;
        }

        Bounds combinedBounds;
        bool foundAnyRenderer = TryGetCombinedBounds(
            groundRoot.GetComponentsInChildren<Renderer>(true),
            IsValidGroundRenderer,
            out combinedBounds
        );

        bool foundAnyCollider = false;

        if (!foundAnyRenderer)
        {
            foundAnyCollider = TryGetCombinedBounds(
                groundRoot.GetComponentsInChildren<Collider>(true),
                IsValidGroundCollider,
                out combinedBounds
            );
        }

        hasGroundBounds = foundAnyRenderer || foundAnyCollider;
        groundBounds = combinedBounds;

        if (!hasGroundBounds)
            Debug.LogWarning("[CommanderCameraController] groundRoot 아래에서 Ground 레이어 bounds를 찾지 못했습니다.");
    }

    public void AddTopDownPan(Vector2 screenDelta)
    {
        if (focusedAgent != null)
            return;

        if (screenDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        float worldUnitsPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(Screen.height, 1);

        Vector3 move =
            (-cachedPanRightOnGround * screenDelta.x - cachedPanUpOnGround * screenDelta.y) *
            worldUnitsPerPixel *
            dragPanSensitivity;

        topDownPanOffset += move;
    }

    public void AddFocusedOrbit(Vector2 screenDelta)
    {
        if (focusedAgent == null)
            return;

        if (screenDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        focusedOrbitYaw += screenDelta.x * orbitSensitivity;
        focusedOrbitPitch -= screenDelta.y * orbitSensitivity;
        focusedOrbitPitch = Mathf.Clamp(focusedOrbitPitch, minOrbitPitch, maxOrbitPitch);
    }

    public void AddZoom(float scrollY)
    {
        if (Mathf.Abs(scrollY) <= Mathf.Epsilon)
            return;

        float zoomDelta = -scrollY * wheelZoomSensitivity * 0.05f;

        if (focusedAgent != null)
        {
            currentFocusedOrthoSize = Mathf.Clamp(
                currentFocusedOrthoSize + zoomDelta,
                minFocusedOrthoSize,
                maxFocusedOrthoSize
            );
            return;
        }

        if (topDownViewMode == TopDownViewMode.FixedView)
        {
            currentTopDownOrthoSize = Mathf.Clamp(
                currentTopDownOrthoSize + zoomDelta,
                minTopDownOrthoSize,
                maxTopDownOrthoSize
            );
        }
        else
        {
            currentFitZoomOffset = Mathf.Clamp(
                currentFitZoomOffset + zoomDelta,
                minFitZoomOffset,
                maxFitZoomOffset
            );
        }
    }

    public bool TryRaycastGround(Vector2 screenPosition, out RaycastHit hit)
    {
        Ray ray = cam.ScreenPointToRay(screenPosition);
        return Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore);
    }

    public Vector3 GetFocusedAgentWorldPoint()
    {
        if (focusedAgent == null)
            return Vector3.zero;

        if (cachedFocusAnchor != null)
            return cachedFocusAnchor.position + focusTargetOffset;

        if (TryGetCombinedBounds(cachedFocusRenderers, IsValidFocusedRenderer, out Bounds rendererBounds))
            return rendererBounds.center + focusTargetOffset;

        if (TryGetCombinedBounds(cachedFocusColliders, IsValidFocusedCollider, out Bounds colliderBounds))
            return colliderBounds.center + focusTargetOffset;

        return focusedAgent.position + focusTargetOffset;
    }

    private void UpdateTopDownView()
    {
        if (!hasGroundBounds)
            return;

        Vector3 center = groundBounds.center + topDownPanOffset;
        Vector3 desiredPosition;
        float desiredOrthoSize;

        if (topDownViewMode == TopDownViewMode.FixedView)
        {
            desiredPosition = center + topDownLocalOffset;
            desiredOrthoSize = currentTopDownOrthoSize;
        }
        else
        {
            desiredPosition = new Vector3(center.x, center.y + fitTopDownHeight, center.z);

            float sizeFromX = groundBounds.extents.x / Mathf.Max(cam.aspect, 0.01f);
            float sizeFromZ = groundBounds.extents.z;
            float baseOrthoSize = Mathf.Max(sizeFromX, sizeFromZ) + fitPadding;

            desiredOrthoSize = Mathf.Clamp(
                baseOrthoSize + currentFitZoomOffset,
                minTopDownOrthoSize,
                maxTopDownOrthoSize
            );
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            cachedTopDownRotation,
            smoothSpeed * Time.deltaTime
        );

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            desiredOrthoSize,
            smoothSpeed * Time.deltaTime
        );
    }

    private void UpdateFocusedAgentView()
    {
        if (focusedAgent == null)
            return;

        Vector3 focusPoint = GetFocusedAgentWorldPoint();
        Quaternion orbitRotation = Quaternion.Euler(focusedOrbitPitch, focusedOrbitYaw, focusedOrbitRoll);
        Vector3 desiredPosition = focusPoint + orbitRotation * focusedLocalOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            orbitRotation,
            smoothSpeed * Time.deltaTime
        );

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            currentFocusedOrthoSize,
            smoothSpeed * Time.deltaTime
        );
    }

    private void CacheFocusedAgentData(Transform root)
    {
        ClearFocusedAgentCache();

        if (root == null)
            return;

        cachedFocusAnchor = FindChildRecursive(root, focusAnchorName);
        cachedFocusRenderers = root.GetComponentsInChildren<Renderer>(true);
        cachedFocusColliders = root.GetComponentsInChildren<Collider>(true);
    }

    private void ClearFocusedAgentCache()
    {
        cachedFocusAnchor = null;
        cachedFocusRenderers = null;
        cachedFocusColliders = null;
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ResetFocusedOrbitAngles()
    {
        focusedOrbitPitch = focusedViewEuler.x;
        focusedOrbitYaw = focusedViewEuler.y;
        focusedOrbitRoll = focusedViewEuler.z;
    }

    private void ResetZoomStates()
    {
        currentTopDownOrthoSize = Mathf.Clamp(
            fixedTopDownOrthoSize,
            minTopDownOrthoSize,
            maxTopDownOrthoSize
        );

        currentFocusedOrthoSize = Mathf.Clamp(
            focusedOrthoSize,
            minFocusedOrthoSize,
            maxFocusedOrthoSize
        );

        currentFitZoomOffset = 0f;
    }

    private void RebuildRotationCaches()
    {
        cachedTopDownRotation = Quaternion.Euler(topDownEuler);
        RebuildPanBasis();
    }

    private void RebuildPanBasis()
    {
        cachedPanRightOnGround = Vector3.ProjectOnPlane(cachedTopDownRotation * Vector3.right, Vector3.up);
        cachedPanUpOnGround = Vector3.ProjectOnPlane(cachedTopDownRotation * Vector3.up, Vector3.up);

        if (cachedPanRightOnGround.sqrMagnitude < 0.0001f)
            cachedPanRightOnGround = Vector3.right;
        else
            cachedPanRightOnGround.Normalize();

        if (cachedPanUpOnGround.sqrMagnitude < 0.0001f)
            cachedPanUpOnGround = Vector3.forward;
        else
            cachedPanUpOnGround.Normalize();
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask.value) != 0;
    }

    private bool IsValidGroundRenderer(Renderer rend)
    {
        return rend != null && IsInLayerMask(rend.gameObject.layer, groundLayer);
    }

    private bool IsValidGroundCollider(Collider col)
    {
        return col != null && IsInLayerMask(col.gameObject.layer, groundLayer);
    }

    private bool IsValidFocusedRenderer(Renderer rend)
    {
        return rend != null && rend.enabled;
    }

    private bool IsValidFocusedCollider(Collider col)
    {
        return col != null && !col.isTrigger;
    }

    private bool TryGetCombinedBounds<T>(T[] items, System.Predicate<T> isValid, out Bounds combinedBounds)
        where T : Component
    {
        combinedBounds = default;

        if (items == null || items.Length == 0)
            return false;

        bool foundAny = false;

        for (int i = 0; i < items.Length; i++)
        {
            T item = items[i];
            if (item == null || !isValid(item))
                continue;

            Bounds itemBounds = GetBounds(item);

            if (!foundAny)
            {
                combinedBounds = itemBounds;
                foundAny = true;
            }
            else
            {
                combinedBounds.Encapsulate(itemBounds);
            }
        }

        return foundAny;
    }

    private Bounds GetBounds(Component component)
    {
        if (component is Renderer renderer)
            return renderer.bounds;

        if (component is Collider collider)
            return collider.bounds;

        return default;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo)
            return;

        if (hasGroundBounds)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundBounds.center, groundBounds.size);
        }

        if (focusedAgent != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetFocusedAgentWorldPoint(), 0.4f);
        }
    }
}