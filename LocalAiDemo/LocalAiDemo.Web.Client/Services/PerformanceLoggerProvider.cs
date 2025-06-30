namespace LocalAiDemo.Web.Client.Services
{
    public class PerformanceLoggerProvider : ILoggerProvider
    {
        private readonly LocalAiDemo.Shared.Services.IPerformanceService _performanceService;

        public PerformanceLoggerProvider(LocalAiDemo.Shared.Services.IPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new PerformanceLogger(_performanceService, categoryName);
        }

        public void Dispose()
        {
            // Cleanup resources if needed
        }
    }

    public class PerformanceLogger : ILogger
    {
        private readonly LocalAiDemo.Shared.Services.IPerformanceService _performanceService;
        private readonly string _categoryName;

        public PerformanceLogger(LocalAiDemo.Shared.Services.IPerformanceService performanceService,
            string categoryName)
        {
            _performanceService = performanceService;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Filter out very noisy log levels
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            // Filter out Components und Pages
            if (ShouldLogMessage(message, _categoryName))
            {
                var formattedMessage = $"{GetShortCategoryName(_categoryName)}: {message}";
                if (exception != null)
                {
                    formattedMessage += $" | Exception: {exception.Message}";
                }

                _performanceService.AddLogEntry(formattedMessage, logLevel);
            }
        }

        private bool ShouldLogMessage(string message, string categoryName)
        {
            // Filter out sehr häufige Debug-Nachrichten
            if (message.Contains("StateHasChanged") ||
                message.Contains("OnParametersSet") ||
                message.Contains("Measurement:") ||
                message.Contains("Updated measurement:"))
                return false;

            // Filter out Components und Pages
            if (categoryName.Contains("LocalAiDemo.Shared.Components") ||
                categoryName.Contains("LocalAiDemo.Shared.Pages"))
                return false;

            // Nur wichtige Kategorien loggen
            var shortCategory = GetShortCategoryName(categoryName);
            return shortCategory == "Home" ||
                   shortCategory == "Chat" ||
                   shortCategory == "AI" ||
                   shortCategory == "STT" ||
                   shortCategory == "TTS" ||
                   shortCategory == "LLM" ||
                   shortCategory == "Performance" ||
                   shortCategory == "Database" ||
                   categoryName.Contains("LocalAiDemo");
        }

        private string GetShortCategoryName(string categoryName)
        {
            // Verkürze den Kategorie-Namen für bessere Lesbarkeit
            if (categoryName.Contains("Home"))
                return "Home";
            if (categoryName.Contains("Chat"))
                return "Chat";
            if (categoryName.Contains("AiAssistant") || categoryName.Contains("TextGeneration"))
                return "AI";
            if (categoryName.Contains("Stt") || categoryName.Contains("Speech"))
                return "STT";
            if (categoryName.Contains("Tts"))
                return "TTS";
            if (categoryName.Contains("Performance"))
                return "Performance";
            if (categoryName.Contains("Database"))
                return "Database";
            if (categoryName.Contains("LocalAiDemo"))
                return "App";

            // Nehme nur den letzten Teil des Namespace
            var parts = categoryName.Split('.');
            return parts.Length > 0 ? parts[^1] : categoryName;
        }
    }
}