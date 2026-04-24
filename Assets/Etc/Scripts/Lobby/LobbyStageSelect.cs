using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyStageSelect : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Progress")]
    [SerializeField] private int firstStageIndex = 0;
    [SerializeField] private int firstDifficultyIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool resetProgressOnStart = false;

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
        PlayerPrefs.SetInt(SelectedStageKey, Mathf.Max(0, firstStageIndex));
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.SetInt(UnlockedStageCountKey, Mathf.Max(1, PlayerPrefs.GetInt(UnlockedStageCountKey, 1)));
        PlayerPrefs.Save();

        SceneManager.LoadScene(gameSceneName);
    }

    public void ResetProgress()
    {
        PlayerPrefs.SetInt(UnlockedStageCountKey, 1);
        PlayerPrefs.SetInt(SelectedStageKey, Mathf.Max(0, firstStageIndex));
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.Save();
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