using UnityEngine;

public enum AgentRole
{
    Chaser,
    Observer,
    Engineer,
    Trickster
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

    [Range(1f, 360f)]
    public float viewAngle = 60f;

    [Header("Spot Light")]
    public bool useSpotLight = true;
    public Color spotLightColor = Color.white;
    public float spotLightIntensity = 100f;
    public float spotLightRange = 10f;

    [Range(1f, 179f)]
    public float spotLightInnerAngle = 16.6f;

    [Range(1f, 179f)]
    public float spotLightOuterAngle = 67.2f;

    [Header("체이서 스킬 게이지")]
    public float accessControlSkillGaugeMax = 100f;

    [Header("옵저버 드론 스킬 설정")]
    public float droneDuration = 20f;
    public float droneRadius = 7f;
    public float droneSpawnHeight = 6f;
    public float droneObservationAreaYOffset = 0.05f;

    [Header("옵저버 스킬 게이지")]
    public float droneSkillGaugeMax = 50f;

    [Header("엔지니어 스킬 설정")]
    public float slowTrapDuration = 3f;
    public float slowTrapStrength = 0.5f;

    [Header("엔지니어 스킬 게이지")]
    public float barricadeSkillGaugeMax = 80f;
    public float slowTrapSkillGaugeMax = 90f;

    [Header("트릭스터 스킬 설정")]
    public float decoyDuration = 5f;
    public float phantomDuration = 5f;
    public float phantomThreatWeight = 1f;

    [Header("트릭스터 스킬 게이지")]
    public float noisemakerSkillGaugeMax = 70f;
    public float hologramSkillGaugeMax = 100f;

    [Header("Legacy Chaser Settings")]
    [HideInInspector] public float dashSpeed = 15f;
    [HideInInspector] public float dashAcceleration = 15f;
    [HideInInspector] public float dashDuration = 1.5f;
    [HideInInspector] public float dashSkillGaugeMax = 80f;
    [HideInInspector] public float smokeSkillGaugeMax = 100f;

    [Header("Legacy Observer Settings")]
    [HideInInspector] public float reconDuration = 5f;
    [HideInInspector] public float reconRadius = 5f;
    [HideInInspector] public float wallSightDuration = 5f;
    [HideInInspector] public float flareSkillGaugeMax = 100f;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        angularSpeed = Mathf.Max(0f, angularSpeed);

        viewRadius = Mathf.Max(0f, viewRadius);

        spotLightIntensity = Mathf.Max(0f, spotLightIntensity);
        spotLightRange = Mathf.Max(0f, spotLightRange);

        accessControlSkillGaugeMax = Mathf.Max(0f, accessControlSkillGaugeMax);

        droneDuration = Mathf.Max(0f, droneDuration);
        droneRadius = Mathf.Max(0f, droneRadius);
        droneSpawnHeight = Mathf.Max(0f, droneSpawnHeight);
        droneObservationAreaYOffset = Mathf.Max(0f, droneObservationAreaYOffset);
        droneSkillGaugeMax = Mathf.Max(0f, droneSkillGaugeMax);

        slowTrapDuration = Mathf.Max(0f, slowTrapDuration);
        slowTrapStrength = Mathf.Max(0f, slowTrapStrength);

        barricadeSkillGaugeMax = Mathf.Max(0f, barricadeSkillGaugeMax);
        slowTrapSkillGaugeMax = Mathf.Max(0f, slowTrapSkillGaugeMax);

        decoyDuration = Mathf.Max(0f, decoyDuration);
        phantomDuration = Mathf.Max(0f, phantomDuration);
        phantomThreatWeight = Mathf.Max(0f, phantomThreatWeight);

        noisemakerSkillGaugeMax = Mathf.Max(0f, noisemakerSkillGaugeMax);
        hologramSkillGaugeMax = Mathf.Max(0f, hologramSkillGaugeMax);

        dashSpeed = Mathf.Max(0f, dashSpeed);
        dashAcceleration = Mathf.Max(0f, dashAcceleration);
        dashDuration = Mathf.Max(0f, dashDuration);
        dashSkillGaugeMax = Mathf.Max(0f, dashSkillGaugeMax);
        smokeSkillGaugeMax = Mathf.Max(0f, smokeSkillGaugeMax);

        reconDuration = Mathf.Max(0f, reconDuration);
        reconRadius = Mathf.Max(0f, reconRadius);
        wallSightDuration = Mathf.Max(0f, wallSightDuration);
        flareSkillGaugeMax = Mathf.Max(0f, flareSkillGaugeMax);
    }

    public float GetSkillGaugeMax(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return 100f;

        string skill = NormalizeSkillName(skillName);

        if (IsNoGaugeSkill(skill))
            return 0f;

        if (IsAccessControlSkill(skill))
            return Mathf.Max(0f, accessControlSkillGaugeMax);

        if (IsDroneSkill(skill))
            return Mathf.Max(0f, droneSkillGaugeMax);

        if (IsBarricadeSkill(skill))
            return Mathf.Max(0f, barricadeSkillGaugeMax);

        if (IsSlowTrapSkill(skill))
            return Mathf.Max(0f, slowTrapSkillGaugeMax);

        if (IsNoisemakerSkill(skill))
            return Mathf.Max(0f, noisemakerSkillGaugeMax);

        if (IsHologramSkill(skill))
            return Mathf.Max(0f, hologramSkillGaugeMax);

        if (IsLegacyDashSkill(skill))
            return Mathf.Max(0f, dashSkillGaugeMax);

        if (IsLegacySmokeSkill(skill))
            return Mathf.Max(0f, smokeSkillGaugeMax);

        return 100f;
    }

    public float GetLargestSkillGaugeMax()
    {
        switch (role)
        {
            case AgentRole.Chaser:
                return Mathf.Max(
                    0f,
                    accessControlSkillGaugeMax
                );

            case AgentRole.Observer:
                return Mathf.Max(
                    0f,
                    droneSkillGaugeMax
                );

            case AgentRole.Engineer:
                return Mathf.Max(
                    0f,
                    barricadeSkillGaugeMax,
                    slowTrapSkillGaugeMax
                );

            case AgentRole.Trickster:
                return Mathf.Max(
                    0f,
                    noisemakerSkillGaugeMax,
                    hologramSkillGaugeMax
                );

            default:
                return Mathf.Max(
                    0f,
                    accessControlSkillGaugeMax,
                    droneSkillGaugeMax,
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

        string skill = NormalizeSkillName(skillName);

        return IsPositionShareSkill(skill) ||
               IsEscapeBlockSkill(skill);
    }

    private string NormalizeSkillName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "";

        return skillName.Trim().ToLower();
    }

    private bool IsAccessControlSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("accesscontrol") ||
               skill.Contains("access control") ||
               skill.Contains("controlzone") ||
               skill.Contains("control zone") ||
               skill.Contains("restricted zone") ||
               skill.Contains("출입통제") ||
               skill.Contains("출입 통제") ||
               skill.Contains("통제구역") ||
               skill.Contains("통제 구역") ||
               skill.Contains("금지구역") ||
               skill.Contains("금지 구역");
    }

    private bool IsEscapeBlockSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("escapeblock") ||
               skill.Contains("escape block") ||
               skill.Contains("escape skill block") ||
               skill.Contains("block escape") ||
               skill.Contains("도주제지") ||
               skill.Contains("도주 제지") ||
               skill.Contains("도주스킬차단") ||
               skill.Contains("도주 스킬 차단") ||
               skill.Contains("도주차단") ||
               skill.Contains("도주 차단") ||
               skill.Contains("탈출차단") ||
               skill.Contains("탈출 차단");
    }

    private bool IsPositionShareSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("positionshare") ||
               skill.Contains("position share") ||
               skill.Contains("target position share") ||
               skill.Contains("위치공유") ||
               skill.Contains("위치 공유") ||
               skill.Contains("타겟위치공유") ||
               skill.Contains("타겟 위치 공유") ||
               skill.Contains("대상위치공유") ||
               skill.Contains("대상 위치 공유");
    }

    private bool IsDroneSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("drone") ||
               skill.Contains("uav") ||
               skill.Contains("드론");
    }

    private bool IsBarricadeSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("barricade") ||
               skill.Contains("바리케이드") ||
               skill.Contains("봉쇄") ||
               skill.Contains("장애물");
    }

    private bool IsSlowTrapSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("slowtrap") ||
               skill.Contains("slow trap") ||
               skill.Contains("snaretrap") ||
               skill.Contains("trap") ||
               skill.Contains("트랩") ||
               skill.Contains("함정") ||
               skill.Contains("감속함정") ||
               skill.Contains("감속 함정") ||
               skill.Contains("구속함정") ||
               skill.Contains("구속 함정");
    }

    private bool IsNoisemakerSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("noisemaker") ||
               skill.Contains("noise") ||
               skill.Contains("소란장치") ||
               skill.Contains("소란 장치") ||
               skill.Contains("소음") ||
               skill.Contains("소란");
    }

    private bool IsHologramSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("hologram") ||
               skill.Contains("홀로그램");
    }

    private bool IsLegacyDashSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("dash") ||
               skill.Contains("대쉬") ||
               skill.Contains("대시");
    }

    private bool IsLegacySmokeSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("smoke") ||
               skill.Contains("연막") ||
               skill.Contains("연막탄");
    }
}