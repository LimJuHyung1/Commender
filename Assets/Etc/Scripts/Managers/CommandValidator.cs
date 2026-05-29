using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class CommandValidator
{
    private const string SkillMove = "";
    private const string SkillHold = "hold";
    private const string SkillLookAround = "lookaround";

    private const string SkillDash = "dash";
    private const string SkillSmoke = "smoke";

    private const string SkillAccessControl = "accesscontrol";
    private const string SkillEscapeBlock = "escapeblock";
    private const string SkillPatrol = "patrol";
    private const string SkillTrackingInstinct = "trackinginstinct";

    private const string SkillDrone = "drone";
    private const string SkillReconnaissance = "reconnaissance";
    private const string SkillObservationSupport = "observationsupport";
    private const string SkillPositionShareOn = "positionshare_on";
    private const string SkillPositionShareOff = "positionshare_off";

    private const string SkillBarricade = "barricade";
    private const string SkillStopSignal = "stopsignal";
    private const string SkillDemolition = "demolition";
    private const string SkillSafeZone = "safezone";

    private const string SkillNoiseMaker = "noisemaker";
    private const string SkillHologram = "hologram";

    private const string SkillFakeBox = "fakebox";
    private const string SkillJokerCard = "jokercard";

    private static readonly Regex CoordinateRegex =
        new Regex(@"(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private static readonly string[] DashInstructionKeywords =
    {
        "dash",
        "대시"
    };

    private static readonly string[] SmokeInstructionKeywords =
    {
        "smoke",
        "연막",
        "연막탄"
    };

    private static readonly string[] AccessControlInstructionKeywords =
    {
        "accesscontrol",
        "access control",
        "control zone",
        "security zone",
        "restricted zone",
        "출입 통제",
        "출입통제",
        "통제 구역",
        "통제구역",
        "접근 금지",
        "접근금지",
        "제한 구역",
        "제한구역",
        "금지 구역",
        "금지구역"
    };

    private static readonly string[] EscapeBlockInstructionKeywords =
    {
        "escapeblock",
        "escape block",
        "escape skill block",
        "escape blocking",
        "block escape",
        "도주 제지",
        "도주제지",
        "도주 스킬 차단",
        "도주스킬차단",
        "도주 차단",
        "도주차단",
        "탈출 차단",
        "탈출차단"
    };

    private static readonly string[] PatrolInstructionKeywords =
    {
        "patrol",
        "patrolling",
        "patrol route",
        "route patrol",
        "순찰",
        "왕복 순찰",
        "왕복순찰"
    };

    private static readonly string[] TrackingInstinctInstructionKeywords =
    {
        "trackinginstinct",
        "tracking instinct",
        "pursuit instinct",
        "추적 본능",
        "추적본능"
    };

    private static readonly string[] DroneInstructionKeywords =
    {
        "drone",
        "uav",
        "드론"
    };

    private static readonly string[] ReconnaissanceInstructionKeywords =
    {
        "reconnaissance",
        "recon",
        "scout",
        "정찰",
        "정찰 드론",
        "드론 정찰"
    };

    private static readonly string[] ObservationSupportInstructionKeywords =
    {
        "observationsupport",
        "observation support",
        "vision support",
        "sight support",
        "관측 지원",
        "관측지원",
        "시야 지원",
        "시야지원",
        "관측 보조",
        "관측보조"
    };

    private static readonly string[] PositionShareInstructionKeywords =
    {
        "positionshare",
        "position share",
        "target position share",
        "share target position",
        "위치 공유",
        "위치공유",
        "타겟 위치 공유",
        "타겟위치공유",
        "타겟 위치 알려",
        "타겟 위치를 알려",
        "발견하면 알려",
        "보이면 알려"
    };

    private static readonly string[] PositionShareOffKeywords =
    {
        "_off",
        "off",
        "disable",
        "꺼",
        "끄",
        "중지",
        "비활성",
        "하지마",
        "하지 마"
    };

    private static readonly string[] BarricadeInstructionKeywords =
    {
        "barricade",
        "바리케이드",
        "바리케이트",
        "봉쇄",
        "장애물",
        "장애물 설치",
        "길막",
        "길 막",
        "막아",
        "막기"
    };

    private static readonly string[] StopSignalInstructionKeywords =
    {
        "stopsignal",
        "stop signal",
        "stop sign",
        "stop signal device",
        "slowtrap",
        "slow trap",
        "snaretrap",
        "정지 신호",
        "정지신호",
        "정지 표지",
        "정지표지",
        "정지 장치",
        "정지장치",
        "신호 설치",
        "신호설치",
        "신호를 설치",
        "통제 신호",
        "통제신호",
        "멈춤 신호",
        "멈춤신호",
        "감속 함정",
        "감속함정",
        "구속 함정",
        "구속함정",
        "함정"
    };

    private static readonly string[] DemolitionInstructionKeywords =
    {
        "demolition",
        "demolish",
        "remove obstacle",
        "remove obstacles",
        "철거",
        "장애물 철거"
    };

    private static readonly string[] SafeZoneInstructionKeywords =
    {
        "safezone",
        "safe zone",
        "safe_zone",
        "안전 구역",
        "안전구역"
    };

    private static readonly string[] NoiseMakerInstructionKeywords =
    {
        "noisemaker",
        "noise",
        "소란 장치",
        "소란장치",
        "소음 장치",
        "소음장치",
        "소음 발생기",
        "소란",
        "기계"
    };

    private static readonly string[] HologramInstructionKeywords =
    {
        "hologram",
        "홀로그램",
        "현재 위치",
        "현재위치"
    };

    private static readonly string[] FakeBoxInstructionKeywords =
    {
        "fakebox",
        "fake box",
        "magicbox",
        "magic box",
        "페이크 박스",
        "페이크박스",
        "마술 상자",
        "마술상자",
        "가짜 상자",
        "가짜상자"
    };

    private static readonly string[] JokerCardInstructionKeywords =
    {
        "jokercard",
        "joker card",
        "조커 카드",
        "조커카드"
    };

    private static readonly string[] LookAroundInstructionKeywords =
    {
        "주변",
        "주위",
        "주변 확인",
        "주위 확인",
        "주변 둘러",
        "주위 둘러",
        "주변 살펴",
        "주위 살펴",
        "look around",
        "check around",
        "around",
        "scan",
        "observe"
    };

    private static readonly string[] MovementInstructionKeywords =
    {
        "이동",
        "가 ",
        "가줘",
        "가라",
        "가자",
        "가서",
        "가고",
        "move",
        "go to"
    };

    public string ValidateSkill(string aiSkill, string originalInstruction)
    {
        string normalizedSkill = Normalize(aiSkill);
        string normalizedInstruction = Normalize(originalInstruction);

        if (TryResolvePositionShareSkill(normalizedInstruction, out string positionShareSkill))
            return positionShareSkill;

        if (TryResolveReconnaissanceSkill(normalizedInstruction, normalizedSkill, out string reconnaissanceSkill))
            return reconnaissanceSkill;

        if (TryResolveObservationSupportSkill(normalizedInstruction, normalizedSkill, out string observationSupportSkill))
            return observationSupportSkill;

        if (TryResolveDroneSkill(normalizedInstruction, normalizedSkill, out string droneSkill))
            return droneSkill;

        if (TryResolveAccessControlSkill(normalizedInstruction, normalizedSkill, out string accessControlSkill))
            return accessControlSkill;

        if (TryResolveEscapeBlockSkill(normalizedInstruction, normalizedSkill, out string escapeBlockSkill))
            return escapeBlockSkill;

        if (IsPatrolInstruction(normalizedInstruction))
            return SkillPatrol;

        if (IsTrackingInstinctInstruction(normalizedInstruction))
        {
            Debug.LogWarning("[Commander] 추적 본능은 패시브 스킬이므로 명령으로 직접 사용할 수 없습니다.");
            return SkillHold;
        }

        if (ShouldForceLookAroundFromInstruction(normalizedInstruction))
            return SkillLookAround;

        if (IsDemolitionInstruction(normalizedInstruction))
            return SkillDemolition;

        if (IsSafeZoneInstruction(normalizedInstruction))
            return SkillSafeZone;

        if (IsBarricadeInstruction(normalizedInstruction))
            return SkillBarricade;

        if (IsStopSignalInstruction(normalizedInstruction))
            return SkillStopSignal;

        if (IsFakeBoxInstruction(normalizedInstruction))
            return SkillFakeBox;

        if (IsJokerCardInstruction(normalizedInstruction) ||
            ContainsAny(normalizedSkill, SkillJokerCard, "joker card"))
        {
            Debug.LogWarning("[Commander] 조커 카드는 자동 발동 스킬이므로 명령으로 직접 사용할 수 없습니다.");
            return SkillHold;
        }

        if (ContainsAny(normalizedSkill, SkillDash))
            return MatchOrHold(normalizedInstruction, DashInstructionKeywords, SkillDash, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillSmoke))
            return MatchOrHold(normalizedInstruction, SmokeInstructionKeywords, SkillSmoke, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillReconnaissance, "recon", "scout"))
            return MatchOrHold(normalizedInstruction, ReconnaissanceInstructionKeywords, SkillReconnaissance, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillObservationSupport, "observation support", "vision support"))
            return MatchOrHold(normalizedInstruction, ObservationSupportInstructionKeywords, SkillObservationSupport, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillDrone))
            return MatchOrHold(normalizedInstruction, DroneInstructionKeywords, SkillDrone, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillPositionShareOn, SkillPositionShareOff, "positionshare"))
        {
            if (ContainsAny(normalizedSkill, SkillPositionShareOff, "_off", "off"))
                return SkillPositionShareOff;

            return SkillPositionShareOn;
        }

        if (ContainsAny(normalizedSkill, SkillPatrol, "patrolling", "patrol route"))
            return MatchOrHold(normalizedInstruction, PatrolInstructionKeywords, SkillPatrol, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillTrackingInstinct, "tracking instinct", "pursuit instinct"))
        {
            Debug.LogWarning("[Commander] 추적 본능은 패시브 스킬이므로 명령으로 직접 사용할 수 없습니다.");
            return SkillHold;
        }

        if (ContainsAny(normalizedSkill, SkillDemolition, "demolish", "remove obstacle"))
            return MatchOrHold(normalizedInstruction, DemolitionInstructionKeywords, SkillDemolition, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillSafeZone, "safe zone", "safe_zone"))
            return MatchOrHold(normalizedInstruction, SafeZoneInstructionKeywords, SkillSafeZone, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillBarricade))
            return MatchOrHold(normalizedInstruction, BarricadeInstructionKeywords, SkillBarricade, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillStopSignal, "stop signal", "stop sign", "slowtrap", "slow trap"))
            return SkillStopSignal;

        if (ContainsAny(normalizedSkill, SkillFakeBox, "fake box", "magic box"))
            return MatchOrHold(normalizedInstruction, FakeBoxInstructionKeywords, SkillFakeBox, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillNoiseMaker, "noise"))
            return MatchOrHold(normalizedInstruction, NoiseMakerInstructionKeywords, SkillNoiseMaker, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillHologram))
            return MatchOrHold(normalizedInstruction, HologramInstructionKeywords, SkillHologram, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillLookAround, "look around", "scan", "observe"))
            return MatchOrHold(normalizedInstruction, LookAroundInstructionKeywords, SkillLookAround, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillHold, "wait", "idle"))
            return SkillHold;

        if (string.IsNullOrWhiteSpace(normalizedSkill))
        {
            if (IsPatrolInstruction(normalizedInstruction))
                return SkillPatrol;

            if (IsTrackingInstinctInstruction(normalizedInstruction))
            {
                Debug.LogWarning("[Commander] 추적 본능은 패시브 스킬이므로 명령으로 직접 사용할 수 없습니다.");
                return SkillHold;
            }

            if (IsReconnaissanceInstruction(normalizedInstruction))
                return SkillReconnaissance;

            if (IsObservationSupportInstruction(normalizedInstruction))
                return SkillObservationSupport;

            if (IsLookAroundInstruction(normalizedInstruction))
                return SkillLookAround;

            if (ContainsAny(normalizedInstruction, DroneInstructionKeywords))
                return SkillDrone;

            if (IsDemolitionInstruction(normalizedInstruction))
                return SkillDemolition;

            if (IsSafeZoneInstruction(normalizedInstruction))
                return SkillSafeZone;

            if (IsBarricadeInstruction(normalizedInstruction))
                return SkillBarricade;

            if (IsStopSignalInstruction(normalizedInstruction))
                return SkillStopSignal;

            if (IsFakeBoxInstruction(normalizedInstruction))
                return SkillFakeBox;

            if (IsJokerCardInstruction(normalizedInstruction))
            {
                Debug.LogWarning("[Commander] 조커 카드는 자동 발동 스킬이므로 명령으로 직접 사용할 수 없습니다.");
                return SkillHold;
            }

            if (IsMovementInstruction(normalizedInstruction))
                return SkillMove;

            return SkillHold;
        }

        Debug.LogWarning($"[Commander] 알 수 없는 skill='{aiSkill}'를 이동으로 처리하지 않고 대기 상태로 전환합니다. 원문: {originalInstruction}");
        return SkillHold;
    }

    public bool IsLookAroundInstruction(string source)
    {
        return ContainsAny(Normalize(source), LookAroundInstructionKeywords);
    }

    public bool IsPatrolInstruction(string source)
    {
        return ContainsAny(Normalize(source), PatrolInstructionKeywords);
    }

    public bool IsTrackingInstinctInstruction(string source)
    {
        return ContainsAny(Normalize(source), TrackingInstinctInstructionKeywords);
    }

    public bool IsReconnaissanceInstruction(string source)
    {
        return ContainsAny(Normalize(source), ReconnaissanceInstructionKeywords);
    }

    public bool IsObservationSupportInstruction(string source)
    {
        return ContainsAny(Normalize(source), ObservationSupportInstructionKeywords);
    }

    public bool IsMovementInstruction(string source)
    {
        string normalized = Normalize(source);

        if (ContainsCoordinate(normalized))
            return true;

        return ContainsAny(normalized, MovementInstructionKeywords);
    }

    public bool IsBarricadeInstruction(string source)
    {
        return ContainsAny(Normalize(source), BarricadeInstructionKeywords);
    }

    public bool IsStopSignalInstruction(string source)
    {
        return ContainsAny(Normalize(source), StopSignalInstructionKeywords);
    }

    public bool IsDemolitionInstruction(string source)
    {
        return ContainsAny(Normalize(source), DemolitionInstructionKeywords);
    }

    public bool IsSafeZoneInstruction(string source)
    {
        return ContainsAny(Normalize(source), SafeZoneInstructionKeywords);
    }

    public bool IsFakeBoxInstruction(string source)
    {
        return ContainsAny(Normalize(source), FakeBoxInstructionKeywords);
    }

    public bool IsJokerCardInstruction(string source)
    {
        return ContainsAny(Normalize(source), JokerCardInstructionKeywords);
    }

    public bool ContainsCoordinate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        return CoordinateRegex.IsMatch(source);
    }

    public bool TryExtractCoordinate(string source, out float x, out float z)
    {
        x = 0f;
        z = 0f;

        if (string.IsNullOrWhiteSpace(source))
            return false;

        Match match = CoordinateRegex.Match(source);

        if (!match.Success)
            return false;

        bool parsedX = float.TryParse(
            match.Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out x
        );

        bool parsedZ = float.TryParse(
            match.Groups[2].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out z
        );

        return parsedX && parsedZ;
    }

    public bool TryExtractCoordinates(string source, out List<Vector2> coordinates)
    {
        coordinates = new List<Vector2>();

        if (string.IsNullOrWhiteSpace(source))
            return false;

        MatchCollection matches = CoordinateRegex.Matches(source);

        if (matches == null || matches.Count == 0)
            return false;

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            bool parsedX = float.TryParse(
                match.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float x
            );

            bool parsedZ = float.TryParse(
                match.Groups[2].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float z
            );

            if (!parsedX || !parsedZ)
                continue;

            coordinates.Add(new Vector2(x, z));
        }

        return coordinates.Count > 0;
    }

    private bool TryResolveAccessControlSkill(
        string normalizedInstruction,
        string normalizedSkill,
        out string skill)
    {
        skill = "";

        bool hasAccessControlKeyword =
            ContainsAny(normalizedInstruction, AccessControlInstructionKeywords) ||
            ContainsAny(normalizedSkill, SkillAccessControl, "access control", "controlzone", "control zone");

        if (!hasAccessControlKeyword)
            return false;

        skill = SkillAccessControl;
        return true;
    }

    private bool TryResolveEscapeBlockSkill(
        string normalizedInstruction,
        string normalizedSkill,
        out string skill)
    {
        skill = "";

        bool hasEscapeBlockKeyword =
            ContainsAny(normalizedInstruction, EscapeBlockInstructionKeywords) ||
            ContainsAny(normalizedSkill, SkillEscapeBlock, "escape block", "escape skill block");

        if (!hasEscapeBlockKeyword)
            return false;

        skill = SkillEscapeBlock;
        return true;
    }

    private bool TryResolveDroneSkill(
        string normalizedInstruction,
        string normalizedSkill,
        out string skill)
    {
        skill = "";

        bool instructionRequestsDrone = ContainsAny(normalizedInstruction, DroneInstructionKeywords);
        bool aiReturnedDrone = ContainsAny(normalizedSkill, SkillDrone);

        if (!instructionRequestsDrone && !aiReturnedDrone)
            return false;

        if (!instructionRequestsDrone)
        {
            Debug.LogWarning($"[Commander] 원문에 드론 요청이 없어 skill='{normalizedSkill}'를 무시합니다.");
            skill = SkillHold;
            return true;
        }

        skill = SkillDrone;
        return true;
    }

    private bool TryResolveReconnaissanceSkill(
        string normalizedInstruction,
        string normalizedSkill,
        out string skill)
    {
        skill = "";

        bool instructionRequestsReconnaissance =
            ContainsAny(normalizedInstruction, ReconnaissanceInstructionKeywords);

        bool aiReturnedReconnaissance =
            ContainsAny(normalizedSkill, SkillReconnaissance, "recon", "scout");

        if (!instructionRequestsReconnaissance && !aiReturnedReconnaissance)
            return false;

        if (!instructionRequestsReconnaissance)
        {
            Debug.LogWarning($"[Commander] 원문에 정찰 요청이 없어 skill='{normalizedSkill}'를 무시합니다.");
            skill = SkillHold;
            return true;
        }

        skill = SkillReconnaissance;
        return true;
    }

    private bool TryResolveObservationSupportSkill(
        string normalizedInstruction,
        string normalizedSkill,
        out string skill)
    {
        skill = "";

        bool instructionRequestsObservationSupport =
            ContainsAny(normalizedInstruction, ObservationSupportInstructionKeywords);

        bool aiReturnedObservationSupport =
            ContainsAny(normalizedSkill, SkillObservationSupport, "observation support", "vision support");

        if (!instructionRequestsObservationSupport && !aiReturnedObservationSupport)
            return false;

        if (!instructionRequestsObservationSupport)
        {
            Debug.LogWarning($"[Commander] 원문에 관측 지원 요청이 없어 skill='{normalizedSkill}'를 무시합니다.");
            skill = SkillHold;
            return true;
        }

        skill = SkillObservationSupport;
        return true;
    }

    private bool TryResolvePositionShareSkill(string normalizedInstruction, out string skill)
    {
        skill = "";

        if (!ContainsAny(normalizedInstruction, PositionShareInstructionKeywords))
            return false;

        if (ContainsAny(normalizedInstruction, PositionShareOffKeywords))
        {
            skill = SkillPositionShareOff;
            return true;
        }

        skill = SkillPositionShareOn;
        return true;
    }

    private bool ShouldForceLookAroundFromInstruction(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        if (!IsLookAroundInstruction(source))
            return false;

        if (IsMovementInstruction(source))
            return false;

        return true;
    }

    private string MatchOrHold(
        string normalizedInstruction,
        string[] requiredKeywords,
        string successSkill,
        string aiSkill,
        string originalInstruction)
    {
        if (ContainsAny(normalizedInstruction, requiredKeywords))
            return successSkill;

        Debug.LogWarning($"[Commander] 원문에 {successSkill} 요청이 없어 skill='{aiSkill}'를 무시합니다. 원문: {originalInstruction}");
        return SkillHold;
    }

    private string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLower();
    }

    private bool ContainsAny(string source, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];

            if (!string.IsNullOrWhiteSpace(keyword) && source.Contains(keyword))
                return true;
        }

        return false;
    }
}