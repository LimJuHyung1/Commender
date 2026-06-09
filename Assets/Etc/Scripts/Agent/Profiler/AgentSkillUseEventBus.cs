using System;
using UnityEngine;

public static class AgentSkillUseEventBus
{
    public static event Action<AgentController, string> OnAgentSkillExecuted;

    public static void RaiseAgentSkillExecuted(AgentController agent, string skillKey)
    {
        if (agent == null)
            return;

        if (string.IsNullOrWhiteSpace(skillKey))
            return;

        OnAgentSkillExecuted?.Invoke(agent, skillKey);
    }
}