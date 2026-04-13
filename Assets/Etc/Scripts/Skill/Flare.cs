using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class Flare : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float burnDuration = 6f;
    [SerializeField] private float detectInterval = 0.05f;

    [Header("Launch Arc")]
    [SerializeField] private float launchSpeed = 8f;
    [SerializeField] private float minLaunchDuration = 1.2f;
    [SerializeField] private float maxLaunchDuration = 3.5f;
    [SerializeField] private float minArcHeight = 4f;
    [SerializeField] private float arcHeightPerDistance = 0.25f;
    [SerializeField] private float hoverHeight = 7f;

    [Header("Optional References")]
    [SerializeField] private Light flareLight;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private CapsuleCollider capsuleCollider;
    private Coroutine lifeRoutine;

    private Vector3 launchStartPosition;
    private Vector3 targetGroundPosition;
    private Quaternion fixedRotation = Quaternion.identity;

    private readonly HashSet<TargetController> revealedTargets = new HashSet<TargetController>();

    private void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (capsuleCollider != null)
            capsuleCollider.isTrigger = true;

        if (flareLight == null)
            flareLight = GetComponentInChildren<Light>(true);

        fixedRotation = transform.rotation;
        SetEmissionActive(false);
    }

    private void OnValidate()
    {
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null)
            col.isTrigger = true;
    }

    public void Launch(Vector3 startPosition, Vector3 destination)
    {
        launchStartPosition = startPosition;
        targetGroundPosition = destination;

        if (lifeRoutine != null)
            StopCoroutine(lifeRoutine);

        ClearAllReveals();

        transform.position = startPosition;
        transform.rotation = fixedRotation;

        lifeRoutine = StartCoroutine(LaunchAndBurnRoutine());
    }

    private IEnumerator LaunchAndBurnRoutine()
    {
        Vector3 start = launchStartPosition;
        Vector3 end = new Vector3(
            targetGroundPosition.x,
            targetGroundPosition.y + hoverHeight,
            targetGroundPosition.z
        );

        Vector3 startXZ = new Vector3(start.x, 0f, start.z);
        Vector3 endXZ = new Vector3(end.x, 0f, end.z);

        float horizontalDistance = Vector3.Distance(startXZ, endXZ);
        float launchDuration = Mathf.Clamp(
            horizontalDistance / Mathf.Max(0.01f, launchSpeed),
            minLaunchDuration,
            maxLaunchDuration
        );

        float arcHeight = Mathf.Max(
            minArcHeight,
            horizontalDistance * arcHeightPerDistance
        );

        float elapsed = 0f;
        float detectTimer = 0f;

        SetEmissionActive(true);

        if (debugLog)
        {
            Debug.Log(
                $"<color=yellow>[Scout Flare]</color> 발사 시작. " +
                $"start={start}, target={targetGroundPosition}, " +
                $"flightTime={launchDuration:0.00}, arcHeight={arcHeight:0.00}"
            );
        }

        while (elapsed < launchDuration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, launchDuration));

            Vector3 horizontalPosition = Vector3.Lerp(start, end, t);
            float baseY = Mathf.Lerp(start.y, end.y, t);
            float arcY = 4f * arcHeight * t * (1f - t);

            Vector3 nextPosition = new Vector3(
                horizontalPosition.x,
                baseY + arcY,
                horizontalPosition.z
            );

            transform.position = nextPosition;
            transform.rotation = fixedRotation;

            detectTimer -= Time.deltaTime;
            if (detectTimer <= 0f)
            {
                RefreshDetectedTargets();
                detectTimer = detectInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        transform.rotation = fixedRotation;

        if (debugLog)
        {
            Debug.Log(
                $"<color=yellow>[Scout Flare]</color> 도착. " +
                $"hoverPos={transform.position}"
            );
        }

        float burnTimer = 0f;

        while (burnTimer < burnDuration)
        {
            burnTimer += Time.deltaTime;
            detectTimer -= Time.deltaTime;

            transform.rotation = fixedRotation;

            if (detectTimer <= 0f)
            {
                RefreshDetectedTargets();
                detectTimer = detectInterval;
            }

            yield return null;
        }

        SetEmissionActive(false);
        ClearAllReveals();

        if (debugLog)
            Debug.Log("<color=yellow>[Scout Flare]</color> 점등 종료.");

        Destroy(gameObject);
    }

    private void RefreshDetectedTargets()
    {
        if (capsuleCollider == null)
            return;

        GetWorldCapsule(out Vector3 point0, out Vector3 point1, out float radius);

        Collider[] hits = Physics.OverlapCapsule(
            point0,
            point1,
            radius,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        HashSet<TargetController> currentTargets = new HashSet<TargetController>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            TargetController target = hit.GetComponentInParent<TargetController>();
            if (target == null)
                continue;

            currentTargets.Add(target);

            if (revealedTargets.Add(target))
            {
                target.AddReconReveal();

                if (debugLog)
                    Debug.Log($"[Scout Flare] Reveal 시작: {target.name}");
            }
        }

        List<TargetController> removeTargets = null;

        foreach (TargetController target in revealedTargets)
        {
            if (target == null)
                continue;

            if (currentTargets.Contains(target))
                continue;

            if (removeTargets == null)
                removeTargets = new List<TargetController>();

            removeTargets.Add(target);
        }

        if (removeTargets == null)
            return;

        for (int i = 0; i < removeTargets.Count; i++)
        {
            TargetController target = removeTargets[i];
            if (target == null)
                continue;

            revealedTargets.Remove(target);
            target.RemoveReconReveal();

            if (debugLog)
                Debug.Log($"[Scout Flare] Reveal 해제: {target.name}");
        }
    }

    private void GetWorldCapsule(out Vector3 point0, out Vector3 point1, out float radius)
    {
        Vector3 center = transform.TransformPoint(capsuleCollider.center);
        Vector3 lossyScale = transform.lossyScale;

        Vector3 localAxis;
        float heightScale;
        float radiusScale;

        switch (capsuleCollider.direction)
        {
            case 0:
                localAxis = Vector3.right;
                heightScale = Mathf.Abs(lossyScale.x);
                radiusScale = Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
                break;

            case 2:
                localAxis = Vector3.forward;
                heightScale = Mathf.Abs(lossyScale.z);
                radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
                break;

            default:
                localAxis = Vector3.up;
                heightScale = Mathf.Abs(lossyScale.y);
                radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
                break;
        }

        radius = capsuleCollider.radius * radiusScale;

        float cylinderHalfHeight =
            Mathf.Max(0f, (capsuleCollider.height * heightScale * 0.5f) - radius);

        Vector3 axis = transform.TransformDirection(localAxis).normalized;

        point0 = center + axis * cylinderHalfHeight;
        point1 = center - axis * cylinderHalfHeight;
    }

    private void SetEmissionActive(bool active)
    {
        if (flareLight != null)
            flareLight.enabled = active;

        if (capsuleCollider != null)
            capsuleCollider.enabled = active;
    }

    private void OnDisable()
    {
        ClearAllReveals();
    }

    private void OnDestroy()
    {
        ClearAllReveals();
    }

    private void ClearAllReveals()
    {
        if (revealedTargets.Count == 0)
            return;

        foreach (TargetController target in revealedTargets)
        {
            if (target != null)
                target.RemoveReconReveal();
        }

        revealedTargets.Clear();
    }
}