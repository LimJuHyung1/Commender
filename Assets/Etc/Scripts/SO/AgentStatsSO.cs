using UnityEngine;

public enum AgentRole
{
    Pursuer,
    Scout,
    Engineer,
    Disruptor
}

[CreateAssetMenu(fileName = "AgentStats", menuName = "Commander/Agent Stats")]
public class AgentStatsSO : ScriptableObject
{
    [Header("기본 정보")]
    public AgentRole role;

    [Header("공통 이동")]
    public float moveSpeed = 4.5f;
    public float acceleration = 8f;
    public float stoppingDistance = 0.2f;
    public float angularSpeed = 120f;

    [Header("시야")]
    public float viewRadius = 7.5f;
    [Range(1f, 360f)] public float viewAngle = 60f;

    [Header("Spot Light")]
    public bool useSpotLight = true;
    public Color spotLightColor = Color.white;
    public float spotLightIntensity = 100f;
    public float spotLightRange = 10f;
    [Range(1f, 179f)] public float spotLightInnerAngle = 16.6f;
    [Range(1f, 179f)] public float spotLightOuterAngle = 67.2f;

    [Header("추격병 대쉬")]
    public float dashSpeed = 15f;
    public float dashAcceleration = 15f;
    public float dashDuration = 1.5f;

    [Header("수색병")]
    public float reconDuration = 5f;
    public float reconRadius = 5f;
    public float wallSightDuration = 5f;

    [Header("공병")]
    public float slowTrapDuration = 3f;
    public float slowTrapStrength = 0.5f;

    [Header("교란병")]
    public float decoyDuration = 5f;
    public float phantomDuration = 5f;
    public float phantomThreatWeight = 1f;

    [Header("추격병 스킬 게이지")]
    public float dashSkillGaugeMax = 80f;
    public float smokeSkillGaugeMax = 100f;

    [Header("수색병 스킬 게이지")]
    public float flareSkillGaugeMax = 100f;

    [Header("공병 스킬 게이지")]
    public float barricadeSkillGaugeMax = 80f;
    public float slowTrapSkillGaugeMax = 90f;

    [Header("교란병 스킬 게이지")]
    public float noisemakerSkillGaugeMax = 70f;
    public float hologramSkillGaugeMax = 100f;

    public float GetSkillGaugeMax(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return 100f;

        string skill = skillName.Trim().ToLower();

        if (IsPositionShareSkill(skill))
            return 0f;

        if (skill.Contains("dash") || skill.Contains("대쉬") || skill.Contains("대시"))
            return Mathf.Max(0f, dashSkillGaugeMax);

        if (skill.Contains("smoke") || skill.Contains("연막"))
            return Mathf.Max(0f, smokeSkillGaugeMax);

        if (skill.Contains("flare") ||
            skill.Contains("signalflare") ||
            skill.Contains("signal flare") ||
            skill.Contains("조명탄") ||
            skill.Contains("신호탄"))
        {
            return Mathf.Max(0f, flareSkillGaugeMax);
        }

        if (skill.Contains("barricade") || skill.Contains("바리케이드"))
            return Mathf.Max(0f, barricadeSkillGaugeMax);

        if (skill.Contains("slowtrap") ||
            skill.Contains("slow trap") ||
            skill.Contains("trap") ||
            skill.Contains("트랩") ||
            skill.Contains("함정"))
        {
            return Mathf.Max(0f, slowTrapSkillGaugeMax);
        }

        if (skill.Contains("noisemaker") ||
            skill.Contains("noise") ||
            skill.Contains("소란") ||
            skill.Contains("소음"))
        {
            return Mathf.Max(0f, noisemakerSkillGaugeMax);
        }

        if (skill.Contains("hologram") || skill.Contains("홀로그램"))
            return Mathf.Max(0f, hologramSkillGaugeMax);

        return 100f;
    }

    public float GetLargestSkillGaugeMax()
    {
        switch (role)
        {
            case AgentRole.Pursuer:
                return Mathf.Max(
                    0f,
                    dashSkillGaugeMax,
                    smokeSkillGaugeMax
                );

            case AgentRole.Scout:
                return Mathf.Max(
                    0f,
                    flareSkillGaugeMax
                );

            case AgentRole.Engineer:
                return Mathf.Max(
                    0f,
                    barricadeSkillGaugeMax,
                    slowTrapSkillGaugeMax
                );

            case AgentRole.Disruptor:
                return Mathf.Max(
                    0f,
                    noisemakerSkillGaugeMax,
                    hologramSkillGaugeMax
                );

            default:
                return Mathf.Max(
                    0f,
                    dashSkillGaugeMax,
                    smokeSkillGaugeMax,
                    flareSkillGaugeMax,
                    barricadeSkillGaugeMax,
                    slowTrapSkillGaugeMax,
                    noisemakerSkillGaugeMax,
                    hologramSkillGaugeMax
                );
        }
    }

    public bool IsNoGaugeSkill(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        string skill = skillName.Trim().ToLower();
        return IsPositionShareSkill(skill);
    }

    private bool IsPositionShareSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("positionshare") ||
               skill.Contains("position share") ||
               skill.Contains("target position share") ||
               skill.Contains("위치공유") ||
               skill.Contains("위치 공유");
    }
}