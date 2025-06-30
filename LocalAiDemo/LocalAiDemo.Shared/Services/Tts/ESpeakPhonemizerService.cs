using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// Native eSpeak-NG integration for phonemization without external process calls
    /// </summary>
    public class ESpeakPhonemizerService : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly string _espeakPath;
        private readonly string _libraryName;
        private bool _isInitialized = false;

        public ESpeakPhonemizerService(ILogger? logger = null)
        {
            _logger = logger;
            _espeakPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ??
                          AppDomain.CurrentDomain.BaseDirectory;
            _libraryName = "libespeak-ng.dll";
        }

        #region P/Invoke Declarations for eSpeak-NG

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int espeak_Initialize(int output, int buflength, IntPtr path, int options);

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void espeak_Terminate();

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int
            espeak_SetVoiceByName(
                [MarshalAs(UnmanagedType.LPStr)] string name); // Correct function signature based on C++ definition

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr espeak_TextToPhonemes(
            ref IntPtr textptr,
            int textmode,
            int phonememode);

        // Delegate for phoneme callback (alternative approach)
        public delegate int EspeakSynthCallback(IntPtr wav, int numsamples, IntPtr events);

        [DllImport("libespeak-ng.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void espeak_SetSynthCallback(EspeakSynthCallback callback);

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

        // Helper method for safe string marshaling
        private static IntPtr MarshalStringToPtr(string text)
        {
            return Marshal.StringToHGlobalAnsi(text);
        }

        private static void FreeStringPtr(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        } // Constants for eSpeak

        private const int ESPEAK_OUTPUT_PHONEMES = 2;
        private const int ESPEAK_OUTPUT_AUDIO = 0;
        private const int ESPEAK_CHARS_AUTO = 0;
        private const int ESPEAK_CHARS_UTF8 = 0x01;
        private const int ESPEAK_PHONEMES_MBROLA = 0x01;
        private const int ESPEAK_PHONEMES_IPA = 0x02;
        private const int ESPEAK_PHONEMES_TIES = 0x04;
        private const int ESPEAK_SSML = 0x10;

        #endregion P/Invoke Declarations for eSpeak-NG

        /// <summary>
        /// Initialize eSpeak-NG engine
        /// </summary>
        public bool Initialize()
        {
            try
            {
                _logger?.LogInformation("Initialisiere eSpeak-NG Phonemizer...");

                // Try to use local data directory first (for standalone/test scenarios)
                string? localDataPath = GetLocalESpeakDataPath();
                IntPtr dataPathPtr = IntPtr.Zero;

                if (!string.IsNullOrEmpty(localDataPath) && Directory.Exists(localDataPath))
                {
                    _logger?.LogDebug("Verwende lokales eSpeak-Datenverzeichnis: {DataPath}", localDataPath);
                    dataPathPtr = Marshal.StringToHGlobalAnsi(localDataPath);
                }
                else
                {
                    _logger?.LogDebug("Verwende Standard-eSpeak-Datenverzeichnis (NULL)");
                }

                try
                {
                    // Initialize eSpeak-NG for phoneme output
                    int result = espeak_Initialize(ESPEAK_OUTPUT_PHONEMES, 0, dataPathPtr, 0);
                    if (result < 0)
                    {
                        _logger?.LogError("Fehler beim Initialisieren von eSpeak-NG: {Result}", result);
                        return false;
                    }

                    // Set German voice
                    int voiceResult = espeak_SetVoiceByName("de");
                    if (voiceResult != 0)
                    {
                        _logger?.LogWarning("Warnung: Deutsche Stimme konnte nicht gesetzt werden: {Result}",
                            voiceResult);
                        // Continue anyway - might still work with default voice
                    }
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
                _logger?.LogInformation("eSpeak-NG Phonemizer erfolgreich initialisiert");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Initialisieren des eSpeak-NG Phonemizers");
                return false;
            }
        }

        /// <summary>
        /// Convert text to eSpeak phonemes
        /// </summary>
        public string[]? TextToPhonemes(string text)
        {
            if (!_isInitialized)
            {
                if (!Initialize())
                {
                    _logger?.LogWarning("eSpeak-NG konnte nicht initialisiert werden");
                    return null;
                }
            }

            try
            {
                _logger?.LogDebug("Konvertiere Text zu Phonemen: '{Text}'", text);

                // Ensure text is not null or empty
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger?.LogWarning("Text für Phonemisierung ist leer");
                    return Array.Empty<string>();
                }

                // Validate that the library is properly loaded before calling
                if (!IsLibraryLoaded())
                {
                    _logger?.LogError("eSpeak-NG Bibliothek ist nicht geladen");
                    return null;
                }

                // Split text into smaller chunks for better eSpeak processing
                var chunks = SplitTextIntoChunks(text);
                var allPhonemes = new List<string>();

                foreach (var chunk in chunks)
                {
                    if (string.IsNullOrWhiteSpace(chunk)) continue;

                    var chunkPhonemes = ProcessTextChunk(chunk);
                    if (chunkPhonemes != null && chunkPhonemes.Length > 0)
                    {
                        allPhonemes.AddRange(chunkPhonemes);
                    }
                }

                _logger?.LogDebug("Gesamt geparste Phoneme: {Phonemes}", string.Join(" ", allPhonemes));
                return allPhonemes.ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler bei der Phonemisierung von '{Text}'", text);
                return null;
            }
        }

        /// <summary>
        /// Split text into smaller chunks for better eSpeak processing
        /// </summary>
        private string[] SplitTextIntoChunks(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            // Split by sentence boundaries and commas for better phonemization
            var chunks = text.Split(new char[] { '.', '!', '?', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            // Trim and clean chunks
            return chunks.Select(chunk => chunk.Trim()).Where(chunk => !string.IsNullOrWhiteSpace(chunk)).ToArray();
        }

        /// <summary>
        /// Process a single text chunk through eSpeak
        /// </summary>
        private string[]? ProcessTextChunk(string chunk)
        {
            IntPtr textPtr = IntPtr.Zero;
            IntPtr phonemePtr = IntPtr.Zero;

            try
            {
                textPtr = Marshal.StringToHGlobalAnsi(chunk);

                // eSpeak expects a pointer to the text pointer and modifies it
                IntPtr textPtrRef = textPtr;
                // Use ASCII phonemes instead of IPA for better compatibility with ONNX models
                phonemePtr = espeak_TextToPhonemes(ref textPtrRef, ESPEAK_CHARS_AUTO, 0); // 0 = ASCII phonemes

                if (phonemePtr == IntPtr.Zero)
                {
                    _logger?.LogWarning("eSpeak-NG gab keine Phoneme zurück für: '{Chunk}'", chunk);
                    return Array.Empty<string>();
                }

                // Convert result to string
                string phonemeString = Marshal.PtrToStringAnsi(phonemePtr) ?? "";
                _logger?.LogDebug("eSpeak-NG Phoneme für '{Chunk}': '{Phonemes}'", chunk, phonemeString);

                // Parse phonemes (eSpeak uses different delimiters)
                var phonemes = ParseESpeakPhonemes(phonemeString);
                _logger?.LogDebug("Geparste Phoneme für '{Chunk}': {Phonemes}", chunk, string.Join(" ", phonemes));
                return phonemes;
            }
            finally
            {
                // Clean up allocated memory
                if (textPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(textPtr);
                // Note: eSpeak manages the returned phoneme string memory, don't free it
            }
        }

        /// <summary>
        /// Check if the eSpeak library is loaded and accessible
        /// </summary>
        private bool IsLibraryLoaded()
        {
            try
            {
                // Try to check if the library file exists in expected locations
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
                        _logger?.LogDebug("eSpeak-Bibliothek gefunden: {Path}", path);
                        break;
                    }
                }

                if (!libraryExists)
                {
                    _logger?.LogWarning("libespeak-ng.dll wurde in keinem der erwarteten Pfade gefunden");
                }

                return true; // If we reach here without DllNotFoundException, the library is accessible
            }
            catch (DllNotFoundException)
            {
                _logger?.LogError("libespeak-ng.dll wurde nicht gefunden");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Überprüfen der eSpeak-Bibliothek");
                return false;
            }
        }

        /// <summary>
        /// Parse eSpeak phoneme string into individual phonemes
        /// </summary>
        private string[] ParseESpeakPhonemes(string phonemeString)
        {
            if (string.IsNullOrWhiteSpace(phonemeString))
                return Array.Empty<string>();

            var phonemes = new List<string>();

            // Add sentence start marker
            phonemes.Add("^");

            // Clean the phoneme string - eSpeak ASCII phonemes use specific delimiters
            string cleanPhonemes = phonemeString
                .Replace("'", "") // Remove all stress markers
                .Replace("_:", "") // Remove stress markers
                .Replace("_", " ") // Word boundaries become spaces
                .Replace(",", " ") // Commas become spaces
                .Replace("  ", " ") // Double spaces become single
                .Trim();

            if (!string.IsNullOrEmpty(cleanPhonemes))
            {
                // Split by spaces and filter empty entries
                var parts = cleanPhonemes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    string cleanPart = part.Trim();
                    if (!string.IsNullOrWhiteSpace(cleanPart))
                    {
                        // Split individual phonemes if they're concatenated
                        var individualPhonemes = SplitConcatenatedPhonemes(cleanPart);
                        phonemes.AddRange(individualPhonemes);
                    }
                }
            }

            // Add sentence end marker
            phonemes.Add("$");

            return phonemes.ToArray();
        }

        /// <summary>
        /// Split concatenated phonemes into individual phonemes
        /// </summary>
        private string[] SplitConcatenatedPhonemes(string concatenatedPhoneme)
        {
            var result = new List<string>();

            // eSpeak ASCII phonemes are usually single characters or simple combinations Split
            // multi-character phoneme strings into individual phonemes
            var currentPhoneme = "";

            for (int i = 0; i < concatenatedPhoneme.Length; i++)
            {
                char c = concatenatedPhoneme[i];

                // Check for common multi-character phonemes
                if (i < concatenatedPhoneme.Length - 1)
                {
                    string twoChar = concatenatedPhoneme.Substring(i, 2);
                    if (IsCommonDigraph(twoChar))
                    {
                        if (!string.IsNullOrEmpty(currentPhoneme))
                        {
                            result.Add(currentPhoneme);
                            currentPhoneme = "";
                        }

                        result.Add(twoChar);
                        i++; // Skip next character
                        continue;
                    }
                }

                // For single characters, add them as individual phonemes
                if (char.IsLetter(c) || c == '@' || c == ':')
                {
                    result.Add(c.ToString());
                }
            }

            // Add any remaining phoneme
            if (!string.IsNullOrEmpty(currentPhoneme))
            {
                result.Add(currentPhoneme);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Check if a two-character string is a common digraph in eSpeak
        /// </summary>
        private bool IsCommonDigraph(string twoChar)
        {
            // Common German digraphs in eSpeak ASCII notation
            return twoChar switch
            {
                "OY" => true, // German oi/eu sound
                "aI" => true, // German ai/ei sound
                "aU" => true, // German au sound
                "tS" => true, // German ch sound
                "dZ" => true, // German j sound
                "pf" => true, // German pf sound
                _ => false
            };
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
                    espeak_Terminate();
                    _isInitialized = false;
                    _logger?.LogInformation("eSpeak-NG Phonemizer wurde beendet");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Fehler beim Beenden des eSpeak-NG Phonemizers");
                }
            }
        }

        ~ESpeakPhonemizerService()
        {
            Dispose();
        }
    }
}