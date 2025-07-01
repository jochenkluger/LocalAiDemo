namespace LocalAiDemo.Shared.Services.Generation
{
    public class AiAssistantService(ITextGenerationService _textGenerationService) : IAiAssistantService
    {
        public async Task<string> GetResponseAsync(string prompt)
        {
            return await GetResponseAsync(prompt, CancellationToken.None);
        }

        public async Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken)
        {
            var response = await _textGenerationService.InferAsync(prompt, cancellationToken);
            return response;
        }

        public string GetAssistantName()
        {
            return "Kluger.net Demo Assistent";
        }
    }
}