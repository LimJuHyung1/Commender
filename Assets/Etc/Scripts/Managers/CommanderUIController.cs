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

    private IReadOnlyList<AgentController> agents;
    private int currentFocusedInputIndex = -1;
    private AgentOutline currentHighlightedOutline;
    private bool uiInteractable = true;

    private readonly Dictionary<InputField, string> originalPlaceholderTextByInput =
        new Dictionary<InputField, string>();

    private void Awake()
    {
        CacheOriginalPlaceholderTexts();
    }

    private void Update()
    {
        if (!uiInteractable)
            return;

        HandleFunctionKeyFocus();
        HandleTabInputNavigation();
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
        UpdateFocusedInputPresentation(true);
    }

    public void SetUIInteractable(bool state)
    {
        uiInteractable = state;

        if (!state)
        {
            currentFocusedInputIndex = -1;
            RestoreAllPlaceholderTexts();

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

        if (agentInputs != null)
        {
            foreach (InputField input in agentInputs)
            {
                if (input != null)
                    input.interactable = state;
            }
        }

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

            string instruction = input.text.Trim();
            if (string.IsNullOrEmpty(instruction))
                continue;

            if (!TryGetAgentAtInputIndex(i, out AgentController agent))
            {
                Debug.LogWarning($"[CommenderUI] Input index {i} 에 연결된 Agent가 없습니다.");
                continue;
            }

            submittedInstructionById[agent.AgentID] = instruction;
            hasAnyValidInput = true;
        }

        return hasAnyValidInput;
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
            return;

        bool movePrevious =
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed;

        int nextIndex = movePrevious
            ? (currentIndex - 1 + agentInputs.Count) % agentInputs.Count
            : (currentIndex + 1) % agentInputs.Count;

        FocusInputField(nextIndex);
    }

    private int GetSelectedInputIndex()
    {
        if (EventSystem.current == null)
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

    private void UpdateFocusedInputPresentation(bool force = false)
    {
        int focusedIndex = GetFocusedInputIndex();

        if (!force && focusedIndex == currentFocusedInputIndex)
            return;

        currentFocusedInputIndex = focusedIndex;

        RestoreAllPlaceholderTexts();

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

        InputField nextInput = agentInputs[index];
        if (nextInput == null || !nextInput.interactable)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nextInput.gameObject);

        nextInput.Select();
        nextInput.ActivateInputField();
        nextInput.MoveTextEnd(false);

        UpdateFocusedInputPresentation(true);
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
                $"[CommenderUI] agents 수({agents.Count})와 agentInputs 수({agentInputs.Count})가 다릅니다. " +
                $"입력칸 인덱스와 AgentID 매핑을 확인해주세요."
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

    private void ApplyFocusedInputSkillPlaceholder(int inputIndex, AgentController agent)
    {
        if (agentInputs == null)
            return;

        if (inputIndex < 0 || inputIndex >= agentInputs.Count)
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
            return "EX) 드론, 투시";

        if (typeName.Contains("Engineer"))
            return "EX) 바리케이드, 함정";

        if (typeName.Contains("Disruptor"))
            return "EX) 소란 장치, 홀로그램";

        switch (agent.AgentID)
        {
            case 0:
                return "EX) 대쉬, 연막";
            case 1:
                return "EX) 드론, 투시";
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
}