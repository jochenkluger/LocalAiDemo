using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace LocalAiDemo.Shared.Services.Stt
{
    public class BrowserSttService : SstServiceBase
    {
        public BrowserSttService(ILogger<BrowserSttService> logger)
            : base(logger)
        {
        }

        /// <summary>
        /// Initialisiert die Spracherkennung mit dem übergebenen DotNetObjectReference
        /// </summary>
        /// <param name="jsRuntime">Die IJSRuntime-Instanz</param>
        /// <param name="dotNetObjectReference">Der DotNetObjectReference für Callbacks</param>
        /// <returns>True wenn erfolgreich initialisiert</returns>
        public override async Task<bool> InitializeSpeechRecognitionAsync<T>(IJSRuntime jsRuntime,
            DotNetObjectReference<T> dotNetObjectReference)
        {
            try
            {
                Logger.LogInformation("Initializing speech recognition");

                // Initialisiere die Spracherkennung mit dem DotNetObjectReference (script is loaded statically)
                var initialized = await jsRuntime.InvokeAsync<bool>("initSpeechRecognition", dotNetObjectReference);

                if (initialized)
                {
                    Logger.LogInformation("Speech recognition initialized successfully");
                    return true;
                }
                else
                {
                    Logger.LogError("Failed to initialize speech recognition");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing speech recognition: {ErrorMessage}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Startet die Spracherkennung
        /// </summary>
        /// <param name="jsRuntime">Die IJSRuntime-Instanz</param>
        /// <returns>Task</returns>
        public override async Task StartSpeechRecognitionAsync(IJSRuntime jsRuntime)
        {
            try
            {
                Logger.LogInformation("Starting speech recognition");
                await jsRuntime.InvokeVoidAsync("startSpeechRecognition");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error starting speech recognition: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Stoppt die Spracherkennung
        /// </summary>
        /// <param name="jsRuntime">Die IJSRuntime-Instanz</param>
        /// <returns>Task</returns>
        public override async Task StopSpeechRecognitionAsync(IJSRuntime jsRuntime)
        {
            try
            {
                Logger.LogInformation("Stopping speech recognition");
                await jsRuntime.InvokeVoidAsync("stopSpeechRecognition");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error stopping speech recognition: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Prüft ob Spracherkennung im Browser verfügbar ist
        /// </summary>
        /// <param name="jsRuntime">Die IJSRuntime-Instanz</param>
        /// <returns>True wenn verfügbar</returns>
        public override async Task<bool> IsAvailableAsync(IJSRuntime jsRuntime)
        {
            try
            {
                // Add retry mechanism for script loading timing issues
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var isAvailable = await jsRuntime.InvokeAsync<bool>("eval",
                            "'webkitSpeechRecognition' in window || 'SpeechRecognition' in window");
                        Logger.LogInformation("Speech recognition availability: {IsAvailable}", isAvailable);
                        return isAvailable;
                    }
                    catch (JSException) when (i < 2)
                    {
                        // Script not loaded yet, wait a bit and retry
                        await Task.Delay(100);
                        continue;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking speech recognition availability: {ErrorMessage}", ex.Message);
                return false;
            }
        }

        public override string GetProviderName()
        {
            return "Browser Speech Recognition";
        }
    }
}