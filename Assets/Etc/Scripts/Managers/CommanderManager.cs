using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CommanderManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CommanderUIController uiController;
    [SerializeField] private CommanderCommandProcessor commandProcessor;
    [SerializeField] private Button submitButton;

    [Header("Options")]
    [SerializeField] private bool refreshAgentsOnStart = true;

    private bool requestInFlight;

    private void Awake()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitButtonClicked);
    }

    private void Start()
    {
        if (commandProcessor == null)
        {
            Debug.LogError("[Commander] CommanderCommandProcessor 참조가 없습니다.");
            return;
        }

        if (uiController == null)
        {
            Debug.LogError("[Commander] CommanderUIController 참조가 없습니다.");
            return;
        }

        if (refreshAgentsOnStart)
            commandProcessor.RefreshAgentsFromScene();

        // 중요:
        // CommanderUIController의 InputField-Agent 매핑은
        // UIController.BindPreplacedAgentUIPanels()에서만 처리합니다.
        // 여기서 uiController.Initialize(...)를 호출하면
        // InputField 목록 없이 Agent 목록만 다시 덮어써서 매핑이 꼬입니다.
    }

    private void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmitButtonClicked);
    }

    private async void OnSubmitButtonClicked()
    {
        await SubmitAllCommandsAsync();
    }

    public async Task SubmitAllCommandsAsync()
    {
        if (requestInFlight)
            return;

        if (commandProcessor == null || uiController == null)
            return;

        requestInFlight = true;
        uiController.SetUIInteractable(false);

        try
        {
            commandProcessor.RefreshAgentsFromScene();

            // 중요:
            // 여기서도 uiController.BindAgents(...)를 호출하지 않습니다.
            // UI 매핑은 이미 UIController.BindPreplacedAgentUIPanels()에서 완료된 상태입니다.

            var result = await commandProcessor.ProcessCommandsFromUIAsync(uiController);

            if (result.HasAnySuccess)
                uiController.ClearInputsByAgentIds(result.SucceededAgentIds);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commander] 명령 제출 중 오류: {e}");
        }
        finally
        {
            uiController.SetUIInteractable(true);
            requestInFlight = false;
        }
    }

    [ContextMenu("Refresh Agents")]
    public void RefreshAgents()
    {
        if (commandProcessor == null)
            return;

        commandProcessor.RefreshAgentsFromScene();

        // 중요:
        // 이 메서드는 명령 처리용 Agent 목록만 갱신합니다.
        // CommanderUIController.BindAgents(...)는 호출하지 않습니다.
    }
}