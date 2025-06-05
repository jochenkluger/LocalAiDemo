namespace LocalAiDemo.Shared.Services
{
    public class AiAssistantService(ITextGenerationService _textGenerationService) : IAiAssistantService
    {
        public async Task<string> GetResponseAsync(string prompt)
        {
            var response = await _textGenerationService.InferAsync(prompt);

            return response;
        }

        public string GetAssistantName()
        {
            return "Kluger.net Demo Assistent";
        }
    }
}