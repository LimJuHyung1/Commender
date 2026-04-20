using UnityEngine;
using UnityEngine.UI;

public class AgentStateController : MonoBehaviour
{
    public enum AgentAwarenessState
    {
        None,
        ChasingTarget,
        BlindedBySmoke
    }

    [Header("References")]
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image stateIcon;
    [SerializeField] private Camera targetCamera;

    [Header("Sprites")]
    [SerializeField] private Sprite eyeSprite;
    [SerializeField] private Sprite eyeSlashSprite;

    [Header("Local Position")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private float towardCameraOffset = 0.2f;

    [Header("Options")]
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private bool hideWhenNone = true;
    [SerializeField] private bool useMainCameraIfMissing = true;

    private AgentAwarenessState currentState = AgentAwarenessState.None;
    private Vector3 currentLocalOffset;

    public AgentAwarenessState CurrentState => currentState;

    private void Awake()
    {
        if (iconRoot == null)
            iconRoot = GetComponent<RectTransform>();

        ResolveCameraIfNeeded();

        currentLocalOffset = localOffset;
        ApplyStateVisual(currentState, true);
    }

    private void LateUpdate()
    {
        ResolveCameraIfNeeded();
        UpdateIconTransform();
    }

    public void SetState(AgentAwarenessState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        ApplyStateVisual(currentState, false);
    }

    public void ClearState()
    {
        SetState(AgentAwarenessState.None);
    }

    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        UpdateIconTransform();
    }

    private void ResolveCameraIfNeeded()
    {
        if (!useMainCameraIfMissing)
            return;

        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main;
    }

    private void ApplyStateVisual(AgentAwarenessState state, bool forceUpdateTransform)
    {
        if (stateIcon == null)
            return;

        switch (state)
        {
            case AgentAwarenessState.None:
                stateIcon.sprite = null;

                if (hideWhenNone)
                {
                    stateIcon.enabled = false;
                }
                else
                {
                    stateIcon.enabled = true;
                    stateIcon.color = new Color(1f, 1f, 1f, 0f);
                }
                break;

            case AgentAwarenessState.ChasingTarget:
                stateIcon.sprite = eyeSprite;
                stateIcon.enabled = true;
                stateIcon.color = Color.white;
                break;

            case AgentAwarenessState.BlindedBySmoke:
                stateIcon.sprite = eyeSlashSprite;
                stateIcon.enabled = true;
                stateIcon.color = Color.white;
                break;
        }

        if (forceUpdateTransform)
            UpdateIconTransform();
    }

    private void UpdateIconTransform()
    {
        if (iconRoot == null)
            return;

        UpdateLocalOffsetTowardCamera();

        iconRoot.localPosition = currentLocalOffset;

        if (billboardToCamera)
            RotateTowardCamera();
    }

    private void UpdateLocalOffsetTowardCamera()
    {
        currentLocalOffset = localOffset;

        if (targetCamera == null || iconRoot.parent == null || Mathf.Abs(towardCameraOffset) <= 0.0001f)
            return;

        Vector3 toCameraWorld = targetCamera.transform.position - iconRoot.parent.position;

        if (toCameraWorld.sqrMagnitude <= 0.0001f)
            return;

        Vector3 localDir = iconRoot.parent.InverseTransformDirection(toCameraWorld.normalized);
        currentLocalOffset += localDir * towardCameraOffset;
    }

    private void RotateTowardCamera()
    {
        if (iconRoot == null || targetCamera == null)
            return;

        Vector3 toCamera = iconRoot.position - targetCamera.transform.position;

        if (toCamera.sqrMagnitude <= 0.0001f)
            return;

        iconRoot.rotation = Quaternion.LookRotation(toCamera.normalized, targetCamera.transform.up);
    }
}