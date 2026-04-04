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
            Debug.LogError("[Commender] CommenderCommandProcessor 참조가 없습니다.");
            return;
        }

        if (uiController == null)
        {
            Debug.LogError("[Commender] CommenderUIController 참조가 없습니다.");
            return;
        }

        if (refreshAgentsOnStart)
            commandProcessor.RefreshAgentsFromScene();

        uiController.Initialize(commandProcessor.GetAgents());
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

            uiController.BindAgents(commandProcessor.GetAgents());

            bool success = await commandProcessor.ProcessCommandsFromUIAsync(uiController);

            if (success)
                uiController.ClearAllInputs();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Commender] 명령 제출 중 오류: {e}");
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
        if (commandProcessor == null || uiController == null)
            return;

        commandProcessor.RefreshAgentsFromScene();
        uiController.BindAgents(commandProcessor.GetAgents());
    }
}