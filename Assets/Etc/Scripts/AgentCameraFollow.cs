using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class AgentCameraFollow : MonoBehaviour
{
    [Header("Ground Reference")]
    [SerializeField] private Transform groundRoot;
    [SerializeField] private LayerMask groundLayer;

    [Header("Common Camera Settings")]
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Top View Settings")]
    [SerializeField] private float topDownHeight = 30f;
    [SerializeField] private float fitPadding = 2f;
    [SerializeField] private float topDownYaw = 0f;

    [Header("Focused Agent View")]
    [SerializeField] private Vector3 focusTargetOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 focusedViewEuler = new Vector3(45f, 45f, 0f);
    [SerializeField] private Vector3 focusedLocalOffset = new Vector3(0f, 0f, -14f);
    [SerializeField] private float focusedOrthoSize = 6f;

    [Header("Ground Click Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private float clickLabelDuration = 2f;
    [SerializeField] private Vector2 clickLabelOffset = new Vector2(16f, -24f);

    private Camera cam;
    private Bounds groundBounds;
    private bool hasGroundBounds = false;

    private Vector3 lastClickedGroundPoint;
    private bool hasClickedGroundPoint = false;

    private Vector2 lastClickScreenPosition;
    private float clickLabelEndTime = 0f;

    private GUIStyle labelStyle;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private Transform focusedAgent;

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
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            DetectGroundPoint();
        }
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

    private void UpdateTopDownView()
    {
        if (!hasGroundBounds)
            return;

        Vector3 center = groundBounds.center;

        Vector3 desiredPosition = new Vector3(center.x, topDownHeight, center.z);
        Quaternion desiredRotation = Quaternion.Euler(90f, topDownYaw, 0f);

        float sizeFromX = groundBounds.extents.x / Mathf.Max(cam.aspect, 0.01f);
        float sizeFromZ = groundBounds.extents.z;
        float desiredOrthoSize = Mathf.Max(sizeFromX, sizeFromZ) + fitPadding;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredOrthoSize, smoothSpeed * Time.deltaTime);
    }

    private void UpdateFocusedAgentView()
    {
        if (focusedAgent == null)
            return;

        Quaternion desiredRotation = Quaternion.Euler(focusedViewEuler);
        Vector3 focusPoint = focusedAgent.position + focusTargetOffset;
        Vector3 desiredPosition = focusPoint + desiredRotation * focusedLocalOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, focusedOrthoSize, smoothSpeed * Time.deltaTime);
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
        {
            Debug.LogWarning("[Camera] groundRoot 아래에서 Ground 레이어 bounds를 찾지 못했습니다.");
        }
    }

    private void DetectGroundPoint()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

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

    private void OnGUI()
    {
        if (!hasClickedGroundPoint)
            return;

        if (Time.unscaledTime > clickLabelEndTime)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box);
            labelStyle.fontSize = 16;
            labelStyle.alignment = TextAnchor.MiddleCenter;
        }

        string labelText = $"({lastClickedGroundPoint.x:F1}, {lastClickedGroundPoint.z:F1})";

        Vector2 guiPosition = new Vector2(
            lastClickScreenPosition.x,
            Screen.height - lastClickScreenPosition.y
        );

        Rect rect = new Rect(guiPosition.x, guiPosition.y, 140f, 28f);
        GUI.Box(rect, labelText, labelStyle);
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
            Gizmos.DrawWireSphere(focusedAgent.position + focusTargetOffset, 0.4f);
        }
    }
}