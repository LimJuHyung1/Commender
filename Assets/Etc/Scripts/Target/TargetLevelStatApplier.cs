using UnityEngine;

public enum TargetType
{
    InformationBroker,
    ConstructionWorker,
    GraffitiArtist
}

[DisallowMultipleComponent]
[RequireComponent(typeof(TargetController))]
[RequireComponent(typeof(TargetEscapeMotor))]
public class TargetLevelStatApplier : MonoBehaviour
{
    [Header("Target Type")]
    [SerializeField] private TargetType targetType = TargetType.InformationBroker;

    [Header("Target Level")]
    [SerializeField][Range(1, 10)] private int targetLevel = 1;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool refillHealthOnApply = true;

    [Header("Fixed Controller Stats")]
    [SerializeField] private float fleeHealthDrainPerSecond = 8f;
    [SerializeField] private float recoveryDuration = 1.5f;

    private TargetController targetController;
    private TargetEscapeMotor escapeMotor;
    private TargetSkillController skillController;

    public TargetType TargetType => targetType;
    public int TargetLevel => targetLevel;

    private static readonly TargetLevelStats[] InformationBrokerStats =
    {
        new TargetLevelStats(100f, 10.0f, 10f, 9.8f, 30f, 1080f, 24f, 5),
        new TargetLevelStats(100f, 9.4f, 12f, 10.2f, 32f, 1080f, 25f, 5),
        new TargetLevelStats(100f, 8.9f, 14f, 10.6f, 34f, 1080f, 25f, 5),
        new TargetLevelStats(100f, 8.3f, 16f, 11.0f, 37f, 1080f, 26f, 5),
        new TargetLevelStats(100f, 7.8f, 18f, 11.4f, 39f, 1080f, 27f, 5),
        new TargetLevelStats(100f, 7.2f, 21f, 11.8f, 41f, 1080f, 27f, 5),
        new TargetLevelStats(100f, 6.7f, 23f, 12.2f, 43f, 1080f, 28f, 5),
        new TargetLevelStats(100f, 6.1f, 25f, 12.6f, 46f, 1080f, 29f, 5),
        new TargetLevelStats(100f, 5.6f, 28f, 13.0f, 48f, 1080f, 29f, 5),
        new TargetLevelStats(100f, 5.0f, 30f, 13.5f, 50f, 1080f, 30f, 5)
    };

    private static readonly TargetLevelStats[] ConstructionWorkerStats =
    {
        new TargetLevelStats(110f, 10.5f, 10f, 9.2f, 28f, 900f, 23f, 5),
        new TargetLevelStats(110f, 10.0f, 12f, 9.6f, 30f, 900f, 24f, 5),
        new TargetLevelStats(110f, 9.4f, 14f, 10.0f, 32f, 900f, 25f, 5),
        new TargetLevelStats(110f, 8.8f, 16f, 10.4f, 35f, 900f, 26f, 5),
        new TargetLevelStats(110f, 8.2f, 18f, 10.8f, 37f, 900f, 27f, 5),
        new TargetLevelStats(110f, 7.6f, 21f, 11.2f, 39f, 900f, 27f, 5),
        new TargetLevelStats(110f, 7.0f, 23f, 11.6f, 42f, 900f, 28f, 5),
        new TargetLevelStats(110f, 6.5f, 25f, 12.0f, 44f, 900f, 29f, 5),
        new TargetLevelStats(110f, 6.0f, 28f, 12.4f, 46f, 900f, 30f, 5),
        new TargetLevelStats(110f, 5.5f, 30f, 12.8f, 48f, 900f, 31f, 5)
    };

    private static readonly TargetLevelStats[] GraffitiArtistStats =
    {
        new TargetLevelStats(95f, 9.5f, 9f, 10.5f, 34f, 1260f, 25f, 5),
        new TargetLevelStats(95f, 8.9f, 11f, 11.0f, 36f, 1260f, 26f, 5),
        new TargetLevelStats(95f, 8.3f, 13f, 11.5f, 38f, 1260f, 27f, 5),
        new TargetLevelStats(95f, 7.7f, 15f, 12.0f, 41f, 1260f, 28f, 5),
        new TargetLevelStats(95f, 7.1f, 17f, 12.5f, 43f, 1260f, 29f, 5),
        new TargetLevelStats(95f, 6.5f, 20f, 13.0f, 45f, 1260f, 29f, 5),
        new TargetLevelStats(95f, 6.0f, 22f, 13.5f, 48f, 1260f, 30f, 5),
        new TargetLevelStats(95f, 5.5f, 24f, 14.0f, 50f, 1260f, 31f, 5),
        new TargetLevelStats(95f, 5.1f, 26f, 14.5f, 52f, 1260f, 31f, 5),
        new TargetLevelStats(95f, 4.7f, 28f, 15.0f, 55f, 1260f, 32f, 5)
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
        fleeHealthDrainPerSecond = Mathf.Max(0f, fleeHealthDrainPerSecond);
        recoveryDuration = Mathf.Max(0.01f, recoveryDuration);
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

        TargetLevelStats stats = GetStats(targetType, targetLevel);

        ApplyControllerStats(stats, refillHealth);
        ApplyEscapeStats(stats);
        ApplySkillUnlocks(targetLevel);
    }

    public void ApplyCurrentLevel(bool refillHealth = true)
    {
        ApplyLevel(targetLevel, refillHealth);
    }

    public void SetTargetType(TargetType newTargetType, bool applyImmediately = true)
    {
        targetType = newTargetType;

        if (applyImmediately)
            ApplyLevel(targetLevel, refillHealthOnApply);
    }

    public void SetTargetLevel(int newLevel, bool refillHealth = true)
    {
        ApplyLevel(newLevel, refillHealth);
    }

    private void ResolveReferences()
    {
        if (targetController == null)
            targetController = GetComponent<TargetController>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (skillController == null)
            skillController = GetComponent<TargetSkillController>();
    }

    private TargetLevelStats GetStats(TargetType type, int level)
    {
        int index = Mathf.Clamp(level, 1, 10) - 1;

        switch (type)
        {
            case TargetType.InformationBroker:
                return InformationBrokerStats[index];

            case TargetType.ConstructionWorker:
                return ConstructionWorkerStats[index];

            case TargetType.GraffitiArtist:
                return GraffitiArtistStats[index];

            default:
                return InformationBrokerStats[index];
        }
    }

    private void ApplyControllerStats(TargetLevelStats stats, bool refillHealth)
    {
        if (targetController == null)
            return;

        targetController.ApplyBaseStats(
            stats.maxHealth,
            stats.maxHealth,
            fleeHealthDrainPerSecond,
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

    private void ApplySkillUnlocks(int level)
    {
        if (skillController == null)
            return;

        skillController.ApplySkillUnlocks(level);
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