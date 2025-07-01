namespace LocalAiDemo.Shared.Services.Generation
{
    public interface IAiAssistantService
    {
        Task<string> GetResponseAsync(string prompt);

        Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken);

        string GetAssistantName();
    }
}