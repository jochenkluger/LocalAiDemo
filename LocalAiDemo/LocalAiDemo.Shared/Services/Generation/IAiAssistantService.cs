namespace LocalAiDemo.Shared.Services.Generation
{
    public interface IAiAssistantService
    {
        Task<string> GetResponseAsync(string prompt);

        string GetAssistantName();
    }
}