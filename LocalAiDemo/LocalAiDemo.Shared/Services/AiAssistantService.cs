namespace LocalAiDemo.Shared.Services
{
    public interface IAiAssistantService
    {
        Task<string> GetResponseAsync(string prompt);

        string GetAssistantName();

        bool IsOnline();

        List<string> GetCapabilities();
    }

    public class AiAssistantService : IAiAssistantService
    {
        // In a real application, this would interact with an AI service API
        public Task<string> GetResponseAsync(string prompt)
        {
            // Simulate AI processing with simple responses
            var responses = new List<string>
            {
                "Das ist eine interessante Frage. Lass mich nachdenken...",
                "Basierend auf meinen Informationen würde ich sagen...",
                "Ich kann dir dabei helfen. Hier ist, was du wissen solltest...",
                "Das ist eine gute Frage! Die Antwort ist...",
                "Ich habe einige Informationen dazu gefunden..."
            };

            var random = new Random();
            return Task.FromResult(responses[random.Next(responses.Count)]);
        }

        public string GetAssistantName()
        {
            return "Smart Assistant";
        }

        public bool IsOnline()
        {
            return true;
        }

        public List<string> GetCapabilities()
        {
            return new List<string>
            {
                "Textgenerierung",
                "Übersetzung",
                "Informationen",
                "Code-Hilfe"
            };
        }
    }
}