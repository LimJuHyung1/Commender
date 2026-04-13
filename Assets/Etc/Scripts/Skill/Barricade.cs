using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BarricadeObject : MonoBehaviour
{
    [Header("Drop")]
    [SerializeField] private float dropHeight = 5f;
    [SerializeField] private float groundProbeHeight = 4f;
    [SerializeField] private float groundProbeDistance = 20f;
    [SerializeField] private LayerMask landingLayer;
    [SerializeField] private bool freezeAfterLanding = true;

    [Header("Scale")]
    [SerializeField] private float fallingStartScale = 0.5f;
    [SerializeField] private float landedScale = 1.5f;

    [Header("Impact Effect")]
    [SerializeField] private GameObject impactEffectObject;
    [SerializeField] private bool detachEffectOnPlay = false;

    private Rigidbody rb;
    private bool isDropping;
    private bool hasLanded;
    private ParticleSystem[] cachedParticleSystems;

    private float dropStartY;
    private float landingY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        CacheParticleSystems();
        PrepareImpactEffectImmediate();
    }

    private void Update()
    {
        UpdateDroppingScale();
    }

    public void Deploy(Vector3 desiredGroundPosition, Quaternion rotation)
    {
        Vector3 landingPosition = ResolveLandingPosition(desiredGroundPosition);
        Vector3 startPosition = landingPosition + Vector3.up * dropHeight;

        PrepareImpactEffectImmediate();

        transform.SetPositionAndRotation(startPosition, rotation);

        dropStartY = startPosition.y;
        landingY = landingPosition.y;

        ApplyUniformScale(fallingStartScale);

        isDropping = true;
        hasLanded = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private Vector3 ResolveLandingPosition(Vector3 desiredGroundPosition)
    {
        Vector3 rayOrigin = desiredGroundPosition + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + groundProbeDistance;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                landingLayer,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return desiredGroundPosition;
    }

    private void UpdateDroppingScale()
    {
        if (!isDropping || hasLanded)
            return;

        float totalDropDistance = dropStartY - landingY;
        if (totalDropDistance <= 0.001f)
        {
            ApplyUniformScale(landedScale);
            return;
        }

        float currentY = transform.position.y;
        float normalized = 1f - Mathf.InverseLerp(landingY, dropStartY, currentY);
        float currentScale = Mathf.Lerp(fallingStartScale, landedScale, normalized);

        ApplyUniformScale(currentScale);
    }

    private void CacheParticleSystems()
    {
        if (impactEffectObject == null)
        {
            cachedParticleSystems = null;
            return;
        }

        cachedParticleSystems = impactEffectObject.GetComponentsInChildren<ParticleSystem>(true);
    }

    private void PrepareImpactEffectImmediate()
    {
        if (impactEffectObject == null)
            return;

        if (detachEffectOnPlay && impactEffectObject.transform.parent == null)
            impactEffectObject.transform.SetParent(transform, true);

        if (cachedParticleSystems != null)
        {
            for (int i = 0; i < cachedParticleSystems.Length; i++)
            {
                if (cachedParticleSystems[i] == null)
                    continue;

                cachedParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                cachedParticleSystems[i].Clear(true);
            }
        }

        impactEffectObject.SetActive(false);
        impactEffectObject.transform.localPosition = Vector3.zero;
        impactEffectObject.transform.localRotation = Quaternion.identity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isDropping || hasLanded)
            return;

        if (!IsLandingCollision(collision.gameObject.layer))
            return;

        hasLanded = true;
        isDropping = false;

        ApplyUniformScale(landedScale);

        if (rb != null && freezeAfterLanding)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        PlayImpactEffect();
    }

    private bool IsLandingCollision(int layer)
    {
        return (landingLayer.value & (1 << layer)) != 0;
    }

    private void ApplyUniformScale(float scaleValue)
    {
        float clamped = Mathf.Max(0.01f, scaleValue);
        transform.localScale = new Vector3(clamped, clamped, clamped);
    }

    private void PlayImpactEffect()
    {
        if (impactEffectObject == null)
            return;

        Vector3 worldPosition = impactEffectObject.transform.position;
        Quaternion worldRotation = impactEffectObject.transform.rotation;

        if (detachEffectOnPlay)
        {
            impactEffectObject.transform.SetParent(null, true);
            impactEffectObject.transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        impactEffectObject.SetActive(true);

        if (cachedParticleSystems == null || cachedParticleSystems.Length == 0)
            CacheParticleSystems();

        if (cachedParticleSystems != null)
        {
            for (int i = 0; i < cachedParticleSystems.Length; i++)
            {
                if (cachedParticleSystems[i] == null)
                    continue;

                cachedParticleSystems[i].Clear(true);
                cachedParticleSystems[i].Play(true);
            }
        }
    }
}