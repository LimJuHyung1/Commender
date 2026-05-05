using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FakeBox : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Effect")]
    [SerializeField] private GameObject triggerEffectPrefab;
    [SerializeField] private float destroyDelayAfterTrigger = 0.05f;

    [Header("Route Interference")]
    [SerializeField] private int reducedRouteCandidateCount = 2;

    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 10f;

    private Trickster owner;
    private Collider triggerCollider;
    private bool hasTriggered;
    private Coroutine lifeTimeRoutine;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        hasTriggered = false;

        if (lifeTime > 0f)
            lifeTimeRoutine = StartCoroutine(LifeTimeRoutine());
    }

    private void OnDisable()
    {
        if (lifeTimeRoutine != null)
        {
            StopCoroutine(lifeTimeRoutine);
            lifeTimeRoutine = null;
        }
    }

    private void OnValidate()
    {
        reducedRouteCandidateCount = Mathf.Max(1, reducedRouteCandidateCount);
        destroyDelayAfterTrigger = Mathf.Max(0f, destroyDelayAfterTrigger);
        lifeTime = Mathf.Max(0f, lifeTime);
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

        ITargetRouteInterferenceReceiver receiver = FindRouteInterferenceReceiver(other);

        if (receiver == null)
        {
            Debug.LogWarning("[FakeBoxTrap] Target entered, but ITargetRouteInterferenceReceiver was not found.");
            return;
        }

        hasTriggered = true;

        receiver.ApplyFakeBoxRouteInterference(
            transform.position,
            reducedRouteCandidateCount
        );

        SpawnTriggerEffect();

        Debug.Log($"[FakeBoxTrap] Triggered by {other.name}. Reduced route candidate count: {reducedRouteCandidateCount}");

        Destroy(gameObject, destroyDelayAfterTrigger);
    }

    private bool IsTargetLayer(int layer)
    {
        if (targetLayer.value == 0)
            return true;

        return (targetLayer.value & (1 << layer)) != 0;
    }

    private ITargetRouteInterferenceReceiver FindRouteInterferenceReceiver(Collider other)
    {
        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITargetRouteInterferenceReceiver receiver)
                return receiver;
        }

        behaviours = other.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITargetRouteInterferenceReceiver receiver)
                return receiver;
        }

        return null;
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

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(lifeTime);
        Destroy(gameObject);
    }
}