using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// Model download URLs and file names
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
    /// Verbesserte ONNX-TTS-Implementierung für deutsche Modelle wie Thorsten Unterstützt echte
    /// ONNX-TTS-Modelle mit Phonem-zu-Audio-Konvertierung und automatischen Download
    /// </summary>
    public class ThorstenOnnxTtsService : TtsServiceBase
    {
        private readonly string _modelsPath;
        private InferenceSession? _ttsSession;
        private Dictionary<string, int>? _phonemeToId;
        private Dictionary<string, object>? _modelConfig;
        private bool _isInitialized;
        private bool _loggedAvailablePhonemes;
        private readonly object _initLock = new object();
        private readonly HttpClient _httpClient;
        private bool _autoDownload = true;
        private string _defaultQuality = "medium";
        private ESpeakPhonemizerService? _phonemizer;
        private readonly ThorstenOnnxSettings _settings;

        // Model download URLs für verschiedene Qualitätsstufen
        private readonly Dictionary<string, ModelUrls> _modelUrls = new()
        {
            ["low"] = new ModelUrls
            {
                ModelUrl =
                    "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/low/de_DE-thorsten-low.onnx",
                ConfigUrl =
                    "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/low/de_DE-thorsten-low.onnx.json",
                ModelFileName = "de_DE-thorsten-low.onnx",
                ConfigFileName = "de_DE-thorsten-low.onnx.json"
            },
            ["medium"] = new ModelUrls
            {
                ModelUrl =
                    "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx",
                ConfigUrl =
                    "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx.json",
                ModelFileName = "de_DE-thorsten-medium.onnx",
                ConfigFileName = "de_DE-thorsten-medium.onnx.json"
            },
            ["high"] = new ModelUrls
            {
                ModelUrl =
                    "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/high/de_DE-thorsten-high.onnx",
                ConfigUrl =
                    "https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/high/de_DE-thorsten-high.onnx.json",
                ModelFileName = "de_DE-thorsten-high.onnx",
                ConfigFileName = "de_DE-thorsten-high.onnx.json"
            }
        };

        public ThorstenOnnxTtsService(ILogger<ThorstenOnnxTtsService> logger, ThorstenOnnxSettings? settings = null)
            : base(logger)
        {
            _settings = settings ?? new ThorstenOnnxSettings();
            _modelsPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ??
                Environment.CurrentDirectory,
                _settings.ModelsPath);
            _autoDownload = _settings.AutoDownload;
            _defaultQuality = _settings.DefaultQuality;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.DownloadTimeout);

            _phonemizer = new ESpeakPhonemizerService(logger);

            Logger.LogInformation("Thorsten ONNX TTS Service initialisiert. Modell-Pfad: {Path}", _modelsPath);
        }

        public override async Task SpeakAsync(string text)
        {
            if (!await EnsureInitializedAsync())
            {
                Logger.LogWarning("Thorsten ONNX TTS ist nicht verfügbar");
                throw new Exception("Thorsten ONNX TTS ist nicht verfügbar");
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit Thorsten ONNX TTS für: {Text}", text);

                // Verwende echte ONNX-Inferenz statt Fallback
                var audioData = await GenerateAudioWithOnnxAsync(text);

                if (audioData != null && audioData.Length > 0)
                {
                    await PlayAudioAsync(audioData);
                }
                else
                {
                    Logger.LogWarning("Keine Audio-Daten von ONNX-Modell generiert");
                    throw new Exception("Keine Audio-Daten von ONNX-Modell generiert");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei Thorsten ONNX TTS: {Text}", text);
                throw new Exception($"Fehler bei Thorsten ONNX TTS: {text}", ex);
            }
        }

        /// <summary>
        /// Generates audio data from text (public API for testing)
        /// </summary>
        public async Task<byte[]?> GenerateAudioAsync(string text)
        {
            if (!await EnsureInitializedAsync())
            {
                Logger.LogWarning("Thorsten ONNX TTS ist nicht initialisiert");
                return null;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit Thorsten ONNX TTS für: {Text}", text);
                return await GenerateAudioWithOnnxAsync(text);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Generieren von Audio: {Text}", text);
                return null;
            }
        }

        private async Task<bool> EnsureInitializedAsync()
        {
            if (_isInitialized)
                return true;

            return await InitializeModelsAsync();
        }

        private async Task<bool> InitializeModelsAsync()
        {
            try
            {
                Logger.LogInformation("Initialisiere Thorsten ONNX-Modelle...");

                // Prüfe ob automatischer Download aktiviert ist
                if (_autoDownload && !AreModelsAvailable())
                {
                    Logger.LogInformation("Keine Modelle gefunden. Starte automatischen Download...");
                    var downloadSuccess = await DownloadModelsAsync(_defaultQuality);
                    if (!downloadSuccess)
                    {
                        Logger.LogWarning("Automatischer Download fehlgeschlagen");
                        return false;
                    }
                } // Prüfe verfügbare Modelle

                var ttsModelPath = FindBestTtsModel();

                if (ttsModelPath == null || !IsValidOnnxFile(ttsModelPath))
                {
                    Logger.LogWarning("Kein gültiges TTS-Modell gefunden. Erwartete Pfade:");
                    LogExpectedModelPaths();
                    return false;
                }

                // Load model configuration
                var configPath = Path.ChangeExtension(ttsModelPath, ".onnx.json");
                if (File.Exists(configPath))
                {
                    await LoadModelConfigurationAsync(configPath);
                }
                else
                {
                    Logger.LogWarning("Keine Modell-Konfiguration gefunden: {Path}", configPath);
                } // Load phoneme mapping - required for operation

                if (_modelConfig != null)
                {
                    LoadPhonemeMapping();
                }
                else
                {
                    Logger.LogError(
                        "Modell-Konfiguration ist erforderlich für Phonem-Mapping. Kein Fallback verfügbar.");
                    return false;
                }

                if (ttsModelPath == null || !IsValidOnnxFile(ttsModelPath))
                {
                    Logger.LogWarning("Kein gültiges TTS-Modell gefunden. Erwartete Pfade:");
                    LogExpectedModelPaths();
                    return false;
                }

                // Versuche ONNX Session zu erstellen und Debug-Informationen zu sammeln
                try
                {
                    _ttsSession = new InferenceSession(ttsModelPath);
                    LogModelInputsAndOutputs(_ttsSession);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Fehler beim Laden des ONNX-Modells: {Path}", ttsModelPath);
                    return false;
                }

                _isInitialized = true;
                Logger.LogInformation("Thorsten ONNX TTS erfolgreich initialisiert");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Initialisieren von Thorsten ONNX TTS");
                return false;
            }
        }

        private void LogModelInputsAndOutputs(InferenceSession session)
        {
            Logger.LogInformation("ONNX Model Inputs:");
            foreach (var input in session.InputMetadata)
            {
                Logger.LogInformation("  {Name}: {Type} {Dimensions}",
                    input.Key, input.Value.ElementType, string.Join("x", input.Value.Dimensions));
            }

            Logger.LogInformation("ONNX Model Outputs:");
            foreach (var output in session.OutputMetadata)
            {
                Logger.LogInformation("  {Name}: {Type} {Dimensions}",
                    output.Key, output.Value.ElementType, string.Join("x", output.Value.Dimensions));
            }
        }

        /// <summary>
        /// Führt ONNX-Inferenz durch mit robustem Input-Mapping für Piper TTS Modelle
        /// </summary>
        private async Task<float[]?> RunOnnxInference(int[] phonemeIds)
        {
            try
            {
                if (_ttsSession == null)
                    return null;

                // Debug: Log verfügbare Input-Namen
                var inputMetadata = _ttsSession.InputMetadata;
                Logger.LogDebug("Verfügbare ONNX Model Inputs: {Inputs}",
                    string.Join(", ", inputMetadata.Keys));

                // Log input metadata details for debugging
                foreach (var input in inputMetadata)
                {
                    Logger.LogDebug("Input '{Name}': Type={Type}, Dimensions=[{Dims}]",
                        input.Key, input.Value.ElementType, string.Join(",", input.Value.Dimensions));
                }

                var inputs = new List<NamedOnnxValue>(); // Handle Piper TTS model inputs
                if (inputMetadata.ContainsKey("input") || inputMetadata.ContainsKey("input_ids"))
                {
                    // Standard Piper input
                    var inputName = inputMetadata.ContainsKey("input") ? "input" : "input_ids";
                    var inputTensor = new DenseTensor<long>(phonemeIds.Select(id => (long)id).ToArray(),
                        new[] { 1, phonemeIds.Length });
                    inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, inputTensor));
                    Logger.LogDebug("Added input '{Name}' with shape [1, {Length}]", inputName, phonemeIds.Length);
                }
                else
                {
                    // Fallback: use first available input
                    var firstInput = inputMetadata.Keys.FirstOrDefault();
                    if (firstInput != null)
                    {
                        var inputTensor = new DenseTensor<long>(phonemeIds.Select(id => (long)id).ToArray(),
                            new[] { 1, phonemeIds.Length });
                        inputs.Add(NamedOnnxValue.CreateFromTensor(firstInput, inputTensor));
                        Logger.LogDebug("Added fallback input '{Name}' with shape [1, {Length}]", firstInput,
                            phonemeIds.Length);
                    }
                }

                // Add input_lengths if required
                if (inputMetadata.ContainsKey("input_lengths"))
                {
                    var inputLengthsTensor = new DenseTensor<long>(new long[] { phonemeIds.Length }, new[] { 1 });
                    inputs.Add(NamedOnnxValue.CreateFromTensor("input_lengths", inputLengthsTensor));
                    Logger.LogDebug("Added input_lengths with value: {Length}", phonemeIds.Length);
                } // Check for additional required inputs

                foreach (var inputName in inputMetadata.Keys)
                {
                    switch (inputName.ToLower())
                    {
                        case "scales":
                            // Get scales from model config or use defaults
                            var noiseScale = GetConfigValue("noise_scale", 0.667f);
                            var lengthScale = GetConfigValue("length_scale", 1.0f); // Normal speech speed
                            var noiseW = GetConfigValue("noise_w", 0.8f);

                            var scalesTensor = new DenseTensor<float>(new float[] { noiseScale, lengthScale, noiseW },
                                new[] { 3 });
                            inputs.Add(NamedOnnxValue.CreateFromTensor("scales", scalesTensor));
                            Logger.LogDebug(
                                "Added scales input: noise_scale={NoiseScale}, length_scale={LengthScale}, noise_w={NoiseW}",
                                noiseScale, lengthScale, noiseW);
                            break;

                        case "speaker_id":
                        case "speaker_ids":
                            // Speaker ID for multi-speaker models
                            var speakerTensor = new DenseTensor<long>(new long[] { 0 }, new[] { 1 });
                            inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, speakerTensor));
                            Logger.LogDebug("Added speaker input '{Name}'", inputName);
                            break;

                        case "length_scale":
                            // Length scale for speech speed
                            var lengthScaleTensor =
                                new DenseTensor<float>(new float[] { 1.0f }, new[] { 1 }); // Normal speech speed
                            inputs.Add(NamedOnnxValue.CreateFromTensor("length_scale", lengthScaleTensor));
                            Logger.LogDebug("Added length_scale input");
                            break;

                        case "noise_scale":
                            // Noise scale for audio variation
                            var noiseScaleTensor = new DenseTensor<float>(new float[] { 0.667f }, new[] { 1 });
                            inputs.Add(NamedOnnxValue.CreateFromTensor("noise_scale", noiseScaleTensor));
                            Logger.LogDebug("Added noise_scale input");
                            break;

                        case "noise_w":
                            // Noise width for audio variation
                            var noiseWTensor = new DenseTensor<float>(new float[] { 0.8f }, new[] { 1 });
                            inputs.Add(NamedOnnxValue.CreateFromTensor("noise_w", noiseWTensor));
                            Logger.LogDebug("Added noise_w input");
                            break;
                    }
                }

                // Remove duplicates (in case we added the same input twice)
                var uniqueInputs = inputs.GroupBy(i => i.Name).Select(g => g.First()).ToList();

                Logger.LogDebug("Führe ONNX-Inferenz durch mit {Count} Inputs: {Names}",
                    uniqueInputs.Count, string.Join(", ", uniqueInputs.Select(i => i.Name)));

                // Führe Inferenz durch
                using var results = await Task.Run(() => _ttsSession.Run(uniqueInputs));

                // Extrahiere Audio-Output
                var outputTensor = results.FirstOrDefault()?.AsTensor<float>();
                if (outputTensor == null)
                {
                    Logger.LogWarning("Kein Audio-Output von ONNX-Modell erhalten");
                    Logger.LogDebug("Available outputs: {Outputs}", string.Join(", ", results.Select(r => r.Name)));
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
                Logger.LogWarning("ONNX-Inferenz fehlgeschlagen");
                return null;
            }
        }

        /// <summary>
        /// Downloads Thorsten TTS models automatically
        /// </summary>
        public async Task<bool> DownloadModelsAsync(string quality = "medium", bool forceDownload = false)
        {
            try
            {
                if (!_modelUrls.ContainsKey(quality))
                {
                    Logger.LogError("Unbekannte Qualitätsstufe: {Quality}. Verfügbar: {Available}",
                        quality, string.Join(", ", _modelUrls.Keys));
                    return false;
                }

                var modelUrls = _modelUrls[quality];
                var modelPath = Path.Combine(_modelsPath, modelUrls.ModelFileName);
                var configPath = Path.Combine(_modelsPath, modelUrls.ConfigFileName);

                // Check if models already exist
                if (!forceDownload && File.Exists(modelPath) && File.Exists(configPath))
                {
                    Logger.LogInformation("Modelle bereits vorhanden: {Quality}", quality);
                    return true;
                }

                // Create models directory
                Directory.CreateDirectory(_modelsPath);

                Logger.LogInformation("Beginne Download von Thorsten {Quality} Modellen...", quality);

                // Download model file
                Logger.LogInformation("Lade Modell herunter: {FileName}", modelUrls.ModelFileName);
                await DownloadFileAsync(modelUrls.ModelUrl, modelPath);

                // Download config file
                Logger.LogInformation("Lade Konfiguration herunter: {FileName}", modelUrls.ConfigFileName);
                await DownloadFileAsync(modelUrls.ConfigUrl, configPath);

                Logger.LogInformation("Download erfolgreich abgeschlossen für Qualität: {Quality}", quality);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Download der Modelle (Qualität: {Quality})", quality);
                return false;
            }
        }

        /// <summary>
        /// Downloads a file from URL to local path with progress logging
        /// </summary>
        private async Task DownloadFileAsync(string url, string localPath)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                Logger.LogInformation("Lade {FileName} herunter ({Size:F2} MB)...",
                    Path.GetFileName(localPath), totalBytes / 1024.0 / 1024.0);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                var totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (double)totalRead / totalBytes * 100;
                        if (totalRead % (1024 * 1024) == 0) // Log every MB
                        {
                            Logger.LogDebug("Download-Fortschritt: {Percentage:F1}% ({Read:F2}/{Total:F2} MB)",
                                percentage, totalRead / 1024.0 / 1024.0, totalBytes / 1024.0 / 1024.0);
                        }
                    }
                }

                Logger.LogInformation("Download abgeschlossen: {FileName} ({Size:F2} MB)",
                    Path.GetFileName(localPath), totalRead / 1024.0 / 1024.0);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Download von {Url} nach {Path}", url, localPath);

                // Clean up partial download
                if (File.Exists(localPath))
                {
                    try
                    {
                        File.Delete(localPath);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Gets information about available models
        /// </summary>
        public async Task<ModelInfo> GetModelInfoAsync()
        {
            var info = new ModelInfo
            {
                ModelsPath = _modelsPath,
                ModelsAvailable = AreModelsAvailable(),
                AvailableQualities = _modelUrls.Keys.ToList(),
                PreferredQuality = _defaultQuality,
                TotalSizeMB = 0
            };

            if (info.ModelsAvailable)
            {
                var modelFiles = Directory.GetFiles(_modelsPath, "*.onnx");
                foreach (var file in modelFiles)
                {
                    var fileInfo = new FileInfo(file);
                    info.TotalSizeMB += fileInfo.Length / 1024.0 / 1024.0;
                    info.AvailableModelFiles.Add(Path.GetFileName(file));
                }
            }

            await Task.CompletedTask;
            return info;
        }

        /// <summary>
        /// Tests the model download functionality
        /// </summary>
        public async Task<bool> TestModelDownloadAsync()
        {
            Logger.LogInformation("Teste Thorsten ONNX Model-Download...");

            var modelInfo = await GetModelInfoAsync();
            Logger.LogInformation("Aktuelle Modell-Information:");
            Logger.LogInformation("- Modelle verfügbar: {Available}", modelInfo.ModelsAvailable);
            Logger.LogInformation("- Verfügbare Qualitäten: {Qualities}",
                string.Join(", ", modelInfo.AvailableQualities));
            Logger.LogInformation("- Gesamtgröße: {Size:F2} MB", modelInfo.TotalSizeMB);

            if (!modelInfo.ModelsAvailable)
            {
                Logger.LogInformation("Keine Modelle gefunden. Starte automatischen Download...");

                if (await DownloadModelsAsync(_defaultQuality))
                {
                    var newModelInfo = await GetModelInfoAsync();
                    Logger.LogInformation("Download-Test erfolgreich:");
                    Logger.LogInformation("- Modelle verfügbar: {Available}", newModelInfo.ModelsAvailable);
                    Logger.LogInformation("- Neue Gesamtgröße: {Size:F2} MB", newModelInfo.TotalSizeMB);
                    return true;
                }
                else
                {
                    Logger.LogError("Download-Test fehlgeschlagen");
                    return false;
                }
            }
            else
            {
                Logger.LogInformation("Modelle bereits vorhanden - Test übersprungen");
                return true;
            }
        }

        /// <summary>
        /// Test method to check model download and initialization
        /// </summary>
        public async Task<string> TestModelStatusAsync()
        {
            var status = new StringBuilder();
            status.AppendLine("=== Thorsten ONNX Model Status ===");

            // Check if models directory exists
            status.AppendLine($"Models Path: {_modelsPath}");
            status.AppendLine($"Directory exists: {Directory.Exists(_modelsPath)}");

            if (Directory.Exists(_modelsPath))
            {
                var files = Directory.GetFiles(_modelsPath, "*.*", SearchOption.AllDirectories);
                status.AppendLine($"Files in directory: {files.Length}");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    status.AppendLine($"  - {Path.GetFileName(file)} ({fileInfo.Length:N0} bytes)");
                }
            }

            // Check model availability
            status.AppendLine($"Models Available: {AreModelsAvailable()}");

            // Check best TTS model
            var bestModel = FindBestTtsModel();
            status.AppendLine($"Best TTS Model: {bestModel ?? "None found"}");

            if (bestModel != null)
            {
                status.AppendLine($"Model is valid: {IsValidOnnxFile(bestModel)}");
            }

            // Check initialization status
            status.AppendLine($"Is Initialized: {_isInitialized}");
            status.AppendLine($"TTS Session: {(_ttsSession != null ? "Loaded" : "Not loaded")}");

            // Try to download models if not available
            if (!AreModelsAvailable())
            {
                status.AppendLine("\nAttempting to download models...");
                try
                {
                    var downloadResult = await DownloadModelsAsync(_defaultQuality);
                    status.AppendLine($"Download result: {downloadResult}");

                    if (downloadResult)
                    {
                        status.AppendLine("Re-checking after download:");
                        status.AppendLine($"Models Available: {AreModelsAvailable()}");
                        var newBestModel = FindBestTtsModel();
                        status.AppendLine($"Best TTS Model: {newBestModel ?? "None found"}");
                    }
                }
                catch (Exception ex)
                {
                    status.AppendLine($"Download failed: {ex.Message}");
                }
            }

            return status.ToString();
        }

        private bool AreModelsAvailable()
        {
            if (!Directory.Exists(_modelsPath))
                return false;

            var onnxFiles = Directory.GetFiles(_modelsPath, "*.onnx");
            return onnxFiles.Any(f => Path.GetFileName(f).Contains("thorsten"));
        }

        private string? FindBestTtsModel()
        {
            if (!Directory.Exists(_modelsPath))
                return null;

            // Suche nach Thorsten-Modellen (bevorzuge neuere Versionen)
            var patterns = new[]
            {
                "*thorsten*.onnx",
                "*de_DE*.onnx",
                "*german*.onnx",
                "*.onnx"
            };

            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(_modelsPath, pattern, SearchOption.AllDirectories)
                    .Where(f => !f.Contains("vocoder") && !f.Contains("hifigan"))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();

                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            return null;
        }

        private string? FindVocoderModel()
        {
            var vocoderPatterns = new[]
            {
                "*vocoder*.onnx",
                "*hifigan*.onnx",
                "*waveglow*.onnx"
            };

            foreach (var pattern in vocoderPatterns)
            {
                var files = Directory.GetFiles(_modelsPath, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return files[0];
            }

            return null;
        }

        private void LogExpectedModelPaths()
        {
            Logger.LogInformation("Erwartete Modell-Pfade:");
            Logger.LogInformation("- {Path}\\*thorsten*.onnx", _modelsPath);
        }

        private bool IsValidOnnxFile(string filePath)
        {
            try
            {
                // Check if file is large enough to be an ONNX model (real models are typically MB
                // in size)
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 1024) // Less than 1KB is definitely not a real model
                {
                    Logger.LogDebug("File {Path} is too small to be a real ONNX model ({Size} bytes)", filePath,
                        fileInfo.Length);
                    return false;
                }

                // Try to read the first few bytes to check for ONNX magic bytes
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[16];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead < 4)
                {
                    Logger.LogDebug("File {Path} too small to read header", filePath);
                    return false;
                }

                // ONNX files should start with specific magic bytes Real validation would check for
                // protobuf format, but this is a simple check
                if (buffer[0] == '#' || buffer[0] == 'T') // Our placeholder files start with # or text
                {
                    Logger.LogDebug("File {Path} appears to be a text placeholder, not a real ONNX model", filePath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error validating ONNX file {Path}", filePath);
                return false;
            }
        }

        private double GetFrequencyForPhoneme(string phoneme)
        {
            if (string.IsNullOrEmpty(phoneme))
                return 440; // Standard A

            var ch = phoneme.ToLower()[0];
            return ch switch
            {
                'a' => 440, // A
                'e' => 523, // C
                'i' => 659, // E
                'o' => 784, // G
                'u' => 880, // A'
                'ä' => 466, // Bb
                'ö' => 554, // C#
                'ü' => 698, // F
                _ => 220 + (ch % 12) * 55 // Verschiedene Frequenzen basierend auf ASCII-Wert
            };
        }

        private byte[] ConvertToWav(short[] audioData, int sampleRate)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // WAV Header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + audioData.Length * 2);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(audioData.Length * 2);

            // Audio Data
            foreach (var sample in audioData)
            {
                writer.Write(sample);
            }

            return memoryStream.ToArray();
        }

        private async Task PlayAudioAsync(byte[] audioData)
        {
            var tempFile = Path.GetTempFileName() + ".wav";
            try
            {
                await File.WriteAllBytesAsync(tempFile, audioData);
                await PlayAudioFileExternal(tempFile);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }

        private async Task PlayAudioFileExternal(string audioFile)
        {
            try
            {
                // Nur auf Desktop-Plattformen verfügbar
                if (!OperatingSystem.IsBrowser())
                {
                    // Prüfe ob wir auf einer Plattform sind, die Process.Start unterstützt
                    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        var playCommand = OperatingSystem.IsLinux() ? "aplay" :
                            OperatingSystem.IsMacOS() ? "afplay" : "powershell";
                        var arguments = OperatingSystem.IsWindows()
                            ? $"-c \"(New-Object Media.SoundPlayer '{audioFile}').PlaySync()\""
                            : audioFile;

                        var processInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = playCommand,
                            Arguments = arguments,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var process = System.Diagnostics.Process.Start(processInfo);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                    else
                    {
                        Logger.LogWarning("Audio-Wiedergabe auf dieser Plattform nicht unterstützt");
                    }
                }
                else
                {
                    Logger.LogWarning("Audio-Wiedergabe im Browser nicht unterstützt");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei externer Audio-Wiedergabe");
            }
        }

        /// <summary>
        /// Generiert Audio mit dem echten ONNX-Modell
        /// </summary>
        private async Task<byte[]?> GenerateAudioWithOnnxAsync(string text)
        {
            try
            {
                if (_ttsSession == null)
                {
                    Logger.LogError("ONNX TTS Session ist nicht initialisiert");
                    return null;
                }

                // 1. Text preprocessing
                var preprocessedText = PreprocessText(text);
                Logger.LogDebug("Preprocessed text: {Text}", preprocessedText);

                // 2. Text zu Phonemen konvertieren
                var phonemes = ConvertTextToPhonemes(preprocessedText);
                Logger.LogDebug("Generated {Count} phonemes: {Phonemes}", phonemes.Length,
                    string.Join(" ", phonemes.Take(10)));

                // 3. Phoneme zu IDs konvertieren
                var phonemeIds = ConvertPhonemesToIds(phonemes);
                Logger.LogDebug("Generated {Count} phoneme IDs", phonemeIds.Length);

                // 4. ONNX Inferenz durchführen
                var audioFeatures = await RunOnnxInference(phonemeIds);
                if (audioFeatures == null || audioFeatures.Length == 0)
                {
                    Logger.LogWarning("ONNX Inferenz lieferte keine Audio-Features");
                    return null;
                }

                // 5. Audio-Features zu WAV konvertieren
                var audioData = ConvertFeaturesToWav(audioFeatures);
                Logger.LogInformation("Generated {Size:F2} KB audio data from ONNX model", audioData.Length / 1024.0);

                return audioData;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei ONNX Audio-Generierung");
                return null;
            }
        }

        /// <summary>
        /// Preprocesst den Text für TTS
        /// </summary>
        private string PreprocessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            Logger.LogDebug("Original text: '{Text}'", text);

            // Grundlegende Text-Normalisierung
            text = text.Trim();

            // Zahlen zu Worten konvertieren (einfache Implementierung)
            text = Regex.Replace(text, @"\b\d+\b", match => NumberToGermanWords(int.Parse(match.Value)));

            // Satzzeichen normalisieren und Pausen einfügen
            text = text.Replace(".", " . ");
            text = text.Replace(",", " , ");
            text = text.Replace("!", " ! ");
            text = text.Replace("?", " ? ");
            text = text.Replace(";", " ; ");
            text = text.Replace(":", " : ");

            // Sonderzeichen normalisieren
            text = text.Replace("ß", "ss");

            // Mehrfache Leerzeichen entfernen
            text = Regex.Replace(text, @"\s+", " ");

            // Gefährliche Zeichen entfernen, aber wichtige Satzzeichen behalten
            text = Regex.Replace(text, @"[^\w\s\.\,\!\?\-\;\:äöüÄÖÜ]", "");

            text = text.ToLower().Trim();

            Logger.LogDebug("Preprocessed text: '{Text}'", text);
            return text;
        }

        /// <summary>
        /// Konvertiert Zahlen zu deutschen Worten
        /// </summary>
        private string NumberToGermanWords(int number)
        {
            return number switch
            {
                0 => "null",
                1 => "eins",
                2 => "zwei",
                3 => "drei",
                4 => "vier",
                5 => "fünf",
                6 => "sechs",
                7 => "sieben",
                8 => "acht",
                9 => "neun",
                10 => "zehn",
                _ => number.ToString() // Fallback für komplexere Zahlen
            };
        }

        /// <summary>
        /// Konvertiert Text zu Phonemen (espeak-kompatible deutsche Phonetik)
        /// </summary>
        private string[] ConvertTextToPhonemes(string text)
        {
            // eSpeak phonemizer is required - no fallback allowed
            if (_phonemizer == null)
            {
                throw new InvalidOperationException(
                    "eSpeak phonemizer ist nicht verfügbar. TTS-Service kann nicht ohne eSpeak funktionieren.");
            }

            var espeakPhonemes = _phonemizer.TextToPhonemes(text);
            if (espeakPhonemes == null || espeakPhonemes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"eSpeak-Phonemizer konnte '{text}' nicht verarbeiten. Keine Fallback-Phonemisierung verfügbar.");
            }

            Logger.LogDebug("Text '{Text}' konvertiert zu eSpeak-Phonemen: {Phonemes}", text,
                string.Join(" ", espeakPhonemes));
            return espeakPhonemes;
        }

        /// <summary>
        /// Konvertiert Phoneme zu numerischen IDs für Piper TTS
        /// </summary>
        private int[] ConvertPhonemesToIds(string[] phonemes)
        {
            var ids = new List<int>();

            // Convert each phoneme to its ID
            for (int i = 0; i < phonemes.Length; i++)
            {
                var phoneme = phonemes[i];
                try
                {
                    var id = GetPhonemeId(phoneme);
                    ids.Add(id);
                    Logger.LogDebug("Phonem '{Phoneme}' (#{Index}) -> ID {Id}", phoneme, i, id);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Fehler beim Konvertieren von Phonem '{Phoneme}' (#{Index}), überspringe",
                        phoneme, i);
                    // Skip this phoneme instead of failing completely
                    continue;
                }
            }

            Logger.LogDebug("Konvertierte {ProcessedCount}/{TotalCount} Phoneme zu IDs: {Phonemes} -> {Ids}",
                ids.Count, phonemes.Length, string.Join(" ", phonemes.Take(10)), string.Join(" ", ids.Take(10)));

            // Ensure we have at least some phoneme IDs
            if (ids.Count == 0)
            {
                Logger.LogWarning("Keine gültigen Phoneme-IDs generiert, verwende Silence");
                if (_phonemeToId != null && _phonemeToId.ContainsKey("_"))
                {
                    ids.Add(_phonemeToId["_"]);
                }
            }

            return ids.ToArray();
        }

        /// <summary>
        /// Gibt die ID für ein Phonem zurück (aus geladener Konfiguration)
        /// </summary>
        private int GetPhonemeId(string phoneme)
        {
            // Require loaded phoneme mapping - no fallback allowed
            if (_phonemeToId == null)
            {
                throw new InvalidOperationException(
                    "Phonem-Mapping ist nicht geladen. TTS-Service kann nicht ohne gültiges Phonem-Mapping funktionieren.");
            }

            // Try exact match first
            if (_phonemeToId.ContainsKey(phoneme))
            {
                return _phonemeToId[phoneme];
            }

            // Try lowercase version
            var lowerPhoneme = phoneme.ToLowerInvariant();
            if (_phonemeToId.ContainsKey(lowerPhoneme))
            {
                Logger.LogDebug("Phonem '{Original}' als '{Lower}' gefunden", phoneme, lowerPhoneme);
                return _phonemeToId[lowerPhoneme];
            }

            // Try uppercase version
            var upperPhoneme = phoneme.ToUpperInvariant();
            if (_phonemeToId.ContainsKey(upperPhoneme))
            {
                Logger.LogDebug("Phonem '{Original}' als '{Upper}' gefunden", phoneme, upperPhoneme);
                return _phonemeToId[upperPhoneme];
            } // Common phoneme fallbacks for eSpeak to Piper mapping

            var fallbackMappings = new Dictionary<string, string>
            {
                // Common eSpeak vowels - map to basic German vowels
                { "I", "i" }, // Uppercase I to lowercase i
                { "E", "e" }, // Uppercase E to lowercase e
                { "A", "a" }, // Uppercase A to lowercase a
                { "O", "o" }, // Uppercase O to lowercase o
                { "U", "u" }, // Uppercase U to lowercase u

                // eSpeak special characters to basic phonemes
                { "@", "a" }, // Schwa - map to basic 'a'
                { "3", "r" }, // R-colored vowel
                { "I2", "i" }, // Near-close near-front unrounded vowel
                { "U2", "u" }, // Near-close near-back rounded vowel

                // Special characters that might not be in mapping - use basic alternatives
                { "{", "a" }, // Open front unrounded vowel -> a
                { "6", "o" }, // Open-mid front rounded vowel -> o
                { "2", "o" }, // Close-mid front rounded vowel -> o
                { "9", "o" }, // Open-mid front rounded vowel -> o
                { "&", "a" }, // Near-open front unrounded vowel -> a
                { "æ", "a" }, // Ash -> a
                { "ø", "o" }, // O-slash -> o
                { "œ", "o" }, // O-e ligature -> o
                { "ŋ", "n" }, // Eng -> n
                { "θ", "t" }, // Theta -> t
                { "ð", "d" }, // Eth -> d
                { "ʃ", "s" }, // Esh -> s
                { "ʒ", "z" }, // Ezh -> z
                { "ç", "h" }, // C-cedilla -> h
                { "ʔ", " " }, // Glottal stop -> space

                // Consonants
                { "R", "r" }, // Alveolar trill
                { "N", "n" }, // If ŋ not available, use n
                { "x", "h" }, // If x not available, use h
                { "S", "s" }, // If ʃ not available, use s
                { "Z", "z" }, // If ʒ not available, use z
                { "?", " " }, // Question mark/glottal stop -> space

                // Common symbols
                { "Q", "o" }, // Open back rounded vowel -> o
                { "V", "u" }, // Open-mid back unrounded vowel -> u
                { "T", "t" }, // Voiceless dental fricative -> t
                { "D", "d" }, // Voiced dental fricative -> d
                { "C", "h" }, // Voiceless palatal fricative -> h
            }; // Try fallback mappings
            if (fallbackMappings.ContainsKey(phoneme))
            {
                var fallbackPhoneme = fallbackMappings[phoneme];
                if (_phonemeToId.ContainsKey(fallbackPhoneme))
                {
                    Logger.LogDebug("Phonem '{Original}' mittels Fallback als '{Fallback}' gefunden", phoneme,
                        fallbackPhoneme);
                    return _phonemeToId[fallbackPhoneme];
                }
                else
                {
                    Logger.LogDebug("Fallback-Phonem '{Fallback}' für '{Original}' auch nicht verfügbar",
                        fallbackPhoneme, phoneme);
                }
            }

            // Try removing stress markers and length markers
            var cleanPhoneme = phoneme.Replace("'", "").Replace(":", "").Replace("ˈ", "").Replace("ˌ", "");
            if (!string.IsNullOrEmpty(cleanPhoneme) && cleanPhoneme != phoneme &&
                _phonemeToId.ContainsKey(cleanPhoneme))
            {
                Logger.LogDebug("Phonem '{Original}' als '{Clean}' (ohne Betonung/Länge) gefunden", phoneme,
                    cleanPhoneme);
                return _phonemeToId[cleanPhoneme];
            }

            // Log available phonemes for debugging (first time only)
            if (!_loggedAvailablePhonemes)
            {
                Logger.LogInformation("Verfügbare Phoneme im Mapping: {Available}",
                    string.Join(", ", _phonemeToId.Keys.OrderBy(k => k).Take(50)));
                _loggedAvailablePhonemes = true;
            }

            Logger.LogWarning("Unbekanntes Phonem '{Phoneme}' - kein Mapping gefunden", phoneme);

            // If still not found, use a default phoneme (silence)
            if (_phonemeToId.ContainsKey("_"))
            {
                Logger.LogDebug("Verwende Silence als Fallback für '{Phoneme}'", phoneme);
                return _phonemeToId["_"];
            }

            // Try space as fallback
            if (_phonemeToId.ContainsKey(" "))
            {
                Logger.LogDebug("Verwende Space als Fallback für '{Phoneme}'", phoneme);
                return _phonemeToId[" "];
            }

            // Last resort: use the first vowel in the mapping
            foreach (var vowel in new[] { "a", "e", "i", "o", "u" })
            {
                if (_phonemeToId.ContainsKey(vowel))
                {
                    Logger.LogWarning("Verwende '{Vowel}' als letzten Fallback für '{Phoneme}'", vowel, phoneme);
                    return _phonemeToId[vowel];
                }
            }

            // Absolute last resort: use the first phoneme in the mapping
            if (_phonemeToId.Count > 0)
            {
                var firstPhoneme = _phonemeToId.First();
                Logger.LogWarning("Verwende '{FirstPhoneme}' als absolut letzten Fallback für '{Phoneme}'",
                    firstPhoneme.Key, phoneme);
                return firstPhoneme.Value;
            }

            throw new InvalidOperationException(
                $"Unbekanntes Phonem '{phoneme}' in Phonem-Mapping. Keine Fallback-Zuordnung verfügbar.");
        }

        /// <summary>
        /// Konvertiert Audio-Features zu WAV-Format
        /// </summary>
        private byte[] ConvertFeaturesToWav(float[] features)
        {
            const int sampleRate = 22050;

            // Normalisiere Features zu 16-bit Audio
            var audioSamples = new short[features.Length];

            // Finde Min/Max für Normalisierung
            var maxVal = features.Max(Math.Abs);
            if (maxVal == 0) maxVal = 1.0f;

            var scale = 32767.0f / maxVal * 0.8f; // 80% der maximalen Amplitude

            for (int i = 0; i < features.Length; i++)
            {
                var sample = features[i] * scale;
                audioSamples[i] = (short)Math.Max(-32768, Math.Min(32767, sample));
            }

            return ConvertToWav(audioSamples, sampleRate);
        }

        public override bool IsAvailable()
        {
            // TTS ist nur verfügbar, wenn eSpeak konfiguriert und initialisiert ist
            return _phonemizer != null && _isInitialized;
        }

        public override string GetProviderName()
        {
            return "Thorsten ONNX TTS";
        }

        public override async Task StopSpeakingAsync()
        {
            // Implementierung für das Stoppen der Sprachausgabe
            Logger.LogDebug("StopSpeakingAsync aufgerufen");
            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            _ttsSession?.Dispose();
            _httpClient?.Dispose();
        }

        /// <summary>
        /// Loads model configuration from the .onnx.json file
        /// </summary>
        private async Task LoadModelConfigurationAsync(string configPath)
        {
            try
            {
                var configText = await File.ReadAllTextAsync(configPath);
                _modelConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(configText);
                Logger.LogInformation("Modell-Konfiguration geladen: {Path}", configPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der Modell-Konfiguration: {Path}", configPath);
                _modelConfig = null;
            }
        }

        /// <summary>
        /// Loads phoneme mapping from model configuration
        /// </summary>
        private void LoadPhonemeMapping()
        {
            try
            {
                if (_modelConfig == null || !_modelConfig.ContainsKey("phoneme_id_map"))
                {
                    Logger.LogWarning("Keine phoneme_id_map in Modell-Konfiguration gefunden");
                    return;
                }

                var phonemeIdMapElement = (JsonElement)_modelConfig["phoneme_id_map"];
                _phonemeToId = new Dictionary<string, int>();

                foreach (var property in phonemeIdMapElement.EnumerateObject())
                {
                    var phoneme = property.Name;
                    if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                    {
                        var id = property.Value[0].GetInt32();
                        _phonemeToId[phoneme] = id;
                    }
                }

                Logger.LogInformation("Phonem-Mapping geladen: {Count} Phoneme", _phonemeToId.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden des Phonem-Mappings");
                _phonemeToId = null;
            }
        }

        /// <summary>
        /// Gets a configuration value from the loaded model config
        /// </summary>
        private float GetConfigValue(string key, float defaultValue)
        {
            try
            {
                if (_modelConfig != null && _modelConfig.ContainsKey("inference"))
                {
                    var inferenceElement = (JsonElement)_modelConfig["inference"];
                    if (inferenceElement.TryGetProperty(key, out var valueElement) &&
                        valueElement.ValueKind == JsonValueKind.Number)
                    {
                        return valueElement.GetSingle();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error getting config value for {Key}, using default {Default}", key, defaultValue);
            }

            return defaultValue;
        }
    }
}