using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CommanderCameraController))]
public class CommanderCameraInput : MonoBehaviour
{
    [Header("Controller Reference")]
    [SerializeField] private CommanderCameraController cameraController;

    [Header("Top View Drag Pan")]
    [SerializeField] private bool enableDragPan = true;
    [SerializeField] private float dragStartThreshold = 8f;

    [Header("Focused Agent Orbit")]
    [SerializeField] private bool enableFocusedOrbit = true;
    [SerializeField] private bool allowOrbitWhenPointerOverUI = true;
    [SerializeField] private float orbitStartThreshold = 3f;

    [Header("Mouse Wheel Zoom")]
    [SerializeField] private bool enableWheelZoom = true;
    [SerializeField] private bool allowWheelZoomWhenPointerOverUI = true;

    [Header("Ground Click Settings")]
    [SerializeField] private bool enableGroundClick = true;
    [SerializeField] private bool blockGroundClickWhenPointerOverUI = true;
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private float clickLabelDuration = 2f;
    [SerializeField] private Vector2 clickLabelOffset = new Vector2(16f, -24f);

    [Header("Copy Settings")]
    [SerializeField] private float copiedLabelDuration = 1f;
    [SerializeField] private Vector2 labelSize = new Vector2(140f, 28f);

    private Camera controlledCamera;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private int cachedUiCheckFrame = -1;
    private Vector2 cachedUiCheckScreenPosition;
    private bool cachedUiCheckResult;

    private bool isLeftMouseHeld;
    private bool isDraggingCamera;
    private Vector2 dragStartScreenPosition;
    private Vector2 lastDragScreenPosition;

    private bool isRightMouseHeld;
    private bool isOrbitDragging;
    private Vector2 orbitDragStartScreenPosition;
    private Vector2 lastOrbitDragScreenPosition;

    private Vector3 lastClickedGroundPoint;
    private bool hasClickedGroundPoint;
    private Vector2 lastClickScreenPosition;
    private float clickLabelEndTime;
    private float copiedLabelEndTime;

    private GUIStyle labelStyle;

    public Vector3 LastClickedGroundPoint => lastClickedGroundPoint;
    public bool HasClickedGroundPoint => hasClickedGroundPoint;
    public bool IsTopDownDragInputActive => !cameraController.HasFocusedAgent && isLeftMouseHeld;
    public bool IsTopDownDragging => !cameraController.HasFocusedAgent && isDraggingCamera;
    public bool IsFocusedOrbitInputActive => cameraController.HasFocusedAgent && isRightMouseHeld;
    public bool IsFocusedOrbitDragging => cameraController.HasFocusedAgent && isOrbitDragging;

    private void Reset()
    {
        cameraController = GetComponent<CommanderCameraController>();
    }

    private void Awake()
    {
        if (cameraController == null)
            cameraController = GetComponent<CommanderCameraController>();

        if (cameraController == null)
        {
            Debug.LogError("[CommanderCameraInput] CommanderCameraController가 필요합니다.");
            enabled = false;
            return;
        }

        controlledCamera = cameraController.ControlledCamera;

        if (EventSystem.current != null)
            pointerEventData = new PointerEventData(EventSystem.current);
    }

    private void Update()
    {
        if (cameraController == null)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (controlledCamera == null)
            controlledCamera = cameraController.ControlledCamera;

        Vector2 mousePosition = mouse.position.ReadValue();

        HandleLeftMouseInput(mouse, mousePosition);
        HandleRightMouseInput(mouse, mousePosition);
        HandleWheelZoom(mouse, mousePosition);
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
            TryStartTopDownDrag(mousePosition);

            if (isDraggingCamera)
                UpdateTopDownDrag(mousePosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (!isDraggingCamera)
            {
                if (TryCopyClickedCoordinate(mousePosition))
                {
                    ResetLeftDragState();
                    return;
                }

                DetectGroundPoint(mousePosition);
            }

            ResetLeftDragState();
        }
    }

    private void HandleRightMouseInput(Mouse mouse, Vector2 mousePosition)
    {
        if (!enableFocusedOrbit)
        {
            ResetOrbitDragState();
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (!cameraController.HasFocusedAgent)
            {
                ResetOrbitDragState();
                return;
            }

            if (!allowOrbitWhenPointerOverUI && IsPointerOverUI(mousePosition))
            {
                ResetOrbitDragState();
                return;
            }

            isRightMouseHeld = true;
            isOrbitDragging = false;
            orbitDragStartScreenPosition = mousePosition;
            lastOrbitDragScreenPosition = mousePosition;
        }

        if (isRightMouseHeld && mouse.rightButton.isPressed)
        {
            if (!cameraController.HasFocusedAgent)
            {
                ResetOrbitDragState();
                return;
            }

            TryStartOrbitDrag(mousePosition);

            if (isOrbitDragging)
                UpdateOrbitDrag(mousePosition);
        }

        if (mouse.rightButton.wasReleasedThisFrame)
            ResetOrbitDragState();
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

        cameraController.AddZoom(scrollY);
    }

    private void TryStartTopDownDrag(Vector2 mousePosition)
    {
        if (!enableDragPan)
            return;

        if (cameraController.HasFocusedAgent)
            return;

        if (isDraggingCamera)
            return;

        if (IsPointerOverUI(mousePosition))
            return;

        float draggedDistance = Vector2.Distance(mousePosition, dragStartScreenPosition);
        if (draggedDistance >= dragStartThreshold)
            isDraggingCamera = true;
    }

    private void UpdateTopDownDrag(Vector2 mousePosition)
    {
        Vector2 screenDelta = mousePosition - lastDragScreenPosition;
        lastDragScreenPosition = mousePosition;

        if (screenDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        cameraController.AddTopDownPan(screenDelta);
    }

    private void TryStartOrbitDrag(Vector2 mousePosition)
    {
        if (!cameraController.HasFocusedAgent)
            return;

        if (isOrbitDragging)
            return;

        if (!allowOrbitWhenPointerOverUI && IsPointerOverUI(mousePosition))
            return;

        float draggedDistance = Vector2.Distance(mousePosition, orbitDragStartScreenPosition);
        if (draggedDistance >= orbitStartThreshold)
            isOrbitDragging = true;
    }

    private void UpdateOrbitDrag(Vector2 mousePosition)
    {
        Vector2 screenDelta = mousePosition - lastOrbitDragScreenPosition;
        lastOrbitDragScreenPosition = mousePosition;

        if (screenDelta.sqrMagnitude <= Mathf.Epsilon)
            return;

        cameraController.AddFocusedOrbit(screenDelta);
    }

    private void DetectGroundPoint(Vector2 mousePosition)
    {
        if (!enableGroundClick)
            return;

        if (cameraController.HasFocusedAgent)
            return;

        if (blockGroundClickWhenPointerOverUI && IsPointerOverUI(mousePosition))
            return;

        if (!cameraController.TryRaycastGround(mousePosition, out RaycastHit hit))
            return;

        lastClickedGroundPoint = hit.point;
        hasClickedGroundPoint = true;
        lastClickScreenPosition = mousePosition + clickLabelOffset;
        clickLabelEndTime = Time.unscaledTime + clickLabelDuration;

        Debug.Log($"[CommanderCameraInput] Ground 클릭 좌표: {lastClickedGroundPoint}");
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

        Debug.Log($"[CommanderCameraInput] 좌표 문자열 복사됨: {copyText}");
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

    private void ResetLeftDragState()
    {
        isLeftMouseHeld = false;
        isDraggingCamera = false;
    }

    private void ResetOrbitDragState()
    {
        isRightMouseHeld = false;
        isOrbitDragging = false;
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
    }
}