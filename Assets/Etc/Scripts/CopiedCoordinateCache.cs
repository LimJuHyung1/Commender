using UnityEngine;

public static class CopiedCoordinateCache
{
    private static bool hasData;
    private static float copiedX;
    private static float copiedZ;
    private static Vector3 copiedWorldPoint;

    public static void Save(float x, float z, Vector3 worldPoint)
    {
        copiedX = x;
        copiedZ = z;
        copiedWorldPoint = worldPoint;
        hasData = true;
    }

    public static bool TryGet(float x, float z, float tolerance, out Vector3 worldPoint)
    {
        worldPoint = default;

        if (!hasData)
            return false;

        if (Mathf.Abs(copiedX - x) > tolerance)
            return false;

        if (Mathf.Abs(copiedZ - z) > tolerance)
            return false;

        worldPoint = copiedWorldPoint;
        return true;
    }

    public static void Clear()
    {
        hasData = false;
    }
}