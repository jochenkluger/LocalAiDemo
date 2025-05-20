using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Services
{
    /// <summary>
    /// Plattformspezifische Implementierungen werden in der konkreten App implementiert, 
    /// da sie plattformspezifische APIs verwenden.
    /// </summary>
    /// 
    #if WINDOWS
    using LocalAiDemo.Shared.Services;
    using Windows.Media.SpeechSynthesis;
    using Windows.Media.Playback;
    using Windows.Media.Core;
    using Windows.Storage.Streams;
    public class WindowsTtsProvider : IPlatformTts, IDisposable
    {
        private readonly ILogger<WindowsTtsProvider> _logger;
        private SpeechSynthesizer? _synthesizer;
        private MediaPlayer? _mediaPlayer;
        private bool _isAvailable;
        
        public WindowsTtsProvider(ILogger<WindowsTtsProvider> logger)
        {
            _logger = logger;
            
            try
            {
                // Initialisiere Windows Media TTS
                _synthesizer = new SpeechSynthesizer();
                _mediaPlayer = new MediaPlayer();
                
                // Versuche, deutsche Stimme zu finden
                var voices = SpeechSynthesizer.AllVoices;
                var germanVoice = voices.FirstOrDefault(v => v.Language.StartsWith("de-"));
                
                // Setze Stimme, wenn verfügbar
                if (germanVoice != null)
                {
                    _synthesizer.Voice = germanVoice;
                    _logger.LogInformation("Deutsche Stimme gefunden: {VoiceName}", germanVoice.DisplayName);
                }
                
                _isAvailable = true;
                _logger.LogInformation("Windows TTS erfolgreich initialisiert");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Initialisierung von Windows TTS");
                _isAvailable = false;
            }
        }        public async Task SpeakAsync(string text)
        {
            if (!_isAvailable || _synthesizer == null || _mediaPlayer == null)
            {
                _logger.LogWarning("Windows TTS ist nicht verfügbar");
                return;
            }
            
            try
            {
                _logger.LogInformation("Windows TTS spricht: {Text}", text);
                
                // Stoppe aktuelle Wiedergabe
                await StopSpeakingAsync();
                
                // Synthetisiere Sprache
                var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
                
                // Setze MediaSource und starte Wiedergabe
                _mediaPlayer.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in Windows TTS");
            }
        }
        
        public Task StopSpeakingAsync()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Pause();
                    _mediaPlayer.Source = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Stoppen von Windows TTS");
            }
            
            return Task.CompletedTask;
        }
        
        public bool IsAvailable()
        {
            return _isAvailable;
        }
          public void Dispose()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Dispose();
                    _mediaPlayer = null;
                }
                
                if (_synthesizer != null)
                {
                    _synthesizer.Dispose();
                    _synthesizer = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aufräumen von Windows TTS-Ressourcen");
            }
        }
    }
    #endif
    
    #if IOS || MACCATALYST
    using LocalAiDemo.Shared.Services;
    using AVFoundation;
    using Foundation;
    
    public class AppleTtsProvider : IPlatformTts, IDisposable
    {
        private readonly ILogger<AppleTtsProvider> _logger;
        private AVSpeechSynthesizer _speechSynthesizer;
        private bool _isAvailable;
        
        public AppleTtsProvider(ILogger<AppleTtsProvider> logger)
        {
            _logger = logger;
            
            try
            {
                // Initialisiere AVFoundation TTS
                _speechSynthesizer = new AVSpeechSynthesizer();
                _isAvailable = true;
                _logger.LogInformation("Apple TTS erfolgreich initialisiert");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Initialisierung von Apple TTS");
                _isAvailable = false;
            }
        }
        
        public async Task SpeakAsync(string text)
        {
            if (!_isAvailable)
            {
                _logger.LogWarning("Apple TTS ist nicht verfügbar");
                return;
            }
            
            try
            {
                _logger.LogInformation("Apple TTS spricht: {Text}", text);
                
                // Stoppe aktuelle Wiedergabe
                await StopSpeakingAsync();
                
                // Erstelle Äußerung mit deutscher Sprache
                var speechUtterance = new AVSpeechUtterance(text)
                {
                    Voice = AVSpeechSynthesisVoice.FromLanguage("de-DE"),
                    Rate = AVSpeechUtterance.DefaultSpeechRate,
                    Volume = 1.0f,
                    PitchMultiplier = 1.0f
                };
                
                // Wenn keine deutsche Stimme gefunden wurde, verwende die Standardstimme
                if (speechUtterance.Voice == null)
                {
                    _logger.LogWarning("Keine deutsche Stimme gefunden, verwende Standardstimme");
                    speechUtterance.Voice = AVSpeechSynthesisVoice.CurrentLanguageVoice;
                }
                
                // Starte Sprachsynthese
                var taskCompletionSource = new TaskCompletionSource<bool>();
                
                // Ereignisbehandlung für Ende der Sprachausgabe
                NSNotificationCenter.DefaultCenter.AddObserver(
                    AVSpeechSynthesizer.DidFinishSpeechUtteranceNotification,
                    notification => taskCompletionSource.TrySetResult(true));
                
                // Starte Sprachsynthese
                _speechSynthesizer.SpeakUtterance(speechUtterance);
                
                // Warte auf Abschluss oder Timeout nach 30 Sekunden
                await Task.WhenAny(taskCompletionSource.Task, Task.Delay(30000));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in Apple TTS");
            }
        }
        
        public Task StopSpeakingAsync()
        {
            try
            {
                if (_isAvailable && _speechSynthesizer != null)
                {
                    _logger.LogInformation("Stoppe Apple TTS");
                    
                    if (_speechSynthesizer.Speaking)
                    {
                        _speechSynthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Stoppen von Apple TTS");
            }
            
            return Task.CompletedTask;
        }
        
        public bool IsAvailable()
        {
            return _isAvailable;
        }
        
        public void Dispose()
        {
            try
            {
                // Entferne Beobachter
                NSNotificationCenter.DefaultCenter.RemoveObserver(this);
                
                // Bereinige Ressourcen
                if (_speechSynthesizer != null)
                {
                    if (_speechSynthesizer.Speaking)
                    {
                        _speechSynthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
                    }
                    
                    _speechSynthesizer.Dispose();
                    _speechSynthesizer = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aufräumen von Apple TTS-Ressourcen");
            }
        }
    }
    #endif
    
    #if ANDROID
    using LocalAiDemo.Shared.Services;
    using Android.Content;
    using Android.OS;
    using Android.Speech.Tts;
    using Java.Util;
    
    public class AndroidTtsProvider : IPlatformTts, TextToSpeech.IOnInitListener, IDisposable
    {
        private readonly ILogger<AndroidTtsProvider> _logger;
        private TextToSpeech _tts;
        private bool _isInitialized;
        private Locale _germanLocale;
        private readonly Handler _handler;
        
        public AndroidTtsProvider(ILogger<AndroidTtsProvider> logger)
        {
            _logger = logger;
            _handler = new Handler(Looper.MainLooper);
            
            try
            {
                // Initialisierung auf dem Main-Thread ausführen
                _handler.Post(() => {
                    _germanLocale = new Locale("de", "DE");
                    _tts = new TextToSpeech(Android.App.Application.Context, this);
                    _logger.LogInformation("Android TTS-Initialisierung gestartet");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Initialisierung von Android TTS");
            }
        }
        
        // Implementierung des TextToSpeech.IOnInitListener-Interfaces
        public void OnInit(OperationResult status)
        {
            if (status == OperationResult.Success)
            {
                try
                {
                    _isInitialized = true;
                    
                    // Setze deutsche Sprache, falls verfügbar
                    var langResult = _tts.SetLanguage(_germanLocale);
                    if (langResult == LanguageAvailability.Available)
                    {
                        _logger.LogInformation("Android TTS erfolgreich mit deutscher Sprache initialisiert");
                    }
                    else
                    {
                        _logger.LogWarning("Deutsche Sprache für Android TTS nicht verfügbar. Status: {Status}", langResult);
                        // Fallback auf Systemsprache
                        _tts.SetLanguage(Locale.Default);
                    }
                    
                    // Setze Sprechgeschwindigkeit und Tonhöhe
                    _tts.SetSpeechRate(1.0f);
                    _tts.SetPitch(1.0f);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler bei der Konfiguration von Android TTS");
                    _isInitialized = false;
                }
            }
            else
            {
                _logger.LogError("Android TTS-Initialisierung fehlgeschlagen. Status: {Status}", status);
                _isInitialized = false;
            }
        }
        
        public async Task SpeakAsync(string text)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Android TTS ist nicht initialisiert. Text wird nicht gesprochen: {Text}", text);
                return;
            }
            
            try
            {
                _logger.LogInformation("Android TTS spricht: {Text}", text);
                
                // Warten, bis TTS verfügbar ist
                int maxAttempts = 10;
                int attempts = 0;
                
                while (!_isInitialized && attempts < maxAttempts)
                {
                    await Task.Delay(300);
                    attempts++;
                }
                
                if (!_isInitialized)
                {
                    _logger.LogWarning("Android TTS konnte nicht initialisiert werden");
                    return;
                }
                
                // Sprechen auf dem Main-Thread ausführen
                var taskCompletionSource = new TaskCompletionSource<bool>();
                
                _handler.Post(() => {
                    try
                    {
                        // Alte Sprachausgabe beenden
                        _tts.Stop();
                        
                        // Generiere eine eindeutige Utterance-ID
                        string utteranceId = Guid.NewGuid().ToString();
                        
                        // Ab API 21 können wir Bundle-Parameter verwenden
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                        {
                            var parameters = new Bundle();
                            parameters.PutString(TextToSpeech.Engine.KeyParamUtteranceId, utteranceId);
                            _tts.Speak(text, QueueMode.Flush, parameters, utteranceId);
                        }
                        else
                        {
                            // Ältere API-Verwendung
                            Dictionary<string, string> parameters = new Dictionary<string, string>
                            {
                                { TextToSpeech.Engine.KeyParamUtteranceId, utteranceId }
                            };
                            
                            #pragma warning disable CS0618 // Veraltete API wird verwendet
                            _tts.Speak(text, QueueMode.Flush, parameters);
                            #pragma warning restore CS0618
                        }
                        
                        taskCompletionSource.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fehler beim Sprechen mit Android TTS");
                        taskCompletionSource.SetException(ex);
                    }
                });
                
                await taskCompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in Android TTS");
            }
        }
        
        public Task StopSpeakingAsync()
        {
            if (!_isInitialized)
            {
                return Task.CompletedTask;
            }
            
            try
            {
                _logger.LogInformation("Stoppe Android TTS");
                
                // Stoppen auf dem Main-Thread ausführen
                _handler.Post(() => {
                    _tts?.Stop();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Stoppen von Android TTS");
            }
            
            return Task.CompletedTask;
        }
        
        public bool IsAvailable()
        {
            return _isInitialized;
        }
        
        public void Dispose()
        {
            try
            {
                if (_tts != null)
                {
                    _tts.Stop();
                    _tts.Shutdown();
                    _tts.Dispose();
                    _tts = null;
                }
                
                _handler?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aufräumen von Android TTS-Ressourcen");
            }
        }
    }
    #endif
    
    /// <summary>
    /// Default-Implementierung für Plattformen, die nicht explizit unterstützt werden
    /// </summary>
    public class DefaultPlatformTtsProvider : LocalAiDemo.Shared.Services.IPlatformTts
    {
        private readonly ILogger<DefaultPlatformTtsProvider> _logger;
        
        public DefaultPlatformTtsProvider(ILogger<DefaultPlatformTtsProvider> logger)
        {
            _logger = logger;
            _logger.LogInformation("Default Platform TTS Provider initialisiert (keine native TTS verfügbar)");
        }
        
        public Task SpeakAsync(string text)
        {
            _logger.LogWarning("TTS nicht verfügbar auf dieser Plattform: {Text}", text);
            return Task.CompletedTask;
        }
        
        public Task StopSpeakingAsync()
        {
            return Task.CompletedTask;
        }
        
        public bool IsAvailable()
        {
            return false;  // Diese Implementierung ist nie verfügbar, sodass auf die Browser-TTS zurückgegriffen wird
        }
    }
}
