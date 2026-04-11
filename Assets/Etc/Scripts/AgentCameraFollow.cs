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

    [Header("Ground Click Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private float clickLabelDuration = 2f;
    [SerializeField] private Vector2 clickLabelOffset = new Vector2(16f, -24f);

    [Header("Copy Settings")]
    [SerializeField] private float copiedLabelDuration = 1.0f;
    [SerializeField] private Vector2 labelSize = new Vector2(140f, 28f);

    private Camera cam;
    private Bounds groundBounds;
    private bool hasGroundBounds = false;

    private Vector3 lastClickedGroundPoint;
    private bool hasClickedGroundPoint = false;
    private Vector2 lastClickScreenPosition;
    private float clickLabelEndTime = 0f;
    private float copiedLabelEndTime = 0f;

    private GUIStyle labelStyle;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private Transform focusedAgent;

    private Vector3 topDownPanOffset = Vector3.zero;
    private bool isLeftMouseHeld = false;
    private bool isDraggingCamera = false;
    private Vector2 dragStartScreenPosition;
    private Vector2 lastDragScreenPosition;

    public Vector3 LastClickedGroundPoint => lastClickedGroundPoint;
    public bool HasClickedGroundPoint => hasClickedGroundPoint;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        RefreshGroundBounds();
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        HandleLeftMouseInput(Mouse.current.position.ReadValue());
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
        focusedAgent = agentTransform;
    }

    public void ClearFocusAgent()
    {
        focusedAgent = null;
    }

    private void HandleLeftMouseInput(Vector2 mousePosition)
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isLeftMouseHeld = true;
            isDraggingCamera = false;
            dragStartScreenPosition = mousePosition;
            lastDragScreenPosition = mousePosition;
        }

        if (isLeftMouseHeld && Mouse.current.leftButton.isPressed)
        {
            TryStartDrag(mousePosition);

            if (isDraggingCamera)
                UpdateDragPan(mousePosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
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

    private void TryStartDrag(Vector2 mousePosition)
    {
        if (focusedAgent != null)
            return;

        if (!enableDragPan)
            return;

        if (IsPointerOverUI(mousePosition))
            return;

        if (isDraggingCamera)
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

        Vector3 rightOnGround;
        Vector3 upOnGround;
        GetTopDownPanBasis(out rightOnGround, out upOnGround);

        float worldUnitsPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(Screen.height, 1);
        Vector3 move =
            (-rightOnGround * screenDelta.x - upOnGround * screenDelta.y) *
            worldUnitsPerPixel *
            dragPanSensitivity;

        topDownPanOffset += move;
    }

    private void GetTopDownPanBasis(out Vector3 rightOnGround, out Vector3 upOnGround)
    {
        Quaternion basisRotation = Quaternion.Euler(topDownEuler);

        rightOnGround = Vector3.ProjectOnPlane(basisRotation * Vector3.right, Vector3.up);
        upOnGround = Vector3.ProjectOnPlane(basisRotation * Vector3.up, Vector3.up);

        if (rightOnGround.sqrMagnitude < 0.0001f)
            rightOnGround = Vector3.right;
        else
            rightOnGround.Normalize();

        if (upOnGround.sqrMagnitude < 0.0001f)
            upOnGround = Vector3.forward;
        else
            upOnGround.Normalize();
    }

    private void ResetDragState()
    {
        isLeftMouseHeld = false;
        isDraggingCamera = false;
    }

    public void ResetTopDownPan()
    {
        topDownPanOffset = Vector3.zero;
    }

    private void UpdateTopDownView()
    {
        if (!hasGroundBounds)
            return;

        Vector3 center = groundBounds.center + topDownPanOffset;
        Vector3 desiredPosition;
        Quaternion desiredRotation = Quaternion.Euler(topDownEuler);
        float desiredOrthoSize;

        if (topDownViewMode == TopDownViewMode.FixedView)
        {
            desiredPosition = center + topDownLocalOffset;
            desiredOrthoSize = fixedTopDownOrthoSize;
        }
        else
        {
            desiredPosition = new Vector3(center.x, center.y + fitTopDownHeight, center.z);

            float sizeFromX = groundBounds.extents.x / Mathf.Max(cam.aspect, 0.01f);
            float sizeFromZ = groundBounds.extents.z;
            desiredOrthoSize = Mathf.Max(sizeFromX, sizeFromZ) + fitPadding;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredOrthoSize, smoothSpeed * Time.deltaTime);
    }

    private void UpdateFocusedAgentView()
    {
        if (focusedAgent == null)
            return;

        Quaternion desiredRotation = Quaternion.Euler(focusedViewEuler);
        Vector3 focusPoint = GetFocusedAgentWorldPoint();
        Vector3 desiredPosition = focusPoint + desiredRotation * focusedLocalOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, focusedOrthoSize, smoothSpeed * Time.deltaTime);
    }

    private Vector3 GetFocusedAgentWorldPoint()
    {
        if (focusedAgent == null)
            return Vector3.zero;

        Transform anchor = FindChildRecursive(focusedAgent, focusAnchorName);
        if (anchor != null)
            return anchor.position + focusTargetOffset;

        if (TryGetRendererBoundsCenter(focusedAgent, out Vector3 rendererCenter))
            return rendererCenter + focusTargetOffset;

        if (TryGetColliderBoundsCenter(focusedAgent, out Vector3 colliderCenter))
            return colliderCenter + focusTargetOffset;

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

    private bool TryGetRendererBoundsCenter(Transform root, out Vector3 center)
    {
        center = Vector3.zero;

        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool foundAny = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null || !rend.enabled)
                continue;

            if (!foundAny)
            {
                combinedBounds = rend.bounds;
                foundAny = true;
            }
            else
            {
                combinedBounds.Encapsulate(rend.bounds);
            }
        }

        if (!foundAny)
            return false;

        center = combinedBounds.center;
        return true;
    }

    private bool TryGetColliderBoundsCenter(Transform root, out Vector3 center)
    {
        center = Vector3.zero;

        if (root == null)
            return false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        bool foundAny = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            if (!foundAny)
            {
                combinedBounds = col.bounds;
                foundAny = true;
            }
            else
            {
                combinedBounds.Encapsulate(col.bounds);
            }
        }

        if (!foundAny)
            return false;

        center = combinedBounds.center;
        return true;
    }

    public void RefreshGroundBounds()
    {
        if (groundRoot == null)
        {
            hasGroundBounds = false;
            Debug.LogWarning("[Camera] groundRoot가 연결되지 않았습니다.");
            return;
        }

        Renderer[] renderers = groundRoot.GetComponentsInChildren<Renderer>(true);

        bool foundAny = false;
        Bounds combinedBounds = default;

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            if (!IsInLayerMask(rend.gameObject.layer, groundLayer))
                continue;

            if (!foundAny)
            {
                combinedBounds = rend.bounds;
                foundAny = true;
            }
            else
            {
                combinedBounds.Encapsulate(rend.bounds);
            }
        }

        if (!foundAny)
        {
            Collider[] colliders = groundRoot.GetComponentsInChildren<Collider>(true);

            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                if (!IsInLayerMask(col.gameObject.layer, groundLayer))
                    continue;

                if (!foundAny)
                {
                    combinedBounds = col.bounds;
                    foundAny = true;
                }
                else
                {
                    combinedBounds.Encapsulate(col.bounds);
                }
            }
        }

        hasGroundBounds = foundAny;
        groundBounds = combinedBounds;

        if (!hasGroundBounds)
            Debug.LogWarning("[Camera] groundRoot 아래에서 Ground 레이어 bounds를 찾지 못했습니다.");
    }

    private void DetectGroundPoint(Vector2 mousePosition)
    {
        if (IsPointerOverUI(mousePosition))
            return;

        Ray ray = cam.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
        {
            lastClickedGroundPoint = hit.point;
            hasClickedGroundPoint = true;
            lastClickScreenPosition = mousePosition + clickLabelOffset;
            clickLabelEndTime = Time.unscaledTime + clickLabelDuration;

            Debug.Log($"[Camera] Ground 클릭 좌표: {lastClickedGroundPoint}");
        }
    }

    private bool TryCopyClickedCoordinate(Vector2 mousePosition)
    {
        if (!IsLabelVisible())
            return false;

        Rect labelRect = GetLabelRect();
        Vector2 guiMousePosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);

        if (!labelRect.Contains(guiMousePosition))
            return false;

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

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPosition;

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        return uiRaycastResults.Count > 0;
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask.value) != 0;
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

    public void SetTargets(List<Transform> newTargets)
    {
        RefreshGroundBounds();
    }

    private void OnDrawGizmos()
    {
        if (showDebugInfo && hasClickedGroundPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastClickedGroundPoint, 0.5f);
        }

        if (showDebugInfo && hasGroundBounds)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundBounds.center, groundBounds.size);
        }

        if (showDebugInfo && focusedAgent != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetFocusedAgentWorldPoint(), 0.4f);
        }
    }

    public void SetGroundRoot(Transform newGroundRoot)
    {
        groundRoot = newGroundRoot;
        ResetTopDownPan();
        RefreshGroundBounds();
    }
}