using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// Basis-Interface für plattformspezifische TTS-Implementierungen
    /// </summary>
    public interface IPlatformTts
    {
        Task SpeakAsync(string text);

        Task StopSpeakingAsync();

        bool IsAvailable();
    }

    /// <summary>
    /// TTS-Service, der die systemeigene TTS-Funktionalität verwendet Die konkrete Implementierung
    /// erfolgt plattformspezifisch
    /// </summary>
    public class SystemTtsService : TtsServiceBase
    {
        private readonly IPlatformTts _platformTts;

        public SystemTtsService(IPlatformTts platformTts, ILogger<SystemTtsService> logger)
            : base(logger)
        {
            _platformTts = platformTts;
            Logger.LogInformation("System TTS initialisiert mit Provider: {PlatformType}",
                _platformTts.GetType().Name);
        }

        public override async Task SpeakAsync(string text)
        {
            try
            {
                Logger.LogInformation("Spreche Text mit System TTS: {Text}", text);
                await _platformTts.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Sprechen mit System TTS: {Text}", text);
            }
        }

        public override async Task StopSpeakingAsync()
        {
            try
            {
                Logger.LogInformation("Stoppe System TTS");
                await _platformTts.StopSpeakingAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Stoppen von System TTS");
            }
        }

        public override bool IsAvailable()
        {
            try
            {
                return _platformTts.IsAvailable();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Prüfen der Verfügbarkeit von System TTS");
                return false;
            }
        }

        public override string GetProviderName()
        {
            return "System";
        }
    }
}