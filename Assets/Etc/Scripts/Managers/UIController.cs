using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Stage UI")]
    [SerializeField] private Text missionText;
    [SerializeField] private Text timerText;

    [Header("Result UI")]
    [SerializeField] private Transform resultRoot;
    [SerializeField] private GameObject resultPanelObject;
    [SerializeField] private Text resultText;

    [Header("Option UI")]
    [SerializeField] private Transform optionsRoot;
    [SerializeField] private Button optionButton;
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private Button exitButton;

    [Header("Option Setting")]
    [SerializeField] private bool pauseGameWhenOptionOpen = true;
    [SerializeField] private bool hideOptionPanelOnAwake = true;
    [SerializeField] private float optionCloseDeactivateDelay = 0.12f;

    [Header("Option DOTween")]
    [SerializeField] private DOTweenAnimation[] optionButtonAnimations;
    [SerializeField] private DOTweenAnimation[] optionPanelAnimations;
    [SerializeField] private DOTweenAnimation[] exitButtonAnimations;

    private bool isOptionOpen = false;
    private float previousTimeScale = 1f;
    private Coroutine closeOptionCoroutine;
    private Coroutine pauseAfterOpenCoroutine;

    private void Awake()
    {
        CacheResultReferences();
        CacheOptionReferences();
        CacheOptionAnimations();

        HideResultPanel();
        InitializeOptionState();
    }

    private void CacheResultReferences()
    {
        if (resultRoot == null)
        {
            Transform foundResultRoot = FindChildRecursive(transform, "ResultPanel");
            if (foundResultRoot != null)
                resultRoot = foundResultRoot;
        }

        if (resultRoot == null)
            return;

        if (resultPanelObject == null)
        {
            Image resultImage = FindFirstImageInChildren(resultRoot);
            if (resultImage != null)
                resultPanelObject = resultImage.gameObject;
        }

        if (resultText == null && resultPanelObject != null)
            resultText = resultPanelObject.GetComponentInChildren<Text>(true);
    }

    private void CacheOptionReferences()
    {
        if (optionsRoot == null)
        {
            Transform foundOptions = FindChildRecursive(transform, "Options");
            if (foundOptions != null)
                optionsRoot = foundOptions;
        }

        if (optionsRoot == null)
            return;

        if (optionButton == null)
        {
            Transform buttonTransform = FindChildRecursive(optionsRoot, "OptionButton");
            if (buttonTransform != null)
                optionButton = buttonTransform.GetComponent<Button>();
        }

        if (optionPanel == null)
        {
            Transform panelTransform = FindChildRecursive(optionsRoot, "OptionPanel");
            if (panelTransform == null)
                panelTransform = FindChildRecursive(optionsRoot, "OptionFrame");

            if (panelTransform != null)
                optionPanel = panelTransform.gameObject;
        }

        if (exitButton == null)
        {
            Transform exitTransform = FindChildRecursive(optionsRoot, "ExitButton");
            if (exitTransform != null)
                exitButton = exitTransform.GetComponent<Button>();
        }
    }

    private void CacheOptionAnimations()
    {
        if ((optionButtonAnimations == null || optionButtonAnimations.Length == 0) && optionButton != null)
            optionButtonAnimations = optionButton.GetComponentsInChildren<DOTweenAnimation>(true);

        if ((optionPanelAnimations == null || optionPanelAnimations.Length == 0) && optionPanel != null)
            optionPanelAnimations = optionPanel.GetComponents<DOTweenAnimation>();

        if ((exitButtonAnimations == null || exitButtonAnimations.Length == 0) && exitButton != null)
            exitButtonAnimations = exitButton.GetComponents<DOTweenAnimation>();
    }

    private void InitializeOptionState()
    {
        isOptionOpen = false;

        if (!hideOptionPanelOnAwake)
            return;

        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (exitButton != null)
            exitButton.gameObject.SetActive(false);
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == targetName)
                return child;

            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Image FindFirstImageInChildren(Transform parent)
    {
        if (parent == null)
            return null;

        Image selfImage = parent.GetComponent<Image>();
        if (selfImage != null)
            return selfImage;

        for (int i = 0; i < parent.childCount; i++)
        {
            Image childImage = FindFirstImageInChildren(parent.GetChild(i));
            if (childImage != null)
                return childImage;
        }

        return null;
    }

    public void SetMissionText(string missionDescription)
    {
        if (missionText == null)
            return;

        missionText.text = missionDescription;
    }

    public void SetTimerVisible(bool visible)
    {
        if (timerText == null)
            return;

        timerText.gameObject.SetActive(visible);

        if (!visible)
            timerText.text = "00:00";
    }

    public void SetTimerText(float remainingTime)
    {
        if (timerText == null)
            return;

        if (!timerText.gameObject.activeSelf)
            timerText.gameObject.SetActive(true);

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void ShowResultPanel(bool isSuccess, string message)
    {
        if (resultPanelObject != null)
            resultPanelObject.SetActive(true);

        if (resultText != null)
        {
            resultText.text = isSuccess
                ? $"성공\n{message}"
                : $"실패\n{message}";
        }
    }

    public void HideResultPanel()
    {
        if (resultPanelObject != null)
            resultPanelObject.SetActive(false);
    }

    public void OnClickOptionButton()
    {
        if (isOptionOpen)
            CloseOptionPanel();
        else
            OpenOptionPanel();
    }

    public void ToggleOptionPanel()
    {
        OnClickOptionButton();
    }

    public void OpenOptionPanel()
    {
        if (isOptionOpen)
            return;

        if (closeOptionCoroutine != null)
        {
            StopCoroutine(closeOptionCoroutine);
            closeOptionCoroutine = null;
        }

        if (pauseAfterOpenCoroutine != null)
        {
            StopCoroutine(pauseAfterOpenCoroutine);
            pauseAfterOpenCoroutine = null;
        }

        isOptionOpen = true;

        PlayRestart(optionButtonAnimations);

        if (optionPanel != null)
            optionPanel.SetActive(true);

        if (exitButton != null)
            exitButton.gameObject.SetActive(true);

        PlayRestart(optionPanelAnimations);
        PlayRestart(exitButtonAnimations);

        float openDuration = Mathf.Max(
            GetMaxTweenDuration(optionButtonAnimations),
            GetMaxTweenDuration(optionPanelAnimations),
            GetMaxTweenDuration(exitButtonAnimations));

        if (pauseGameWhenOptionOpen)
            pauseAfterOpenCoroutine = StartCoroutine(PauseAfterOpenAnimation(openDuration));
    }

    public void CloseOptionPanel()
    {
        if (!isOptionOpen)
            return;

        if (pauseAfterOpenCoroutine != null)
        {
            StopCoroutine(pauseAfterOpenCoroutine);
            pauseAfterOpenCoroutine = null;
        }

        isOptionOpen = false;
        RestoreTimeScale();

        PlayBackward(optionButtonAnimations);
        PlayBackward(optionPanelAnimations);
        PlayBackward(exitButtonAnimations);

        float reverseDuration = Mathf.Max(
            GetMaxTweenDuration(optionButtonAnimations),
            GetMaxTweenDuration(optionPanelAnimations),
            GetMaxTweenDuration(exitButtonAnimations));

        float hideDelay = optionCloseDeactivateDelay > 0f
            ? Mathf.Min(reverseDuration, optionCloseDeactivateDelay)
            : reverseDuration;

        if (closeOptionCoroutine != null)
            StopCoroutine(closeOptionCoroutine);

        closeOptionCoroutine = StartCoroutine(CloseOptionAfterDelay(hideDelay));
    }

    public void CloseOptionPanelImmediate()
    {
        if (closeOptionCoroutine != null)
        {
            StopCoroutine(closeOptionCoroutine);
            closeOptionCoroutine = null;
        }

        if (pauseAfterOpenCoroutine != null)
        {
            StopCoroutine(pauseAfterOpenCoroutine);
            pauseAfterOpenCoroutine = null;
        }

        isOptionOpen = false;
        RestoreTimeScale();

        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (exitButton != null)
            exitButton.gameObject.SetActive(false);
    }

    private IEnumerator PauseAfterOpenAnimation(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (isOptionOpen)
            PauseTimeScale();

        pauseAfterOpenCoroutine = null;
    }

    private IEnumerator CloseOptionAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (!isOptionOpen)
        {
            if (optionPanel != null)
                optionPanel.SetActive(false);

            if (exitButton != null)
                exitButton.gameObject.SetActive(false);
        }

        closeOptionCoroutine = null;
    }

    private void PlayRestart(DOTweenAnimation[] tweenAnimations)
    {
        if (tweenAnimations == null)
            return;

        for (int i = 0; i < tweenAnimations.Length; i++)
        {
            DOTweenAnimation tweenAnimation = tweenAnimations[i];
            if (tweenAnimation == null)
                continue;

            tweenAnimation.DORestart();
        }
    }

    private void PlayBackward(DOTweenAnimation[] tweenAnimations)
    {
        if (tweenAnimations == null)
            return;

        for (int i = 0; i < tweenAnimations.Length; i++)
        {
            DOTweenAnimation tweenAnimation = tweenAnimations[i];
            if (tweenAnimation == null)
                continue;

            tweenAnimation.DOPlayBackwards();
        }
    }

    private float GetMaxTweenDuration(DOTweenAnimation[] tweenAnimations)
    {
        float maxDuration = 0f;

        if (tweenAnimations == null)
            return maxDuration;

        for (int i = 0; i < tweenAnimations.Length; i++)
        {
            DOTweenAnimation tweenAnimation = tweenAnimations[i];
            if (tweenAnimation == null)
                continue;

            maxDuration = Mathf.Max(maxDuration, tweenAnimation.duration);
        }

        return maxDuration;
    }

    private void PauseTimeScale()
    {
        if (!pauseGameWhenOptionOpen)
            return;

        if (Time.timeScale > 0f)
            previousTimeScale = Time.timeScale;

        Time.timeScale = 0f;
    }

    private void RestoreTimeScale()
    {
        if (!pauseGameWhenOptionOpen)
            return;

        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    public void OnClickRestartGame()
    {
        CloseOptionPanelImmediate();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnClickExitToLobbyAsFailure()
    {
        CloseOptionPanelImmediate();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.FailStage("작전을 중단하고 로비로 돌아갔습니다.");
            GameManager.Instance.ReturnToLobby();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("Lobby");
    }

    public void OnClickReturnToLobby()
    {
        OnClickExitToLobbyAsFailure();
    }

    public void OnClickQuitGame()
    {
        CloseOptionPanelImmediate();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        Debug.Log("[UIController] 에디터에서는 Application.Quit()가 동작하지 않습니다.");
#else
        Application.Quit();
#endif
    }

    public void OnClickToggleTargetDebugReveal()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.ToggleTargetDebugReveal();
    }
}