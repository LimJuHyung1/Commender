using System;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class CommandValidator
{
    private const string SkillMove = "";
    private const string SkillHold = "hold";
    private const string SkillLookAround = "lookaround";
    private const string SkillDash = "dash";
    private const string SkillSmoke = "smoke";
    private const string SkillReveal = "reveal";
    private const string SkillWallSight = "wallsight";
    private const string SkillBarricade = "barricade";
    private const string SkillSlowTrap = "slowtrap";
    private const string SkillNoiseMaker = "noisemaker";
    private const string SkillHologram = "hologram";

    private static readonly string[] DashInstructionKeywords =
    {
        "dash", "대시", "대쉬"
    };

    private static readonly string[] SmokeInstructionKeywords =
    {
        "smoke", "연막", "연막탄"
    };

    private static readonly string[] RevealInstructionKeywords =
    {
        "reveal", "recon", "recondrone", "드론", "정찰", "정찰 드론", "정찰드론"
    };

    private static readonly string[] WallSightInstructionKeywords =
    {
        "wallsight", "truesight", "투시", "벽 너머", "벽너머", "시야"
    };

    private static readonly string[] BarricadeInstructionKeywords =
    {
        "barricade", "바리케이드", "바리게이트", "봉쇄", "장애물"
    };

    private static readonly string[] SlowTrapInstructionKeywords =
    {
        "slowtrap", "snaretrap", "trap", "함정", "정지 함정", "구속 함정", "속박 함정", "트랩"
    };

    private static readonly string[] NoiseMakerInstructionKeywords =
    {
        "noisemaker", "noise", "소란 장치", "장치", "소란", "기계"
    };

    private static readonly string[] HologramInstructionKeywords =
    {
        "hologram", "홀로그램", "현재 위치", "현재위치", "위치"
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
        "around"
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

        if (ShouldForceLookAroundFromInstruction(normalizedInstruction))
            return SkillLookAround;

        if (ContainsAny(normalizedSkill, SkillDash))
            return MatchOrHold(normalizedInstruction, DashInstructionKeywords, SkillDash, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillSmoke))
            return MatchOrHold(normalizedInstruction, SmokeInstructionKeywords, SkillSmoke, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillReveal, "recon", "recondrone"))
            return MatchOrHold(normalizedInstruction, RevealInstructionKeywords, SkillReveal, aiSkill, originalInstruction);

        if (ContainsAny(normalizedSkill, SkillWallSight, "truesight"))
            return MatchOrHold(normalizedInstruction, WallSightInstructionKeywords, SkillWallSight, aiSkill, originalInstruction);

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

        Debug.LogWarning($"[Commender] 알 수 없는 skill='{aiSkill}' 를 이동으로 처리하지 않고 대기 상태로 전환합니다. 원문: {originalInstruction}");
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

    public bool ContainsCoordinate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        return Regex.IsMatch(source, @"-?\d+(\.\d+)?\s*,\s*-?\d+(\.\d+)?");
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

        Debug.LogWarning($"[Commender] 원문에 {successSkill} 요청이 없어 skill='{aiSkill}' 를 무시합니다. 원문: {originalInstruction}");
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