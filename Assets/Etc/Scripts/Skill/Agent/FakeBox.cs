using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FakeBox : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Punch Trap Animation")]
    [SerializeField] private Animator punchTrapAnimator;
    [SerializeField] private string punchTrapChildName = "PunchTrap";
    [SerializeField] private bool autoFindPunchTrapAnimator = true;
    [SerializeField] private bool disableAnimatorUntilTriggered = true;

    [Header("Animation Play Mode")]
    [SerializeField] private bool useTriggerParameter = true;
    [SerializeField] private string punchTriggerParameter = "Punch";
    [SerializeField] private string punchStateName = "";
    [SerializeField] private int punchAnimatorLayerIndex = 0;

    [Header("Effect")]
    [SerializeField] private GameObject triggerEffectPrefab;

    [Header("Route Interference")]
    [SerializeField] private int reducedRouteCandidateCount = 2;

    [Header("Destroy")]
    [SerializeField] private float destroyDelayAfterTriggered = 5f;

    private Trickster owner;
    private Collider triggerCollider;
    private bool hasTriggered;
    private Coroutine destroyRoutine;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        TryAutoFindPunchTrapAnimator();
        ResetPunchTrapAnimator();
    }

    private void OnEnable()
    {
        hasTriggered = false;

        if (triggerCollider != null)
            triggerCollider.enabled = true;

        ResetPunchTrapAnimator();
    }

    private void OnDisable()
    {
        if (destroyRoutine != null)
        {
            StopCoroutine(destroyRoutine);
            destroyRoutine = null;
        }
    }

    private void OnValidate()
    {
        reducedRouteCandidateCount = Mathf.Max(1, reducedRouteCandidateCount);
        destroyDelayAfterTriggered = Mathf.Max(0f, destroyDelayAfterTriggered);
        punchAnimatorLayerIndex = Mathf.Max(0, punchAnimatorLayerIndex);
    }

    public void SetOwner(Trickster newOwner)
    {
        owner = newOwner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        if (triggerOnlyOnce && hasTriggered)
            return;

        if (!IsTargetLayer(other.gameObject.layer))
            return;

        hasTriggered = true;

        if (triggerOnlyOnce && triggerCollider != null)
            triggerCollider.enabled = false;

        PlayPunchTrapAnimation();
        SpawnTriggerEffect();
        ApplyRouteInterference(other);

        Debug.Log($"[FakeBox] Triggered by {other.name}. Reduced route candidate count: {reducedRouteCandidateCount}");

        StartDestroyAfterTriggered();
    }

    private void TryAutoFindPunchTrapAnimator()
    {
        if (!autoFindPunchTrapAnimator)
            return;

        if (punchTrapAnimator != null)
            return;

        if (!string.IsNullOrWhiteSpace(punchTrapChildName))
        {
            Transform punchTrap = transform.Find(punchTrapChildName);

            if (punchTrap != null)
            {
                punchTrapAnimator = punchTrap.GetComponent<Animator>();

                if (punchTrapAnimator != null)
                    return;
            }
        }

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < childAnimators.Length; i++)
        {
            if (childAnimators[i] == null)
                continue;

            if (childAnimators[i].transform == transform)
                continue;

            punchTrapAnimator = childAnimators[i];
            return;
        }
    }

    private void ResetPunchTrapAnimator()
    {
        if (punchTrapAnimator == null)
            return;

        if (punchTrapAnimator.runtimeAnimatorController != null)
        {
            if (useTriggerParameter &&
                !string.IsNullOrWhiteSpace(punchTriggerParameter) &&
                HasAnimatorParameter(punchTrapAnimator, punchTriggerParameter, AnimatorControllerParameterType.Trigger))
            {
                punchTrapAnimator.ResetTrigger(punchTriggerParameter);
            }

            punchTrapAnimator.Rebind();
            punchTrapAnimator.Update(0f);
        }

        if (disableAnimatorUntilTriggered)
            punchTrapAnimator.enabled = false;
    }

    private void PlayPunchTrapAnimation()
    {
        if (punchTrapAnimator == null)
        {
            Debug.LogWarning("[FakeBox] PunchTrap Animator가 연결되지 않았습니다.");
            return;
        }

        if (punchTrapAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[FakeBox] PunchTrap Animator에 Animator Controller가 없습니다.");
            return;
        }

        if (!punchTrapAnimator.enabled)
            punchTrapAnimator.enabled = true;

        punchTrapAnimator.Rebind();
        punchTrapAnimator.Update(0f);

        if (useTriggerParameter)
        {
            if (string.IsNullOrWhiteSpace(punchTriggerParameter))
            {
                Debug.LogWarning("[FakeBox] Punch Trigger Parameter가 비어 있습니다.");
                return;
            }

            if (!HasAnimatorParameter(punchTrapAnimator, punchTriggerParameter, AnimatorControllerParameterType.Trigger))
            {
                Debug.LogWarning($"[FakeBox] Animator에 Trigger 파라미터 '{punchTriggerParameter}'가 없습니다.");
                return;
            }

            punchTrapAnimator.ResetTrigger(punchTriggerParameter);
            punchTrapAnimator.SetTrigger(punchTriggerParameter);
            return;
        }

        if (string.IsNullOrWhiteSpace(punchStateName))
        {
            Debug.LogWarning("[FakeBox] Punch State Name이 비어 있습니다.");
            return;
        }

        if (!punchTrapAnimator.HasState(punchAnimatorLayerIndex, Animator.StringToHash(punchStateName)))
        {
            Debug.LogWarning($"[FakeBox] Animator Layer {punchAnimatorLayerIndex}에 State '{punchStateName}'가 없습니다.");
            return;
        }

        punchTrapAnimator.Play(punchStateName, punchAnimatorLayerIndex, 0f);
    }

    private void ApplyRouteInterference(Collider other)
    {
        ITargetRouteInterferenceReceiver receiver = FindRouteInterferenceReceiver(other);

        if (receiver == null)
        {
            Debug.LogWarning("[FakeBox] Target entered, but ITargetRouteInterferenceReceiver was not found.");
            return;
        }

        receiver.ApplyFakeBoxRouteInterference(
            transform.position,
            reducedRouteCandidateCount
        );
    }

    private bool IsTargetLayer(int layer)
    {
        if (targetLayer.value == 0)
            return true;

        return (targetLayer.value & (1 << layer)) != 0;
    }

    private ITargetRouteInterferenceReceiver FindRouteInterferenceReceiver(Collider other)
    {
        MonoBehaviour[] parentBehaviours = other.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < parentBehaviours.Length; i++)
        {
            if (parentBehaviours[i] is ITargetRouteInterferenceReceiver receiver)
                return receiver;
        }

        MonoBehaviour[] childBehaviours = other.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < childBehaviours.Length; i++)
        {
            if (childBehaviours[i] is ITargetRouteInterferenceReceiver receiver)
                return receiver;
        }

        return null;
    }

    private bool HasAnimatorParameter(
        Animator targetAnimator,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null)
            return false;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter == null)
                continue;

            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    private void SpawnTriggerEffect()
    {
        if (triggerEffectPrefab == null)
            return;

        Instantiate(
            triggerEffectPrefab,
            transform.position,
            Quaternion.identity
        );
    }

    private void StartDestroyAfterTriggered()
    {
        if (destroyRoutine != null)
            StopCoroutine(destroyRoutine);

        destroyRoutine = StartCoroutine(DestroyAfterTriggeredRoutine());
    }

    private IEnumerator DestroyAfterTriggeredRoutine()
    {
        if (destroyDelayAfterTriggered > 0f)
            yield return new WaitForSeconds(destroyDelayAfterTriggered);

        Destroy(gameObject);
    }
}