namespace LocalAiDemo.Shared.Services.Generation
{
    public interface ITextGenerationService
    {
        Task InitializeAsync(Action<double>? onProgress = null);

        Task StartChatAsync(Action<double>? onProgress = null);

        Task<string> InferAsync(string message);

        Task<string> InferAsync(string message, CancellationToken cancellationToken);

        void CancelCurrentOperation();
    }
}