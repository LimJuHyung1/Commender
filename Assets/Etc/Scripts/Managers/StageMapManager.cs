using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public class StageMapManager : MonoBehaviour
{
    [System.Serializable]
    public class StageMapGroup
    {
        public string groupName;
        public int minStageNumber = 1;
        public int maxStageNumber = 1;
        public GameObject[] mapPrefabs;
    }

    [Header("Stages")]
    [SerializeField] private int totalStageCount = 10;
    [SerializeField] private StageMapGroup[] stageMapGroups;

    [Header("Unit Prefabs")]
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private GameObject[] targetPrefabs;
    [SerializeField] private GameObject[] debugTargetPrefabs;
    [SerializeField] private GameObject[] agentPrefabs;

    [Header("Options")]
    [SerializeField] private bool buildNavMeshOnSpawn = false;
    [SerializeField] private Vector3 mapSpawnEuler = new Vector3(0f, 90f, 0f);

    [Header("Debug Map")]
    [SerializeField] private bool useFixedMapIndexForDebug = false;
    [SerializeField] private int debugMapIndex = 0;

    private GameObject currentMap;
    private GameObject currentTarget;
    private readonly List<GameObject> currentAgents = new List<GameObject>();

    private Transform groundRoot;
    private Transform agentSpawnPointsRoot;
    private Transform targetSpawnPointsRoot;
    private Transform fallbackTargetSpawnPoint;

    private int currentStageIndex = 0;
    private StageMapGroup currentStageMapGroup;
    private int currentStageMapGroupIndex = -1;
    private int currentMapIndex = -1;

    public int CurrentStageIndex => currentStageIndex;
    public int CurrentStageNumber => currentStageIndex + 1;
    public int StageCount => Mathf.Max(1, totalStageCount);
    public string CurrentStageDisplayName => GetStageDisplayName(currentStageIndex);
    public int CurrentMapIndex => currentMapIndex;
    public IReadOnlyList<GameObject> CurrentAgents => currentAgents;

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private const string DebugStageEnabledKey = "DebugStageEnabled";
    private const string DebugStageIndexKey = "DebugStageIndex";
    private const string DebugTargetEnabledKey = "DebugTargetEnabled";
    private const string DebugTargetIndexKey = "DebugTargetIndex";
    private const string DebugTargetClearKeyPrefix = "DebugTargetClear";

    private const string PlayedMapHistoryKeyPrefix = "PlayedMapHistory";
    private const int MaxStoredMapGroupCount = 64;

    private void Start()
    {
        GenerateStageFromSelection();
    }

    public static void LoadNormalGameScene(string gameSceneName)
    {
        ClearDebugStageSelection();
        ClearPlayedMapHistory();

        PlayerPrefs.SetInt(SelectedStageKey, 0);
        PlayerPrefs.Save();

        SceneManager.LoadScene(gameSceneName);
    }

    public static void LoadDebugStageScene(string gameSceneName, int stageNumber)
    {
        SetDebugStageSelection(stageNumber);
        ClearDebugTargetSelection();

        SceneManager.LoadScene(gameSceneName);
    }

    public static void LoadDebugStageScene(string gameSceneName, int stageNumber, int targetPrefabIndex)
    {
        SetDebugStageSelection(stageNumber);
        SetDebugTargetSelection(targetPrefabIndex);

        SceneManager.LoadScene(gameSceneName);
    }

    public static void SetDebugStageSelection(int stageNumber)
    {
        if (!CanUseDebugStage())
        {
            Debug.LogWarning("[StageMapManager] 현재 빌드에서는 디버그 스테이지 시작을 사용할 수 없습니다.");
            return;
        }

        int stageIndex = Mathf.Max(0, stageNumber - 1);

        PlayerPrefs.SetInt(DebugStageEnabledKey, 1);
        PlayerPrefs.SetInt(DebugStageIndexKey, stageIndex);
        PlayerPrefs.SetInt(SelectedStageKey, stageIndex);
        PlayerPrefs.Save();

        Debug.Log($"[StageMapManager] 디버그 스테이지 예약 완료: StageNumber={stageNumber}, StageIndex={stageIndex}");
    }

    public static void SetDebugTargetSelection(int targetPrefabIndex)
    {
        if (!CanUseDebugStage())
        {
            Debug.LogWarning("[StageMapManager] 현재 빌드에서는 디버그 타겟 선택을 사용할 수 없습니다.");
            return;
        }

        int safeTargetPrefabIndex = Mathf.Max(0, targetPrefabIndex);

        PlayerPrefs.SetInt(DebugTargetEnabledKey, 1);
        PlayerPrefs.SetInt(DebugTargetIndexKey, safeTargetPrefabIndex);
        PlayerPrefs.Save();

        Debug.Log($"[StageMapManager] 디버그 타겟 예약 완료: TargetPrefabIndex={safeTargetPrefabIndex}");
    }

    public static void ClearDebugStageSelection()
    {
        PlayerPrefs.SetInt(DebugStageEnabledKey, 0);
        PlayerPrefs.DeleteKey(DebugStageIndexKey);

        ClearDebugTargetSelection();

        PlayerPrefs.Save();

        Debug.Log("[StageMapManager] 디버그 스테이지 예약 해제");
    }

    public static void ClearDebugTargetSelection()
    {
        PlayerPrefs.SetInt(DebugTargetEnabledKey, 0);
        PlayerPrefs.DeleteKey(DebugTargetIndexKey);
        PlayerPrefs.Save();

        Debug.Log("[StageMapManager] 디버그 타겟 예약 해제");
    }

    public static bool IsDebugTargetCleared(int stageNumber, int targetPrefabIndex)
    {
        int stageIndex = Mathf.Max(0, stageNumber - 1);
        int safeTargetPrefabIndex = Mathf.Max(0, targetPrefabIndex);

        string clearKey = GetDebugTargetClearKey(stageIndex, safeTargetPrefabIndex);

        return PlayerPrefs.GetInt(clearKey, 0) == 1;
    }

    public static void ClearDebugTargetClearState(int stageNumber, int targetPrefabIndex)
    {
        int stageIndex = Mathf.Max(0, stageNumber - 1);
        int safeTargetPrefabIndex = Mathf.Max(0, targetPrefabIndex);

        string clearKey = GetDebugTargetClearKey(stageIndex, safeTargetPrefabIndex);

        PlayerPrefs.DeleteKey(clearKey);
        PlayerPrefs.Save();
    }

    public static void ClearDebugTargetClearStates(int stageNumber, int targetPrefabCount)
    {
        int safeTargetPrefabCount = Mathf.Max(0, targetPrefabCount);

        for (int i = 0; i < safeTargetPrefabCount; i++)
        {
            ClearDebugTargetClearState(stageNumber, i);
        }

        PlayerPrefs.Save();
    }

    public static void ClearPlayedMapHistory()
    {
        for (int i = 0; i < MaxStoredMapGroupCount; i++)
        {
            PlayerPrefs.DeleteKey(GetPlayedMapHistoryKey(i));
        }

        PlayerPrefs.Save();

        Debug.Log("[StageMapManager] 플레이된 맵 기록 초기화");
    }

    private static string GetDebugTargetClearKey(int stageIndex, int targetPrefabIndex)
    {
        return $"{DebugTargetClearKeyPrefix}_{stageIndex}_{targetPrefabIndex}";
    }

    private static string GetPlayedMapHistoryKey(int groupIndex)
    {
        return $"{PlayedMapHistoryKeyPrefix}_{groupIndex}";
    }

    private static bool CanUseDebugStage()
    {
#if UNITY_EDITOR
        return true;
#elif DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }

    public void GenerateStageFromSelection()
    {
        ClearStage();

        currentStageIndex = GetStartStageIndex();
        currentStageIndex = Mathf.Clamp(currentStageIndex, 0, StageCount - 1);

        int currentStageNumber = CurrentStageNumber;

        currentStageMapGroup = GetStageMapGroup(currentStageNumber, out currentStageMapGroupIndex);
        if (currentStageMapGroup == null)
        {
            Debug.LogError($"[StageMapManager] Stage {currentStageNumber}에 맞는 StageMapGroup을 찾지 못했습니다.");
            return;
        }

        GameObject selectedMapPrefab = GetSelectedMapPrefab(currentStageMapGroup, currentStageMapGroupIndex, out currentMapIndex);
        if (selectedMapPrefab == null)
        {
            Debug.LogError($"[StageMapManager] Stage {currentStageNumber}에 사용할 수 있는 맵 프리팹이 없습니다.");
            return;
        }

        currentMap = Instantiate(
            selectedMapPrefab,
            Vector3.zero,
            Quaternion.Euler(mapSpawnEuler)
        );

        SavePlayedMapIndexIfNeeded(currentStageMapGroupIndex, currentMapIndex);

        if (!CacheMapPoints())
        {
            Debug.LogError("[StageMapManager] 맵 내부 포인트를 찾지 못했습니다.");
            return;
        }

        RegisterGroundRootToCamera();

        if (buildNavMeshOnSpawn)
            BuildNavMesh();

        TryApplyTargetMilestoneUpgrades();

        SpawnTarget();
        SpawnAgents();
        ApplyAgentUpgradesToCurrentAgents();
        RegisterAgentsToTarget();
        RefreshCommanderAgents();
        BindAgentUIPanels();

        Debug.Log(
            $"[StageMapManager] 맵 생성 완료: " +
            $"Stage={CurrentStageDisplayName}, " +
            $"StageIndex={currentStageIndex}, " +
            $"StageNumber={currentStageNumber}, " +
            $"Group={currentStageMapGroup.groupName}, " +
            $"GroupIndex={currentStageMapGroupIndex}, " +
            $"MapPrefab={selectedMapPrefab.name}, " +
            $"MapIndex={currentMapIndex}, " +
            $"MapRotation={mapSpawnEuler}, " +
            $"DebugMode={IsDebugStageMode()}"
        );
    }

    private int GetStartStageIndex()
    {
        int selectedStageIndex = PlayerPrefs.GetInt(SelectedStageKey, 0);

        if (IsDebugStageMode())
            return PlayerPrefs.GetInt(DebugStageIndexKey, selectedStageIndex);

        return selectedStageIndex;
    }

    private bool IsDebugStageMode()
    {
        if (!CanUseDebugStage())
            return false;

        return PlayerPrefs.GetInt(DebugStageEnabledKey, 0) == 1;
    }

    private StageMapGroup GetStageMapGroup(int stageNumber, out int groupIndex)
    {
        groupIndex = -1;

        if (stageMapGroups == null || stageMapGroups.Length == 0)
            return null;

        for (int i = 0; i < stageMapGroups.Length; i++)
        {
            StageMapGroup group = stageMapGroups[i];

            if (group == null)
                continue;

            int minStage = Mathf.Min(group.minStageNumber, group.maxStageNumber);
            int maxStage = Mathf.Max(group.minStageNumber, group.maxStageNumber);

            if (stageNumber >= minStage && stageNumber <= maxStage)
            {
                groupIndex = i;
                return group;
            }
        }

        return null;
    }

    private StageMapGroup GetStageMapGroup(int stageNumber)
    {
        return GetStageMapGroup(stageNumber, out _);
    }

    private GameObject GetSelectedMapPrefab(StageMapGroup group, int groupIndex, out int selectedMapIndex)
    {
        selectedMapIndex = -1;

        if (group == null || group.mapPrefabs == null || group.mapPrefabs.Length == 0)
            return null;

        List<int> validMapIndexes = GetValidMapIndexes(group);

        if (validMapIndexes.Count == 0)
            return null;

        if (IsDebugStageMode() && useFixedMapIndexForDebug)
        {
            int clampedDebugMapIndex = Mathf.Clamp(debugMapIndex, 0, group.mapPrefabs.Length - 1);

            if (group.mapPrefabs[clampedDebugMapIndex] != null)
            {
                selectedMapIndex = clampedDebugMapIndex;
                return group.mapPrefabs[selectedMapIndex];
            }

            Debug.LogWarning($"[StageMapManager] Debug Map Index {clampedDebugMapIndex}의 맵 프리팹이 비어 있습니다. 이전 맵 제외 랜덤 선택으로 대체합니다.");
        }

        List<int> candidateMapIndexes = GetUnplayedMapIndexes(group, groupIndex, validMapIndexes);

        if (candidateMapIndexes.Count == 0)
        {
            ClearPlayedMapHistoryForGroup(groupIndex);

            candidateMapIndexes = new List<int>(validMapIndexes);

            Debug.Log(
                $"[StageMapManager] GroupIndex={groupIndex}의 모든 맵을 이미 플레이했습니다. " +
                "해당 그룹의 맵 기록을 초기화하고 다시 랜덤 선택합니다."
            );
        }

        int randomListIndex = Random.Range(0, candidateMapIndexes.Count);
        selectedMapIndex = candidateMapIndexes[randomListIndex];

        return group.mapPrefabs[selectedMapIndex];
    }

    private List<int> GetValidMapIndexes(StageMapGroup group)
    {
        List<int> validMapIndexes = new List<int>();

        if (group == null || group.mapPrefabs == null)
            return validMapIndexes;

        for (int i = 0; i < group.mapPrefabs.Length; i++)
        {
            if (group.mapPrefabs[i] != null)
                validMapIndexes.Add(i);
        }

        return validMapIndexes;
    }

    private List<int> GetUnplayedMapIndexes(StageMapGroup group, int groupIndex, List<int> validMapIndexes)
    {
        List<int> candidateMapIndexes = new List<int>();

        if (group == null || validMapIndexes == null)
            return candidateMapIndexes;

        HashSet<int> playedMapIndexes = LoadPlayedMapIndexes(groupIndex);

        for (int i = 0; i < validMapIndexes.Count; i++)
        {
            int mapIndex = validMapIndexes[i];

            if (!playedMapIndexes.Contains(mapIndex))
                candidateMapIndexes.Add(mapIndex);
        }

        return candidateMapIndexes;
    }

    private void SavePlayedMapIndexIfNeeded(int groupIndex, int mapIndex)
    {
        if (groupIndex < 0 || mapIndex < 0)
            return;

        if (IsDebugStageMode() && useFixedMapIndexForDebug)
            return;

        HashSet<int> playedMapIndexes = LoadPlayedMapIndexes(groupIndex);

        if (playedMapIndexes.Contains(mapIndex))
            return;

        playedMapIndexes.Add(mapIndex);
        SavePlayedMapIndexes(groupIndex, playedMapIndexes);
    }

    private HashSet<int> LoadPlayedMapIndexes(int groupIndex)
    {
        HashSet<int> playedMapIndexes = new HashSet<int>();

        if (groupIndex < 0)
            return playedMapIndexes;

        string key = GetPlayedMapHistoryKey(groupIndex);
        string savedValue = PlayerPrefs.GetString(key, string.Empty);

        if (string.IsNullOrWhiteSpace(savedValue))
            return playedMapIndexes;

        string[] splitValues = savedValue.Split(',');

        for (int i = 0; i < splitValues.Length; i++)
        {
            if (int.TryParse(splitValues[i], out int mapIndex))
                playedMapIndexes.Add(mapIndex);
        }

        return playedMapIndexes;
    }

    private void SavePlayedMapIndexes(int groupIndex, HashSet<int> playedMapIndexes)
    {
        if (groupIndex < 0)
            return;

        string key = GetPlayedMapHistoryKey(groupIndex);

        if (playedMapIndexes == null || playedMapIndexes.Count == 0)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return;
        }

        List<int> sortedIndexes = new List<int>(playedMapIndexes);
        sortedIndexes.Sort();

        string savedValue = string.Join(",", sortedIndexes);
        PlayerPrefs.SetString(key, savedValue);
        PlayerPrefs.Save();
    }

    private void ClearPlayedMapHistoryForGroup(int groupIndex)
    {
        if (groupIndex < 0)
            return;

        PlayerPrefs.DeleteKey(GetPlayedMapHistoryKey(groupIndex));
        PlayerPrefs.Save();
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

        if (agentSpawnPointsRoot == null)
            Debug.LogWarning("[StageMapManager] AgentSpawnPoints를 찾지 못했습니다.");

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

    private void TryApplyTargetMilestoneUpgrades()
    {
        if (IsDebugStageMode())
            return;

        if (UpgradeManager.Instance == null)
            return;

        UpgradeManager.Instance.TryApplyTargetMilestoneUpgrades(CurrentStageNumber);
    }

    private void SpawnTarget()
    {
        GameObject selectedTargetPrefab = GetSelectedTargetPrefab();

        if (selectedTargetPrefab == null)
        {
            Debug.LogWarning("[StageMapManager] 생성할 타겟 프리팹이 설정되어 있지 않습니다.");
            return;
        }

        Transform spawnPoint = GetRandomTargetSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.LogError("[StageMapManager] 타겟 생성 위치를 찾지 못했습니다.");
            return;
        }

        currentTarget = Instantiate(selectedTargetPrefab, spawnPoint.position, spawnPoint.rotation);

        PlayTargetMusicForCurrentTarget();

        ApplyTargetLevelToCurrentTarget();
        ApplyTargetUpgradesToCurrentTarget();

        Debug.Log($"[StageMapManager] 타겟 생성 완료: Prefab={selectedTargetPrefab.name}, SpawnPoint={spawnPoint.name}");
    }

    private void PlayTargetMusicForCurrentTarget()
    {
        if (currentTarget == null)
            return;

        TargetBgmPlayer bgmPlayer = TargetBgmPlayer.Instance;

        if (bgmPlayer == null)
            bgmPlayer = FindFirstObjectByType<TargetBgmPlayer>();

        if (bgmPlayer == null)
        {
            Debug.LogWarning("[StageMapManager] TargetBgmPlayer를 찾지 못했습니다.");
            return;
        }

        bgmPlayer.PlayTargetMusic(currentTarget);
    }

    private GameObject GetSelectedTargetPrefab()
    {
        if (IsDebugStageMode() && PlayerPrefs.GetInt(DebugTargetEnabledKey, 0) == 1)
        {
            int targetPrefabIndex = PlayerPrefs.GetInt(DebugTargetIndexKey, 0);

            if (debugTargetPrefabs != null &&
                targetPrefabIndex >= 0 &&
                targetPrefabIndex < debugTargetPrefabs.Length &&
                debugTargetPrefabs[targetPrefabIndex] != null)
            {
                return debugTargetPrefabs[targetPrefabIndex];
            }

            Debug.LogWarning($"[StageMapManager] 디버그 타겟 인덱스가 유효하지 않습니다. TargetPrefabIndex={targetPrefabIndex}");
        }

        return GetRandomNormalTargetPrefab();
    }

    private GameObject GetRandomNormalTargetPrefab()
    {
        if (targetPrefabs != null && targetPrefabs.Length > 0)
        {
            List<GameObject> validPrefabs = new List<GameObject>();

            for (int i = 0; i < targetPrefabs.Length; i++)
            {
                if (targetPrefabs[i] != null)
                    validPrefabs.Add(targetPrefabs[i]);
            }

            if (validPrefabs.Count > 0)
            {
                int randomIndex = Random.Range(0, validPrefabs.Count);
                return validPrefabs[randomIndex];
            }
        }

        return targetPrefab;
    }

    private void ApplyTargetLevelToCurrentTarget()
    {
        if (currentTarget == null)
            return;

        TargetLevelStatApplier statApplier = currentTarget.GetComponent<TargetLevelStatApplier>();

        if (statApplier == null)
            statApplier = currentTarget.GetComponentInChildren<TargetLevelStatApplier>(true);

        if (statApplier == null)
        {
            Debug.LogWarning("[StageMapManager] TargetLevelStatApplier를 찾지 못했습니다.");
            return;
        }

        statApplier.ApplyFromStageNumber(CurrentStageNumber);

        Debug.Log($"[StageMapManager] Target Level 적용 완료: Stage={CurrentStageNumber}, TargetLevel={statApplier.TargetLevel}");
    }

    private void ApplyTargetUpgradesToCurrentTarget()
    {
        if (IsDebugStageMode())
            return;

        if (currentTarget == null)
            return;

        if (UpgradeManager.Instance == null)
            return;

        if (!TryGetTargetType(currentTarget, out CommanderTargetType targetType))
        {
            Debug.LogWarning("[StageMapManager] 현재 타겟 타입을 확인하지 못해 타겟 강화를 적용할 수 없습니다.");
            return;
        }

        List<UpgradeDefinition> upgrades = UpgradeManager.Instance.GetTargetUpgrades(targetType);
        ApplyUpgradesToHierarchy(currentTarget, upgrades);

        Debug.Log($"[StageMapManager] 타겟 강화 적용 완료: TargetType={targetType}, Count={upgrades.Count}");
    }

    private bool TryGetTargetType(GameObject targetObject, out CommanderTargetType targetType)
    {
        targetType = CommanderTargetType.None;

        if (targetObject == null)
            return false;

        if (targetObject.GetComponentInChildren<InformationBroker>(true) != null)
        {
            targetType = CommanderTargetType.InformationBroker;
            return true;
        }

        if (targetObject.GetComponentInChildren<ConstructionWorker>(true) != null)
        {
            targetType = CommanderTargetType.ConstructionWorker;
            return true;
        }

        if (targetObject.GetComponentInChildren<GraffitiArtist>(true) != null)
        {
            targetType = CommanderTargetType.GraffitiArtist;
            return true;
        }

        return false;
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
        if (agentPrefabs == null || agentPrefabs.Length == 0)
        {
            Debug.LogWarning("[StageMapManager] agentPrefabs가 비어 있습니다.");
            return;
        }

        if (agentSpawnPointsRoot == null)
        {
            Debug.LogWarning("[StageMapManager] AgentSpawnPoints가 없어서 에이전트를 생성할 수 없습니다.");
            return;
        }

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

    private void ApplyAgentUpgradesToCurrentAgents()
    {
        if (IsDebugStageMode())
            return;

        if (UpgradeManager.Instance == null)
            return;

        List<UpgradeDefinition> upgrades = UpgradeManager.Instance.GetSelectedAgentUpgrades();

        if (upgrades == null || upgrades.Count <= 0)
            return;

        for (int i = 0; i < currentAgents.Count; i++)
        {
            GameObject agent = currentAgents[i];

            if (agent == null)
                continue;

            ApplyUpgradesToHierarchy(agent, upgrades);
        }

        Debug.Log($"[StageMapManager] 에이전트 강화 적용 완료: UpgradeCount={upgrades.Count}");
    }

    private void ApplyUpgradesToHierarchy(GameObject rootObject, IReadOnlyList<UpgradeDefinition> upgrades)
    {
        if (rootObject == null)
            return;

        if (upgrades == null || upgrades.Count <= 0)
            return;

        MonoBehaviour[] behaviours = rootObject.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            IUpgradeReceiver receiver = behaviours[i] as IUpgradeReceiver;

            if (receiver == null)
                continue;

            for (int j = 0; j < upgrades.Count; j++)
            {
                UpgradeDefinition upgrade = upgrades[j];

                if (upgrade == null)
                    continue;

                if (!receiver.CanApplyUpgrade(upgrade))
                    continue;

                receiver.ApplyUpgrade(upgrade);
            }
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

    private void BindAgentUIPanels()
    {
        UIController uiController = FindFirstObjectByType<UIController>();

        if (uiController == null)
            return;

        uiController.BindPreplacedAgentUIPanels(currentAgents);
    }

    public void CompleteStage()
    {
        if (IsDebugStageMode())
        {
            SaveCurrentDebugTargetClearState();

            Debug.Log("[StageMapManager] 디버그 스테이지 클리어 기록을 저장했습니다.");
            return;
        }

        int nextStageIndex = currentStageIndex + 1;
        int nextUnlockedStageCount = Mathf.Max(PlayerPrefs.GetInt(UnlockedStageCountKey, 1), nextStageIndex + 1);

        PlayerPrefs.SetInt(UnlockedStageCountKey, Mathf.Clamp(nextUnlockedStageCount, 1, StageCount));
        PlayerPrefs.Save();
    }

    private void SaveCurrentDebugTargetClearState()
    {
        if (PlayerPrefs.GetInt(DebugTargetEnabledKey, 0) != 1)
            return;

        int debugStageIndex = PlayerPrefs.GetInt(DebugStageIndexKey, currentStageIndex);
        int debugTargetIndex = PlayerPrefs.GetInt(DebugTargetIndexKey, -1);

        if (debugTargetIndex < 0)
            return;

        string clearKey = GetDebugTargetClearKey(debugStageIndex, debugTargetIndex);

        PlayerPrefs.SetInt(clearKey, 1);
        PlayerPrefs.Save();

        Debug.Log($"[StageMapManager] 디버그 타겟 클리어 저장: StageIndex={debugStageIndex}, TargetIndex={debugTargetIndex}");
    }

    public bool HasNextStage()
    {
        return currentStageIndex + 1 < StageCount;
    }

    public void SelectNextStage()
    {
        int nextStageIndex = Mathf.Clamp(currentStageIndex + 1, 0, StageCount - 1);

        PlayerPrefs.SetInt(SelectedStageKey, nextStageIndex);

        if (IsDebugStageMode())
            PlayerPrefs.SetInt(DebugStageIndexKey, nextStageIndex);

        PlayerPrefs.Save();
    }

    public void ResetToFirstStageSelection()
    {
        PlayerPrefs.SetInt(SelectedStageKey, 0);
        ClearDebugStageSelection();
        ClearPlayedMapHistory();
        PlayerPrefs.Save();
    }

    public void ClearStage()
    {
        UIController uiController = FindFirstObjectByType<UIController>();

        if (uiController != null)
            uiController.ClearPreplacedAgentUIPanels();

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

        currentStageMapGroup = null;
        currentStageMapGroupIndex = -1;
        currentMapIndex = -1;

        groundRoot = null;
        agentSpawnPointsRoot = null;
        targetSpawnPointsRoot = null;
        fallbackTargetSpawnPoint = null;
    }

    public string GetStageDisplayName(int stageIndex)
    {
        int stageNumber = stageIndex + 1;
        StageMapGroup group = GetStageMapGroup(stageNumber);

        if (group != null && !string.IsNullOrWhiteSpace(group.groupName))
            return $"Stage {stageNumber} - {group.groupName}";

        return $"Stage {stageNumber}";
    }
}