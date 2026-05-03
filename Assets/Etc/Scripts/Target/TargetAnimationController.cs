using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum TargetAnimationState
{
    LookAround = 0,
    Walk = 1,
    Run = 2,
    TimeOver = 3,
    Captured = 4,
    Exhausted = 5
}

public class TargetAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private TargetController targetController;
    [SerializeField] private TargetThreatTracker threatTracker;
    [SerializeField] private TargetEscapeMotor escapeMotor;
    [SerializeField] private TargetWanderMotor wanderMotor;
    [SerializeField] private TargetSkillController skillController;

    [Header("Animator Parameter Names")]
    [SerializeField] private string stateParameterName = "TargetAnimState";
    [SerializeField] private string moveSpeedParameterName = "MoveSpeed";
    [SerializeField] private string skillLockedParameterName = "IsSkillLocked";
    [SerializeField] private string emergencyEscapingParameterName = "IsEmergencyEscaping";
    [SerializeField] private string exhaustedParameterName = "IsExhausted";

    [SerializeField] private string communicationJamTriggerName = "SkillCommunicationJam";
    [SerializeField] private string commandDistortionTriggerName = "SkillCommandDistortion";
    [SerializeField] private string hologramSlideTriggerName = "HologramSlide";
    [SerializeField] private string emergencyEscapeTriggerName = "EmergencyEscape";
    [SerializeField] private string capturedTriggerName = "Captured";
    [SerializeField] private string timeOverTriggerName = "TimeOver";
    [SerializeField] private string exhaustedTriggerName = "Exhausted";

    [Header("Movement Detection")]
    [SerializeField] private float movingSpeedThreshold = 0.08f;
    [SerializeField] private float arriveDistance = 0.35f;
    [SerializeField] private float moveSpeedDampTime = 0.08f;

    [Header("Skill Animation Duration")]
    [SerializeField] private float communicationJamLockDuration = 1.2f;
    [SerializeField] private float commandDistortionLockDuration = 1.2f;
    [SerializeField] private bool useRootForLockedSkill = true;

    [Header("End State")]
    [SerializeField] private bool disableTargetLogicOnTimeOver = true;

    [Header("Animator Option")]
    [SerializeField] private bool forceDisableRootMotion = true;

    private Coroutine skillLockRoutine;

    private int stateHash;
    private int moveSpeedHash;
    private int skillLockedHash;
    private int emergencyEscapingHash;
    private int exhaustedHash;

    private int communicationJamTriggerHash;
    private int commandDistortionTriggerHash;
    private int hologramSlideTriggerHash;
    private int emergencyEscapeTriggerHash;
    private int capturedTriggerHash;
    private int timeOverTriggerHash;
    private int exhaustedTriggerHash;

    private bool hasStateParameter;
    private bool hasMoveSpeedParameter;
    private bool hasSkillLockedParameter;
    private bool hasEmergencyEscapingParameter;
    private bool hasExhaustedParameter;

    private bool hasCommunicationJamTrigger;
    private bool hasCommandDistortionTrigger;
    private bool hasHologramSlideTrigger;
    private bool hasEmergencyEscapeTrigger;
    private bool hasCapturedTrigger;
    private bool hasTimeOverTrigger;
    private bool hasExhaustedTrigger;

    private bool isSkillLocked;
    private bool isTimeOver;
    private bool capturedTriggered;
    private bool exhaustedTriggered;
    private bool wasEmergencyEscaping;
    private int lastEmergencyEscapeTriggerFrame = -1;

    private bool manualAgentLockActive;
    private bool manualAgentWasStopped;

    private bool storedLogicState;
    private bool storedTargetControllerEnabled;
    private bool storedEscapeMotorEnabled;
    private bool storedWanderMotorEnabled;
    private bool storedSkillControllerEnabled;

    private void Awake()
    {
        ResolveReferences();

        if (animator != null && forceDisableRootMotion)
            animator.applyRootMotion = false;

        CacheAnimatorHashes();
        CacheAnimatorParameterStatus();
    }

    private void OnValidate()
    {
        movingSpeedThreshold = Mathf.Max(0f, movingSpeedThreshold);
        arriveDistance = Mathf.Max(0f, arriveDistance);
        moveSpeedDampTime = Mathf.Max(0f, moveSpeedDampTime);

        communicationJamLockDuration = Mathf.Max(0.01f, communicationJamLockDuration);
        commandDistortionLockDuration = Mathf.Max(0.01f, commandDistortionLockDuration);
    }

    private void Update()
    {
        bool isCaught = IsCaughtNow();
        bool isExhausted = IsExhaustedNow();

        if (isCaught && !capturedTriggered)
            PlayCaptured();

        if (!isCaught && isExhausted && !exhaustedTriggered)
            PlayExhausted();

        if (!isCaught && !isExhausted && exhaustedTriggered)
            ClearExhaustedState();

        if (isTimeOver || isExhausted)
            ForceStopAgent(true);

        bool isEmergencyEscaping = escapeMotor != null && escapeMotor.IsEmergencyEscaping;

        if (!isCaught &&
            !isExhausted &&
            isEmergencyEscaping &&
            !wasEmergencyEscaping)
        {
            PlayEmergencyEscape();
        }

        wasEmergencyEscaping = isEmergencyEscaping;

        if (animator == null)
            return;

        TargetAnimationState animationState = DecideAnimationState(
            isCaught,
            isExhausted,
            isEmergencyEscaping
        );

        float moveSpeed = GetCurrentMoveSpeed();

        SetIntegerSafe(stateHash, (int)animationState, hasStateParameter);
        SetFloatSafe(moveSpeedHash, moveSpeed, hasMoveSpeedParameter);
        SetBoolSafe(skillLockedHash, isSkillLocked, hasSkillLockedParameter);
        SetBoolSafe(emergencyEscapingHash, isEmergencyEscaping, hasEmergencyEscapingParameter);
        SetBoolSafe(exhaustedHash, isExhausted, hasExhaustedParameter);
    }

    public void PlayCommunicationJamSkill()
    {
        PlayLockedSkillAnimation(
            communicationJamTriggerHash,
            hasCommunicationJamTrigger,
            communicationJamLockDuration
        );
    }

    public void PlayCommandDistortionSkill()
    {
        PlayLockedSkillAnimation(
            commandDistortionTriggerHash,
            hasCommandDistortionTrigger,
            commandDistortionLockDuration
        );
    }

    public void PlayHologramSlide()
    {
        if (IsSpecialAnimationBlocked())
            return;

        SetTriggerSafe(hologramSlideTriggerHash, hasHologramSlideTrigger);
    }

    public void PlayEmergencyEscape()
    {
        if (IsSpecialAnimationBlocked())
            return;

        if (Time.frameCount == lastEmergencyEscapeTriggerFrame)
            return;

        lastEmergencyEscapeTriggerFrame = Time.frameCount;
        SetTriggerSafe(emergencyEscapeTriggerHash, hasEmergencyEscapeTrigger);
    }

    public void PlayTimeOverCelebration()
    {
        if (IsCaughtNow())
            return;

        if (IsExhaustedNow())
            return;

        isTimeOver = true;
        isSkillLocked = false;

        StopSkillLockRoutine();
        ForceStopAgent(true);

        if (disableTargetLogicOnTimeOver)
            DisableTargetLogicForTimeOver();

        SetIntegerSafe(stateHash, (int)TargetAnimationState.TimeOver, hasStateParameter);
        SetTriggerSafe(timeOverTriggerHash, hasTimeOverTrigger);
    }

    public void ClearTimeOverCelebration()
    {
        isTimeOver = false;

        if (storedLogicState)
            RestoreTargetLogicAfterTimeOver();

        SetIntegerSafe(stateHash, (int)TargetAnimationState.LookAround, hasStateParameter);
    }

    public void PlayCaptured()
    {
        if (capturedTriggered)
            return;

        capturedTriggered = true;
        isTimeOver = false;
        isSkillLocked = false;

        StopSkillLockRoutine();
        ForceStopAgent(true);

        SetIntegerSafe(stateHash, (int)TargetAnimationState.Captured, hasStateParameter);
        SetTriggerSafe(capturedTriggerHash, hasCapturedTrigger);
    }

    public void PlayExhausted()
    {
        if (capturedTriggered)
            return;

        exhaustedTriggered = true;
        isTimeOver = false;
        isSkillLocked = false;

        StopSkillLockRoutine();
        ForceStopAgent(true);

        SetIntegerSafe(stateHash, (int)TargetAnimationState.Exhausted, hasStateParameter);
        SetBoolSafe(exhaustedHash, true, hasExhaustedParameter);
        SetTriggerSafe(exhaustedTriggerHash, hasExhaustedTrigger);
    }

    public void ClearExhaustedState()
    {
        exhaustedTriggered = false;
        SetBoolSafe(exhaustedHash, false, hasExhaustedParameter);
    }

    private void PlayLockedSkillAnimation(int triggerHash, bool hasTrigger, float duration)
    {
        if (IsSpecialAnimationBlocked())
            return;

        StopSkillLockRoutine();
        skillLockRoutine = StartCoroutine(LockedSkillRoutine(triggerHash, hasTrigger, duration));
    }

    private IEnumerator LockedSkillRoutine(int triggerHash, bool hasTrigger, float duration)
    {
        isSkillLocked = true;
        SetBoolSafe(skillLockedHash, true, hasSkillLockedParameter);
        SetTriggerSafe(triggerHash, hasTrigger);

        if (useRootForLockedSkill && escapeMotor != null)
        {
            escapeMotor.ApplyRoot(duration);
        }
        else
        {
            BeginManualAgentLock();
        }

        yield return new WaitForSeconds(duration);

        isSkillLocked = false;
        SetBoolSafe(skillLockedHash, false, hasSkillLockedParameter);

        if (!useRootForLockedSkill)
            EndManualAgentLock();

        skillLockRoutine = null;
    }

    private TargetAnimationState DecideAnimationState(
        bool isCaught,
        bool isExhausted,
        bool isEmergencyEscaping)
    {
        if (isCaught)
            return TargetAnimationState.Captured;

        if (isExhausted)
            return TargetAnimationState.Exhausted;

        if (isTimeOver)
            return TargetAnimationState.TimeOver;

        if (isSkillLocked)
            return TargetAnimationState.LookAround;

        if (isEmergencyEscaping)
            return TargetAnimationState.Run;

        bool hasThreat = HasThreat();

        if (hasThreat)
            return TargetAnimationState.Run;

        if (IsMoving())
            return TargetAnimationState.Walk;

        return TargetAnimationState.LookAround;
    }

    private bool HasThreat()
    {
        if (targetController != null)
            return targetController.HasActiveThreat;

        if (threatTracker != null)
            return threatTracker.HasAnyThreat();

        return false;
    }

    private bool IsMoving()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return false;

        if (navAgent.velocity.sqrMagnitude >= movingSpeedThreshold * movingSpeedThreshold)
            return true;

        if (navAgent.pathPending)
            return false;

        if (!navAgent.hasPath)
            return false;

        return navAgent.remainingDistance > navAgent.stoppingDistance + arriveDistance;
    }

    private float GetCurrentMoveSpeed()
    {
        if (navAgent == null)
            return 0f;

        return navAgent.velocity.magnitude;
    }

    private bool IsCaughtNow()
    {
        return targetController != null && targetController.IsCaught;
    }

    private bool IsExhaustedNow()
    {
        return targetController != null && targetController.IsExhausted;
    }

    private bool IsSpecialAnimationBlocked()
    {
        if (isTimeOver)
            return true;

        if (IsCaughtNow())
            return true;

        if (IsExhaustedNow())
            return true;

        return false;
    }

    private void BeginManualAgentLock()
    {
        if (!CanUseNavAgent())
            return;

        manualAgentLockActive = true;
        manualAgentWasStopped = navAgent.isStopped;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
    }

    private void EndManualAgentLock()
    {
        if (!manualAgentLockActive)
            return;

        manualAgentLockActive = false;

        if (!CanUseNavAgent())
            return;

        if (!manualAgentWasStopped && !IsSpecialAnimationBlocked())
            navAgent.isStopped = false;
    }

    private void ForceStopAgent(bool clearPath)
    {
        if (!CanUseNavAgent())
            return;

        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;

        if (clearPath)
            navAgent.ResetPath();
    }

    private bool CanUseNavAgent()
    {
        if (navAgent == null)
            return false;

        if (!navAgent.isActiveAndEnabled)
            return false;

        if (!navAgent.isOnNavMesh)
            return false;

        return true;
    }

    private void DisableTargetLogicForTimeOver()
    {
        if (storedLogicState)
            return;

        storedLogicState = true;

        storedTargetControllerEnabled = targetController != null && targetController.enabled;
        storedEscapeMotorEnabled = escapeMotor != null && escapeMotor.enabled;
        storedWanderMotorEnabled = wanderMotor != null && wanderMotor.enabled;
        storedSkillControllerEnabled = skillController != null && skillController.enabled;

        if (targetController != null)
            targetController.enabled = false;

        if (escapeMotor != null)
            escapeMotor.enabled = false;

        if (wanderMotor != null)
            wanderMotor.enabled = false;

        if (skillController != null)
            skillController.enabled = false;
    }

    private void RestoreTargetLogicAfterTimeOver()
    {
        storedLogicState = false;

        if (targetController != null)
            targetController.enabled = storedTargetControllerEnabled;

        if (escapeMotor != null)
            escapeMotor.enabled = storedEscapeMotorEnabled;

        if (wanderMotor != null)
            wanderMotor.enabled = storedWanderMotorEnabled;

        if (skillController != null)
            skillController.enabled = storedSkillControllerEnabled;
    }

    private void StopSkillLockRoutine()
    {
        if (skillLockRoutine == null)
            return;

        StopCoroutine(skillLockRoutine);
        skillLockRoutine = null;

        isSkillLocked = false;
        EndManualAgentLock();
        SetBoolSafe(skillLockedHash, false, hasSkillLockedParameter);
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (targetController == null)
            targetController = GetComponent<TargetController>();

        if (threatTracker == null)
            threatTracker = GetComponent<TargetThreatTracker>();

        if (escapeMotor == null)
            escapeMotor = GetComponent<TargetEscapeMotor>();

        if (wanderMotor == null)
            wanderMotor = GetComponent<TargetWanderMotor>();

        if (skillController == null)
            skillController = GetComponent<TargetSkillController>();
    }

    private void CacheAnimatorHashes()
    {
        stateHash = Animator.StringToHash(stateParameterName);
        moveSpeedHash = Animator.StringToHash(moveSpeedParameterName);
        skillLockedHash = Animator.StringToHash(skillLockedParameterName);
        emergencyEscapingHash = Animator.StringToHash(emergencyEscapingParameterName);
        exhaustedHash = Animator.StringToHash(exhaustedParameterName);

        communicationJamTriggerHash = Animator.StringToHash(communicationJamTriggerName);
        commandDistortionTriggerHash = Animator.StringToHash(commandDistortionTriggerName);
        hologramSlideTriggerHash = Animator.StringToHash(hologramSlideTriggerName);
        emergencyEscapeTriggerHash = Animator.StringToHash(emergencyEscapeTriggerName);
        capturedTriggerHash = Animator.StringToHash(capturedTriggerName);
        timeOverTriggerHash = Animator.StringToHash(timeOverTriggerName);
        exhaustedTriggerHash = Animator.StringToHash(exhaustedTriggerName);
    }

    private void CacheAnimatorParameterStatus()
    {
        hasStateParameter = HasAnimatorParameter(stateParameterName, AnimatorControllerParameterType.Int);
        hasMoveSpeedParameter = HasAnimatorParameter(moveSpeedParameterName, AnimatorControllerParameterType.Float);
        hasSkillLockedParameter = HasAnimatorParameter(skillLockedParameterName, AnimatorControllerParameterType.Bool);
        hasEmergencyEscapingParameter = HasAnimatorParameter(emergencyEscapingParameterName, AnimatorControllerParameterType.Bool);
        hasExhaustedParameter = HasAnimatorParameter(exhaustedParameterName, AnimatorControllerParameterType.Bool);

        hasCommunicationJamTrigger = HasAnimatorParameter(communicationJamTriggerName, AnimatorControllerParameterType.Trigger);
        hasCommandDistortionTrigger = HasAnimatorParameter(commandDistortionTriggerName, AnimatorControllerParameterType.Trigger);
        hasHologramSlideTrigger = HasAnimatorParameter(hologramSlideTriggerName, AnimatorControllerParameterType.Trigger);
        hasEmergencyEscapeTrigger = HasAnimatorParameter(emergencyEscapeTriggerName, AnimatorControllerParameterType.Trigger);
        hasCapturedTrigger = HasAnimatorParameter(capturedTriggerName, AnimatorControllerParameterType.Trigger);
        hasTimeOverTrigger = HasAnimatorParameter(timeOverTriggerName, AnimatorControllerParameterType.Trigger);
        hasExhaustedTrigger = HasAnimatorParameter(exhaustedTriggerName, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == parameterType)
                return true;
        }

        return false;
    }

    private void SetIntegerSafe(int hash, int value, bool hasParameter)
    {
        if (animator == null || !hasParameter)
            return;

        animator.SetInteger(hash, value);
    }

    private void SetFloatSafe(int hash, float value, bool hasParameter)
    {
        if (animator == null || !hasParameter)
            return;

        animator.SetFloat(hash, value, moveSpeedDampTime, Time.deltaTime);
    }

    private void SetBoolSafe(int hash, bool value, bool hasParameter)
    {
        if (animator == null || !hasParameter)
            return;

        animator.SetBool(hash, value);
    }

    private void SetTriggerSafe(int hash, bool hasParameter)
    {
        if (animator == null || !hasParameter)
            return;

        animator.SetTrigger(hash);
    }
}