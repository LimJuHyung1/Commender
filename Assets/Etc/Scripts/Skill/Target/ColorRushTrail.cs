using UnityEngine;

public class ColorRushTrail : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float revealCheckInterval = 0.15f;
    [SerializeField] private bool stayVisibleAfterReveal = true;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool autoFindVisualRoot = true;
    [SerializeField] private string visualRootName = "Quad";

    [Header("Random Paint Colors")]
    [SerializeField] private Color hotPinkColor = new Color(1f, 0.05f, 0.42f, 1f);
    [SerializeField] private Color mintColor = new Color(0f, 0.85f, 0.72f, 1f);
    [SerializeField] private Color limeColor = new Color(0.65f, 0.95f, 0.12f, 1f);
    [SerializeField] private Color edgeColor = new Color(1f, 0.9f, 0.96f, 1f);

    [Header("Shader Property Names")]
    [SerializeField] private string paintColorPropertyName = "_PaintColor";
    [SerializeField] private string edgeColorPropertyName = "_EdgeColor";
    [SerializeField] private string revealTimePropertyName = "_RevealTime";
    [SerializeField] private string seedPropertyName = "_Seed";
    [SerializeField] private string fadePropertyName = "_Fade";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;

    private MaterialPropertyBlock propertyBlock;
    private ColorRushTrailPool ownerPool;

    private Color selectedPaintColor;
    private float selectedSeed;
    private float lifetime = 10f;
    private float spawnTime;
    private float nextRevealCheckTime;

    private int paintColorPropertyId;
    private int edgeColorPropertyId;
    private int revealTimePropertyId;
    private int seedPropertyId;
    private int fadePropertyId;

    private bool isInitialized;
    private bool isRevealed;

    public bool IsRevealed => isRevealed;

    private void Awake()
    {
        CacheShaderPropertyIds();
        ResolveVisualRoot();
        CacheComponents();
        EnsurePropertyBlock();

        HideVisuals();
        DisableColliders();
    }

    private void OnEnable()
    {
        if (!isInitialized)
            return;

        ResolveVisualRoot();
        CacheComponents();
        EnsurePropertyBlock();

        HideVisuals();
        DisableColliders();

        spawnTime = Time.time;
        nextRevealCheckTime = Time.time;
        isRevealed = false;

        ApplyMaterialProperties(false);
    }

    private void OnValidate()
    {
        revealCheckInterval = Mathf.Max(0.01f, revealCheckInterval);

        CacheShaderPropertyIds();
        ResolveVisualRoot();
        CacheComponents();
    }

    public void Initialize(
        ColorRushTrailPool ownerPool,
        float lifetime)
    {
        this.ownerPool = ownerPool;
        this.lifetime = Mathf.Max(0.1f, lifetime);

        isInitialized = true;
        spawnTime = Time.time;
        nextRevealCheckTime = Time.time;
        isRevealed = false;

        CacheShaderPropertyIds();
        ResolveVisualRoot();
        CacheComponents();
        EnsurePropertyBlock();

        SelectRandomPaintColor();
        ApplyMaterialProperties(false);

        HideVisuals();
        DisableColliders();
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        if (Time.time - spawnTime >= lifetime)
        {
            ReturnToPool();
            return;
        }

        if (isRevealed && stayVisibleAfterReveal)
            return;

        if (Time.time < nextRevealCheckTime)
            return;

        nextRevealCheckTime = Time.time + revealCheckInterval;
        TryRevealByAgentVision();
    }

    private void TryRevealByAgentVision()
    {
        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            VisionSensor visionSensor = agent.VisionSensor;

            if (visionSensor == null)
                continue;

            if (!visionSensor.CanDirectlySeeTransform(transform))
                continue;

            Reveal();

            if (enableDebugLog)
            {
                Debug.Log(
                    $"[ColorRushTrail] Revealed by AgentID: {agent.AgentID}"
                );
            }

            return;
        }
    }

    public void Reveal()
    {
        if (isRevealed)
            return;

        isRevealed = true;

        ApplyMaterialProperties(true);
        ShowVisuals();
        EnableColliders();
    }

    public void Hide()
    {
        isRevealed = false;

        ApplyMaterialProperties(false);
        HideVisuals();
        DisableColliders();
    }

    public void ReturnToPool()
    {
        Hide();

        if (ownerPool != null)
        {
            ownerPool.ReturnTrail(this);
            return;
        }

        gameObject.SetActive(false);
    }

    private void SelectRandomPaintColor()
    {
        int colorIndex = Random.Range(0, 3);

        if (colorIndex == 0)
        {
            selectedPaintColor = hotPinkColor;
        }
        else if (colorIndex == 1)
        {
            selectedPaintColor = mintColor;
        }
        else
        {
            selectedPaintColor = limeColor;
        }

        selectedSeed = Random.Range(0.0f, 999.0f);
    }

    private void ApplyMaterialProperties(bool startSpreadAnimation)
    {
        if (cachedRenderers == null)
            return;

        EnsurePropertyBlock();

        float revealTime = startSpreadAnimation ? Time.time : -999f;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer currentRenderer = cachedRenderers[i];

            if (currentRenderer == null)
                continue;

            currentRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(paintColorPropertyId, selectedPaintColor);
            propertyBlock.SetColor(edgeColorPropertyId, edgeColor);
            propertyBlock.SetFloat(revealTimePropertyId, revealTime);
            propertyBlock.SetFloat(seedPropertyId, selectedSeed);
            propertyBlock.SetFloat(fadePropertyId, 1f);

            currentRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
            return;

        if (!autoFindVisualRoot)
            return;

        Transform found = transform.Find(visualRootName);

        if (found == null)
            found = transform.Find("Quad");

        if (found == null && transform.childCount > 0)
            found = transform.GetChild(0);

        if (found != null)
            visualRoot = found;
    }

    private void CacheComponents()
    {
        if (visualRoot != null)
        {
            cachedRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            cachedColliders = visualRoot.GetComponentsInChildren<Collider>(true);
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void CacheShaderPropertyIds()
    {
        paintColorPropertyId = Shader.PropertyToID(paintColorPropertyName);
        edgeColorPropertyId = Shader.PropertyToID(edgeColorPropertyName);
        revealTimePropertyId = Shader.PropertyToID(revealTimePropertyName);
        seedPropertyId = Shader.PropertyToID(seedPropertyName);
        fadePropertyId = Shader.PropertyToID(fadePropertyName);
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    private void ShowVisuals()
    {
        if (cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = true;
        }
    }

    private void HideVisuals()
    {
        if (cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = false;
        }
    }

    private void EnableColliders()
    {
        if (cachedColliders == null)
            return;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = true;
        }
    }

    private void DisableColliders()
    {
        if (cachedColliders == null)
            return;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}