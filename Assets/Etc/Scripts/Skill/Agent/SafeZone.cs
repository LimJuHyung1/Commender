using UnityEngine;

public class EngineerSafeZone : MonoBehaviour
{
    [Header("Safe Zone")]
    [SerializeField] private Vector2 areaSize = new Vector2(6f, 6f);
    [SerializeField] private float lifeTime = 15f;
    [SerializeField] private float gaugeRecoveryPerSecond = 4f;
    [SerializeField] private float agentSearchInterval = 0.1f;

    private float elapsedTime;
    private float searchTimer;

    public void Configure(Vector2 newAreaSize, float newLifeTime, float newGaugeRecoveryPerSecond)
    {
        areaSize.x = Mathf.Max(0.1f, newAreaSize.x);
        areaSize.y = Mathf.Max(0.1f, newAreaSize.y);
        lifeTime = Mathf.Max(0.1f, newLifeTime);
        gaugeRecoveryPerSecond = Mathf.Max(0f, newGaugeRecoveryPerSecond);

        elapsedTime = 0f;
        searchTimer = 0f;

        UpdateVisualScale();
    }

    private void OnValidate()
    {
        areaSize.x = Mathf.Max(0.1f, areaSize.x);
        areaSize.y = Mathf.Max(0.1f, areaSize.y);
        lifeTime = Mathf.Max(0.1f, lifeTime);
        gaugeRecoveryPerSecond = Mathf.Max(0f, gaugeRecoveryPerSecond);
        agentSearchInterval = Mathf.Max(0.02f, agentSearchInterval);

        UpdateVisualScale();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        elapsedTime += deltaTime;

        if (elapsedTime >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        searchTimer -= deltaTime;

        if (searchTimer > 0f)
            return;

        searchTimer = agentSearchInterval;

        RecoverAgentGauges(agentSearchInterval);
    }

    private void RecoverAgentGauges(float deltaTime)
    {
        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float recoveryAmount = gaugeRecoveryPerSecond * deltaTime;

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            if (!IsInsideArea(agent.transform.position))
                continue;

            agent.AddSkillGauge(recoveryAmount);
        }
    }

    private bool IsInsideArea(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);

        float halfX = areaSize.x * 0.5f;
        float halfZ = areaSize.y * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfX &&
               Mathf.Abs(localPosition.z) <= halfZ;
    }

    private void UpdateVisualScale()
    {
        transform.localScale = new Vector3(areaSize.x, 1f, areaSize.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, 0.05f, areaSize.y));
        Gizmos.matrix = Matrix4x4.identity;
    }
}