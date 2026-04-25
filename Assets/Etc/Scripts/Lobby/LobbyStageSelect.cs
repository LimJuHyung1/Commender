using UnityEngine;

public class LobbyStageSelect : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "Stage1";

    [Header("Progress")]
    public int firstStageIndex = 0;
    public int firstDifficultyIndex = 0;

    [Header("Debug")]
    public bool resetProgressOnStart = false;
    public int debugStageNumber = 1;

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string SelectedDifficultyKey = "SelectedDifficultyIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private void Start()
    {
        if (resetProgressOnStart)
            ResetProgress();
        else
            EnsureDefaultProgress();
    }

    public void PlayButton()
    {
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.SetInt(UnlockedStageCountKey, Mathf.Max(1, PlayerPrefs.GetInt(UnlockedStageCountKey, 1)));
        PlayerPrefs.Save();

        StageMapManager.LoadNormalGameScene(gameSceneName);
    }

    public void DebugStageButton()
    {
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.SetInt(UnlockedStageCountKey, Mathf.Max(1, PlayerPrefs.GetInt(UnlockedStageCountKey, 1)));
        PlayerPrefs.Save();

        StageMapManager.LoadDebugStageScene(gameSceneName, debugStageNumber);
    }

    public void DebugStageButtonByNumber(int stageNumber)
    {
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.SetInt(UnlockedStageCountKey, Mathf.Max(1, PlayerPrefs.GetInt(UnlockedStageCountKey, 1)));
        PlayerPrefs.Save();

        StageMapManager.LoadDebugStageScene(gameSceneName, stageNumber);
    }

    public void ResetProgress()
    {
        PlayerPrefs.SetInt(UnlockedStageCountKey, 1);
        PlayerPrefs.SetInt(SelectedStageKey, Mathf.Max(0, firstStageIndex));
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.Save();

        StageMapManager.ClearDebugStageSelection();
    }

    private void EnsureDefaultProgress()
    {
        if (!PlayerPrefs.HasKey(UnlockedStageCountKey))
            PlayerPrefs.SetInt(UnlockedStageCountKey, 1);

        if (!PlayerPrefs.HasKey(SelectedStageKey))
            PlayerPrefs.SetInt(SelectedStageKey, Mathf.Max(0, firstStageIndex));

        if (!PlayerPrefs.HasKey(SelectedDifficultyKey))
            PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));

        PlayerPrefs.Save();
    }
}