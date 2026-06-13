using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

public class CommanderCommandProcessor : MonoBehaviour
{
    private static readonly Regex CoordinateRegex =
        new Regex(@"(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private static readonly Regex DelaySecondsRegex =
        new Regex(
            @"(?:(\d+(?:\.\d+)?)\s*초\s*(?:후|뒤|이후)?|after\s+(\d+(?:\.\d+)?)\s+seconds?|in\s+(\d+(?:\.\d+)?)\s+seconds?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

    [Header("Auto Bind")]
    [SerializeField] private bool autoBindAgentsFromScene = true;
    [SerializeField] private bool sortAgentsById = true;

    [Header("World Point Resolve")]
    [SerializeField] private AgentCameraFollow commandCamera;
    [SerializeField] private LayerMask commandGroundLayer;
    [SerializeField] private float coordinateMatchTolerance = 0.2f;
    [SerializeField] private float groundProbeHeight = 50f;
    [SerializeField] private float groundProbeDistance = 200f;

    [Header("Waypoint Move")]
    [SerializeField] private float waypointReachDistance = 0.45f;
    [SerializeField] private float waypointStartGraceSeconds = 0.25f;

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
        public List<Vector3> Waypoints = new List<Vector3>();
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
            Debug.LogError("[Commander] CommanderUIController 참조가 없습니다.");
            return result;
        }

        RefreshAgentsFromScene(true);

        if (!uiController.TryBuildSubmittedInstructions(out Dictionary<int, string> submittedInstructionById))
        {
            Debug.LogWarning("[Commander] 제출할 입력값이 없습니다.");
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
            Debug.LogWarning("[Commander] 처리할 에이전트 명령이 없습니다.");
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

            ScheduleCommand(plan.Agent, plan.Destination, plan.Waypoints, plan.Skill, plan.DelaySeconds);
            result.SucceededAgentIds.Add(plan.AgentId);
        }

        return result;
    }

    private async Task<PendingCommandPlan> BuildPendingCommandPlanAsync(
        AgentController targetAgent,
        string instruction)
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

        if (TryBuildPatrolPlan(
                targetAgent,
                instruction,
                out PendingCommandPlan patrolPlan,
                out bool handledPatrolInstruction))
        {
            return patrolPlan;
        }

        if (handledPatrolInstruction)
            return failedPlan;

        if (TryBuildMultiCoordinateMovePlan(
                targetAgent,
                instruction,
                out PendingCommandPlan multiCoordinatePlan,
                out bool handledMultiCoordinateInstruction))
        {
            return multiCoordinatePlan;
        }

        if (handledMultiCoordinateInstruction)
            return failedPlan;

        ChatServiceOpenAI chatService = targetAgent.CommandChatService;

        if (chatService == null)
        {
            Debug.LogError($"[Commander] Agent {targetAgent.AgentID}의 ChatServiceOpenAI 참조가 없습니다.");
            return failedPlan;
        }

        try
        {
            string systemPrompt = GetSystemPromptForAgent(targetAgent);
            string userPrompt = BuildUserPrompt(targetAgent, instruction);

            string rawResponse = await chatService.GetOneShotAsync(systemPrompt, userPrompt);

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID}의 AI 응답이 비어 있습니다.");
                return failedPlan;
            }

            if (TryBuildPendingCommandPlan(rawResponse, targetAgent, instruction, out PendingCommandPlan plan))
                return plan;

            return failedPlan;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commander] Agent {targetAgent.AgentID} 명령 생성 중 오류: {e}");
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

        Debug.Log($"[Commander] Agent {targetAgent.AgentID} AI 응답 데이터: {raw}");

        if (string.IsNullOrWhiteSpace(raw))
        {
            Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID} AI 응답이 비어 있습니다.");
            return false;
        }

        try
        {
            string json = ExtractFirstJsonObject(raw);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID} 응답에서 JSON 객체를 찾지 못했습니다.");
                return false;
            }

            CommandGroup group = JsonUtility.FromJson<CommandGroup>(json);

            if (group == null || group.commands == null || group.commands.Count == 0)
            {
                Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID} 명령 데이터가 비어 있습니다.");
                return false;
            }

            MoveCommand cmd = group.commands[0];

            if (cmd == null)
                return false;

            if (group.commands.Count > 1)
            {
                Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID} 응답에 명령이 여러 개 들어왔습니다. 첫 번째 명령만 사용합니다.");
            }

            string validatedSkill = commandValidator.ValidateSkill(cmd.skill, originalInstruction);

            if (validatedSkill == "hold")
            {
                if (commandValidator.IsJokerCardInstruction(originalInstruction))
                {
                    Debug.Log("[Commander] 조커 카드는 마술사 게이지가 가득 차면 자동 발동되므로 직접 명령하지 않습니다.");
                    validatedSkill = "hold";
                }
                else if (commandValidator.IsGuardInstinctInstruction(originalInstruction))
                {
                    Debug.Log("[Commander] 경계 본능은 패시브 스킬이므로 직접 명령하지 않습니다.");
                    validatedSkill = "hold";
                }
                else if (TryResolveSkillFromAgentDefinitionInstruction(
                             targetAgent,
                             originalInstruction,
                             out string resolvedSkillFromDefinition))
                {
                    validatedSkill = resolvedSkillFromDefinition;
                }
                else if (commandValidator.IsDogDeployInstruction(originalInstruction))
                {
                    validatedSkill = "dogdeploy";
                }
                else if (commandValidator.IsTreatInstruction(originalInstruction))
                {
                    validatedSkill = "treat";
                }
                else if (commandValidator.IsOffLeashInstruction(originalInstruction))
                {
                    validatedSkill = "offleash";
                }
                else if (commandValidator.IsLookAroundInstruction(originalInstruction))
                {
                    validatedSkill = "lookaround";
                }
                else if (commandValidator.IsReconnaissanceInstruction(originalInstruction))
                {
                    validatedSkill = "reconnaissance";
                }
                else if (commandValidator.IsObservationSupportInstruction(originalInstruction))
                {
                    validatedSkill = "observationsupport";
                }
                else if (commandValidator.IsDemolitionInstruction(originalInstruction))
                {
                    validatedSkill = "demolition";
                }
                else if (commandValidator.IsSafeZoneInstruction(originalInstruction))
                {
                    validatedSkill = "safezone";
                }
                else if (commandValidator.IsStopSignalInstruction(originalInstruction))
                {
                    validatedSkill = "stopsignal";
                }
                else if (IsBarricadeInstruction(originalInstruction))
                {
                    validatedSkill = "barricade";
                }
                else if (commandValidator.IsFakeBoxInstruction(originalInstruction))
                {
                    validatedSkill = "fakebox";
                }
                else if (commandValidator.IsVanishingInstruction(originalInstruction))
                {
                    validatedSkill = "vanishing";
                }
                else if (commandValidator.IsMisdirectionInstruction(originalInstruction))
                {
                    validatedSkill = "misdirection";
                }
                else if (commandValidator.IsMovementInstruction(originalInstruction))
                {
                    validatedSkill = "";
                }
            }

            if (validatedSkill == "vanishing" && !commandValidator.ContainsCoordinate(originalInstruction))
            {
                Debug.LogWarning($"[Commander] 배니싱 명령에는 좌표가 필요합니다. 예: 10,5 배니싱 / 원문: {originalInstruction}");
                return false;
            }

            if (!IsSkillAllowedForAgent(targetAgent, validatedSkill))
            {
                Debug.LogWarning(
                    $"[Commander] Agent {targetAgent.AgentID}는 '{validatedSkill}' 스킬을 사용할 수 없습니다. " +
                    $"원문: {originalInstruction}"
                );

                return false;
            }

            Vector3 dest = ResolveDestination(targetAgent, cmd, originalInstruction, validatedSkill);
            List<Vector3> waypoints = BuildMovementWaypointsFromInstruction(targetAgent, originalInstruction, validatedSkill);

            if (waypoints.Count > 0)
                dest = waypoints[waypoints.Count - 1];

            float validatedDelaySeconds = Mathf.Max(0f, cmd.delaySeconds);

            plan = new PendingCommandPlan
            {
                AgentId = targetAgent.AgentID,
                Agent = targetAgent,
                Destination = dest,
                Waypoints = waypoints,
                Skill = validatedSkill,
                DelaySeconds = validatedDelaySeconds,
                IsValid = true
            };

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commander] Agent {targetAgent.AgentID} 파싱 오류: {e}");
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
        "You are a tactical coordinator for the game 'Commander'.\n\n" +
        "You will receive exactly one instruction for exactly one agent.\n" +
        $"The fixed agent id is {targetAgent.AgentID}.\n\n" +
        "RULES:\n" +
        $"1. Always output exactly one command with \"id\": {targetAgent.AgentID}.\n" +
        "2. Never output commands for any other agent id.\n" +
        "3. If the instruction says a time delay such as '5초 후', '5초 뒤', '5초 이후', 'after 5 seconds', or 'in 5 seconds', set \"delaySeconds\" to that value.\n" +
        "4. If no time delay is specified, set \"delaySeconds\": 0.0.\n" +
        "5. \"delaySeconds\" must never be negative.\n" +
        "6. If the instruction is movement only, skill MUST be an empty string.\n" +
        "7. If the instruction asks to check surroundings, use skill \"lookaround\".\n" +
        "8. If the instruction is vague, unsupported, or outside the supported command set, use skill \"hold\".\n" +
        "9. When using \"lookaround\" or \"hold\", pos should be {\"x\":0.0,\"z\":0.0}.\n" +
        "10. Output JSON only.\n";

        if (TryBuildSystemPromptFromAgentDefinition(targetAgent, commonRules, out string definitionPrompt))
            return definitionPrompt;

        if (targetAgent is Chaser)
        {
            return commonRules +
                   "11. Allowed skills for this agent are only \"accesscontrol\", \"escapeblock\", and \"patrol\".\n" +
                   "12. Use \"accesscontrol\" ONLY when the instruction explicitly asks for 출입 통제, 출입통제, 통제 구역, access control, or control zone.\n" +
                   "13. Use \"escapeblock\" ONLY when the instruction explicitly mentions 도주 제지, 도주제지, 도주 스킬 차단, escape block, or escape skill block.\n" +
                   "14. escapeblock is an automatic gauge-based skill. It does not turn on or off.\n" +
                   "15. patrol is handled directly by the game when the instruction contains two coordinates and a patrol keyword such as 순찰 or patrol.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 5,4에 출입 통제\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 5.0, \"z\": 4.0}}, \"skill\": \"accesscontrol\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 도주 제지\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"escapeblock\" }} ] }}";
        }

        if (targetAgent is Observer)
        {
            return commonRules +
                   "11. Allowed skills for this agent are only \"drone\", \"reconnaissance\", \"observationsupport\", \"positionshare_on\", and \"positionshare_off\".\n" +
                   "12. Use \"drone\" ONLY when the instruction explicitly asks for drone or 드론.\n" +
                   "13. Use \"reconnaissance\" ONLY when the instruction explicitly asks for 정찰, reconnaissance, recon, or scout.\n" +
                   "14. Use \"observationsupport\" ONLY when the instruction explicitly asks for 관측 지원, 시야 지원, observation support, or vision support.\n" +
                   "15. Use \"positionshare_on\" ONLY when the instruction explicitly asks to enable position sharing.\n" +
                   "16. Use \"positionshare_off\" ONLY when the instruction explicitly asks to disable position sharing.\n" +
                   "17. If the instruction asks for 정찰 without coordinates, use the agent current position so the skill can use the agent forward direction.\n" +
                   "18. If the instruction asks for 관측 지원 without coordinates, use the agent current position.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 10,5에 정찰\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 10.0, \"z\": 5.0}}, \"skill\": \"reconnaissance\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 관측 지원\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"observationsupport\" }} ] }}";
        }

        if (targetAgent is Engineer)
        {
            return commonRules +
                   "11. Allowed skills for this agent are only \"barricade\", \"stopsignal\", \"demolition\", and \"safezone\".\n" +
                   "12. Use \"barricade\" ONLY when the instruction explicitly asks for barricade, 바리케이드, 봉쇄, or 장애물 설치.\n" +
                   "13. Use \"stopsignal\" ONLY when the instruction explicitly asks for 정지 신호, 정지신호, 신호 설치, 통제 신호, stop signal, or stopsignal.\n" +
                   "14. Use \"demolition\" ONLY when the instruction explicitly asks for 철거, demolition, demolish, or remove obstacle.\n" +
                   "15. Use \"safezone\" ONLY when the instruction explicitly asks for 안전 구역, 안전구역, safe zone, or safezone.\n" +
                   "16. If the instruction is demolition without coordinates, use current position.\n" +
                   "17. If the instruction is safezone with coordinates, use the given coordinates.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 철거\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"demolition\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 10,5에 안전 구역\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 10.0, \"z\": 5.0}}, \"skill\": \"safezone\" }} ] }}";
        }

        if (targetAgent is Trickster)
        {
            return commonRules +
                   "11. This agent is the Magician-type Trickster agent.\n" +
                   "12. Allowed manually commanded skills for this agent are \"fakebox\", \"vanishing\", and \"misdirection\".\n" +
                   "13. Use \"fakebox\" ONLY when the instruction explicitly asks for fakebox, fake box, magic box, 페이크 박스, 페이크박스, 마술 상자, 마술상자, 가짜 상자, or 가짜상자.\n" +
                   "14. Use \"vanishing\" ONLY when the instruction explicitly asks for 배니싱, vanishing, or vanish. Vanishing requires coordinates.\n" +
                   "15. Use \"misdirection\" ONLY when the instruction explicitly asks for 미스디렉션, 미스 디렉션, or misdirection. Misdirection does not require coordinates, so use pos {\"x\":0.0,\"z\":0.0}.\n" +
                   "16. Joker Card is an automatic gauge-based skill. Never output \"jokercard\" as a command.\n" +
                   "17. If the user asks to use Joker Card, output skill \"hold\".\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 10,5에 페이크 박스\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 10.0, \"z\": 5.0}}, \"skill\": \"fakebox\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 10,5에 배니싱\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 10.0, \"z\": 5.0}}, \"skill\": \"vanishing\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 미스디렉션\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"misdirection\" }} ] }}";
        }

        if (targetAgent is DogHandler)
        {
            return commonRules +
                   "11. Allowed skills for this agent are only \"dogdeploy\", \"treat\", and \"offleash\".\n" +
                   "12. Use \"dogdeploy\" ONLY when the instruction explicitly asks for 탐지견 배치, 탐지견배치, 배치, dog deploy, detection dog deploy, or deploy dog. Dog deploy requires coordinates.\n" +
                   "13. Use \"treat\" ONLY when the instruction explicitly asks for 간식, 탐지견 간식, treat, or dog treat. Treat does not require coordinates, so use pos {\"x\":0.0,\"z\":0.0}.\n" +
                   "14. Use \"offleash\" ONLY when the instruction explicitly asks for 오프리쉬, offleash, off leash, or off-leash. Off leash does not require coordinates, so use pos {\"x\":0.0,\"z\":0.0}.\n" +
                   "15. Guard Instinct is a passive skill. Never output \"guardinstinct\" as a command.\n" +
                   "16. If the user asks to use Guard Instinct or 경계 본능, output skill \"hold\".\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 10,5에 탐지견 배치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 10.0, \"z\": 5.0}}, \"skill\": \"dogdeploy\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 간식\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"treat\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 오프리쉬\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"offleash\" }} ] }}";
        }

        return commonRules;
    }

    private bool IsSkillAllowedForAgent(AgentController agent, string skill)
    {
        if (agent == null)
            return false;

        if (string.IsNullOrWhiteSpace(skill))
            return true;

        skill = NormalizeSkillKey(skill);

        if (skill == "hold" || skill == "lookaround")
            return true;

        if (TryCheckSkillAllowedByAgentDefinition(agent, skill, out bool allowedByDefinition))
            return allowedByDefinition;

        if (agent is Chaser)
        {
            Chaser chaser = agent as Chaser;

            return skill == "accesscontrol" ||
                   skill == "escapeblock" ||
                   (skill == "patrol" && chaser != null && chaser.CanUsePatrolSkill);
        }

        if (agent is Observer)
        {
            return skill == "drone" ||
                   skill == "reconnaissance" ||
                   skill == "observationsupport" ||
                   skill == "positionshare_on" ||
                   skill == "positionshare_off";
        }

        if (agent is Engineer)
        {
            return skill == "barricade" ||
                   skill == "stopsignal" ||
                   skill == "demolition" ||
                   skill == "safezone";
        }

        if (agent is Trickster)
        {
            Trickster trickster = agent as Trickster;

            return skill == "fakebox" ||
                   (skill == "vanishing" && trickster != null && trickster.CanUseVanishingSkill) ||
                   (skill == "misdirection" && trickster != null && trickster.CanUseMisdirectionSkill);
        }

        if (agent is DogHandler)
        {
            DogHandler dogHandler = agent as DogHandler;

            return skill == "dogdeploy" ||
                   (skill == "treat" && dogHandler != null && dogHandler.IsTreatUnlocked) ||
                   (skill == "offleash" && dogHandler != null && dogHandler.IsOffLeashUnlocked);
        }

        return true;
    }

    private bool TryBuildSystemPromptFromAgentDefinition(
    AgentController targetAgent,
    string commonRules,
    out string prompt)
    {
        prompt = "";

        if (targetAgent == null)
            return false;

        AgentDefinitionSO definition = targetAgent.AgentDefinition;

        if (definition == null)
            return false;

        List<SkillDefinitionSO> commandableSkills = GetAvailableCommandableSkills(targetAgent);

        if (commandableSkills.Count <= 0)
            return false;

        StringBuilder builder = new StringBuilder();

        builder.Append(commonRules);
        builder.AppendLine();
        builder.AppendLine($"AGENT:");
        builder.AppendLine($"- Agent display name: {targetAgent.AgentDisplayName}");
        builder.AppendLine($"- Agent id key: {targetAgent.AgentId}");
        builder.AppendLine();
        builder.AppendLine("AGENT SKILL RULES:");
        builder.AppendLine("11. Allowed skills for this agent are only the skills listed below.");
        builder.AppendLine("12. Use a skill only when the instruction explicitly matches the skill name, command keyword, or alternative keyword.");
        builder.AppendLine("13. If the instruction does not clearly match one of the listed skills, use skill \"hold\".");
        builder.AppendLine("14. Passive or automatic skills must not be output as direct commands.");
        builder.AppendLine();

        builder.Append("Allowed skill output values: ");

        List<string> allOutputKeys = new List<string>();

        for (int i = 0; i < commandableSkills.Count; i++)
        {
            AddRuntimeKeysFromSkill(commandableSkills[i], allOutputKeys);
        }

        builder.AppendLine(BuildQuotedList(allOutputKeys));
        builder.AppendLine();

        builder.AppendLine("SKILL GUIDE:");

        for (int i = 0; i < commandableSkills.Count; i++)
        {
            SkillDefinitionSO skill = commandableSkills[i];

            if (skill == null)
                continue;

            List<string> outputKeys = GetRuntimeKeysFromSkill(skill);
            List<string> keywords = GetKeywordsFromSkill(skill);

            builder.AppendLine($"- {GetSafeSkillDisplayName(skill)}");
            builder.AppendLine($"  Output skill value: {BuildQuotedList(outputKeys)}");

            if (keywords.Count > 0)
                builder.AppendLine($"  Use when instruction mentions: {BuildQuotedList(keywords)}");

            if (!string.IsNullOrWhiteSpace(skill.UsageText))
                builder.AppendLine($"  Usage: {skill.UsageText}");

            builder.AppendLine();
        }

        builder.AppendLine("OUTPUT FORMAT:");
        builder.AppendLine("{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }");
        builder.AppendLine();

        builder.AppendLine("POSITION RULES:");
        builder.AppendLine("- If a skill instruction contains coordinates, use those coordinates in pos.");
        builder.AppendLine("- If a skill does not need coordinates, use pos {\"x\":0.0,\"z\":0.0}.");
        builder.AppendLine("- If a skill needs coordinates but the instruction has no coordinates, use skill \"hold\".");
        builder.AppendLine("- If the instruction is movement only, use an empty skill value.");

        prompt = builder.ToString();
        return true;
    }

    private List<SkillDefinitionSO> GetAvailableCommandableSkills(AgentController agent)
    {
        List<SkillDefinitionSO> result = new List<SkillDefinitionSO>();

        if (agent == null)
            return result;

        AgentDefinitionSO definition = agent.AgentDefinition;

        if (definition == null)
            return result;

        AddAvailableCommandableSkills(agent, definition.BasicSkills, result);
        AddAvailableCommandableSkills(agent, definition.UnlockableSkills, result);

        return result;
    }

    private void AddAvailableCommandableSkills(
        AgentController agent,
        IReadOnlyList<SkillDefinitionSO> source,
        List<SkillDefinitionSO> result)
    {
        if (agent == null || source == null || result == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            SkillDefinitionSO skill = source[i];

            if (skill == null)
                continue;

            if (!IsSkillAvailableForCommand(agent, skill))
                continue;

            if (result.Contains(skill))
                continue;

            result.Add(skill);
        }
    }

    private bool IsSkillAvailableForCommand(AgentController agent, SkillDefinitionSO skill)
    {
        if (agent == null || skill == null)
            return false;

        if (!skill.IsCommandSkill && !skill.IsToggleSkill)
            return false;

        if (skill.IsBasicSkill)
            return true;

        if (!skill.IsUnlockableSkill)
            return true;

        if (!skill.HasUnlockUpgradeId)
            return false;

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return false;

        return upgradeManager.HasAgentUpgrade(skill.UnlockUpgradeId);
    }

    private bool TryCheckSkillAllowedByAgentDefinition(
        AgentController agent,
        string skill,
        out bool allowed)
    {
        allowed = false;

        if (agent == null)
            return false;

        AgentDefinitionSO definition = agent.AgentDefinition;

        if (definition == null)
            return false;

        List<SkillDefinitionSO> commandableSkills = GetAvailableCommandableSkills(agent);

        if (commandableSkills.Count <= 0)
            return false;

        string normalizedSkill = NormalizeSkillKey(skill);

        for (int i = 0; i < commandableSkills.Count; i++)
        {
            SkillDefinitionSO skillDefinition = commandableSkills[i];

            if (skillDefinition == null)
                continue;

            if (SkillMatchesRuntimeOutput(skillDefinition, normalizedSkill))
            {
                allowed = true;
                return true;
            }
        }

        allowed = false;
        return true;
    }

    private bool TryResolveSkillFromAgentDefinitionInstruction(
        AgentController agent,
        string instruction,
        out string runtimeSkillKey)
    {
        runtimeSkillKey = "";

        if (agent == null)
            return false;

        if (string.IsNullOrWhiteSpace(instruction))
            return false;

        List<SkillDefinitionSO> commandableSkills = GetAvailableCommandableSkills(agent);

        if (commandableSkills.Count <= 0)
            return false;

        for (int i = 0; i < commandableSkills.Count; i++)
        {
            SkillDefinitionSO skill = commandableSkills[i];

            if (skill == null)
                continue;

            if (!InstructionMatchesSkillKeyword(instruction, skill))
                continue;

            runtimeSkillKey = GetPrimaryRuntimeKeyFromSkill(skill);

            if (string.IsNullOrWhiteSpace(runtimeSkillKey))
                continue;

            return true;
        }

        return false;
    }

    private bool InstructionMatchesSkillKeyword(string instruction, SkillDefinitionSO skill)
    {
        if (string.IsNullOrWhiteSpace(instruction) || skill == null)
            return false;

        List<string> keywords = GetKeywordsFromSkill(skill);

        for (int i = 0; i < keywords.Count; i++)
        {
            if (ContainsKeyword(instruction, keywords[i]))
                return true;
        }

        return false;
    }

    private bool SkillMatchesRuntimeOutput(SkillDefinitionSO skill, string runtimeOutput)
    {
        if (skill == null)
            return false;

        if (string.IsNullOrWhiteSpace(runtimeOutput))
            return false;

        string normalizedRuntimeOutput = NormalizeSkillKey(runtimeOutput);

        if (NormalizeSkillKey(skill.RuntimeSkillKey) == normalizedRuntimeOutput)
            return true;

        if (NormalizeSkillKey(skill.SkillId) == normalizedRuntimeOutput)
            return true;

        if (NormalizeSkillKey(skill.CommandKeyword) == normalizedRuntimeOutput)
            return true;

        IReadOnlyList<string> aliases = skill.RuntimeSkillAliases;

        if (aliases != null)
        {
            for (int i = 0; i < aliases.Count; i++)
            {
                if (NormalizeSkillKey(aliases[i]) == normalizedRuntimeOutput)
                    return true;
            }
        }

        return false;
    }

    private string GetPrimaryRuntimeKeyFromSkill(SkillDefinitionSO skill)
    {
        if (skill == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skill.RuntimeSkillKey))
            return NormalizeSkillKey(skill.RuntimeSkillKey);

        if (!string.IsNullOrWhiteSpace(skill.SkillId))
            return NormalizeSkillKey(skill.SkillId);

        if (!string.IsNullOrWhiteSpace(skill.CommandKeyword))
            return NormalizeSkillKey(skill.CommandKeyword);

        return "";
    }

    private List<string> GetRuntimeKeysFromSkill(SkillDefinitionSO skill)
    {
        List<string> result = new List<string>();

        AddRuntimeKeysFromSkill(skill, result);

        return result;
    }

    private void AddRuntimeKeysFromSkill(SkillDefinitionSO skill, List<string> result)
    {
        if (skill == null || result == null)
            return;

        AddUniqueNormalizedValue(result, skill.RuntimeSkillKey);
        AddUniqueNormalizedValue(result, skill.SkillId);

        IReadOnlyList<string> aliases = skill.RuntimeSkillAliases;

        if (aliases == null)
            return;

        for (int i = 0; i < aliases.Count; i++)
        {
            AddUniqueNormalizedValue(result, aliases[i]);
        }
    }

    private List<string> GetKeywordsFromSkill(SkillDefinitionSO skill)
    {
        List<string> result = new List<string>();

        if (skill == null)
            return result;

        AddUniqueKeywordValue(result, skill.DisplayName);
        AddUniqueKeywordValue(result, skill.CommandKeyword);
        AddUniqueKeywordValue(result, skill.SkillId);
        AddUniqueKeywordValue(result, skill.RuntimeSkillKey);

        IReadOnlyList<string> alternativeKeywords = skill.AlternativeCommandKeywords;

        if (alternativeKeywords != null)
        {
            for (int i = 0; i < alternativeKeywords.Count; i++)
            {
                AddUniqueKeywordValue(result, alternativeKeywords[i]);
            }
        }

        IReadOnlyList<string> aliases = skill.RuntimeSkillAliases;

        if (aliases != null)
        {
            for (int i = 0; i < aliases.Count; i++)
            {
                AddUniqueKeywordValue(result, aliases[i]);
            }
        }

        return result;
    }

    private string GetSafeSkillDisplayName(SkillDefinitionSO skill)
    {
        if (skill == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skill.DisplayName))
            return skill.DisplayName;

        if (!string.IsNullOrWhiteSpace(skill.SkillId))
            return skill.SkillId;

        return skill.name;
    }

    private bool ContainsKeyword(string source, string keyword)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        if (string.IsNullOrWhiteSpace(keyword))
            return false;

        string normalizedSource = NormalizeKeywordForContains(source);
        string normalizedKeyword = NormalizeKeywordForContains(keyword);

        if (normalizedSource.Contains(normalizedKeyword))
            return true;

        string compactSource = normalizedSource.Replace(" ", "");
        string compactKeyword = normalizedKeyword.Replace(" ", "");

        return compactSource.Contains(compactKeyword);
    }

    private string NormalizeKeywordForContains(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");
    }

    private string NormalizeSkillKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private void AddUniqueNormalizedValue(List<string> values, string value)
    {
        if (values == null)
            return;

        string normalizedValue = NormalizeSkillKey(value);

        if (string.IsNullOrWhiteSpace(normalizedValue))
            return;

        if (values.Contains(normalizedValue))
            return;

        values.Add(normalizedValue);
    }

    private void AddUniqueKeywordValue(List<string> values, string value)
    {
        if (values == null)
            return;

        if (string.IsNullOrWhiteSpace(value))
            return;

        string trimmedValue = value.Trim();

        for (int i = 0; i < values.Count; i++)
        {
            if (NormalizeKeywordForContains(values[i]) == NormalizeKeywordForContains(trimmedValue))
                return;
        }

        values.Add(trimmedValue);
    }

    private string BuildQuotedList(List<string> values)
    {
        if (values == null || values.Count <= 0)
            return "\"\"";

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append("\"");
            builder.Append(values[i]);
            builder.Append("\"");
        }

        return builder.ToString();
    }

    private bool IsBarricadeInstruction(string source)
    {
        if (commandValidator == null)
            commandValidator = new CommandValidator();

        return commandValidator.IsBarricadeInstruction(source);
    }

    private bool TryBuildPatrolPlan(
        AgentController targetAgent,
        string instruction,
        out PendingCommandPlan plan,
        out bool handledPatrolInstruction)
    {
        plan = null;
        handledPatrolInstruction = false;

        if (targetAgent == null)
            return false;

        if (!commandValidator.IsPatrolInstruction(instruction))
            return false;

        handledPatrolInstruction = true;

        Chaser chaser = targetAgent as Chaser;

        if (chaser == null)
        {
            Debug.LogWarning($"[Commander] 순찰 명령은 보안 요원만 사용할 수 있습니다. 원문: {instruction}");
            return false;
        }

        if (!chaser.CanUsePatrolSkill)
        {
            Debug.LogWarning($"[Commander] 보안 요원이 아직 순찰 스킬을 배우지 않았습니다. 원문: {instruction}");
            return false;
        }

        if (!TryExtractCoordinates(instruction, out List<Vector2> coordinates) || coordinates.Count < 2)
        {
            Debug.LogWarning($"[Commander] 순찰 명령에는 좌표가 2개 필요합니다. 예: (1,2), (5,8) 순찰 / 원문: {instruction}");
            return false;
        }

        if (coordinates.Count > 2)
        {
            Debug.LogWarning($"[Commander] 순찰 명령에 좌표가 3개 이상 포함되어 있습니다. 앞의 2개 좌표만 사용합니다. 원문: {instruction}");
        }

        Vector3 firstPoint = ResolveWorldPointFromCoordinate(
            targetAgent,
            coordinates[0].x,
            coordinates[0].y
        );

        Vector3 secondPoint = ResolveWorldPointFromCoordinate(
            targetAgent,
            coordinates[1].x,
            coordinates[1].y
        );

        List<Vector3> waypoints = new List<Vector3>
        {
            firstPoint,
            secondPoint
        };

        float delaySeconds = ExtractDelaySeconds(instruction);

        plan = new PendingCommandPlan
        {
            AgentId = targetAgent.AgentID,
            Agent = targetAgent,
            Destination = secondPoint,
            Waypoints = waypoints,
            Skill = "patrol",
            DelaySeconds = delaySeconds,
            IsValid = true
        };

        Debug.Log(
            $"[Commander] Agent {targetAgent.AgentID} 순찰 명령 직접 처리. " +
            $"A={firstPoint}, B={secondPoint}, delay={delaySeconds:0.##}"
        );

        return true;
    }

    private bool TryBuildMultiCoordinateMovePlan(
        AgentController targetAgent,
        string instruction,
        out PendingCommandPlan plan,
        out bool handledMultiCoordinateInstruction)
    {
        plan = null;
        handledMultiCoordinateInstruction = false;

        if (targetAgent == null)
            return false;

        if (!TryExtractCoordinates(instruction, out List<Vector2> coordinates))
            return false;

        if (coordinates.Count < 2)
            return false;

        handledMultiCoordinateInstruction = true;

        if (ContainsSkillKeywordInMultiCoordinateInstruction(instruction))
        {
            Debug.LogWarning(
                $"[Commander] 두 개 이상의 좌표가 포함된 명령에는 스킬을 함께 사용할 수 없습니다. " +
                $"해당 명령을 무효 처리합니다. 원문: {instruction}"
            );

            return false;
        }

        List<Vector3> waypoints = new List<Vector3>();

        for (int i = 0; i < coordinates.Count; i++)
        {
            Vector2 coordinate = coordinates[i];
            Vector3 worldPoint = ResolveWorldPointFromCoordinate(targetAgent, coordinate.x, coordinate.y);
            waypoints.Add(worldPoint);
        }

        if (waypoints.Count < 2)
        {
            Debug.LogWarning($"[Commander] 경유 이동 좌표 생성에 실패했습니다. 원문: {instruction}");
            return false;
        }

        float delaySeconds = ExtractDelaySeconds(instruction);

        plan = new PendingCommandPlan
        {
            AgentId = targetAgent.AgentID,
            Agent = targetAgent,
            Destination = waypoints[waypoints.Count - 1],
            Waypoints = waypoints,
            Skill = "",
            DelaySeconds = delaySeconds,
            IsValid = true
        };

        Debug.Log(
            $"[Commander] Agent {targetAgent.AgentID} 두 좌표 이상 경유 이동 직접 처리. " +
            $"경유지 수: {waypoints.Count}, delay: {delaySeconds:0.##}"
        );

        return true;
    }

    private float ExtractDelaySeconds(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return 0f;

        Match match = DelaySecondsRegex.Match(source);

        if (!match.Success)
            return 0f;

        for (int i = 1; i < match.Groups.Count; i++)
        {
            Group group = match.Groups[i];

            if (group == null || !group.Success)
                continue;

            if (float.TryParse(
                    group.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float seconds))
            {
                return Mathf.Max(0f, seconds);
            }
        }

        return 0f;
    }

    private bool ContainsSkillKeywordInMultiCoordinateInstruction(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        if (commandValidator.IsLookAroundInstruction(source))
            return true;

        if (commandValidator.IsPatrolInstruction(source))
            return true;

        if (commandValidator.IsBarricadeInstruction(source))
            return true;

        if (commandValidator.IsStopSignalInstruction(source))
            return true;

        if (commandValidator.IsDemolitionInstruction(source))
            return true;

        if (commandValidator.IsSafeZoneInstruction(source))
            return true;

        if (commandValidator.IsFakeBoxInstruction(source))
            return true;

        if (commandValidator.IsReconnaissanceInstruction(source))
            return true;

        if (commandValidator.IsObservationSupportInstruction(source))
            return true;

        if (commandValidator.IsJokerCardInstruction(source))
            return true;

        if (commandValidator.IsVanishingInstruction(source))
            return true;

        if (commandValidator.IsMisdirectionInstruction(source))
            return true;

        if (commandValidator.IsDogDeployInstruction(source))
            return true;

        if (commandValidator.IsGuardInstinctInstruction(source))
            return true;

        if (commandValidator.IsTreatInstruction(source))
            return true;

        if (commandValidator.IsOffLeashInstruction(source))
            return true;

        string normalized = source.Trim().ToLower();

        return ContainsAnyKeyword(
            normalized,

            "accesscontrol",
            "access control",
            "control zone",
            "security zone",
            "restricted zone",
            "출입 통제",
            "출입통제",
            "통제 구역",
            "통제구역",
            "접근 금지",
            "접근금지",
            "제한 구역",
            "제한구역",
            "금지 구역",
            "금지구역",

            "escapeblock",
            "escape block",
            "escape skill block",
            "escape blocking",
            "block escape",
            "도주 제지",
            "도주제지",
            "도주 스킬 차단",
            "도주스킬차단",
            "도주 차단",
            "도주차단",
            "탈출 차단",
            "탈출차단",

            "drone",
            "uav",
            "드론",

            "reconnaissance",
            "recon",
            "scout",
            "정찰",
            "정찰 드론",
            "드론 정찰",

            "observationsupport",
            "observation support",
            "vision support",
            "sight support",
            "관측 지원",
            "관측지원",
            "시야 지원",
            "시야지원",

            "positionshare",
            "position share",
            "target position share",
            "share target position",
            "위치 공유",
            "위치공유",
            "타겟 위치 공유",
            "타겟위치공유",
            "타겟 위치 알려",
            "발견하면 알려",
            "보이면 알려",

            "barricade",
            "바리케이드",
            "봉쇄",
            "장애물",
            "장애물 설치",
            "길막",
            "길 막",
            "막아",
            "막기",

            "stopsignal",
            "stop signal",
            "stop sign",
            "stop signal device",
            "slowtrap",
            "slow trap",
            "snaretrap",
            "정지 신호",
            "정지신호",
            "정지 표지",
            "정지표지",
            "정지 장치",
            "정지장치",
            "신호 설치",
            "신호설치",
            "통제 신호",
            "통제신호",
            "멈춤 신호",
            "멈춤신호",
            "감속 함정",
            "감속함정",
            "구속 함정",
            "구속함정",
            "함정",

            "demolition",
            "demolish",
            "remove obstacle",
            "remove obstacles",
            "철거",
            "장애물 철거",

            "safezone",
            "safe zone",
            "safe_zone",
            "안전 구역",
            "안전구역",

            "fakebox",
            "fake box",
            "magicbox",
            "magic box",
            "페이크 박스",
            "페이크박스",
            "마술 상자",
            "마술상자",
            "가짜 상자",
            "가짜상자",

            "patrol",
            "patrolling",
            "patrol route",
            "route patrol",
            "순찰",
            "왕복 순찰",
            "왕복순찰",

            "jokercard",
            "joker card",
            "조커 카드",
            "조커카드",

            "vanishing",
"vanish",
"배니싱",

"misdirection",
"mis direction",
"미스디렉션",
"미스 디렉션",

"dogdeploy",
"dog_deploy",
"dog deploy",
"detectiondogdeploy",
"detection dog deploy",
"deploy dog",
"탐지견 배치",
"탐지견배치",
"탐지견",
"배치",

"guardinstinct",
"guard_instinct",
"guard instinct",
"dog guard instinct",
"경계 본능",
"경계본능",

"treat",
"dog treat",
"dog_treat",
"간식",
"탐지견 간식",
"탐지견간식",

"offleash",
"off_leash",
"off leash",
"off-leash",
"오프리쉬"
        );
    }

    private bool ContainsAnyKeyword(string source, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];

            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (source.Contains(keyword))
                return true;
        }

        return false;
    }

    private List<Vector3> BuildMovementWaypointsFromInstruction(
        AgentController targetAgent,
        string originalInstruction,
        string validatedSkill)
    {
        List<Vector3> waypoints = new List<Vector3>();

        if (targetAgent == null)
            return waypoints;

        if (!string.IsNullOrWhiteSpace(validatedSkill))
            return waypoints;

        if (!TryExtractCoordinates(originalInstruction, out List<Vector2> coordinates))
            return waypoints;

        if (coordinates.Count < 2)
            return waypoints;

        if (ContainsSkillKeywordInMultiCoordinateInstruction(originalInstruction))
            return waypoints;

        for (int i = 0; i < coordinates.Count; i++)
        {
            Vector2 coordinate = coordinates[i];
            Vector3 worldPoint = ResolveWorldPointFromCoordinate(targetAgent, coordinate.x, coordinate.y);
            waypoints.Add(worldPoint);
        }

        Debug.Log($"[Commander] Agent {targetAgent.AgentID} 경유 이동 좌표 {waypoints.Count}개 생성");

        return waypoints;
    }

    private bool TryExtractCoordinates(string source, out List<Vector2> coordinates)
    {
        coordinates = new List<Vector2>();

        if (string.IsNullOrWhiteSpace(source))
            return false;

        MatchCollection matches = CoordinateRegex.Matches(source);

        if (matches == null || matches.Count == 0)
            return false;

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];

            if (match == null || !match.Success)
                continue;

            bool parsedX = float.TryParse(
                match.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float x
            );

            bool parsedZ = float.TryParse(
                match.Groups[2].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float z
            );

            if (!parsedX || !parsedZ)
                continue;

            coordinates.Add(new Vector2(x, z));
        }

        return coordinates.Count > 0;
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
            Debug.Log($"[Commander] Agent {targetAgent.AgentID} 좌표 없는 현재 위치 스킬이므로 현재 위치 사용: {currentPosition}");
            return currentPosition;
        }

        if (ShouldUseInstructionCoordinate(originalInstruction, validatedSkill) &&
            commandValidator.TryExtractCoordinate(originalInstruction, out float parsedX, out float parsedZ))
        {
            Vector3 parsedDestination = ResolveWorldPointFromCoordinate(targetAgent, parsedX, parsedZ);
            Debug.Log($"[Commander] Agent {targetAgent.AgentID} 좌표를 원문에서 직접 사용: {parsedDestination}");
            return parsedDestination;
        }

        return BuildDestinationFromAI(targetAgent, cmd);
    }

    private bool ShouldUseCurrentPositionWhenNoCoordinate(string originalInstruction, string validatedSkill)
    {
        if (string.IsNullOrWhiteSpace(originalInstruction))
            return false;

        if (commandValidator.ContainsCoordinate(originalInstruction))
            return false;

        if (validatedSkill == "demolition")
            return commandValidator.IsDemolitionInstruction(originalInstruction);

        if (validatedSkill == "safezone")
            return commandValidator.IsSafeZoneInstruction(originalInstruction);

        if (validatedSkill == "stopsignal")
            return commandValidator.IsStopSignalInstruction(originalInstruction);

        if (validatedSkill == "barricade")
            return commandValidator.IsBarricadeInstruction(originalInstruction);

        if (validatedSkill == "fakebox")
            return commandValidator.IsFakeBoxInstruction(originalInstruction);

        if (validatedSkill == "reconnaissance")
            return commandValidator.IsReconnaissanceInstruction(originalInstruction);

        if (validatedSkill == "observationsupport")
            return commandValidator.IsObservationSupportInstruction(originalInstruction);

        if (validatedSkill == "treat")
            return commandValidator.IsTreatInstruction(originalInstruction);

        if (validatedSkill == "offleash")
            return commandValidator.IsOffLeashInstruction(originalInstruction);

        return false;
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
            Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID} 명령에 pos가 없습니다. 현재 위치 기준으로 처리합니다.");
        }

        return ResolveWorldPointFromCoordinate(targetAgent, x, z);
    }

    private Vector3 ResolveWorldPointFromCoordinate(AgentController targetAgent, float x, float z)
    {
        if (TryGetMatchedClickedGroundPoint(x, z, out Vector3 clickedPoint))
        {
            Debug.Log($"[Commander] 클릭 좌표 기반 월드 위치 사용: {clickedPoint}");
            return clickedPoint;
        }

        if (TryRaycastGroundPoint(targetAgent, x, z, out Vector3 raycastPoint))
        {
            Debug.Log($"[Commander] 레이캐스트 기반 월드 위치 사용: {raycastPoint}");
            return raycastPoint;
        }

        Vector3 fallback = new Vector3(x, targetAgent.transform.position.y, z);
        Debug.LogWarning($"[Commander] 좌표 ({x:F2}, {z:F2})의 높이를 찾지 못해 현재 Agent 높이로 대체합니다: {fallback}");
        return fallback;
    }

    private bool TryGetMatchedClickedGroundPoint(float x, float z, out Vector3 point)
    {
        point = default;

        if (CopiedCoordinateCache.TryGet(x, z, coordinateMatchTolerance, out Vector3 copiedPoint))
        {
            point = copiedPoint;
            Debug.Log($"[Commander] 복사 좌표 캐시 기반 월드 위치 사용: {point}");
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
        Debug.Log($"[Commander] 카메라 마지막 클릭 좌표 기반 월드 위치 사용: {point}");
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
        List<Vector3> waypoints,
        string validatedSkill,
        float delaySeconds)
    {
        if (targetAgent == null)
            return;

        int agentId = targetAgent.AgentID;

        CancelScheduledCommand(agentId, true);

        Coroutine routine = StartCoroutine(
            ExecuteScheduledCommandCoroutine(targetAgent, dest, waypoints, validatedSkill, delaySeconds)
        );

        scheduledCommandByAgentId[agentId] = routine;

        if (delaySeconds <= 0f)
            return;

        if (IsPatrolCommand(validatedSkill, waypoints))
        {
            Debug.Log($"[Commander] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 순찰 시작");
        }
        else if (IsWaypointMoveCommand(validatedSkill, waypoints))
        {
            Debug.Log($"[Commander] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 경유지 {waypoints.Count}개 순차 이동");
        }
        else if (validatedSkill == "lookaround")
        {
            Debug.Log($"[Commander] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 주변 둘러보기");
        }
        else if (validatedSkill == "hold")
        {
            Debug.Log($"[Commander] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 제자리 대기");
        }
        else if (!string.IsNullOrWhiteSpace(validatedSkill))
        {
            Debug.Log($"[Commander] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 {dest} 위치에 '{validatedSkill}' 스킬 사용");
        }
        else
        {
            Debug.Log($"[Commander] Agent {agentId} 예약 등록: {delaySeconds:0.##}초 후 {dest}로 이동");
        }
    }

    private IEnumerator ExecuteScheduledCommandCoroutine(
        AgentController targetAgent,
        Vector3 dest,
        List<Vector3> waypoints,
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
            Debug.LogWarning($"[Commander] Agent {agentId} 예약 명령 실행 시점에 대상이 사라졌습니다.");
            yield break;
        }

        if (IsPatrolCommand(validatedSkill, waypoints))
        {
            ExecutePatrolCommand(targetAgent, waypoints);
            scheduledCommandByAgentId.Remove(agentId);
            yield break;
        }

        if (IsWaypointMoveCommand(validatedSkill, waypoints))
        {
            yield return ExecuteWaypointMoveCoroutine(targetAgent, waypoints);
            scheduledCommandByAgentId.Remove(agentId);
            yield break;
        }

        commandExecutor.Execute(targetAgent, dest, validatedSkill);
        scheduledCommandByAgentId.Remove(agentId);
    }

    private void ExecutePatrolCommand(
        AgentController targetAgent,
        List<Vector3> waypoints)
    {
        if (targetAgent == null || waypoints == null || waypoints.Count < 2)
            return;

        Chaser chaser = targetAgent as Chaser;

        if (chaser == null)
        {
            Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID}는 순찰 명령을 실행할 수 없습니다.");
            return;
        }

        bool started = chaser.TryStartPatrol(waypoints[0], waypoints[1]);

        if (!started)
        {
            Debug.LogWarning($"[Commander] Agent {targetAgent.AgentID} 순찰 시작에 실패했습니다.");
            return;
        }

        Debug.Log($"[Commander] Agent {targetAgent.AgentID} 순찰 시작: {waypoints[0]} <-> {waypoints[1]}");
    }

    private IEnumerator ExecuteWaypointMoveCoroutine(
        AgentController targetAgent,
        List<Vector3> waypoints)
    {
        if (targetAgent == null || waypoints == null || waypoints.Count == 0)
            yield break;

        int agentId = targetAgent.AgentID;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (targetAgent == null)
                yield break;

            if (targetAgent.IsChasing)
            {
                Debug.LogWarning($"[Commander] Agent {agentId} 경유 이동이 취소되었습니다. 현재 추격 중입니다.");
                yield break;
            }

            Vector3 waypoint = waypoints[i];

            Debug.Log($"[Commander] Agent {agentId} 경유 이동 {i + 1}/{waypoints.Count}: {waypoint}");

            commandExecutor.Execute(targetAgent, waypoint, "");

            float waitUntil = Time.time + waypointStartGraceSeconds;

            while (targetAgent != null &&
                   !targetAgent.IsManualMoving &&
                   !targetAgent.IsChasing &&
                   Time.time < waitUntil)
            {
                yield return null;
            }

            if (targetAgent == null)
                yield break;

            if (targetAgent.IsChasing)
            {
                Debug.LogWarning($"[Commander] Agent {agentId} 경유 이동이 취소되었습니다. 이동 중 추격 상태로 전환되었습니다.");
                yield break;
            }

            if (!targetAgent.IsManualMoving &&
                GetPlanarDistance(targetAgent.transform.position, waypoint) > GetWaypointArrivalDistance())
            {
                Debug.LogWarning($"[Commander] Agent {agentId} 경유지 이동을 시작하지 못했습니다. 경유 이동을 중단합니다. waypoint={waypoint}");
                yield break;
            }

            while (targetAgent != null && !targetAgent.IsChasing)
            {
                float distance = GetPlanarDistance(targetAgent.transform.position, waypoint);

                if (distance <= GetWaypointArrivalDistance())
                    break;

                if (!targetAgent.IsManualMoving)
                    break;

                yield return null;
            }

            if (targetAgent == null)
                yield break;

            if (targetAgent.IsChasing)
            {
                Debug.LogWarning($"[Commander] Agent {agentId} 경유 이동이 취소되었습니다. 이동 중 추격 상태로 전환되었습니다.");
                yield break;
            }

            Debug.Log($"[Commander] Agent {agentId} 경유지 {i + 1}/{waypoints.Count} 도착 처리");
        }

        Debug.Log($"[Commander] Agent {agentId} 경유 이동 완료");
    }

    private float GetWaypointArrivalDistance()
    {
        return Mathf.Max(waypointReachDistance, 0.8f);
    }

    private float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    private bool IsPatrolCommand(string validatedSkill, List<Vector3> waypoints)
    {
        return validatedSkill == "patrol" &&
               waypoints != null &&
               waypoints.Count >= 2;
    }

    private bool IsWaypointMoveCommand(string validatedSkill, List<Vector3> waypoints)
    {
        return string.IsNullOrWhiteSpace(validatedSkill) &&
               waypoints != null &&
               waypoints.Count > 1;
    }

    private void CancelScheduledCommand(int agentId, bool logMessage)
    {
        if (!scheduledCommandByAgentId.TryGetValue(agentId, out Coroutine routine))
            return;

        if (routine != null)
            StopCoroutine(routine);

        scheduledCommandByAgentId.Remove(agentId);

        if (logMessage)
            Debug.Log($"[Commander] Agent {agentId} 기존 예약 명령을 취소했습니다.");
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
            Debug.Log("[Commander] 모든 예약 명령을 취소했습니다.");
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
                Debug.LogError($"[Commander] 중복된 AgentID가 있습니다. ID: {id}");
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
            {
                depth++;
            }
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