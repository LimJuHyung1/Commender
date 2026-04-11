using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public abstract class SmokeBase : MonoBehaviour
{
    [Header("Smoke Settings")]
    [SerializeField] protected float lifetime = 10f;
    [SerializeField] protected float fadeOutTime = 2f;
    [SerializeField] protected float reducedRadius = 1.5f;
    [SerializeField] protected float debuffDuration = 10f;

    protected ParticleSystem smokeParticle;
    protected SphereCollider smokeCollider;

    private readonly HashSet<int> appliedIds = new HashSet<int>();

    protected virtual void Awake()
    {
        smokeParticle = GetComponentInChildren<ParticleSystem>();
        smokeCollider = GetComponent<SphereCollider>();

        if (smokeCollider != null)
            smokeCollider.isTrigger = true;
    }

    protected virtual void Start()
    {
        StartCoroutine(DisappearRoutine());
    }

    private IEnumerator DisappearRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (smokeCollider != null)
            smokeCollider.enabled = false;

        if (smokeParticle != null)
            smokeParticle.Stop();

        yield return new WaitForSeconds(fadeOutTime);
        Destroy(gameObject);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!CanAffect(other))
            return;

        MonoBehaviour receiverComponent = FindReceiver(other);
        if (receiverComponent == null)
            return;

        int id = receiverComponent.GetInstanceID();
        if (!appliedIds.Add(id))
            return;

        if (receiverComponent is ISmokeDebuffReceiver receiver)
        {
            receiver.ApplySmokeDebuff(reducedRadius, debuffDuration);
            OnDebuffApplied(receiverComponent);
        }
    }

    protected virtual void OnDebuffApplied(MonoBehaviour receiverComponent)
    {
        Debug.Log($"[Smoke] {name} -> {receiverComponent.name}, radius={reducedRadius}, duration={debuffDuration}");
    }

    protected abstract bool CanAffect(Collider other);
    protected abstract MonoBehaviour FindReceiver(Collider other);
}