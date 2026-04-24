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
    [SerializeField] private TargetSkillController targetSkillController;

    [Header("Hotkeys")]
    [SerializeField] private bool enableFunctionKeyFocus = true;

    [Header("Communication Jam")]
    [SerializeField] private string jammedPlaceholderText = "통신 방해 중...";
    [SerializeField] private bool clearTextOnJam = true;

    private IReadOnlyList<AgentController> agents;
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

        if (targetSkillController == null)
            targetSkillController = FindFirstObjectByType<TargetSkillController>();

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
        WarnIfInputCountMismatch();
        RefreshAllInputInteractableStates();
        UpdateFocusedInputPresentation(true);
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

            pendingRefocusAfterOrbit = false;
            currentFocusedInputIndex = -1;
            RestoreAllPlaceholderTexts();
            ApplyAllJammedPlaceholders();

            if (agentCameraFollow != null)
                agentCameraFollow.ClearFocusAgent();

            if (currentHighlightedOutline != null)
            {
                currentHighlightedOutline.SetOutlineVisible(false);
                currentHighlightedOutline = null;
            }

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
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
                Debug.LogWarning($"[CommanderUI] Input index {i} 에 연결된 Agent가 없습니다.");
                continue;
            }

            submittedInstructionById[agent.AgentID] = instruction;
            hasAnyValidInput = true;
        }

        return hasAnyValidInput;
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
        UpdateFocusedInputPresentation(true);
    }

    public bool IsAgentInputJammed(int agentId)
    {
        return jammedAgentIds.Contains(agentId);
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

        FocusInputField(inputIndex);
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

        int currentIndex = GetSelectedInputIndex();
        if (currentIndex < 0)
            currentIndex = GetEffectiveFocusedInputIndex();

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

        if (selectedIndex < 0 && focusedIndex < 0)
            return;

        ClearInputFocusBeforeSubmit();
        submitButton.onClick.Invoke();
    }

    private void HandleCommandSubmitCommunicationJam()
    {
        if (!uiInteractable)
            return;

        if (targetSkillController == null)
            return;

        if (!TryBuildSubmittedInstructions(out Dictionary<int, string> submittedInstructionById))
            return;

        if (submittedInstructionById == null || submittedInstructionById.Count == 0)
            return;

        targetSkillController.TryUseCommunicationJamOnCommandSubmission();
    }

    private void ClearInputFocusBeforeSubmit()
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

        if (currentHighlightedOutline != null)
        {
            currentHighlightedOutline.SetOutlineVisible(false);
            currentHighlightedOutline = null;
        }

        currentFocusedInputIndex = -1;
        RestoreAllPlaceholderTexts();
        ApplyAllJammedPlaceholders();
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

        if (ShouldRetainFocusDuringOrbitInput() && CanFocusInputIndex(currentFocusedInputIndex))
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

        if (!force && focusedIndex == currentFocusedInputIndex)
            return;

        currentFocusedInputIndex = focusedIndex;

        RestoreAllPlaceholderTexts();
        ApplyAllJammedPlaceholders();

        if (currentHighlightedOutline != null)
        {
            currentHighlightedOutline.SetOutlineVisible(false);
            currentHighlightedOutline = null;
        }

        if (!TryGetAgentAtInputIndex(focusedIndex, out AgentController targetAgent))
        {
            if (agentCameraFollow != null)
                agentCameraFollow.ClearFocusAgent();

            return;
        }

        if (!IsInputIndexJammed(focusedIndex))
            ApplyFocusedInputSkillPlaceholder(focusedIndex, targetAgent);

        if (agentCameraFollow != null)
            agentCameraFollow.FocusAgent(targetAgent.transform);

        AgentOutline outline = targetAgent.GetComponent<AgentOutline>();
        if (outline != null)
        {
            outline.SetOutlineVisible(true);
            currentHighlightedOutline = outline;
        }
    }

    private void FocusInputField(int index)
    {
        if (agentInputs == null)
            return;

        if (index < 0 || index >= agentInputs.Count)
            return;

        if (IsInputIndexJammed(index))
            return;

        InputField nextInput = agentInputs[index];
        if (nextInput == null || !nextInput.interactable)
            return;

        pendingRefocusAfterOrbit = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nextInput.gameObject);

        nextInput.Select();
        nextInput.ActivateInputField();
        nextInput.MoveTextEnd(false);

        UpdateFocusedInputPresentation(true);
    }

    private void FocusNextAvailableInputFrom(int startIndex)
    {
        int nextIndex = FindNextAvailableInputIndex(startIndex, false);

        if (nextIndex >= 0 && nextIndex != startIndex)
        {
            FocusInputField(nextIndex);
            return;
        }

        pendingRefocusAfterOrbit = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (agentCameraFollow != null)
            agentCameraFollow.ClearFocusAgent();

        if (currentHighlightedOutline != null)
        {
            currentHighlightedOutline.SetOutlineVisible(false);
            currentHighlightedOutline = null;
        }

        currentFocusedInputIndex = -1;
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

        if (agents == null)
            return false;

        if (index < 0 || index >= agents.Count)
            return false;

        agent = agents[index];
        return agent != null;
    }

    private void WarnIfInputCountMismatch()
    {
        if (agentInputs == null || agents == null)
            return;

        if (agents.Count != agentInputs.Count)
        {
            Debug.LogWarning(
                $"[CommanderUI] agents 수({agents.Count})와 agentInputs 수({agentInputs.Count})가 다릅니다. " +
                $"입력칸 인덱스와 AgentID 연결을 확인해주세요."
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

        string typeName = agent.GetType().Name;

        if (typeName.Contains("Pursuer"))
            return "EX) 대쉬, 연막";

        if (typeName.Contains("Scout"))
            return "EX) 조명탄, 투시";

        if (typeName.Contains("Engineer"))
            return "EX) 바리케이드, 함정";

        if (typeName.Contains("Disruptor"))
            return "EX) 소란 장치, 홀로그램";

        switch (agent.AgentID)
        {
            case 0:
                return "EX) 대쉬, 연막";
            case 1:
                return "EX) 조명탄, 투시";
            case 2:
                return "EX) 바리케이드, 함정";
            case 3:
                return "EX) 소란 장치, 홀로그램";
        }

        return "";
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

        int startIndex = Random.Range(0, agents.Count);

        for (int offset = 0; offset < agents.Count; offset++)
        {
            int index = (startIndex + offset) % agents.Count;
            AgentController agent = agents[index];

            if (agent == null)
                continue;

            if (TryJamAgentInput(agent.AgentID, 15f))
            {
                Debug.Log($"[CommanderUI] 통신 방해 테스트 성공: AgentID {agent.AgentID}, 15초");
                return;
            }
        }

        Debug.LogWarning("[CommanderUI] 통신 방해 테스트 실패: 방해 가능한 에이전트가 없습니다.");
    }
}