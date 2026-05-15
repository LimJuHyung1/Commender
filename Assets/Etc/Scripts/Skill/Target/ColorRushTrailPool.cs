using System.Collections.Generic;
using UnityEngine;

public class ColorRushTrailPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private Transform poolParent;
    [SerializeField] private int initialPoolSize = 40;
    [SerializeField] private bool allowExpand = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private readonly Queue<ColorRushTrail> pooledTrails = new Queue<ColorRushTrail>();
    private readonly HashSet<ColorRushTrail> activeTrails = new HashSet<ColorRushTrail>();

    private bool isInitialized;

    public int ActiveCount => activeTrails.Count;
    public int PooledCount => pooledTrails.Count;

    public void Initialize(
        GameObject trailPrefab,
        int initialPoolSize,
        Transform parent)
    {
        this.trailPrefab = trailPrefab;
        this.initialPoolSize = Mathf.Max(1, initialPoolSize);
        poolParent = parent;

        if (this.trailPrefab == null)
        {
            Debug.LogWarning("[ColorRushTrailPool] trailPrefab이 설정되지 않았습니다.");
            return;
        }

        if (isInitialized)
            return;

        PrewarmPool(this.initialPoolSize);
        isInitialized = true;

        if (enableDebugLog)
        {
            Debug.Log(
                $"[ColorRushTrailPool] Initialized. Pool Size: {pooledTrails.Count}"
            );
        }
    }

    public ColorRushTrail SpawnTrail(
        Vector3 position,
        Quaternion rotation,
        float lifetime)
    {
        if (trailPrefab == null)
        {
            Debug.LogWarning("[ColorRushTrailPool] trailPrefab이 설정되지 않아 페인트 자국을 생성할 수 없습니다.");
            return null;
        }

        if (!isInitialized)
        {
            PrewarmPool(initialPoolSize);
            isInitialized = true;
        }

        ColorRushTrail trail = GetTrailFromPool();

        if (trail == null)
            return null;

        Transform trailTransform = trail.transform;
        trailTransform.SetPositionAndRotation(position, rotation);

        trail.Initialize(this, lifetime);

        activeTrails.Add(trail);
        trail.gameObject.SetActive(true);

        return trail;
    }

    public void ReturnTrail(ColorRushTrail trail)
    {
        if (trail == null)
            return;

        if (!activeTrails.Contains(trail) && pooledTrails.Contains(trail))
            return;

        activeTrails.Remove(trail);

        GameObject trailObject = trail.gameObject;

        if (trailObject.activeSelf)
            trailObject.SetActive(false);

        Transform trailTransform = trail.transform;

        if (poolParent != null)
            trailTransform.SetParent(poolParent);

        pooledTrails.Enqueue(trail);
    }

    public void ClearPool(bool destroyObjects)
    {
        foreach (ColorRushTrail trail in activeTrails)
        {
            if (trail == null)
                continue;

            if (destroyObjects)
                Destroy(trail.gameObject);
            else
                trail.gameObject.SetActive(false);
        }

        activeTrails.Clear();

        while (pooledTrails.Count > 0)
        {
            ColorRushTrail trail = pooledTrails.Dequeue();

            if (trail == null)
                continue;

            if (destroyObjects)
                Destroy(trail.gameObject);
            else
                trail.gameObject.SetActive(false);
        }

        isInitialized = false;
    }

    private void PrewarmPool(int count)
    {
        if (trailPrefab == null)
            return;

        count = Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            ColorRushTrail trail = CreateTrailInstance();

            if (trail != null)
                pooledTrails.Enqueue(trail);
        }
    }

    private ColorRushTrail GetTrailFromPool()
    {
        while (pooledTrails.Count > 0)
        {
            ColorRushTrail trail = pooledTrails.Dequeue();

            if (trail != null)
                return trail;
        }

        if (!allowExpand)
        {
            if (enableDebugLog)
                Debug.LogWarning("[ColorRushTrailPool] 사용할 수 있는 페인트 자국 오브젝트가 없습니다.");

            return null;
        }

        return CreateTrailInstance();
    }

    private ColorRushTrail CreateTrailInstance()
    {
        if (trailPrefab == null)
            return null;

        GameObject trailObject = Instantiate(
            trailPrefab,
            poolParent != null ? poolParent : transform
        );

        trailObject.name = $"{trailPrefab.name}_PooledTrail";
        trailObject.SetActive(false);

        ColorRushTrail trail = trailObject.GetComponent<ColorRushTrail>();

        if (trail == null)
            trail = trailObject.AddComponent<ColorRushTrail>();

        return trail;
    }

    private void OnDestroy()
    {
        activeTrails.Clear();
        pooledTrails.Clear();
    }
}