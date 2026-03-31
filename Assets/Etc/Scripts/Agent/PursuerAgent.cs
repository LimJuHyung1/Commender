using UnityEngine;
using System.Collections;

public class PursuerAgent : AgentController
{
    [Header("연막탄 스킬 설정")]
    [SerializeField] private GameObject smokePrefab;

    protected override void Awake()
    {
        agentID = 0;
        base.Awake();
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Pursuer {AgentID}] 스킬 요청: {skillName} (위치: {targetPos})");

        if (skill.Contains("dash"))
        {
            StopAllCoroutines();
            StartCoroutine(DashRoutine());
        }
        else if (skill.Contains("smoke"))
        {
            ExecuteSmokeSkill(targetPos);
        }
        else
        {
            Debug.LogWarning($"[Pursuer {AgentID}] 알 수 없는 스킬: {skillName}");
        }
    }

    private IEnumerator DashRoutine()
    {
        if (navAgent == null)
            yield break;

        if (stats == null)
        {
            Debug.LogWarning($"[Pursuer {AgentID}] AgentStatsSO가 없어 대쉬를 사용할 수 없습니다.");
            yield break;
        }

        float originalSpeed = navAgent.speed;
        float originalAcceleration = navAgent.acceleration;

        navAgent.speed = stats.dashSpeed;
        navAgent.acceleration = stats.dashAcceleration;

        Debug.Log(
            $"<color=cyan>[Pursuer Skill]</color> Agent {AgentID} : " +
            $"대쉬 시작 (speed={stats.dashSpeed}, accel={stats.dashAcceleration}, duration={stats.dashDuration})"
        );

        yield return new WaitForSeconds(stats.dashDuration);

        navAgent.speed = stats.moveSpeed;
        navAgent.acceleration = stats.acceleration;

        Debug.Log(
            $"<color=cyan>[Pursuer Skill]</color> Agent {AgentID} : " +
            $"대쉬 종료. 기본 이동값으로 복구 (speed={stats.moveSpeed}, accel={stats.acceleration})"
        );
    }

    private void ExecuteSmokeSkill(Vector3 targetPos)
    {
        if (smokePrefab == null)
        {
            Debug.LogWarning($"[Pursuer {AgentID}] smokePrefab이 연결되지 않았습니다.");
            return;
        }

        Debug.Log($"<color=gray>[Skill]</color> Agent {AgentID} : {targetPos} 지점에 연막탄 투척!");

        Instantiate(smokePrefab, targetPos, Quaternion.identity);
    }
}