using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace LocalAiDemo.Shared.Services.Stt
{
    /// <summary>
    /// Implementierung des SST-Services mit Whisper.NET für lokale Spracherkennung
    /// </summary>
    public class WhisperSttService : SstServiceBase
    {
        private readonly WhisperService _whisperService;
        private bool _isRecording = false;
        private bool _initialized = false;

        public WhisperSttService(WhisperService whisperService, ILogger<WhisperSttService> logger)
            : base(logger)
        {
            _whisperService = whisperService;
        }

        private bool CheckWebViewContext(IJSRuntime jsRuntime)
        {
            try
            {
                // Check if we're in a valid JavaScript context
                var runtimeType = jsRuntime.GetType().Name;
                Logger.LogDebug("JSRuntime type: {RuntimeType}", runtimeType);

                return runtimeType.Contains("WebView") ||
                       runtimeType.Contains("WebAssembly") ||
                       runtimeType.Contains("Remote");
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error detecting WebView context");
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
                    Logger.LogInformation(
                        "Initializing Whisper recording script..."); // Test if JavaScript calls are possible
                    await jsRuntime.InvokeVoidAsync("eval", "console.log('Whisper: JS context available');");

                    // Instead of injecting JavaScript, use the existing whisper-recording.js file
                    // The JavaScript file should already be loaded via the index.html script tag

                    // Add a small delay to ensure scripts are loaded
                    await Task.Delay(500);

                    // Check if the whisper-recording.js file loaded properly
                    await jsRuntime.InvokeVoidAsync("eval",
                        "console.log('Checking if whisper-recording.js loaded:', typeof window.isWhisperRecordingSupported);"); // Test if the whisper recording functions are available
                    try
                    {
                        // First check if the function exists
                        await jsRuntime.InvokeVoidAsync("eval",
                            "console.log('Testing function availability:', typeof window.isWhisperRecordingSupported);");

                        var isSupported = await jsRuntime.InvokeAsync<bool>("isWhisperRecordingSupported");
                        Logger.LogInformation("Whisper recording support check: {IsSupported}", isSupported);
                    }
                    catch (JSException ex)
                    {
                        Logger.LogError(ex,
                            "Whisper recording functions not available. Trying to load script dynamically.");

                        // Try to load the script dynamically
                        try
                        {
                            await jsRuntime.InvokeVoidAsync("eval", @"
                                if (typeof window.isWhisperRecordingSupported === 'undefined') {
                                    console.log('Attempting to load whisper-recording.js dynamically');
                                    var script = document.createElement('script');
                                    script.src = '_content/LocalAiDemo.Shared/whisper-recording.js';
                                    script.onload = function() { console.log('whisper-recording.js loaded dynamically'); };
                                    script.onerror = function() { console.log('Failed to load whisper-recording.js'); };
                                    document.head.appendChild(script);
                                }
                            ");

                            // Wait a bit for the script to load
                            await Task.Delay(1000);

                            // Try again
                            var isSupported = await jsRuntime.InvokeAsync<bool>("isWhisperRecordingSupported");
                            Logger.LogInformation("Whisper recording support check after dynamic load: {IsSupported}",
                                isSupported);
                        }
                        catch (Exception dynamicEx)
                        {
                            Logger.LogError(dynamicEx, "Failed to load whisper-recording.js dynamically");

                            // Try to check what functions are available
                            try
                            {
                                await jsRuntime.InvokeVoidAsync("eval", @"
                                    console.log('Available Whisper functions:');
                                    console.log('initializeWhisperRecording:', typeof window.initializeWhisperRecording);
                                    console.log('isWhisperRecordingSupported:', typeof window.isWhisperRecordingSupported);
                                    console.log('startWhisperRecording:', typeof window.startWhisperRecording);
                                    console.log('stopWhisperRecording:', typeof window.stopWhisperRecording);
                                ");
                            }
                            catch (Exception debugEx)
                            {
                                Logger.LogError(debugEx, "Error during function availability debugging");
                            }

                            _initialized = false;
                            return;
                        }
                    }

                    _initialized = true;
                    Logger.LogInformation(
                        "Whisper recording script initialized successfully (using whisper-recording.js)");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("WebView context") ||
                                                           ex.Message.Contains("JavaScript"))
                {
                    Logger.LogWarning("JavaScript context not available for Whisper: {Message}", ex.Message);
                    _initialized = false;
                }
                catch (JSException ex)
                {
                    Logger.LogWarning(ex, "JavaScript error during Whisper initialization: {Message}", ex.Message);
                    _initialized = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error initializing Whisper recording script");
                    _initialized = false;
                }
            }
            else if (!isWebViewContext)
            {
                Logger.LogDebug("Whisper initialization skipped - no WebView context");
            }
        }

        public override async Task<bool> InitializeSpeechRecognitionAsync<T>(IJSRuntime jsRuntime,
            DotNetObjectReference<T> dotNetObjectReference) where T : class
        {
            try
            {
                Logger.LogInformation("Initializing Whisper speech recognition");

                var isWebViewContext = CheckWebViewContext(jsRuntime);

                if (!isWebViewContext)
                {
                    Logger.LogDebug("Whisper initialization skipped - no WebView context available");
                    return false;
                }

                // Ensure the script is loaded dynamically
                await EnsureInitializedAsync(jsRuntime);

                if (!_initialized)
                {
                    Logger.LogWarning("Whisper script not initialized properly");
                    return false;
                }

                // Initialize Whisper model
                await _whisperService.Initialize();

                // Initialize browser audio recording capabilities with dynamically loaded script
                var initialized =
                    await jsRuntime.InvokeAsync<bool>("initializeWhisperRecording", dotNetObjectReference);

                if (initialized)
                {
                    Logger.LogInformation("Whisper speech recognition initialized successfully");
                    return true;
                }
                else
                {
                    Logger.LogError("Failed to initialize Whisper recording");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing Whisper speech recognition");
                return false;
            }
        }

        public override async Task StartSpeechRecognitionAsync(IJSRuntime jsRuntime)
        {
            Logger.LogInformation("StartSpeechRecognitionAsync called - _isRecording: {IsRecording}", _isRecording);

            if (_isRecording)
            {
                Logger.LogWarning("Speech recognition is already running");
                return;
            }

            var isWebViewContext = CheckWebViewContext(jsRuntime);
            Logger.LogInformation("WebView context check: {IsWebViewContext}", isWebViewContext);

            if (!isWebViewContext)
            {
                Logger.LogDebug("Whisper start skipped - no WebView context available");
                return;
            }

            await EnsureInitializedAsync(jsRuntime);
            Logger.LogInformation("After EnsureInitializedAsync - _initialized: {IsInitialized}", _initialized);

            if (!_initialized)
            {
                Logger.LogWarning("Whisper script not initialized. Cannot start recording.");
                return;
            }

            try
            {
                Logger.LogInformation("Starting Whisper speech recognition - calling startWhisperRecording");
                _isRecording = true;

                // Start audio recording in browser
                await jsRuntime.InvokeVoidAsync("startWhisperRecording");
                Logger.LogInformation("startWhisperRecording JavaScript call completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error starting Whisper speech recognition");
                _isRecording = false;
            }
        }

        public override async Task StopSpeechRecognitionAsync(IJSRuntime jsRuntime)
        {
            if (!_isRecording)
            {
                Logger.LogWarning("Speech recognition is not running");
                return;
            }

            var isWebViewContext = CheckWebViewContext(jsRuntime);

            if (!isWebViewContext || !_initialized)
            {
                return;
            }

            try
            {
                Logger.LogInformation("Stopping Whisper speech recognition");

                // Stop audio recording and get the recorded data
                await jsRuntime.InvokeVoidAsync("stopWhisperRecording");
                _isRecording = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error stopping Whisper speech recognition");
                _isRecording = false;
            }
        }

        public override async Task<bool> IsAvailableAsync(IJSRuntime jsRuntime)
        {
            try
            {
                var isWebViewContext = CheckWebViewContext(jsRuntime);

                if (!isWebViewContext)
                {
                    Logger.LogDebug("Whisper availability check skipped - no WebView context");
                    return false;
                }

                // Ensure the script is loaded
                await EnsureInitializedAsync(jsRuntime);

                if (!_initialized)
                {
                    Logger.LogDebug("Whisper script not initialized");
                    return false;
                }

                // Check if browser supports media recording and Whisper is available Add retry
                // mechanism for script loading timing issues
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var isAvailable = await jsRuntime.InvokeAsync<bool>("isWhisperRecordingSupported");
                        Logger.LogInformation("Whisper recording availability: {IsAvailable}", isAvailable);
                        return isAvailable;
                    }
                    catch (JSException ex) when (ex.Message.Contains("isWhisperRecordingSupported") && i < 2)
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
                Logger.LogError(ex, "Error checking Whisper availability");
                return false;
            }
        }

        public override string GetProviderName()
        {
            return "Whisper";
        }

        /// <summary>
        /// Called from JavaScript when audio recording is completed This method will be invoked by
        /// the browser with the recorded audio data
        /// </summary>
        [JSInvokable]
        public async Task<string> ProcessAudioData(byte[] audioData)
        {
            try
            {
                Logger.LogInformation("Processing audio data with Whisper (Length: {Length} bytes)", audioData.Length);

                // Transcribe the audio using Whisper
                var transcription = await _whisperService.TranscribeAudioData(audioData);

                Logger.LogInformation("Whisper transcription completed: {Text}", transcription);
                return transcription;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing audio data with Whisper");
                return string.Empty;
            }
        }

        /// <summary>
        /// Called from JavaScript when audio recording is completed - accepts base64 encoded audio
        /// data This method will be invoked by the browser with the recorded audio data as base64 string
        /// </summary>
        [JSInvokable]
        public async Task<string> ProcessAudioDataBase64(string base64AudioData)
        {
            try
            {
                Logger.LogInformation("Processing base64 audio data with Whisper (Length: {Length} chars)",
                    base64AudioData.Length);

                // Convert base64 string back to byte array
                var audioData = Convert.FromBase64String(base64AudioData);
                Logger.LogInformation("Converted to byte array (Length: {Length} bytes)", audioData.Length);

                // Transcribe the audio using Whisper
                var transcription = await _whisperService.TranscribeAudioData(audioData);

                Logger.LogInformation("Whisper transcription completed: {Text}", transcription);
                return transcription;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing base64 audio data with Whisper");
                return string.Empty;
            }
        }
    }
}