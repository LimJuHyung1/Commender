using UnityEngine;

public interface ITargetEscapeSkillBlockReceiver
{
    bool IsEscapeSkillBlocked { get; }

    void SetEscapeSkillBlocked(Component source, bool blocked);
}