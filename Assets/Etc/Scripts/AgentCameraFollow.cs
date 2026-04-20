using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class AgentCameraFollow : MonoBehaviour
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

    [Header("Top View Drag Pan")]
    [SerializeField] private bool enableDragPan = true;
    [SerializeField] private float dragPanSensitivity = 1f;
    [SerializeField] private float dragStartThreshold = 8f;

    [Header("Focused Agent View")]
    [SerializeField] private string focusAnchorName = "CameraFocusPoint";
    [SerializeField] private Vector3 focusTargetOffset = Vector3.zero;
    [SerializeField] private Vector3 focusedViewEuler = new Vector3(45f, -45f, 0f);
    [SerializeField] private Vector3 focusedLocalOffset = new Vector3(0f, 0f, -7f);
    [SerializeField] private float focusedOrthoSize = 6f;

    [Header("Focused Agent Orbit")]
    [SerializeField] private bool enableFocusedOrbit = true;
    [SerializeField] private bool allowOrbitWhenPointerOverUI = true;
    [SerializeField] private bool resetOrbitOnFocusChanged = true;
    [SerializeField] private float orbitSensitivity = 0.2f;
    [SerializeField] private float orbitStartThreshold = 3f;
    [SerializeField] private float minOrbitPitch = 15f;
    [SerializeField] private float maxOrbitPitch = 80f;

    [Header("Mouse Wheel Zoom")]
    [SerializeField] private bool enableWheelZoom = true;
    [SerializeField] private bool allowWheelZoomWhenPointerOverUI = true;
    [SerializeField] private float wheelZoomSensitivity = 5f;
    [SerializeField] private float minTopDownOrthoSize = 8f;
    [SerializeField] private float maxTopDownOrthoSize = 30f;
    [SerializeField] private float minFocusedOrthoSize = 3f;
    [SerializeField] private float maxFocusedOrthoSize = 12f;
    [SerializeField] private float minFitZoomOffset = -12f;
    [SerializeField] private float maxFitZoomOffset = 12f;

    [Header("Ground Click Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private float clickLabelDuration = 2f;
    [SerializeField] private Vector2 clickLabelOffset = new Vector2(16f, -24f);

    [Header("Copy Settings")]
    [SerializeField] private float copiedLabelDuration = 1.0f;
    [SerializeField] private Vector2 labelSize = new Vector2(140f, 28f);

    private Camera cam;

    private Bounds groundBounds;
    private bool hasGroundBounds;

    private Vector3 lastClickedGroundPoint;
    private bool hasClickedGroundPoint;
    private Vector2 lastClickScreenPosition;
    private float clickLabelEndTime;
    private float copiedLabelEndTime;

    private GUIStyle labelStyle;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private int cachedUiCheckFrame = -1;
    private Vector2 cachedUiCheckScreenPosition;
    private bool cachedUiCheckResult;

    private Transform focusedAgent;

    private Transform cachedFocusAnchor;
    private Renderer[] cachedFocusRenderers;
    private Collider[] cachedFocusColliders;

    private Vector3 topDownPanOffset = Vector3.zero;
    private bool isLeftMouseHeld;
    private bool isDraggingCamera;
    private Vector2 dragStartScreenPosition;
    private Vector2 lastDragScreenPosition;

    private bool isRightMouseHeld;
    private bool isOrbitDragging;
    private Vector2 orbitDragStartScreenPosition;
    private Vector2 lastOrbitDragScreenPosition;
    private float focusedOrbitYaw;
    private float focusedOrbitPitch;
    private float focusedOrbitRoll;

    private float currentTopDownOrthoSize;
    private float currentFocusedOrthoSize;
    private float currentFitZoomOffset;

    private Quaternion cachedTopDownRotation;
    private Vector3 cachedPanRightOnGround;
    private Vector3 cachedPanUpOnGround;

    public Vector3 LastClickedGroundPoint => lastClickedGroundPoint;
    public bool HasClickedGroundPoint => hasClickedGroundPoint;

    public bool HasFocusedAgent => focusedAgent != null;
    public bool IsFocusedOrbitInputActive => focusedAgent != null && isRightMouseHeld;
    public bool IsFocusedOrbitDragging => focusedAgent != null && isOrbitDragging;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        RebuildRotationCaches();
        ResetFocusedOrbitAngles();
        ResetZoomStates();
        RefreshGroundBounds();

        if (EventSystem.current != null)
            pointerEventData = new PointerEventData(EventSystem.current);
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

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        HandleLeftMouseInput(mouse, mousePosition);
        HandleRightMouseInput(mouse, mousePosition);
        HandleWheelZoom(mouse, mousePosition);
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
        ResetFocusedOrbitDragState();
    }

    public void ResetTopDownPan()
    {
        topDownPanOffset = Vector3.zero;
    }

    public void ResetZoom()
    {
        ResetZoomStates();
    }

    public void RefreshGroundBounds()
    {
        if (groundRoot == null)
        {
            hasGroundBounds = false;
            Debug.LogWarning("[Camera] groundRoot가 연결되지 않았습니다.");
            return;
        }

        bool foundAny =
            TryGetCombinedBounds(
                groundRoot.GetComponentsInChildren<Renderer>(true),
                IsValidGroundRenderer,
                out Bounds combinedBounds)
            ||
            TryGetCombinedBounds(
                groundRoot.GetComponentsInChildren<Collider>(true),
                IsValidGroundCollider,
                out combinedBounds);

        hasGroundBounds = foundAny;
        groundBounds = combinedBounds;

        if (!hasGroundBounds)
            Debug.LogWarning("[Camera] groundRoot 아래에서 Ground 레이어 bounds를 찾지 못했습니다.");
    }

    public void SetGroundRoot(Transform newGroundRoot)
    {
        groundRoot = newGroundRoot;
        ResetTopDownPan();
        RefreshGroundBounds();
    }

    public void SetTargets(List<Transform> newTargets)
    {
        RefreshGroundBounds();
    }

    private void HandleLeftMouseInput(Mouse mouse, Vector2 mousePosition)
    {
        if (mouse.leftButton.wasPressedThisFrame)
        {
            isLeftMouseHeld = true;
            isDraggingCamera = false;
            dragStartScreenPosition = mousePosition;
            lastDragScreenPosition = mousePosition;
        }

        if (isLeftMouseHeld && mouse.leftButton.isPressed)
        {
            TryStartDrag(mousePosition);

            if (isDraggingCamera)
                UpdateDragPan(mousePosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (!isDraggingCamera)
            {
                if (TryCopyClickedCoordinate(mousePosition))
                {
                    ResetDragState();
                    return;
                }

                DetectGroundPoint(mousePosition);
            }

            ResetDragState();
        }
    }

    private void HandleRightMouseInput(Mouse mouse, Vector2 mousePosition)
    {
        if (!enableFocusedOrbit)
        {
            ResetFocusedOrbitDragState();
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (focusedAgent == null)
            {
                ResetFocusedOrbitDragState();
                return;
            }

            if (!allowOrbitWhenPointerOverUI && IsPointerOverUI(mousePosition))
            {
                ResetFocusedOrbitDragState();
                return;
            }

            isRightMouseHeld = true;
            isOrbitDragging = false;
            orbitDragStartScreenPosition = mousePosition;
            lastOrbitDragScreenPosition = mousePosition;
        }

        if (isRightMouseHeld && mouse.rightButton.isPressed)
        {
            if (focusedAgent == null)
            {
                ResetFocusedOrbitDragState();
                return;
            }

            TryStartFocusedOrbit(mousePosition);

            if (isOrbitDragging)
                UpdateFocusedOrbit(mousePosition);
        }

        if (mouse.rightButton.wasReleasedThisFrame)
            ResetFocusedOrbitDragState();
    }

    private void HandleWheelZoom(Mouse mouse, Vector2 mousePosition)
    {
        if (!enableWheelZoom)
            return;

        if (!allowWheelZoomWhenPointerOverUI && IsPointerOverUI(mousePosition))
            return;

        float scrollY = mouse.scroll.ReadValue().y;
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

    private void TryStartDrag(Vector2 mousePosition)
    {
        if (focusedAgent != null)
            return;

        if (!enableDragPan)
            return;

        if (isDraggingCamera)
            return;

        if (IsPointerOverUI(mousePosition))
            return;

        float draggedDistance = Vector2.Distance(mousePosition, dragStartScreenPosition);
        if (draggedDistance >= dragStartThreshold)
            isDraggingCamera = true;
    }

    private void UpdateDragPan(Vector2 mousePosition)
    {
        Vector2 screenDelta = mousePosition - lastDragScreenPosition;
        lastDragScreenPosition = mousePosition;

        if (screenDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        float worldUnitsPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(Screen.height, 1);

        Vector3 move =
            (-cachedPanRightOnGround * screenDelta.x - cachedPanUpOnGround * screenDelta.y) *
            worldUnitsPerPixel *
            dragPanSensitivity;

        topDownPanOffset += move;
    }

    private void TryStartFocusedOrbit(Vector2 mousePosition)
    {
        if (focusedAgent == null)
            return;

        if (isOrbitDragging)
            return;

        if (!allowOrbitWhenPointerOverUI && IsPointerOverUI(mousePosition))
            return;

        float draggedDistance = Vector2.Distance(mousePosition, orbitDragStartScreenPosition);
        if (draggedDistance >= orbitStartThreshold)
            isOrbitDragging = true;
    }

    private void UpdateFocusedOrbit(Vector2 mousePosition)
    {
        Vector2 screenDelta = mousePosition - lastOrbitDragScreenPosition;
        lastOrbitDragScreenPosition = mousePosition;

        if (screenDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        focusedOrbitYaw += screenDelta.x * orbitSensitivity;
        focusedOrbitPitch -= screenDelta.y * orbitSensitivity;
        focusedOrbitPitch = Mathf.Clamp(focusedOrbitPitch, minOrbitPitch, maxOrbitPitch);
    }

    private void ResetDragState()
    {
        isLeftMouseHeld = false;
        isDraggingCamera = false;
    }

    private void ResetFocusedOrbitDragState()
    {
        isRightMouseHeld = false;
        isOrbitDragging = false;
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

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, cachedTopDownRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredOrthoSize, smoothSpeed * Time.deltaTime);
    }

    private void UpdateFocusedAgentView()
    {
        if (focusedAgent == null)
            return;

        Vector3 focusPoint = GetFocusedAgentWorldPoint();
        Quaternion orbitRotation = Quaternion.Euler(focusedOrbitPitch, focusedOrbitYaw, focusedOrbitRoll);
        Vector3 desiredPosition = focusPoint + orbitRotation * focusedLocalOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, orbitRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, currentFocusedOrthoSize, smoothSpeed * Time.deltaTime);
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

    private Vector3 GetFocusedAgentWorldPoint()
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

    private bool DetectGroundRaycast(Vector2 mousePosition, out RaycastHit hit)
    {
        Ray ray = cam.ScreenPointToRay(mousePosition);
        return Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void DetectGroundPoint(Vector2 mousePosition)
    {
        if (focusedAgent != null)
            return;

        if (IsPointerOverUI(mousePosition))
            return;

        if (!DetectGroundRaycast(mousePosition, out RaycastHit hit))
            return;

        lastClickedGroundPoint = hit.point;
        hasClickedGroundPoint = true;
        lastClickScreenPosition = mousePosition + clickLabelOffset;
        clickLabelEndTime = Time.unscaledTime + clickLabelDuration;

        Debug.Log($"[Camera] Ground 클릭 좌표: {lastClickedGroundPoint}");
    }

    private bool TryCopyClickedCoordinate(Vector2 mousePosition)
    {
        if (!IsLabelVisible())
            return false;

        Rect labelRect = GetLabelRect();
        Vector2 guiMousePosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);

        if (!labelRect.Contains(guiMousePosition))
            return false;

        CopiedCoordinateCache.Save(
    float.Parse(lastClickedGroundPoint.x.ToString("F1")),
    float.Parse(lastClickedGroundPoint.z.ToString("F1")),
    lastClickedGroundPoint
);

        string copyText = GetCopyCoordinateText();
        GUIUtility.systemCopyBuffer = copyText;
        copiedLabelEndTime = Time.unscaledTime + copiedLabelDuration;

        Debug.Log($"[Camera] 좌표 문자열 복사됨: {copyText}");
        return true;
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        if (pointerEventData == null)
            pointerEventData = new PointerEventData(EventSystem.current);

        if (cachedUiCheckFrame == Time.frameCount && cachedUiCheckScreenPosition == screenPosition)
            return cachedUiCheckResult;

        pointerEventData.position = screenPosition;
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);

        cachedUiCheckFrame = Time.frameCount;
        cachedUiCheckScreenPosition = screenPosition;
        cachedUiCheckResult = uiRaycastResults.Count > 0;

        return cachedUiCheckResult;
    }

    private bool IsLabelVisible()
    {
        if (!hasClickedGroundPoint)
            return false;

        return Time.unscaledTime <= clickLabelEndTime;
    }

    private Rect GetLabelRect()
    {
        Vector2 guiPosition = new Vector2(
            lastClickScreenPosition.x,
            Screen.height - lastClickScreenPosition.y
        );

        return new Rect(guiPosition.x, guiPosition.y, labelSize.x, labelSize.y);
    }

    private string GetDisplayLabelText()
    {
        if (Time.unscaledTime <= copiedLabelEndTime)
            return "Copied!";

        return $"({lastClickedGroundPoint.x:F1}, {lastClickedGroundPoint.z:F1})";
    }

    private string GetCopyCoordinateText()
    {
        return $"{lastClickedGroundPoint.x:F1},{lastClickedGroundPoint.z:F1}";
    }

    private void OnGUI()
    {
        if (!IsLabelVisible())
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box);
            labelStyle.fontSize = 16;
            labelStyle.alignment = TextAnchor.MiddleCenter;
        }

        Rect rect = GetLabelRect();
        GUI.Box(rect, GetDisplayLabelText(), labelStyle);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo)
            return;

        if (hasClickedGroundPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastClickedGroundPoint, 0.5f);
        }

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
}