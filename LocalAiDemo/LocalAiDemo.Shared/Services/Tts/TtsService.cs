using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace LocalAiDemo.Shared.Services.Tts
{
    public interface ITtsService
    {
        Task SpeakAsync(string text);
        Task SpeakAsync(string text, IJSRuntime? jsRuntime);
        Task StopSpeakingAsync();
        Task StopSpeakingAsync(IJSRuntime? jsRuntime);
        bool IsAvailable();
        Task<bool> IsAvailableAsync(IJSRuntime? jsRuntime);
        string GetProviderName();
    }    /// <summary>
    /// Abstrakte Basisklasse für TTS-Dienste, die gemeinsame Funktionalität bereitstellt
    /// </summary>
    public abstract class TtsServiceBase : ITtsService
    {
        protected readonly ILogger Logger;

        protected TtsServiceBase(ILogger logger)
        {
            Logger = logger;
        }

        public abstract Task SpeakAsync(string text);
        public virtual Task SpeakAsync(string text, IJSRuntime? jsRuntime)
        {
            // Standard-Implementierung ignoriert JSRuntime
            return SpeakAsync(text);
        }
        
        public abstract Task StopSpeakingAsync();
        public virtual Task StopSpeakingAsync(IJSRuntime? jsRuntime)
        {
            // Standard-Implementierung ignoriert JSRuntime
            return StopSpeakingAsync();
        }
        
        public abstract bool IsAvailable();
        public virtual Task<bool> IsAvailableAsync(IJSRuntime? jsRuntime)
        {
            // Standard-Implementierung ignoriert JSRuntime
            return Task.FromResult(IsAvailable());
        }
        
        public abstract string GetProviderName();
    }

    /// <summary>
    /// Factory-Service, der verschiedene TTS-Implementierungen verwaltet und zurückgibt
    /// </summary>
    public interface ITtsProviderFactory
    {
        ITtsService GetTtsService();
        void SetProvider(string providerName);
        List<string> GetAvailableProviders();
        string GetCurrentProvider();
    }

    public class TtsProviderFactory : ITtsProviderFactory
    {
        private readonly Dictionary<string, ITtsService> _ttsServices;
        private string _currentProvider;
        private readonly ILogger<TtsProviderFactory> _logger;

        public TtsProviderFactory(
            IEnumerable<ITtsService> ttsServices,
            ILogger<TtsProviderFactory> logger)
        {
            _logger = logger;
            _ttsServices = ttsServices.ToDictionary(s => s.GetProviderName(), s => s);
            
            // Setze den ersten verfügbaren Provider als Standard
            _currentProvider = _ttsServices.FirstOrDefault(s => s.Value.IsAvailable()).Key 
                ?? _ttsServices.Keys.FirstOrDefault() 
                ?? "None";
            
            _logger.LogInformation("TTS Provider Factory initialisiert mit {Count} Providern. Aktueller Provider: {Provider}", 
                _ttsServices.Count, _currentProvider);
        }

        public ITtsService GetTtsService()
        {
            if (_ttsServices.TryGetValue(_currentProvider, out var service))
            {
                return service;
            }
            
            _logger.LogWarning("Der Provider {Provider} wurde nicht gefunden. Verwende den ersten verfügbaren Provider.", _currentProvider);
            
            // Fallback auf den ersten verfügbaren Provider
            var availableProvider = _ttsServices.FirstOrDefault(s => s.Value.IsAvailable());
            
            if (availableProvider.Key != null)
            {
                _currentProvider = availableProvider.Key;
                return availableProvider.Value;
            }
            
            _logger.LogError("Kein verfügbarer TTS-Provider gefunden!");
            return new NullTtsService(_logger);
        }

        public void SetProvider(string providerName)
        {
            if (_ttsServices.ContainsKey(providerName))
            {
                _currentProvider = providerName;
                _logger.LogInformation("TTS-Provider geändert zu: {Provider}", _currentProvider);
            }
            else
            {
                _logger.LogWarning("TTS-Provider {Provider} nicht gefunden", providerName);
            }
        }

        public List<string> GetAvailableProviders()
        {
            return _ttsServices.Keys.ToList();
        }

        public string GetCurrentProvider()
        {
            return _currentProvider;
        }
    }

    /// <summary>
    /// Null-Implementierung für den Fall, dass kein TTS-Service verfügbar ist
    /// </summary>
    public class NullTtsService : TtsServiceBase
    {
        public NullTtsService(ILogger logger) : base(logger)
        {
        }

        public override Task SpeakAsync(string text)
        {
            Logger.LogWarning("Versuch Text auszugeben, aber kein TTS-Service verfügbar: {Text}", text);
            return Task.CompletedTask;
        }

        public override Task StopSpeakingAsync()
        {
            return Task.CompletedTask;
        }

        public override bool IsAvailable()
        {
            return false;
        }

        public override string GetProviderName()
        {
            return "None";
        }
    }
}
