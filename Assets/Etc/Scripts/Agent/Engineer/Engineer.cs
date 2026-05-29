using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Engineer : AgentController, IUpgradeReceiver
{
    private enum EngineerMoveMode
    {
        IdleLookAround = 0,
        Run = 1,
        Reserved = 2,
        DebuffedRun = 3
    }

    private const string SkillBarricade = "barricade";
    private const string SkillStopSignal = "stopsignal";
    private const string SkillDemolition = "demolition";
    private const string SkillSafeZone = "safezone";

    private const string UpgradeBarricadeLarge = "engineer_barricade_large";
    private const string UpgradeBarricadeMulti = "engineer_barricade_multi";
    private const string UpgradeStopSignalWideArea = "engineer_stop_signal_wide_area";
    private const string UpgradeStopSignalLongDuration = "engineer_stop_signal_long_duration";

    private const string IsMovingParameter = "IsMoving";
    private const string MoveSpeedParameter = "MoveSpeed";
    private const string MoveModeParameter = "MoveMode";
    private const string HitReactionTriggerName = "HitReaction";
    private const string VictoryTriggerName = "Victory";
    private const string DefeatTriggerName = "Defeat";
    private const string DeployBarricadeTriggerName = "DeployBarricade";
    private const string DeployStopSignalTriggerName = "DeployStopSignal";

    [Header("설치 프리팹")]
    [SerializeField] private GameObject barricadePrefab;
    [SerializeField] private GameObject stopSignalPrefab;
    [SerializeField] private Transform deployParent;

    [Header("타겟 참조")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private bool autoFindTargetIfMissing = true;

    [Header("설치 위치 보정")]
    [SerializeField] private float deployY = 0f;
    [SerializeField] private float placementNavMeshSampleRadius = 2f;
    [SerializeField] private float groundProbeHeight = 4f;
    [SerializeField] private float groundProbeDistance = 20f;
    [SerializeField] private LayerMask placementGroundLayer;

    [Header("바리케이드 설정")]
    [SerializeField] private bool replaceExistingBarricade = true;
    [SerializeField] private float barricadeYawOffset = 0f;

    [Header("정지 신호 설정")]
    [SerializeField] private bool replaceExistingStopSignal = true;
    [SerializeField] private float stopSignalRadius = 3f;
    [SerializeField] private float stopSignalDuration = 2f;
    [SerializeField] private float stopSignalLifeTime = 12f;

    [Header("철거 설정")]
    [SerializeField] private float demolitionRadius = 3.5f;
    [SerializeField] private LayerMask demolitionObstacleLayer;
    [SerializeField] private string demolitionRootName = "ObstacleRoot";

    [Header("안전 구역 설정")]
    [SerializeField] private GameObject safeZonePrefab;
    [SerializeField] private bool replaceExistingSafeZone = true;
    [SerializeField] private Vector2 safeZoneSize = new Vector2(6f, 6f);
    [SerializeField] private float safeZoneLifeTime = 15f;
    [SerializeField] private float safeZoneGaugeRecoveryPerSecond = 4f;

    [Header("Upgrade - Engineer")]
    [SerializeField] private float largeBarricadeScaleMultiplier = 3f;
    [SerializeField] private int multiBarricadeCount = 3;
    [SerializeField] private float multiBarricadeSpacing = 2.25f;
    [SerializeField] private float wideStopSignalRadiusMultiplier = 2f;
    [SerializeField] private float longStopSignalDurationMultiplier = 2f;

    [Header("안전 관리자 애니메이션")]
    [SerializeField] private float animationMovingThreshold = 0.05f;
    [SerializeField] private float destinationBuffer = 0.2f;
    [SerializeField] private float minimumMovingNormalizedSpeed = 0.15f;
    [SerializeField] private bool stopWhenUseSkill = true;

    [Header("스킬 애니메이션 타이밍")]
    [SerializeField] private float barricadeDeployLockSeconds = 0.8f;
    [SerializeField] private float barricadeSpawnDelay = 0.35f;
    [SerializeField] private float stopSignalDeployLockSeconds = 0.8f;
    [SerializeField] private float stopSignalSpawnDelay = 0.35f;

    [Header("피격 애니메이션")]
    [SerializeField] private float hitReactionLockSeconds = 0.45f;
    [SerializeField] private bool faceAwayFromHitSource = true;

    private readonly List<GameObject> currentBarricades = new List<GameObject>();
    private GameObject currentStopSignal;
    private GameObject currentSafeZone;

    private float currentBarricadeScaleMultiplier = 1f;
    private int currentBarricadeSpawnCount = 1;
    private float currentStopSignalRadiusMultiplier = 1f;
    private float currentStopSignalDurationMultiplier = 1f;
    private float currentStopSignalLifeTimeMultiplier = 1f;

    private Transform skillCameraFocusAnchor;

    private int isMovingHash;
    private int moveSpeedHash;
    private int moveModeHash;
    private int hitReactionHash;
    private int victoryHash;
    private int defeatHash;
    private int deployBarricadeHash;
    private int deployStopSignalHash;

    private bool hasIsMovingParameter;
    private bool hasMoveSpeedParameter;
    private bool hasMoveModeParameter;
    private bool hasHitReactionTrigger;
    private bool hasVictoryTrigger;
    private bool hasDefeatTrigger;
    private bool hasDeployBarricadeTrigger;
    private bool hasDeployStopSignalTrigger;

    private bool isBarricadeDeployLocked;
    private bool isStopSignalDeployLocked;
    private bool isHitReactionLocked;
    private bool isResultAnimationLocked;

    private Coroutine barricadeDeployRoutine;
    private Coroutine stopSignalDeployRoutine;
    private Coroutine hitReactionRoutine;

    public bool IsResultAnimationLocked => isResultAnimationLocked;
    public bool IsBarricadeDeployLocked => isBarricadeDeployLocked;
    public bool IsStopSignalDeployLocked => isStopSignalDeployLocked;

    protected override void Awake()
    {
        agentID = 2;

        CacheEngineerAnimationHashes();

        base.Awake();

        if (animator != null)
            animator.applyRootMotion = false;

        CacheEngineerAnimatorParameters();
        CacheTargetTransform();
        UpdateAnimationState(true);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        deployY = Mathf.Max(0f, deployY);
        placementNavMeshSampleRadius = Mathf.Max(0f, placementNavMeshSampleRadius);
        groundProbeHeight = Mathf.Max(0f, groundProbeHeight);
        groundProbeDistance = Mathf.Max(0f, groundProbeDistance);

        stopSignalRadius = Mathf.Max(0f, stopSignalRadius);
        stopSignalDuration = Mathf.Max(0f, stopSignalDuration);
        stopSignalLifeTime = Mathf.Max(0f, stopSignalLifeTime);

        largeBarricadeScaleMultiplier = Mathf.Max(1f, largeBarricadeScaleMultiplier);
        multiBarricadeCount = Mathf.Max(1, multiBarricadeCount);
        multiBarricadeSpacing = Mathf.Max(0f, multiBarricadeSpacing);
        wideStopSignalRadiusMultiplier = Mathf.Max(1f, wideStopSignalRadiusMultiplier);
        longStopSignalDurationMultiplier = Mathf.Max(1f, longStopSignalDurationMultiplier);

        animationMovingThreshold = Mathf.Max(0f, animationMovingThreshold);
        destinationBuffer = Mathf.Max(0f, destinationBuffer);
        minimumMovingNormalizedSpeed = Mathf.Clamp01(minimumMovingNormalizedSpeed);

        barricadeDeployLockSeconds = Mathf.Max(0f, barricadeDeployLockSeconds);
        barricadeSpawnDelay = Mathf.Max(0f, barricadeSpawnDelay);
        stopSignalDeployLockSeconds = Mathf.Max(0f, stopSignalDeployLockSeconds);
        stopSignalSpawnDelay = Mathf.Max(0f, stopSignalSpawnDelay);

        demolitionRadius = Mathf.Max(0.1f, demolitionRadius);

        safeZoneSize.x = Mathf.Max(0.1f, safeZoneSize.x);
        safeZoneSize.y = Mathf.Max(0.1f, safeZoneSize.y);
        safeZoneLifeTime = Mathf.Max(0.1f, safeZoneLifeTime);
        safeZoneGaugeRecoveryPerSecond = Mathf.Max(0f, safeZoneGaugeRecoveryPerSecond);

        hitReactionLockSeconds = Mathf.Max(0f, hitReactionLockSeconds);

        CacheEngineerAnimationHashes();
    }

    protected override void OnDisable()
    {
        StopBarricadeDeployRoutine();
        StopStopSignalDeployRoutine();
        StopHitReactionRoutine();

        isBarricadeDeployLocked = false;
        isStopSignalDeployLocked = false;
        isHitReactionLocked = false;
        isResultAnimationLocked = false;

        base.OnDisable();
    }

    protected override void Update()
    {
        if (isResultAnimationLocked)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        if (isHitReactionLocked)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        if ((isBarricadeDeployLocked || isStopSignalDeployLocked) && stopWhenUseSkill)
        {
            KeepStopped();
            UpdateAnimationState();
            UpdateStateIcon();
            return;
        }

        base.Update();
    }

    protected override string[] GetCurrentAgentGaugeKeys()
    {
        return new[]
        {
        SkillBarricade,
        SkillStopSignal,
        SkillDemolition,
        SkillSafeZone
    };
    }

    public bool CanApplyUpgrade(UpgradeDefinition upgrade)
    {
        return upgrade != null && upgrade.MatchesAgent(CommanderAgentType.Engineer);
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (!CanApplyUpgrade(upgrade))
            return;

        switch (upgrade.UpgradeId)
        {
            case UpgradeBarricadeLarge:
                ApplyLargeBarricadeUpgrade(upgrade.Value);
                break;

            case UpgradeBarricadeMulti:
                ApplyMultiBarricadeUpgrade(upgrade.Value);
                break;

            case UpgradeStopSignalWideArea:
                ApplyWideStopSignalUpgrade(upgrade.Value);
                break;

            case UpgradeStopSignalLongDuration:
                ApplyLongStopSignalUpgrade(upgrade.Value);
                break;

            default:
                Debug.LogWarning($"[Engineer {AgentID}] 알 수 없는 강화 ID입니다: {upgrade.UpgradeId}");
                break;
        }
    }

    private void ApplyLargeBarricadeUpgrade(float value)
    {
        currentBarricadeScaleMultiplier = value > 0f
            ? Mathf.Max(1f, value)
            : largeBarricadeScaleMultiplier;

        Debug.Log(
            $"[Engineer {AgentID}] 대형 바리케이드 강화 적용. " +
            $"ScaleMultiplier={currentBarricadeScaleMultiplier:F2}"
        );
    }

    private void ApplyMultiBarricadeUpgrade(float value)
    {
        currentBarricadeSpawnCount = value > 0f
            ? Mathf.Max(1, Mathf.RoundToInt(value))
            : multiBarricadeCount;

        Debug.Log(
            $"[Engineer {AgentID}] 다중 바리케이드 강화 적용. " +
            $"SpawnCount={currentBarricadeSpawnCount}"
        );
    }

    private void ApplyWideStopSignalUpgrade(float value)
    {
        currentStopSignalRadiusMultiplier = value > 0f
            ? Mathf.Max(1f, value)
            : wideStopSignalRadiusMultiplier;

        Debug.Log(
            $"[Engineer {AgentID}] 광역 정지 신호 강화 적용. " +
            $"RadiusMultiplier={currentStopSignalRadiusMultiplier:F2}"
        );
    }

    private void ApplyLongStopSignalUpgrade(float value)
    {
        float multiplier = value > 0f
            ? Mathf.Max(1f, value)
            : longStopSignalDurationMultiplier;

        currentStopSignalDurationMultiplier = multiplier;
        currentStopSignalLifeTimeMultiplier = multiplier;

        Debug.Log(
            $"[Engineer {AgentID}] 장시간 정지 신호 강화 적용. " +
            $"LifeTimeMultiplier={currentStopSignalLifeTimeMultiplier:F2}, " +
            $"StopDurationMultiplier={currentStopSignalDurationMultiplier:F2}"
        );
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        if (isResultAnimationLocked || isHitReactionLocked || isBarricadeDeployLocked || isStopSignalDeployLocked)
            return;

        if (!CanReceivePlayerSkillCommand(true))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Engineer {AgentID}] 스킬 요청: {skillName}, 위치: {targetPos}");

        if (IsBarricadeSkill(skill))
        {
            TryDeployBarricade(targetPos);
            return;
        }

        if (IsStopSignalSkill(skill))
        {
            TryDeployStopSignal(targetPos);
            return;
        }

        if (IsDemolitionSkill(skill))
        {
            TryDemolishObstacle();
            return;
        }

        if (IsSafeZoneSkill(skill))
        {
            TryDeploySafeZone(targetPos);
            return;
        }

        Debug.LogWarning($"[Engineer {AgentID}] 알 수 없는 스킬입니다: {skillName}");
    }

    protected override void UpdateAnimationState(bool immediate = false)
    {
        if (animator == null || navAgent == null)
            return;

        bool isMoving = ResolveEngineerIsMoving();
        EngineerMoveMode moveMode = ResolveEngineerMoveMode(isMoving);

        if (hasIsMovingParameter)
            animator.SetBool(isMovingHash, isMoving);

        if (hasMoveModeParameter)
            animator.SetInteger(moveModeHash, (int)moveMode);

        if (!hasMoveSpeedParameter)
            return;

        float actualSpeed = navAgent.velocity.magnitude;

        if (!isMoving || actualSpeed <= animationMovingThreshold)
        {
            animator.SetFloat(moveSpeedHash, 0f);
            return;
        }

        float normalizedSpeed;

        if (stats != null && stats.moveSpeed > 0.01f)
            normalizedSpeed = Mathf.Clamp01(actualSpeed / stats.moveSpeed);
        else
            normalizedSpeed = Mathf.Clamp01(actualSpeed);

        normalizedSpeed = Mathf.Max(normalizedSpeed, minimumMovingNormalizedSpeed);

        if (immediate)
            animator.SetFloat(moveSpeedHash, normalizedSpeed);
        else
            animator.SetFloat(moveSpeedHash, normalizedSpeed, 0.08f, Time.deltaTime);
    }

    public override void PlayHitReaction(Vector3 hitSourcePosition)
    {
        if (isResultAnimationLocked)
            return;

        if (hitReactionRoutine != null)
            StopCoroutine(hitReactionRoutine);

        hitReactionRoutine = StartCoroutine(HitReactionRoutine(hitSourcePosition));
    }

    public override void PlayVictoryPose()
    {
        PlayResultAnimation(victoryHash, hasVictoryTrigger, "Victory");
    }

    public override void PlayDefeatPose()
    {
        PlayResultAnimation(defeatHash, hasDefeatTrigger, "Defeat");
    }

    public override void ClearResultAnimationLock()
    {
        isResultAnimationLocked = false;
        isHitReactionLocked = false;
        isBarricadeDeployLocked = false;
        isStopSignalDeployLocked = false;

        StopBarricadeDeployRoutine();
        StopStopSignalDeployRoutine();
        StopHitReactionRoutine();

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private bool IsBarricadeSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillBarricade) ||
               skill.Contains("barrier") ||
               skill.Contains("바리케이드");
    }

    private bool IsStopSignalSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillStopSignal) ||
               skill.Contains("stop signal") ||
               skill.Contains("stop_signal") ||
               skill.Contains("정지 신호") ||
               skill.Contains("정지신호");
    }

    private bool IsDemolitionSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillDemolition) ||
               skill.Contains("demolish") ||
               skill.Contains("철거");
    }

    private bool IsSafeZoneSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains(SkillSafeZone) ||
               skill.Contains("safe zone") ||
               skill.Contains("safe_zone") ||
               skill.Contains("안전 구역") ||
               skill.Contains("안전구역");
    }

    private void TryDeployBarricade(Vector3 targetPos)
    {
        if (barricadePrefab == null)
        {
            Debug.LogWarning($"[Engineer {AgentID}] barricadePrefab이 연결되지 않았습니다.");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillBarricade))
            return;

        if (hasDeployBarricadeTrigger)
        {
            if (barricadeDeployRoutine != null)
                StopCoroutine(barricadeDeployRoutine);

            barricadeDeployRoutine = StartCoroutine(BarricadeDeployRoutine(targetPos));
            return;
        }

        Debug.LogWarning($"[Engineer {AgentID}] Animator에 DeployBarricade Trigger가 없습니다. 애니메이션 없이 바리케이드를 설치합니다.");

        if (stopWhenUseSkill)
            ForceStopForSkill();

        DeployBarricade(targetPos);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void TryDeployStopSignal(Vector3 targetPos)
    {
        if (stopSignalPrefab == null)
        {
            Debug.LogWarning($"[Engineer {AgentID}] stopSignalPrefab이 연결되지 않았습니다.");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillStopSignal))
            return;

        if (hasDeployStopSignalTrigger)
        {
            if (stopSignalDeployRoutine != null)
                StopCoroutine(stopSignalDeployRoutine);

            stopSignalDeployRoutine = StartCoroutine(StopSignalDeployRoutine(targetPos));
            return;
        }

        Debug.LogWarning($"[Engineer {AgentID}] Animator에 DeployStopSignal Trigger가 없습니다. 애니메이션 없이 정지 신호를 설치합니다.");

        if (stopWhenUseSkill)
            ForceStopForSkill();

        DeployStopSignal(targetPos);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void TryDemolishObstacle()
    {
        if (!TryFindNearestObstacle(out GameObject targetObject, out Vector3 targetPosition))
        {
            Debug.LogWarning($"[Engineer {AgentID}] 철거 가능한 Obstacle 레이어 오브젝트가 범위 안에 없습니다.");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillDemolition))
            return;

        if (stopWhenUseSkill)
            ForceStopForSkill();

        RequestInstalledObjectCameraAtPosition(targetPosition);

        targetObject.SetActive(false);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);

        Debug.Log($"[Engineer {AgentID}] 철거 완료. 대상={targetObject.name}, 위치={targetPosition}");
    }

    private bool TryFindNearestObstacle(out GameObject targetObject, out Vector3 targetPosition)
    {
        targetObject = null;
        targetPosition = transform.position;

        int obstacleMask = GetDemolitionObstacleMask();

        if (obstacleMask == 0)
            return false;

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            demolitionRadius,
            obstacleMask,
            QueryTriggerInteraction.Collide
        );

        if (colliders == null || colliders.Length == 0)
            return false;

        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidateCollider = colliders[i];

            if (candidateCollider == null)
                continue;

            GameObject candidateObject = ResolveDemolitionTargetObject(candidateCollider.transform);

            if (candidateObject == null || !candidateObject.activeInHierarchy)
                continue;

            Vector3 closestPoint = candidateCollider.ClosestPoint(transform.position);
            float sqrDistance = (closestPoint - transform.position).sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            targetObject = candidateObject;
            targetPosition = closestPoint;
        }

        return targetObject != null;
    }

    private GameObject ResolveDemolitionTargetObject(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        Transform obstacleRoot = FindAncestorByName(hitTransform, demolitionRootName);

        if (obstacleRoot == null)
            return hitTransform.gameObject;

        Transform current = hitTransform;

        while (current != null && current.parent != null)
        {
            if (current.parent == obstacleRoot)
                return current.gameObject;

            current = current.parent;
        }

        return hitTransform.gameObject;
    }

    private Transform FindAncestorByName(Transform startTransform, string targetName)
    {
        if (startTransform == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform current = startTransform;

        while (current != null)
        {
            if (current.name == targetName)
                return current;

            current = current.parent;
        }

        return null;
    }


    private int GetDemolitionObstacleMask()
    {
        if (demolitionObstacleLayer.value != 0)
            return demolitionObstacleLayer.value;

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");

        if (obstacleLayer < 0)
            return 0;

        return 1 << obstacleLayer;
    }

    private void TryDeploySafeZone(Vector3 targetPos)
    {
        if (!TryConsumeSkillGaugeForSkill(SkillSafeZone))
            return;

        if (stopWhenUseSkill)
            ForceStopForSkill();

        DeploySafeZone(targetPos);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void DeploySafeZone(Vector3 targetPos)
    {
        Vector3 spawnPos = BuildSpawnPosition(targetPos);
        Quaternion spawnRotation = Quaternion.identity;

        if (replaceExistingSafeZone && currentSafeZone != null)
        {
            Destroy(currentSafeZone);
            currentSafeZone = null;
        }

        GameObject spawnedSafeZone;

        if (safeZonePrefab != null)
        {
            spawnedSafeZone = Instantiate(
                safeZonePrefab,
                spawnPos,
                spawnRotation,
                deployParent != null ? deployParent : null
            );
        }
        else
        {
            spawnedSafeZone = new GameObject($"Engineer_{AgentID}_SafeZone");
            spawnedSafeZone.transform.SetParent(deployParent != null ? deployParent : null);
            spawnedSafeZone.transform.SetPositionAndRotation(spawnPos, spawnRotation);
        }

        EngineerSafeZone safeZone = spawnedSafeZone.GetComponent<EngineerSafeZone>();

        if (safeZone == null)
            safeZone = spawnedSafeZone.AddComponent<EngineerSafeZone>();

        safeZone.Configure(
            safeZoneSize,
            safeZoneLifeTime,
            safeZoneGaugeRecoveryPerSecond
        );

        currentSafeZone = spawnedSafeZone;

        RequestInstalledObjectCamera(spawnedSafeZone.transform);

        Debug.Log(
            $"[Engineer {AgentID}] 안전 구역 설치 완료. " +
            $"Position={spawnPos}, Size={safeZoneSize}, LifeTime={safeZoneLifeTime}, RecoveryPerSecond={safeZoneGaugeRecoveryPerSecond}"
        );
    }

    private IEnumerator BarricadeDeployRoutine(Vector3 targetPos)
    {
        isBarricadeDeployLocked = true;

        if (stopWhenUseSkill)
            ForceStopForSkill();

        UpdateAnimationState(true);

        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);
        ResetAnimatorTrigger(deployStopSignalHash, hasDeployStopSignalTrigger);

        animator.SetTrigger(deployBarricadeHash);

        float spawnDelay = Mathf.Clamp(barricadeSpawnDelay, 0f, barricadeDeployLockSeconds);

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        DeployBarricade(targetPos);

        float remainTime = barricadeDeployLockSeconds - spawnDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isBarricadeDeployLocked = false;
        barricadeDeployRoutine = null;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh && !isResultAnimationLocked)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private IEnumerator StopSignalDeployRoutine(Vector3 targetPos)
    {
        isStopSignalDeployLocked = true;

        if (stopWhenUseSkill)
            ForceStopForSkill();

        UpdateAnimationState(true);

        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);
        ResetAnimatorTrigger(deployBarricadeHash, hasDeployBarricadeTrigger);

        animator.SetTrigger(deployStopSignalHash);

        float spawnDelay = Mathf.Clamp(stopSignalSpawnDelay, 0f, stopSignalDeployLockSeconds);

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        DeployStopSignal(targetPos);

        float remainTime = stopSignalDeployLockSeconds - spawnDelay;

        if (remainTime > 0f)
            yield return new WaitForSeconds(remainTime);

        isStopSignalDeployLocked = false;
        stopSignalDeployRoutine = null;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh && !isResultAnimationLocked)
            navAgent.isStopped = false;

        UpdateAnimationState(true);
    }

    private void ForceStopForSkill()
    {
        currentTarget = null;
        isManualMoving = false;

        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void KeepStopped()
    {
        if (navAgent == null)
            return;

        if (!navAgent.isActiveAndEnabled)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        navAgent.ResetPath();
    }

    private void DeployBarricade(Vector3 targetPos)
    {
        Vector3 centerSpawnPos = BuildSpawnPosition(targetPos);
        Quaternion spawnRotation = BuildPlacementRotationTowardTarget(centerSpawnPos, barricadeYawOffset);

        if (replaceExistingBarricade)
            ClearCurrentBarricades();

        int spawnCount = Mathf.Max(1, currentBarricadeSpawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = GetBarricadeSpawnPosition(
                centerSpawnPos,
                spawnRotation,
                i,
                spawnCount
            );

            spawnPos = BuildSpawnPosition(spawnPos);

            GameObject spawnedBarricade = Instantiate(
                barricadePrefab,
                Vector3.zero,
                spawnRotation,
                deployParent != null ? deployParent : null
            );

            Barricade barricade = spawnedBarricade.GetComponent<Barricade>();

            if (barricade != null)
            {
                barricade.Deploy(
                    spawnPos,
                    spawnRotation,
                    currentBarricadeScaleMultiplier
                );
            }
            else
            {
                spawnedBarricade.transform.SetPositionAndRotation(spawnPos, spawnRotation);
                spawnedBarricade.transform.localScale *= currentBarricadeScaleMultiplier;
            }

            currentBarricades.Add(spawnedBarricade);
        }

        RequestInstalledObjectCameraAtPosition(centerSpawnPos);

        Debug.Log(
            $"[Engineer {AgentID}] 바리케이드 설치 완료. " +
            $"Center={centerSpawnPos}, Count={spawnCount}, ScaleMultiplier={currentBarricadeScaleMultiplier:F2}"
        );
    }

    private void DeployStopSignal(Vector3 targetPos)
    {
        Vector3 spawnPos = BuildSpawnPosition(targetPos);
        Quaternion spawnRotation = Quaternion.identity;

        if (replaceExistingStopSignal && currentStopSignal != null)
        {
            Destroy(currentStopSignal);
            currentStopSignal = null;
        }

        GameObject spawnedStopSignal = Instantiate(
            stopSignalPrefab,
            spawnPos,
            spawnRotation,
            deployParent != null ? deployParent : null
        );

        StopSignal stopSignal = spawnedStopSignal.GetComponent<StopSignal>();

        if (stopSignal == null)
            stopSignal = spawnedStopSignal.AddComponent<StopSignal>();

        float finalRadius = GetCurrentStopSignalRadius();
        float finalStopDuration = GetCurrentStopSignalDuration();
        float finalLifeTime = GetCurrentStopSignalLifeTime();

        stopSignal.Configure(
            finalRadius,
            finalStopDuration,
            finalLifeTime,
            targetLayer
        );

        currentStopSignal = spawnedStopSignal;

        RequestInstalledObjectCamera(spawnedStopSignal.transform);

        Debug.Log(
            $"[Engineer {AgentID}] 정지 신호 설치 완료. " +
            $"Position={spawnPos}, Radius={finalRadius}, StopDuration={finalStopDuration}, LifeTime={finalLifeTime}"
        );
    }

    private Vector3 GetBarricadeSpawnPosition(
        Vector3 centerPosition,
        Quaternion rotation,
        int index,
        int count)
    {
        if (count <= 1)
            return centerPosition;

        float centerOffset = (count - 1) * 0.5f;
        float offsetIndex = index - centerOffset;

        Vector3 right = rotation * Vector3.right;
        return centerPosition + right * offsetIndex * multiBarricadeSpacing;
    }

    private void ClearCurrentBarricades()
    {
        if (currentBarricades.Count <= 0)
            return;

        for (int i = 0; i < currentBarricades.Count; i++)
        {
            if (currentBarricades[i] != null)
                Destroy(currentBarricades[i]);
        }

        currentBarricades.Clear();
    }

    private float GetCurrentStopSignalRadius()
    {
        return stopSignalRadius * currentStopSignalRadiusMultiplier;
    }

    private float GetCurrentStopSignalDuration()
    {
        return stopSignalDuration * currentStopSignalDurationMultiplier;
    }

    private float GetCurrentStopSignalLifeTime()
    {
        return stopSignalLifeTime * currentStopSignalLifeTimeMultiplier;
    }

    private void RequestInstalledObjectCameraAtPosition(Vector3 focusPosition)
    {
        Transform focusAnchor = GetOrCreateSkillCameraFocusAnchor();

        focusAnchor.position = focusPosition;
        focusAnchor.rotation = Quaternion.identity;

        RequestInstalledObjectCamera(focusAnchor);
    }

    private Transform GetOrCreateSkillCameraFocusAnchor()
    {
        if (skillCameraFocusAnchor != null)
            return skillCameraFocusAnchor;

        GameObject anchorObject = new GameObject($"Engineer_{AgentID}_SkillCameraFocusAnchor");
        anchorObject.hideFlags = HideFlags.HideInHierarchy;

        skillCameraFocusAnchor = anchorObject.transform;
        skillCameraFocusAnchor.SetParent(transform, false);

        return skillCameraFocusAnchor;
    }

    private Vector3 BuildSpawnPosition(Vector3 targetPos)
    {
        Vector3 desiredPosition = targetPos;

        if (NavMesh.SamplePosition(
                targetPos,
                out NavMeshHit navHit,
                placementNavMeshSampleRadius,
                NavMesh.AllAreas))
        {
            desiredPosition = navHit.position;
        }

        if (placementGroundLayer.value == 0)
        {
            return new Vector3(
                desiredPosition.x,
                desiredPosition.y + deployY,
                desiredPosition.z
            );
        }

        Vector3 rayOrigin = desiredPosition + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + groundProbeDistance;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                placementGroundLayer,
                QueryTriggerInteraction.Ignore))
        {
            return new Vector3(hit.point.x, hit.point.y + deployY, hit.point.z);
        }

        return new Vector3(
            desiredPosition.x,
            desiredPosition.y + deployY,
            desiredPosition.z
        );
    }

    private Quaternion BuildPlacementRotationTowardTarget(Vector3 spawnPos, float yawOffset)
    {
        CacheTargetTransform();

        Vector3 direction;

        if (targetTransform != null)
            direction = targetTransform.position - spawnPos;
        else
            direction = transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        Quaternion baseRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion offsetRotation = Quaternion.Euler(0f, yawOffset, 0f);

        return baseRotation * offsetRotation;
    }

    private EngineerMoveMode ResolveEngineerMoveMode(bool isMoving)
    {
        if (isResultAnimationLocked || isHitReactionLocked || isBarricadeDeployLocked || isStopSignalDeployLocked)
            return EngineerMoveMode.IdleLookAround;

        if (!isMoving)
            return EngineerMoveMode.IdleLookAround;

        if (IsSmokeDebuffed)
            return EngineerMoveMode.DebuffedRun;

        return EngineerMoveMode.Run;
    }

    private bool ResolveEngineerIsMoving()
    {
        if (navAgent == null)
            return false;

        if (navAgent.isStopped)
            return false;

        bool hasMovementIntent =
            navAgent.pathPending ||
            isManualMoving ||
            currentTarget != null ||
            IsFollowingSharedTargetPosition ||
            HasActivePathForEngineerAnimation();

        bool hasVelocity =
            navAgent.velocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold ||
            navAgent.desiredVelocity.sqrMagnitude > animationMovingThreshold * animationMovingThreshold;

        bool hasNotReachedDestination = !HasReachedDestinationForEngineerAnimation();

        return hasMovementIntent && (hasVelocity || hasNotReachedDestination);
    }

    private bool HasActivePathForEngineerAnimation()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.hasPath)
            return false;

        if (navAgent.pathPending)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return true;

        return !HasReachedDestinationForEngineerAnimation();
    }

    private bool HasReachedDestinationForEngineerAnimation()
    {
        if (navAgent == null)
            return true;

        if (navAgent.pathPending)
            return false;

        if (!navAgent.hasPath)
            return true;

        if (float.IsInfinity(navAgent.remainingDistance))
            return false;

        float stopDistance = Mathf.Max(navAgent.stoppingDistance, 0.05f);
        return navAgent.remainingDistance <= stopDistance + destinationBuffer;
    }

    private IEnumerator HitReactionRoutine(Vector3 hitSourcePosition)
    {
        isHitReactionLocked = true;

        StopBarricadeDeployRoutine();
        StopStopSignalDeployRoutine();

        bool previousStopped = false;
        bool previousUpdateRotation = true;

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            previousStopped = navAgent.isStopped;
            previousUpdateRotation = navAgent.updateRotation;

            navAgent.isStopped = true;
            navAgent.updateRotation = false;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }

        if (faceAwayFromHitSource)
            FaceAwayFromHitSource(hitSourcePosition);

        UpdateAnimationState(true);
        SetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);

        if (hitReactionLockSeconds > 0f)
            yield return new WaitForSeconds(hitReactionLockSeconds);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh && !isResultAnimationLocked)
        {
            navAgent.isStopped = previousStopped;
            navAgent.updateRotation = previousUpdateRotation;
        }

        isHitReactionLocked = false;
        hitReactionRoutine = null;

        UpdateAnimationState(true);
    }

    private void FaceAwayFromHitSource(Vector3 hitSourcePosition)
    {
        Vector3 awayDirection = transform.position - hitSourcePosition;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(awayDirection.normalized, Vector3.up);
    }

    private void PlayResultAnimation(int triggerHash, bool hasTrigger, string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"[Engineer {AgentID}] Animator가 없어서 {triggerName} 애니메이션을 실행할 수 없습니다.");
            return;
        }

        if (!hasTrigger)
        {
            Debug.LogWarning($"[Engineer {AgentID}] Animator에 {triggerName} Trigger가 없습니다.");
            return;
        }

        isResultAnimationLocked = true;
        isHitReactionLocked = false;
        isBarricadeDeployLocked = false;
        isStopSignalDeployLocked = false;

        StopBarricadeDeployRoutine();
        StopStopSignalDeployRoutine();
        StopHitReactionRoutine();

        currentTarget = null;
        isManualMoving = false;
        ClearSharedTargetPosition();

        KeepStopped();
        UpdateAnimationState(true);

        ResetAnimatorTrigger(hitReactionHash, hasHitReactionTrigger);
        ResetAnimatorTrigger(victoryHash, hasVictoryTrigger);
        ResetAnimatorTrigger(defeatHash, hasDefeatTrigger);
        ResetAnimatorTrigger(deployBarricadeHash, hasDeployBarricadeTrigger);
        ResetAnimatorTrigger(deployStopSignalHash, hasDeployStopSignalTrigger);

        animator.SetTrigger(triggerHash);

        Debug.Log($"[Engineer {AgentID}] {triggerName} 애니메이션 실행");
    }

    private void StopBarricadeDeployRoutine()
    {
        if (barricadeDeployRoutine == null)
            return;

        StopCoroutine(barricadeDeployRoutine);
        barricadeDeployRoutine = null;
        isBarricadeDeployLocked = false;
    }

    private void StopStopSignalDeployRoutine()
    {
        if (stopSignalDeployRoutine == null)
            return;

        StopCoroutine(stopSignalDeployRoutine);
        stopSignalDeployRoutine = null;
        isStopSignalDeployLocked = false;
    }

    private void StopHitReactionRoutine()
    {
        if (hitReactionRoutine == null)
            return;

        StopCoroutine(hitReactionRoutine);
        hitReactionRoutine = null;
        isHitReactionLocked = false;
    }

    private void SetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
            return;

        animator.SetTrigger(triggerHash);
    }

    private void ResetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
            return;

        animator.ResetTrigger(triggerHash);
    }

    private void CacheEngineerAnimationHashes()
    {
        isMovingHash = Animator.StringToHash(IsMovingParameter);
        moveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        moveModeHash = Animator.StringToHash(MoveModeParameter);
        hitReactionHash = Animator.StringToHash(HitReactionTriggerName);
        victoryHash = Animator.StringToHash(VictoryTriggerName);
        defeatHash = Animator.StringToHash(DefeatTriggerName);
        deployBarricadeHash = Animator.StringToHash(DeployBarricadeTriggerName);
        deployStopSignalHash = Animator.StringToHash(DeployStopSignalTriggerName);
    }

    private void CacheEngineerAnimatorParameters()
    {
        hasIsMovingParameter = HasAnimatorParameter(IsMovingParameter, AnimatorControllerParameterType.Bool);
        hasMoveSpeedParameter = HasAnimatorParameter(MoveSpeedParameter, AnimatorControllerParameterType.Float);
        hasMoveModeParameter = HasAnimatorParameter(MoveModeParameter, AnimatorControllerParameterType.Int);
        hasHitReactionTrigger = HasAnimatorParameter(HitReactionTriggerName, AnimatorControllerParameterType.Trigger);
        hasVictoryTrigger = HasAnimatorParameter(VictoryTriggerName, AnimatorControllerParameterType.Trigger);
        hasDefeatTrigger = HasAnimatorParameter(DefeatTriggerName, AnimatorControllerParameterType.Trigger);
        hasDeployBarricadeTrigger = HasAnimatorParameter(DeployBarricadeTriggerName, AnimatorControllerParameterType.Trigger);
        hasDeployStopSignalTrigger = HasAnimatorParameter(DeployStopSignalTriggerName, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == parameterType)
                return true;
        }

        Debug.LogWarning($"[Engineer {AgentID}] Animator 파라미터가 없습니다: {parameterName} ({parameterType})");
        return false;
    }

    private void CacheTargetTransform()
    {
        if (targetTransform != null)
            return;

        if (!autoFindTargetIfMissing)
            return;

        TargetController foundTarget = FindFirstObjectByType<TargetController>();

        if (foundTarget != null)
            targetTransform = foundTarget.transform;
    }
}