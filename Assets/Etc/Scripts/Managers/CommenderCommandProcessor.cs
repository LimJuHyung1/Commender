using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CommenderCommandProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChatServiceOpenAI chatService;

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
    "11. Use \"noisemaker\" ONLY when the instruction explicitly asks for noisemaker, noise, 소란 장치, 소음 장치, 소음 발생기, 소란 기계, or 소란 장치 설치.\n" +
    "12. Use \"hologram\" ONLY when the instruction explicitly asks for hologram, 홀로그램, 홀로그램 설치, 현재 위치에 홀로그램, or 자기 위치에 홀로그램.\n" +
    "13. Hologram is always created at the disruptor agent's CURRENT POSITION, not at the requested coordinate.\n" +
    "14. Only one hologram can exist for that agent at a time.\n" +
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
    "Input: Agent 3 Instruction: 9,6에 소란 장치 설치\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 3, \"pos\": {\"x\": 9.0, \"z\": 6.0}, \"skill\": \"noisemaker\" } ] }\n\n" +
    "Input: Agent 3 Instruction: 홀로그램 사용\n" +
    "Output:\n" +
    "{ \"commands\": [ { \"id\": 3, \"pos\": {\"x\": 0.0, \"z\": 0.0}, \"skill\": \"hologram\" } ] }";

    public async Task ProcessCommandsAsync(
        string combinedPrompt,
        Dictionary<int, string> submittedInstructionById,
        Dictionary<int, AgentController> agentById)
    {
        if (chatService == null)
        {
            Debug.LogError("[Commender] ChatServiceOpenAI 참조가 없습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(combinedPrompt))
        {
            Debug.LogWarning("[Commender] 전달된 프롬프트가 비어 있습니다.");
            return;
        }

        string rawResponse = await chatService.GetResponseAsync(systemPrompt, combinedPrompt);

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            Debug.LogWarning("[Commender] AI 응답이 비어 있어 명령 처리를 중단합니다.");
            return;
        }

        ProcessAICommand(rawResponse, submittedInstructionById, agentById);
    }

    private void ProcessAICommand(
        string raw,
        Dictionary<int, string> submittedInstructionById,
        Dictionary<int, AgentController> agentById)
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

                if (agentById == null || !agentById.TryGetValue(cmd.id, out AgentController targetAgent) || targetAgent == null)
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
                if (submittedInstructionById != null &&
                    submittedInstructionById.TryGetValue(cmd.id, out string savedInstruction))
                {
                    originalInstruction = savedInstruction;
                }

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
            if (ContainsAny(normalizedInstruction, "reveal", "recon", "recondrone", "드론", "정찰", "정찰 드론", "정찰드론"))
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
            if (ContainsAny(normalizedInstruction, "barricade", "바리케이드", "바리게이트", "봉쇄", "장애물"))
                return "barricade";

            Debug.LogWarning($"[Commender] 원문에 barricade 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("slowtrap") || normalizedSkill.Contains("snaretrap") || normalizedSkill.Contains("trap"))
        {
            if (ContainsAny(normalizedInstruction, "slowtrap", "snaretrap", "trap", "함정", "정지 함정", "구속 함정", "속박 함정", "트랩"))
                return "slowtrap";

            Debug.LogWarning($"[Commender] 원문에 slowtrap 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("noisemaker") || normalizedSkill.Contains("noise"))
        {
            if (ContainsAny(normalizedInstruction, "noisemaker", "noise", "소란 장치", "장치", "소란", "기계"))
                return "noisemaker";

            Debug.LogWarning($"[Commender] 원문에 noisemaker 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
            return "";
        }

        if (normalizedSkill.Contains("hologram"))
        {
            if (ContainsAny(normalizedInstruction, "hologram", "홀로그램", "현재 위치", "현재위치", "위치"))
                return "hologram";

            Debug.LogWarning($"[Commender] 원문에 hologram 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
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