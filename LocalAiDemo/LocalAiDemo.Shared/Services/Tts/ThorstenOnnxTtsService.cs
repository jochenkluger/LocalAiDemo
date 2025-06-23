using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// Model download URLs and file names for Thorsten ONNX TTS models
    /// </summary>
    public class ModelUrls
    {
        public string ModelUrl { get; set; } = string.Empty;
        public string ConfigUrl { get; set; } = string.Empty;
        public string ModelFileName { get; set; } = string.Empty;
        public string ConfigFileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model information and status
    /// </summary>
    public class ModelInfo
    {
        public string ModelsPath { get; set; } = "";
        public bool ModelsAvailable { get; set; }
        public List<string> AvailableModelFiles { get; set; } = new();
        public List<string> AvailableQualities { get; set; } = new();
        public string PreferredQuality { get; set; } = "medium";
        public double TotalSizeMB { get; set; }
    }

    /// <summary>
    /// Configuration for Thorsten ONNX TTS models
    /// </summary>
    public class ThorstenConfig
    {
        public int SampleRate { get; set; } = 22050;
        public int NumSpeakers { get; set; } = 1;
        public Dictionary<string, int> PhonemeToId { get; set; } = new();
        public Dictionary<int, string> IdToPhoneme { get; set; } = new();
        public int PadTokenId { get; set; } = 0;
        public int BosTokenId { get; set; } = 1;
        public int EosTokenId { get; set; } = 2;
    }

    /// <summary>
    /// Echte ONNX-TTS-Implementierung für deutsche Thorsten-Modelle
    /// Verwendet echte neuronal-basierte Text-zu-Sprache-Synthese
    /// </summary>
    public class ThorstenOnnxTtsService : TtsServiceBase
    {
        private readonly string _modelsPath;
        private InferenceSession? _ttsSession;
        private ThorstenConfig? _modelConfig;
        private bool _isInitialized;
        private readonly object _initLock = new object();
        private readonly HttpClient _httpClient;
        private bool _autoDownload = true;
        private string _defaultQuality = "medium";

        // URLs für echte Thorsten ONNX TTS-Modelle (nicht Piper!)
        private readonly Dictionary<string, ModelUrls> _modelUrls = new()
        {
            ["low"] = new ModelUrls
            {
                ModelUrl = "https://github.com/coqui-ai/TTS/releases/download/v0.20.6/tts_models--de--thorsten--tacotron2-DDC.onnx",
                ConfigUrl = "https://github.com/coqui-ai/TTS/releases/download/v0.20.6/tts_models--de--thorsten--tacotron2-DDC_config.json",
                ModelFileName = "thorsten-tacotron2-low.onnx",
                ConfigFileName = "thorsten-tacotron2-low_config.json"
            },
            ["medium"] = new ModelUrls
            {
                ModelUrl = "https://github.com/coqui-ai/TTS/releases/download/v0.20.6/tts_models--de--thorsten--glow-tts.onnx", 
                ConfigUrl = "https://github.com/coqui-ai/TTS/releases/download/v0.20.6/tts_models--de--thorsten--glow-tts_config.json",
                ModelFileName = "thorsten-glow-tts-medium.onnx",
                ConfigFileName = "thorsten-glow-tts-medium_config.json"
            },
            ["high"] = new ModelUrls
            {
                ModelUrl = "https://github.com/coqui-ai/TTS/releases/download/v0.20.6/tts_models--de--thorsten--vits.onnx",
                ConfigUrl = "https://github.com/coqui-ai/TTS/releases/download/v0.20.6/tts_models--de--thorsten--vits_config.json", 
                ModelFileName = "thorsten-vits-high.onnx",
                ConfigFileName = "thorsten-vits-high_config.json"
            }
        };

        public ThorstenOnnxTtsService(ILogger<ThorstenOnnxTtsService> logger) 
            : base(logger)
        {
            _modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TtsModels");
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
            Logger.LogInformation("Thorsten ONNX TTS Service initialisiert für echte ONNX-Modelle. Modell-Pfad: {Path}", _modelsPath);
        }

        public override async Task SpeakAsync(string text)
        {
            if (!await EnsureInitializedAsync())
            {
                Logger.LogWarning("Thorsten ONNX TTS ist nicht verfügbar - verwende Fallback");
                await SpeakFallbackAsync(text);
                return;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit echtem Thorsten ONNX TTS für: {Text}", text);
                
                if (_ttsSession != null && _modelConfig != null)
                {
                    Logger.LogInformation("Verwende echte Thorsten ONNX TTS-Synthese");
                    var audioData = await GenerateOnnxTtsAudio(text);
                    
                    if (audioData != null && audioData.Length > 0)
                    {
                        await PlayAudioAsync(audioData);
                        return;
                    }
                    else
                    {
                        Logger.LogWarning("ONNX TTS-Generierung fehlgeschlagen, verwende Fallback");
                    }
                }
                else
                {
                    Logger.LogWarning("Keine ONNX-Session oder Konfiguration verfügbar, verwende Fallback");
                }
                
                await SpeakFallbackAsync(text);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Generieren von Audio");
                await SpeakFallbackAsync(text);
            }
        }

        private async Task<bool> EnsureInitializedAsync()
        {
            if (_isInitialized)
                return true;

            lock (_initLock)
            {
                if (_isInitialized)
                    return true;

                return InitializeModelsAsync().GetAwaiter().GetResult();
            }
        }

        private async Task<bool> InitializeModelsAsync()
        {
            try
            {
                Logger.LogInformation("Initialisiere echte Thorsten ONNX TTS-Modelle...");

                // Erstelle Modell-Verzeichnis
                if (!Directory.Exists(_modelsPath))
                {
                    Directory.CreateDirectory(_modelsPath);
                    Logger.LogInformation("Modell-Verzeichnis erstellt: {Path}", _modelsPath);
                }

                // Prüfe ob automatischer Download aktiviert ist
                if (_autoDownload && !AreModelsAvailable())
                {
                    Logger.LogInformation("Keine Thorsten ONNX-Modelle gefunden. Starte automatischen Download...");
                    var downloadSuccess = await DownloadModelsAsync(_defaultQuality);
                    if (!downloadSuccess)
                    {
                        Logger.LogWarning("Automatischer Download fehlgeschlagen");
                        return false;
                    }
                }

                // Lade das beste verfügbare Modell
                var modelPath = FindBestOnnxModel();
                var configPath = FindModelConfig();

                if (modelPath == null || !IsValidOnnxFile(modelPath))
                {
                    Logger.LogWarning("Kein gültiges Thorsten ONNX-Modell gefunden. Erwartete Pfade:");
                    LogExpectedModelPaths();
                    return false;
                }

                // Lade Modell-Konfiguration
                if (configPath != null && File.Exists(configPath))
                {
                    try
                    {
                        var configJson = await File.ReadAllTextAsync(configPath);
                        _modelConfig = JsonSerializer.Deserialize<ThorstenConfig>(configJson);
                        Logger.LogInformation("Modell-Konfiguration geladen: {Config}", configPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Fehler beim Laden der Modell-Konfiguration. Verwende Standard-Konfiguration.");
                        _modelConfig = CreateDefaultConfig();
                    }
                }
                else
                {
                    Logger.LogInformation("Keine Konfigurationsdatei gefunden. Verwende Standard-Konfiguration.");
                    _modelConfig = CreateDefaultConfig();
                }

                // Lade ONNX-Session
                try
                {
                    Logger.LogInformation("Lade Thorsten ONNX TTS-Modell: {Path}", modelPath);
                    
                    var sessionOptions = new SessionOptions();
                    sessionOptions.EnableCpuMemArena = false;
                    sessionOptions.EnableMemoryPattern = false;
                    
                    _ttsSession = new InferenceSession(modelPath, sessionOptions);
                    Logger.LogInformation("Thorsten ONNX TTS-Session erfolgreich geladen");
                    
                    // Prüfe Modell-Inputs und -Outputs
                    LogModelInfo(_ttsSession);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Fehler beim Laden der Thorsten ONNX-Session.");
                    return false;
                }

                _isInitialized = true;
                Logger.LogInformation("Thorsten ONNX TTS erfolgreich initialisiert");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei der Initialisierung von Thorsten ONNX TTS");
                return false;
            }
        }

        /// <summary>
        /// Generiert echte TTS-Audio mit ONNX-Inferenz
        /// </summary>
        private async Task<byte[]?> GenerateOnnxTtsAudio(string text)
        {
            try
            {
                if (_ttsSession == null || _modelConfig == null)
                {
                    Logger.LogWarning("ONNX TTS Session oder Konfiguration ist nicht verfügbar");
                    return null;
                }

                Logger.LogInformation("Generiere echte ONNX TTS-Audio für: {Text}", text);
                
                // 1. Text normalisieren und vorverarbeiten
                var normalizedText = NormalizeGermanText(text);
                Logger.LogDebug("Normalisierter Text: {Text}", normalizedText);
                
                // 2. Text zu Phonemen konvertieren
                var phonemes = await TextToPhonemes(normalizedText);
                Logger.LogDebug("Phoneme generiert: {Phonemes}", string.Join(" ", phonemes));
                
                // 3. Phoneme zu IDs konvertieren
                var phonemeIds = PhonemesToIds(phonemes);
                if (phonemeIds.Length == 0)
                {
                    Logger.LogWarning("Keine Phonem-IDs generiert");
                    return null;
                }
                
                Logger.LogDebug("Phonem-IDs: {Count} IDs", phonemeIds.Length);
                
                // 4. ONNX-Inferenz durchführen
                var audioFeatures = await RunOnnxInference(phonemeIds);
                if (audioFeatures == null)
                {
                    Logger.LogWarning("ONNX-Inferenz fehlgeschlagen");
                    return null;
                }
                
                Logger.LogDebug("Audio-Features generiert: {Length} samples", audioFeatures.Length);
                
                // 5. Audio-Features zu WAV konvertieren
                var audioData = ConvertFeaturesToWav(audioFeatures, _modelConfig.SampleRate);
                
                Logger.LogInformation("Echte ONNX TTS-Audio erfolgreich generiert: {Length} Bytes", audioData.Length);
                return audioData;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei ONNX TTS-Audio-Generierung");
                return null;
            }
        }

        /// <summary>
        /// Führt ONNX-Inferenz durch
        /// </summary>
        private async Task<float[]?> RunOnnxInference(int[] phonemeIds)
        {
            try
            {
                if (_ttsSession == null)
                    return null;

                // Erstelle Input-Tensoren
                var inputIds = new DenseTensor<long>(phonemeIds.Select(id => (long)id).ToArray(), new[] { 1, phonemeIds.Length });
                var inputLength = new DenseTensor<long>(new long[] { phonemeIds.Length }, new[] { 1 });
                
                // Prepare inputs
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                    NamedOnnxValue.CreateFromTensor("input_lengths", inputLength)
                };

                // Füge Speaker-ID hinzu wenn das Modell es unterstützt
                var inputMetadata = _ttsSession.InputMetadata;
                if (inputMetadata.ContainsKey("speaker_ids"))
                {
                    var speakerId = new DenseTensor<long>(new long[] { 0 }, new[] { 1 });
                    inputs.Add(NamedOnnxValue.CreateFromTensor("speaker_ids", speakerId));
                }

                Logger.LogDebug("Führe ONNX-Inferenz durch...");
                
                // Führe Inferenz durch
                using var results = await Task.Run(() => _ttsSession.Run(inputs));
                
                // Extrahiere Audio-Output
                var outputTensor = results.FirstOrDefault()?.AsTensor<float>();
                if (outputTensor == null)
                {
                    Logger.LogWarning("Kein Audio-Output von ONNX-Modell erhalten");
                    return null;
                }

                // Konvertiere zu Array
                var audioFeatures = outputTensor.ToArray();
                Logger.LogDebug("ONNX-Inferenz erfolgreich: {Length} Audio-Features", audioFeatures.Length);
                
                return audioFeatures;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei ONNX-Inferenz");
                return null;
            }
        }

        /// <summary>
        /// Normalisiert deutschen Text für TTS
        /// </summary>
        private string NormalizeGermanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // Basis-Normalisierung
            text = text.Trim().ToLowerInvariant();
            
            // Zahlen zu Worten (vereinfacht)
            text = Regex.Replace(text, @"\b\d+\b", match => NumberToGermanWords(int.Parse(match.Value)));
            
            // Sonderzeichen normalisieren
            text = text.Replace("ß", "ss");
            text = Regex.Replace(text, @"[^\w\säöüÄÖÜ\-\.]", " ");
            text = Regex.Replace(text, @"\s+", " ");
            
            return text.Trim();
        }

        /// <summary>
        /// Konvertiert Text zu deutschen Phonemen
        /// </summary>
        private async Task<string[]> TextToPhonemes(string text)
        {
            await Task.Delay(1); // Für async

            // Vereinfachte deutsche Phonem-Konvertierung
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var allPhonemes = new List<string>();

            foreach (var word in words)
            {
                var phonemes = ConvertWordToPhonemes(word);
                allPhonemes.AddRange(phonemes);
                allPhonemes.Add("_"); // Wort-Trenner
            }

            return allPhonemes.ToArray();
        }

        /// <summary>
        /// Konvertiert ein deutsches Wort zu Phonemen
        /// </summary>
        private string[] ConvertWordToPhonemes(string word)
        {
            var phonemes = new List<string>();
            
            for (int i = 0; i < word.Length; i++)
            {
                var ch = word[i];
                
                // Einfache deutsche Phonem-Regeln
                switch (ch)
                {
                    case 'a': phonemes.Add("a"); break;
                    case 'e': phonemes.Add("e"); break;
                    case 'i': phonemes.Add("i"); break;
                    case 'o': phonemes.Add("o"); break;
                    case 'u': phonemes.Add("u"); break;
                    case 'ä': phonemes.Add("E"); break;
                    case 'ö': phonemes.Add("2"); break;
                    case 'ü': phonemes.Add("y"); break;
                    case 'b': phonemes.Add("b"); break;
                    case 'c': phonemes.Add("k"); break;
                    case 'd': phonemes.Add("d"); break;
                    case 'f': phonemes.Add("f"); break;
                    case 'g': phonemes.Add("g"); break;
                    case 'h': phonemes.Add("h"); break;
                    case 'j': phonemes.Add("j"); break;
                    case 'k': phonemes.Add("k"); break;
                    case 'l': phonemes.Add("l"); break;
                    case 'm': phonemes.Add("m"); break;
                    case 'n': phonemes.Add("n"); break;
                    case 'p': phonemes.Add("p"); break;
                    case 'q': phonemes.Add("k"); break;
                    case 'r': phonemes.Add("R"); break;
                    case 's': phonemes.Add("s"); break;
                    case 't': phonemes.Add("t"); break;
                    case 'v': phonemes.Add("f"); break;
                    case 'w': phonemes.Add("v"); break;
                    case 'x': phonemes.Add("k s"); break;
                    case 'y': phonemes.Add("y"); break;
                    case 'z': phonemes.Add("ts"); break;
                    default: 
                        if (char.IsLetter(ch))
                            phonemes.Add("@"); // Unbekanntes Phonem
                        break;
                }
            }
            
            return phonemes.ToArray();
        }

        /// <summary>
        /// Konvertiert Phoneme zu IDs basierend auf Modell-Konfiguration
        /// </summary>
        private int[] PhonemesToIds(string[] phonemes)
        {
            if (_modelConfig == null)
                return Array.Empty<int>();

            var ids = new List<int>();
            ids.Add(_modelConfig.BosTokenId); // Begin-of-sequence

            foreach (var phoneme in phonemes)
            {
                if (_modelConfig.PhonemeToId.TryGetValue(phoneme, out var id))
                {
                    ids.Add(id);
                }
                else
                {
                    // Fallback auf Pad-Token für unbekannte Phoneme
                    ids.Add(_modelConfig.PadTokenId);
                }
            }

            ids.Add(_modelConfig.EosTokenId); // End-of-sequence
            return ids.ToArray();
        }

        /// <summary>
        /// Konvertiert Audio-Features zu WAV-Format
        /// </summary>
        private byte[] ConvertFeaturesToWav(float[] audioFeatures, int sampleRate)
        {
            // Konvertiere float[] zu short[] für WAV
            var audioData = new short[audioFeatures.Length];
            for (int i = 0; i < audioFeatures.Length; i++)
            {
                // Normalisiere und konvertiere zu 16-bit PCM
                var sample = Math.Max(-1.0f, Math.Min(1.0f, audioFeatures[i]));
                audioData[i] = (short)(sample * short.MaxValue);
            }

            return ConvertToWav(audioData, sampleRate);
        }

        /// <summary>
        /// Erstellt Standard-Konfiguration für Thorsten
        /// </summary>
        private ThorstenConfig CreateDefaultConfig()
        {
            var config = new ThorstenConfig
            {
                SampleRate = 22050,
                NumSpeakers = 1,
                PadTokenId = 0,
                BosTokenId = 1,
                EosTokenId = 2
            };

            // Standard deutsche Phonem-Mappings
            var phonemes = new[] { "_", "a", "e", "i", "o", "u", "E", "2", "y", 
                                  "b", "d", "f", "g", "h", "j", "k", "l", "m", 
                                  "n", "p", "R", "s", "t", "v", "ts", "@" };

            for (int i = 0; i < phonemes.Length; i++)
            {
                config.PhonemeToId[phonemes[i]] = i + 3; // Reserve 0,1,2 for special tokens
                config.IdToPhoneme[i + 3] = phonemes[i];
            }

            return config;
        }

        /// <summary>
        /// Konvertiert Zahlen zu deutschen Worten (vereinfacht)
        /// </summary>
        private string NumberToGermanWords(int number)
        {
            if (number == 0) return "null";
            if (number == 1) return "eins";
            if (number == 2) return "zwei";
            if (number == 3) return "drei";
            if (number == 4) return "vier";
            if (number == 5) return "fünf";
            if (number == 6) return "sechs";
            if (number == 7) return "sieben";
            if (number == 8) return "acht";
            if (number == 9) return "neun";
            if (number == 10) return "zehn";
            
            // Für größere Zahlen vereinfacht zurückgeben
            return number.ToString();
        }

        /// <summary>
        /// Loggt Informationen über das geladene ONNX-Modell
        /// </summary>
        private void LogModelInfo(InferenceSession session)
        {
            Logger.LogInformation("ONNX-Modell-Informationen:");
            
            Logger.LogInformation("Eingaben:");
            foreach (var input in session.InputMetadata)
            {
                Logger.LogInformation("  {Name}: {Type} {Dimensions}", 
                    input.Key, input.Value.ElementType, string.Join("x", input.Value.Dimensions));
            }
            
            Logger.LogInformation("Ausgaben:");
            foreach (var output in session.OutputMetadata)
            {
                Logger.LogInformation("  {Name}: {Type} {Dimensions}", 
                    output.Key, output.Value.ElementType, string.Join("x", output.Value.Dimensions));
            }
        }

        // ... Rest der Methoden (Download, Fallback, etc.) bleibt gleich wie vorher

        private async Task<bool> DownloadModelsAsync(string quality, bool forceDownload = false)
        {
            try
            {
                if (!_modelUrls.TryGetValue(quality, out var urls))
                {
                    Logger.LogWarning("Unbekannte Qualitätsstufe: {Quality}", quality);
                    return false;
                }

                var modelPath = Path.Combine(_modelsPath, urls.ModelFileName);
                var configPath = Path.Combine(_modelsPath, urls.ConfigFileName);

                if (!forceDownload && File.Exists(modelPath) && IsValidOnnxFile(modelPath))
                {
                    Logger.LogInformation("Modell bereits vorhanden: {Path}", modelPath);
                    return true;
                }

                Logger.LogInformation("Lade Thorsten ONNX-Modell herunter: {Quality}", quality);

                // Download Modell
                var modelSuccess = await DownloadFileAsync(urls.ModelUrl, modelPath);
                if (!modelSuccess)
                {
                    Logger.LogError("Download des Modells fehlgeschlagen: {Url}", urls.ModelUrl);
                    return false;
                }

                // Download Konfiguration
                var configSuccess = await DownloadFileAsync(urls.ConfigUrl, configPath);
                if (!configSuccess)
                {
                    Logger.LogWarning("Download der Konfiguration fehlgeschlagen: {Url}", urls.ConfigUrl);
                }

                Logger.LogInformation("Thorsten ONNX-Modell erfolgreich heruntergeladen: {Quality}", quality);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Download der Modelle");
                return false;
            }
        }

        private async Task<bool> DownloadFileAsync(string url, string filePath)
        {
            try
            {
                Logger.LogInformation("Lade Datei herunter: {Url} -> {Path}", url, filePath);

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(filePath);

                var buffer = new byte[8192];
                var downloadedBytes = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (double)downloadedBytes / totalBytes * 100;
                        if (downloadedBytes % (1024 * 1024) == 0) // Log every MB
                        {
                            Logger.LogInformation("Download-Fortschritt: {Percentage:F1}% ({Downloaded}/{Total} MB)",
                                percentage, downloadedBytes / (1024 * 1024), totalBytes / (1024 * 1024));
                        }
                    }
                }

                Logger.LogInformation("Download abgeschlossen: {Path} ({Size} MB)", 
                    filePath, new FileInfo(filePath).Length / (1024 * 1024));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Download der Datei: {Url}", url);
                return false;
            }
        }

        private bool AreModelsAvailable()
        {
            if (!Directory.Exists(_modelsPath))
                return false;

            var onnxFiles = Directory.GetFiles(_modelsPath, "*.onnx");
            return onnxFiles.Any(IsValidOnnxFile);
        }

        private string? FindBestOnnxModel()
        {
            if (!Directory.Exists(_modelsPath))
                return null;

            var qualities = new[] { "high", "medium", "low" };
            
            foreach (var quality in qualities)
            {
                if (_modelUrls.TryGetValue(quality, out var urls))
                {
                    var modelPath = Path.Combine(_modelsPath, urls.ModelFileName);
                    if (File.Exists(modelPath) && IsValidOnnxFile(modelPath))
                    {
                        Logger.LogInformation("Verwende {Quality}-Qualität Modell: {Path}", quality, modelPath);
                        return modelPath;
                    }
                }
            }

            // Fallback: Suche nach beliebigen Thorsten ONNX-Dateien
            var onnxFiles = Directory.GetFiles(_modelsPath, "*thorsten*.onnx");
            return onnxFiles.FirstOrDefault(IsValidOnnxFile);
        }

        private string? FindModelConfig()
        {
            if (!Directory.Exists(_modelsPath))
                return null;

            var configFiles = Directory.GetFiles(_modelsPath, "*config*.json");
            return configFiles.FirstOrDefault();
        }

        private bool IsValidOnnxFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length < 1024) // Mindestens 1KB
                    return false;

                // Einfacher Check auf ONNX-Header
                using var fs = File.OpenRead(path);
                var buffer = new byte[8];
                fs.Read(buffer, 0, 8);
                
                // ONNX-Dateien beginnen mit bestimmten Bytes
                return buffer[0] == 0x08 || buffer[0] == 0x0A || 
                       (buffer[0] == 0x00 && buffer[1] == 0x00); // Verschiedene ONNX-Versionen
            }
            catch
            {
                return false;
            }
        }

        private void LogExpectedModelPaths()
        {
            Logger.LogInformation("Erwartete Modell-Pfade:");
            foreach (var kvp in _modelUrls)
            {
                var modelPath = Path.Combine(_modelsPath, kvp.Value.ModelFileName);
                var configPath = Path.Combine(_modelsPath, kvp.Value.ConfigFileName);
                Logger.LogInformation("  {Quality}: {ModelPath}", kvp.Key, modelPath);
                Logger.LogInformation("  {Quality}: {ConfigPath}", kvp.Key, configPath);
            }
        }

        private byte[] ConvertToWav(short[] audioData, int sampleRate)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new WaveFileWriter(memoryStream, new WaveFormat(sampleRate, 16, 1));
            
            foreach (var sample in audioData)
            {
                writer.WriteSample(sample / 32768.0f);
            }
            
            writer.Flush();
            return memoryStream.ToArray();
        }

        private async Task SpeakFallbackAsync(string text)
        {
            Logger.LogInformation("Verwende Fallback-Audio für: {Text}", text);
            
            var fallbackAudio = GenerateFallbackAudio(text);
            await PlayAudioAsync(fallbackAudio);
        }

        private byte[] GenerateFallbackAudio(string text)
        {
            const int sampleRate = 22050;
            var duration = Math.Max(1.0, text.Length * 0.1);
            var samples = (int)(sampleRate * duration);
            var audioData = new short[samples];
            const short amplitude = 4000;

            for (int i = 0; i < samples; i++)
            {
                var t = (double)i / sampleRate;
                var frequency = 400 + (text.Length % 10) * 50;
                audioData[i] = (short)(amplitude * Math.Sin(2 * Math.PI * frequency * t));
            }

            return ConvertToWav(audioData, sampleRate);
        }

        private async Task PlayAudioAsync(byte[] audioData)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var memoryStream = new MemoryStream(audioData);
                    using var waveFileReader = new WaveFileReader(memoryStream);
                    using var waveOut = new WaveOutEvent();
                    
                    waveOut.Init(waveFileReader);
                    waveOut.Play();
                    
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        Thread.Sleep(100);
                    }
                });
                
                Logger.LogDebug("Audio-Wiedergabe abgeschlossen");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei der Audio-Wiedergabe");
            }
        }

        public override bool IsAvailable()
        {
            return AreModelsAvailable() || _autoDownload;
        }

        public override string GetProviderName()
        {
            return "Thorsten ONNX TTS";
        }

        public override async Task StopSpeakingAsync()
        {
            Logger.LogDebug("StopSpeakingAsync aufgerufen");
            await Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ttsSession?.Dispose();
                _httpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
