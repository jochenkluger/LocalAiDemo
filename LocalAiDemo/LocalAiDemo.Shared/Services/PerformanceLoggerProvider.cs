using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services
{
    public class PerformanceLoggerProvider : ILoggerProvider
    {
        private readonly IPerformanceService _performanceService;
        private readonly ConcurrentDictionary<string, PerformanceLogger> _loggers = new();

        public PerformanceLoggerProvider(IPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new PerformanceLogger(name, _performanceService));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    public class PerformanceLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IPerformanceService _performanceService;

        public PerformanceLogger(string categoryName, IPerformanceService performanceService)
        {
            _categoryName = categoryName;
            _performanceService = performanceService;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            // Filter out very frequent/unimportant log messages
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
            // Filter out very frequent debug messages
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