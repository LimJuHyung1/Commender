using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class VisionConeVisualizer : MonoBehaviour
{
    public VisionSensor visionSensor;
    public Transform visionOrigin;

    public int rayCount = 40;
    public float meshHeightOffset = 0.03f;
    public float updateInterval = 0.03f;

    private Mesh mesh;
    private float updateTimer;

    private void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Vision Cone Mesh";

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        if (visionSensor == null)
            visionSensor = GetComponentInParent<VisionSensor>();

        if (visionOrigin == null && visionSensor != null)
            visionOrigin = visionSensor.transform;
    }

    private void Update()
    {
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
}