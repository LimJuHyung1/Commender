using UnityEngine;

public class DogFoundTargetMarker : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float defaultLifetime = 6f;
    [SerializeField] private bool destroyOnLifetimeEnd = true;
    [SerializeField] private bool useUnscaledTime = false;

    private float despawnTime;
    private bool initialized;

    private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;

    private void OnEnable()
    {
        if (initialized)
            return;

        Initialize(defaultLifetime);
    }

    private void Update()
    {
        if (!destroyOnLifetimeEnd)
            return;

        if (CurrentTime < despawnTime)
            return;

        Destroy(gameObject);
    }

    public void Initialize(float lifetime)
    {
        initialized = true;

        float safeLifetime = Mathf.Max(0.01f, lifetime);
        despawnTime = CurrentTime + safeLifetime;
    }
}