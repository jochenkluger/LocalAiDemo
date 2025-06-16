using Android.Content;
using Android.OS;
using Android.Speech.Tts;
using Java.Util;
using LocalAiDemo.Shared.Services.Tts;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;

namespace LocalAiDemo.Platforms.Android
{
    public class PlatformTtsService : Java.Lang.Object, IPlatformTts, TextToSpeech.IOnInitListener, IDisposable
    {
        private readonly ILogger<PlatformTtsService> _logger;
        private TextToSpeech _tts;
        private bool _isInitialized;
        private Java.Util.Locale _germanLocale;
        private readonly Handler _handler;

        public PlatformTtsService(ILogger<PlatformTtsService> logger)
        {
            _logger = logger;
            _handler = new Handler(Looper.MainLooper);

            try
            {
                // Initialize TTS on the main thread
                _handler.Post(() =>
                {
                    _germanLocale = new Java.Util.Locale("de", "DE");
                    _tts = new TextToSpeech(global::Android.App.Application.Context, this);
                    _logger.LogInformation("Android TTS initialization started");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Android TTS");
            }
        }

        // Implementation of TextToSpeech.IOnInitListener interface
        public void OnInit(OperationResult status)
        {
            if (status == OperationResult.Success)
            {
                try
                {
                    _isInitialized = true;

                    // Set German language if available
                    var langResult = _tts.SetLanguage(_germanLocale);
                    if (langResult == LanguageAvailableResult.Available)
                    {
                        _logger.LogInformation("Android TTS successfully initialized with German language");
                    }
                    else
                    {
                        _logger.LogWarning("German language for Android TTS not available. Status: {Status}",
                            langResult);
                        // Fallback to system language
                        _tts.SetLanguage(Java.Util.Locale.Default);
                    }

                    // Set speech rate and pitch
                    _tts.SetSpeechRate(1.0f);
                    _tts.SetPitch(1.0f);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error configuring Android TTS");
                    _isInitialized = false;
                }
            }
            else
            {
                _logger.LogError("Android TTS initialization failed. Status: {Status}", status);
                _isInitialized = false;
            }
        }

        public async Task SpeakAsync(string text)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Android TTS is not initialized. Text will not be spoken: {Text}", text);
                return;
            }

            try
            {
                _logger.LogInformation("Android TTS speaking: {Text}", text);

                // Wait until TTS is available
                int maxAttempts = 10;
                int attempts = 0;

                while (!_isInitialized && attempts < maxAttempts)
                {
                    await Task.Delay(300);
                    attempts++;
                }

                if (!_isInitialized)
                {
                    _logger.LogWarning("Android TTS could not be initialized");
                    return;
                }

                // Execute speech on the main thread
                var taskCompletionSource = new TaskCompletionSource<bool>();

                _handler.Post(() =>
                {
                    try
                    {
                        // Stop any current speech
                        _tts.Stop();

                        // Generate a unique utterance ID
                        string utteranceId = Guid.NewGuid().ToString();

                        // For API level 21 and higher, we can use Bundle parameters
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                        {
                            var parameters = new Bundle();
                            parameters.PutString(TextToSpeech.Engine.KeyParamUtteranceId, utteranceId);
                            _tts.Speak(text, QueueMode.Flush, parameters, utteranceId);
                        }
                        else
                        {
                            // Older API usage
                            Dictionary<string, string> parameters = new Dictionary<string, string>
                            {
                                { TextToSpeech.Engine.KeyParamUtteranceId, utteranceId }
                            };

#pragma warning disable CS0618 // Type or member is obsolete
                            _tts.Speak(text, QueueMode.Flush, parameters);
#pragma warning restore CS0618
                        }

                        taskCompletionSource.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error speaking with Android TTS");
                        taskCompletionSource.SetException(ex);
                    }
                });

                await taskCompletionSource.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Android TTS");
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
                _logger.LogInformation("Stopping Android TTS");

                // Execute stop on the main thread
                _handler.Post(() => { _tts?.Stop(); });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Android TTS");
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
                _logger.LogError(ex, "Error cleaning up Android TTS resources");
            }
        }
    }
}