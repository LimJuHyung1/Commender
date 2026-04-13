using UnityEngine;

public sealed class CommandExecutor
{
    private const string SkillHold = "hold";
    private const string SkillLookAround = "lookaround";
    private const string SkillDash = "dash";
    private const string SkillHologram = "hologram";

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
                Debug.Log($"<color=grey>[Action]</color> Agent {agentId} : 제자리 대기");
                return;

            case SkillLookAround:
                ExecuteLookAround(targetAgent, agentId);
                return;

            case SkillDash:
                ExecuteDash(targetAgent, agentId, dest);
                return;

            case SkillHologram:
                Vector3 currentPosition = targetAgent.transform.position;
                Debug.Log($"<color=cyan>[Action]</color> Agent {agentId} : 현재 위치 {currentPosition} 에 'hologram' 스킬 사용");
                targetAgent.ExecuteSkill(validatedSkill, currentPosition);
                return;

            default:
                Debug.Log($"<color=cyan>[Action]</color> Agent {agentId} : {dest} 위치에 '{validatedSkill}' 스킬 사용");
                targetAgent.ExecuteSkill(validatedSkill, dest);
                return;
        }
    }

    private void ExecuteLookAround(AgentController targetAgent, int agentId)
    {
        bool started = targetAgent.TryStartLookAround();

        if (!started)
        {
            Debug.LogWarning($"[Commender] Agent {agentId} 주변 둘러보기를 시작하지 못했습니다.");
            return;
        }

        Debug.Log($"<color=yellow>[Action]</color> Agent {agentId} : 주변 둘러보기");
    }

    private void ExecuteDash(AgentController targetAgent, int agentId, Vector3 dest)
    {
        if (targetAgent.IsChasing)
        {
            Debug.LogWarning($"[Commender] Agent {agentId} 예약된 dash 이동이 취소되었습니다. 현재 추격 중입니다.");
            return;
        }

        Debug.Log($"<color=cyan>[Action]</color> Agent {agentId} : {dest} 로 dash 이동");
        targetAgent.MoveTo(dest);
        targetAgent.ExecuteSkill(SkillDash, dest);
    }

    private void ExecuteMoveCommand(AgentController targetAgent, int agentId, Vector3 dest)
    {
        if (targetAgent.IsChasing)
        {
            Debug.LogWarning($"[Commender] Agent {agentId} 예약된 이동이 취소되었습니다. 현재 추격 중입니다.");
            return;
        }

        Debug.Log($"<color=green>[Action]</color> Agent {agentId} : {dest} 로 이동 명령");
        targetAgent.MoveTo(dest);
    }
}