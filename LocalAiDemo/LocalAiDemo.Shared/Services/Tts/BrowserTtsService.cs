using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// Implementierung des TTS-Services mit der Web Speech API des Browsers
    /// </summary>
    public class BrowserTtsService : TtsServiceBase
    {
        private bool _initialized;

        public BrowserTtsService(ILogger<BrowserTtsService> logger)
            : base(logger)
        {
            _initialized = false;
        }

        private bool CheckWebViewContext(IJSRuntime jsRuntime)
        {
            try
            {
                // Check if we're in a valid JavaScript context In MAUI WebView or Blazor
                // WebAssembly, this should be true In server-side rendering or other contexts, this
                // will be false
                var runtimeType = jsRuntime.GetType().Name;
                Logger.LogDebug("JSRuntime type: {RuntimeType}", runtimeType);

                // Common JSRuntime types that support JavaScript interop:
                // - WebViewJSRuntime (MAUI)
                // - RemoteJSRuntime (Blazor WebAssembly)
                // - WebAssemblyJSRuntime (Blazor WebAssembly)
                return runtimeType.Contains("WebView") ||
                       runtimeType.Contains("WebAssembly") ||
                       runtimeType.Contains("Remote");
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Fehler bei der Erkennung des WebView-Kontexts");
                return false;
            }
        }

        private async Task EnsureInitializedAsync(IJSRuntime jsRuntime)
        {
            var isWebViewContext = CheckWebViewContext(jsRuntime);

            if (!_initialized && isWebViewContext)
            {
                try
                {
                    Logger.LogInformation("Initialisiere Browser TTS...");

                    // Teste zuerst, ob JavaScript-Aufrufe möglich sind
                    await jsRuntime.InvokeVoidAsync("eval", "console.log('TTS: JS-Kontext verfügbar');");

                    // Definiere TTS-Funktionen direkt inline (wie in Home.razor)
                    await jsRuntime.InvokeVoidAsync("eval", @"
                        // Definiere TTS-Funktionen direkt im Window-Objekt
                        window.speechSynthesis = window.speechSynthesis || {};
                        window.ttsVoices = [];
                        window.currentUtterance = null;

                        // TTS-Initialisierungsfunktion
                        window.initBrowserTts = function() {
                            try {
                                if ('speechSynthesis' in window) {
                                    // Abrufen der verfügbaren Stimmen
                                    window.ttsVoices = window.speechSynthesis.getVoices();

                                    // In Chrome werden die Stimmen asynchron geladen
                                    if (window.ttsVoices.length === 0) {
                                        window.speechSynthesis.addEventListener('voiceschanged', function() {
                                            window.ttsVoices = window.speechSynthesis.getVoices();
                                            console.log('TTS: Geladen ' + window.ttsVoices.length + ' Stimmen');
                                        });
                                    } else {
                                        console.log('TTS: Geladen ' + window.ttsVoices.length + ' Stimmen');
                                    }

                                    console.log('Browser TTS erfolgreich initialisiert');
                                    return true;
                                } else {
                                    console.error('Keine Browser TTS-Unterstützung verfügbar');
                                    return false;
                                }
                            } catch (error) {
                                console.error('Fehler bei der Initialisierung von Browser TTS:', error);
                                return false;
                            }
                        };

                        // Text vorlesen Funktion
                        window.speakText = function(text) {
                            try {
                                if (!('speechSynthesis' in window)) {
                                    console.error('TTS ist nicht verfügbar');
                                    return false;
                                }

                                // Stoppe aktuelle Sprache, falls vorhanden
                                if (window.currentUtterance) {
                                    window.stopSpeaking();
                                }

                                // Erstelle neue Äußerung
                                var utterance = new SpeechSynthesisUtterance(text);
                                window.currentUtterance = utterance;

                                // Konfiguriere Stimme (bevorzuge deutsche Stimmen)
                                var voices = window.speechSynthesis.getVoices();
                                var germanVoice = voices.find(voice => voice.lang === 'de-DE' && voice.localService);
                                var anyGermanVoice = voices.find(voice => voice.lang.startsWith('de'));

                                if (germanVoice) {
                                    utterance.voice = germanVoice;
                                    console.log('TTS: Verwende lokale deutsche Stimme: ' + germanVoice.name);
                                } else if (anyGermanVoice) {
                                    utterance.voice = anyGermanVoice;
                                    console.log('TTS: Verwende deutsche Stimme: ' + anyGermanVoice.name);
                                } else {
                                    console.log('TTS: Keine deutsche Stimme gefunden, verwende Standard-Stimme');
                                }

                                // Konfiguriere Sprachparameter
                                utterance.lang = 'de-DE';
                                utterance.rate = 1.0;
                                utterance.pitch = 1.0;
                                utterance.volume = 1.0;

                                // Event-Handler
                                utterance.onend = function() {
                                    console.log('TTS: Sprechen beendet');
                                    window.currentUtterance = null;
                                };

                                utterance.onerror = function(event) {
                                    console.error('TTS Fehler:', event.error);
                                    window.currentUtterance = null;
                                };

                                // Starte Sprachausgabe
                                window.speechSynthesis.speak(utterance);
                                console.log('TTS: Spreche Text: ' + text);
                                return true;
                            } catch (error) {
                                console.error('TTS Fehler beim Sprechen:', error);
                                return false;
                            }
                        };

                        // Stoppe Sprachausgabe
                        window.stopSpeaking = function() {
                            try {
                                if ('speechSynthesis' in window) {
                                    // Verwende sowohl cancel() als auch pause() für sicheres Stoppen
                                    window.speechSynthesis.cancel();
                                    window.speechSynthesis.pause();
                                    
                                    // Setze currentUtterance auf null
                                    window.currentUtterance = null;
                                    
                                    // Force clear the speech queue (workaround für manche Browser)
                                    setTimeout(() => {
                                        window.speechSynthesis.cancel();
                                    }, 10);
                                    
                                    console.log('TTS: Sprache gestoppt');
                                    return true;
                                }
                                return false;
                            } catch (error) {
                                console.error('TTS Fehler beim Stoppen:', error);
                                return false;
                            }
                        };
                    "); // Kurze Verzögerung für die JavaScript-Ausführung
                    await Task.Delay(100);

                    // Initialisiere den TTS-Dienst
                    var isAvailable = await jsRuntime.InvokeAsync<bool>("initBrowserTts");

                    _initialized = isAvailable;
                    Logger.LogInformation("Browser TTS initialisiert: {Available}", isAvailable);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("WebView context") ||
                                                           ex.Message.Contains("JavaScript"))
                {
                    Logger.LogWarning("JavaScript-Kontext nicht verfügbar für TTS: {Message}", ex.Message);
                    _initialized = false;
                }
                catch (JSException ex)
                {
                    Logger.LogWarning(ex, "JavaScript-Fehler bei TTS-Initialisierung: {Message}", ex.Message);
                    _initialized = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Fehler bei der Initialisierung von Browser TTS");
                    _initialized = false;
                }
            }
            else if (!isWebViewContext)
            {
                Logger.LogDebug("TTS-Initialisierung übersprungen - kein WebView-Kontext");
            }
        }

        public override Task SpeakAsync(string text)
        {
            // Diese Überladung sollte für Browser TTS nicht verwendet werden
            Logger.LogError("SpeakAsync ohne IJSRuntime aufgerufen - Browser TTS benötigt JavaScript-Kontext");
            throw new InvalidOperationException(
                "Browser TTS Service benötigt einen IJSRuntime Parameter. " +
                "Verwenden Sie SpeakAsync(string text, IJSRuntime jsRuntime) stattdessen.");
        }

        public override async Task SpeakAsync(string text, IJSRuntime? jsRuntime)
        {
            if (jsRuntime == null)
            {
                Logger.LogError("SpeakAsync mit null IJSRuntime aufgerufen - Browser TTS benötigt JavaScript-Kontext");
                throw new ArgumentNullException(nameof(jsRuntime),
                    "Browser TTS Service benötigt einen gültigen IJSRuntime Parameter.");
            }

            var isWebViewContext = CheckWebViewContext(jsRuntime);

            if (!isWebViewContext)
            {
                Logger.LogDebug("TTS übersprungen - kein WebView-Kontext verfügbar");
                return;
            }

            await EnsureInitializedAsync(jsRuntime);

            if (!_initialized)
            {
                Logger.LogWarning("Browser TTS nicht initialisiert. Text kann nicht gesprochen werden: {Text}", text);
                return;
            }

            try
            {
                Logger.LogInformation("Spreche Text mit Browser TTS: {Text}", text);
                await jsRuntime.InvokeVoidAsync("speakText", text);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("WebView context") ||
                                                       ex.Message.Contains("JavaScript"))
            {
                Logger.LogWarning("JavaScript-Kontext nicht verfügbar für TTS: {Message}", ex.Message);
            }
            catch (JSException ex)
            {
                Logger.LogWarning(ex, "JavaScript-Fehler beim Sprechen: {Text}", text);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Sprechen mit Browser TTS: {Text}", text);
            }
        }

        public override Task StopSpeakingAsync()
        {
            // Diese Überladung sollte für Browser TTS nicht verwendet werden
            Logger.LogError("StopSpeakingAsync ohne IJSRuntime aufgerufen - Browser TTS benötigt JavaScript-Kontext");
            throw new InvalidOperationException(
                "Browser TTS Service benötigt einen IJSRuntime Parameter. " +
                "Verwenden Sie StopSpeakingAsync(IJSRuntime jsRuntime) stattdessen.");
        }

        public override async Task StopSpeakingAsync(IJSRuntime? jsRuntime)
        {
            if (jsRuntime == null)
            {
                Logger.LogError(
                    "StopSpeakingAsync mit null IJSRuntime aufgerufen - Browser TTS benötigt JavaScript-Kontext");
                throw new ArgumentNullException(nameof(jsRuntime),
                    "Browser TTS Service benötigt einen gültigen IJSRuntime Parameter.");
            }

            var isWebViewContext = CheckWebViewContext(jsRuntime);

            if (!isWebViewContext || !_initialized)
            {
                return;
            }

            try
            {
                Logger.LogInformation("Stoppe Browser TTS");
                await jsRuntime.InvokeVoidAsync("stopSpeaking");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("WebView context") ||
                                                       ex.Message.Contains("JavaScript"))
            {
                Logger.LogWarning("JavaScript-Kontext nicht verfügbar für TTS-Stop: {Message}", ex.Message);
            }
            catch (JSException ex)
            {
                Logger.LogWarning(ex, "JavaScript-Fehler beim Stoppen von TTS");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Stoppen von Browser TTS");
            }
        }

        public override bool IsAvailable()
        {
            // Da wir das IJSRuntime nicht haben, geben wir true zurück Die tatsächliche
            // Verfügbarkeit wird zur Laufzeit geprüft
            return true;
        }

        public override Task<bool> IsAvailableAsync(IJSRuntime? jsRuntime)
        {
            if (jsRuntime == null)
            {
                return Task.FromResult(IsAvailable());
            }

            // Nur verfügbar, wenn wir in einem gültigen WebView-Kontext sind
            return Task.FromResult(CheckWebViewContext(jsRuntime));
        }

        public override string GetProviderName()
        {
            return "Browser";
        }
    }
}