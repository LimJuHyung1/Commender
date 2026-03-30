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
    [SerializeField] private ChatServiceOpenAI chatService;
    [SerializeField] private List<AgentController> agents;
    [SerializeField] private List<InputField> agentInputs;
    [SerializeField] private Button submitButton;
    [SerializeField] private AgentCameraFollow agentCameraFollow;

    private string systemPrompt =
    "You are a tactical coordinator for the game 'Commender'.\n\n" +
    "RULES:\n" +
    "1. Each input line formatted as 'Agent N Instruction: ...' MUST produce a command with \"id\": N.\n" +
    "2. Never change the agent id.\n" +
    "3. If the user specifies a location like '0,0', set pos as {\"x\":0.0,\"z\":0.0}.\n" +
    "4. If the instruction is movement only, skill MUST be an empty string.\n" +
    "5. Use \"dash\" ONLY when the instruction explicitly asks for dash, 대시, or 대쉬.\n" +
    "6. Use \"smoke\" ONLY when the instruction explicitly asks for smoke or 연막.\n" +
    "7. Use \"reveal\" ONLY when the instruction explicitly asks for reveal, recon, recondrone, drone, 드론, 정찰, 정찰 드론, 정찰드론, 드론 설치, or 정찰 드론 설치.\n" +
    "8. Use \"wallsight\" ONLY when the instruction explicitly asks for wallsight, 투시, 벽 너머 시야, 벽너머 시야, or 벽너머 보기.\n" +
    "9. Use \"barricade\" ONLY when the instruction explicitly asks for barricade, 바리케이드, 봉쇄, or 장애물 설치.\n" +
    "10. Use \"slowtrap\" ONLY when the instruction explicitly asks for slowtrap, snaretrap, trap, 함정, 정지 함정, 구속 함정, 속박 함정, or 트랩 설치.\n" +
    "11. Use \"decoysignal\" ONLY when the instruction explicitly asks for decoysignal, decoy, 유인 신호, 유인기, 유인 장치, or 유인 신호 설치.\n" +
    "12. Use \"phantom\" ONLY when the instruction explicitly asks for phantom, 가짜 위협, 환영, 현재 위치에 환영, 자기 위치에 환영, or 가짜 신호.\n" +
    "13. Phantom is always created at the disruptor agent's CURRENT POSITION, not at the requested coordinate.\n" +
    "14. Only one phantom can exist for that agent at a time.\n" +
    "15. Do not infer skills from normal movement.\n" +
    "16. Only dash may be combined with movement. All other skills must be skill-only actions.\n" +
    "17. If a skill is used without a location, set pos as {\"x\":0.0,\"z\":0.0}.\n" +
    "18. Output JSON only.\n\n" +
    "EXAMPLES:\n" +
    "Input: Agent 0 Instruction: 0,0으로 이동\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 3,2로 대시\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"pos\": {\"x\": 3.0, \"z\": 2.0}, \"skill\": \"dash\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 5,4에 연막 사용\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"pos\": {\"x\": 5.0, \"z\": 4.0}, \"skill\": \"smoke\" } ] }\n\n" +
    "Input: Agent 1 Instruction: 10,5에 드론 설치\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 1, \"pos\": {\"x\": 10.0, \"z\": 5.0}, \"skill\": \"reveal\" } ] }\n\n" +
    "Input: Agent 1 Instruction: 투시 사용\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 1, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"wallsight\" } ] }\n\n" +
    "Input: Agent 2 Instruction: 8,4에 바리케이드 설치\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 2, \"pos\": {\"x\": 8.0, \"z\": 4.0}, \"skill\": \"barricade\" } ] }\n\n" +
    "Input: Agent 2 Instruction: 6,7에 함정 설치\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 2, \"pos\": {\"x\": 6.0, \"z\": 7.0}, \"skill\": \"slowtrap\" } ] }\n\n" +
    "Input: Agent 3 Instruction: 9,6에 유인 신호 설치\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 3, \"pos\": {\"x\": 9.0, \"z\": 6.0}, \"skill\": \"decoysignal\" } ] }\n\n" +
    "Input: Agent 3 Instruction: 환영 사용\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 3, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"phantom\" } ] }";

    private bool requestInFlight = false;
    private int currentFocusedInputIndex = -1;
    private AgentOutline currentHighlightedOutline;

    private readonly Dictionary<int, AgentController> agentById = new Dictionary<int, AgentController>();
    private readonly Dictionary<int, string> submittedInstructionById = new Dictionary<int, string>();

    private void Awake()
    {
        RebuildAgentLookup();

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitAllCommands);
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

        if (currentHighlightedOutline != null)
        {
            currentHighlightedOutline.SetOutlineVisible(false);
            currentHighlightedOutline = null;
        }

        if (focusedIndex < 0 || focusedIndex >= agents.Count)
        {
            if (agentCameraFollow != null)
                agentCameraFollow.ClearFocusAgent();

            return;
        }

        AgentController targetAgent = agents[focusedIndex];
        if (targetAgent == null)
        {
            if (agentCameraFollow != null)
                agentCameraFollow.ClearFocusAgent();

            return;
        }

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

        EventSystem.current.SetSelectedGameObject(nextInput.gameObject);
        nextInput.Select();
        nextInput.ActivateInputField();
        nextInput.MoveTextEnd(false);

        UpdateFocusedInputPresentation();
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

    public async void OnSubmitAllCommands()
    {
        if (requestInFlight)
            return;

        if (chatService == null)
        {
            Debug.LogError("[Commender] ChatServiceOpenAI 참조가 없습니다.");
            return;
        }

        RebuildAgentLookup();
        submittedInstructionById.Clear();

        StringBuilder combinedPrompt = new StringBuilder();
        bool hasAnyValidInput = false;

        for (int i = 0; i < agentInputs.Count; i++)
        {
            InputField input = agentInputs[i];
            if (input == null)
                continue;

            string instruction = input.text.Trim();
            if (string.IsNullOrEmpty(instruction))
                continue;

            submittedInstructionById[i] = instruction;
            combinedPrompt.AppendLine($"Agent {i} Instruction: {instruction}");
            hasAnyValidInput = true;
        }

        if (!hasAnyValidInput)
            return;

        requestInFlight = true;
        SetUIInteractable(false);

        try
        {
            string rawResponse = await chatService.GetResponseAsync(systemPrompt, combinedPrompt.ToString());

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                Debug.LogWarning("[Commender] AI 응답이 비어 있어 명령 처리를 중단합니다.");
                return;
            }

            ProcessAICommand(rawResponse);
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

    private void ProcessAICommand(string raw)
    {
        Debug.Log($"[Commender] AI 응답 데이터: {raw}");

        if (string.IsNullOrWhiteSpace(raw))
        {
            Debug.LogWarning("[Commender] AI 응답이 비어 있습니다.");
            return;
        }

        try
        {
            string json = ExtractJsonObject(raw);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[Commender] 응답에서 JSON 객체를 찾지 못했습니다.");
                return;
            }

            CommandGroup group = JsonUtility.FromJson<CommandGroup>(json);

            if (group == null || group.commands == null || group.commands.Count == 0)
            {
                Debug.LogWarning("[Commender] 명령 데이터가 비어 있습니다.");
                return;
            }

            foreach (MoveCommand cmd in group.commands)
            {
                if (cmd == null)
                    continue;

                if (!agentById.TryGetValue(cmd.id, out AgentController targetAgent) || targetAgent == null)
                {
                    Debug.LogWarning($"[Commender] Agent ID {cmd.id} 를 찾지 못했습니다.");
                    continue;
                }

                float x = targetAgent.transform.position.x;
                float z = targetAgent.transform.position.z;

                if (cmd.pos != null)
                {
                    x = cmd.pos.x;
                    z = cmd.pos.z;
                }
                else
                {
                    Debug.LogWarning($"[Commender] Agent {cmd.id} 명령에 pos가 없습니다. 현재 위치 기준으로 처리합니다.");
                }

                Vector3 dest = new Vector3(x, targetAgent.transform.position.y, z);

                string originalInstruction = "";
                if (submittedInstructionById.TryGetValue(cmd.id, out string savedInstruction))
                    originalInstruction = savedInstruction;

                string validatedSkill = ValidateSkill(cmd.skill, originalInstruction);

                if (!string.IsNullOrWhiteSpace(validatedSkill))
                {
                    if (validatedSkill == "dash")
                    {
                        Debug.Log($"<color=cyan>[Action]</color> Agent {cmd.id} : {dest} 로 dash 이동");
                        targetAgent.MoveTo(dest);
                        targetAgent.ExecuteSkill(validatedSkill, dest);
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>[Action]</color> Agent {cmd.id} : {dest} 위치에 '{validatedSkill}' 스킬 사용");
                        targetAgent.ExecuteSkill(validatedSkill, dest);
                    }
                }
                else
                {
                    Debug.Log($"<color=green>[Action]</color> Agent {cmd.id} : {dest} 로 이동 명령");
                    targetAgent.MoveTo(dest);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commender] 파싱 오류: {e}");
        }
    }

    private string ValidateSkill(string aiSkill, string originalInstruction)
    {
        if (string.IsNullOrWhiteSpace(aiSkill))
            return "";

        string normalizedSkill = aiSkill.Trim().ToLower();
        string normalizedInstruction = originalInstruction == null ? "" : originalInstruction.Trim().ToLower();

        if (normalizedSkill.Contains("dash"))
        {
            if (ContainsAny(normalizedInstruction, "dash", "대시", "대쉬"))
                return "dash";

            Debug.LogWarning($"[Commender] 원문에 dash 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("smoke"))
        {
            if (ContainsAny(normalizedInstruction, "smoke", "연막", "연막탄"))
                return "smoke";

            Debug.LogWarning($"[Commender] 원문에 smoke 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("reveal") || normalizedSkill.Contains("recon") || normalizedSkill.Contains("recondrone"))
        {
            if (ContainsAny(normalizedInstruction, "reveal", "recon", "recondrone", "드론", "정찰 드론", "정찰드론"))
                return "reveal";

            Debug.LogWarning($"[Commender] 원문에 reveal 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("wallsight") || normalizedSkill.Contains("truesight"))
        {
            if (ContainsAny(normalizedInstruction, "wallsight", "truesight", "투시", "벽 너머", "벽너머", "시야"))
                return "wallsight";

            Debug.LogWarning($"[Commender] 원문에 wallsight 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("barricade"))
        {
            if (ContainsAny(normalizedInstruction, "barricade", "바리케이드", "바리게이트", "장애물"))
                return "barricade";

            Debug.LogWarning($"[Commender] 원문에 barricade 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("slowtrap") || normalizedSkill.Contains("snaretrap") || normalizedSkill.Contains("trap"))
        {
            if (ContainsAny(normalizedInstruction, "slowtrap", "snaretrap", "trap", "함정", "트랩"))
                return "slowtrap";

            Debug.LogWarning($"[Commender] 원문에 slowtrap 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("decoysignal") || normalizedSkill.Contains("decoy"))
        {
            if (ContainsAny(normalizedInstruction, "decoysignal", "decoy", "유인 신호", "유인기", "유인"))
                return "decoysignal";

            Debug.LogWarning($"[Commender] 원문에 decoysignal 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("phantom"))
        {
            if (ContainsAny(normalizedInstruction, "phantom", "홀로그램", "환영", "현재 위치", "현재위치"))
                return "phantom";

            Debug.LogWarning($"[Commender] 원문에 phantom 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        Debug.LogWarning($"[Commender] 알 수 없는 skill='{aiSkill}' 를 무시합니다.");
        return "";
    }

    private bool ContainsAny(string source, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        foreach (string keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && source.Contains(keyword))
                return true;
        }

        return false;
    }

    private string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
            return "";

        return text.Substring(start, end - start + 1);
    }

    private void SetUIInteractable(bool state)
    {
        if (!state)
        {
            currentFocusedInputIndex = -1;

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

    [Serializable]
    public class CommandGroup
    {
        public List<MoveCommand> commands;
    }

    [Serializable]
    public class MoveCommand
    {
        public int id;
        public PosData pos;
        public string skill;
    }

    [Serializable]
    public class PosData
    {
        public float x;
        public float z;
    }
}