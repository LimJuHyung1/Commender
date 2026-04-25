using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CommanderCommandProcessor : MonoBehaviour
{
    [Header("Auto Bind")]
    [SerializeField] private bool autoBindAgentsFromScene = true;
    [SerializeField] private bool sortAgentsById = true;

    [Header("World Point Resolve")]
    [SerializeField] private AgentCameraFollow commandCamera;
    [SerializeField] private LayerMask commandGroundLayer;
    [SerializeField] private float coordinateMatchTolerance = 0.2f;
    [SerializeField] private float groundProbeHeight = 50f;
    [SerializeField] private float groundProbeDistance = 200f;

    private readonly List<AgentController> agents = new List<AgentController>();
    private readonly Dictionary<int, AgentController> agentById = new Dictionary<int, AgentController>();
    private readonly Dictionary<int, Coroutine> scheduledCommandByAgentId = new Dictionary<int, Coroutine>();

    private CommandValidator commandValidator;
    private CommandExecutor commandExecutor;

    [Serializable]
    public class CommandProcessResult
    {
        public List<int> SucceededAgentIds = new List<int>();
        public List<int> FailedAgentIds = new List<int>();

        public bool HasAnySuccess => SucceededAgentIds != null && SucceededAgentIds.Count > 0;
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

    private class PendingCommandPlan
    {
        public int AgentId;
        public AgentController Agent;
        public Vector3 Destination;
        public string Skill;
        public float DelaySeconds;
        public bool IsValid;
    }

    private void Awake()
    {
        EnsureHelpers();
        RefreshAgentsFromScene(true);

        if (commandCamera == null)
            commandCamera = FindFirstObjectByType<AgentCameraFollow>();
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

    public async Task<CommandProcessResult> ProcessCommandsFromUIAsync(CommanderUIController uiController)
    {
        CommandProcessResult result = new CommandProcessResult();

        if (uiController == null)
        {
            Debug.LogError("[Commender] CommenderUIController 참조가 없습니다.");
            return result;
        }

        RefreshAgentsFromScene(true);

        if (!uiController.TryBuildSubmittedInstructions(out Dictionary<int, string> submittedInstructionById))
        {
            Debug.LogWarning("[Commender] 제출할 입력값이 없습니다.");
            return result;
        }

        List<Task<PendingCommandPlan>> requestTasks = new List<Task<PendingCommandPlan>>();

        for (int i = 0; i < agents.Count; i++)
        {
            AgentController agent = agents[i];
            if (agent == null)
                continue;

            if (!submittedInstructionById.TryGetValue(agent.AgentID, out string instruction))
                continue;

            if (string.IsNullOrWhiteSpace(instruction))
                continue;

            requestTasks.Add(BuildPendingCommandPlanAsync(agent, instruction));
        }

        if (requestTasks.Count == 0)
        {
            Debug.LogWarning("[Commender] 처리할 에이전트 명령이 없습니다.");
            return result;
        }

        PendingCommandPlan[] plans = await Task.WhenAll(requestTasks);

        for (int i = 0; i < plans.Length; i++)
        {
            PendingCommandPlan plan = plans[i];
            if (plan == null)
                continue;

            if (!plan.IsValid || plan.Agent == null)
            {
                result.FailedAgentIds.Add(plan != null ? plan.AgentId : -1);
                continue;
            }

            ScheduleCommand(plan.Agent, plan.Destination, plan.Skill, plan.DelaySeconds);
            result.SucceededAgentIds.Add(plan.AgentId);
        }

        return result;
    }

    private async Task<PendingCommandPlan> BuildPendingCommandPlanAsync(AgentController targetAgent, string instruction)
    {
        PendingCommandPlan failedPlan = new PendingCommandPlan
        {
            AgentId = targetAgent != null ? targetAgent.AgentID : -1,
            Agent = targetAgent,
            IsValid = false
        };

        EnsureHelpers();

        if (targetAgent == null)
            return failedPlan;

        ChatServiceOpenAI chatService = targetAgent.CommandChatService;
        if (chatService == null)
        {
            Debug.LogError($"[Commender] Agent {targetAgent.AgentID} 의 ChatServiceOpenAI 참조가 없습니다.");
            return failedPlan;
        }

        try
        {
            string systemPrompt = GetSystemPromptForAgent(targetAgent);
            string userPrompt = BuildUserPrompt(targetAgent, instruction);

            string rawResponse = await chatService.GetOneShotAsync(systemPrompt, userPrompt);

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                Debug.LogWarning($"[Commender] Agent {targetAgent.AgentID} 의 AI 응답이 비어 있습니다.");
                return failedPlan;
            }

            if (TryBuildPendingCommandPlan(rawResponse, targetAgent, instruction, out PendingCommandPlan plan))
                return plan;

            return failedPlan;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commender] Agent {targetAgent.AgentID} 명령 생성 중 오류: {e}");
            return failedPlan;
        }
    }

    private bool TryBuildPendingCommandPlan(
        string raw,
        AgentController targetAgent,
        string originalInstruction,
        out PendingCommandPlan plan)
    {
        plan = new PendingCommandPlan
        {
            AgentId = targetAgent != null ? targetAgent.AgentID : -1,
            Agent = targetAgent,
            IsValid = false
        };

        EnsureHelpers();

        if (targetAgent == null)
            return false;

        Debug.Log($"[Commender] Agent {targetAgent.AgentID} AI 응답 데이터: {raw}");

        if (string.IsNullOrWhiteSpace(raw))
        {
            Debug.LogWarning($"[Commender] Agent {targetAgent.AgentID} AI 응답이 비어 있습니다.");
            return false;
        }

        try
        {
            string json = ExtractFirstJsonObject(raw);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[Commender] Agent {targetAgent.AgentID} 응답에서 JSON 객체를 찾지 못했습니다.");
                return false;
            }

            CommandGroup group = JsonUtility.FromJson<CommandGroup>(json);

            if (group == null || group.commands == null || group.commands.Count == 0)
            {
                Debug.LogWarning($"[Commender] Agent {targetAgent.AgentID} 명령 데이터가 비어 있습니다.");
                return false;
            }

            MoveCommand cmd = group.commands[0];
            if (cmd == null)
                return false;

            if (group.commands.Count > 1)
            {
                Debug.LogWarning($"[Commender] Agent {targetAgent.AgentID} 응답에 명령이 여러 개 들어왔습니다. 첫 번째 명령만 사용합니다.");
            }

            string validatedSkill = commandValidator.ValidateSkill(cmd.skill, originalInstruction);

            if (validatedSkill == "hold")
            {
                if (commandValidator.IsLookAroundInstruction(originalInstruction))
                    validatedSkill = "lookaround";
                else if (commandValidator.IsTrapInstruction(originalInstruction))
                    validatedSkill = "slowtrap";
                else if (commandValidator.IsMovementInstruction(originalInstruction))
                    validatedSkill = "";
            }

            Vector3 dest = ResolveDestination(targetAgent, cmd, originalInstruction, validatedSkill);
            float validatedDelaySeconds = Mathf.Max(0f, cmd.delaySeconds);

            plan = new PendingCommandPlan
            {
                AgentId = targetAgent.AgentID,
                Agent = targetAgent,
                Destination = dest,
                Skill = validatedSkill,
                DelaySeconds = validatedDelaySeconds,
                IsValid = true
            };

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commender] Agent {targetAgent.AgentID} 파싱 오류: {e}");
            return false;
        }
    }

    private string BuildUserPrompt(AgentController targetAgent, string instruction)
    {
        return $"Agent {targetAgent.AgentID} Instruction: {instruction}";
    }

    private string GetSystemPromptForAgent(AgentController targetAgent)
    {
        string commonRules =
            "You are a tactical coordinator for the game 'Commender'.\n\n" +
            "You will receive exactly one instruction for exactly one agent.\n" +
            $"The fixed agent id is {targetAgent.AgentID}.\n\n" +
            "RULES:\n" +
            $"1. Always output exactly one command with \"id\": {targetAgent.AgentID}.\n" +
            "2. Never output commands for any other agent id.\n" +
            "3. If the instruction says a time delay such as '5초 후', '5초 뒤', '5초 이후', 'after 5 seconds', or 'in 5 seconds', set \"delaySeconds\" to that value.\n" +
            "4. If no time delay is specified, set \"delaySeconds\": 0.0.\n" +
            "5. \"delaySeconds\" must never be negative.\n" +
            "6. If the instruction is movement only, skill MUST be an empty string.\n" +
            "7. If the instruction explicitly asks to check surroundings such as 주변, 주위, 주변 확인, 주위 확인, 주변 둘러봐, 주위 둘러봐, 주변 살펴봐, 주위 살펴봐, look around, or check around, use skill \"lookaround\".\n" +
            "8. Bare instructions like '주변' or '주위' should also be interpreted as \"lookaround\".\n" +
            "9. If the instruction is vague, unsupported, or outside the supported command set, use skill \"hold\".\n" +
            "10. When using \"lookaround\" or \"hold\", pos should be {\"x\":0.0,\"z\":0.0}.\n" +
            "11. If a skill is used without a location, set pos as {\"x\":0.0,\"z\":0.0} unless the skill is defined to use current position.\n" +
            "12. Output JSON only.\n";

        if (targetAgent is PursuerAgent)
        {
            return commonRules +
                   "13. Allowed skills for this agent are only \"dash\" and \"smoke\".\n" +
                   "14. Use \"dash\" ONLY when the instruction explicitly asks for dash, 대시, or 대쉬.\n" +
                   "15. Use \"smoke\" ONLY when the instruction explicitly asks for smoke, 연막, or 연막탄.\n" +
                   "16. Only dash may be combined with movement.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 5,5\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 5.0, \"z\": 5.0}}, \"skill\": \"\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 3,2로 대시\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 3.0, \"z\": 2.0}}, \"skill\": \"dash\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 5,4에 연막 사용\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 5.0, \"z\": 4.0}}, \"skill\": \"smoke\" }} ] }}";
        }

        if (targetAgent is ScoutAgent)
        {
            return commonRules +
                   "13. Allowed skills for this agent are only \"flare\", \"positionshare_on\", and \"positionshare_off\".\n" +
                   "14. Use \"flare\" ONLY when the instruction explicitly asks for flare, signal flare, 조명탄, 신호탄, or 플레어.\n" +
                   "15. Use \"positionshare_on\" ONLY when the instruction explicitly asks to enable position sharing, 위치 공유 켜, 위치 공유 시작, 위치 공유해, 타겟 위치 공유, or 타겟 위치 알려줘.\n" +
                   "16. Use \"positionshare_off\" ONLY when the instruction explicitly asks to disable position sharing, 위치 공유 꺼, 위치 공유 중지, 위치 공유하지 마, or 타겟 위치 공유하지 마.\n" +
                   "17. Do not use wallsight or truesight. Those skills are not supported anymore.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 8,3에 신호탄 발사\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 8.0, \"z\": 3.0}}, \"skill\": \"flare\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 위치 공유 꺼\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"positionshare_off\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 위치 공유 켜\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"positionshare_on\" }} ] }}";
        }

        if (targetAgent is EngineerAgent)
        {
            return commonRules +
                   "13. Allowed skills for this agent are only \"barricade\" and \"slowtrap\".\n" +
                   "14. Use \"barricade\" ONLY when the instruction explicitly asks for barricade, 바리케이드, 봉쇄, or 장애물 설치.\n" +
                   "15. Use \"slowtrap\" ONLY when the instruction explicitly asks for slowtrap, snaretrap, trap, 트랩, 함정, 정지 함정, 구속 함정, 속박 함정, 트랩 설치, or 함정 설치.\n" +
                   "16. If the instruction is just trap, 트랩, or 함정 without coordinates, interpret it as using the trap at the engineer's current position.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 4,1에 바리케이드 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 4.0, \"z\": 1.0}}, \"skill\": \"barricade\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 2,6에 함정 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 2.0, \"z\": 6.0}}, \"skill\": \"slowtrap\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 트랩\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"slowtrap\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 함정\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"slowtrap\" }} ] }}";
        }

        if (targetAgent is DisruptorAgent)
        {
            return commonRules +
                   "13. Allowed skills for this agent are only \"noisemaker\" and \"hologram\".\n" +
                   "14. Use \"noisemaker\" ONLY when the instruction explicitly asks for noisemaker, noise, 소란 장치, 소음 장치, 소음 발생기, 소란 기계, or 소란 장치 설치.\n" +
                   "15. Use \"hologram\" ONLY when the instruction explicitly asks for hologram, 홀로그램, 홀로그램 설치, 현재 위치에 홀로그램, or 자기 위치에 홀로그램.\n" +
                   "16. Hologram is always created at the disruptor agent's CURRENT POSITION, not at the requested coordinate.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 7,7에 소란 장치 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 7.0, \"z\": 7.0}}, \"skill\": \"noisemaker\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 현재 위치에 홀로그램 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"hologram\" }} ] }}";
        }

        return commonRules;
    }

    private Vector3 ResolveDestination(
        AgentController targetAgent,
        MoveCommand cmd,
        string originalInstruction,
        string validatedSkill)
    {
        if (targetAgent == null)
            return Vector3.zero;

        if (ShouldUseCurrentPositionWhenNoCoordinate(originalInstruction, validatedSkill))
        {
            Vector3 currentPosition = targetAgent.transform.position;
            Debug.Log($"[Commender] Agent {targetAgent.AgentID} 좌표 없는 함정 명령이므로 현재 위치 사용: {currentPosition}");
            return currentPosition;
        }

        if (ShouldUseInstructionCoordinate(originalInstruction, validatedSkill) &&
            commandValidator.TryExtractCoordinate(originalInstruction, out float parsedX, out float parsedZ))
        {
            Vector3 parsedDestination = ResolveWorldPointFromCoordinate(targetAgent, parsedX, parsedZ);
            Debug.Log($"[Commender] Agent {targetAgent.AgentID} 좌표를 원문에서 직접 사용: {parsedDestination}");
            return parsedDestination;
        }

        return BuildDestinationFromAI(targetAgent, cmd);
    }

    private bool ShouldUseCurrentPositionWhenNoCoordinate(string originalInstruction, string validatedSkill)
    {
        if (string.IsNullOrWhiteSpace(originalInstruction))
            return false;

        if (validatedSkill != "slowtrap")
            return false;

        if (commandValidator.ContainsCoordinate(originalInstruction))
            return false;

        return commandValidator.IsTrapInstruction(originalInstruction);
    }

    private bool ShouldUseInstructionCoordinate(string originalInstruction, string validatedSkill)
    {
        if (string.IsNullOrWhiteSpace(originalInstruction))
            return false;

        if (!commandValidator.ContainsCoordinate(originalInstruction))
            return false;

        if (validatedSkill == "hold")
            return false;

        if (validatedSkill == "lookaround")
            return false;

        if (validatedSkill == "hologram")
            return false;

        return true;
    }

    private Vector3 BuildDestinationFromAI(AgentController targetAgent, MoveCommand cmd)
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
            Debug.LogWarning($"[Commender] Agent {targetAgent.AgentID} 명령에 pos가 없습니다. 현재 위치 기준으로 처리합니다.");
        }

        return ResolveWorldPointFromCoordinate(targetAgent, x, z);
    }

    private Vector3 ResolveWorldPointFromCoordinate(AgentController targetAgent, float x, float z)
    {
        if (TryGetMatchedClickedGroundPoint(x, z, out Vector3 clickedPoint))
        {
            Debug.Log($"[Commender] 클릭 좌표 기반 월드 위치 사용: {clickedPoint}");
            return clickedPoint;
        }

        if (TryRaycastGroundPoint(targetAgent, x, z, out Vector3 raycastPoint))
        {
            Debug.Log($"[Commender] 레이캐스트 기반 월드 위치 사용: {raycastPoint}");
            return raycastPoint;
        }

        Vector3 fallback = new Vector3(x, targetAgent.transform.position.y, z);
        Debug.LogWarning($"[Commender] 좌표 ({x:F2}, {z:F2})의 높이를 찾지 못해 현재 Agent 높이로 대체합니다: {fallback}");
        return fallback;
    }

    private bool TryGetMatchedClickedGroundPoint(float x, float z, out Vector3 point)
    {
        point = default;

        if (CopiedCoordinateCache.TryGet(x, z, coordinateMatchTolerance, out Vector3 copiedPoint))
        {
            point = copiedPoint;
            Debug.Log($"[Commender] 복사 좌표 캐시 기반 월드 위치 사용: {point}");
            return true;
        }

        if (commandCamera == null)
            return false;

        if (!commandCamera.HasClickedGroundPoint)
            return false;

        Vector3 clicked = commandCamera.LastClickedGroundPoint;

        if (Mathf.Abs(clicked.x - x) > coordinateMatchTolerance)
            return false;

        if (Mathf.Abs(clicked.z - z) > coordinateMatchTolerance)
            return false;

        point = clicked;
        Debug.Log($"[Commender] 카메라 마지막 클릭 좌표 기반 월드 위치 사용: {point}");
        return true;
    }

    private bool TryRaycastGroundPoint(AgentController targetAgent, float x, float z, out Vector3 point)
    {
        point = default;

        if (commandGroundLayer.value == 0)
            return false;

        float agentY = targetAgent != null ? targetAgent.transform.position.y : 0f;
        float originY = Mathf.Max(agentY, 0f) + groundProbeHeight;
        Vector3 origin = new Vector3(x, originY, z);
        float distance = groundProbeHeight + groundProbeDistance;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, commandGroundLayer, QueryTriggerInteraction.Ignore))
            return false;

        point = hit.point;
        return true;
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

    private string ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        int start = text.IndexOf('{');
        if (start < 0)
            return "";

        int depth = 0;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;

                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }
        }

        return "";
    }
}