using UnityEngine;
using UnityEngine.UI;

public class LobbyStageSelect : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string defaultGameSceneName = "WarehouseStage";
    [SerializeField] private string warehouseSceneName = "WarehouseStage";
    [SerializeField] private string citySceneName = "CityStage";

    [Header("Progress")]
    [SerializeField] private int firstStageIndex = 0;
    [SerializeField] private int firstDifficultyIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool resetProgressOnStart = false;
    [SerializeField] private int debugStageNumber = 10;

    [Header("Debug Target Buttons")]
    [SerializeField] private Button[] debugTargetButtons;
    [SerializeField, Range(0.1f, 1f)] private float unclearedButtonAlpha = 1.0f;
    [SerializeField, Range(0.1f, 1f)] private float clearedButtonAlpha = 0.35f;
    [SerializeField] private bool keepClearedButtonsInteractable = true;

    private const string SelectedStageKey = "SelectedStageIndex";
    private const string SelectedDifficultyKey = "SelectedDifficultyIndex";
    private const string UnlockedStageCountKey = "UnlockedStageCount";

    private void Start()
    {
        if (resetProgressOnStart)
            ResetProgress();
        else
            EnsureDefaultProgress();

        RefreshDebugTargetButtonVisuals();
    }

    public void PlayButton()
    {
        PlayScene(defaultGameSceneName);
    }

    public void PlayWarehouseButton()
    {
        PlayScene(warehouseSceneName);
    }

    public void PlayCityButton()
    {
        PlayScene(citySceneName);
    }

    public void DebugStageButton()
    {
        DebugStageButtonByTargetIndex(0);
    }

    public void DebugStageTargetButton1()
    {
        DebugStageButtonByTargetIndex(0);
    }

    public void DebugStageTargetButton2()
    {
        DebugStageButtonByTargetIndex(1);
    }

    public void DebugStageTargetButton3()
    {
        DebugStageButtonByTargetIndex(2);
    }

    public void DebugStageButtonByTargetIndex(int targetPrefabIndex)
    {
        SaveBaseProgressPrefs();

        StageMapManager.LoadDebugStageScene(defaultGameSceneName, debugStageNumber, targetPrefabIndex);
    }

    public void DebugStageButtonByNumber(int stageNumber)
    {
        SaveBaseProgressPrefs();

        StageMapManager.LoadDebugStageScene(defaultGameSceneName, stageNumber);
    }

    public void ResetProgress()
    {
        PlayerPrefs.SetInt(UnlockedStageCountKey, 1);
        PlayerPrefs.SetInt(SelectedStageKey, Mathf.Max(0, firstStageIndex));
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.Save();

        StageMapManager.ClearDebugStageSelection();

        int buttonCount = debugTargetButtons != null ? debugTargetButtons.Length : 0;
        StageMapManager.ClearDebugTargetClearStates(debugStageNumber, buttonCount);

        RefreshDebugTargetButtonVisuals();
    }

    public void RefreshDebugTargetButtonVisuals()
    {
        if (debugTargetButtons == null || debugTargetButtons.Length == 0)
            return;

        for (int i = 0; i < debugTargetButtons.Length; i++)
        {
            Button button = debugTargetButtons[i];

            if (button == null)
                continue;

            bool isCleared = StageMapManager.IsDebugTargetCleared(debugStageNumber, i);

            ApplyButtonVisual(button, isCleared);
        }
    }

    private void PlayScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[LobbyStageSelect] 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        SaveBaseProgressPrefs();

        StageMapManager.LoadNormalGameScene(sceneName);
    }

    private void ApplyButtonVisual(Button button, bool isCleared)
    {
        if (button == null)
            return;

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = isCleared ? clearedButtonAlpha : unclearedButtonAlpha;

        bool canClick = keepClearedButtonsInteractable || !isCleared;

        button.interactable = canClick;
        canvasGroup.interactable = canClick;
        canvasGroup.blocksRaycasts = canClick;
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

    private void SaveBaseProgressPrefs()
    {
        PlayerPrefs.SetInt(SelectedDifficultyKey, Mathf.Max(0, firstDifficultyIndex));
        PlayerPrefs.SetInt(UnlockedStageCountKey, Mathf.Max(1, PlayerPrefs.GetInt(UnlockedStageCountKey, 1)));
        PlayerPrefs.Save();
    }
}