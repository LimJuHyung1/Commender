using OpenAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChatServiceOpenAI : MonoBehaviour, IChatService
{
    [Header("AI 설정")]
    [SerializeField] private string modelName = "gpt-4o-mini";
    [SerializeField] private int maxContextMessages = 30;

    [Header("옵션")]
    [SerializeField] private bool logRawResponse = false;

    [Header("실패 대응")]
    [SerializeField] private bool returnFallbackMessageOnFailure = false;
    [TextArea]
    [SerializeField] private string fallbackMessage = "지금은 대답할 수 없어.";

    private OpenAIApi api;
    private readonly List<ChatMessage> messages = new List<ChatMessage>();

    private bool requestInFlight;
    private bool apiInitializeTried;
    private bool apiReady;
    private string currentSystemPrompt;

    public async Task<string> GetResponseAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return null;

        if (!EnsureApiInitialized())
            return HandleFailure("OpenAI API initialization failed.");

        if (requestInFlight)
        {
            Debug.LogWarning("[ChatServiceOpenAI] A request is already in progress.");
            return null;
        }

        requestInFlight = true;

        try
        {
            EnsureContext(systemPrompt);

            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = userPrompt.Trim()
            });

            TrimHistoryIfNeeded();

            var req = new CreateChatCompletionRequest
            {
                Messages = messages,
                Model = string.IsNullOrEmpty(modelName) ? "gpt-4o-mini" : modelName
            };

            var res = await api.CreateChatCompletion(req);

            if (res.Choices == null || res.Choices.Count == 0)
                return HandleFailure("OpenAI response was empty.");

            var msg = res.Choices[0].Message;
            var reply = msg.Content;

            if (logRawResponse && !string.IsNullOrEmpty(reply))
                Debug.Log(reply);

            if (!string.IsNullOrWhiteSpace(reply))
            {
                messages.Add(msg);
                TrimHistoryIfNeeded();
            }

            return string.IsNullOrWhiteSpace(reply)
                ? HandleFailure("Assistant reply was empty.")
                : reply;
        }
        catch (Exception e)
        {
            return HandleFailure("[ChatServiceOpenAI] API error: " + e.Message);
        }
        finally
        {
            requestInFlight = false;
        }
    }

    public async Task<string> GetOneShotAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return null;

        if (!EnsureApiInitialized())
            return HandleFailure("OpenAI API initialization failed.");

        var temp = new List<ChatMessage>();

        string sp = string.IsNullOrWhiteSpace(systemPrompt) ? "" : systemPrompt.Trim();
        if (!string.IsNullOrEmpty(sp))
        {
            temp.Add(new ChatMessage
            {
                Role = "system",
                Content = sp
            });
        }

        temp.Add(new ChatMessage
        {
            Role = "user",
            Content = userPrompt.Trim()
        });

        var req = new CreateChatCompletionRequest
        {
            Messages = temp,
            Model = string.IsNullOrEmpty(modelName) ? "gpt-4o-mini" : modelName
        };

        try
        {
            var res = await api.CreateChatCompletion(req);

            if (res.Choices == null || res.Choices.Count == 0)
                return HandleFailure("OpenAI response was empty.");

            string reply = res.Choices[0].Message.Content;
            return string.IsNullOrWhiteSpace(reply)
                ? HandleFailure("Assistant reply was empty.")
                : reply;
        }
        catch (Exception e)
        {
            return HandleFailure("[ChatServiceOpenAI] OneShot error: " + e.Message);
        }
    }

    public void ResetContextNow()
    {
        currentSystemPrompt = null;
        messages.Clear();
    }

    private bool EnsureApiInitialized()
    {
        if (apiReady && api != null)
            return true;

        if (apiInitializeTried)
            return false;

        apiInitializeTried = true;

        APIKeyManager keyManager = APIKeyManager.Instance;
        if (keyManager == null)
        {
            keyManager = FindFirstObjectByType<APIKeyManager>();
        }

        if (keyManager == null)
        {
            Debug.LogError("[ChatServiceOpenAI] APIKeyManager was not found in the scene.");
            apiReady = false;
            return false;
        }

        if (!keyManager.Initialize())
        {
            Debug.LogError("[ChatServiceOpenAI] APIKeyManager initialization failed. " + keyManager.LastError);
            apiReady = false;
            return false;
        }

        if (!keyManager.TryGetAuthData(out AuthData authData))
        {
            Debug.LogError("[ChatServiceOpenAI] Auth data is missing or invalid. " + keyManager.LastError);
            apiReady = false;
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(authData.Organization))
            {
                api = new OpenAIApi(authData.ApiKey);
            }
            else
            {
                api = new OpenAIApi(authData.ApiKey, authData.Organization);
            }

            apiReady = true;
            Debug.Log("[ChatServiceOpenAI] OpenAI API initialized successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[ChatServiceOpenAI] Failed to construct OpenAIApi: " + ex.Message);
            apiReady = false;
            return false;
        }
    }

    private void EnsureContext(string systemPrompt)
    {
        string sp = string.IsNullOrWhiteSpace(systemPrompt) ? "" : systemPrompt.Trim();

        if (currentSystemPrompt == sp && messages.Count > 0)
            return;

        currentSystemPrompt = sp;
        messages.Clear();

        if (!string.IsNullOrEmpty(currentSystemPrompt))
        {
            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = currentSystemPrompt
            });
        }
    }

    private void TrimHistoryIfNeeded()
    {
        int systemCount = 0;
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role == "system")
                systemCount++;
            else
                break;
        }

        int budget = Mathf.Max(10, maxContextMessages);
        int maxPairs = Mathf.Max(1, (budget - systemCount) / 2);
        int keepCount = systemCount + (maxPairs * 2);

        if (messages.Count <= keepCount)
            return;

        int removeCount = messages.Count - keepCount;
        messages.RemoveRange(systemCount, removeCount);
    }

    private string HandleFailure(string logMessage)
    {
        Debug.LogError(logMessage);

        if (returnFallbackMessageOnFailure && !string.IsNullOrWhiteSpace(fallbackMessage))
            return fallbackMessage;

        return null;
    }
}
