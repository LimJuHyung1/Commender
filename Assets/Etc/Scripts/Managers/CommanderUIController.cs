using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class CommanderUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private List<InputField> agentInputs = new List<InputField>();
    [SerializeField] private Button submitButton;
    [SerializeField] private AgentCameraFollow agentCameraFollow;

    [Header("Hotkeys")]
    [SerializeField] private bool enableFunctionKeyFocus = true;

    [Header("Skill Name Paste")]
    [SerializeField] private bool addSpaceBeforePastedSkillName = true;
    [SerializeField] private bool clearFocusAfterPastedSkillName = true;

    [Header("Communication Jam")]
    [SerializeField] private string jammedPlaceholderText = "통신 방해 중... 명령 버튼을 눌러 해제";
    [SerializeField] private bool clearTextOnJam = true;
    [SerializeField] private bool releaseJamOnCommandButton = true;
    [SerializeField] private bool releaseJamOnlyWhenSubmittedInstructionExists = true;

    private IReadOnlyList<AgentController> agents;

    private readonly Dictionary<InputField, AgentController> agentByInput =
        new Dictionary<InputField, AgentController>();

    private readonly Dictionary<int, InputField> inputByAgentId =
        new Dictionary<int, InputField>();

    private int currentFocusedInputIndex = -1;
    private AgentOutline currentHighlightedOutline;
    private bool uiInteractable = true;
    private bool pendingRefocusAfterOrbit;

    private Coroutine tabMoveRoutine;

    private readonly Dictionary<InputField, string> originalPlaceholderTextByInput =
        new Dictionary<InputField, string>();

    private readonly HashSet<int> jammedAgentIds = new HashSet<int>();

    private readonly Dictionary<int, Coroutine> jamReleaseRoutineByAgentId =
        new Dictionary<int, Coroutine>();

    private void Awake()
    {
        CacheOriginalPlaceholderTexts();

        if (submitButton != null)
            submitButton.onClick.AddListener(HandleCommandSubmitCommunicationJam);
    }

    private void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(HandleCommandSubmitCommunicationJam);
    }

    private void Update()
    {
        if (!uiInteractable)
            return;

        HandleFunctionKeyFocus();
        HandleTabInputNavigation();
        HandleSubmitHotkey();
        HandlePointerFocusRelease();
        HandleOrbitFocusRetention();
        UpdateFocusedInputPresentation();
    }

    public void Initialize(IReadOnlyList<AgentController> boundAgents)
    {
        CacheOriginalPlaceholderTexts();
        BindAgents(boundAgents);
    }

    public void BindAgents(IReadOnlyList<AgentController> boundAgents)
    {
        agents = boundAgents;
        RebuildInputAgentBindingCacheFromCurrentLists();
        WarnIfInputCountMismatch();
        RefreshAllInputInteractableStates();
        UpdateFocusedInputPresentation(true);
    }

    public void FocusAgentByInputField(InputField inputField)
    {
        if (!uiInteractable)
            return;

        if (inputField == null)
            return;

        int inputIndex = GetInputIndex(inputField);

        if (inputIndex < 0)
            return;

        if (!CanFocusInputIndex(inputIndex))
            return;

        if (!TryGetAgentFromInputField(inputField, inputIndex, out AgentController targetAgent))
            return;

        pendingRefocusAfterOrbit = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);

        ApplyFocusedAgentImmediately(inputIndex, inputField, targetAgent, false);
    }

    public void FocusAgentByInputIndex(int inputIndex)
    {
        if (!uiInteractable)
            return;

        if (!CanFocusInputIndex(inputIndex))
            return;

        InputField inputField = agentInputs[inputIndex];

        if (inputField == null)
            return;

        if (!TryGetAgentFromInputField(inputField, inputIndex, out AgentController targetAgent))
            return;

        pendingRefocusAfterOrbit = false;
        ApplyFocusedAgentImmediately(inputIndex, inputField, targetAgent, true);
    }

    public void SetUIInteractable(bool state)
    {
        uiInteractable = state;

        if (!state)
        {
            if (tabMoveRoutine != null)
            {
                StopCoroutine(tabMoveRoutine);
                tabMoveRoutine = null;
            }

            ClearCurrentInputFocus();
        }

        RefreshAllInputInteractableStates();

        if (submitButton != null)
            submitButton.interactable = state;
    }

    public void ClearAllInputs()
    {
        if (agentInputs == null)
            return;

        foreach (InputField input in agentInputs)
        {
            if (input != null)
                input.text = "";
        }
    }

    public void ClearInputsByAgentIds(IEnumerable<int> agentIds)
    {
        if (agentIds == null || agentInputs == null)
            return;

        foreach (int agentId in agentIds)
        {
            if (!TryGetInputIndexByAgentId(agentId, out int inputIndex))
                continue;

            if (inputIndex < 0 || inputIndex >= agentInputs.Count)
                continue;

            InputField input = agentInputs[inputIndex];

            if (input != null)
                input.text = "";
        }
    }

    public bool TryBuildSubmittedInstructions(out Dictionary<int, string> submittedInstructionById)
    {
        submittedInstructionById = new Dictionary<int, string>();

        if (agentInputs == null || agentInputs.Count == 0)
            return false;

        bool hasAnyValidInput = false;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];

            if (input == null)
                continue;

            if (IsInputIndexJammed(i))
                continue;

            string instruction = input.text.Trim();

            if (string.IsNullOrEmpty(instruction))
                continue;

            if (!TryGetAgentAtInputIndex(i, out AgentController agent))
            {
                Debug.LogWarning($"[CommanderUI] Input index {i}에 연결된 Agent가 없습니다.");
                continue;
            }

            submittedInstructionById[agent.AgentID] = instruction;
            hasAnyValidInput = true;
        }

        return hasAnyValidInput;
    }

    public bool TryPasteSkillDisplayNameToInputIndex(
        int inputIndex,
        int clickedAgentId,
        string skillDisplayName,
        bool requireInputAgentMatched)
    {
        if (!uiInteractable)
            return false;

        if (string.IsNullOrWhiteSpace(skillDisplayName))
            return false;

        if (!CanFocusInputIndex(inputIndex))
            return false;

        if (requireInputAgentMatched)
        {
            if (!TryGetAgentAtInputIndex(inputIndex, out AgentController agent))
                return false;

            if (agent.AgentID != clickedAgentId)
                return false;
        }

        InputField input = agentInputs[inputIndex];

        if (input == null)
            return false;

        PasteSkillNameToInput(input, skillDisplayName.Trim());

        if (clearFocusAfterPastedSkillName)
            ClearCurrentInputFocus();
        else
            FocusInputField(inputIndex);

        return true;
    }

    private void PasteSkillNameToInput(InputField input, string skillDisplayName)
    {
        if (input == null)
            return;

        if (string.IsNullOrWhiteSpace(skillDisplayName))
            return;

        string currentText = input.text ?? "";
        string pasteText = skillDisplayName.Trim();

        if (addSpaceBeforePastedSkillName &&
            currentText.Length > 0 &&
            !char.IsWhiteSpace(currentText[currentText.Length - 1]))
        {
            currentText += " ";
        }

        input.text = currentText + pasteText;

        int caretPosition = input.text.Length;
        input.caretPosition = caretPosition;
        input.selectionAnchorPosition = caretPosition;
        input.selectionFocusPosition = caretPosition;
        input.ForceLabelUpdate();
    }

    public bool TryJamAgentInput(int agentId, float duration)
    {
        if (duration <= 0f)
            return false;

        if (!TryGetInputIndexByAgentId(agentId, out int inputIndex))
            return false;

        SetAgentInputJammedState(agentId, true);

        InputField input = agentInputs[inputIndex];

        if (input != null)
        {
            if (clearTextOnJam)
                input.text = "";

            input.DeactivateInputField();
        }

        if (jamReleaseRoutineByAgentId.TryGetValue(agentId, out Coroutine runningRoutine) &&
            runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }

        jamReleaseRoutineByAgentId[agentId] = StartCoroutine(ReleaseJamAfterDelay(agentId, duration));

        if (GetSelectedInputIndex() == inputIndex || currentFocusedInputIndex == inputIndex)
            FocusNextAvailableInputFrom(inputIndex);

        UpdateFocusedInputPresentation(true);
        return true;
    }

    public bool TryJamAgentInputUntilCommandButton(int agentId)
    {
        if (!TryGetInputIndexByAgentId(agentId, out int inputIndex))
            return false;

        if (jammedAgentIds.Contains(agentId))
            return false;

        if (jamReleaseRoutineByAgentId.TryGetValue(agentId, out Coroutine runningRoutine) &&
            runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }

        jamReleaseRoutineByAgentId.Remove(agentId);

        SetAgentInputJammedState(agentId, true);

        InputField input = agentInputs[inputIndex];

        if (input != null)
        {
            if (clearTextOnJam)
                input.text = "";

            input.DeactivateInputField();
        }

        if (GetSelectedInputIndex() == inputIndex || currentFocusedInputIndex == inputIndex)
            FocusNextAvailableInputFrom(inputIndex);

        UpdateFocusedInputPresentation(true);

        Debug.Log($"[CommanderUI] AgentID {agentId} 통신 방해 적용. 명령 버튼 클릭 시 해제됩니다.");
        return true;
    }

    public bool TryJamRandomAvailableAgentInput(float duration, out int jammedAgentId)
    {
        jammedAgentId = -1;

        if (duration <= 0f)
            return false;

        if (agents == null || agents.Count == 0)
            return false;

        List<int> candidateAgentIds = new List<int>();

        for (int i = 0; i < agents.Count; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (jammedAgentIds.Contains(agent.AgentID))
                continue;

            candidateAgentIds.Add(agent.AgentID);
        }

        if (candidateAgentIds.Count == 0)
            return false;

        int randomIndex = Random.Range(0, candidateAgentIds.Count);
        jammedAgentId = candidateAgentIds[randomIndex];

        return TryJamAgentInput(jammedAgentId, duration);
    }

    public bool TryJamRandomAvailableAgentInputUntilCommandButton(out int jammedAgentId)
    {
        jammedAgentId = -1;

        if (agents == null || agents.Count == 0)
        {
            Debug.LogWarning("[CommanderUI] 에이전트 목록이 비어 있어서 통신 방해를 적용할 수 없습니다.");
            return false;
        }

        List<int> candidateAgentIds = new List<int>();

        for (int i = 0; i < agents.Count; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (jammedAgentIds.Contains(agent.AgentID))
                continue;

            candidateAgentIds.Add(agent.AgentID);
        }

        if (candidateAgentIds.Count == 0)
        {
            Debug.LogWarning("[CommanderUI] 방해 가능한 에이전트가 없습니다.");
            return false;
        }

        int randomIndex = Random.Range(0, candidateAgentIds.Count);
        jammedAgentId = candidateAgentIds[randomIndex];

        return TryJamAgentInputUntilCommandButton(jammedAgentId);
    }

    public void ClearAgentInputJam(int agentId)
    {
        if (jamReleaseRoutineByAgentId.TryGetValue(agentId, out Coroutine runningRoutine) &&
            runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
        }

        jamReleaseRoutineByAgentId.Remove(agentId);
        SetAgentInputJammedState(agentId, false);
        UpdateFocusedInputPresentation(true);
    }

    public void ClearAllAgentInputJams()
    {
        foreach (KeyValuePair<int, Coroutine> pair in jamReleaseRoutineByAgentId)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }

        jamReleaseRoutineByAgentId.Clear();
        jammedAgentIds.Clear();

        RefreshAllInputInteractableStates();
        RestoreAllPlaceholderTexts();
        UpdateFocusedInputPresentation(true);
    }

    public bool TryReleaseJammedInputsByCommandButton()
    {
        if (jammedAgentIds.Count == 0)
            return false;

        List<int> releasedAgentIds = new List<int>(jammedAgentIds);

        for (int i = 0; i < releasedAgentIds.Count; i++)
        {
            ClearAgentInputJam(releasedAgentIds[i]);
        }

        Debug.Log($"[CommanderUI] 명령 버튼 클릭으로 통신 방해 해제. 해제 수: {releasedAgentIds.Count}");
        return true;
    }

    public bool IsAgentInputJammed(int agentId)
    {
        return jammedAgentIds.Contains(agentId);
    }

    private IEnumerator ReleaseJamAfterDelay(int agentId, float duration)
    {
        yield return new WaitForSeconds(duration);

        jamReleaseRoutineByAgentId.Remove(agentId);
        SetAgentInputJammedState(agentId, false);
        UpdateFocusedInputPresentation(true);
    }

    private void SetAgentInputJammedState(int agentId, bool jammed)
    {
        if (jammed)
            jammedAgentIds.Add(agentId);
        else
            jammedAgentIds.Remove(agentId);

        RefreshAllInputInteractableStates();
        RestoreAllPlaceholderTexts();
        ApplyAllJammedPlaceholders();
    }

    private void RefreshAllInputInteractableStates()
    {
        if (agentInputs == null)
            return;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];

            if (input == null)
                continue;

            bool canInteract = uiInteractable && !IsInputIndexJammed(i);
            input.interactable = canInteract;
        }
    }

    private void HandleFunctionKeyFocus()
    {
        if (!enableFunctionKeyFocus)
            return;

        if (agentInputs == null || agentInputs.Count == 0)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            FocusInputFieldByFunctionKey(0);
            return;
        }

        if (keyboard.f2Key.wasPressedThisFrame)
        {
            FocusInputFieldByFunctionKey(1);
            return;
        }

        if (keyboard.f3Key.wasPressedThisFrame)
        {
            FocusInputFieldByFunctionKey(2);
            return;
        }

        if (keyboard.f4Key.wasPressedThisFrame)
        {
            FocusInputFieldByFunctionKey(3);
            return;
        }
    }

    private void FocusInputFieldByFunctionKey(int inputIndex)
    {
        if (agentInputs == null)
            return;

        if (inputIndex < 0 || inputIndex >= agentInputs.Count)
            return;

        if (IsInputIndexJammed(inputIndex))
            return;

        if (IsInputIndexCurrentlyFocused(inputIndex))
        {
            ClearCurrentInputFocus();
            return;
        }

        FocusInputField(inputIndex);
    }

    private bool IsInputIndexCurrentlyFocused(int inputIndex)
    {
        if (inputIndex < 0)
            return false;

        int focusedIndex = GetEffectiveFocusedInputIndex();

        if (focusedIndex >= 0)
            return focusedIndex == inputIndex;

        return currentFocusedInputIndex == inputIndex;
    }

    private void ClearCurrentInputFocus()
    {
        if (agentInputs != null)
        {
            foreach (InputField input in agentInputs)
            {
                if (input == null)
                    continue;

                input.DeactivateInputField();
            }
        }

        pendingRefocusAfterOrbit = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (agentCameraFollow != null)
            agentCameraFollow.ClearFocusAgent();

        ClearCurrentAgentVisualFocus();

        currentFocusedInputIndex = -1;

        RestoreAllPlaceholderTexts();
        ApplyAllJammedPlaceholders();
    }

    private void HandleTabInputNavigation()
    {
        if (agentInputs == null || agentInputs.Count == 0)
            return;

        if (EventSystem.current == null)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (!keyboard.tabKey.wasPressedThisFrame)
            return;

        int currentIndex = GetEffectiveFocusedInputIndex();

        if (currentIndex < 0)
            return;

        bool movePrevious =
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed;

        int nextIndex = FindNextAvailableInputIndex(currentIndex, movePrevious);

        if (nextIndex < 0 || nextIndex == currentIndex)
            return;

        if (tabMoveRoutine != null)
            StopCoroutine(tabMoveRoutine);

        tabMoveRoutine = StartCoroutine(MoveFocusAfterImeCommit(currentIndex, nextIndex));
    }

    private void HandleSubmitHotkey()
    {
        if (submitButton == null || !submitButton.interactable)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        bool ctrlPressed =
            keyboard.leftCtrlKey.isPressed ||
            keyboard.rightCtrlKey.isPressed;

        if (!ctrlPressed)
            return;

        bool enterPressedThisFrame =
            keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame;

        if (!enterPressedThisFrame)
            return;

        if (IsImeComposing())
            return;

        int selectedIndex = GetSelectedInputIndex();
        int focusedIndex = GetEffectiveFocusedInputIndex();

        if (selectedIndex < 0 && focusedIndex < 0 && jammedAgentIds.Count == 0)
            return;

        ClearInputFocusBeforeSubmit();
        submitButton.onClick.Invoke();
    }

    private void HandleCommandSubmitCommunicationJam()
    {
        if (!uiInteractable)
            return;

        if (!releaseJamOnCommandButton)
            return;

        if (releaseJamOnlyWhenSubmittedInstructionExists)
        {
            if (!TryBuildSubmittedInstructions(out Dictionary<int, string> submittedInstructionById))
            {
                Debug.Log("[CommanderUI] 제출 가능한 명령이 없어 통신 방해를 해제하지 않습니다.");
                return;
            }
        }

        TryReleaseJammedInputsByCommandButton();
    }

    private void ClearInputFocusBeforeSubmit()
    {
        ClearCurrentInputFocus();
    }

    private IEnumerator MoveFocusAfterImeCommit(int currentIndex, int nextIndex)
    {
        while (IsImeComposing())
            yield return null;

        InputField currentInput = agentInputs[currentIndex];

        if (currentInput != null)
            currentInput.DeactivateInputField();

        yield return null;

        FocusInputField(nextIndex);
        tabMoveRoutine = null;
    }

    private bool IsImeComposing()
    {
        return !string.IsNullOrEmpty(UnityEngine.Input.compositionString);
    }

    private void HandlePointerFocusRelease()
    {
        if (currentFocusedInputIndex < 0)
            return;

        if (ShouldRetainFocusDuringOrbitInput())
            return;

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (!mouse.leftButton.wasPressedThisFrame)
            return;

        Vector2 screenPosition = mouse.position.ReadValue();

        if (IsPointerOverManagedFocusKeepArea(screenPosition))
            return;

        ClearCurrentInputFocus();
    }

    private bool IsPointerOverManagedFocusKeepArea(Vector2 screenPosition)
    {
        if (IsPointerOverManagedInput(screenPosition))
            return true;

        if (IsPointerOverManagedAgentPanel(screenPosition))
            return true;

        if (IsPointerOverSubmitButton(screenPosition))
            return true;

        return false;
    }

    private bool IsPointerOverManagedInput(Vector2 screenPosition)
    {
        if (agentInputs == null)
            return false;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];

            if (input == null)
                continue;

            RectTransform rectTransform = input.transform as RectTransform;

            if (IsPointerInsideRect(rectTransform, screenPosition))
                return true;
        }

        return false;
    }

    private bool IsPointerOverManagedAgentPanel(Vector2 screenPosition)
    {
        if (agentInputs == null)
            return false;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];

            if (input == null)
                continue;

            AgentUIPanel panel = input.GetComponentInParent<AgentUIPanel>(true);

            if (panel == null)
                continue;

            RectTransform rectTransform = panel.transform as RectTransform;

            if (IsPointerInsideRect(rectTransform, screenPosition))
                return true;
        }

        return false;
    }

    private bool IsPointerOverSubmitButton(Vector2 screenPosition)
    {
        if (submitButton == null)
            return false;

        RectTransform rectTransform = submitButton.transform as RectTransform;
        return IsPointerInsideRect(rectTransform, screenPosition);
    }

    private bool IsPointerInsideRect(RectTransform rectTransform, Vector2 screenPosition)
    {
        if (rectTransform == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            screenPosition,
            GetCanvasCamera(rectTransform)
        );
    }

    private Camera GetCanvasCamera(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return null;

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();

        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private int GetSelectedInputIndex()
    {
        if (agentInputs == null || EventSystem.current == null)
            return -1;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        if (selectedObject == null)
            return -1;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];

            if (input == null)
                continue;

            if (selectedObject == input.gameObject)
                return i;

            if (selectedObject.transform != null && selectedObject.transform.IsChildOf(input.transform))
                return i;
        }

        return -1;
    }

    private int GetFocusedInputIndex()
    {
        if (agentInputs == null)
            return -1;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];

            if (input == null || !input.interactable)
                continue;

            if (input.isFocused)
                return i;
        }

        return -1;
    }

    private int GetEffectiveFocusedInputIndex()
    {
        int focusedIndex = GetFocusedInputIndex();

        if (focusedIndex >= 0)
            return focusedIndex;

        int selectedIndex = GetSelectedInputIndex();

        if (selectedIndex >= 0)
            return selectedIndex;

        if (ShouldRetainFocusDuringOrbitInput() && CanFocusInputIndex(currentFocusedInputIndex))
            return currentFocusedInputIndex;

        if (currentFocusedInputIndex >= 0 && CanFocusInputIndex(currentFocusedInputIndex))
            return currentFocusedInputIndex;

        return -1;
    }

    private void HandleOrbitFocusRetention()
    {
        if (ShouldRetainFocusDuringOrbitInput())
        {
            if (GetFocusedInputIndex() < 0 && CanFocusInputIndex(currentFocusedInputIndex))
                pendingRefocusAfterOrbit = true;

            return;
        }

        if (!pendingRefocusAfterOrbit)
            return;

        TryRestoreFocusAfterOrbit();
    }

    private bool ShouldRetainFocusDuringOrbitInput()
    {
        if (!uiInteractable)
            return false;

        if (currentFocusedInputIndex < 0)
            return false;

        if (!CanFocusInputIndex(currentFocusedInputIndex))
            return false;

        if (agentCameraFollow == null || !agentCameraFollow.HasFocusedAgent)
            return false;

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.rightButton.isPressed)
            return true;

        return agentCameraFollow.IsFocusedOrbitInputActive;
    }

    private void TryRestoreFocusAfterOrbit()
    {
        if (!uiInteractable)
        {
            pendingRefocusAfterOrbit = false;
            return;
        }

        if (ShouldRetainFocusDuringOrbitInput())
            return;

        if (GetFocusedInputIndex() >= 0)
        {
            pendingRefocusAfterOrbit = false;
            return;
        }

        int selectedIndex = GetSelectedInputIndex();

        if (selectedIndex >= 0)
        {
            pendingRefocusAfterOrbit = false;
            return;
        }

        if (!CanFocusInputIndex(currentFocusedInputIndex))
        {
            pendingRefocusAfterOrbit = false;
            return;
        }

        FocusInputField(currentFocusedInputIndex);
        pendingRefocusAfterOrbit = false;
    }

    private void UpdateFocusedInputPresentation(bool force = false)
    {
        int focusedIndex = GetEffectiveFocusedInputIndex();

        if (focusedIndex < 0)
        {
            if (!force)
                return;

            RestoreAllPlaceholderTexts();
            ApplyAllJammedPlaceholders();
            ClearCurrentAgentVisualFocus();

            if (agentCameraFollow != null)
                agentCameraFollow.ClearFocusAgent();

            return;
        }

        if (!force && focusedIndex == currentFocusedInputIndex)
            return;

        if (!TryGetAgentAtInputIndex(focusedIndex, out AgentController targetAgent))
        {
            if (!force)
                return;

            RestoreAllPlaceholderTexts();
            ApplyAllJammedPlaceholders();
            ClearCurrentAgentVisualFocus();

            if (agentCameraFollow != null)
                agentCameraFollow.ClearFocusAgent();

            return;
        }

        InputField inputField = agentInputs[focusedIndex];

        if (inputField == null)
            return;

        ApplyFocusedAgentImmediately(focusedIndex, inputField, targetAgent, false);
    }

    private void FocusInputField(int index)
    {
        if (agentInputs == null)
            return;

        if (index < 0 || index >= agentInputs.Count)
            return;

        if (IsInputIndexJammed(index))
            return;

        InputField inputField = agentInputs[index];

        if (inputField == null || !inputField.interactable)
            return;

        if (!TryGetAgentFromInputField(inputField, index, out AgentController targetAgent))
            return;

        pendingRefocusAfterOrbit = false;

        ApplyFocusedAgentImmediately(index, inputField, targetAgent, true);
    }

    private void ApplyFocusedAgentImmediately(
        int inputIndex,
        InputField inputField,
        AgentController targetAgent,
        bool activateInputField)
    {
        if (inputField == null || targetAgent == null)
            return;

        currentFocusedInputIndex = inputIndex;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);

        if (activateInputField)
        {
            inputField.Select();
            inputField.ActivateInputField();
            inputField.MoveTextEnd(false);
        }

        RestoreAllPlaceholderTexts();
        ApplyAllJammedPlaceholders();
        ClearCurrentAgentVisualFocus();

        if (!IsInputIndexJammed(inputIndex))
            ApplyFocusedInputSkillPlaceholder(inputIndex, targetAgent);

        if (agentCameraFollow != null)
            agentCameraFollow.FocusAgent(targetAgent.transform);

        AgentOutline outline = targetAgent.GetComponent<AgentOutline>();

        if (outline != null)
        {
            outline.SetOutlineVisible(true);
            currentHighlightedOutline = outline;
        }
    }

    private void ClearCurrentAgentVisualFocus()
    {
        if (currentHighlightedOutline != null)
        {
            currentHighlightedOutline.SetOutlineVisible(false);
            currentHighlightedOutline = null;
        }
    }

    private void FocusNextAvailableInputFrom(int startIndex)
    {
        int nextIndex = FindNextAvailableInputIndex(startIndex, false);

        if (nextIndex >= 0 && nextIndex != startIndex)
        {
            FocusInputField(nextIndex);
            return;
        }

        ClearCurrentInputFocus();
    }

    private int FindNextAvailableInputIndex(int startIndex, bool movePrevious)
    {
        if (agentInputs == null || agentInputs.Count == 0)
            return -1;

        int count = agentInputs.Count;
        int step = movePrevious ? -1 : 1;

        for (int offset = 1; offset <= count; offset++)
        {
            int index = (startIndex + step * offset + count) % count;

            if (CanFocusInputIndex(index))
                return index;
        }

        return -1;
    }

    private bool CanFocusInputIndex(int index)
    {
        if (agentInputs == null)
            return false;

        if (index < 0 || index >= agentInputs.Count)
            return false;

        InputField input = agentInputs[index];

        if (input == null)
            return false;

        if (!uiInteractable)
            return false;

        if (IsInputIndexJammed(index))
            return false;

        return input.interactable;
    }

    private bool IsInputIndexJammed(int inputIndex)
    {
        if (!TryGetAgentAtInputIndex(inputIndex, out AgentController agent))
            return false;

        return jammedAgentIds.Contains(agent.AgentID);
    }

    private bool TryGetInputIndexByAgentId(int agentId, out int inputIndex)
    {
        inputIndex = -1;

        if (inputByAgentId.TryGetValue(agentId, out InputField input))
        {
            inputIndex = GetInputIndex(input);
            return inputIndex >= 0;
        }

        if (agents == null)
            return false;

        for (int i = 0; i < agents.Count; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (agent.AgentID != agentId)
                continue;

            inputIndex = i;
            return true;
        }

        return false;
    }

    private bool TryGetAgentAtInputIndex(int index, out AgentController agent)
    {
        agent = null;

        if (agentInputs == null)
            return false;

        if (index < 0 || index >= agentInputs.Count)
            return false;

        InputField input = agentInputs[index];

        if (input == null)
            return false;

        return TryGetAgentFromInputField(input, index, out agent);
    }

    private bool TryGetAgentFromInputField(
        InputField inputField,
        int inputIndex,
        out AgentController agent)
    {
        agent = null;

        if (inputField == null)
            return false;

        if (agentByInput.TryGetValue(inputField, out agent) && agent != null)
            return true;

        if (agents == null)
            return false;

        if (inputIndex < 0 || inputIndex >= agents.Count)
            return false;

        agent = agents[inputIndex];

        if (agent == null)
            return false;

        CacheInputAgentBinding(inputField, agent);
        return true;
    }

    private int GetInputIndex(InputField targetInput)
    {
        if (targetInput == null || agentInputs == null)
            return -1;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            if (agentInputs[i] == targetInput)
                return i;
        }

        return -1;
    }

    private void RegisterInputAgentBinding(int inputIndex, InputField input, AgentController agent)
    {
        if (input == null || agent == null)
            return;

        CacheInputAgentBinding(input, agent);
    }

    private void CacheInputAgentBinding(InputField input, AgentController agent)
    {
        if (input == null || agent == null)
            return;

        agentByInput[input] = agent;

        if (inputByAgentId.TryGetValue(agent.AgentID, out InputField existingInput) &&
            existingInput != null &&
            existingInput != input)
        {
            Debug.LogWarning(
                $"[CommanderUI] AgentID가 중복되었거나 InputField 매핑이 덮어써졌습니다. " +
                $"AgentID={agent.AgentID}, ExistingInput={existingInput.name}, NewInput={input.name}, Agent={agent.name}"
            );
        }

        inputByAgentId[agent.AgentID] = input;
    }

    private void ClearInputAgentBindingCache()
    {
        agentByInput.Clear();
        inputByAgentId.Clear();
    }

    private void RebuildInputAgentBindingCacheFromCurrentLists()
    {
        ClearInputAgentBindingCache();

        if (agentInputs == null || agents == null)
            return;

        int count = Mathf.Min(agentInputs.Count, agents.Count);

        for (int i = 0; i < count; i++)
        {
            RegisterInputAgentBinding(i, agentInputs[i], agents[i]);
        }
    }

    private void WarnIfInputCountMismatch()
    {
        if (agentInputs == null || agents == null)
            return;

        if (agents.Count != agentInputs.Count)
        {
            Debug.LogWarning(
                $"[CommanderUI] agents 수({agents.Count})와 agentInputs 수({agentInputs.Count})가 다릅니다. " +
                "입력칸 인덱스와 AgentID 연결을 확인해주세요."
            );
        }
    }

    private void CacheOriginalPlaceholderTexts()
    {
        originalPlaceholderTextByInput.Clear();

        if (agentInputs == null)
            return;

        foreach (InputField input in agentInputs)
        {
            if (input == null)
                continue;

            originalPlaceholderTextByInput[input] = GetPlaceholderText(input);
        }
    }

    private void RestoreAllPlaceholderTexts()
    {
        foreach (KeyValuePair<InputField, string> pair in originalPlaceholderTextByInput)
        {
            SetPlaceholderText(pair.Key, pair.Value);
        }
    }

    private void ApplyAllJammedPlaceholders()
    {
        if (agentInputs == null)
            return;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            if (!IsInputIndexJammed(i))
                continue;

            InputField input = agentInputs[i];

            if (input == null)
                continue;

            SetPlaceholderText(input, jammedPlaceholderText);
        }
    }

    private void ApplyFocusedInputSkillPlaceholder(int inputIndex, AgentController agent)
    {
        if (agentInputs == null)
            return;

        if (inputIndex < 0 || inputIndex >= agentInputs.Count)
            return;

        if (IsInputIndexJammed(inputIndex))
            return;

        InputField input = agentInputs[inputIndex];

        if (input == null)
            return;

        string skillPlaceholder = GetSkillPlaceholderText(agent);

        if (string.IsNullOrWhiteSpace(skillPlaceholder))
            return;

        SetPlaceholderText(input, skillPlaceholder);
    }

    private string GetSkillPlaceholderText(AgentController agent)
    {
        if (agent == null)
            return "";

        if (TryGetSkillPlaceholderTextByAgentDefinition(agent, out string definitionPlaceholder))
            return definitionPlaceholder;

        return "";
    }

    private bool TryGetSkillPlaceholderTextByAgentDefinition(
        AgentController agent,
        out string placeholderText)
    {
        placeholderText = "";

        if (agent == null)
            return false;

        AgentDefinitionSO definition = agent.AgentDefinition;

        if (definition == null)
            return false;

        List<string> skillTexts = GetSkillPlaceholderTextsFromDefinition(definition);

        if (skillTexts.Count <= 0)
            return false;

        placeholderText = "EX) " + string.Join(", ", skillTexts);
        return true;
    }

    private List<string> GetSkillPlaceholderTextsFromDefinition(AgentDefinitionSO definition)
    {
        List<string> result = new List<string>();

        if (definition == null)
            return result;

        AddPlaceholderSkillTexts(definition.BasicSkills, result);

        return result;
    }

    private void AddPlaceholderSkillTexts(
        IReadOnlyList<SkillDefinitionSO> skills,
        List<string> result)
    {
        if (skills == null || result == null)
            return;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinitionSO skill = skills[i];

            if (skill == null)
                continue;

            if (!ShouldShowSkillInPlaceholder(skill))
                continue;

            string skillText = GetPlaceholderTextFromSkill(skill);

            if (string.IsNullOrWhiteSpace(skillText))
                continue;

            if (ContainsSamePlaceholderText(result, skillText))
                continue;

            result.Add(skillText);
        }
    }

    private bool ShouldShowSkillInPlaceholder(SkillDefinitionSO skill)
    {
        if (skill == null)
            return false;

        return skill.IsCommandSkill || skill.IsToggleSkill;
    }

    private string GetPlaceholderTextFromSkill(SkillDefinitionSO skill)
    {
        if (skill == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skill.UsageText))
            return skill.UsageText.Trim();

        if (!string.IsNullOrWhiteSpace(skill.CommandKeyword))
            return skill.CommandKeyword.Trim();

        if (!string.IsNullOrWhiteSpace(skill.DisplayName))
            return skill.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(skill.SkillId))
            return skill.SkillId.Trim();

        return skill.name;
    }

    private bool ContainsSamePlaceholderText(List<string> values, string text)
    {
        if (values == null)
            return false;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string normalizedText = NormalizePlaceholderText(text);

        for (int i = 0; i < values.Count; i++)
        {
            if (NormalizePlaceholderText(values[i]) == normalizedText)
                return true;
        }

        return false;
    }

    private string NormalizePlaceholderText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");
    }

    private string GetPlaceholderText(InputField input)
    {
        if (input == null || input.placeholder == null)
            return "";

        Text placeholderText = input.placeholder.GetComponent<Text>();

        if (placeholderText == null)
            return "";

        return placeholderText.text;
    }

    private void SetPlaceholderText(InputField input, string text)
    {
        if (input == null || input.placeholder == null)
            return;

        Text placeholderText = input.placeholder.GetComponent<Text>();

        if (placeholderText == null)
            return;

        placeholderText.text = text;
    }

    public void TestRandomCommunicationJamFromButton()
    {
        if (agents == null || agents.Count == 0)
        {
            Debug.LogWarning("[CommanderUI] 에이전트가 아직 바인딩되지 않았습니다.");
            return;
        }

        if (TryJamRandomAvailableAgentInputUntilCommandButton(out int jammedAgentId))
        {
            Debug.Log($"[CommanderUI] 통신 방해 테스트 성공: AgentID {jammedAgentId}, 명령 버튼 클릭 시 해제");
            return;
        }

        Debug.LogWarning("[CommanderUI] 통신 방해 테스트 실패: 방해 가능한 에이전트가 없습니다.");
    }

    public bool TryPasteSkillDisplayNameToAgentInput(int clickedAgentId, string skillDisplayName)
    {
        if (!TryGetInputIndexByAgentId(clickedAgentId, out int inputIndex))
            return false;

        return TryPasteSkillDisplayNameToInputIndex(
            inputIndex,
            clickedAgentId,
            skillDisplayName,
            true
        );
    }

    public void BindInputFieldsAndAgents(
        IReadOnlyList<InputField> inputFields,
        IReadOnlyList<AgentController> boundAgents)
    {
        ClearCurrentInputFocus();
        ClearAllAgentInputJams();

        agentInputs.Clear();
        ClearInputAgentBindingCache();

        List<AgentController> normalizedAgents = new List<AgentController>();

        if (inputFields != null)
        {
            for (int i = 0; i < inputFields.Count; i++)
            {
                InputField input = inputFields[i];

                if (input == null)
                    continue;

                AgentController agent = null;

                if (boundAgents != null && i < boundAgents.Count)
                    agent = boundAgents[i];

                agentInputs.Add(input);
                normalizedAgents.Add(agent);

                RegisterInputAgentBinding(agentInputs.Count - 1, input, agent);
            }
        }

        agents = normalizedAgents;

        CacheOriginalPlaceholderTexts();
        WarnIfInputCountMismatch();
        RefreshAllInputInteractableStates();
        UpdateFocusedInputPresentation(true);
    }
}