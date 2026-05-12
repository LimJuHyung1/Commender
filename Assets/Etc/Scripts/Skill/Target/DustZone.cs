using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DustZone : MonoBehaviour
{
    private enum LifeState
    {
        Appearing,
        Active,
        Disappearing
    }

    [Header("Dust Zone")]
    [SerializeField] private float radius = 4f;
    [SerializeField] private float duration = 10f;
    [SerializeField][Range(0.1f, 1f)] private float speedMultiplier = 0.5f;
    [SerializeField] private float checkInterval = 0.1f;

    [Header("Vertical Animation")]
    [SerializeField] private bool useVerticalAnimation = true;
    [SerializeField] private float hiddenY = -1f;
    [SerializeField] private float visibleY = 0f;
    [SerializeField] private float appearDuration = 3f;
    [SerializeField] private float disappearDuration = 3f;
    [SerializeField] private bool applySlowDuringAppear = false;
    [SerializeField] private bool applySlowDuringDisappear = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private readonly Dictionary<NavMeshAgent, float> originalSpeedMap = new Dictionary<NavMeshAgent, float>();
    private readonly HashSet<NavMeshAgent> agentsInsideThisFrame = new HashSet<NavMeshAgent>();

    private LifeState lifeState;

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;

    private float stateElapsedTime;
    private float activeElapsedTime;
    private float nextCheckTime;

    public void Initialize(
        float newRadius,
        float newDuration,
        float newSpeedMultiplier,
        bool newEnableDebugLog)
    {
        Initialize(
            newRadius,
            newDuration,
            newSpeedMultiplier,
            newEnableDebugLog,
            useVerticalAnimation,
            hiddenY,
            visibleY,
            appearDuration,
            disappearDuration
        );
    }

    public void Initialize(
        float newRadius,
        float newDuration,
        float newSpeedMultiplier,
        bool newEnableDebugLog,
        bool newUseVerticalAnimation,
        float newHiddenY,
        float newVisibleY,
        float newAppearDuration,
        float newDisappearDuration)
    {
        radius = Mathf.Max(0.1f, newRadius);
        duration = Mathf.Max(0.1f, newDuration);
        speedMultiplier = Mathf.Clamp(newSpeedMultiplier, 0.1f, 1f);
        enableDebugLog = newEnableDebugLog;

        useVerticalAnimation = newUseVerticalAnimation;
        hiddenY = newHiddenY;
        visibleY = newVisibleY;
        appearDuration = Mathf.Max(0f, newAppearDuration);
        disappearDuration = Mathf.Max(0f, newDisappearDuration);

        activeElapsedTime = 0f;
        stateElapsedTime = 0f;
        nextCheckTime = 0f;

        SetupVerticalPositions();

        if (useVerticalAnimation)
        {
            transform.position = hiddenPosition;
            lifeState = LifeState.Appearing;
        }
        else
        {
            transform.position = visiblePosition;
            lifeState = LifeState.Active;
        }
    }

    private void OnDisable()
    {
        RestoreAllAgents();
    }

    private void OnDestroy()
    {
        RestoreAllAgents();
    }

    private void Update()
    {
        UpdateLifeState();

        if (!ShouldApplySlowNow())
        {
            if (originalSpeedMap.Count > 0)
                RestoreAllAgents();

            return;
        }

        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        UpdateAgentsInZone();
    }

    private void UpdateLifeState()
    {
        switch (lifeState)
        {
            case LifeState.Appearing:
                UpdateAppearingState();
                break;

            case LifeState.Active:
                UpdateActiveState();
                break;

            case LifeState.Disappearing:
                UpdateDisappearingState();
                break;
        }
    }

    private void UpdateAppearingState()
    {
        bool finished = MoveVerticalPosition(
            hiddenPosition,
            visiblePosition,
            appearDuration
        );

        if (!finished)
            return;

        transform.position = visiblePosition;
        ChangeState(LifeState.Active);
    }

    private void UpdateActiveState()
    {
        activeElapsedTime += Time.deltaTime;

        if (activeElapsedTime < duration)
            return;

        StartDisappear();
    }

    private void UpdateDisappearingState()
    {
        bool finished = MoveVerticalPosition(
            visiblePosition,
            hiddenPosition,
            disappearDuration
        );

        if (!finished)
            return;

        transform.position = hiddenPosition;
        Destroy(gameObject);
    }

    private bool MoveVerticalPosition(Vector3 from, Vector3 to, float moveDuration)
    {
        if (moveDuration <= 0f)
        {
            transform.position = to;
            return true;
        }

        stateElapsedTime += Time.deltaTime;

        float t = Mathf.Clamp01(stateElapsedTime / moveDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        transform.position = Vector3.Lerp(from, to, smoothT);

        return t >= 1f;
    }

    private void StartDisappear()
    {
        transform.position = visiblePosition;

        if (!applySlowDuringDisappear)
            RestoreAllAgents();

        ChangeState(LifeState.Disappearing);
    }

    private void ChangeState(LifeState nextState)
    {
        lifeState = nextState;
        stateElapsedTime = 0f;
        nextCheckTime = 0f;
    }

    private bool ShouldApplySlowNow()
    {
        if (lifeState == LifeState.Appearing)
            return applySlowDuringAppear;

        if (lifeState == LifeState.Active)
            return true;

        if (lifeState == LifeState.Disappearing)
            return applySlowDuringDisappear;

        return false;
    }

    private void SetupVerticalPositions()
    {
        visiblePosition = transform.position;
        visiblePosition.y = visibleY;

        hiddenPosition = visiblePosition;
        hiddenPosition.y = hiddenY;
    }

    private void UpdateAgentsInZone()
    {
        agentsInsideThisFrame.Clear();

        AgentController[] agents = FindObjectsByType<AgentController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float sqrRadius = radius * radius;

        for (int i = 0; i < agents.Length; i++)
        {
            AgentController agent = agents[i];

            if (agent == null)
                continue;

            NavMeshAgent navAgent = agent.GetComponent<NavMeshAgent>();

            if (navAgent == null)
                continue;

            if (!navAgent.enabled)
                continue;

            if (!navAgent.isActiveAndEnabled)
                continue;

            float sqrDistance = GetHorizontalSqrDistance(
                visiblePosition,
                agent.transform.position
            );

            if (sqrDistance > sqrRadius)
                continue;

            agentsInsideThisFrame.Add(navAgent);
            ApplySlow(navAgent, agent.name);
        }

        RestoreAgentsOutsideZone();
    }

    private void ApplySlow(NavMeshAgent navAgent, string agentName)
    {
        if (navAgent == null)
            return;

        if (originalSpeedMap.ContainsKey(navAgent))
            return;

        originalSpeedMap.Add(navAgent, navAgent.speed);
        navAgent.speed *= speedMultiplier;

        if (enableDebugLog)
            Debug.Log($"[DustZone] Slow applied - Agent: {agentName}");
    }

    private void RestoreAgentsOutsideZone()
    {
        List<NavMeshAgent> agentsToRestore = new List<NavMeshAgent>();

        foreach (KeyValuePair<NavMeshAgent, float> pair in originalSpeedMap)
        {
            NavMeshAgent navAgent = pair.Key;

            if (navAgent == null)
            {
                agentsToRestore.Add(navAgent);
                continue;
            }

            if (!agentsInsideThisFrame.Contains(navAgent))
                agentsToRestore.Add(navAgent);
        }

        for (int i = 0; i < agentsToRestore.Count; i++)
        {
            RestoreAgent(agentsToRestore[i]);
        }
    }

    private void RestoreAgent(NavMeshAgent navAgent)
    {
        if (navAgent == null)
        {
            originalSpeedMap.Remove(navAgent);
            return;
        }

        if (!originalSpeedMap.TryGetValue(navAgent, out float originalSpeed))
            return;

        if (navAgent.enabled)
            navAgent.speed = originalSpeed;

        originalSpeedMap.Remove(navAgent);

        if (enableDebugLog)
            Debug.Log($"[DustZone] Slow removed - Agent: {navAgent.name}");
    }

    private void RestoreAllAgents()
    {
        foreach (KeyValuePair<NavMeshAgent, float> pair in originalSpeedMap)
        {
            NavMeshAgent navAgent = pair.Key;

            if (navAgent == null)
                continue;

            if (!navAgent.enabled)
                continue;

            navAgent.speed = pair.Value;
        }

        originalSpeedMap.Clear();
        agentsInsideThisFrame.Clear();
    }

    private float GetHorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return (a - b).sqrMagnitude;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 gizmoCenter = transform.position;
        gizmoCenter.y = visibleY;

        Gizmos.DrawWireSphere(gizmoCenter, radius);
    }
}