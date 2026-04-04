using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class CommanderCommandProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChatServiceOpenAI chatService;

    [Header("Auto Bind")]
    [SerializeField] private bool autoBindAgentsFromScene = true;
    [SerializeField] private bool sortAgentsById = true;

    private readonly List<AgentController> agents = new List<AgentController>();
    private readonly Dictionary<int, AgentController> agentById = new Dictionary<int, AgentController>();
    private readonly Dictionary<int, Coroutine> scheduledCommandByAgentId = new Dictionary<int, Coroutine>();

    private CommandValidator commandValidator;
    private CommandExecutor commandExecutor;

    private string systemPrompt =
    "You are a tactical coordinator for the game 'Commender'.\n\n" +
    "RULES:\n" +
    "1. Each input line formatted as 'Agent N Instruction: ...' MUST produce exactly one command with \"id\": N.\n" +
    "2. Never change the agent id.\n" +
    "3. If the instruction says a time delay such as '5초 후', '5초 뒤', '5초 이후', 'after 5 seconds', or 'in 5 seconds', set \"delaySeconds\" to that value.\n" +
    "4. If no time delay is specified, set \"delaySeconds\": 0.0.\n" +
    "5. \"delaySeconds\" must never be negative.\n" +
    "6. If the user clearly specifies a location like '0,0' for movement, set pos as {\"x\":0.0,\"z\":0.0}.\n" +
    "7. If the instruction is movement only, skill MUST be an empty string.\n" +
    "8. If the instruction explicitly asks to check surroundings such as 주변, 주위, 주변 확인, 주위 확인, 주변 둘러봐, 주위 둘러봐, 주변 살펴봐, 주위 살펴봐, look around, or check around, use skill \"lookaround\".\n" +
    "9. Bare instructions like '주변' or '주위' should also be interpreted as \"lookaround\".\n" +
    "10. If the instruction is vague, unsupported, or outside the supported command set, use skill \"hold\" and do not convert it into movement.\n" +
    "11. When using \"lookaround\" or \"hold\", pos should be {\"x\":0.0,\"z\":0.0}.\n" +
    "12. Use \"dash\" ONLY when the instruction explicitly asks for dash, 대시, or 대쉬.\n" +
    "13. Use \"smoke\" ONLY when the instruction explicitly asks for smoke, 연막, or 연막탄.\n" +
    "14. Use \"reveal\" ONLY when the instruction explicitly asks for reveal, recon, recondrone, drone, 드론, 정찰, 정찰 드론, 정찰드론, 드론 설치, or 정찰 드론 설치.\n" +
    "15. Use \"wallsight\" ONLY when the instruction explicitly asks for wallsight, 투시, 벽 너머 시야, 벽너머 시야, or 벽너머 보기.\n" +
    "16. Use \"barricade\" ONLY when the instruction explicitly asks for barricade, 바리케이드, 봉쇄, or 장애물 설치.\n" +
    "17. Use \"slowtrap\" ONLY when the instruction explicitly asks for slowtrap, snaretrap, trap, 함정, 정지 함정, 구속 함정, 속박 함정, or 트랩 설치.\n" +
    "18. Use \"noisemaker\" ONLY when the instruction explicitly asks for noisemaker, noise, 소란 장치, 소음 장치, 소음 발생기, 소란 기계, or 소란 장치 설치.\n" +
    "19. Use \"hologram\" ONLY when the instruction explicitly asks for hologram, 홀로그램, 홀로그램 설치, 현재 위치에 홀로그램, or 자기 위치에 홀로그램.\n" +
    "20. Hologram is always created at the disruptor agent's CURRENT POSITION, not at the requested coordinate.\n" +
    "21. Only one hologram can exist for that agent at a time.\n" +
    "22. Do not infer unsupported movement from unknown text.\n" +
    "23. Only dash may be combined with movement. All other skills must be skill-only actions.\n" +
    "24. If a skill is used without a location, set pos as {\"x\":0.0,\"z\":0.0}.\n" +
    "25. Do not create chained actions, conditional actions, or multiple sequential actions from one input line.\n" +
    "26. Output JSON only.\n\n" +
    "OUTPUT FORMAT:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
    "EXAMPLES:\n" +
    "Input: Agent 0 Instruction: 0,0으로 이동\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 주변\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"lookaround\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 주위\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"lookaround\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 주변 살펴봐\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"lookaround\" } ] }\n\n" +
    "Input: Agent 1 Instruction: 3초 후 주위 확인\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 1, \"delaySeconds\": 3.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"lookaround\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 이해할 수 없는 명령\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"hold\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 3,2로 대시\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 3.0, \"z\": 2.0}, \"skill\": \"dash\" } ] }\n\n" +
    "Input: Agent 0 Instruction: 5,4에 연막 사용\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 5.0, \"z\": 4.0}, \"skill\": \"smoke\" } ] }";

    private void Awake()
    {
        EnsureHelpers();
        RefreshAgentsFromScene(true);
    }

    private void OnDisable()
    {
        CancelAllScheduledCommandsInternal(false);
    }

    private void OnDestroy()
    {
        CancelAllScheduledCommandsInternal(false);
    }

    [ContextMenu("Refresh Agents From Scene")]
    public void RefreshAgentsFromScene()
    {
        RefreshAgentsFromScene(true);
    }

    [ContextMenu("Cancel All Scheduled Commands")]
    public void CancelAllScheduledCommands()
    {
        CancelAllScheduledCommandsInternal(true);
    }

    public IReadOnlyList<AgentController> GetAgents()
    {
        return agents;
    }

    public async Task<bool> ProcessCommandsFromUIAsync(CommanderUIController uiController)
    {
        if (uiController == null)
        {
            Debug.LogError("[Commender] CommenderUIController 참조가 없습니다.");
            return false;
        }

        RefreshAgentsFromScene(true);

        if (!uiController.TryBuildSubmittedInstructions(out Dictionary<int, string> submittedInstructionById))
        {
            Debug.LogWarning("[Commender] 제출할 입력값이 없습니다.");
            return false;
        }

        if (!TryBuildCombinedPrompt(submittedInstructionById, out string combinedPrompt))
        {
            Debug.LogWarning("[Commender] 프롬프트 생성에 실패했습니다.");
            return false;
        }

        return await ProcessCommandsAsync(combinedPrompt, submittedInstructionById);
    }

    public async Task<bool> ProcessCommandsAsync(
        string combinedPrompt,
        Dictionary<int, string> submittedInstructionById)
    {
        EnsureHelpers();

        if (chatService == null)
        {
            Debug.LogError("[Commender] ChatServiceOpenAI 참조가 없습니다.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(combinedPrompt))
        {
            Debug.LogWarning("[Commender] 전달된 프롬프트가 비어 있습니다.");
            return false;
        }

        string rawResponse = await chatService.GetResponseAsync(systemPrompt, combinedPrompt);

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            Debug.LogWarning("[Commender] AI 응답이 비어 있어 명령 처리를 중단합니다.");
            return false;
        }

        return ProcessAICommand(rawResponse, submittedInstructionById);
    }

    private void EnsureHelpers()
    {
        if (commandValidator == null)
            commandValidator = new CommandValidator();

        if (commandExecutor == null)
            commandExecutor = new CommandExecutor();
    }

    private void RefreshAgentsFromScene(bool force)
    {
        if (!autoBindAgentsFromScene)
        {
            RebuildAgentLookup();
            return;
        }

        if (!force && !NeedsAgentRefresh())
            return;

        AgentController[] foundAgents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        agents.Clear();

        if (foundAgents == null || foundAgents.Length == 0)
        {
            agentById.Clear();
            return;
        }

        if (sortAgentsById)
            Array.Sort(foundAgents, CompareAgentsById);

        for (int i = 0; i < foundAgents.Length; i++)
        {
            if (foundAgents[i] != null)
                agents.Add(foundAgents[i]);
        }

        RebuildAgentLookup();
    }

    private bool NeedsAgentRefresh()
    {
        if (agents.Count == 0)
            return true;

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] == null)
                return true;
        }

        return false;
    }

    private static int CompareAgentsById(AgentController a, AgentController b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        return a.AgentID.CompareTo(b.AgentID);
    }

    private void RebuildAgentLookup()
    {
        agentById.Clear();

        for (int i = 0; i < agents.Count; i++)
        {
            AgentController agent = agents[i];
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

    private bool TryBuildCombinedPrompt(
        Dictionary<int, string> submittedInstructionById,
        out string combinedPrompt)
    {
        combinedPrompt = "";

        if (submittedInstructionById == null || submittedInstructionById.Count == 0)
            return false;

        StringBuilder builder = new StringBuilder();

        foreach (KeyValuePair<int, string> pair in submittedInstructionById)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
                continue;

            builder.AppendLine($"Agent {pair.Key} Instruction: {pair.Value}");
        }

        if (builder.Length == 0)
            return false;

        combinedPrompt = builder.ToString();
        return true;
    }

    private bool ProcessAICommand(
        string raw,
        Dictionary<int, string> submittedInstructionById)
    {
        EnsureHelpers();

        Debug.Log($"[Commender] AI 응답 데이터: {raw}");

        if (string.IsNullOrWhiteSpace(raw))
        {
            Debug.LogWarning("[Commender] AI 응답이 비어 있습니다.");
            return false;
        }

        try
        {
            string json = ExtractJsonObject(raw);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[Commender] 응답에서 JSON 객체를 찾지 못했습니다.");
                return false;
            }

            CommandGroup group = JsonUtility.FromJson<CommandGroup>(json);

            if (group == null || group.commands == null || group.commands.Count == 0)
            {
                Debug.LogWarning("[Commender] 명령 데이터가 비어 있습니다.");
                return false;
            }

            bool hasAnyHandledCommand = false;

            foreach (MoveCommand cmd in group.commands)
            {
                if (cmd == null)
                    continue;

                if (!agentById.TryGetValue(cmd.id, out AgentController targetAgent) || targetAgent == null)
                {
                    Debug.LogWarning($"[Commender] Agent ID {cmd.id} 를 찾지 못했습니다.");
                    continue;
                }

                Vector3 dest = BuildDestination(targetAgent, cmd);
                string originalInstruction = GetOriginalInstruction(cmd.id, submittedInstructionById);
                string validatedSkill = commandValidator.ValidateSkill(cmd.skill, originalInstruction);
                float validatedDelaySeconds = Mathf.Max(0f, cmd.delaySeconds);

                ScheduleCommand(targetAgent, dest, validatedSkill, validatedDelaySeconds);
                hasAnyHandledCommand = true;
            }

            return hasAnyHandledCommand;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commender] 파싱 오류: {e}");
            return false;
        }
    }

    private Vector3 BuildDestination(AgentController targetAgent, MoveCommand cmd)
    {
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

        return new Vector3(x, targetAgent.transform.position.y, z);
    }

    private string GetOriginalInstruction(int agentId, Dictionary<int, string> submittedInstructionById)
    {
        if (submittedInstructionById == null)
            return "";

        if (submittedInstructionById.TryGetValue(agentId, out string savedInstruction))
            return savedInstruction;

        return "";
    }

    private void ScheduleCommand(
        AgentController targetAgent,
        Vector3 dest,
        string validatedSkill,
        float delaySeconds)
    {
        if (targetAgent == null)
            return;

        int agentId = targetAgent.AgentID;

        CancelScheduledCommand(agentId, true);

        Coroutine routine = StartCoroutine(
            ExecuteScheduledCommandCoroutine(targetAgent, dest, validatedSkill, delaySeconds)
        );

        scheduledCommandByAgentId[agentId] = routine;

        if (delaySeconds <= 0f)
            return;

        if (validatedSkill == "lookaround")
        {
            Debug.Log($"[Commender] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 주변 둘러보기");
        }
        else if (validatedSkill == "hold")
        {
            Debug.Log($"[Commender] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 제자리 대기");
        }
        else if (!string.IsNullOrWhiteSpace(validatedSkill))
        {
            if (validatedSkill == "dash")
                Debug.Log($"[Commender] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 {dest} 로 dash 이동");
            else
                Debug.Log($"[Commender] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 {dest} 위치에 '{validatedSkill}' 스킬 사용");
        }
        else
        {
            Debug.Log($"[Commender] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 {dest} 로 이동");
        }
    }

    private IEnumerator ExecuteScheduledCommandCoroutine(
        AgentController targetAgent,
        Vector3 dest,
        string validatedSkill,
        float delaySeconds)
    {
        if (targetAgent == null)
            yield break;

        int agentId = targetAgent.AgentID;

        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        if (targetAgent == null)
        {
            scheduledCommandByAgentId.Remove(agentId);
            Debug.LogWarning($"[Commender] Agent {agentId} 예약 명령 실행 시점에 대상이 사라졌습니다.");
            yield break;
        }

        commandExecutor.Execute(targetAgent, dest, validatedSkill);
        scheduledCommandByAgentId.Remove(agentId);
    }

    private void CancelScheduledCommand(int agentId, bool logMessage)
    {
        if (!scheduledCommandByAgentId.TryGetValue(agentId, out Coroutine routine))
            return;

        if (routine != null)
            StopCoroutine(routine);

        scheduledCommandByAgentId.Remove(agentId);

        if (logMessage)
            Debug.Log($"[Commender] Agent {agentId} 기존 예약 명령을 취소했습니다.");
    }

    private void CancelAllScheduledCommandsInternal(bool logMessage)
    {
        if (scheduledCommandByAgentId.Count == 0)
            return;

        List<int> ids = new List<int>(scheduledCommandByAgentId.Keys);

        for (int i = 0; i < ids.Count; i++)
        {
            CancelScheduledCommand(ids[i], false);
        }

        scheduledCommandByAgentId.Clear();

        if (logMessage)
            Debug.Log("[Commender] 모든 예약 명령을 취소했습니다.");
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

    [Serializable]
    public class CommandGroup
    {
        public List<MoveCommand> commands;
    }

    [Serializable]
    public class MoveCommand
    {
        public int id;
        public float delaySeconds;
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