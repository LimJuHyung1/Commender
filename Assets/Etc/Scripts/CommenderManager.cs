using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class CommenderManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CommenderCommandProcessor commandProcessor;
    [SerializeField] private List<AgentController> agents;
    [SerializeField] private List<InputField> agentInputs;
    [SerializeField] private Button submitButton;
    [SerializeField] private AgentCameraFollow agentCameraFollow;

    private bool requestInFlight = false;
    private int currentFocusedInputIndex = -1;
    private AgentOutline currentHighlightedOutline;

    private readonly Dictionary<int, AgentController> agentById = new Dictionary<int, AgentController>();
    private readonly Dictionary<int, string> submittedInstructionById = new Dictionary<int, string>();
    private readonly Dictionary<InputField, string> originalPlaceholderTextByInput = new Dictionary<InputField, string>();

    private void Awake()
    {
        RebuildAgentLookup();
        CacheOriginalPlaceholderTexts();

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitAllCommands);
    }

    private void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmitAllCommands);
    }

    private void Update()
    {
        HandleTabInputNavigation();
        UpdateFocusedInputPresentation();
    }

    private void HandleTabInputNavigation()
    {
        if (requestInFlight)
            return;

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

    private void UpdateFocusedInputPresentation()
    {
        int focusedIndex = GetFocusedInputIndex();

        if (focusedIndex == currentFocusedInputIndex)
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

        UpdateFocusedInputPresentation();
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

    private void RebuildAgentLookup()
    {
        agentById.Clear();

        if (agents == null)
            return;

        foreach (AgentController agent in agents)
        {
            if (agent == null)
                continue;

            int id = agent.AgentID;

            if (agentById.ContainsKey(id))
            {
                Debug.LogError($"[Commender] 중복된 AgentID가 있습니다. ID: {id}");
                continue;
            }

            agentById.Add(id, agent);
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

    public async void OnSubmitAllCommands()
    {
        if (requestInFlight)
            return;

        if (commandProcessor == null)
        {
            Debug.LogError("[Commender] CommenderCommandProcessor 참조가 없습니다.");
            return;
        }

        RebuildAgentLookup();
        submittedInstructionById.Clear();

        if (!TryBuildCombinedPrompt(out string combinedPrompt))
            return;

        requestInFlight = true;
        SetUIInteractable(false);

        try
        {
            await commandProcessor.ProcessCommandsAsync(
                combinedPrompt,
                submittedInstructionById,
                agentById
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commender] 오류: {e}");
        }
        finally
        {
            requestInFlight = false;
            SetUIInteractable(true);
            ClearAllInputs();
            submittedInstructionById.Clear();
        }
    }

    private bool TryBuildCombinedPrompt(out string combinedPrompt)
    {
        combinedPrompt = "";
        StringBuilder builder = new StringBuilder();
        bool hasAnyValidInput = false;

        if (agentInputs == null || agentInputs.Count == 0)
            return false;

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
                Debug.LogWarning($"[Commender] Input index {i} 에 연결된 Agent가 없습니다.");
                continue;
            }

            int agentId = agent.AgentID;

            submittedInstructionById[agentId] = instruction;
            builder.AppendLine($"Agent {agentId} Instruction: {instruction}");
            hasAnyValidInput = true;
        }

        if (!hasAnyValidInput)
            return false;

        combinedPrompt = builder.ToString();
        return true;
    }

    private void SetUIInteractable(bool state)
    {
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

    private void ClearAllInputs()
    {
        if (agentInputs == null)
            return;

        foreach (InputField input in agentInputs)
        {
            if (input != null)
                input.text = "";
        }
    }
}