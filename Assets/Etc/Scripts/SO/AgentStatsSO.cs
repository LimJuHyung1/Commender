using UnityEngine;

public enum AgentRole
{
    Pursuer,
    Scout,
    Engineer,
    Disruptor
}

[CreateAssetMenu(fileName = "AgentStats", menuName = "Commender/Agent Stats")]
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

    [Header("정찰병")]
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
}