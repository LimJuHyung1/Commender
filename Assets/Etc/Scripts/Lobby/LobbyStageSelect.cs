using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyStageSelect : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject stage1Panel;
    [SerializeField] private GameObject difficultyPanel;

    [Header("Stage Buttons")]
    [SerializeField] private Button[] stageButtons;
    [SerializeField] private GameObject[] lockedObjects;

    [Header("Difficulty Buttons")]
    [SerializeField] private Button[] difficultyButtons;
    [SerializeField] private GameObject[] difficultyLockedObjects;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Debug")]
    [SerializeField] private bool resetProgressOnStart = false;

    private int pendingStageIndex = -1;

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string SelectedDifficultyKey = "SelectedDifficultyIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private void Start()
    {
        if (resetProgressOnStart)
            ResetProgress();
        else
            EnsureDefaultProgress();

        RegisterStageButtonEvents();
        RegisterDifficultyButtonEvents();
        RefreshStageButtons();

        if (stage1Panel != null)
            stage1Panel.SetActive(false);

        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
    }

    private void RegisterStageButtonEvents()
    {
        if (stageButtons == null)
            return;

        for (int i = 0; i < stageButtons.Length; i++)
        {
            if (stageButtons[i] == null)
                continue;

            stageButtons[i].onClick.RemoveAllListeners();

            int stageIndex = i;
            stageButtons[i].onClick.AddListener(() => OnClickStage(stageIndex));
        }
    }

    private void RegisterDifficultyButtonEvents()
    {
        if (difficultyButtons == null)
            return;

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null)
                continue;

            difficultyButtons[i].onClick.RemoveAllListeners();

            int difficultyIndex = i;
            difficultyButtons[i].onClick.AddListener(() => OnClickDifficulty(difficultyIndex));
        }
    }

    public void RefreshStageButtons()
    {
        int unlockedStageCount = PlayerPrefs.GetInt(UnlockedStageCountKey, 1);

        for (int i = 0; i < stageButtons.Length; i++)
        {
            bool isUnlocked = i < unlockedStageCount;

            if (stageButtons[i] != null)
                stageButtons[i].interactable = isUnlocked;

            if (lockedObjects != null && i < lockedObjects.Length && lockedObjects[i] != null)
                lockedObjects[i].SetActive(!isUnlocked);
        }
    }

    private void RefreshDifficultyButtons(int stageIndex)
    {
        int unlockedDifficultyCount = GetUnlockedDifficultyCount(stageIndex);

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            bool isUnlocked = i < unlockedDifficultyCount;

            if (difficultyButtons[i] != null)
                difficultyButtons[i].interactable = isUnlocked;

            if (difficultyLockedObjects != null &&
                i < difficultyLockedObjects.Length &&
                difficultyLockedObjects[i] != null)
            {
                difficultyLockedObjects[i].SetActive(!isUnlocked);
            }
        }
    }

    public void OnClickStage(int stageIndex)
    {
        int unlockedStageCount = PlayerPrefs.GetInt(UnlockedStageCountKey, 1);

        if (stageIndex < 0 || stageIndex >= unlockedStageCount)
        {
            Debug.LogWarning("[LobbyStageSelect] 아직 열리지 않은 스테이지입니다.");
            return;
        }

        pendingStageIndex = stageIndex;

        if (difficultyPanel != null)
            difficultyPanel.SetActive(true);

        RefreshDifficultyButtons(stageIndex);
    }

    public void OnClickDifficulty(int difficultyIndex)
    {
        if (pendingStageIndex < 0)
        {
            Debug.LogWarning("[LobbyStageSelect] 먼저 스테이지를 선택해주세요.");
            return;
        }

        int clampedDifficultyIndex = ClampDifficultyIndex(difficultyIndex);
        int unlockedDifficultyCount = GetUnlockedDifficultyCount(pendingStageIndex);

        if (clampedDifficultyIndex >= unlockedDifficultyCount)
        {
            Debug.LogWarning("[LobbyStageSelect] 아직 열리지 않은 난이도입니다.");
            return;
        }

        PlayerPrefs.SetInt(SelectedStageKey, pendingStageIndex);
        PlayerPrefs.SetInt(SelectedDifficultyKey, clampedDifficultyIndex);
        PlayerPrefs.Save();

        SceneManager.LoadScene(gameSceneName);
    }

    public void PlayButton()
    {
        if (stage1Panel != null)
            stage1Panel.SetActive(true);

        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        pendingStageIndex = -1;
        RefreshStageButtons();
    }

    public void CloseStagePanel()
    {
        if (stage1Panel != null)
            stage1Panel.SetActive(false);

        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        pendingStageIndex = -1;
    }

    public void CloseDifficultyPanel()
    {
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
    }

    public void ResetProgress()
    {
        PlayerPrefs.SetInt(UnlockedStageCountKey, 1);
        PlayerPrefs.SetInt(SelectedStageKey, 0);
        PlayerPrefs.SetInt(SelectedDifficultyKey, 0);

        if (stageButtons != null)
        {
            for (int i = 0; i < stageButtons.Length; i++)
                PlayerPrefs.DeleteKey(GetUnlockedDifficultyKey(i));
        }

        PlayerPrefs.SetInt(GetUnlockedDifficultyKey(0), 1);
        PlayerPrefs.Save();

        pendingStageIndex = -1;
        RefreshStageButtons();

        if (difficultyPanel != null && difficultyPanel.activeSelf)
            RefreshDifficultyButtons(0);
    }

    public void UnlockAllStages()
    {
        if (stageButtons == null)
            return;

        PlayerPrefs.SetInt(UnlockedStageCountKey, stageButtons.Length);

        int allDifficultyCount = difficultyButtons != null && difficultyButtons.Length > 0
            ? difficultyButtons.Length
            : 1;

        for (int i = 0; i < stageButtons.Length; i++)
            PlayerPrefs.SetInt(GetUnlockedDifficultyKey(i), allDifficultyCount);

        PlayerPrefs.Save();
        RefreshStageButtons();

        if (pendingStageIndex >= 0)
            RefreshDifficultyButtons(pendingStageIndex);
    }

    private void EnsureDefaultProgress()
    {
        if (!PlayerPrefs.HasKey(UnlockedStageCountKey))
            PlayerPrefs.SetInt(UnlockedStageCountKey, 1);

        if (!PlayerPrefs.HasKey(GetUnlockedDifficultyKey(0)))
            PlayerPrefs.SetInt(GetUnlockedDifficultyKey(0), 1);

        if (!PlayerPrefs.HasKey(SelectedStageKey))
            PlayerPrefs.SetInt(SelectedStageKey, 0);

        if (!PlayerPrefs.HasKey(SelectedDifficultyKey))
            PlayerPrefs.SetInt(SelectedDifficultyKey, 0);

        PlayerPrefs.Save();
    }

    private int GetUnlockedDifficultyCount(int stageIndex)
    {
        if (stageIndex < 0)
            return 0;

        int defaultValue = stageIndex == 0 ? 1 : 0;
        int unlockedCount = PlayerPrefs.GetInt(GetUnlockedDifficultyKey(stageIndex), defaultValue);

        if (difficultyButtons == null || difficultyButtons.Length == 0)
            return Mathf.Max(0, unlockedCount);

        return Mathf.Clamp(unlockedCount, 0, difficultyButtons.Length);
    }

    private string GetUnlockedDifficultyKey(int stageIndex)
    {
        return $"UnlockedDifficultyCount_Stage_{stageIndex}";
    }

    private int ClampDifficultyIndex(int difficultyIndex)
    {
        if (difficultyButtons == null || difficultyButtons.Length == 0)
            return 0;

        return Mathf.Clamp(difficultyIndex, 0, difficultyButtons.Length - 1);
    }
}