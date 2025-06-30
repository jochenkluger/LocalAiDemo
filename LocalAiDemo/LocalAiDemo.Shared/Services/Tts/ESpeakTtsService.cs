using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der eSpeak-NG Library direkt für lokale Text-to-Speech verwendet Nutzt die
    /// native eSpeak-NG Library ohne externe Prozesse
    /// </summary>
    public class ESpeakTtsService : TtsServiceBase, IDisposable
    {
        private readonly string _espeakPath;
        private readonly string _libraryName;
        private bool _isInitialized = false;
        private bool _isSpeaking = false;

        public ESpeakTtsService(ILogger<ESpeakTtsService> logger)
            : base(logger)
        {
            _espeakPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            _libraryName = "libespeak-ng.dll";
            Logger.LogInformation("eSpeak TTS Service (Native Library) initialisiert");
        }

        #region P/Invoke Declarations for eSpeak-NG

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int espeak_Initialize(int output, int buflength, IntPtr path, int options);

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void espeak_Terminate();

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int espeak_SetVoiceByName([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int espeak_Synth(
            [MarshalAs(UnmanagedType.LPStr)] string text,
            int size,
            int position,
            int position_type,
            int end_position,
            int flags,
            IntPtr unique_identifier,
            IntPtr user_data);

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void espeak_Synchronize();

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int espeak_Cancel();

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int espeak_SetParameter(int parameter, int value, int relative);

        // Constants for eSpeak
        private const int ESPEAK_OUTPUT_AUDIO = 0;

        private const int ESPEAK_CHARS_AUTO = 0;
        private const int ESPEAK_POS_CHARACTER = 1;
        private const int ESPEAK_SSML = 0x10;

        // Parameters
        private const int espeakRATE = 1; // Speed (words per minute)

        private const int espeakVOLUME = 2; // Volume (0-200)
        private const int espeakPITCH = 3; // Pitch (0-100)
        private const int espeakRANGE = 4; // Pitch range (0-100)

        #endregion P/Invoke Declarations for eSpeak-NG

        /// <summary>
        /// Initialize eSpeak-NG engine for audio output
        /// </summary>
        public bool Initialize()
        {
            try
            {
                Logger.LogInformation("Initialisiere eSpeak-NG TTS Engine...");

                // Try to use local data directory first
                string? localDataPath = GetLocalESpeakDataPath();
                IntPtr dataPathPtr = IntPtr.Zero;

                if (!string.IsNullOrEmpty(localDataPath) && Directory.Exists(localDataPath))
                {
                    Logger.LogDebug("Verwende lokales eSpeak-Datenverzeichnis: {DataPath}", localDataPath);
                    dataPathPtr = Marshal.StringToHGlobalAnsi(localDataPath);
                }
                else
                {
                    Logger.LogDebug("Verwende Standard-eSpeak-Datenverzeichnis (NULL)");
                }

                try
                {
                    // Initialize eSpeak-NG for audio output
                    int result = espeak_Initialize(ESPEAK_OUTPUT_AUDIO, 0, dataPathPtr, 0);
                    if (result < 0)
                    {
                        Logger.LogError("Fehler beim Initialisieren von eSpeak-NG: {Result}", result);
                        return false;
                    }

                    // Set German voice
                    int voiceResult = espeak_SetVoiceByName("de");
                    if (voiceResult != 0)
                    {
                        Logger.LogWarning("Warnung: Deutsche Stimme konnte nicht gesetzt werden: {Result}",
                            voiceResult);
                        // Continue anyway - might still work with default voice
                    }

                    // Set speech parameters for better quality
                    espeak_SetParameter(espeakRATE, 150, 0); // 150 words per minute
                    espeak_SetParameter(espeakVOLUME, 80, 0); // 80% volume
                    espeak_SetParameter(espeakPITCH, 50, 0); // Normal pitch
                    espeak_SetParameter(espeakRANGE, 50, 0); // Normal pitch range
                }
                finally
                {
                    // Free the allocated string
                    if (dataPathPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(dataPathPtr);
                    }
                }

                _isInitialized = true;
                Logger.LogInformation("eSpeak-NG TTS Engine erfolgreich initialisiert");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Initialisieren der eSpeak-NG TTS Engine");
                return false;
            }
        }

        public override async Task SpeakAsync(string text)
        {
            if (!_isInitialized)
            {
                if (!Initialize())
                {
                    Logger.LogWarning("eSpeak-NG konnte nicht initialisiert werden");
                    return;
                }
            }

            try
            {
                Logger.LogInformation("Spreche mit eSpeak TTS: {Text}", text);

                // Stop previous speech
                await StopSpeakingAsync();

                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.LogWarning("Text für TTS ist leer");
                    return;
                }

                _isSpeaking = true;

                // Use eSpeak native synthesis
                await Task.Run(() =>
                {
                    try
                    {
                        int result = espeak_Synth(
                            text,
                            text.Length,
                            0, // position (start at beginning)
                            ESPEAK_POS_CHARACTER, // position type
                            0, // end position (0 = end of text)
                            ESPEAK_SSML, // flags (support SSML)
                            IntPtr.Zero, // unique identifier
                            IntPtr.Zero // user data
                        );

                        if (result != 0)
                        {
                            Logger.LogError("eSpeak Synthese-Fehler: {Result}", result);
                        }
                        else
                        {
                            // Wait for synthesis to complete
                            espeak_Synchronize();
                            Logger.LogDebug("eSpeak TTS erfolgreich abgeschlossen");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Fehler bei eSpeak native Synthese");
                    }
                    finally
                    {
                        _isSpeaking = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei eSpeak TTS: {Text}", text);
                _isSpeaking = false;
            }
        }

        public override Task StopSpeakingAsync()
        {
            try
            {
                if (_isSpeaking)
                {
                    Logger.LogInformation("Stoppe eSpeak TTS-Wiedergabe");
                    espeak_Cancel();
                    _isSpeaking = false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Stoppen von eSpeak TTS");
            }

            return Task.CompletedTask;
        }

        public override bool IsAvailable()
        {
            try
            {
                // Check if the library file exists in expected locations
                string[] possiblePaths =
                {
                    Path.Combine(_espeakPath, _libraryName),
                    Path.Combine(Environment.SystemDirectory, _libraryName),
                    _libraryName // Let Windows search PATH
                };

                bool libraryExists = false;
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        libraryExists = true;
                        Logger.LogDebug("eSpeak-Bibliothek gefunden: {Path}", path);
                        break;
                    }
                }

                if (!libraryExists)
                {
                    Logger.LogWarning("libespeak-ng.dll wurde in keinem der erwarteten Pfade gefunden");
                    return false;
                }

                // Try to initialize if not already done
                if (!_isInitialized)
                {
                    return Initialize();
                }

                return true; // If we reach here without DllNotFoundException, the library is accessible
            }
            catch (DllNotFoundException)
            {
                Logger.LogError("libespeak-ng.dll wurde nicht gefunden");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Überprüfen der eSpeak-Bibliothek");
                return false;
            }
        }

        public override string GetProviderName()
        {
            return "eSpeak-Native";
        }

        /// <summary>
        /// Get path to local eSpeak data directory (for standalone/test scenarios)
        /// </summary>
        private string? GetLocalESpeakDataPath()
        {
            try
            {
                // Check in current executable directory
                string? exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    string localDataPath = Path.Combine(exeDir, "TtsModels", "espeak-ng-data");
                    if (Directory.Exists(localDataPath))
                    {
                        return localDataPath;
                    }
                }

                // Check in current working directory
                string workingDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TtsModels", "espeak-ng-data");
                if (Directory.Exists(workingDataPath))
                {
                    return workingDataPath;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Cleanup eSpeak-NG resources
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                try
                {
                    espeak_Cancel(); // Stop any ongoing synthesis
                    espeak_Terminate();
                    _isInitialized = false;
                    Logger.LogInformation("eSpeak-NG TTS Engine wurde beendet");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Fehler beim Beenden der eSpeak-NG TTS Engine");
                }
            }
        }

        ~ESpeakTtsService()
        {
            Dispose();
        }
    }
}