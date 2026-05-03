using System;
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

    private const string SkillFlare = "flare";
    private const string SkillPositionShareOn = "positionshare_on";
    private const string SkillPositionShareOff = "positionshare_off";

    private const string SkillBarricade = "barricade";
    private const string SkillSlowTrap = "slowtrap";

    private const string SkillNoiseMaker = "noisemaker";
    private const string SkillHologram = "hologram";

    private static readonly Regex CoordinateRegex =
        new Regex(@"(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private static readonly string[] DashInstructionKeywords =
    {
        "dash",
        "대쉬",
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

    private static readonly string[] FlareInstructionKeywords =
    {
        "flare",
        "signal flare",
        "signalflare",
        "조명탄",
        "신호탄",
        "플레어"
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
        "끄",
        "꺼",
        "중지",
        "비활성",
        "하지마",
        "하지 마"
    };

    private static readonly string[] BarricadeInstructionKeywords =
    {
        "barricade",
        "바리케이드",
        "봉쇄",
        "장애물"
    };

    private static readonly string[] SlowTrapInstructionKeywords =
    {
        "slowtrap",
        "snaretrap",
        "trap",
        "트랩",
        "함정",
        "정지 함정",
        "구속 함정",
        "속박 함정",
        "트랩 설치",
        "함정 설치"
    };

    private static readonly string[] NoiseMakerInstructionKeywords =
    {
        "noisemaker",
        "noise",
        "소란 장치",
        "소란장치",
        "장치",
        "소란",
        "기계"
    };

    private static readonly string[] HologramInstructionKeywords =
    {
        "hologram",
        "홀로그램",
        "현재 위치",
        "현재위치",
        "위치"
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

        if (TryResolveAccessControlSkill(normalizedInstruction, normalizedSkill, out string accessControlSkill))
            return accessControlSkill;

        if (TryResolveEscapeBlockSkill(normalizedInstruction, normalizedSkill, out string escapeBlockSkill))
            return escapeBlockSkill;

        if (ShouldForceLookAroundFromInstruction(normalizedInstruction))
            return SkillLookAround;

        if (IsTrapInstruction(normalizedInstruction))
            return SkillSlowTrap;

        if (ContainsAny(normalizedSkill, SkillDash))
            return MatchOrHold(normalizedInstruction, DashInstructionKeywords, SkillDash, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillSmoke))
            return MatchOrHold(normalizedInstruction, SmokeInstructionKeywords, SkillSmoke, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillFlare, "signalflare"))
            return MatchOrHold(normalizedInstruction, FlareInstructionKeywords, SkillFlare, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillPositionShareOn, SkillPositionShareOff, "positionshare"))
        {
            if (ContainsAny(normalizedSkill, SkillPositionShareOff, "_off", "off"))
                return SkillPositionShareOff;

            return SkillPositionShareOn;
        }

        if (ContainsAny(normalizedSkill, SkillBarricade))
            return MatchOrHold(normalizedInstruction, BarricadeInstructionKeywords, SkillBarricade, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillSlowTrap, "snaretrap", "trap"))
            return MatchOrHold(normalizedInstruction, SlowTrapInstructionKeywords, SkillSlowTrap, aiSkill, originalInstruction);

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
            if (IsLookAroundInstruction(normalizedInstruction))
                return SkillLookAround;

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

    public bool IsMovementInstruction(string source)
    {
        string normalized = Normalize(source);

        if (ContainsCoordinate(normalized))
            return true;

        return ContainsAny(normalized, MovementInstructionKeywords);
    }

    public bool IsTrapInstruction(string source)
    {
        return ContainsAny(Normalize(source), SlowTrapInstructionKeywords);
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