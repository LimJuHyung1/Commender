using UnityEngine;
using UnityEngine.Serialization;

public enum AgentRole
{
    Chaser,
    Observer,
    Engineer,
    Trickster,
    Profiler
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

    [Header("체이서 출입 통제 설정")]
    public float accessControlRadius = 10f;
    public float accessControlDuration = 20f;
    public float targetSpeedMultiplierInAccessControl = 0.5f;
    public float targetAngularSpeedMultiplierInAccessControl = 0.5f;
    public float chaserSpeedMultiplierInAccessControl = 1.5f;
    public float chaserAngularSpeedMultiplierInAccessControl = 1.5f;

    [Header("체이서 도주 제지 설정")]
    public float escapeBlockGaugeMax = 25f;
    public float escapeBlockGaugeDrainPerSecond = 10f;
    public bool escapeBlockStartsFull = true;
    public float escapeBlockMaxDistance = 10f;
    public float escapeBlockRequiredSightTime = 0.25f;
    public float escapeBlockReleaseDelay = 0.5f;

    [Header("체이서 도주 제지 강화 기본값")]
    public float pressureVisionRadiusMultiplier = 1.5f;
    public float pressureVisionHealthDrainMultiplier = 2f;

    [Header("체이서 순찰 설정")]
    public float patrolArrivalDistance = 0.8f;
    public float patrolNavMeshSampleDistance = 2f;
    public float patrolSpeedMultiplier = 1f;
    public float patrolVisionRadiusMultiplier = 1f;

    [Range(0f, 1f)]
    public float patrolSkillGaugeChargeMultiplier = 0.25f;

    [Header("체이서 순찰 강화 기본값")]
    public float patrolPressureTrackingDuration = 4f;
    public float patrolPressureTrackingHealthDrainMultiplier = 1.5f;
    public float upgradedPatrolSpeedMultiplier = 1.5f;

    [Header("체이서 추적 본능 설정")]
    public float trackingInstinctDistancePerStack = 30f;
    public float trackingInstinctSpeedBonusPerStack = 0.05f;
    public int trackingInstinctMaxStack = 5;
    public bool resetTrackingInstinctOnSkillGaugeReset = true;

    [Header("체이서 추적 본능 강화 기본값")]
    public int upgradedTrackingInstinctMaxStack = 10;
    public int instinctiveChargeRequiredStack = 5;
    public float instinctiveChargeDuration = 3f;
    public float instinctiveChargeSpeedMultiplier = 1f;
    public bool instinctiveChargeOncePerStage = true;

    [Header("옵저버 드론 스킬 설정")]
    public float droneDuration = 20f;
    public float droneRadius = 7f;
    public float droneSpawnHeight = 6f;
    public float droneObservationAreaYOffset = 0.05f;

    [Header("옵저버 정찰 스킬 설정")]
    public float reconnaissanceRadius = 3.5f;
    public float reconnaissanceMaxDistance = 18f;
    public float reconnaissanceFlightSpeed = 12f;
    public float reconnaissanceRevealHoldDuration = 2.5f;

    [Header("옵저버 관측 지원 스킬 설정")]
    public float observationSupportDuration = 10f;
    public float observationSupportViewRadiusMultiplier = 1.5f;

    [Header("옵저버 스킬 게이지")]
    public float droneSkillGaugeMax = 100f;
    public float reconnaissanceSkillGaugeMax = 100f;
    public float observationSupportSkillGaugeMax = 100f;

    [Header("안전 관리자 스킬 게이지")]
    public float barricadeSkillGaugeMax = 50f;

    [FormerlySerializedAs("slowTrapSkillGaugeMax")]
    public float stopSignalSkillGaugeMax = 80f;

    public float demolitionSkillGaugeMax = 60f;
    public float safeZoneSkillGaugeMax = 100f;

    [Header("트릭스터 스킬 게이지")]
    public float fakeBoxSkillGaugeMax = 70f;
    public float jokerCardSkillGaugeMax = 100f;
    public float vanishingSkillGaugeMax = 90f;
    public float misdirectionSkillGaugeMax = 80f;

    [Header("트릭스터 조커 카드 설정")]
    public float jokerCardDuration = 10f;
    public float jokerCardMoveSpeedMultiplier = 1.5f;
    public float jokerCardViewRadiusMultiplier = 1.25f;
    public float jokerCardViewAngleBonus = 30f;

    [Header("트릭스터 배니싱 설정")]
    public float vanishingCastTime = 5f;
    public float vanishingRecoveryLockSeconds = 5f;

    [Header("트릭스터 미스디렉션 설정")]
    public float misdirectionDuration = 10f;

    [Header("Legacy Trickster Settings")]
    [HideInInspector] public float decoyDuration = 5f;
    [HideInInspector] public float phantomDuration = 5f;
    [HideInInspector] public float phantomThreatWeight = 1f;
    [HideInInspector] public float noisemakerSkillGaugeMax = 70f;
    [HideInInspector] public float hologramSkillGaugeMax = 100f;

    [Header("Legacy Engineer Settings")]
    [HideInInspector] public float slowTrapDuration = 3f;
    [HideInInspector] public float slowTrapStrength = 0.5f;

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
        spotLightInnerAngle = Mathf.Clamp(spotLightInnerAngle, 1f, 179f);
        spotLightOuterAngle = Mathf.Clamp(spotLightOuterAngle, 1f, 179f);

        accessControlSkillGaugeMax = Mathf.Max(0f, accessControlSkillGaugeMax);

        accessControlRadius = Mathf.Max(0f, accessControlRadius);
        accessControlDuration = Mathf.Max(0f, accessControlDuration);
        targetSpeedMultiplierInAccessControl = Mathf.Max(0.01f, targetSpeedMultiplierInAccessControl);
        targetAngularSpeedMultiplierInAccessControl = Mathf.Max(0.01f, targetAngularSpeedMultiplierInAccessControl);
        chaserSpeedMultiplierInAccessControl = Mathf.Max(0.01f, chaserSpeedMultiplierInAccessControl);
        chaserAngularSpeedMultiplierInAccessControl = Mathf.Max(0.01f, chaserAngularSpeedMultiplierInAccessControl);

        escapeBlockGaugeMax = Mathf.Max(0f, escapeBlockGaugeMax);
        escapeBlockGaugeDrainPerSecond = Mathf.Max(0f, escapeBlockGaugeDrainPerSecond);
        escapeBlockMaxDistance = Mathf.Max(0f, escapeBlockMaxDistance);
        escapeBlockRequiredSightTime = Mathf.Max(0f, escapeBlockRequiredSightTime);
        escapeBlockReleaseDelay = Mathf.Max(0f, escapeBlockReleaseDelay);

        pressureVisionRadiusMultiplier = Mathf.Max(1f, pressureVisionRadiusMultiplier);
        pressureVisionHealthDrainMultiplier = Mathf.Max(1f, pressureVisionHealthDrainMultiplier);

        patrolArrivalDistance = Mathf.Max(0.1f, patrolArrivalDistance);
        patrolNavMeshSampleDistance = Mathf.Max(0.1f, patrolNavMeshSampleDistance);
        patrolSpeedMultiplier = Mathf.Max(0.01f, patrolSpeedMultiplier);
        patrolVisionRadiusMultiplier = Mathf.Max(1f, patrolVisionRadiusMultiplier);
        patrolSkillGaugeChargeMultiplier = Mathf.Clamp01(patrolSkillGaugeChargeMultiplier);

        patrolPressureTrackingDuration = Mathf.Max(0f, patrolPressureTrackingDuration);
        patrolPressureTrackingHealthDrainMultiplier = Mathf.Max(1f, patrolPressureTrackingHealthDrainMultiplier);
        upgradedPatrolSpeedMultiplier = Mathf.Max(1f, upgradedPatrolSpeedMultiplier);

        trackingInstinctDistancePerStack = Mathf.Max(1f, trackingInstinctDistancePerStack);
        trackingInstinctSpeedBonusPerStack = Mathf.Max(0f, trackingInstinctSpeedBonusPerStack);
        trackingInstinctMaxStack = Mathf.Max(0, trackingInstinctMaxStack);

        upgradedTrackingInstinctMaxStack = Mathf.Max(1, upgradedTrackingInstinctMaxStack);
        instinctiveChargeRequiredStack = Mathf.Max(1, instinctiveChargeRequiredStack);
        instinctiveChargeDuration = Mathf.Max(0f, instinctiveChargeDuration);
        instinctiveChargeSpeedMultiplier = Mathf.Max(1f, instinctiveChargeSpeedMultiplier);

        droneDuration = Mathf.Max(0f, droneDuration);
        droneRadius = Mathf.Max(0f, droneRadius);
        droneSpawnHeight = Mathf.Max(0f, droneSpawnHeight);
        droneObservationAreaYOffset = Mathf.Max(0f, droneObservationAreaYOffset);

        reconnaissanceRadius = Mathf.Max(0f, reconnaissanceRadius);
        reconnaissanceMaxDistance = Mathf.Max(0f, reconnaissanceMaxDistance);
        reconnaissanceFlightSpeed = Mathf.Max(0.01f, reconnaissanceFlightSpeed);
        reconnaissanceRevealHoldDuration = Mathf.Max(0f, reconnaissanceRevealHoldDuration);

        observationSupportDuration = Mathf.Max(0f, observationSupportDuration);
        observationSupportViewRadiusMultiplier = Mathf.Max(1f, observationSupportViewRadiusMultiplier);

        droneSkillGaugeMax = Mathf.Max(0f, droneSkillGaugeMax);
        reconnaissanceSkillGaugeMax = Mathf.Max(0f, reconnaissanceSkillGaugeMax);
        observationSupportSkillGaugeMax = Mathf.Max(0f, observationSupportSkillGaugeMax);

        barricadeSkillGaugeMax = Mathf.Max(0f, barricadeSkillGaugeMax);
        stopSignalSkillGaugeMax = Mathf.Max(0f, stopSignalSkillGaugeMax);
        demolitionSkillGaugeMax = Mathf.Max(0f, demolitionSkillGaugeMax);
        safeZoneSkillGaugeMax = Mathf.Max(0f, safeZoneSkillGaugeMax);

        fakeBoxSkillGaugeMax = Mathf.Max(0f, fakeBoxSkillGaugeMax);
        jokerCardSkillGaugeMax = Mathf.Max(0f, jokerCardSkillGaugeMax);
        vanishingSkillGaugeMax = Mathf.Max(0f, vanishingSkillGaugeMax);
        misdirectionSkillGaugeMax = Mathf.Max(0f, misdirectionSkillGaugeMax);

        jokerCardDuration = Mathf.Max(0f, jokerCardDuration);
        jokerCardMoveSpeedMultiplier = Mathf.Max(0f, jokerCardMoveSpeedMultiplier);
        jokerCardViewRadiusMultiplier = Mathf.Max(0f, jokerCardViewRadiusMultiplier);
        jokerCardViewAngleBonus = Mathf.Max(0f, jokerCardViewAngleBonus);

        vanishingCastTime = Mathf.Max(0f, vanishingCastTime);
        vanishingRecoveryLockSeconds = Mathf.Max(0f, vanishingRecoveryLockSeconds);
        misdirectionDuration = Mathf.Max(0f, misdirectionDuration);

        decoyDuration = Mathf.Max(0f, decoyDuration);
        phantomDuration = Mathf.Max(0f, phantomDuration);
        phantomThreatWeight = Mathf.Max(0f, phantomThreatWeight);

        noisemakerSkillGaugeMax = Mathf.Max(0f, noisemakerSkillGaugeMax);
        hologramSkillGaugeMax = Mathf.Max(0f, hologramSkillGaugeMax);

        slowTrapDuration = Mathf.Max(0f, slowTrapDuration);
        slowTrapStrength = Mathf.Max(0f, slowTrapStrength);

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

        if (IsTrackingInstinctSkill(skill))
            return Mathf.Max(0f, trackingInstinctMaxStack);

        if (IsReconnaissanceSkill(skill))
            return Mathf.Max(0f, reconnaissanceSkillGaugeMax);

        if (IsObservationSupportSkill(skill))
            return Mathf.Max(0f, observationSupportSkillGaugeMax);

        if (IsDroneSkill(skill))
            return Mathf.Max(0f, droneSkillGaugeMax);

        if (IsBarricadeSkill(skill))
            return Mathf.Max(0f, barricadeSkillGaugeMax);

        if (IsStopSignalSkill(skill))
            return Mathf.Max(0f, stopSignalSkillGaugeMax);

        if (IsDemolitionSkill(skill))
            return Mathf.Max(0f, demolitionSkillGaugeMax);

        if (IsSafeZoneSkill(skill))
            return Mathf.Max(0f, safeZoneSkillGaugeMax);

        if (IsFakeBoxSkill(skill))
            return Mathf.Max(0f, fakeBoxSkillGaugeMax);

        if (IsJokerCardSkill(skill))
            return Mathf.Max(0f, jokerCardSkillGaugeMax);

        if (IsVanishingSkill(skill))
            return Mathf.Max(0f, vanishingSkillGaugeMax);

        if (IsMisdirectionSkill(skill))
            return Mathf.Max(0f, misdirectionSkillGaugeMax);

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
                    accessControlSkillGaugeMax,
                    escapeBlockGaugeMax,
                    trackingInstinctMaxStack
                );

            case AgentRole.Observer:
                return Mathf.Max(
                    0f,
                    droneSkillGaugeMax,
                    reconnaissanceSkillGaugeMax,
                    observationSupportSkillGaugeMax
                );

            case AgentRole.Engineer:
                return Mathf.Max(
                    0f,
                    barricadeSkillGaugeMax,
                    stopSignalSkillGaugeMax,
                    demolitionSkillGaugeMax,
                    safeZoneSkillGaugeMax
                );

            case AgentRole.Trickster:
                return Mathf.Max(
                    0f,
                    fakeBoxSkillGaugeMax,
                    jokerCardSkillGaugeMax,
                    vanishingSkillGaugeMax,
                    misdirectionSkillGaugeMax
                );

            default:
                return Mathf.Max(
                    0f,
                    accessControlSkillGaugeMax,
                    escapeBlockGaugeMax,
                    trackingInstinctMaxStack,
                    droneSkillGaugeMax,
                    reconnaissanceSkillGaugeMax,
                    observationSupportSkillGaugeMax,
                    barricadeSkillGaugeMax,
                    stopSignalSkillGaugeMax,
                    demolitionSkillGaugeMax,
                    safeZoneSkillGaugeMax,
                    fakeBoxSkillGaugeMax,
                    jokerCardSkillGaugeMax,
                    vanishingSkillGaugeMax,
                    misdirectionSkillGaugeMax,
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
               IsEscapeBlockSkill(skill) ||
               IsPatrolSkill(skill);
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

    private bool IsPatrolSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("patrol") ||
               skill.Contains("순찰");
    }

    private bool IsTrackingInstinctSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("trackinginstinct") ||
               skill.Contains("tracking instinct") ||
               skill.Contains("추적본능") ||
               skill.Contains("추적 본능");
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

    private bool IsReconnaissanceSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("reconnaissance") ||
               skill.Contains("recon") ||
               skill.Contains("scout") ||
               skill.Contains("정찰");
    }

    private bool IsObservationSupportSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("observationsupport") ||
               skill.Contains("observation support") ||
               skill.Contains("vision support") ||
               skill.Contains("관측지원") ||
               skill.Contains("관측 지원") ||
               skill.Contains("시야지원") ||
               skill.Contains("시야 지원");
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

    private bool IsStopSignalSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("stopsignal") ||
               skill.Contains("stop signal") ||
               skill.Contains("stop sign") ||
               skill.Contains("정지신호") ||
               skill.Contains("정지 신호") ||
               skill.Contains("정지표지") ||
               skill.Contains("정지 표지") ||
               skill.Contains("정지장치") ||
               skill.Contains("정지 장치") ||
               skill.Contains("신호설치") ||
               skill.Contains("신호 설치") ||
               skill.Contains("통제신호") ||
               skill.Contains("통제 신호") ||
               IsLegacySlowTrapSkill(skill);
    }

    private bool IsDemolitionSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("demolition") ||
               skill.Contains("demolish") ||
               skill.Contains("철거");
    }

    private bool IsSafeZoneSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("safezone") ||
               skill.Contains("safe zone") ||
               skill.Contains("safe_zone") ||
               skill.Contains("안전구역") ||
               skill.Contains("안전 구역");
    }

    private bool IsFakeBoxSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("fakebox") ||
               skill.Contains("fake box") ||
               skill.Contains("magicbox") ||
               skill.Contains("magic box") ||
               skill.Contains("페이크박스") ||
               skill.Contains("페이크 박스") ||
               skill.Contains("마술상자") ||
               skill.Contains("마술 상자");
    }

    private bool IsJokerCardSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("jokercard") ||
               skill.Contains("joker card") ||
               skill.Contains("조커카드") ||
               skill.Contains("조커 카드");
    }

    private bool IsVanishingSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("vanishing") ||
               skill.Contains("vanish") ||
               skill.Contains("배니싱");
    }

    private bool IsMisdirectionSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("misdirection") ||
               skill.Contains("mis direction") ||
               skill.Contains("미스디렉션") ||
               skill.Contains("미스 디렉션");
    }

    private bool IsLegacySlowTrapSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("slowtrap") ||
               skill.Contains("slow trap") ||
               skill.Contains("snaretrap") ||
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