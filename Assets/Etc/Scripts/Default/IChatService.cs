using System.Threading.Tasks;

public interface IChatService
{
    Task<string> GetResponseAsync(string systemPrompt, string userPrompt);

    // 추가: 일회성(판정용) 호출을 위한 메서드 정의
    Task<string> GetOneShotAsync(string systemPrompt, string userPrompt);
}