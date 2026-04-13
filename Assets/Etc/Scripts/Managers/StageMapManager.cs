using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class StageMapManager : MonoBehaviour
{
    [System.Serializable]
    public class DifficultyMapEntry
    {
        public string difficultyName = "Easy";
        [Min(1)] public int difficultyProfileNumber = 1;
        public GameObject mapPrefab;
    }

    [System.Serializable]
    public class StageEntry
    {
        public string stageName;
        public DifficultyMapEntry[] difficultyMaps;
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
    private int currentDifficultyIndex = 0;

    public int CurrentStageIndex => currentStageIndex;
    public int CurrentDifficultyIndex => currentDifficultyIndex;
    public string CurrentStageDisplayName => GetStageDisplayName(currentStageIndex);
    public string CurrentDifficultyDisplayName => GetDifficultyDisplayName(currentStageIndex, currentDifficultyIndex);

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string SelectedDifficultyKey = "SelectedDifficultyIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private void Start()
    {
        GenerateStageFromSelection();
    }

    public void GenerateStageFromSelection()
    {
        ClearStage();

        currentStageIndex = PlayerPrefs.GetInt(SelectedStageKey, 0);
        currentDifficultyIndex = PlayerPrefs.GetInt(SelectedDifficultyKey, 0);

        if (stages == null || stages.Length == 0)
        {
            Debug.LogError("[StageMapManager] stages가 비어 있습니다.");
            return;
        }

        currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stages.Length - 1);

        DifficultyMapEntry selectedDifficultyEntry = GetSelectedDifficultyEntry(currentStageIndex, currentDifficultyIndex);
        if (selectedDifficultyEntry == null || selectedDifficultyEntry.mapPrefab == null)
        {
            Debug.LogError("[StageMapManager] 선택된 스테이지/난이도에 맞는 맵 프리팹이 없습니다.");
            return;
        }

        currentMap = Instantiate(selectedDifficultyEntry.mapPrefab, Vector3.zero, Quaternion.identity);

        if (!CacheMapPoints())
        {
            Debug.LogError("[StageMapManager] 맵 내부 포인트를 찾지 못했습니다.");
            return;
        }

        RegisterGroundRootToCamera();

        if (buildNavMeshOnSpawn)
            BuildNavMesh();

        SpawnTarget();
        SpawnAgents();
        RegisterAgentsToTarget();
        RefreshCommanderAgents();

        Debug.Log(
            $"[StageMapManager] 맵 생성 완료: " +
            $"Stage={CurrentStageDisplayName}, Difficulty={CurrentDifficultyDisplayName}, " +
            $"Profile={selectedDifficultyEntry.difficultyProfileNumber}"
        );
    }

    private DifficultyMapEntry GetSelectedDifficultyEntry(int stageIndex, int difficultyIndex)
    {
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
            return null;

        StageEntry stageEntry = stages[stageIndex];
        if (stageEntry == null || stageEntry.difficultyMaps == null || stageEntry.difficultyMaps.Length == 0)
            return null;

        int clampedDifficultyIndex = Mathf.Clamp(difficultyIndex, 0, stageEntry.difficultyMaps.Length - 1);
        currentDifficultyIndex = clampedDifficultyIndex;

        return stageEntry.difficultyMaps[clampedDifficultyIndex];
    }

    private int GetDifficultyCountForStage(int stageIndex)
    {
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
            return 0;

        StageEntry stageEntry = stages[stageIndex];
        if (stageEntry == null || stageEntry.difficultyMaps == null)
            return 0;

        return stageEntry.difficultyMaps.Length;
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
            Debug.LogError("[StageMapManager] 타겟 생성 위치를 찾지 못했습니다.");
            return;
        }

        currentTarget = Instantiate(targetPrefab, spawnPoint.position, spawnPoint.rotation);
        Debug.Log($"[StageMapManager] 타겟 생성 위치: {spawnPoint.name}");

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

        DifficultyMapEntry selectedDifficultyEntry = GetSelectedDifficultyEntry(currentStageIndex, currentDifficultyIndex);
        if (selectedDifficultyEntry == null)
        {
            Debug.LogWarning("[StageMapManager] 선택된 난이도 데이터를 찾지 못했습니다.");
            return;
        }

        targetController.SetStageNumber(selectedDifficultyEntry.difficultyProfileNumber);
        targetController.ApplyDifficultyForCurrentStage();
        ApplyTargetSkillSetByDifficulty();

        Debug.Log(
            $"[StageMapManager] 타겟 난이도 적용: " +
            $"{selectedDifficultyEntry.difficultyName}, " +
            $"프로필 번호={selectedDifficultyEntry.difficultyProfileNumber}"
        );
    }

    private void ApplyTargetSkillSetByDifficulty()
    {
        if (currentTarget == null)
            return;

        TargetSkillController targetSkillController = currentTarget.GetComponent<TargetSkillController>();
        if (targetSkillController == null)
            targetSkillController = currentTarget.GetComponentInChildren<TargetSkillController>(true);

        if (targetSkillController == null)
        {
            Debug.LogWarning("[StageMapManager] TargetSkillController를 찾지 못했습니다.");
            return;
        }

        targetSkillController.ApplySkillSetByDifficultyIndex(currentDifficultyIndex);

        Debug.Log(
            $"[StageMapManager] 타겟 스킬 세트 적용: DifficultyIndex={currentDifficultyIndex}, " +
            $"DifficultyName={CurrentDifficultyDisplayName}"
        );
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
            Debug.LogWarning("[StageMapManager] currentTarget가 없어서 타겟 시야 등록을 할 수 없습니다.");
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

    private void RefreshCommanderAgents()
    {
        CommanderManager commanderManager = FindFirstObjectByType<CommanderManager>();
        if (commanderManager == null)
            return;

        commanderManager.RefreshAgents();
    }

    public void CompleteStage()
    {
        int currentStageDifficultyCount = GetDifficultyCountForStage(currentStageIndex);
        int currentUnlockedDifficultyCount = GetUnlockedDifficultyCount(currentStageIndex, currentStageDifficultyCount);

        bool unlockedSomething = false;

        if (currentDifficultyIndex < currentStageDifficultyCount - 1)
        {
            int nextUnlockedDifficultyCount = Mathf.Max(
                currentUnlockedDifficultyCount,
                currentDifficultyIndex + 2
            );

            if (nextUnlockedDifficultyCount > currentUnlockedDifficultyCount)
            {
                PlayerPrefs.SetInt(GetUnlockedDifficultyKey(currentStageIndex), nextUnlockedDifficultyCount);
                unlockedSomething = true;

                Debug.Log(
                    $"[StageMapManager] 같은 스테이지의 다음 난이도 해금: " +
                    $"Stage={currentStageIndex}, UnlockedDifficultyCount={nextUnlockedDifficultyCount}"
                );
            }
        }
        else
        {
            int unlockedStageCount = PlayerPrefs.GetInt(UnlockedStageCountKey, 1);
            int nextUnlockedStageCount = currentStageIndex + 2;

            if (nextUnlockedStageCount > unlockedStageCount)
            {
                PlayerPrefs.SetInt(UnlockedStageCountKey, nextUnlockedStageCount);
                unlockedSomething = true;

                Debug.Log($"[StageMapManager] 다음 스테이지 해금: {nextUnlockedStageCount - 1}");
            }

            int nextStageIndex = currentStageIndex + 1;
            if (nextStageIndex < stages.Length)
            {
                int nextStageUnlockedDifficultyCount = GetUnlockedDifficultyCount(
                    nextStageIndex,
                    GetDifficultyCountForStage(nextStageIndex)
                );

                if (nextStageUnlockedDifficultyCount < 1)
                {
                    PlayerPrefs.SetInt(GetUnlockedDifficultyKey(nextStageIndex), 1);
                    unlockedSomething = true;

                    Debug.Log($"[StageMapManager] 다음 스테이지 첫 난이도 해금: Stage={nextStageIndex}");
                }
            }
        }

        if (unlockedSomething)
            PlayerPrefs.Save();
    }

    private int GetUnlockedDifficultyCount(int stageIndex, int difficultyCount)
    {
        int defaultValue = stageIndex == 0 ? 1 : 0;
        int unlockedCount = PlayerPrefs.GetInt(GetUnlockedDifficultyKey(stageIndex), defaultValue);
        return Mathf.Clamp(unlockedCount, 0, Mathf.Max(0, difficultyCount));
    }

    private string GetUnlockedDifficultyKey(int stageIndex)
    {
        return $"UnlockedDifficultyCount_Stage_{stageIndex}";
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

    public string GetDifficultyDisplayName(int stageIndex, int difficultyIndex)
    {
        DifficultyMapEntry entry = GetSelectedDifficultyEntry(stageIndex, difficultyIndex);
        if (entry != null && !string.IsNullOrWhiteSpace(entry.difficultyName))
            return entry.difficultyName;

        switch (difficultyIndex)
        {
            case 0: return "Easy";
            case 1: return "Normal";
            case 2: return "Difficult";
            default: return $"난이도 {difficultyIndex + 1}";
        }
    }
}