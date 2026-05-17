using System;
using System.Collections.Generic;
using CodeMonkey.HealthSystemCM;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(TargetThreatTracker))]
[RequireComponent(typeof(TargetEscapeMotor))]
[RequireComponent(typeof(TargetWanderMotor))]
public class TargetController : MonoBehaviour, IGetHealthSystem, ISmokeDebuffReceiver, ITargetRouteInterferenceReceiver
{
    [Header("References")]
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private TargetEscapeMotor escapeMotor;
    [SerializeField] private TargetSkillController skillController;
    [SerializeField] private TargetAnimationController animationController;
    [SerializeField] private TargetVisibilityController visibilityController;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float startHealth = 100f;

    [Header("Flee Health Drain")]
    [SerializeField] private float fleeHealthDrainPerSecond = 8f;

    [Header("Safe Recovery")]
    [SerializeField] private float recoveryDelayAfterSafe = 2f;
    [SerializeField] private float recoveryAmountTotal = 30f;
    [SerializeField] private float recoveryDuration = 1.5f;

    private readonly HashSet<Component> fleeHealthDrainBlockSources = new HashSet<Component>();

    private NavMeshAgent navAgent;
    private TargetWanderMotor wanderMotor;
    private HealthSystem healthSystem;

    private float safeRecoveryTimer = 0f;
    private bool isRecoveringAfterSafe = false;
    private float recoveryStartHealth = 0f;

    private bool hadThreatLastFrame = false;
    private bool isCaught = false;

    private bool escapeMotorEnabledOnAwake = true;
    private bool wanderMotorEnabledOnAwake = true;

    public TargetThreatTracker ThreatTracker => threatTracker;
    public TargetEscapeMotor EscapeMotor => escapeMotor;
    public TargetSkillController SkillController => skillController;
    public TargetAnimationController AnimationController => animationController;
    public TargetVisibilityController VisibilityController => visibilityController;

    public bool IsCaught => isCaught;
    public bool IsExhausted => healthSystem != null && healthSystem.IsDead();

    public bool IsRevealedToPlayer => threatTracker != null && threatTracker.IsRevealedToPlayer;
    public bool HasActiveThreat => threatTracker != null && threatTracker.HasAnyThreat();
    public bool IsRooted => escapeMotor != null && escapeMotor.IsRooted;
    public bool IsSlowed => escapeMotor != null && escapeMotor.IsSlowed;
    public bool HasUsedEmergencyEscape => escapeMotor != null && escapeMotor.HasUsedEmergencyEscape;
    public bool IsEmergencyEscaping => escapeMotor != null && escapeMotor.IsEmergencyEscaping;
    public bool CanBeCaught => !isCaught && (escapeMotor == null || escapeMotor.CanBeCaught);
    public bool IsFleeHealthDrainBlocked => fleeHealthDrainBlockSources.Count > 0;

    public int RemainingEmergencyEscapeCount => escapeMotor != null ? escapeMotor.RemainingEmergencyEscapeCount : 0;

    public float CurrentHealth => healthSystem != null ? healthSystem.GetHealth() : 0f;
    public float MaxHealth => healthSystem != null ? healthSystem.GetHealthMax() : maxHealth;

    private void Awake()
    {
        ResolveReferences();

        escapeMotorEnabledOnAwake = escapeMotor == null || escapeMotor.enabled;
        wanderMotorEnabledOnAwake = wanderMotor == null || wanderMotor.enabled;

        ConnectReferences();

        ClampValues();
        RecreateHealthSystem();
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
        if (isCaught)
        {
            ForceStopMovement();
            return;
        }

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
            HandleThreatState();
        else
            HandleSafeState();

        hadThreatLastFrame = hasThreat;
    }

    public void MarkAsCaught()
    {
        if (isCaught)
        {
            ForceStopMovement();
            return;
        }

        isCaught = true;

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;
        recoveryStartHealth = 0f;
        hadThreatLastFrame = false;

        ClearFleeHealthDrainBlockSources();

        if (wanderMotor != null)
        {
            wanderMotor.StopWandering(true);
            wanderMotor.enabled = false;
        }

        if (escapeMotor != null)
            escapeMotor.enabled = false;

        ForceStopMovement();

        Debug.Log($"[TargetController] {name} 이(가) 체포되어 이동을 중지했습니다.");
    }

    public void ApplyBaseStats(
        float newMaxHealth,
        float newStartHealth,
        float newFleeHealthDrainPerSecond,
        float newRecoveryDelayAfterSafe,
        float newRecoveryAmountTotal,
        float newRecoveryDuration,
        bool refillHealth)
    {
        float nextHealth = newStartHealth;

        if (!refillHealth && healthSystem != null)
            nextHealth = healthSystem.GetHealth();

        maxHealth = Mathf.Max(1f, newMaxHealth);
        startHealth = Mathf.Clamp(nextHealth, 0f, maxHealth);

        fleeHealthDrainPerSecond = Mathf.Max(0f, newFleeHealthDrainPerSecond);

        recoveryDelayAfterSafe = Mathf.Max(0f, newRecoveryDelayAfterSafe);
        recoveryAmountTotal = Mathf.Max(0f, newRecoveryAmountTotal);
        recoveryDuration = Mathf.Max(0.01f, newRecoveryDuration);

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;
        recoveryStartHealth = 0f;

        ClampValues();

        if (healthSystem != null)
            RecreateHealthSystem();
    }

    public void SetFleeHealthDrainBlocked(Component source, bool blocked)
    {
        if (source == null)
            return;

        if (blocked)
            fleeHealthDrainBlockSources.Add(source);
        else
            fleeHealthDrainBlockSources.Remove(source);
    }

    public void ClearFleeHealthDrainBlockSources()
    {
        fleeHealthDrainBlockSources.Clear();
    }

    private void HandleThreatState()
    {
        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;

        if (wanderMotor != null && wanderMotor.IsWandering)
            wanderMotor.StopWandering(true);

        escapeMotor.RefreshDynamicMovementSettings(true, GetHealthRatio());

        if (!IsFleeHealthDrainBlocked)
        {
            float damageAmount = fleeHealthDrainPerSecond * Time.deltaTime;
            healthSystem.Damage(damageAmount);
        }

        if (healthSystem == null || healthSystem.IsDead())
            return;

        if (TryAutoEmergencyEscape())
            return;

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

        ForceStopMovement();

        Debug.Log("[TargetController] 체력이 0이 되어 더 이상 도망칠 수 없습니다.");
    }

    private void HandleSafeRecovery()
    {
        if (isCaught)
            return;

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
        if (isCaught)
            return;

        if (healthSystem == null || healthSystem.IsDead())
            return;

        healthSystem.Damage(amount);
    }

    public void Heal(float amount)
    {
        if (isCaught)
            return;

        if (healthSystem == null || healthSystem.IsDead())
            return;

        healthSystem.Heal(amount);
    }

    public void HealComplete()
    {
        if (isCaught)
            return;

        if (healthSystem == null)
            return;

        healthSystem.HealComplete();

        if (navAgent != null && navAgent.isStopped)
            navAgent.isStopped = false;
    }

    public void ApplySmokeDebuff(float targetRadius, float duration)
    {
        if (isCaught)
            return;

        if (threatTracker == null)
            return;

        threatTracker.ApplySmokeDebuff(targetRadius, duration);
    }

    public void ApplyRoot(float duration)
    {
        if (isCaught)
            return;

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
        if (isCaught)
            return;

        if (escapeMotor == null)
            return;

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;

        escapeMotor.ApplySlow(multiplier, duration);
    }

    public void ApplyFakeBoxRouteInterference(Vector3 boxPosition, int reducedRouteCandidateCount)
    {
        if (isCaught)
            return;

        if (healthSystem == null || healthSystem.IsDead())
            return;

        if (escapeMotor == null)
            return;

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;

        if (wanderMotor != null)
            wanderMotor.StopWandering(true);

        escapeMotor.ApplyFakeBoxRouteInterference(
            boxPosition,
            reducedRouteCandidateCount
        );
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
        if (isCaught)
            return false;

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
        if (isCaught)
            return false;

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

    public void ResetRuntimeState(
        bool resetEmergencyEscapeUsage = true,
        bool resetHealth = true)
    {
        isCaught = false;

        safeRecoveryTimer = 0f;
        isRecoveringAfterSafe = false;
        recoveryStartHealth = 0f;
        hadThreatLastFrame = false;

        ClearFleeHealthDrainBlockSources();

        if (resetHealth)
            RecreateHealthSystem();

        if (animationController != null)
            animationController.ResetRuntimeState();

        if (threatTracker != null)
            threatTracker.ResetRuntimeState();

        if (escapeMotor != null)
            escapeMotor.enabled = escapeMotorEnabledOnAwake;

        if (wanderMotor != null)
            wanderMotor.enabled = wanderMotorEnabledOnAwake;

        if (escapeMotor != null)
            escapeMotor.ResetRuntimeState(resetEmergencyEscapeUsage);

        if (wanderMotor != null)
            wanderMotor.ResetRuntimeState(true);

        if (skillController != null)
            skillController.ResetRuntimeState(true, true);

        RestoreNavAgentAfterReset();

        if (visibilityController != null)
            visibilityController.ResetRuntimeState();
    }

    private void RestoreNavAgentAfterReset()
    {
        if (navAgent == null)
            return;

        if (!navAgent.enabled)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        if (healthSystem != null && healthSystem.IsDead())
            return;

        navAgent.isStopped = false;
        navAgent.ResetPath();
        navAgent.velocity = Vector3.zero;
    }

    private void ForceStopMovement()
    {
        if (navAgent == null)
            return;

        if (!navAgent.enabled)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.ResetPath();
        navAgent.velocity = Vector3.zero;
    }

    private void ResolveReferences()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (skillController == null)
            skillController = GetComponent<TargetSkillController>();

        if (animationController == null)
            animationController = GetComponent<TargetAnimationController>();

        if (visibilityController == null)
            visibilityController = GetComponent<TargetVisibilityController>();

        wanderMotor = GetComponent<TargetWanderMotor>();
    }

    private void ConnectReferences()
    {
        if (escapeMotor != null && threatTracker != null)
            escapeMotor.SetThreatTracker(threatTracker);

        if (escapeMotor != null && skillController != null)
            escapeMotor.SetSkillController(skillController);

        if (skillController != null)
        {
            skillController.SetTargetController(this);
            skillController.SetThreatTracker(threatTracker);
            skillController.SetEscapeMotor(escapeMotor);
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

    public void ApplyFleeHealthDrainMultiplier(float multiplier, float deltaTime)
    {
        if (isCaught)
            return;

        if (healthSystem == null || healthSystem.IsDead())
            return;

        if (multiplier <= 1f)
            return;

        if (deltaTime <= 0f)
            return;

        float extraDamage = fleeHealthDrainPerSecond * (multiplier - 1f) * deltaTime;

        if (extraDamage <= 0f)
            return;

        healthSystem.Damage(extraDamage);
    }
}