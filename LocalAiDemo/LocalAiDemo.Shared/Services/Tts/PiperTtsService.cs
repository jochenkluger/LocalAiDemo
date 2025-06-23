using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der Piper für hochqualitative lokale Text-to-Speech verwendet
    /// Piper ist die TTS-Engine von Home Assistant mit neuronalen Netzwerken
    /// </summary>
    public class PiperTtsService : TtsServiceBase
    {
        private readonly string _piperExecutable;
        private readonly string _modelsPath;
        private Process? _currentProcess;
        private readonly Dictionary<string, PiperVoiceModel> _availableModels;

        public PiperTtsService(ILogger<PiperTtsService> logger) 
            : base(logger)
        {
            _piperExecutable = FindPiperExecutable();
            _modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PiperModels");
            _availableModels = new Dictionary<string, PiperVoiceModel>();
            
            Logger.LogInformation("Piper TTS Service initialisiert. Executable: {Path}, Models: {ModelsPath}", 
                _piperExecutable, _modelsPath);
            
            _ = Task.Run(LoadAvailableModelsAsync);
        }

        private string FindPiperExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Tools\piper\piper.exe",
                @".\Tools\piper\piper.exe",
                @"piper.exe",
                @"piper",
                @"/usr/local/bin/piper",
                @"/opt/piper/piper",
                @"./piper"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return "piper"; // Fallback
        }

        public override async Task SpeakAsync(string text)
        {
            if (!IsAvailable())
            {
                Logger.LogWarning("Piper TTS ist nicht verfügbar - Executable oder Modelle nicht gefunden");
                return;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit Piper TTS für: {Text}", text);
                
                await StopSpeakingAsync();
                
                var selectedModel = GetBestGermanModel();
                if (selectedModel == null)
                {
                    Logger.LogError("Kein deutsches Piper-Modell verfügbar");
                    return;
                }
                
                var tempAudioFile = Path.GetTempFileName() + ".wav";
                
                await GenerateAudioWithPiperAsync(text, selectedModel, tempAudioFile);
                await PlayAudioFileAsync(tempAudioFile);
                
                // Cleanup nach Verzögerung
                _ = Task.Delay(5000).ContinueWith(_ => 
                {
                    try { File.Delete(tempAudioFile); } 
                    catch { /* Ignore */ }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei Piper TTS: {Text}", text);
            }
        }

        private async Task GenerateAudioWithPiperAsync(string text, PiperVoiceModel model, string outputFile)
        {
            try
            {
                var modelPath = Path.Combine(_modelsPath, model.FileName);
                var configPath = Path.Combine(_modelsPath, model.ConfigFileName);
                
                if (!File.Exists(modelPath) || !File.Exists(configPath))
                {
                    Logger.LogError("Piper-Modell oder Konfigurationsdatei nicht gefunden: {Model}, {Config}", 
                        modelPath, configPath);
                    return;
                }

                var arguments = $"--model \"{modelPath}\" --config \"{configPath}\" --output_file \"{outputFile}\"";
                
                Logger.LogDebug("Piper Aufruf: {Executable} {Arguments}", _piperExecutable, arguments);
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = _piperExecutable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                
                if (process != null)
                {
                    // Text über stdin an Piper senden
                    await process.StandardInput.WriteLineAsync(text);
                    await process.StandardInput.FlushAsync();
                    process.StandardInput.Close();
                    
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        Logger.LogError("Piper Fehler (Exit Code {Code}): {Error}", process.ExitCode, error);
                    }
                    else
                    {
                        Logger.LogDebug("Piper Audio erfolgreich generiert: {File}", outputFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Aufrufen von Piper");
                throw;
            }
        }

        private PiperVoiceModel? GetBestGermanModel()
        {
            // Bevorzuge hochqualitative deutsche Modelle
            var germanModels = _availableModels.Values
                .Where(m => m.Language.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Quality)
                .ThenBy(m => m.SpeakerName)
                .ToList();

            return germanModels.FirstOrDefault();
        }

        private async Task PlayAudioFileAsync(string audioFile)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var playCommand = $"(New-Object Media.SoundPlayer '{audioFile}').PlaySync()";
                    
                    _currentProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{playCommand}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else if (OperatingSystem.IsLinux())
                {
                    var audioPlayer = File.Exists("/usr/bin/aplay") ? "aplay" : "paplay";
                    _currentProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = audioPlayer,
                        Arguments = audioFile,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    _currentProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "afplay",
                        Arguments = audioFile,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                
                if (_currentProcess != null)
                {
                    await _currentProcess.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei der Piper Audio-Wiedergabe: {File}", audioFile);
            }
        }

        public override Task StopSpeakingAsync()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    Logger.LogInformation("Stoppe Piper TTS-Wiedergabe");
                    _currentProcess.Kill();
                    _currentProcess.Dispose();
                    _currentProcess = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Stoppen von Piper TTS");
            }
            
            return Task.CompletedTask;
        }

        public override bool IsAvailable()
        {
            try
            {
                // Prüfe Piper executable
                if (!File.Exists(_piperExecutable) && !IsInPath("piper"))
                {
                    return false;
                }
                
                // Prüfe verfügbare Modelle
                return _availableModels.Any(m => m.Value.Language.StartsWith("de", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private bool IsInPath(string executable)
        {
            try
            {
                var testProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (testProcess != null)
                {
                    testProcess.WaitForExit(2000);
                    var available = testProcess.ExitCode == 0 || testProcess.ExitCode == 1; // Help zeigt oft exit code 1
                    testProcess.Dispose();
                    return available;
                }
            }
            catch { }
            
            return false;
        }

        public override string GetProviderName()
        {
            return "Piper-Local";
        }

        private async Task LoadAvailableModelsAsync()
        {
            try
            {
                Logger.LogInformation("Lade verfügbare Piper-Modelle...");
                
                if (!Directory.Exists(_modelsPath))
                {
                    Directory.CreateDirectory(_modelsPath);
                    Logger.LogInformation("Piper-Modelle Verzeichnis erstellt: {Path}", _modelsPath);
                    
                    // Informiere den Benutzer über Modell-Download
                    Logger.LogInformation("Keine Piper-Modelle gefunden. Deutsche Modelle können heruntergeladen werden von:");
                    Logger.LogInformation("https://huggingface.co/rhasspy/piper-voices/tree/main/de");
                    Logger.LogInformation("Empfohlene deutsche Modelle:");
                    Logger.LogInformation("- de_DE-thorsten-low.onnx (klein, schnell)");
                    Logger.LogInformation("- de_DE-thorsten-medium.onnx (mittlere Qualität)");
                    Logger.LogInformation("- de_DE-kerstin-low.onnx (weibliche Stimme)");
                    
                    return;
                }
                
                var modelFiles = Directory.GetFiles(_modelsPath, "*.onnx");
                
                foreach (var modelFile in modelFiles)
                {
                    var configFile = Path.ChangeExtension(modelFile, ".onnx.json");
                    
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var configContent = await File.ReadAllTextAsync(configFile);
                            var config = JsonSerializer.Deserialize<PiperModelConfig>(configContent);
                            
                            if (config != null)
                            {
                                var model = new PiperVoiceModel
                                {
                                    FileName = Path.GetFileName(modelFile),
                                    ConfigFileName = Path.GetFileName(configFile),
                                    Language = config.language?.code ?? "unknown",
                                    SpeakerName = config.speaker_id_map?.FirstOrDefault().Key ?? "default",
                                    Quality = DetermineQuality(modelFile),
                                    SampleRate = config.audio?.sample_rate ?? 22050
                                };
                                
                                _availableModels[model.FileName] = model;
                                Logger.LogInformation("Piper-Modell geladen: {Model} ({Language}, {Quality})", 
                                    model.FileName, model.Language, model.Quality);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "Fehler beim Laden der Modell-Konfiguration: {Config}", configFile);
                        }
                    }
                }
                
                Logger.LogInformation("Insgesamt {Count} Piper-Modelle geladen", _availableModels.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der Piper-Modelle");
            }
        }

        private string DetermineQuality(string modelFile)
        {
            var fileName = Path.GetFileNameWithoutExtension(modelFile).ToLower();
            
            if (fileName.Contains("high")) return "high";
            if (fileName.Contains("medium")) return "medium";
            if (fileName.Contains("low")) return "low";
            
            // Basierend auf Dateigröße schätzen
            var fileInfo = new FileInfo(modelFile);
            if (fileInfo.Length > 50 * 1024 * 1024) return "high";   // > 50MB
            if (fileInfo.Length > 20 * 1024 * 1024) return "medium"; // > 20MB
            return "low";
        }

        public List<PiperVoiceModel> GetAvailableModels()
        {
            return _availableModels.Values.ToList();
        }

        // Data classes for Piper configuration
        public class PiperVoiceModel
        {
            public string FileName { get; set; } = "";
            public string ConfigFileName { get; set; } = "";
            public string Language { get; set; } = "";
            public string SpeakerName { get; set; } = "";
            public string Quality { get; set; } = "";
            public int SampleRate { get; set; }
        }

        public class PiperModelConfig
        {
            public PiperLanguage? language { get; set; }
            public PiperAudio? audio { get; set; }
            public Dictionary<string, int>? speaker_id_map { get; set; }
        }

        public class PiperLanguage
        {
            public string code { get; set; } = "";
            public string family { get; set; } = "";
            public string region { get; set; } = "";
            public string name_native { get; set; } = "";
            public string name_english { get; set; } = "";
        }

        public class PiperAudio
        {
            public int sample_rate { get; set; }
        }
    }
}
