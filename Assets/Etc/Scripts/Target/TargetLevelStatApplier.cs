using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TargetController))]
[RequireComponent(typeof(TargetEscapeMotor))]
public class TargetLevelStatApplier : MonoBehaviour
{
    [Header("Target Level")]
    [Range(1, 10)] public int targetLevel = 1;
    public bool applyOnStart = true;
    public bool refillHealthOnApply = true;

    private TargetController targetController;
    private TargetEscapeMotor escapeMotor;

    private static readonly TargetLevelStats[] LevelStats =
    {
        new TargetLevelStats(100f, 10.0f, 10f, 10.0f, 30f, 1080f, 24f, 5),
        new TargetLevelStats(100f, 9.4f, 12f, 10.5f, 32f, 1080f, 25f, 5),
        new TargetLevelStats(100f, 8.9f, 14f, 11.0f, 34f, 1080f, 25f, 5),
        new TargetLevelStats(100f, 8.3f, 16f, 11.5f, 37f, 1080f, 26f, 5),
        new TargetLevelStats(100f, 7.8f, 18f, 12.0f, 39f, 1080f, 27f, 5),
        new TargetLevelStats(100f, 7.2f, 21f, 12.5f, 41f, 1080f, 27f, 5),
        new TargetLevelStats(100f, 6.7f, 23f, 13.0f, 43f, 1080f, 28f, 5),
        new TargetLevelStats(100f, 6.1f, 25f, 13.5f, 46f, 1080f, 29f, 5),
        new TargetLevelStats(100f, 5.6f, 28f, 14.0f, 48f, 1080f, 29f, 5),
        new TargetLevelStats(100f, 5.0f, 30f, 15.0f, 50f, 1080f, 30f, 5)
    };

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (applyOnStart)
            ApplyLevel(targetLevel, refillHealthOnApply);
    }

    private void OnValidate()
    {
        targetLevel = Mathf.Clamp(targetLevel, 1, 10);
    }

    public void ApplyFromStageNumber(int stageNumber)
    {
        int level = Mathf.Clamp(stageNumber, 1, 10);
        ApplyLevel(level, true);
    }

    public void ApplyLevel(int level, bool refillHealth)
    {
        ResolveReferences();

        targetLevel = Mathf.Clamp(level, 1, 10);

        TargetLevelStats stats = LevelStats[targetLevel - 1];

        ApplyControllerStats(stats, refillHealth);
        ApplyEscapeStats(stats);
    }

    private void ResolveReferences()
    {
        if (targetController == null)
            targetController = GetComponent<TargetController>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();
    }

    private void ApplyControllerStats(TargetLevelStats stats, bool refillHealth)
    {
        if (targetController == null)
            return;

        float fleeHealthDrain = targetController.fleeHealthDrainPerSecond;
        float recoveryDuration = targetController.recoveryDuration;

        targetController.ApplyBaseStats(
            stats.maxHealth,
            stats.maxHealth,
            fleeHealthDrain,
            stats.recoveryDelayAfterSafe,
            stats.recoveryAmountTotal,
            recoveryDuration,
            refillHealth
        );
    }

    private void ApplyEscapeStats(TargetLevelStats stats)
    {
        if (escapeMotor == null)
            return;

        EscapeSettings settings = escapeMotor.settings != null
            ? escapeMotor.settings.Clone()
            : new EscapeSettings();

        settings.fleeMoveSpeed = stats.fleeMoveSpeed;
        settings.fleeAcceleration = stats.fleeAcceleration;
        settings.fleeAngularSpeed = stats.fleeAngularSpeed;

        settings.safeSearchRadius = stats.safeSearchRadius;
        settings.safePointSampleCount = stats.safePointSampleCount;

        escapeMotor.ApplySettings(settings);
    }

    private struct TargetLevelStats
    {
        public float maxHealth;
        public float recoveryDelayAfterSafe;
        public float recoveryAmountTotal;

        public float fleeMoveSpeed;
        public float fleeAcceleration;
        public float fleeAngularSpeed;

        public float safeSearchRadius;
        public int safePointSampleCount;

        public TargetLevelStats(
            float maxHealth,
            float recoveryDelayAfterSafe,
            float recoveryAmountTotal,
            float fleeMoveSpeed,
            float fleeAcceleration,
            float fleeAngularSpeed,
            float safeSearchRadius,
            int safePointSampleCount)
        {
            this.maxHealth = maxHealth;
            this.recoveryDelayAfterSafe = recoveryDelayAfterSafe;
            this.recoveryAmountTotal = recoveryAmountTotal;
            this.fleeMoveSpeed = fleeMoveSpeed;
            this.fleeAcceleration = fleeAcceleration;
            this.fleeAngularSpeed = fleeAngularSpeed;
            this.safeSearchRadius = safeSearchRadius;
            this.safePointSampleCount = safePointSampleCount;
        }
    }
}