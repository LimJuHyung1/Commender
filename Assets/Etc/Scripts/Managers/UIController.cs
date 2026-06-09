using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Michsky.UI.MTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Stage UI")]
    [SerializeField] private Text timerText;

    [Header("Mission UI")]
    [SerializeField] private StyleManager missionStyle;
    [SerializeField] private string missionTextItemID = "";
    [SerializeField] private float missionDisplaySeconds = 5f;
    [SerializeField] private bool useUnscaledTimeForMission = true;

    [Header("Result UI")]
    [SerializeField] private Transform resultRoot;
    [SerializeField] private GameObject resultPanelObject;
    [SerializeField] private Text resultText;

    [Header("Michsky Result UI")]
    [SerializeField] private bool useMichskyResultUI = true;
    [SerializeField] private StyleManager successResultStyle;
    [SerializeField] private StyleManager failureResultStyle;
    [SerializeField] private bool useUnscaledTimeForResult = true;
    [SerializeField] private bool hideResultRootOnHide = false;

    [Header("Result Text Item IDs")]
    [SerializeField] private string resultFirstTextItemID = "First Text";
    [SerializeField] private string resultSecondTextItemID = "Second Text";
    [SerializeField] private string resultThirdTextItemID = "Thid Text";
    [SerializeField] private string resultThirdTextFallbackItemID = "Third Text";

    [Header("Success Result Text")]
    [SerializeField] private string successFirstText = "TARGET";
    [SerializeField] private string successSecondText = "CAPTURED!";
    [SerializeField] private string successThirdText = "다음 스테이지로";

    [Header("Failure Result Text")]
    [SerializeField] private string failureFirstText = "MISSION";
    [SerializeField] private string failureSecondText = "FAILED!";
    [SerializeField] private string failureThirdText = "로비로";

    [Header("Result Detail")]
    [SerializeField] private bool showResultDetailMessageInFirstText = false;

    [Header("Preplaced Agent UI")]
    [SerializeField] private Transform agentUIRoot;
    [SerializeField] private AgentUIPanel[] agentUIPanels;
    [SerializeField] private bool autoFindAgentUIPanels = true;
    [SerializeField] private bool hideUnusedAgentUIPanels = true;
    [SerializeField] private bool sortAgentUIPanelsByHierarchyOrder = true;

    [Header("Commander UI")]
    [SerializeField] private CommanderUIController commanderUIController;

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
    private bool hasBoundAgentUIPanels = false;
    private float previousTimeScale = 1f;

    private Coroutine closeOptionCoroutine;
    private Coroutine pauseAfterOpenCoroutine;
    private Coroutine missionStyleCoroutine;

    private void Awake()
    {
        CacheMissionReferences();
        CacheResultReferences();
        CachePreplacedAgentUIPanels();
        CacheCommanderUIController();
        CacheOptionReferences();
        CacheOptionAnimations();

        InitializeMissionState();
        HideResultPanel();
        InitializePreplacedAgentUIState();
        InitializeOptionState();
    }

    private void CacheMissionReferences()
    {
        if (missionStyle != null)
            return;

        Transform foundMissionStyle = FindChildRecursive(transform, "MTP-3");

        if (foundMissionStyle != null)
            missionStyle = foundMissionStyle.GetComponent<StyleManager>();
    }

    private void InitializeMissionState()
    {
        if (missionStyle == null)
            return;

        missionStyle.playOnEnable = false;
        missionStyle.playOutAnimation = false;
        missionStyle.loopAnimations = false;
        missionStyle.showFor = missionDisplaySeconds;
        missionStyle.UseUnscaledTime = useUnscaledTimeForMission;

        missionStyle.gameObject.SetActive(false);
    }

    private void CacheResultReferences()
    {
        if (resultRoot == null)
        {
            Transform foundResultRoot = FindChildRecursive(transform, "ResultUI");

            if (foundResultRoot == null)
                foundResultRoot = FindChildRecursive(transform, "resultUI");

            if (foundResultRoot == null)
                foundResultRoot = FindChildRecursive(transform, "ResultPanel");

            if (foundResultRoot != null)
                resultRoot = foundResultRoot;
        }

        if (resultRoot == null)
            return;

        if (successResultStyle == null)
        {
            Transform successStyleTransform = FindChildRecursive(resultRoot, "MTP-13");

            if (successStyleTransform != null)
                successResultStyle = successStyleTransform.GetComponent<StyleManager>();
        }

        if (failureResultStyle == null)
        {
            Transform failureStyleTransform = FindChildRecursive(resultRoot, "MTP-20");

            if (failureStyleTransform != null)
                failureResultStyle = failureStyleTransform.GetComponent<StyleManager>();
        }

        if (resultPanelObject == null)
        {
            Transform panelTransform = FindDirectChild(resultRoot, "Panel");

            if (panelTransform == null)
                panelTransform = FindChildRecursive(resultRoot, "Panel");

            if (panelTransform != null)
                resultPanelObject = panelTransform.gameObject;
        }

        if (resultText == null && resultPanelObject != null)
            resultText = resultPanelObject.GetComponentInChildren<Text>(true);
    }

    private void CachePreplacedAgentUIPanels()
    {
        if (agentUIRoot == null)
        {
            Transform foundRoot = FindChildRecursive(transform, "AgentUIRoot");

            if (foundRoot == null)
                foundRoot = FindChildRecursive(transform, "AgentUI");

            if (foundRoot == null)
                foundRoot = FindChildRecursive(transform, "AgentsUI");

            if (foundRoot != null)
                agentUIRoot = foundRoot;
        }

        if (autoFindAgentUIPanels && agentUIRoot != null)
        {
            List<AgentUIPanel> foundPanels = new List<AgentUIPanel>();

            for (int i = 0; i < agentUIRoot.childCount; i++)
            {
                Transform child = agentUIRoot.GetChild(i);

                if (child == null)
                    continue;

                AgentUIPanel panel = child.GetComponent<AgentUIPanel>();

                if (panel != null)
                    foundPanels.Add(panel);
            }

            agentUIPanels = foundPanels.ToArray();
        }

        SortAgentUIPanelsByHierarchyOrder();
    }

    private void SortAgentUIPanelsByHierarchyOrder()
    {
        if (!sortAgentUIPanelsByHierarchyOrder)
            return;

        if (agentUIPanels == null || agentUIPanels.Length <= 1)
            return;

        List<AgentUIPanel> sortedPanels = new List<AgentUIPanel>();

        for (int i = 0; i < agentUIPanels.Length; i++)
        {
            if (agentUIPanels[i] != null)
                sortedPanels.Add(agentUIPanels[i]);
        }

        sortedPanels.Sort((a, b) =>
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return 1;

            if (b == null)
                return -1;

            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        });

        agentUIPanels = sortedPanels.ToArray();
    }

    private void CacheCommanderUIController()
    {
        if (commanderUIController != null)
            return;

        commanderUIController = FindFirstObjectByType<CommanderUIController>();
    }

    private void InitializePreplacedAgentUIState()
    {
        hasBoundAgentUIPanels = false;

        if (agentUIPanels == null)
            return;

        for (int i = 0; i < agentUIPanels.Length; i++)
        {
            AgentUIPanel panel = agentUIPanels[i];

            if (panel == null)
                continue;

            ClearAgentUIPanel(panel);

            if (hideUnusedAgentUIPanels)
                panel.gameObject.SetActive(false);
        }

        if (hideUnusedAgentUIPanels && agentUIRoot != null)
            agentUIRoot.gameObject.SetActive(false);
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

    private Transform FindDirectChild(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == targetName)
                return child;
        }

        return null;
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

    public void BindPreplacedAgentUIPanels(IReadOnlyList<GameObject> agents)
    {
        CachePreplacedAgentUIPanels();
        CacheCommanderUIController();

        if (agentUIPanels == null || agentUIPanels.Length <= 0)
        {
            Debug.LogWarning("[UIController] 배치된 AgentUIPanel을 찾지 못했습니다.");
            return;
        }

        int agentCount = agents != null ? agents.Count : 0;

        List<InputField> boundInputs = new List<InputField>();
        List<AgentController> boundAgents = new List<AgentController>();

        if (agentUIRoot != null)
            agentUIRoot.gameObject.SetActive(agentCount > 0);

        for (int i = 0; i < agentUIPanels.Length; i++)
        {
            AgentUIPanel panel = agentUIPanels[i];

            if (panel == null)
                continue;

            if (i < agentCount && agents[i] != null)
            {
                GameObject agentObject = agents[i];
                AgentController agentController = FindAgentController(agentObject);

                panel.gameObject.SetActive(true);
                panel.Bind(i, agentObject);

                InputField inputField = GetInputFieldInPanel(panel);

                if (inputField != null && agentController != null)
                {
                    boundInputs.Add(inputField);
                    boundAgents.Add(agentController);
                }

                Debug.Log(
                    $"[UIController] Agent UI Bind: Index={i}, " +
                    $"Panel={panel.name}, InputField={(inputField != null ? inputField.name : "None")}, " +
                    $"Agent={agentObject.name}, AgentID={(agentController != null ? agentController.AgentID.ToString() : "None")}"
                );
            }
            else
            {
                ClearAgentUIPanel(panel);

                if (hideUnusedAgentUIPanels)
                    panel.gameObject.SetActive(false);
            }
        }

        hasBoundAgentUIPanels = boundInputs.Count > 0;

        if (commanderUIController != null)
            commanderUIController.BindInputFieldsAndAgents(boundInputs, boundAgents);
    }

    public void ClearPreplacedAgentUIPanels()
    {
        CachePreplacedAgentUIPanels();
        CacheCommanderUIController();

        hasBoundAgentUIPanels = false;

        if (agentUIPanels != null)
        {
            for (int i = 0; i < agentUIPanels.Length; i++)
            {
                AgentUIPanel panel = agentUIPanels[i];

                if (panel == null)
                    continue;

                ClearAgentUIPanel(panel);

                if (hideUnusedAgentUIPanels)
                    panel.gameObject.SetActive(false);
            }
        }

        if (commanderUIController != null)
            commanderUIController.BindInputFieldsAndAgents(new List<InputField>(), new List<AgentController>());

        if (hideUnusedAgentUIPanels && agentUIRoot != null)
            agentUIRoot.gameObject.SetActive(false);
    }

    private void ClearAgentUIPanel(AgentUIPanel panel)
    {
        if (panel == null)
            return;

        panel.Clear();

        InputField inputField = GetInputFieldInPanel(panel);

        if (inputField != null)
        {
            inputField.text = "";
            inputField.DeactivateInputField();
        }
    }

    private AgentController FindAgentController(GameObject agentObject)
    {
        if (agentObject == null)
            return null;

        AgentController agentController = agentObject.GetComponent<AgentController>();

        if (agentController == null)
            agentController = agentObject.GetComponentInChildren<AgentController>(true);

        return agentController;
    }

    private InputField GetInputFieldInPanel(AgentUIPanel panel)
    {
        if (panel == null)
            return null;

        if (panel.InputField != null)
            return panel.InputField;

        return panel.GetComponentInChildren<InputField>(true);
    }

    public void SetAgentUIVisible(bool visible)
    {
        if (agentUIRoot == null)
            return;

        agentUIRoot.gameObject.SetActive(visible && hasBoundAgentUIPanels);
    }

    public void SetMissionText(string missionDescription)
    {
        string message = string.IsNullOrWhiteSpace(missionDescription)
            ? string.Empty
            : missionDescription;

        if (string.IsNullOrEmpty(message))
        {
            StopMissionStyleImmediate();
            return;
        }

        PlayMissionStyle(message);
    }

    private void PlayMissionStyle(string message)
    {
        if (missionStyle == null)
            return;

        if (missionStyleCoroutine != null)
        {
            StopCoroutine(missionStyleCoroutine);
            missionStyleCoroutine = null;
        }

        missionStyle.playOnEnable = false;
        missionStyle.playOutAnimation = false;
        missionStyle.loopAnimations = false;
        missionStyle.showFor = missionDisplaySeconds;
        missionStyle.UseUnscaledTime = useUnscaledTimeForMission;

        if (!missionStyle.gameObject.activeSelf)
            missionStyle.gameObject.SetActive(true);

        ApplyTextToStyle(missionStyle, message, missionTextItemID, true);
        PrepareStyleAnimator(missionStyle, useUnscaledTimeForMission);

        missionStyleCoroutine = StartCoroutine(PlayMissionStyleCoroutine(missionStyle));
    }

    private IEnumerator PlayMissionStyleCoroutine(StyleManager styleManager)
    {
        if (styleManager == null)
        {
            missionStyleCoroutine = null;
            yield break;
        }

        styleManager.PlayIn();
        ForceAnimatorUpdate(styleManager);

        float inDuration = GetStyleAnimationDuration(styleManager, styleManager.inAnim);

        if (inDuration > 0f)
            yield return WaitForStyleSeconds(inDuration, useUnscaledTimeForMission);

        if (missionDisplaySeconds > 0f)
            yield return WaitForStyleSeconds(missionDisplaySeconds, useUnscaledTimeForMission);

        if (styleManager != null && styleManager.gameObject.activeInHierarchy)
        {
            styleManager.PlayOut();
            ForceAnimatorUpdate(styleManager);

            float outDuration = GetStyleAnimationDuration(styleManager, styleManager.outAnim);

            if (outDuration > 0f)
                yield return WaitForStyleSeconds(outDuration, useUnscaledTimeForMission);

            if (styleManager != null && styleManager.disableOnOut)
                styleManager.gameObject.SetActive(false);
        }

        missionStyleCoroutine = null;
    }

    private void StopMissionStyleImmediate()
    {
        if (missionStyleCoroutine != null)
        {
            StopCoroutine(missionStyleCoroutine);
            missionStyleCoroutine = null;
        }

        if (missionStyle != null)
            missionStyle.Stop();
    }

    private IEnumerator WaitForStyleSeconds(float seconds, bool useUnscaledTime)
    {
        if (seconds <= 0f)
            yield break;

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return new WaitForSeconds(seconds);
    }

    private float GetStyleAnimationDuration(StyleManager styleManager, AnimationClip clip)
    {
        if (clip == null)
            return 0f;

        float speed = 1f;

        if (styleManager != null)
            speed = Mathf.Max(0.01f, Mathf.Abs(styleManager.AnimationSpeed));

        return clip.length / speed;
    }

    private void ForceAnimatorUpdate(StyleManager styleManager)
    {
        if (styleManager == null)
            return;

        if (styleManager.styleAnimator == null)
            styleManager.styleAnimator = styleManager.GetComponent<Animator>();

        if (styleManager.styleAnimator != null)
            styleManager.styleAnimator.Update(0f);
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
        if (resultRoot != null)
            resultRoot.gameObject.SetActive(true);

        if (resultPanelObject != null)
            resultPanelObject.SetActive(true);

        if (useMichskyResultUI)
        {
            PlayResultStyle(isSuccess, message);
            return;
        }

        if (resultText != null)
        {
            string resultMessage = isSuccess
                ? $"성공\n{message}"
                : $"실패\n{message}";

            resultText.text = resultMessage;
        }
    }

    private void PlayResultStyle(bool isSuccess, string message)
    {
        StyleManager targetStyle = isSuccess ? successResultStyle : failureResultStyle;
        StyleManager otherStyle = isSuccess ? failureResultStyle : successResultStyle;

        if (otherStyle != null)
            otherStyle.gameObject.SetActive(false);

        if (targetStyle == null)
        {
            if (resultText != null)
            {
                string fallbackMessage = isSuccess
                    ? $"성공\n{message}"
                    : $"실패\n{message}";

                resultText.text = fallbackMessage;
            }

            return;
        }

        if (!targetStyle.gameObject.activeSelf)
            targetStyle.gameObject.SetActive(true);

        ResultTextSet resultTextSet = CreateResultTextSet(isSuccess, message);

        ApplyResultTextsToStyle(
            targetStyle,
            resultTextSet.firstText,
            resultTextSet.secondText,
            resultTextSet.thirdText
        );

        PrepareStyleAnimator(targetStyle, useUnscaledTimeForResult);
        targetStyle.Play();
    }

    private ResultTextSet CreateResultTextSet(bool isSuccess, string message)
    {
        string firstText = isSuccess ? successFirstText : failureFirstText;
        string secondText = isSuccess ? successSecondText : failureSecondText;
        string thirdText = isSuccess ? successThirdText : failureThirdText;

        if (showResultDetailMessageInFirstText && !string.IsNullOrWhiteSpace(message))
            firstText = $"{firstText}\n{message}";

        return new ResultTextSet(firstText, secondText, thirdText);
    }

    private void ApplyResultTextsToStyle(
        StyleManager styleManager,
        string firstText,
        string secondText,
        string thirdText)
    {
        if (styleManager == null || styleManager.textItems == null)
            return;

        SetTextItemByIDOrIndex(styleManager, resultFirstTextItemID, 0, firstText);
        SetTextItemByIDOrIndex(styleManager, resultSecondTextItemID, 1, secondText);

        bool thirdApplied = SetTextItemByIDOrIndex(styleManager, resultThirdTextItemID, 2, thirdText);

        if (!thirdApplied)
            SetTextItemByIDOrIndex(styleManager, resultThirdTextFallbackItemID, 2, thirdText);
    }

    private bool SetTextItemByIDOrIndex(
        StyleManager styleManager,
        string textItemID,
        int fallbackIndex,
        string message)
    {
        if (styleManager == null || styleManager.textItems == null)
            return false;

        TextItem targetTextItem = FindTextItemByID(styleManager, textItemID);

        if (targetTextItem == null)
        {
            if (fallbackIndex >= 0 && fallbackIndex < styleManager.textItems.Count)
                targetTextItem = styleManager.textItems[fallbackIndex];
        }

        if (targetTextItem == null)
            return false;

        UpdateTextItem(targetTextItem, message);
        return true;
    }

    private TextItem FindTextItemByID(StyleManager styleManager, string textItemID)
    {
        if (styleManager == null || styleManager.textItems == null)
            return null;

        if (string.IsNullOrEmpty(textItemID))
            return null;

        for (int i = 0; i < styleManager.textItems.Count; i++)
        {
            TextItem textItem = styleManager.textItems[i];

            if (textItem == null)
                continue;

            if (textItem.itemID == textItemID)
                return textItem;
        }

        return null;
    }

    private void ApplyTextToStyle(
        StyleManager styleManager,
        string message,
        string textItemID,
        bool applyToAllMatched)
    {
        if (styleManager == null || styleManager.textItems == null)
            return;

        bool hasTextItemID = !string.IsNullOrEmpty(textItemID);
        bool applied = false;
        TextItem firstValidTextItem = null;

        for (int i = 0; i < styleManager.textItems.Count; i++)
        {
            TextItem textItem = styleManager.textItems[i];

            if (textItem == null)
                continue;

            if (firstValidTextItem == null)
                firstValidTextItem = textItem;

            if (hasTextItemID && textItem.itemID != textItemID)
                continue;

            UpdateTextItem(textItem, message);
            applied = true;

            if (!applyToAllMatched)
                return;
        }

        if (!applied && firstValidTextItem != null)
            UpdateTextItem(firstValidTextItem, message);
    }

    private void UpdateTextItem(TextItem textItem, string message)
    {
        if (textItem == null)
            return;

        textItem.text = message;

        if (textItem.textObject == null)
            textItem.textObject = textItem.GetComponent<TMPro.TextMeshProUGUI>();

        if (textItem.textObject == null)
            return;

        textItem.UpdateText();
    }

    private void PrepareStyleAnimator(StyleManager styleManager, bool useUnscaledTime)
    {
        if (styleManager == null)
            return;

        styleManager.playOnEnable = false;
        styleManager.UseUnscaledTime = useUnscaledTime;
        styleManager.InitializeSpeed(styleManager.AnimationSpeed);

        if (styleManager.styleAnimator == null)
            styleManager.styleAnimator = styleManager.GetComponent<Animator>();

        Animator animator = styleManager.styleAnimator;

        if (animator == null)
            return;

        animator.updateMode = useUnscaledTime
            ? AnimatorUpdateMode.UnscaledTime
            : AnimatorUpdateMode.Normal;

        animator.Rebind();
        animator.Update(0f);
    }

    public void HideResultPanel()
    {
        if (successResultStyle != null)
            successResultStyle.gameObject.SetActive(false);

        if (failureResultStyle != null)
            failureResultStyle.gameObject.SetActive(false);

        if (resultPanelObject != null)
            resultPanelObject.SetActive(false);

        if (hideResultRootOnHide && resultRoot != null)
            resultRoot.gameObject.SetActive(false);
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

    public void SetStageHudVisible(bool visible)
    {
        if (!visible)
            StopMissionStyleImmediate();

        if (timerText != null)
            timerText.gameObject.SetActive(visible);

        SetAgentUIVisible(visible);
    }

    public void SetOptionButtonVisible(bool visible)
    {
        if (optionButton == null)
            return;

        optionButton.gameObject.SetActive(visible);
    }

    private readonly struct ResultTextSet
    {
        public readonly string firstText;
        public readonly string secondText;
        public readonly string thirdText;

        public ResultTextSet(string firstText, string secondText, string thirdText)
        {
            this.firstText = firstText;
            this.secondText = secondText;
            this.thirdText = thirdText;
        }
    }
}