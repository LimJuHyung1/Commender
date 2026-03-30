using OpenAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Shelter Talk용 OpenAI Chat Service
/// - NPC.cs의 ChatGPTModule 구조를 참고하여 구현
/// - 요청 중복 방지(requestInFlight)
/// - 대화 히스토리(messages) 유지 + 길이 제한 Trim
/// - CreateChatCompletion 호출 후 Choices[0].Message.Content 추출
///
/// 사용처:
/// - Manager 오브젝트에 붙여서 AnimalActor2D에 IChatService로 주입
/// </summary>
public class ChatServiceOpenAI : MonoBehaviour, IChatService
{
    [Header("AI 설정")]
    [SerializeField] private string modelName = "gpt-4o-mini";
    [SerializeField] private int maxContextMessages = 30;

    [Header("옵션")]
    [SerializeField] private bool logRawResponse = false;

    private OpenAIApi api;
    private readonly List<ChatMessage> messages = new List<ChatMessage>();

    private bool requestInFlight;
    private string currentSystemPrompt;

    private void Awake()
    {
        api = new OpenAIApi();
    }

    /// <summary>
    /// AnimalActor2D가 호출하는 진입점
    /// - systemPrompt가 바뀌면 컨텍스트를 새로 구성
    /// - userPrompt를 user 메시지로 추가
    /// - 응답을 assistant 메시지로 추가
    /// </summary>
    public async Task<string> GetResponseAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return null;

        if (requestInFlight)
        {
            Debug.LogWarning("[ShelterTalkOpenAIChatService] 요청이 이미 진행 중입니다. 새 입력을 무시합니다.");
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
                return null;

            var msg = res.Choices[0].Message;
            var reply = msg.Content;

            if (logRawResponse && !string.IsNullOrEmpty(reply))
                Debug.Log(reply);

            // 응답을 컨텍스트에 누적
            if (!string.IsNullOrWhiteSpace(reply))
            {
                messages.Add(msg);
                TrimHistoryIfNeeded();
            }

            return reply;
        }
        catch (Exception e)
        {
            Debug.LogError("[ShelterTalkOpenAIChatService] API error: " + e.Message);
            return null;
        }
        finally
        {
            requestInFlight = false;
        }
    }

    /// <summary>
    /// systemPrompt가 바뀌면 대화 컨텍스트를 초기화한다.
    /// NPC.cs의 ResetContext 느낌 그대로.
    /// </summary>
    private void EnsureContext(string systemPrompt)
    {
        string sp = string.IsNullOrWhiteSpace(systemPrompt) ? "" : systemPrompt.Trim();

        if (currentSystemPrompt == sp && messages.Count > 0)
            return;

        // 새 시스템 프롬프트면 컨텍스트 리셋
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

    /// <summary>
    /// NPC.cs와 같은 방식: system 메시지는 최대한 보존하고, 뒤쪽 user/assistant만 줄인다.
    /// </summary>
    private void TrimHistoryIfNeeded()
    {
        int systemCount = 0;
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role == "system") systemCount++;
            else break;
        }

        int budget = Mathf.Max(10, maxContextMessages);

        // system 제외하고 페어 단위로 계산
        int maxPairs = Mathf.Max(1, (budget - systemCount) / 2);
        int keepCount = systemCount + (maxPairs * 2);

        if (messages.Count <= keepCount) return;

        int removeCount = messages.Count - keepCount;
        messages.RemoveRange(systemCount, removeCount);
    }

    /// <summary>
    /// 외부에서 강제로 컨텍스트를 초기화하고 싶을 때 사용(선택)
    /// </summary>
    public void ResetContextNow()
    {
        currentSystemPrompt = null;
        messages.Clear();
    }



    public async Task<string> GetOneShotAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return null;

        var temp = new List<ChatMessage>();

        string sp = string.IsNullOrWhiteSpace(systemPrompt) ? "" : systemPrompt.Trim();
        if (!string.IsNullOrEmpty(sp))
        {
            temp.Add(new ChatMessage { Role = "system", Content = sp });
        }

        temp.Add(new ChatMessage { Role = "user", Content = userPrompt.Trim() });

        var req = new CreateChatCompletionRequest
        {
            Messages = temp,
            Model = string.IsNullOrEmpty(modelName) ? "gpt-4o-mini" : modelName
        };

        try
        {
            var res = await api.CreateChatCompletion(req);

            if (res.Choices == null || res.Choices.Count == 0)
                return null;

            // ChatMessage는 struct일 수 있으니 msg != null 같은 비교는 하지 말 것
            string reply = res.Choices[0].Message.Content;
            return string.IsNullOrWhiteSpace(reply) ? null : reply;
        }
        catch (Exception e)
        {
            Debug.LogError("[ShelterTalkOpenAIChatService] OneShot error: " + e.Message);
            return null;
        }
    }

}
