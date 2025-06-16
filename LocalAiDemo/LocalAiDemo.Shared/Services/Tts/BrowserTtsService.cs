using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// Implementierung des TTS-Services mit der Web Speech API des Browsers
    /// </summary>
    public class BrowserTtsService : TtsServiceBase
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _initialized;

        public BrowserTtsService(IJSRuntime jsRuntime, ILogger<BrowserTtsService> logger)
            : base(logger)
        {
            _jsRuntime = jsRuntime;
            _initialized = false;
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                try
                {
                    Logger.LogInformation("Initialisiere Browser TTS...");

                    // Lade das TTS-Skript dynamisch
                    await _jsRuntime.InvokeVoidAsync("eval",
                        "if (!document.getElementById('browser-tts-js')) {" +
                        "  var script = document.createElement('script');" +
                        "  script.id = 'browser-tts-js';" +
                        "  script.src = '_content/LocalAiDemo.Shared/browser-tts.js';" +
                        "  script.async = true;" +
                        "  script.onload = function() { console.log('Browser TTS script loaded successfully'); };" +
                        "  script.onerror = function() { console.error('Failed to load Browser TTS script'); };" +
                        "  document.body.appendChild(script);" +
                        "}");

                    // Kurze Verzögerung, um sicherzustellen, dass das Skript geladen wurde
                    await Task.Delay(500);

                    // Initialisiere den TTS-Dienst
                    var isAvailable = await _jsRuntime.InvokeAsync<bool>("initBrowserTts");

                    _initialized = isAvailable;
                    Logger.LogInformation("Browser TTS initialisiert: {Available}", isAvailable);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Fehler bei der Initialisierung von Browser TTS");
                    _initialized = false;
                }
            }
        }

        public override async Task SpeakAsync(string text)
        {
            await EnsureInitializedAsync();

            if (!_initialized)
            {
                Logger.LogWarning("Browser TTS nicht initialisiert. Text kann nicht gesprochen werden: {Text}", text);
                return;
            }

            try
            {
                Logger.LogInformation("Spreche Text mit Browser TTS: {Text}", text);
                await _jsRuntime.InvokeVoidAsync("speakText", text);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Sprechen mit Browser TTS: {Text}", text);
            }
        }

        public override async Task StopSpeakingAsync()
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                Logger.LogInformation("Stoppe Browser TTS");
                await _jsRuntime.InvokeVoidAsync("stopSpeaking");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Stoppen von Browser TTS");
            }
        }

        public override bool IsAvailable()
        {
            // Wir können nicht synchron den Browser abfragen, daher gehen wir davon aus, dass es
            // grundsätzlich verfügbar sein könnte Die tatsächliche Verfügbarkeit wird bei der
            // Initialisierung geprüft
            return true;
        }

        public override string GetProviderName()
        {
            return "Browser";
        }
    }
}