using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Text;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der eSpeak für lokale Text-to-Speech verwendet
    /// eSpeak ist eine leichtgewichtige, Open-Source TTS-Engine
    /// </summary>
    public class ESpeakTtsService : TtsServiceBase
    {
        private readonly string _eSpeakExecutable;
        private Process? _currentProcess;

        public ESpeakTtsService(ILogger<ESpeakTtsService> logger) 
            : base(logger)
        {
            // eSpeak executable path (muss installiert sein)
            _eSpeakExecutable = FindESpeakExecutable();
            Logger.LogInformation("eSpeak TTS Service initialisiert. Executable: {Path}", _eSpeakExecutable);
        }

        private string FindESpeakExecutable()
        {
            // Suche eSpeak in verschiedenen Standard-Pfaden
            var possiblePaths = new[]
            {
                @"C:\Program Files\eSpeak NG\espeak-ng.exe",
                @"C:\Program Files (x86)\eSpeak NG\espeak-ng.exe",
                @"espeak-ng.exe", // Im PATH
                @"espeak.exe",    // Ältere Version
                @"/usr/bin/espeak-ng",     // Linux
                @"/usr/local/bin/espeak-ng", // Linux/macOS
                @"/opt/homebrew/bin/espeak-ng" // macOS (Homebrew)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Versuche über PATH zu finden
            try
            {
                var result = Process.Start(new ProcessStartInfo
                {
                    FileName = "where", // Windows
                    Arguments = "espeak-ng.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (result != null)
                {
                    var output = result.StandardOutput.ReadToEnd();
                    result.WaitForExit();
                    
                    if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        return output.Trim().Split('\n')[0];
                    }
                }
            }
            catch
            {
                // Ignore errors when trying to find executable
            }

            return "espeak-ng"; // Fallback - hoffen dass es im PATH ist
        }

        public override async Task SpeakAsync(string text)
        {
            if (!IsAvailable())
            {
                Logger.LogWarning("eSpeak TTS ist nicht verfügbar - Executable nicht gefunden");
                return;
            }

            try
            {
                Logger.LogInformation("Spreche mit eSpeak TTS: {Text}", text);
                
                // Stoppe vorherige Wiedergabe
                await StopSpeakingAsync();
                
                // Erstelle temporäre Audio-Datei
                var tempAudioFile = Path.GetTempFileName() + ".wav";
                
                await GenerateAudioFileAsync(text, tempAudioFile);
                
                // Audio-Datei abspielen
                await PlayAudioFileAsync(tempAudioFile);
                
                // Temporäre Datei nach kurzer Verzögerung löschen
                _ = Task.Delay(5000).ContinueWith(_ => 
                {
                    try { File.Delete(tempAudioFile); } 
                    catch { /* Ignore */ }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei eSpeak TTS: {Text}", text);
            }
        }

        private async Task GenerateAudioFileAsync(string text, string outputFile)
        {
            try
            {
                var arguments = BuildESpeakArguments(text, outputFile);
                
                Logger.LogDebug("eSpeak Aufruf: {Executable} {Arguments}", _eSpeakExecutable, arguments);
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = _eSpeakExecutable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        Logger.LogError("eSpeak Fehler (Exit Code {Code}): {Error}", process.ExitCode, error);
                    }
                    else
                    {
                        Logger.LogDebug("eSpeak Audio erfolgreich generiert: {File}", outputFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Aufrufen von eSpeak");
                throw;
            }
        }

        private string BuildESpeakArguments(string text, string outputFile)
        {
            var args = new StringBuilder();
            
            // Sprache auf Deutsch setzen
            args.Append("-v de ");
            
            // Geschwindigkeit (150 WPM - Wörter pro Minute)
            args.Append("-s 150 ");
            
            // Pitch (50 = normal)
            args.Append("-p 50 ");
            
            // Amplitude (100 = normal)
            args.Append("-a 100 ");
            
            // Output to WAV file
            args.Append($"-w \"{outputFile}\" ");
            
            // Text in Anführungszeichen
            args.Append($"\"{text.Replace("\"", "\\\"")}\"");
            
            return args.ToString();
        }

        private async Task PlayAudioFileAsync(string audioFile)
        {
            try
            {
                // Plattformspezifische Audio-Wiedergabe
                if (OperatingSystem.IsWindows())
                {
                    // Windows: Verwende PowerShell für Audio-Wiedergabe
                    var playCommand = $"(New-Object Media.SoundPlayer '{audioFile}').PlaySync()";
                    
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{playCommand}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    _currentProcess = Process.Start(processInfo);
                    if (_currentProcess != null)
                    {
                        await _currentProcess.WaitForExitAsync();
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Linux: Verwende aplay oder paplay
                    var audioPlayer = File.Exists("/usr/bin/aplay") ? "aplay" : "paplay";
                    
                    _currentProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = audioPlayer,
                        Arguments = audioFile,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    
                    if (_currentProcess != null)
                    {
                        await _currentProcess.WaitForExitAsync();
                    }
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // macOS: Verwende afplay
                    _currentProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "afplay",
                        Arguments = audioFile,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    
                    if (_currentProcess != null)
                    {
                        await _currentProcess.WaitForExitAsync();
                    }
                }
                else
                {
                    Logger.LogWarning("Unsupported platform for audio playback");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei der Audio-Wiedergabe: {File}", audioFile);
            }
        }

        public override Task StopSpeakingAsync()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    Logger.LogInformation("Stoppe eSpeak TTS-Wiedergabe");
                    _currentProcess.Kill();
                    _currentProcess.Dispose();
                    _currentProcess = null;
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
                // Teste, ob eSpeak ausführbar ist
                var testProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = _eSpeakExecutable,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (testProcess != null)
                {
                    testProcess.WaitForExit(3000); // 3 Sekunden Timeout
                    var available = testProcess.ExitCode == 0;
                    testProcess.Dispose();
                    return available;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "eSpeak Verfügbarkeitstest fehlgeschlagen");
            }
            
            return false;
        }

        public override string GetProviderName()
        {
            return "eSpeak-Local";
        }

        /// <summary>
        /// Gibt Informationen über die verfügbaren eSpeak-Stimmen zurück
        /// </summary>
        public async Task<List<string>> GetAvailableVoicesAsync()
        {
            var voices = new List<string>();
            
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _eSpeakExecutable,
                    Arguments = "--voices",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(processInfo);
                
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var line in lines.Skip(1)) // Überspringe Header
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                voices.Add(parts[1]); // Voice name/language
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der eSpeak-Stimmen");
            }
            
            return voices;
        }
    }
}
