using System;
using CodeMonkey.HealthSystemCM;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(TargetThreatTracker))]
[RequireComponent(typeof(TargetEscapeMotor))]
[RequireComponent(typeof(TargetWanderMotor))]
public class TargetController : MonoBehaviour, IGetHealthSystem, ISmokeDebuffReceiver
{
    [Header("References")]
    public TargetThreatTracker threatTracker;
    public TargetEscapeMotor escapeMotor;
    public TargetSkillController skillController;

    [Header("Health")]
    public float maxHealth = 100f;
    public float startHealth = 100f;

    [Header("Flee Health Drain")]
    public float fleeHealthDrainPerSecond = 12f;

    [Header("Safe Recovery")]
    public float recoveryDelayAfterSafe = 2f;
    public float recoveryAmountTotal = 30f;
    public float recoveryDuration = 1.5f;

    private NavMeshAgent navAgent;
    private TargetWanderMotor wanderMotor;
    private HealthSystem healthSystem;

    private float safeRecoveryTimer = 0f;
    private bool isRecoveringAfterSafe = false;
    private float recoveryStartHealth = 0f;

    private bool hadThreatLastFrame = false;

    public bool IsRevealedToPlayer => threatTracker != null && threatTracker.IsRevealedToPlayer;
    public bool HasActiveThreat => threatTracker != null && threatTracker.HasAnyThreat();
    public bool IsRooted => escapeMotor != null && escapeMotor.IsRooted;
    public bool IsSlowed => escapeMotor != null && escapeMotor.IsSlowed;
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

        if (skillController == null)
            skillController = GetComponent<TargetSkillController>();

        wanderMotor = GetComponent<TargetWanderMotor>();

        if (escapeMotor != null && threatTracker != null)
            escapeMotor.SetThreatTracker(threatTracker);

        if (escapeMotor != null && skillController != null)
            escapeMotor.SetSkillController(skillController);

        ClampValues();
        RecreateHealthSystem();

        if (skillController != null)
            skillController.ResetRuntimeState(true, true);
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
            healthSystem.OnDead -= HealthSystem_OnDead;
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void Update()
    {
        if (escapeMotor == null || threatTracker == null)
            return;

        bool hasThreat = threatTracker.HasAnyThreat();
        HandleThreatTransition(hasThreat);

        if (healthSystem == null || healthSystem.IsDead())
        {
            if (wanderMotor != null)
                wanderMotor.StopWandering(true);

            hadThreatLastFrame = hasThreat;
            return;
        }

        if (escapeMotor.IsRooted)
        {
            if (wanderMotor != null)
                wanderMotor.StopWandering(true);

            hadThreatLastFrame = hasThreat;
            return;
        }

        escapeMotor.RefreshDynamicMovementSettings(hasThreat, GetHealthRatio());

        if (hasThreat)
        {
            HandleThreatState();
        }
        else
        {
            HandleSafeState();
        }

        hadThreatLastFrame = hasThreat;
    }

    private void HandleThreatState()
    {
        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;

        if (wanderMotor != null && wanderMotor.IsWandering)
            wanderMotor.StopWandering(true);

        escapeMotor.RefreshDynamicMovementSettings(true, GetHealthRatio());

        TryAutoEmergencyEscape();

        float damageAmount = fleeHealthDrainPerSecond * Time.deltaTime;
        healthSystem.Damage(damageAmount);

        if (healthSystem != null && !healthSystem.IsDead())
            escapeMotor.TryFleeFromThreats();
    }

    private void HandleSafeState()
    {
        HandleSafeRecovery();

        if (wanderMotor != null)
            wanderMotor.TickSafeWander(false);
    }

    private void HandleThreatTransition(bool hasThreat)
    {
        if (hadThreatLastFrame && !hasThreat)
        {
            safeRecoveryTimer = 0f;
            isRecoveringAfterSafe = false;

            if (wanderMotor != null)
                wanderMotor.BeginSafeDelay(true);
        }
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
        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        Debug.Log("[Target] 체력이 0이 되어 더 이상 도망칠 수 없습니다.");
    }

    private void HandleSafeRecovery()
    {
        if (healthSystem == null || healthSystem.IsDead())
            return;

        if (healthSystem.GetHealth() >= healthSystem.GetHealthMax())
            return;

        safeRecoveryTimer += Time.deltaTime;

        if (safeRecoveryTimer < recoveryDelayAfterSafe)
            return;

        if (!isRecoveringAfterSafe)
        {
            isRecoveringAfterSafe = true;
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

        float healthMax = healthSystem.GetHealthMax();
        if (healthMax <= 0.01f)
            return 1f;

        return healthSystem.GetHealth() / healthMax;
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

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;

        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        escapeMotor.ApplyRoot(duration);
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (escapeMotor == null)
            return;

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;

        escapeMotor.ApplySlow(multiplier, duration);
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
        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        if (skillController != null)
            return skillController.TryUseEmergencyEscape();

        if (escapeMotor == null)
            return false;

        return escapeMotor.TryActivateEmergencyEscape();
    }

    private bool TryAutoEmergencyEscape()
    {
        float healthRatio = GetHealthRatio();

        if (skillController != null)
            return skillController.TryAutoEmergencyEscape(healthRatio);

        if (escapeMotor == null)
            return false;

        if (!escapeMotor.ShouldAutoTriggerEmergencyEscape(healthRatio))
            return false;

        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        return escapeMotor.TryActivateEmergencyEscape();
    }

    public void ResetRuntimeState(bool resetEmergencyEscapeUsage = true)
    {
        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;
        recoveryStartHealth = 0f;
        hadThreatLastFrame = false;

        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        if (escapeMotor != null)
            escapeMotor.ResetRuntimeState(resetEmergencyEscapeUsage);

        if (skillController != null)
            skillController.ResetRuntimeState(true, true);

        if (navAgent != null && healthSystem != null && !healthSystem.IsDead())
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }
    }

    private void ClampValues()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        startHealth = Mathf.Clamp(startHealth, 0f, maxHealth);

        fleeHealthDrainPerSecond = Mathf.Max(0f, fleeHealthDrainPerSecond);

        recoveryDelayAfterSafe = Mathf.Max(0f, recoveryDelayAfterSafe);
        recoveryAmountTotal = Mathf.Max(0f, recoveryAmountTotal);
        recoveryDuration = Mathf.Max(0.01f, recoveryDuration);
    }
}