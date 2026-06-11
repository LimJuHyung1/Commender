using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ManholeWarpManager : MonoBehaviour
{
    private static ManholeWarpManager instance;
    private static readonly List<ManholeWarp> warpPoints = new();
    private static readonly List<ManholeWarp> candidatePoints = new();

    [Header("Warp Rule")]
    [SerializeField] private bool requireDifferentPoint = true;
    [SerializeField] private bool consumeEntrancePoint = true;
    [SerializeField] private bool consumeDestinationPoint = true;
    [SerializeField] private bool ignoreInactivePoints = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    public static int RegisteredCount => warpPoints.Count;

    private static bool RequireDifferentPoint => instance == null || instance.requireDifferentPoint;
    private static bool ConsumeEntrancePoint => instance == null || instance.consumeEntrancePoint;
    private static bool ConsumeDestinationPoint => instance == null || instance.consumeDestinationPoint;
    private static bool IgnoreInactivePoints => instance != null && instance.ignoreInactivePoints;
    private static bool ShowDebugLog => instance != null && instance.showDebugLog;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[ManholeWarpManager] 씬에 ManholeWarpManager가 여러 개 있습니다. 중복 오브젝트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void Register(ManholeWarp point)
    {
        if (point == null)
            return;

        if (warpPoints.Contains(point))
            return;

        warpPoints.Add(point);

        if (ShowDebugLog)
        {
            Debug.Log($"[ManholeWarpManager] Registered: {point.name} / Count: {warpPoints.Count}", point);
        }
    }

    public static void Unregister(ManholeWarp point)
    {
        if (point == null)
            return;

        warpPoints.Remove(point);

        if (ShowDebugLog)
        {
            Debug.Log($"[ManholeWarpManager] Unregistered: {point.name} / Count: {warpPoints.Count}", point);
        }
    }

    public static bool TryGetRandomDestination(ManholeWarp entrancePoint, out ManholeWarp destinationPoint)
    {
        destinationPoint = null;

        CleanupNullPoints();

        if (entrancePoint == null)
        {
            if (ShowDebugLog)
                Debug.LogWarning("[ManholeWarpManager] 입구 맨홀이 null입니다.");

            return false;
        }

        if (!entrancePoint.IsAvailable)
        {
            if (ShowDebugLog)
                Debug.Log($"[ManholeWarpManager] 입구 맨홀이 이미 사용되었거나 비활성 상태입니다: {entrancePoint.name}", entrancePoint);

            return false;
        }

        candidatePoints.Clear();

        for (int i = 0; i < warpPoints.Count; i++)
        {
            ManholeWarp point = warpPoints[i];

            if (point == null)
                continue;

            if (!point.IsAvailable)
                continue;

            if (IgnoreInactivePoints && !point.gameObject.activeInHierarchy)
                continue;

            if (RequireDifferentPoint && point == entrancePoint)
                continue;

            candidatePoints.Add(point);
        }

        if (candidatePoints.Count <= 0)
        {
            if (ShowDebugLog)
            {
                Debug.Log($"[ManholeWarpManager] {entrancePoint.name}에서 이동할 수 있는 남은 맨홀이 없습니다.", entrancePoint);
            }

            return false;
        }

        int randomIndex = Random.Range(0, candidatePoints.Count);
        destinationPoint = candidatePoints[randomIndex];

        if (ShowDebugLog)
        {
            Debug.Log($"[ManholeWarpManager] Random Destination: {entrancePoint.name} -> {destinationPoint.name}", destinationPoint);
        }

        return true;
    }

    public static void ConsumeWarpPair(ManholeWarp entrancePoint, ManholeWarp destinationPoint)
    {
        if (entrancePoint == null || destinationPoint == null)
            return;

        if (ConsumeEntrancePoint)
            entrancePoint.MarkUsed();

        if (ConsumeDestinationPoint)
            destinationPoint.MarkUsed();

        if (ShowDebugLog)
        {
            Debug.Log($"[ManholeWarpManager] Consumed Warp Pair: {entrancePoint.name}, {destinationPoint.name}");
        }
    }

    public static int GetAvailablePointCount(ManholeWarp exceptPoint = null)
    {
        CleanupNullPoints();

        int count = 0;

        for (int i = 0; i < warpPoints.Count; i++)
        {
            ManholeWarp point = warpPoints[i];

            if (point == null)
                continue;

            if (!point.IsAvailable)
                continue;

            if (IgnoreInactivePoints && !point.gameObject.activeInHierarchy)
                continue;

            if (RequireDifferentPoint && exceptPoint != null && point == exceptPoint)
                continue;

            count++;
        }

        return count;
    }

    public static void ResetAllManholes()
    {
        CleanupNullPoints();

        for (int i = 0; i < warpPoints.Count; i++)
        {
            ManholeWarp point = warpPoints[i];

            if (point == null)
                continue;

            point.ResetUsage();
        }

        if (ShowDebugLog)
        {
            Debug.Log("[ManholeWarpManager] 모든 맨홀 사용 상태를 초기화했습니다.");
        }
    }

    private static void CleanupNullPoints()
    {
        for (int i = warpPoints.Count - 1; i >= 0; i--)
        {
            if (warpPoints[i] == null)
                warpPoints.RemoveAt(i);
        }
    }
}