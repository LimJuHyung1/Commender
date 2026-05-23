using System;

public static class SkillCutsceneEventBus
{
    public static event Action<AgentController, string> AgentSkillCutsceneRequested;

    public static void RequestAgentSkillCutscene(AgentController agent, string skillKey)
    {
        if (agent == null)
            return;

        if (string.IsNullOrWhiteSpace(skillKey))
            return;

        AgentSkillCutsceneRequested?.Invoke(agent, skillKey);
    }
}