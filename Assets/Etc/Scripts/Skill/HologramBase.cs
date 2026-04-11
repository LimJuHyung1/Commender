using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public abstract class HologramBase : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool showDebugGizmo = true;
    [SerializeField] private float debugRadius = 0.75f;
    [SerializeField] private float lifeTime = 0f;

    private SphereCollider triggerCollider;
    private Coroutine lifeRoutine;
    private bool isDestroying;

    public Vector3 Position => transform.position;
    public Transform OwnerRoot => ownerRoot;
    public float DebugRadius => debugRadius;

    protected abstract Color GizmoColor { get; }

    protected virtual void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        ConfigureTriggerCollider();
    }

    protected virtual void OnValidate()
    {
        debugRadius = Mathf.Max(0.1f, debugRadius);
        lifeTime = Mathf.Max(0f, lifeTime);

        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        ConfigureTriggerCollider();
    }

    protected virtual void OnEnable()
    {
        isDestroying = false;
        RegisterInstance();

        if (lifeRoutine != null)
            StopCoroutine(lifeRoutine);

        if (lifeTime > 0f)
            lifeRoutine = StartCoroutine(LifeRoutine());
    }

    protected virtual void OnDisable()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }

        UnregisterInstance();
    }

    protected virtual void OnDestroy()
    {
        UnregisterInstance();
    }

    public void Initialize(Transform newOwnerRoot)
    {
        ownerRoot = newOwnerRoot;
    }

    protected void DestroySelf()
    {
        if (isDestroying)
            return;

        isDestroying = true;
        Destroy(gameObject);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        OnHologramTriggerEnter(other);
    }

    protected virtual void OnHologramTriggerEnter(Collider other)
    {
    }

    protected abstract void RegisterInstance();
    protected abstract void UnregisterInstance();

    private IEnumerator LifeRoutine()
    {
        yield return new WaitForSeconds(lifeTime);
        DestroySelf();
    }

    private void ConfigureTriggerCollider()
    {
        if (triggerCollider == null)
            return;

        triggerCollider.isTrigger = true;
        triggerCollider.radius = debugRadius;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmo)
            return;

        Gizmos.color = GizmoColor;
        Gizmos.DrawWireSphere(transform.position, debugRadius);
    }
}