using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class VisionConeVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VisionSensor visionSensor;
    [SerializeField] private Transform visionOrigin;

    [Header("Mesh")]
    [SerializeField] private int rayCount = 40;
    [SerializeField] private float meshHeightOffset = 0.03f;
    [SerializeField] private float updateInterval = 0.03f;

    [Header("»ö»ó")]
    [SerializeField] private Color normalColor = DefaultNormalColor;
    [SerializeField] private Color positionShareEnabledColor = DefaultPositionShareEnabledColor;
    [SerializeField] private Color positionSharingColor = DefaultPositionSharingColor;
    [SerializeField] private bool useSharingColorWhileTargetVisible = true;

    private static readonly Color DefaultNormalColor = new Color(1f, 1f, 1f, 0.25f);
    private static readonly Color DefaultPositionShareEnabledColor = new Color(0.1f, 0.65f, 1f, 0.35f);
    private static readonly Color DefaultPositionSharingColor = new Color(0.1f, 1f, 0.45f, 0.45f);

    private static readonly Color OldYellowNormalColor = new Color(1f, 0.9f, 0.1f, 0.25f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Observer observer;

    private float updateTimer;
    private Color lastAppliedColor;
    private bool hasAppliedColor;

    private void Reset()
    {
        normalColor = DefaultNormalColor;
        positionShareEnabledColor = DefaultPositionShareEnabledColor;
        positionSharingColor = DefaultPositionSharingColor;
        useSharingColorWhileTargetVisible = true;
    }

    private void OnValidate()
    {
        rayCount = Mathf.Max(1, rayCount);
        meshHeightOffset = Mathf.Max(0f, meshHeightOffset);
        updateInterval = Mathf.Max(0.01f, updateInterval);

        if (IsSameColor(normalColor, OldYellowNormalColor))
            normalColor = DefaultNormalColor;
    }

    private void Awake()
    {
        if (IsSameColor(normalColor, OldYellowNormalColor))
            normalColor = DefaultNormalColor;

        mesh = new Mesh();
        mesh.name = "Vision Cone Mesh";

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (visionSensor == null)
            visionSensor = GetComponentInParent<VisionSensor>();

        if (visionOrigin == null && visionSensor != null)
            visionOrigin = visionSensor.transform;

        observer = GetComponentInParent<Observer>();

        ApplyVisionColor(true);
    }

    private void Update()
    {
        ApplyVisionColor(false);

        if (visionSensor == null || visionOrigin == null)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;
        UpdateVisionMesh();
    }

    private void UpdateVisionMesh()
    {
        float viewRadius = visionSensor.CurrentViewRadius;
        float viewAngle = visionSensor.CurrentViewAngle;

        Vector3 origin = visionOrigin.position;
        origin.y += meshHeightOffset;

        int vertexCount = rayCount + 2;

        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[rayCount * 3];

        vertices[0] = transform.InverseTransformPoint(origin);

        float startAngle = -viewAngle * 0.5f;
        float angleStep = viewAngle / rayCount;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * visionOrigin.forward;

            Vector3 endPoint = origin + direction * viewRadius;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, viewRadius, visionSensor.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                endPoint.y += meshHeightOffset;
            }

            vertices[i + 1] = transform.InverseTransformPoint(endPoint);
        }

        for (int i = 0; i < rayCount; i++)
        {
            int triangleIndex = i * 3;

            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    private void ApplyVisionColor(bool force)
    {
        if (meshRenderer == null)
            return;

        Color targetColor = ResolveVisionColor();

        if (!force && hasAppliedColor && targetColor == lastAppliedColor)
            return;

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, targetColor);
        propertyBlock.SetColor(ColorId, targetColor);
        meshRenderer.SetPropertyBlock(propertyBlock);

        lastAppliedColor = targetColor;
        hasAppliedColor = true;
    }

    private Color ResolveVisionColor()
    {
        if (observer == null)
            return normalColor;

        if (!observer.IsTargetPositionShareEnabled)
            return normalColor;

        if (useSharingColorWhileTargetVisible && observer.IsTargetPositionSharing)
            return positionSharingColor;

        return positionShareEnabledColor;
    }

    private bool IsSameColor(Color a, Color b)
    {
        const float tolerance = 0.01f;

        return Mathf.Abs(a.r - b.r) <= tolerance &&
               Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance &&
               Mathf.Abs(a.a - b.a) <= tolerance;
    }
}