using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class StageMapManager : MonoBehaviour
{
    [System.Serializable]
    public class StageEntry
    {
        public string stageName;
        public GameObject mapPrefab;
    }

    [Header("Stages")]
    [SerializeField] private StageEntry[] stages;

    [Header("Unit Prefabs")]
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private GameObject[] agentPrefabs;

    [Header("Options")]
    [SerializeField] private bool buildNavMeshOnSpawn = false;

    private GameObject currentMap;
    private GameObject currentTarget;
    private readonly List<GameObject> currentAgents = new List<GameObject>();

    private Transform groundRoot;
    private Transform agentSpawnPointsRoot;
    private Transform targetSpawnPointsRoot;
    private Transform fallbackTargetSpawnPoint;

    private int currentStageIndex = 0;

    public int CurrentStageIndex => currentStageIndex;
    public string CurrentStageDisplayName => GetStageDisplayName(currentStageIndex);

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private void Start()
    {
        GenerateStageFromSelection();
    }

    public void GenerateStageFromSelection()
    {
        ClearStage();

        currentStageIndex = PlayerPrefs.GetInt(SelectedStageKey, 0);

        if (stages == null || stages.Length == 0)
        {
            Debug.LogError("[StageMapManager] stages가 비어 있습니다.");
            return;
        }

        currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stages.Length - 1);

        GameObject selectedMapPrefab = GetSelectedMapPrefab(currentStageIndex);
        if (selectedMapPrefab == null)
        {
            Debug.LogError("[StageMapManager] 선택된 스테이지의 맵 프리팹이 없습니다.");
            return;
        }

        currentMap = Instantiate(selectedMapPrefab, Vector3.zero, Quaternion.identity);

        if (!CacheMapPoints())
        {
            Debug.LogError("[StageMapManager] 맵 내부 구조를 찾지 못했습니다.");
            return;
        }

        RegisterGroundRootToCamera();

        if (buildNavMeshOnSpawn)
            BuildNavMesh();

        SpawnTarget();
        SpawnAgents();
        RegisterAgentsToTarget();

        RefreshCommenderAgents();
    }

    private GameObject GetSelectedMapPrefab(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stages.Length)
            return null;

        return stages[stageIndex].mapPrefab;
    }

    private bool CacheMapPoints()
    {
        if (currentMap == null)
            return false;

        groundRoot = currentMap.transform.Find("GroundRoot");
        agentSpawnPointsRoot = currentMap.transform.Find("AgentSpawnPoints");

        targetSpawnPointsRoot = currentMap.transform.Find("TargetSpawnPoints");
        fallbackTargetSpawnPoint = currentMap.transform.Find("TargetSpawnPoint");

        if (groundRoot == null)
            Debug.LogWarning("[StageMapManager] GroundRoot를 찾지 못했습니다.");

        if (targetSpawnPointsRoot == null && fallbackTargetSpawnPoint == null)
            Debug.LogWarning("[StageMapManager] TargetSpawnPoints 또는 TargetSpawnPoint를 찾지 못했습니다.");

        return agentSpawnPointsRoot != null && (targetSpawnPointsRoot != null || fallbackTargetSpawnPoint != null);
    }

    private void RegisterGroundRootToCamera()
    {
        if (groundRoot == null)
            return;

        AgentCameraFollow cameraFollow = FindFirstObjectByType<AgentCameraFollow>();
        if (cameraFollow == null)
        {
            Debug.LogWarning("[StageMapManager] AgentCameraFollow를 찾지 못했습니다.");
            return;
        }

        cameraFollow.SetGroundRoot(groundRoot);
    }

    private void BuildNavMesh()
    {
        if (currentMap == null)
            return;

        NavMeshSurface[] surfaces = currentMap.GetComponentsInChildren<NavMeshSurface>(true);

        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] != null)
                surfaces[i].BuildNavMesh();
        }
    }

    private void SpawnTarget()
    {
        if (targetPrefab == null)
            return;

        Transform spawnPoint = GetRandomTargetSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogError("[StageMapManager] 타겟 스폰 포인트를 찾지 못했습니다.");
            return;
        }

        currentTarget = Instantiate(targetPrefab, spawnPoint.position, spawnPoint.rotation);
        Debug.Log($"[StageMapManager] 타겟 랜덤 스폰 위치: {spawnPoint.name}");

        ApplyTargetDifficulty();
    }

    private void ApplyTargetDifficulty()
    {
        if (currentTarget == null)
            return;

        TargetController targetController = currentTarget.GetComponent<TargetController>();
        if (targetController == null)
        {
            Debug.LogWarning("[StageMapManager] TargetController를 찾지 못했습니다.");
            return;
        }

        int targetDifficultyStageNumber = currentStageIndex + 1;

        targetController.SetStageNumber(targetDifficultyStageNumber);
        targetController.ApplyDifficultyForCurrentStage();

        Debug.Log($"[StageMapManager] 타겟 난이도 적용: Stage {targetDifficultyStageNumber}");
    }

    private Transform GetRandomTargetSpawnPoint()
    {
        if (targetSpawnPointsRoot != null && targetSpawnPointsRoot.childCount > 0)
        {
            List<Transform> validSpawnPoints = new List<Transform>();

            for (int i = 0; i < targetSpawnPointsRoot.childCount; i++)
            {
                Transform child = targetSpawnPointsRoot.GetChild(i);
                if (child != null)
                    validSpawnPoints.Add(child);
            }

            if (validSpawnPoints.Count > 0)
            {
                int randomIndex = Random.Range(0, validSpawnPoints.Count);
                return validSpawnPoints[randomIndex];
            }
        }

        if (fallbackTargetSpawnPoint != null)
            return fallbackTargetSpawnPoint;

        return null;
    }

    private void SpawnAgents()
    {
        if (agentPrefabs == null || agentPrefabs.Length == 0 || agentSpawnPointsRoot == null)
            return;

        int spawnPointCount = agentSpawnPointsRoot.childCount;
        int spawnCount = Mathf.Min(agentPrefabs.Length, spawnPointCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform spawnPoint = agentSpawnPointsRoot.GetChild(i);

            if (spawnPoint == null || agentPrefabs[i] == null)
                continue;

            GameObject agent = Instantiate(agentPrefabs[i], spawnPoint.position, spawnPoint.rotation);
            currentAgents.Add(agent);
        }
    }

    private void RegisterAgentsToTarget()
    {
        if (currentTarget == null)
        {
            Debug.LogWarning("[StageMapManager] currentTarget이 없어서 에이전트 참조를 등록할 수 없습니다.");
            return;
        }

        TargetVisibilityController targetVisibility = currentTarget.GetComponent<TargetVisibilityController>();

        if (targetVisibility == null)
            targetVisibility = currentTarget.GetComponentInChildren<TargetVisibilityController>(true);

        if (targetVisibility == null)
        {
            Debug.LogWarning("[StageMapManager] TargetVisibilityController를 찾지 못했습니다.");
            return;
        }

        for (int i = 0; i < currentAgents.Count; i++)
        {
            GameObject agent = currentAgents[i];
            if (agent == null)
                continue;

            VisionSensor[] sensors = agent.GetComponentsInChildren<VisionSensor>(true);
            if (sensors == null || sensors.Length == 0)
            {
                Debug.LogWarning($"[StageMapManager] {agent.name} 에 VisionSensor가 없습니다.");
                continue;
            }

            for (int j = 0; j < sensors.Length; j++)
            {
                if (sensors[j] != null)
                    targetVisibility.RegisterSensor(sensors[j]);
            }
        }
    }

    private void RefreshCommenderAgents()
    {
        CommanderManager commanderManager = FindFirstObjectByType<CommanderManager>();
        if (commanderManager == null)
            return;

        commanderManager.RefreshAgents();
    }

    public void CompleteStage()
    {
        int unlockedStageCount = PlayerPrefs.GetInt(UnlockedStageCountKey, 1);
        int nextUnlockedCount = currentStageIndex + 2;

        if (nextUnlockedCount > unlockedStageCount)
        {
            PlayerPrefs.SetInt(UnlockedStageCountKey, nextUnlockedCount);
            PlayerPrefs.Save();
        }
    }

    public void ClearStage()
    {
        if (currentTarget != null)
        {
            Destroy(currentTarget);
            currentTarget = null;
        }

        for (int i = 0; i < currentAgents.Count; i++)
        {
            if (currentAgents[i] != null)
                Destroy(currentAgents[i]);
        }

        currentAgents.Clear();

        if (currentMap != null)
        {
            Destroy(currentMap);
            currentMap = null;
        }

        groundRoot = null;
        agentSpawnPointsRoot = null;
        targetSpawnPointsRoot = null;
        fallbackTargetSpawnPoint = null;
    }

    public string GetStageDisplayName(int stageIndex)
    {
        if (stages == null || stages.Length == 0)
            return $"Stage {stageIndex + 1}";

        if (stageIndex < 0 || stageIndex >= stages.Length)
            return $"Stage {stageIndex + 1}";

        StageEntry entry = stages[stageIndex];

        if (entry != null && !string.IsNullOrWhiteSpace(entry.stageName))
            return entry.stageName;

        return $"Stage {stageIndex + 1}";
    }
}