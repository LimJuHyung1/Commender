using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
                else if (commandValidator.IsLookAroundInstruction(originalInstruction))
                {
                    validatedSkill = "lookaround";
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
                else if (commandValidator.IsMovementInstruction(originalInstruction))
                {
                    validatedSkill = "";
                }
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
            "7. If the instruction explicitly asks to check surroundings such as 주변, 주위, 주변 확인, 주위 확인, 주변 둘러봐, 주위 둘러봐, 주변 살펴봐, 주위 살펴봐, look around, or check around, use skill \"lookaround\".\n" +
            "8. Bare instructions like '주변' or '주위' should also be interpreted as \"lookaround\".\n" +
            "9. If the instruction is vague, unsupported, or outside the supported command set, use skill \"hold\".\n" +
            "10. When using \"lookaround\" or \"hold\", pos should be {\"x\":0.0,\"z\":0.0}.\n" +
            "11. If a skill is used without a location, set pos as {\"x\":0.0,\"z\":0.0} unless the skill is defined to use current position.\n" +
            "12. Output JSON only.\n";

        if (targetAgent is Chaser)
        {
            return commonRules +
                   "13. Allowed skills for this agent are only \"accesscontrol\" and \"escapeblock\".\n" +
                   "14. Use \"accesscontrol\" ONLY when the instruction explicitly asks for 출입 통제, 출입통제, 통제 구역, access control, or control zone.\n" +
                   "15. accesscontrol MUST use the requested coordinate as the center position of the control zone.\n" +
                   "16. Use \"escapeblock\" ONLY when the instruction explicitly mentions 도주 제지, 도주제지, 도주 스킬 차단, escape block, or escape skill block.\n" +
                   "17. escapeblock is an automatic gauge-based skill. It does not turn on or off. If no coordinate is provided, set pos as {\"x\":0.0,\"z\":0.0}.\n\n" +
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
                   "13. Allowed skills for this agent are only \"drone\", \"positionshare_on\", and \"positionshare_off\".\n" +
                   "14. Use \"drone\" ONLY when the instruction explicitly asks for drone or 드론.\n" +
                   "15. drone MUST use the requested coordinate as the center position of the drone observation area.\n" +
                   "16. Use \"positionshare_on\" ONLY when the instruction explicitly asks to enable position sharing, 위치 공유 켜, 위치 공유 시작, 위치 공유해, 타겟 위치 공유, 타겟 위치 알려줘, 발견하면 알려, or 보이면 알려.\n" +
                   "17. Use \"positionshare_off\" ONLY when the instruction explicitly asks to disable position sharing, 위치 공유 꺼, 위치 공유 중지, 위치 공유하지 마, or 타겟 위치 공유하지 마.\n" +
                   "18. Do not use flare, signalflare, wallsight, or truesight. Those skills are not supported by ObserverAgent.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 5,4에 드론\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 5.0, \"z\": 4.0}}, \"skill\": \"drone\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 위치 공유 꺼\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"positionshare_off\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 위치 공유 켜\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"positionshare_on\" }} ] }}";
        }

        if (targetAgent is Engineer)
        {
            return commonRules +
                   "13. Allowed skills for this agent are only \"barricade\" and \"stopsignal\".\n" +
                   "14. Use \"barricade\" ONLY when the instruction explicitly asks for barricade, 바리케이드, 봉쇄, or 장애물 설치.\n" +
                   "15. Use \"stopsignal\" ONLY when the instruction explicitly asks for 정지 신호, 정지신호, 신호 설치, 통제 신호, stop signal, or stopsignal.\n" +
                   "16. barricade MUST use the requested coordinate as the center position of the barricade.\n" +
                   "17. stopsignal MUST use the requested coordinate as the center position of the stop signal device.\n" +
                   "18. If the instruction is just 바리케이드, barricade, 정지 신호, 정지신호, 신호 설치, or stopsignal without coordinates, interpret it as using the skill at the engineer's current position.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 4,1에 바리케이드 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 4.0, \"z\": 1.0}}, \"skill\": \"barricade\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 2,6에 정지 신호 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 2.0, \"z\": 6.0}}, \"skill\": \"stopsignal\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 정지 신호\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"stopsignal\" }} ] }}";
        }

        if (targetAgent is Trickster)
        {
            return commonRules +
                   "13. This agent is the Magician-type Trickster agent.\n" +
                   "14. The only manually commanded skill for this agent is \"fakebox\".\n" +
                   "15. Use \"fakebox\" ONLY when the instruction explicitly asks for fakebox, fake box, magic box, 페이크 박스, 페이크박스, 마술 상자, 마술상자, 가짜 상자, or 가짜상자.\n" +
                   "16. fakebox MUST use the requested coordinate as the center position of the fake box.\n" +
                   "17. If the instruction is just 페이크 박스, 페이크박스, 마술 상자, 마술상자, 가짜 상자, 가짜상자, fakebox, fake box, magic box, or magicbox without coordinates, interpret it as using the skill at the magician's current position.\n" +
                   "18. Joker Card is an automatic gauge-based skill. Never output \"jokercard\" as a command.\n" +
                   "19. If the user asks to use Joker Card, output skill \"hold\" because Joker Card activates automatically when its gauge is full.\n" +
                   "20. Do not use noisemaker, noise, hologram, or 소란 장치. Those are no longer skills for this magician agent.\n\n" +
                   "OUTPUT FORMAT:\n" +
                   "{ \"commands\": [ { \"id\": 0, \"delaySeconds\": 0.0, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"\" } ] }\n\n" +
                   "EXAMPLES:\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 7,7에 페이크 박스 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 7.0, \"z\": 7.0}}, \"skill\": \"fakebox\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 마술 상자 설치\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"fakebox\" }} ] }}\n\n" +
                   $"Input: Agent {targetAgent.AgentID} Instruction: 조커 카드 사용\n" +
                   $"Output:\n{{ \"commands\": [ {{ \"id\": {targetAgent.AgentID}, \"delaySeconds\": 0.0, \"pos\": {{\"x\": 0.0, \"z\": 0.0}}, \"skill\": \"hold\" }} ] }}";
        }

        return commonRules;
    }

    private bool IsSkillAllowedForAgent(AgentController agent, string skill)
    {
        if (agent == null)
            return false;

        if (string.IsNullOrWhiteSpace(skill))
            return true;

        if (skill == "hold" || skill == "lookaround")
            return true;

        if (agent is Chaser)
        {
            return skill == "accesscontrol" ||
                   skill == "escapeblock";
        }

        if (agent is Observer)
        {
            return skill == "drone" ||
                   skill == "positionshare_on" ||
                   skill == "positionshare_off";
        }

        if (agent is Engineer)
        {
            return skill == "barricade" ||
                   skill == "stopsignal";
        }

        if (agent is Trickster)
        {
            return skill == "fakebox";
        }

        return true;
    }

    private bool IsBarricadeInstruction(string source)
    {
        if (commandValidator == null)
            commandValidator = new CommandValidator();

        return commandValidator.IsBarricadeInstruction(source);
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

        if (commandValidator.IsBarricadeInstruction(source))
            return true;

        if (commandValidator.IsStopSignalInstruction(source))
            return true;

        if (commandValidator.IsFakeBoxInstruction(source))
            return true;

        if (commandValidator.IsJokerCardInstruction(source))
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

            "jokercard",
            "joker card",
            "조커 카드",
            "조커카드",

            "look around",
            "check around",
            "around",
            "scan",
            "observe",
            "주변",
            "주위",
            "주변 확인",
            "주위 확인",
            "주변 둘러",
            "주위 둘러",
            "주변 살펴",
            "주위 살펴"
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

        if (validatedSkill == "stopsignal")
            return commandValidator.IsStopSignalInstruction(originalInstruction);

        if (validatedSkill == "barricade")
            return commandValidator.IsBarricadeInstruction(originalInstruction);

        if (validatedSkill == "fakebox")
            return commandValidator.IsFakeBoxInstruction(originalInstruction);

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

        if (IsWaypointMoveCommand(validatedSkill, waypoints))
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

        if (IsWaypointMoveCommand(validatedSkill, waypoints))
        {
            yield return ExecuteWaypointMoveCoroutine(targetAgent, waypoints);
            scheduledCommandByAgentId.Remove(agentId);
            yield break;
        }

        commandExecutor.Execute(targetAgent, dest, validatedSkill);
        scheduledCommandByAgentId.Remove(agentId);
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