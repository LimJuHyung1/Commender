using UnityEngine;

public class SafeZone : MonoBehaviour
{
    [Header("Safe Zone")]
    [SerializeField] private Vector2 areaSize = new Vector2(4f, 4f);
    [SerializeField] private float lifeTime = 15f;
    [SerializeField] private float gaugeRecoveryPerSecond = 4f;
    [SerializeField] private float agentSearchInterval = 0.1f;

    [Header("Follow Upgrade")]
    [SerializeField] private bool followFirstEnteredAgent;
    [SerializeField] private bool keepOriginalY = true;
    [SerializeField] private float followMoveSpeed = 20f;

    private float elapsedTime;
    private float searchTimer;
    private float originalY;
    private Transform followTarget;

    public void Configure(Vector2 newAreaSize, float newLifeTime, float newGaugeRecoveryPerSecond)
    {
        areaSize.x = Mathf.Max(0.1f, newAreaSize.x);
        areaSize.y = Mathf.Max(0.1f, newAreaSize.y);
        lifeTime = Mathf.Max(0.1f, newLifeTime);
        gaugeRecoveryPerSecond = Mathf.Max(0f, newGaugeRecoveryPerSecond);

        elapsedTime = 0f;
        searchTimer = 0f;
        followTarget = null;
        originalY = transform.position.y;

        UpdateVisualScale();
    }

    public void SetFollowFirstEnteredAgent(bool enabled)
    {
        followFirstEnteredAgent = enabled;

        if (!followFirstEnteredAgent)
            followTarget = null;
    }

    private void Awake()
    {
        originalY = transform.position.y;
    }

    private void OnValidate()
    {
        areaSize.x = Mathf.Max(0.1f, areaSize.x);
        areaSize.y = Mathf.Max(0.1f, areaSize.y);
        lifeTime = Mathf.Max(0.1f, lifeTime);
        gaugeRecoveryPerSecond = Mathf.Max(0f, gaugeRecoveryPerSecond);
        agentSearchInterval = Mathf.Max(0.02f, agentSearchInterval);
        followMoveSpeed = Mathf.Max(0f, followMoveSpeed);

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

        UpdateFollowTargetPosition(deltaTime);

        searchTimer -= deltaTime;

        if (searchTimer > 0f)
            return;

        searchTimer = agentSearchInterval;

        RecoverAgentGauges(agentSearchInterval);
    }

    private void UpdateFollowTargetPosition(float deltaTime)
    {
        if (!followFirstEnteredAgent)
            return;

        if (followTarget == null)
            return;

        Vector3 targetPosition = followTarget.position;

        if (keepOriginalY)
            targetPosition.y = originalY;

        if (followMoveSpeed <= 0f)
        {
            transform.position = targetPosition;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            followMoveSpeed * deltaTime
        );
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

            TrySetFollowTarget(agent);
            agent.AddSkillGauge(recoveryAmount);
        }
    }

    private void TrySetFollowTarget(AgentController agent)
    {
        if (!followFirstEnteredAgent)
            return;

        if (followTarget != null)
            return;

        if (agent == null)
            return;

        followTarget = agent.transform;
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