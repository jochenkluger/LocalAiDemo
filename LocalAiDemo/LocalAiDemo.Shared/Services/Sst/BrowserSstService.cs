using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace LocalAiDemo.Shared.Services.Sst
{
    public class BrowserSstService : SstServiceBase
    {
        public BrowserSstService(ILogger<BrowserSstService> logger)
            : base(logger)
        {
        }        /// <summary>
        /// Initialisiert die Spracherkennung mit dem übergebenen DotNetObjectReference
        /// </summary>
        /// <param name="jsRuntime">Die IJSRuntime-Instanz</param>
        /// <param name="dotNetObjectReference">Der DotNetObjectReference für Callbacks</param>
        /// <returns>True wenn erfolgreich initialisiert</returns>
        public override async Task<bool> InitializeSpeechRecognitionAsync<T>(IJSRuntime jsRuntime, DotNetObjectReference<T> dotNetObjectReference)
        {
            try
            {
                Logger.LogInformation("Initializing speech recognition");
                
                // Lade das Speech Recognition Script
                await jsRuntime.InvokeVoidAsync("eval", 
                    "if (!document.getElementById('speech-recognition-js')) {" +
                    "  var script = document.createElement('script');" +
                    "  script.id = 'speech-recognition-js';" +
                    "  script.src = '_content/LocalAiDemo.Shared/speech-recognition.js';" +
                    "  script.async = true;" +
                    "  script.onload = function() { console.log('Speech recognition script loaded successfully'); };" +
                    "  script.onerror = function() { console.error('Failed to load speech recognition script'); };" +
                    "  document.body.appendChild(script);" +
                    "}");
                
                // Kurze Verzögerung um sicherzustellen, dass das Script geladen ist
                await Task.Delay(1000);
                
                // Initialisiere die Spracherkennung mit dem DotNetObjectReference
                await jsRuntime.InvokeVoidAsync("initSpeechRecognition", dotNetObjectReference);
                Logger.LogInformation("Speech recognition initialized successfully");
                
                return true;
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
                var isAvailable = await jsRuntime.InvokeAsync<bool>("eval", 
                    "'webkitSpeechRecognition' in window || 'SpeechRecognition' in window");
                Logger.LogInformation("Speech recognition availability: {IsAvailable}", isAvailable);
                return isAvailable;
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
