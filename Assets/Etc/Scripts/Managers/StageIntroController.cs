using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class StageIntroController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas introCanvas;
    [SerializeField] private Canvas gameplayCanvas;

    [Header("Intro UI")]
    [SerializeField] private RectTransform topBlackBar;
    [SerializeField] private RectTransform bottomBlackBar;
    [SerializeField] private Text stageInfoText;
    [SerializeField] private string titleFormat = "{0} ({1})";

    [Header("Timing")]
    [SerializeField] private float introDuration = 5f;
    [SerializeField] private float skipInputDelay = 0.25f;
    [SerializeField] private float barSlideDuration = 0.4f;
    [SerializeField] private float hiddenBarPadding = 20f;

    [Header("Camera")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera gameplayCamera;

    [Header("Focus")]
    [SerializeField] private Transform explicitFocusTarget;
    [SerializeField] private string autoFocusPointName = "IntroFocusPoint";
    [SerializeField] private string groundRootName = "GroundRoot";
    [SerializeField] private float lookHeight = 1.5f;

    [Header("Orbit")]
    [SerializeField] private float orbitDegrees = 120f;
    [SerializeField] private AnimationCurve orbitEase = null;
    [SerializeField] private AnimationCurve barEase = null;

    [Header("Gameplay Lock")]
    [SerializeField] private bool pauseTimeScaleDuringIntro = true;
    [SerializeField] private MonoBehaviour[] disableDuringIntroBehaviours;
    [SerializeField] private GameObject[] deactivateDuringIntroObjects;
    [SerializeField] private GameObject[] activateOnIntroFinishedObjects;

    private Vector2 topShownPos;
    private Vector2 bottomShownPos;
    private Vector2 topHiddenPos;
    private Vector2 bottomHiddenPos;

    private Vector3 focusPoint;
    private Vector3 initialCameraOffset;

    private AudioListener introAudioListener;
    private AudioListener gameplayAudioListener;

    private float previousTimeScale = 1f;
    private bool introFinished;

    private void Reset()
    {
        orbitEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        barEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        CacheAudioListeners();
        CacheBarPositions();
        ApplyInitialVisualState();
        LockGameplay();
    }

    private void Start()
    {
        if (pauseTimeScaleDuringIntro)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        StartCoroutine(BeginIntroRoutine());
    }

    private void OnDestroy()
    {
        if (!introFinished && pauseTimeScaleDuringIntro)
            Time.timeScale = previousTimeScale;
    }

    public void SkipIntro()
    {
        if (introFinished)
            return;

        StartCoroutine(FinishIntroRoutine());
    }

    private IEnumerator BeginIntroRoutine()
    {
        // StageMapManager가 Start에서 맵을 생성하므로 한 프레임 대기
        yield return null;

        UpdateStageInfoText();
        ResolveFocusPoint();
        PrepareIntroCamera();

        yield return StartCoroutine(AnimateBars(show: true));

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, introDuration));
            UpdateOrbit(normalized);

            if (elapsed >= skipInputDelay && IsSkipInputPressedThisFrame())
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!introFinished)
            StartCoroutine(FinishIntroRoutine());
    }

    private IEnumerator FinishIntroRoutine()
    {
        if (introFinished)
            yield break;

        introFinished = true;

        yield return StartCoroutine(AnimateBars(show: false));

        UnlockGameplay();
    }

    private void CacheAudioListeners()
    {
        introAudioListener = introCamera != null ? introCamera.GetComponent<AudioListener>() : null;
        gameplayAudioListener = gameplayCamera != null ? gameplayCamera.GetComponent<AudioListener>() : null;

        if (introCamera != null && introAudioListener == null)
            introAudioListener = introCamera.GetComponentInChildren<AudioListener>(true);

        if (gameplayCamera != null && gameplayAudioListener == null)
            gameplayAudioListener = gameplayCamera.GetComponentInChildren<AudioListener>(true);
    }

    private void SetActiveAudioListener(bool useIntroCamera)
    {
        if (introAudioListener != null)
            introAudioListener.enabled = useIntroCamera;

        if (gameplayAudioListener != null)
            gameplayAudioListener.enabled = !useIntroCamera;
    }

    private void CacheBarPositions()
    {
        if (topBlackBar != null)
        {
            topShownPos = topBlackBar.anchoredPosition;
            float hiddenY = topShownPos.y + topBlackBar.rect.height + hiddenBarPadding;
            topHiddenPos = new Vector2(topShownPos.x, hiddenY);
        }

        if (bottomBlackBar != null)
        {
            bottomShownPos = bottomBlackBar.anchoredPosition;
            float hiddenY = bottomShownPos.y - bottomBlackBar.rect.height - hiddenBarPadding;
            bottomHiddenPos = new Vector2(bottomShownPos.x, hiddenY);
        }
    }

    private void ApplyInitialVisualState()
    {
        if (introCanvas != null)
            introCanvas.gameObject.SetActive(true);

        if (gameplayCanvas != null)
            gameplayCanvas.gameObject.SetActive(false);

        if (topBlackBar != null)
            topBlackBar.anchoredPosition = topHiddenPos;

        if (bottomBlackBar != null)
            bottomBlackBar.anchoredPosition = bottomHiddenPos;

        if (introCamera != null)
            introCamera.enabled = true;

        if (gameplayCamera != null)
            gameplayCamera.enabled = false;

        SetActiveAudioListener(true);

        if (stageInfoText != null)
            stageInfoText.text = "";
    }

    private void LockGameplay()
    {
        if (disableDuringIntroBehaviours != null)
        {
            for (int i = 0; i < disableDuringIntroBehaviours.Length; i++)
            {
                if (disableDuringIntroBehaviours[i] != null)
                    disableDuringIntroBehaviours[i].enabled = false;
            }
        }

        if (deactivateDuringIntroObjects != null)
        {
            for (int i = 0; i < deactivateDuringIntroObjects.Length; i++)
            {
                if (deactivateDuringIntroObjects[i] != null)
                    deactivateDuringIntroObjects[i].SetActive(false);
            }
        }

        if (activateOnIntroFinishedObjects != null)
        {
            for (int i = 0; i < activateOnIntroFinishedObjects.Length; i++)
            {
                if (activateOnIntroFinishedObjects[i] != null)
                    activateOnIntroFinishedObjects[i].SetActive(false);
            }
        }
    }

    private void UnlockGameplay()
    {
        if (pauseTimeScaleDuringIntro)
            Time.timeScale = previousTimeScale;

        if (introCanvas != null)
            introCanvas.gameObject.SetActive(false);

        if (gameplayCanvas != null)
            gameplayCanvas.gameObject.SetActive(true);

        if (introCamera != null)
            introCamera.enabled = false;

        if (gameplayCamera != null)
            gameplayCamera.enabled = true;

        SetActiveAudioListener(false);

        if (disableDuringIntroBehaviours != null)
        {
            for (int i = 0; i < disableDuringIntroBehaviours.Length; i++)
            {
                if (disableDuringIntroBehaviours[i] != null)
                    disableDuringIntroBehaviours[i].enabled = true;
            }
        }

        if (deactivateDuringIntroObjects != null)
        {
            for (int i = 0; i < deactivateDuringIntroObjects.Length; i++)
            {
                if (deactivateDuringIntroObjects[i] != null)
                    deactivateDuringIntroObjects[i].SetActive(true);
            }
        }

        if (activateOnIntroFinishedObjects != null)
        {
            for (int i = 0; i < activateOnIntroFinishedObjects.Length; i++)
            {
                if (activateOnIntroFinishedObjects[i] != null)
                    activateOnIntroFinishedObjects[i].SetActive(true);
            }
        }
    }

    private void UpdateStageInfoText()
    {
        if (stageInfoText == null)
            return;

        StageMapManager stageMapManager = FindFirstObjectByType<StageMapManager>();
        if (stageMapManager == null)
        {
            stageInfoText.text = "스테이지";
            return;
        }

        string stageName = stageMapManager.CurrentStageDisplayName;
        string difficultyName = stageMapManager.CurrentDifficultyDisplayName;
        stageInfoText.text = string.Format(titleFormat, stageName, difficultyName);
    }

    private void ResolveFocusPoint()
    {
        if (explicitFocusTarget != null)
        {
            focusPoint = explicitFocusTarget.position;
            return;
        }

        GameObject namedFocus = GameObject.Find(autoFocusPointName);
        if (namedFocus != null)
        {
            focusPoint = namedFocus.transform.position;
            return;
        }

        GameObject groundRoot = GameObject.Find(groundRootName);
        if (groundRoot != null)
        {
            if (TryGetBoundsCenterFromRenderers(groundRoot.transform, out Vector3 rendererCenter))
            {
                focusPoint = rendererCenter;
                return;
            }

            if (TryGetBoundsCenterFromColliders(groundRoot.transform, out Vector3 colliderCenter))
            {
                focusPoint = colliderCenter;
                return;
            }

            focusPoint = groundRoot.transform.position;
            return;
        }

        focusPoint = Vector3.zero;
    }

    private void PrepareIntroCamera()
    {
        if (introCamera == null)
            return;

        initialCameraOffset = introCamera.transform.position - focusPoint;

        if (initialCameraOffset.sqrMagnitude < 0.01f)
            initialCameraOffset = new Vector3(-14f, 10f, -14f);

        UpdateOrbit(0f);
    }

    private void UpdateOrbit(float normalized)
    {
        if (introCamera == null)
            return;

        float eased = orbitEase != null ? orbitEase.Evaluate(normalized) : normalized;
        float angle = orbitDegrees * eased;

        Vector3 rotatedOffset = Quaternion.Euler(0f, angle, 0f) * initialCameraOffset;
        Vector3 cameraPosition = focusPoint + rotatedOffset;

        introCamera.transform.position = cameraPosition;
        introCamera.transform.LookAt(focusPoint + Vector3.up * lookHeight);
    }

    private IEnumerator AnimateBars(bool show)
    {
        float duration = Mathf.Max(0.01f, barSlideDuration);
        float elapsed = 0f;

        Vector2 startTop = topBlackBar != null ? topBlackBar.anchoredPosition : Vector2.zero;
        Vector2 startBottom = bottomBlackBar != null ? bottomBlackBar.anchoredPosition : Vector2.zero;

        Vector2 targetTop = show ? topShownPos : topHiddenPos;
        Vector2 targetBottom = show ? bottomShownPos : bottomHiddenPos;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased = barEase != null ? barEase.Evaluate(t) : t;

            if (topBlackBar != null)
                topBlackBar.anchoredPosition = Vector2.LerpUnclamped(startTop, targetTop, eased);

            if (bottomBlackBar != null)
                bottomBlackBar.anchoredPosition = Vector2.LerpUnclamped(startBottom, targetBottom, eased);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (topBlackBar != null)
            topBlackBar.anchoredPosition = targetTop;

        if (bottomBlackBar != null)
            bottomBlackBar.anchoredPosition = targetBottom;
    }

    private bool IsSkipInputPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetBoundsCenterFromRenderers(Transform root, out Vector3 center)
    {
        center = Vector3.zero;

        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds bounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            return false;

        center = bounds.center;
        return true;
    }

    private bool TryGetBoundsCenterFromColliders(Transform root, out Vector3 center)
    {
        center = Vector3.zero;

        if (root == null)
            return false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds bounds = new Bounds();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
                continue;

            if (!hasBounds)
            {
                bounds = c.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        if (!hasBounds)
            return false;

        center = bounds.center;
        return true;
    }
}