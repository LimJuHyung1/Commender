using System;
using System.Collections.Generic;
using CodeMonkey.HealthSystemCM;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(TargetThreatTracker))]
[RequireComponent(typeof(TargetEscapeMotor))]
public class TargetController : MonoBehaviour, IGetHealthSystem
{
    [Serializable]
    private class DifficultyAnchor
    {
        [Min(1)]
        public int stageNumber = 1;

        [Range(0f, 1f)]
        public float catchDifficulty = 0f;

        public bool isBossStage = false;
    }

    private struct RuntimeDifficultyProfile
    {
        public float maxHealth;
        public float startHealth;
        public float fleeHealthDrainPerSecond;

        public float recoveryDelayAfterStop;
        public float recoveryAmountTotal;
        public float recoveryDuration;

        public ThreatSettings threatSettings;
        public EscapeSettings escapeSettings;
    }

    [Header("References")]
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private TargetEscapeMotor escapeMotor;

    [Header("Difficulty")]
    [SerializeField] private bool applyDifficultyOnAwake = true;
    [SerializeField][Min(1)] private int currentStageNumber = 1;
    [SerializeField] private List<DifficultyAnchor> difficultyAnchors = new List<DifficultyAnchor>();

    private NavMeshAgent navAgent;
    private HealthSystem healthSystem;

    private float maxHealth = 100f;
    private float startHealth = 100f;
    private float fleeHealthDrainPerSecond = 12f;
    private float recoveryDelayAfterStop = 10f;
    private float recoveryAmountTotal = 20f;
    private float recoveryDuration = 2f;

    private float stoppedRecoveryTimer = 0f;
    private bool isRecoveringAfterStop = false;
    private float recoveryStartHealth = 0f;

    public bool IsRevealedToPlayer => threatTracker != null && threatTracker.IsRevealedToPlayer;
    public bool IsRooted => escapeMotor != null && escapeMotor.IsRooted;
    public bool HasUsedEmergencyEscape => escapeMotor != null && escapeMotor.HasUsedEmergencyEscape;
    public bool IsEmergencyEscaping => escapeMotor != null && escapeMotor.IsEmergencyEscaping;
    public bool CanBeCaught => escapeMotor == null || escapeMotor.CanBeCaught;
    public int RemainingEmergencyEscapeCount => escapeMotor != null ? escapeMotor.RemainingEmergencyEscapeCount : 0;

    public float CurrentHealth => healthSystem != null ? healthSystem.GetHealth() : 0f;
    public float MaxHealth => healthSystem != null ? healthSystem.GetHealthMax() : maxHealth;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (escapeMotor != null && threatTracker != null)
            escapeMotor.SetThreatTracker(threatTracker);

        if (applyDifficultyOnAwake)
            ApplyDifficultyForStage(currentStageNumber, false);
        else
            RecreateHealthSystem();
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
            healthSystem.OnDead -= HealthSystem_OnDead;
    }

    private void OnValidate()
    {
        if (currentStageNumber < 1)
            currentStageNumber = 1;

        if (difficultyAnchors == null)
            return;

        for (int i = 0; i < difficultyAnchors.Count; i++)
        {
            if (difficultyAnchors[i] == null)
                continue;

            if (difficultyAnchors[i].stageNumber < 1)
                difficultyAnchors[i].stageNumber = 1;

            difficultyAnchors[i].catchDifficulty = Mathf.Clamp01(difficultyAnchors[i].catchDifficulty);
        }
    }

    private void Update()
    {
        if (escapeMotor == null || threatTracker == null)
            return;

        if (escapeMotor.IsRooted)
            return;

        if (healthSystem == null || healthSystem.IsDead())
            return;

        bool hasThreat = threatTracker.HasAnyThreat();

        escapeMotor.RefreshDynamicMovementSettings(hasThreat, GetHealthRatio());

        if (hasThreat)
        {
            stoppedRecoveryTimer = 0f;
            isRecoveringAfterStop = false;

            escapeMotor.TryAutoEmergencyEscape(GetHealthRatio());

            float damageAmount = fleeHealthDrainPerSecond * Time.deltaTime;
            healthSystem.Damage(damageAmount);

            if (healthSystem != null && !healthSystem.IsDead())
                escapeMotor.TryFleeFromThreats();
        }
        else
        {
            HandleStoppedRecovery();
        }
    }

    [ContextMenu("Apply Current Stage Difficulty")]
    public void ApplyCurrentStageDifficulty()
    {
        ApplyDifficultyForStage(currentStageNumber, true);
    }

    public void SetStageNumber(int stageNumber)
    {
        currentStageNumber = Mathf.Max(1, stageNumber);
    }

    public void ApplyDifficultyForCurrentStage()
    {
        ApplyDifficultyForStage(currentStageNumber, true);
    }

    public void ApplyDifficultyForStage(int stageNumber)
    {
        ApplyDifficultyForStage(stageNumber, true);
    }

    private void ApplyDifficultyForStage(int stageNumber, bool writeLog)
    {
        currentStageNumber = Mathf.Max(1, stageNumber);

        DifficultyAnchor resolved = ResolveAnchor(currentStageNumber);
        if (resolved == null)
        {
            Debug.LogWarning("[Target] 적용할 difficulty anchor를 찾지 못했습니다.");
            RecreateHealthSystem();
            return;
        }

        RuntimeDifficultyProfile profile = BuildProfile(resolved.catchDifficulty, resolved.isBossStage);

        maxHealth = profile.maxHealth;
        startHealth = profile.startHealth;
        fleeHealthDrainPerSecond = profile.fleeHealthDrainPerSecond;
        recoveryDelayAfterStop = profile.recoveryDelayAfterStop;
        recoveryAmountTotal = profile.recoveryAmountTotal;
        recoveryDuration = profile.recoveryDuration;

        if (threatTracker != null)
            threatTracker.ApplySettings(profile.threatSettings);

        if (escapeMotor != null)
        {
            escapeMotor.ApplySettings(profile.escapeSettings);
            escapeMotor.ResetRuntimeState(true);
        }

        stoppedRecoveryTimer = 0f;
        isRecoveringAfterStop = false;
        recoveryStartHealth = 0f;

        RecreateHealthSystem();

        if (navAgent != null && healthSystem != null && !healthSystem.IsDead())
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }

        if (writeLog)
        {
            Debug.Log(
                $"[Target] Stage {currentStageNumber} 난이도 적용 완료 " +
                $"(catchDifficulty={resolved.catchDifficulty:F2}, boss={resolved.isBossStage})"
            );
        }
    }

    private DifficultyAnchor ResolveAnchor(int stageNumber)
    {
        List<DifficultyAnchor> sortedAnchors = GetSortedAnchors();
        if (sortedAnchors.Count == 0)
            return null;

        for (int i = 0; i < sortedAnchors.Count; i++)
        {
            if (sortedAnchors[i].stageNumber == stageNumber)
                return CloneAnchor(sortedAnchors[i]);
        }

        if (stageNumber <= sortedAnchors[0].stageNumber)
            return CloneAnchor(sortedAnchors[0]);

        if (stageNumber >= sortedAnchors[sortedAnchors.Count - 1].stageNumber)
            return CloneAnchor(sortedAnchors[sortedAnchors.Count - 1]);

        DifficultyAnchor lower = null;
        DifficultyAnchor upper = null;

        for (int i = 0; i < sortedAnchors.Count - 1; i++)
        {
            DifficultyAnchor current = sortedAnchors[i];
            DifficultyAnchor next = sortedAnchors[i + 1];

            if (current.stageNumber < stageNumber && stageNumber < next.stageNumber)
            {
                lower = current;
                upper = next;
                break;
            }
        }

        if (lower == null || upper == null)
            return CloneAnchor(sortedAnchors[0]);

        float t = Mathf.InverseLerp(lower.stageNumber, upper.stageNumber, stageNumber);
        return LerpAnchor(lower, upper, t, stageNumber);
    }

    private List<DifficultyAnchor> GetSortedAnchors()
    {
        List<DifficultyAnchor> sorted = new List<DifficultyAnchor>();

        if (difficultyAnchors == null)
            return sorted;

        for (int i = 0; i < difficultyAnchors.Count; i++)
        {
            if (difficultyAnchors[i] != null)
                sorted.Add(difficultyAnchors[i]);
        }

        sorted.Sort((a, b) => a.stageNumber.CompareTo(b.stageNumber));
        return sorted;
    }

    private DifficultyAnchor CloneAnchor(DifficultyAnchor source)
    {
        if (source == null)
            return null;

        return new DifficultyAnchor
        {
            stageNumber = source.stageNumber,
            catchDifficulty = Mathf.Clamp01(source.catchDifficulty),
            isBossStage = source.isBossStage
        };
    }

    private DifficultyAnchor LerpAnchor(DifficultyAnchor a, DifficultyAnchor b, float t, int targetStageNumber)
    {
        return new DifficultyAnchor
        {
            stageNumber = targetStageNumber,
            catchDifficulty = Mathf.Lerp(a.catchDifficulty, b.catchDifficulty, t),
            isBossStage = false
        };
    }

    private RuntimeDifficultyProfile BuildProfile(float catchDifficulty, bool isBossStage)
    {
        float t = Mathf.Clamp01(catchDifficulty);

        RuntimeDifficultyProfile profile = new RuntimeDifficultyProfile();

        profile.maxHealth = Mathf.Lerp(90f, 150f, t);
        profile.startHealth = profile.maxHealth;

        profile.fleeHealthDrainPerSecond = Mathf.Lerp(16f, 8f, t);

        profile.recoveryDelayAfterStop = Mathf.Lerp(12f, 5f, t);
        profile.recoveryAmountTotal = Mathf.Lerp(5f, 25f, t);
        profile.recoveryDuration = Mathf.Lerp(3.5f, 1.5f, t);

        profile.threatSettings = new ThreatSettings
        {
            detectionRadius = Mathf.Lerp(4.5f, 7.5f, t) * 1.5f,
            reconDroneWeight = Mathf.Lerp(0.4f, 1.0f, t),
            reconDroneInfluenceRadius = Mathf.Lerp(8f, 15f, t),
            hologramInfluenceRadius = Mathf.Lerp(10f, 18f, t)
        };

        profile.escapeSettings = new EscapeSettings
        {
            repathCooldown = Mathf.Lerp(0.35f, 0.18f, t),
            fleeMoveSpeed = Mathf.Lerp(11f, 17f, t),
            fleeAngularSpeed = Mathf.Lerp(720f, 1200f, t),
            fleeAcceleration = Mathf.Lerp(30f, 55f, t),

            minNearestThreatDistanceGain = Mathf.Lerp(0.2f, 0.8f, t),
            minPathStartAlignment = Mathf.Lerp(-0.2f, 0.05f, t),

            safeSearchRadius = Mathf.Lerp(14f, 24f, t),
            safePointSampleCount = Mathf.RoundToInt(Mathf.Lerp(10f, 24f, t)),
            safePointMinDistance = Mathf.Lerp(4f, 9f, t),
            navMeshSampleRadius = Mathf.Lerp(3f, 5f, t),
            fleeDirectionBias = Mathf.Lerp(1.0f, 2.5f, t),

            usePanicBoost = t >= 0.45f,
            panicHealthThresholdRatio = Mathf.Lerp(0.25f, 0.5f, t),
            panicSpeedMultiplier = Mathf.Lerp(1.05f, 1.3f, t),
            panicSearchRadiusMultiplier = Mathf.Lerp(1.05f, 1.25f, t),
            panicSampleCountBonus = Mathf.RoundToInt(Mathf.Lerp(2f, 8f, t)),
            panicFleeDirectionBiasBonus = Mathf.Lerp(0.2f, 1.0f, t),

            enableEmergencyEscape = isBossStage,
            emergencyEscapeCharges = isBossStage ? 1 : 0,
            autoUseEmergencyEscape = isBossStage,
            emergencyEscapeAutoTriggerHealthRatio = Mathf.Lerp(0.3f, 0.45f, t),
            emergencyEscapeAutoTriggerThreatDistance = Mathf.Lerp(4f, 7f, t),
            emergencyEscapeDuration = Mathf.Lerp(0.6f, 1.0f, t),
            emergencyEscapeSpeed = Mathf.Lerp(18f, 24f, t),
            emergencyEscapeAcceleration = Mathf.Lerp(25f, 40f, t),
            emergencyEscapeRepathInterval = Mathf.Lerp(0.25f, 0.15f, t)
        };

        return profile;
    }

    private void RecreateHealthSystem()
    {
        if (healthSystem != null)
            healthSystem.OnDead -= HealthSystem_OnDead;

        healthSystem = new HealthSystem(maxHealth);

        float clampedStartHealth = Mathf.Clamp(startHealth, 0f, maxHealth);
        healthSystem.SetHealth(clampedStartHealth);
        healthSystem.OnDead += HealthSystem_OnDead;
    }

    private void HealthSystem_OnDead(object sender, EventArgs e)
    {
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        Debug.Log("<color=red>[Target]</color> 체력이 0이 되어 더 이상 이동하지 않습니다.");
    }

    private void HandleStoppedRecovery()
    {
        if (escapeMotor == null || healthSystem == null || healthSystem.IsDead())
            return;

        if (!escapeMotor.IsCompletelyStopped())
        {
            stoppedRecoveryTimer = 0f;
            isRecoveringAfterStop = false;
            return;
        }

        stoppedRecoveryTimer += Time.deltaTime;

        if (stoppedRecoveryTimer < recoveryDelayAfterStop)
            return;

        if (!isRecoveringAfterStop)
        {
            isRecoveringAfterStop = true;
            recoveryStartHealth = healthSystem.GetHealth();
        }

        float targetRecoveryHealth = Mathf.Min(
            recoveryStartHealth + recoveryAmountTotal,
            healthSystem.GetHealthMax()
        );

        if (healthSystem.GetHealth() >= targetRecoveryHealth)
            return;

        float recoverPerSecond = recoveryAmountTotal / Mathf.Max(0.01f, recoveryDuration);
        float recoverAmount = recoverPerSecond * Time.deltaTime;

        float nextHealth = Mathf.Min(
            healthSystem.GetHealth() + recoverAmount,
            targetRecoveryHealth
        );

        healthSystem.SetHealth(nextHealth);
    }

    private float GetHealthRatio()
    {
        if (healthSystem == null)
            return 1f;

        float max = healthSystem.GetHealthMax();
        if (max <= 0.01f)
            return 1f;

        return healthSystem.GetHealth() / max;
    }

    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }

    public void Damage(float amount)
    {
        if (healthSystem == null || healthSystem.IsDead())
            return;

        healthSystem.Damage(amount);
    }

    public void Heal(float amount)
    {
        if (healthSystem == null || healthSystem.IsDead())
            return;

        healthSystem.Heal(amount);
    }

    public void HealComplete()
    {
        if (healthSystem == null)
            return;

        healthSystem.HealComplete();

        if (navAgent != null && navAgent.isStopped)
            navAgent.isStopped = false;
    }

    public void ApplySmokeDebuff(float targetRadius, float duration)
    {
        if (threatTracker == null)
            return;

        threatTracker.ApplySmokeDebuff(targetRadius, duration);
    }

    public void ApplyRoot(float duration)
    {
        if (escapeMotor == null)
            return;

        stoppedRecoveryTimer = 0f;
        isRecoveringAfterStop = false;

        escapeMotor.ApplyRoot(duration);
    }

    public void AddReconReveal()
    {
        if (threatTracker == null)
            return;

        threatTracker.AddReconReveal();
    }

    public void RemoveReconReveal()
    {
        if (threatTracker == null)
            return;

        threatTracker.RemoveReconReveal();
    }

    public bool TryActivateEmergencyEscape()
    {
        if (escapeMotor == null)
            return false;

        return escapeMotor.TryActivateEmergencyEscape();
    }

    public void ResetRuntimeState(bool resetEmergencyEscapeUsage = true)
    {
        stoppedRecoveryTimer = 0f;
        isRecoveringAfterStop = false;
        recoveryStartHealth = 0f;

        if (escapeMotor != null)
            escapeMotor.ResetRuntimeState(resetEmergencyEscapeUsage);

        if (navAgent != null && healthSystem != null && !healthSystem.IsDead())
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }
    }
}