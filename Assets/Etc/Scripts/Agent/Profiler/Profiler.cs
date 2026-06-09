using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Profiler : AgentController, IUpgradeReceiver
{
    private const string SkillEscapePatternAnalysis = "escape_pattern_analysis";
    private const string SkillBehaviorBriefing = "behavior_briefing";
    private const string SkillLinkedAnalysis = "linked_analysis";
    private const string SkillRouteIdentification = "route_identification";

    private const string UpgradeUnlockLinkedAnalysis = "profiler_unlock_linked_analysis";
    private const string UpgradeUnlockRouteIdentification = "profiler_unlock_route_identification";

    [Header("Target Reference")]
    [SerializeField] private TargetController targetController;
    [SerializeField] private bool autoFindTarget = true;

    [Header("Escape Pattern Analysis")]
    [SerializeField] private float escapePatternAnalysisGaugeMax = 100f;
    [SerializeField] private float escapePatternGaugeGainPerEscape = 25f;
    [SerializeField] private float escapePatternGainCooldown = 1f;
    [SerializeField] private float escapePatternTargetSpeedMultiplier = 0.8f;
    [SerializeField] private float escapePatternSlowDuration = 6f;

    [Header("Behavior Briefing")]
    [SerializeField] private float behaviorBriefingGaugeMax = 100f;
    [SerializeField] private float behaviorBriefingInitialSightGaugeGain = 25f;
    [SerializeField] private float behaviorBriefingSustainedGaugePerSecond = 5f;
    [SerializeField] private float behaviorBriefingDuration = 8f;
    [SerializeField] private float behaviorBriefingMoveSpeedMultiplier = 1.2f;
    [SerializeField] private bool behaviorBriefingIncludesSelf = true;

    [Header("Linked Analysis")]
    [SerializeField] private bool linkedAnalysisUnlockedByDefault = false;
    [SerializeField] private float linkedAnalysisGaugeMax = 100f;
    [SerializeField] private float linkedAnalysisGaugeGainPerAllySkill = 20f;
    [SerializeField] private float linkedAnalysisDuration = 12f;
    [SerializeField] private float linkedAnalysisTargetRevealDuration = 3f;
    [SerializeField] private float linkedAnalysisRevealCooldown = 2f;
    [SerializeField] private bool countProfilerSkillsForLinkedAnalysis = false;

    [Header("Route Identification")]
    [SerializeField] private bool routeIdentificationUnlockedByDefault = false;
    [SerializeField] private float routeIdentificationGaugeMax = 100f;
    [SerializeField] private float routeIdentificationGaugeGainPerMeter = 2f;
    [SerializeField] private float routeIdentificationDuration = 10f;
    [SerializeField] private float routeIdentificationViewRadiusMultiplier = 1.25f;
    [SerializeField] private float routeIdentificationViewAngleBonus = 20f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Coroutine escapePatternRoutine;
    private Coroutine behaviorBriefingRoutine;
    private Coroutine linkedAnalysisRoutine;
    private Coroutine routeIdentificationRoutine;
    private Coroutine targetRevealRoutine;

    private TargetEscapeMotor activeSlowedTargetMotor;
    private TargetController revealedTargetController;

    private bool isBehaviorBriefingActive;
    private bool isLinkedAnalysisActive;
    private bool isRouteIdentificationActive;

    private float lastEscapePatternGaugeGainTime = -999f;
    private float lastLinkedAnalysisRevealTime = -999f;

    private readonly List<AgentMoveSnapshot> behaviorBriefingSnapshots = new List<AgentMoveSnapshot>();

    private float originalSpotLightRange;
    private float originalSpotLightOuterAngle;
    private bool hasSpotLightSnapshot;

    public bool CanUseLinkedAnalysisSkill => linkedAnalysisUnlockedByDefault || IsAgentUpgradeUnlocked(UpgradeUnlockLinkedAnalysis);
    public bool CanUseRouteIdentificationSkill => routeIdentificationUnlockedByDefault || IsAgentUpgradeUnlocked(UpgradeUnlockRouteIdentification);

    protected override float SkillGaugeChargeMultiplier => 0f;

    protected override void Awake()
    {
        agentID = 4;

        base.Awake();

        CacheTargetIfNeeded();
    }

    private void OnEnable()
    {
        TargetEscapeMotor.OnAnyEscapeAction += HandleTargetEscapeAction;
        AgentSkillUseEventBus.OnAgentSkillExecuted += HandleAgentSkillExecuted;

        if (visionSensor != null)
            visionSensor.OnVisionChanged += HandleVisionChanged;
    }

    protected override void OnDisable()
    {
        TargetEscapeMotor.OnAnyEscapeAction -= HandleTargetEscapeAction;
        AgentSkillUseEventBus.OnAgentSkillExecuted -= HandleAgentSkillExecuted;

        if (visionSensor != null)
            visionSensor.OnVisionChanged -= HandleVisionChanged;

        StopAllProfilerEffects();

        base.OnDisable();
    }

    protected override void Update()
    {
        base.Update();

        UpdateBehaviorBriefingSustainedSightGauge();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        escapePatternAnalysisGaugeMax = Mathf.Max(0f, escapePatternAnalysisGaugeMax);
        escapePatternGaugeGainPerEscape = Mathf.Max(0f, escapePatternGaugeGainPerEscape);
        escapePatternGainCooldown = Mathf.Max(0f, escapePatternGainCooldown);
        escapePatternTargetSpeedMultiplier = Mathf.Clamp(escapePatternTargetSpeedMultiplier, 0.05f, 1f);
        escapePatternSlowDuration = Mathf.Max(0f, escapePatternSlowDuration);

        behaviorBriefingGaugeMax = Mathf.Max(0f, behaviorBriefingGaugeMax);
        behaviorBriefingInitialSightGaugeGain = Mathf.Max(0f, behaviorBriefingInitialSightGaugeGain);
        behaviorBriefingSustainedGaugePerSecond = Mathf.Max(0f, behaviorBriefingSustainedGaugePerSecond);
        behaviorBriefingDuration = Mathf.Max(0f, behaviorBriefingDuration);
        behaviorBriefingMoveSpeedMultiplier = Mathf.Max(1f, behaviorBriefingMoveSpeedMultiplier);

        linkedAnalysisGaugeMax = Mathf.Max(0f, linkedAnalysisGaugeMax);
        linkedAnalysisGaugeGainPerAllySkill = Mathf.Max(0f, linkedAnalysisGaugeGainPerAllySkill);
        linkedAnalysisDuration = Mathf.Max(0f, linkedAnalysisDuration);
        linkedAnalysisTargetRevealDuration = Mathf.Max(0f, linkedAnalysisTargetRevealDuration);
        linkedAnalysisRevealCooldown = Mathf.Max(0f, linkedAnalysisRevealCooldown);

        routeIdentificationGaugeMax = Mathf.Max(0f, routeIdentificationGaugeMax);
        routeIdentificationGaugeGainPerMeter = Mathf.Max(0f, routeIdentificationGaugeGainPerMeter);
        routeIdentificationDuration = Mathf.Max(0f, routeIdentificationDuration);
        routeIdentificationViewRadiusMultiplier = Mathf.Max(1f, routeIdentificationViewRadiusMultiplier);
        routeIdentificationViewAngleBonus = Mathf.Max(0f, routeIdentificationViewAngleBonus);
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillBehaviorBriefing))
        {
            TryUseBehaviorBriefing();
            return;
        }

        if (skill == NormalizeSkillKey(SkillRouteIdentification))
        {
            TryUseRouteIdentification();
            return;
        }

        Debug.LogWarning($"[Profiler {AgentID}] 처리할 수 없는 스킬입니다. skill={skillName}");
    }

    public override float GetSkillGaugeMaxForSkill(string skillName)
    {
        string skill = NormalizeSkillKey(skillName);

        if (skill == NormalizeSkillKey(SkillEscapePatternAnalysis))
            return escapePatternAnalysisGaugeMax;

        if (skill == NormalizeSkillKey(SkillBehaviorBriefing))
            return behaviorBriefingGaugeMax;

        if (skill == NormalizeSkillKey(SkillLinkedAnalysis))
            return linkedAnalysisGaugeMax;

        if (skill == NormalizeSkillKey(SkillRouteIdentification))
            return routeIdentificationGaugeMax;

        return base.GetSkillGaugeMaxForSkill(skillName);
    }

    public override float GetSkillGaugeRequiredForSkill(string skillName)
    {
        return GetSkillGaugeMaxForSkill(skillName);
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        List<string> keys = new List<string>
        {
            SkillEscapePatternAnalysis,
            SkillBehaviorBriefing
        };

        if (CanUseLinkedAnalysisSkill)
            keys.Add(SkillLinkedAnalysis);

        if (CanUseRouteIdentificationSkill)
            keys.Add(SkillRouteIdentification);

        return keys.ToArray();
    }

    protected override void OnAgentMoved(float movedDistance)
    {
        if (!CanUseRouteIdentificationSkill)
            return;

        if (isRouteIdentificationActive)
            return;

        if (movedDistance <= 0f)
            return;

        AddSkillGaugeForSkill(
            SkillRouteIdentification,
            movedDistance * routeIdentificationGaugeGainPerMeter
        );
    }

    public override void ResetSkillGauge()
    {
        base.ResetSkillGauge();
        StopAllProfilerEffects();
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return CanApplyUpgradeByAgentDefinitionOrLegacy(
            upgrade,
            CommanderAgentType.Profiler
        );
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        if (upgrade.IsUnlockSkillUpgrade)
            return;

        ApplyProfilerUpgradeValue(upgrade);
    }

    private void HandleTargetEscapeAction(TargetEscapeMotor escapeMotor)
    {
        if (escapeMotor == null)
            return;

        if (Time.time - lastEscapePatternGaugeGainTime < escapePatternGainCooldown)
            return;

        lastEscapePatternGaugeGainTime = Time.time;

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 도주 행동 감지. 도주 패턴 분석 게이지 획득.");

        AddSkillGaugeForSkill(SkillEscapePatternAnalysis, escapePatternGaugeGainPerEscape);
        TryAutoActivateEscapePatternAnalysis(escapeMotor);
    }

    private void TryAutoActivateEscapePatternAnalysis(TargetEscapeMotor escapeMotor)
    {
        if (escapePatternRoutine != null)
            return;

        if (!CanUseSkillGaugeForSkill(SkillEscapePatternAnalysis, false))
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillEscapePatternAnalysis))
            return;

        if (escapeMotor == null)
            escapeMotor = ResolveTargetEscapeMotor();

        if (escapeMotor == null)
            return;

        escapePatternRoutine = StartCoroutine(EscapePatternAnalysisRoutine(escapeMotor));
    }

    private IEnumerator EscapePatternAnalysisRoutine(TargetEscapeMotor escapeMotor)
    {
        activeSlowedTargetMotor = escapeMotor;

        if (activeSlowedTargetMotor != null)
        {
            activeSlowedTargetMotor.SetExternalSpeedMultiplier(
                this,
                escapePatternTargetSpeedMultiplier
            );
        }

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 도주 패턴 분석 발동. 타겟 이동속도 감소.");

        yield return new WaitForSeconds(escapePatternSlowDuration);

        if (activeSlowedTargetMotor != null)
            activeSlowedTargetMotor.RemoveExternalSpeedMultiplier(this);

        activeSlowedTargetMotor = null;
        escapePatternRoutine = null;
    }

    private void HandleVisionChanged(VisionSensor sensor, bool isSeeingTarget, Transform seenTarget)
    {
        if (!isSeeingTarget)
            return;

        if (seenTarget == null)
            return;

        TargetController seenTargetController = ResolveTargetControllerFromTransform(seenTarget);

        if (seenTargetController == null)
            return;

        targetController = seenTargetController;

        if (isBehaviorBriefingActive)
            return;

        AddSkillGaugeForSkill(SkillBehaviorBriefing, behaviorBriefingInitialSightGaugeGain);

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 타겟 최초 포착. 행동 브리핑 게이지 획득.");
    }

    private void UpdateBehaviorBriefingSustainedSightGauge()
    {
        if (isBehaviorBriefingActive)
            return;

        if (visionSensor == null)
            return;

        if (!visionSensor.IsSeeingTarget)
            return;

        if (visionSensor.CurrentSeenTarget == null)
            return;

        if (ResolveTargetControllerFromTransform(visionSensor.CurrentSeenTarget) == null)
            return;

        float gain = behaviorBriefingSustainedGaugePerSecond * Time.deltaTime;

        if (gain <= 0f)
            return;

        AddSkillGaugeForSkill(SkillBehaviorBriefing, gain);
    }

    private void TryUseBehaviorBriefing()
    {
        if (isBehaviorBriefingActive)
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillBehaviorBriefing))
            return;

        if (behaviorBriefingRoutine != null)
            StopCoroutine(behaviorBriefingRoutine);

        behaviorBriefingRoutine = StartCoroutine(BehaviorBriefingRoutine());
    }

    private IEnumerator BehaviorBriefingRoutine()
    {
        isBehaviorBriefingActive = true;

        ApplyBehaviorBriefingMoveSpeedBuff();

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 행동 브리핑 발동. 아군 이동속도 증가.");

        yield return new WaitForSeconds(behaviorBriefingDuration);

        RestoreBehaviorBriefingMoveSpeedBuff();

        isBehaviorBriefingActive = false;
        behaviorBriefingRoutine = null;
    }

    private void ApplyBehaviorBriefingMoveSpeedBuff()
    {
        RestoreBehaviorBriefingMoveSpeedBuff();

        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        if (agents == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!behaviorBriefingIncludesSelf && agent == this)
                continue;

            NavMeshAgent targetNavAgent = agent.GetComponent<NavMeshAgent>();

            if (targetNavAgent == null)
                continue;

            AgentMoveSnapshot snapshot = new AgentMoveSnapshot(
                targetNavAgent,
                targetNavAgent.speed,
                targetNavAgent.acceleration
            );

            behaviorBriefingSnapshots.Add(snapshot);

            targetNavAgent.speed *= behaviorBriefingMoveSpeedMultiplier;
            targetNavAgent.acceleration *= behaviorBriefingMoveSpeedMultiplier;
        }
    }

    private void RestoreBehaviorBriefingMoveSpeedBuff()
    {
        for (int i = 0; i < behaviorBriefingSnapshots.Count; i++)
        {
            AgentMoveSnapshot snapshot = behaviorBriefingSnapshots[i];

            if (snapshot == null || snapshot.NavAgent == null)
                continue;

            snapshot.NavAgent.speed = snapshot.OriginalSpeed;
            snapshot.NavAgent.acceleration = snapshot.OriginalAcceleration;
        }

        behaviorBriefingSnapshots.Clear();
    }

    private void HandleAgentSkillExecuted(AgentController agent, string skillKey)
    {
        if (agent == null)
            return;

        if (agent == this && !countProfilerSkillsForLinkedAnalysis)
            return;

        if (string.IsNullOrWhiteSpace(skillKey))
            return;

        if (!CanUseLinkedAnalysisSkill)
            return;

        if (isLinkedAnalysisActive)
        {
            TryRevealTargetByLinkedAnalysis();
            return;
        }

        AddSkillGaugeForSkill(SkillLinkedAnalysis, linkedAnalysisGaugeGainPerAllySkill);
        TryAutoActivateLinkedAnalysis();
    }

    private void TryAutoActivateLinkedAnalysis()
    {
        if (linkedAnalysisRoutine != null)
            return;

        if (!CanUseSkillGaugeForSkill(SkillLinkedAnalysis, false))
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillLinkedAnalysis))
            return;

        linkedAnalysisRoutine = StartCoroutine(LinkedAnalysisRoutine());
    }

    private IEnumerator LinkedAnalysisRoutine()
    {
        isLinkedAnalysisActive = true;
        lastLinkedAnalysisRevealTime = -999f;

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 연계 분석 상태 시작.");

        yield return new WaitForSeconds(linkedAnalysisDuration);

        isLinkedAnalysisActive = false;
        linkedAnalysisRoutine = null;

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 연계 분석 상태 종료.");
    }

    private void TryRevealTargetByLinkedAnalysis()
    {
        if (Time.time - lastLinkedAnalysisRevealTime < linkedAnalysisRevealCooldown)
            return;

        TargetController target = ResolveTargetController();

        if (target == null)
            return;

        lastLinkedAnalysisRevealTime = Time.time;

        if (targetRevealRoutine != null)
            StopTargetReveal();

        revealedTargetController = target;
        revealedTargetController.AddReconReveal();

        targetRevealRoutine = StartCoroutine(TargetRevealRoutine());

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 연계 분석으로 타겟 위치 표시.");
    }

    private IEnumerator TargetRevealRoutine()
    {
        yield return new WaitForSeconds(linkedAnalysisTargetRevealDuration);

        StopTargetReveal();
    }

    private void StopTargetReveal()
    {
        if (targetRevealRoutine != null)
        {
            StopCoroutine(targetRevealRoutine);
            targetRevealRoutine = null;
        }

        if (revealedTargetController != null)
            revealedTargetController.RemoveReconReveal();

        revealedTargetController = null;
    }

    private void TryUseRouteIdentification()
    {
        if (!CanUseRouteIdentificationSkill)
        {
            Debug.LogWarning($"[Profiler {AgentID}] 동선 파악 스킬이 아직 해금되지 않았습니다.");
            return;
        }

        if (isRouteIdentificationActive)
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillRouteIdentification))
            return;

        if (routeIdentificationRoutine != null)
            StopCoroutine(routeIdentificationRoutine);

        routeIdentificationRoutine = StartCoroutine(RouteIdentificationRoutine());
    }

    private IEnumerator RouteIdentificationRoutine()
    {
        isRouteIdentificationActive = true;

        ApplyRouteIdentificationVisionBuff();

        if (debugLog)
            Debug.Log($"[Profiler {AgentID}] 동선 파악 발동. 프로파일러 시야 강화.");

        yield return new WaitForSeconds(routeIdentificationDuration);

        RestoreRouteIdentificationVisionBuff();

        isRouteIdentificationActive = false;
        routeIdentificationRoutine = null;
    }

    private void ApplyRouteIdentificationVisionBuff()
    {
        if (visionSensor != null)
        {
            visionSensor.SetExternalViewRadiusMultiplier(
                this,
                routeIdentificationViewRadiusMultiplier
            );

            visionSensor.SetExternalViewAngleOffset(
                this,
                routeIdentificationViewAngleBonus
            );
        }

        if (spotLight != null)
        {
            originalSpotLightRange = spotLight.range;
            originalSpotLightOuterAngle = spotLight.spotAngle;
            hasSpotLightSnapshot = true;

            spotLight.range *= routeIdentificationViewRadiusMultiplier;
            spotLight.spotAngle = Mathf.Clamp(
                spotLight.spotAngle + routeIdentificationViewAngleBonus,
                1f,
                179f
            );
        }
    }

    private void RestoreRouteIdentificationVisionBuff()
    {
        if (visionSensor != null)
        {
            visionSensor.RemoveExternalViewRadiusMultiplier(this);
            visionSensor.RemoveExternalViewAngleOffset(this);
        }

        if (spotLight != null && hasSpotLightSnapshot)
        {
            spotLight.range = originalSpotLightRange;
            spotLight.spotAngle = originalSpotLightOuterAngle;
        }

        hasSpotLightSnapshot = false;
    }

    private TargetController ResolveTargetController()
    {
        if (targetController != null)
            return targetController;

        if (currentTarget != null)
        {
            targetController = ResolveTargetControllerFromTransform(currentTarget);

            if (targetController != null)
                return targetController;
        }

        if (!autoFindTarget)
            return null;

        targetController = FindFirstObjectByType<TargetController>();
        return targetController;
    }

    private TargetEscapeMotor ResolveTargetEscapeMotor()
    {
        TargetController target = ResolveTargetController();

        if (target == null)
            return null;

        return target.EscapeMotor;
    }

    private TargetController ResolveTargetControllerFromTransform(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        TargetController controller = targetTransform.GetComponentInParent<TargetController>();

        if (controller != null)
            return controller;

        return targetTransform.GetComponent<TargetController>();
    }

    private void CacheTargetIfNeeded()
    {
        if (targetController != null)
            return;

        if (!autoFindTarget)
            return;

        targetController = FindFirstObjectByType<TargetController>();
    }

    private bool IsAgentUpgradeUnlocked(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return false;

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return false;

        return upgradeManager.HasAgentUpgrade(upgradeId);
    }

    private void ApplyProfilerUpgradeValue(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
            return;

        string key = NormalizeUpgradeKey(upgrade.EffectKey);

        if (string.IsNullOrWhiteSpace(key))
            key = NormalizeUpgradeKey(upgrade.SkillId);

        switch (key)
        {
            case "escapepatternanalysisgaugegain":
                ApplyUpgradeFloat(ref escapePatternGaugeGainPerEscape, upgrade, 0f);
                break;

            case "escapepatternanalysisduration":
                ApplyUpgradeFloat(ref escapePatternSlowDuration, upgrade, 0f);
                break;

            case "escapepatternanalysisspeedmultiplier":
                ApplyUpgradeFloat(ref escapePatternTargetSpeedMultiplier, upgrade, 0.05f);
                escapePatternTargetSpeedMultiplier = Mathf.Clamp(escapePatternTargetSpeedMultiplier, 0.05f, 1f);
                break;

            case "behaviorbriefingduration":
                ApplyUpgradeFloat(ref behaviorBriefingDuration, upgrade, 0f);
                break;

            case "behaviorbriefingspeedmultiplier":
                ApplyUpgradeFloat(ref behaviorBriefingMoveSpeedMultiplier, upgrade, 1f);
                break;

            case "behaviorbriefinggaugegain":
                ApplyUpgradeFloat(ref behaviorBriefingInitialSightGaugeGain, upgrade, 0f);
                break;

            case "linkedanalysisduration":
                ApplyUpgradeFloat(ref linkedAnalysisDuration, upgrade, 0f);
                break;

            case "linkedanalysisrevealduration":
                ApplyUpgradeFloat(ref linkedAnalysisTargetRevealDuration, upgrade, 0f);
                break;

            case "linkedanalysisgaugegain":
                ApplyUpgradeFloat(ref linkedAnalysisGaugeGainPerAllySkill, upgrade, 0f);
                break;

            case "routeidentificationduration":
                ApplyUpgradeFloat(ref routeIdentificationDuration, upgrade, 0f);
                break;

            case "routeidentificationviewradius":
                ApplyUpgradeFloat(ref routeIdentificationViewRadiusMultiplier, upgrade, 1f);
                break;

            case "routeidentificationviewangle":
                ApplyUpgradeFloat(ref routeIdentificationViewAngleBonus, upgrade, 0f);
                break;

            case "routeidentificationgaugegain":
                ApplyUpgradeFloat(ref routeIdentificationGaugeGainPerMeter, upgrade, 0f);
                break;
        }
    }

    private void ApplyUpgradeFloat(ref float targetValue, UpgradeDefinition upgrade, float minValue)
    {
        if (upgrade == null)
            return;

        float value = upgrade.Value;

        switch (upgrade.EffectType)
        {
            case UpgradeEffectType.ValueAdd:
            case UpgradeEffectType.DurationAdd:
            case UpgradeEffectType.ViewAngleAdd:
            case UpgradeEffectType.MaxGaugeAdd:
                targetValue += value;
                break;

            case UpgradeEffectType.ValueMultiplier:
            case UpgradeEffectType.SpeedMultiplier:
            case UpgradeEffectType.ViewRadiusMultiplier:
            case UpgradeEffectType.GaugeCostMultiplier:
                targetValue *= value;
                break;

            default:
                targetValue = value;
                break;
        }

        targetValue = Mathf.Max(minValue, targetValue);
    }

    private string NormalizeSkillKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private string NormalizeUpgradeKey(string value)
    {
        return NormalizeSkillKey(value);
    }

    private void StopAllProfilerEffects()
    {
        if (escapePatternRoutine != null)
        {
            StopCoroutine(escapePatternRoutine);
            escapePatternRoutine = null;
        }

        if (behaviorBriefingRoutine != null)
        {
            StopCoroutine(behaviorBriefingRoutine);
            behaviorBriefingRoutine = null;
        }

        if (linkedAnalysisRoutine != null)
        {
            StopCoroutine(linkedAnalysisRoutine);
            linkedAnalysisRoutine = null;
        }

        if (routeIdentificationRoutine != null)
        {
            StopCoroutine(routeIdentificationRoutine);
            routeIdentificationRoutine = null;
        }

        if (activeSlowedTargetMotor != null)
            activeSlowedTargetMotor.RemoveExternalSpeedMultiplier(this);

        activeSlowedTargetMotor = null;

        RestoreBehaviorBriefingMoveSpeedBuff();
        RestoreRouteIdentificationVisionBuff();
        StopTargetReveal();

        isBehaviorBriefingActive = false;
        isLinkedAnalysisActive = false;
        isRouteIdentificationActive = false;
    }

    private sealed class AgentMoveSnapshot
    {
        public readonly NavMeshAgent NavAgent;
        public readonly float OriginalSpeed;
        public readonly float OriginalAcceleration;

        public AgentMoveSnapshot(
            NavMeshAgent navAgent,
            float originalSpeed,
            float originalAcceleration)
        {
            NavAgent = navAgent;
            OriginalSpeed = originalSpeed;
            OriginalAcceleration = originalAcceleration;
        }
    }
}