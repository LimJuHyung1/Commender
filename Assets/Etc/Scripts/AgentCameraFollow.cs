using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// Camera 컴포넌트가 반드시 필요하므로 자동으로 요구한다.
[RequireComponent(typeof(Camera))]
public class AgentCameraFollow : MonoBehaviour
{
    [Header("Ground Reference")]
    // 맵의 바닥 오브젝트 루트.
    // 이 하위의 Renderer / Collider bounds를 모아 전체 맵 범위를 계산한다.
    [SerializeField] private Transform groundRoot;

    // 바닥 판정에 사용할 레이어 마스크.
    [SerializeField] private LayerMask groundLayer;

    [Header("Common Camera Settings")]
    // 카메라 이동, 회전, orthographicSize 보간 속도.
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Top View Settings")]
    // 전체 탑뷰 상태에서 카메라의 높이.
    [SerializeField] private float topDownHeight = 30f;

    // 맵 bounds를 화면에 여유 있게 담기 위한 padding.
    [SerializeField] private float fitPadding = -1f;

    // 탑뷰 상태에서의 Yaw 회전값.
    [SerializeField] private float topDownYaw = 0f;

    [Header("Focused Agent View")]
    [SerializeField] private string focusAnchorName = "CameraFocusPoint";
    [SerializeField] private Vector3 focusTargetOffset = Vector3.zero;
    [SerializeField] private Vector3 focusedViewEuler = new Vector3(45f, -45f, 0f);
    [SerializeField] private Vector3 focusedLocalOffset = new Vector3(0f, 0f, -7f);
    [SerializeField] private float focusedOrthoSize = 6f;

    [Header("Ground Click Settings")]
    // Gizmos 및 디버그 정보 표시 여부.
    [SerializeField] private bool showDebugInfo = true;

    // 좌표 라벨이 화면에 남아있는 시간.
    [SerializeField] private float clickLabelDuration = 2f;

    // 클릭 위치 근처에 좌표 라벨을 띄울 때의 화면 오프셋.
    [SerializeField] private Vector2 clickLabelOffset = new Vector2(16f, -24f);

    [Header("Copy Settings")]
    // 좌표 복사 후 "Copied!" 라벨이 유지되는 시간.
    [SerializeField] private float copiedLabelDuration = 1.0f;

    // 좌표 라벨의 크기.
    [SerializeField] private Vector2 labelSize = new Vector2(140f, 28f);

    // 현재 카메라 참조.
    private Camera cam;

    // groundRoot 하위에서 계산된 전체 바닥 bounds.
    private Bounds groundBounds;

    // groundBounds가 정상 계산되었는지 여부.
    private bool hasGroundBounds = false;

    // 마지막으로 클릭한 바닥 월드 좌표.
    private Vector3 lastClickedGroundPoint;

    // 클릭 좌표가 한 번이라도 저장되었는지 여부.
    private bool hasClickedGroundPoint = false;

    // 마지막 클릭 시점의 스크린 좌표.
    private Vector2 lastClickScreenPosition;

    // 좌표 라벨 표시 종료 시간.
    private float clickLabelEndTime = 0f;

    // "Copied!" 텍스트 표시 종료 시간.
    private float copiedLabelEndTime = 0f;

    // OnGUI용 라벨 스타일.
    private GUIStyle labelStyle;

    // UI 위 클릭 여부 판정 시 재사용할 Raycast 결과 리스트.
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    // 현재 포커스 중인 에이전트 Transform.
    private Transform focusedAgent;

    // 외부에서 마지막 클릭 좌표를 읽을 수 있도록 제공.
    public Vector3 LastClickedGroundPoint => lastClickedGroundPoint;

    // 외부에서 클릭 좌표 존재 여부를 확인할 수 있도록 제공.
    public bool HasClickedGroundPoint => hasClickedGroundPoint;

    private void Awake()
    {
        // Camera 컴포넌트 캐싱.
        cam = GetComponent<Camera>();

        // 이 스크립트는 정사영 카메라를 전제로 한다.
        cam.orthographic = true;

        // 시작 시 ground bounds를 계산한다.
        RefreshGroundBounds();
    }

    private void Update()
    {
        // 마우스가 존재하고, 좌클릭이 이번 프레임에 눌렸는지 확인한다.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            // 먼저 클릭한 위치가 기존 좌표 라벨 위라면 좌표 문자열 복사를 시도한다.
            if (TryCopyClickedCoordinate(mousePosition))
                return;

            // 라벨 클릭이 아니라면 바닥 좌표 검출을 시도한다.
            DetectGroundPoint(mousePosition);
        }
    }

    private void LateUpdate()
    {
        // 특정 에이전트에 포커스 중이라면 그 뷰를 우선 갱신한다.
        if (focusedAgent != null)
        {
            UpdateFocusedAgentView();
            return;
        }

        // 포커스 중인 대상이 없다면 전체 탑뷰 상태를 유지한다.
        UpdateTopDownView();
    }

    // 특정 에이전트를 따라보는 포커스 모드로 전환한다.
    public void FocusAgent(Transform agentTransform)
    {
        focusedAgent = agentTransform;
    }

    // 포커스를 해제하고 다시 전체 탑뷰 모드로 복귀한다.
    public void ClearFocusAgent()
    {
        focusedAgent = null;
    }

    // 전체 지면 bounds를 기준으로 탑뷰 카메라를 갱신한다.
    private void UpdateTopDownView()
    {
        if (!hasGroundBounds)
            return;

        Vector3 center = groundBounds.center;

        // 맵 중심 상공에서 내려다보는 위치.
        Vector3 desiredPosition = new Vector3(center.x, topDownHeight, center.z);

        // 위에서 내려다보는 회전값.
        Quaternion desiredRotation = Quaternion.Euler(90f, topDownYaw, 90f);

        // orthographic 카메라에서 맵 전체가 화면 안에 들어오도록 size 계산.
        float sizeFromX = groundBounds.extents.x / Mathf.Max(cam.aspect, 0.01f);
        float sizeFromZ = groundBounds.extents.z;
        float desiredOrthoSize = Mathf.Max(sizeFromX, sizeFromZ) + fitPadding;

        // 부드럽게 위치/회전/줌을 보간한다.
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, smoothSpeed * Time.deltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredOrthoSize, smoothSpeed * Time.deltaTime);
    }

    // 특정 에이전트를 비스듬히 바라보는 포커스 카메라 상태를 갱신한다.
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

    // groundRoot 하위의 Renderer / Collider를 바탕으로 전체 바닥 bounds를 다시 계산한다.
    public void RefreshGroundBounds()
    {
        if (groundRoot == null)
        {
            hasGroundBounds = false;
            Debug.LogWarning("[Camera] groundRoot가 연결되지 않았습니다.");
            return;
        }

        // 먼저 Renderer bounds를 우선적으로 사용해 맵 범위를 계산한다.
        Renderer[] renderers = groundRoot.GetComponentsInChildren<Renderer>(true);

        bool foundAny = false;
        Bounds combinedBounds = default;

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            // groundLayer에 속한 오브젝트만 bounds 계산에 포함한다.
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

        // Renderer에서 찾지 못했다면 Collider bounds로 한 번 더 시도한다.
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

    // 마우스 위치를 기준으로 바닥 클릭 좌표를 검출한다.
    private void DetectGroundPoint(Vector2 mousePosition)
    {
        // UI 위를 클릭한 경우에는 바닥 클릭으로 처리하지 않는다.
        if (IsPointerOverUI(mousePosition))
            return;

        Ray ray = cam.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        // groundLayer에 대해서만 Raycast를 수행한다.
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // 클릭된 월드 좌표 저장.
            lastClickedGroundPoint = hit.point;
            hasClickedGroundPoint = true;

            // 라벨은 클릭 지점 근처 스크린 위치에 띄운다.
            lastClickScreenPosition = mousePosition + clickLabelOffset;
            clickLabelEndTime = Time.unscaledTime + clickLabelDuration;

            Debug.Log($"[Camera] Ground 클릭 좌표: {lastClickedGroundPoint}");
        }
    }

    // 현재 표시 중인 좌표 라벨을 클릭했다면 좌표 문자열을 클립보드에 복사한다.
    private bool TryCopyClickedCoordinate(Vector2 mousePosition)
    {
        // 라벨이 안 보이면 복사 시도할 필요가 없다.
        if (!IsLabelVisible())
            return false;

        Rect labelRect = GetLabelRect();

        // GUI 좌표계는 Screen 좌표계와 Y축 방향이 반대이므로 변환한다.
        Vector2 guiMousePosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);

        if (!labelRect.Contains(guiMousePosition))
            return false;

        string copyText = GetCopyCoordinateText();
        GUIUtility.systemCopyBuffer = copyText;
        copiedLabelEndTime = Time.unscaledTime + copiedLabelDuration;

        Debug.Log($"[Camera] 좌표 문자열 복사됨: {copyText}");
        return true;
    }

    // 현재 마우스 포인터가 UI 위에 있는지 EventSystem으로 판정한다.
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

    // 특정 레이어가 LayerMask에 포함되어 있는지 확인한다.
    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask.value) != 0;
    }

    // 좌표 라벨이 현재 표시 중인지 확인한다.
    private bool IsLabelVisible()
    {
        if (!hasClickedGroundPoint)
            return false;

        return Time.unscaledTime <= clickLabelEndTime;
    }

    // 좌표 라벨이 화면에 표시될 Rect를 계산한다.
    private Rect GetLabelRect()
    {
        Vector2 guiPosition = new Vector2(
            lastClickScreenPosition.x,
            Screen.height - lastClickScreenPosition.y
        );

        return new Rect(guiPosition.x, guiPosition.y, labelSize.x, labelSize.y);
    }

    // 라벨에 표시할 문자열을 반환한다.
    // 복사 직후 짧은 시간 동안은 "Copied!"를 표시한다.
    private string GetDisplayLabelText()
    {
        if (Time.unscaledTime <= copiedLabelEndTime)
            return "Copied!";

        return $"({lastClickedGroundPoint.x:F1}, {lastClickedGroundPoint.z:F1})";
    }

    // 실제 클립보드에 복사할 문자열 형식.
    private string GetCopyCoordinateText()
    {
        return $"{lastClickedGroundPoint.x:F1},{lastClickedGroundPoint.z:F1}";
    }

    private void OnGUI()
    {
        if (!IsLabelVisible())
            return;

        // 스타일은 한 번만 생성해서 재사용한다.
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box);
            labelStyle.fontSize = 16;
            labelStyle.alignment = TextAnchor.MiddleCenter;
        }

        Rect rect = GetLabelRect();
        GUI.Box(rect, GetDisplayLabelText(), labelStyle);
    }

    // 외부에서 타겟 목록이 갱신되었을 때 호출 가능.
    // 현재 구현에서는 ground bounds만 다시 계산한다.
    public void SetTargets(List<Transform> newTargets)
    {
        RefreshGroundBounds();
    }

    private void OnDrawGizmos()
    {
        // 마지막 클릭 좌표를 Scene 뷰에서 확인할 수 있도록 표시.
        if (showDebugInfo && hasClickedGroundPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastClickedGroundPoint, 0.5f);
        }

        // 계산된 ground bounds를 Scene 뷰에서 확인할 수 있도록 표시.
        if (showDebugInfo && hasGroundBounds)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundBounds.center, groundBounds.size);
        }

        // 포커스 대상 위치를 Scene 뷰에서 확인할 수 있도록 표시.
        if (showDebugInfo && focusedAgent != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetFocusedAgentWorldPoint(), 0.4f);
        }
    }

    public void SetGroundRoot(Transform newGroundRoot)
    {
        groundRoot = newGroundRoot;
        RefreshGroundBounds();
    }
}