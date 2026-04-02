using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyStageSelect : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject stage1Panel;

    [Header("Stage Buttons")]
    [SerializeField] private Button[] stageButtons;
    [SerializeField] private GameObject[] lockedObjects;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Debug")]
    [SerializeField] private bool resetProgressOnStart = false;

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private void Start()
    {
        if (resetProgressOnStart)
            ResetProgress();

        RegisterButtonEvents();
        RefreshStageButtons();

        if (stage1Panel != null)
            stage1Panel.SetActive(false);
    }

    private void RegisterButtonEvents()
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

    public void OnClickStage(int stageIndex)
    {
        int unlockedStageCount = PlayerPrefs.GetInt(UnlockedStageCountKey, 1);

        if (stageIndex < 0 || stageIndex >= unlockedStageCount)
        {
            Debug.LogWarning("[LobbyStageSelect] 아직 열리지 않은 스테이지입니다.");
            return;
        }

        PlayerPrefs.SetInt(SelectedStageKey, stageIndex);
        PlayerPrefs.Save();

        SceneManager.LoadScene(gameSceneName);
    }

    public void PlayButton()
    {
        if (stage1Panel != null)
            stage1Panel.SetActive(true);

        RefreshStageButtons();
    }

    public void CloseStagePanel()
    {
        if (stage1Panel != null)
            stage1Panel.SetActive(false);
    }

    public void ResetProgress()
    {
        PlayerPrefs.SetInt(UnlockedStageCountKey, 1);
        PlayerPrefs.SetInt(SelectedStageKey, 0);
        PlayerPrefs.Save();

        RefreshStageButtons();
    }

    public void UnlockAllStages()
    {
        if (stageButtons == null)
            return;

        PlayerPrefs.SetInt(UnlockedStageCountKey, stageButtons.Length);
        PlayerPrefs.Save();

        RefreshStageButtons();
    }
}