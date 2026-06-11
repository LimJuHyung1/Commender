using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class TargetManholeWarpController : MonoBehaviour
{
    [Header("Warp")]
    [SerializeField] private float navMeshSampleRadius = 2.5f;
    [SerializeField] private bool consumeEntranceAndDestination = true;

    [Header("Effect")]
    [SerializeField] private ParticleSystem warpOutEffect;
    [SerializeField] private ParticleSystem warpInEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warpSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    private TargetController targetController;
    private NavMeshAgent navAgent;
    private bool isWarping;

    private void Awake()
    {
        targetController = GetComponent<TargetController>();
        navAgent = GetComponent<NavMeshAgent>();
    }

    public bool TryWarpFrom(ManholeWarp entrancePoint)
    {
        if (isWarping)
            return false;

        if (entrancePoint == null)
            return false;

        if (!CanUseManhole())
        {
            if (showDebugLog)
            {
                Debug.Log($"[TargetManholeWarpController] 맨홀 사용 불가 상태입니다. Entrance: {entrancePoint.name}", this);
            }

            return false;
        }

        if (!ManholeWarpManager.TryGetRandomDestination(entrancePoint, out ManholeWarp destinationPoint))
        {
            if (showDebugLog)
            {
                Debug.Log($"[TargetManholeWarpController] 이동 가능한 목적지 맨홀이 없습니다. Entrance: {entrancePoint.name}", this);
            }

            return false;
        }

        if (destinationPoint == null)
            return false;

        if (!destinationPoint.TryGetExitPosition(out Vector3 rawExitPosition))
            return false;

        if (!TryGetValidNavMeshPosition(rawExitPosition, out Vector3 validExitPosition))
        {
            if (showDebugLog)
            {
                Debug.LogWarning($"[TargetManholeWarpController] 목적지 맨홀의 ExitPoint가 NavMesh 위에 없습니다. Destination: {destinationPoint.name}", destinationPoint);
            }

            return false;
        }

        return Warp(entrancePoint, destinationPoint, validExitPosition);
    }

    private bool CanUseManhole()
    {
        if (targetController == null)
            return false;

        if (navAgent == null)
            return false;

        if (!navAgent.enabled)
            return false;

        if (!navAgent.isOnNavMesh)
            return false;

        if (targetController.IsCaught)
            return false;

        if (targetController.IsExhausted)
            return false;

        if (targetController.IsRooted)
            return false;

        if (!targetController.HasActiveThreat)
            return false;

        if (GameManager.Instance != null && GameManager.Instance.IsStageFinished)
            return false;

        return true;
    }

    private bool Warp(ManholeWarp entrancePoint, ManholeWarp destinationPoint, Vector3 destinationPosition)
    {
        isWarping = true;

        PlayWarpOutEffect();
        PlayWarpSound();

        bool warped = navAgent.Warp(destinationPosition);

        if (!warped)
        {
            isWarping = false;

            if (showDebugLog)
            {
                Debug.LogWarning($"[TargetManholeWarpController] NavMeshAgent.Warp 실패. Destination: {destinationPoint.name}", destinationPoint);
            }

            return false;
        }

        navAgent.ResetPath();

        if (consumeEntranceAndDestination)
        {
            ManholeWarpManager.ConsumeWarpPair(entrancePoint, destinationPoint);
        }
        else
        {
            entrancePoint.MarkUsed();
        }

        PlayWarpInEffect();

        targetController.ForceRecalculateEscapeRoute();

        if (showDebugLog)
        {
            Debug.Log($"[TargetManholeWarpController] 맨홀 워프 성공: {entrancePoint.name} -> {destinationPoint.name}", this);
        }

        isWarping = false;
        return true;
    }

    private bool TryGetValidNavMeshPosition(Vector3 origin, out Vector3 validPosition)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            validPosition = hit.position;
            return true;
        }

        validPosition = origin;
        return false;
    }

    private void PlayWarpOutEffect()
    {
        if (warpOutEffect == null)
            return;

        warpOutEffect.transform.position = transform.position;
        warpOutEffect.Play();
    }

    private void PlayWarpInEffect()
    {
        if (warpInEffect == null)
            return;

        warpInEffect.transform.position = transform.position;
        warpInEffect.Play();
    }

    private void PlayWarpSound()
    {
        if (audioSource == null || warpSound == null)
            return;

        audioSource.PlayOneShot(warpSound);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (navMeshSampleRadius < 0.1f)
            navMeshSampleRadius = 0.1f;
    }
#endif
}