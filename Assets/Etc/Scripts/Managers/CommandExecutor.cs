using UnityEngine;

public sealed class CommandExecutor
{
    private const string SkillHold = "hold";
    private const string SkillLookAround = "lookaround";
    private const string SkillDash = "dash";
    private const string SkillHologram = "hologram";

    private TargetSkillController targetSkillController;

    public CommandExecutor()
    {
    }

    public CommandExecutor(TargetSkillController targetSkillController)
    {
        this.targetSkillController = targetSkillController;
    }

    public void SetTargetSkillController(TargetSkillController newTargetSkillController)
    {
        targetSkillController = newTargetSkillController;
    }

    public void Execute(AgentController targetAgent, Vector3 dest, string validatedSkill)
    {
        if (targetAgent == null)
            return;

        int agentId = targetAgent.AgentID;

        if (!string.IsNullOrWhiteSpace(validatedSkill))
        {
            ExecuteSkillCommand(targetAgent, agentId, dest, validatedSkill);
            return;
        }

        ExecuteMoveCommand(targetAgent, agentId, dest);
    }

    private void ExecuteSkillCommand(
        AgentController targetAgent,
        int agentId,
        Vector3 dest,
        string validatedSkill)
    {
        switch (validatedSkill)
        {
            case SkillHold:
                ExecuteHold(agentId);
                return;

            case SkillLookAround:
                ExecuteLookAround(targetAgent, agentId);
                return;

            case SkillDash:
                ExecuteDash(targetAgent, agentId, dest);
                return;

            case SkillHologram:
                ExecuteHologram(targetAgent, agentId);
                return;

            default:
                ExecuteDefaultSkill(targetAgent, agentId, dest, validatedSkill);
                return;
        }
    }

    private void ExecuteHold(int agentId)
    {
        Debug.Log($"<color=grey>[Action]</color> Agent {agentId} : 제자리 대기");
    }

    private void ExecuteLookAround(AgentController targetAgent, int agentId)
    {
        bool started = targetAgent.TryStartLookAround();

        if (!started)
        {
            Debug.LogWarning($"[Commander] Agent {agentId} 주변 둘러보기를 시작하지 못했습니다.");
            return;
        }

        Debug.Log($"<color=yellow>[Action]</color> Agent {agentId} : 주변 둘러보기");
    }

    private void ExecuteDash(AgentController targetAgent, int agentId, Vector3 dest)
    {
        if (targetAgent.IsChasing)
        {
            Debug.LogWarning($"[Commander] Agent {agentId} 예약된 dash 이동이 취소되었습니다. 현재 추격 중입니다.");
            return;
        }

        if (!targetAgent.CanUseSkillGaugeForSkill(SkillDash, true))
            return;

        Vector3 finalDest = ResolveCommandPosition(dest, SkillDash, agentId);

        Debug.Log($"<color=cyan>[Action]</color> Agent {agentId} : {finalDest} 로 dash 이동");

        targetAgent.MoveTo(finalDest);
        targetAgent.ExecuteSkill(SkillDash, finalDest);
    }

    private void ExecuteHologram(AgentController targetAgent, int agentId)
    {
        Vector3 currentPosition = targetAgent.transform.position;

        Debug.Log($"<color=cyan>[Action]</color> Agent {agentId} : 현재 위치 {currentPosition} 에 'hologram' 스킬 사용");

        targetAgent.ExecuteSkill(SkillHologram, currentPosition);
    }

    private void ExecuteDefaultSkill(
        AgentController targetAgent,
        int agentId,
        Vector3 dest,
        string validatedSkill)
    {
        Vector3 finalDest = ResolveCommandPosition(dest, validatedSkill, agentId);

        Debug.Log($"<color=cyan>[Action]</color> Agent {agentId} : {finalDest} 위치에 '{validatedSkill}' 스킬 사용");

        targetAgent.ExecuteSkill(validatedSkill, finalDest);
    }

    private void ExecuteMoveCommand(AgentController targetAgent, int agentId, Vector3 dest)
    {
        if (targetAgent.IsChasing)
        {
            Debug.LogWarning($"[Commander] Agent {agentId} 예약된 이동이 취소되었습니다. 현재 추격 중입니다.");
            return;
        }

        Vector3 finalDest = ResolveCommandPosition(dest, "move", agentId);

        Debug.Log($"<color=green>[Action]</color> Agent {agentId} : {finalDest} 로 이동 명령");

        targetAgent.MoveTo(finalDest);
    }

    private Vector3 ResolveCommandPosition(Vector3 originalPosition, string commandName, int agentId)
    {
        TargetSkillController skillController = GetTargetSkillController();

        if (skillController == null)
        {
            Debug.Log(
                $"[CommandExecutor] 명령 변조 체크 불가 - TargetSkillController를 찾지 못했습니다. " +
                $"AgentID: {agentId}, Command: {commandName}, Position: {originalPosition}");

            return originalPosition;
        }

        Debug.Log(
            $"[CommandExecutor] 명령 변조 체크 요청 - " +
            $"AgentID: {agentId}, Command: {commandName}, InputPosition: {originalPosition}");

        if (skillController.TryDistortCommandPosition(originalPosition, out Vector3 distortedPosition))
        {
            Debug.Log(
                $"[CommandExecutor] 명령 변조 적용 - " +
                $"AgentID: {agentId}, Command: {commandName}, " +
                $"Original: {originalPosition}, Final: {distortedPosition}, " +
                $"Distance: {Vector3.Distance(originalPosition, distortedPosition):F2}");

            return distortedPosition;
        }

        Debug.Log(
            $"[CommandExecutor] 명령 변조 미적용 - " +
            $"AgentID: {agentId}, Command: {commandName}, Final: {originalPosition}");

        return originalPosition;
    }

    private TargetSkillController GetTargetSkillController()
    {
        if (targetSkillController != null)
            return targetSkillController;

        targetSkillController = Object.FindFirstObjectByType<TargetSkillController>();

        return targetSkillController;
    }
}