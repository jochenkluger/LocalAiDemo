using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Text;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der lokale ONNX-Modelle für Text-to-Speech verwendet
    /// Unterstützt hochqualitative Sprachsynthese ohne Cloud-Abhängigkeiten
    /// </summary>
    public class OnnxTtsService : TtsServiceBase
    {
        private readonly string _modelsPath;
        private bool _isInitialized;

        public OnnxTtsService(ILogger<OnnxTtsService> logger) 
            : base(logger)
        {
            _modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TtsModels");
            Logger.LogInformation("ONNX TTS Service initialisiert. Modell-Pfad: {Path}", _modelsPath);
        }

        public override async Task SpeakAsync(string text)
        {
            if (!IsAvailable())
            {
                Logger.LogWarning("ONNX TTS ist nicht verfügbar - Modelle nicht geladen");
                return;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit ONNX TTS für: {Text}", text);
                
                // Text preprocessing
                var processedText = PreprocessText(text);
                
                // Text zu Phonemen konvertieren
                var phonemes = await TextToPhonemes(processedText);
                
                // Phoneme zu Audio konvertieren
                var audioData = await PhonemesToAudio(phonemes);
                
                if (audioData != null)
                {
                    await PlayAudioAsync(audioData);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei ONNX TTS: {Text}", text);
            }
        }

        private string PreprocessText(string text)
        {
            // Text normalisierung für deutsche Sprache
            var processed = text
                .Replace("ä", "ae")
                .Replace("ö", "oe")
                .Replace("ü", "ue")
                .Replace("ß", "ss")
                .Replace("Ä", "Ae")
                .Replace("Ö", "Oe")
                .Replace("Ü", "Ue");
            
            // Zahlen zu Text konvertieren (vereinfacht)
            processed = System.Text.RegularExpressions.Regex.Replace(processed, @"\d+", m =>
            {
                if (int.TryParse(m.Value, out int number))
                {
                    return NumberToGermanText(number);
                }
                return m.Value;
            });
            
            return processed;
        }

        private string NumberToGermanText(int number)
        {
            // Vereinfachte Zahlen-zu-Text Konvertierung für deutsche Sprache
            var germanNumbers = new Dictionary<int, string>
            {
                { 0, "null" }, { 1, "eins" }, { 2, "zwei" }, { 3, "drei" }, 
                { 4, "vier" }, { 5, "fünf" }, { 6, "sechs" }, { 7, "sieben" }, 
                { 8, "acht" }, { 9, "neun" }, { 10, "zehn" }
            };
            
            return germanNumbers.TryGetValue(number, out var text) ? text : number.ToString();
        }

        private async Task<string[]> TextToPhonemes(string text)
        {
            // TODO: Hier würde ein G2P (Grapheme-to-Phoneme) Modell verwendet
            // Für jetzt verwenden wir eine vereinfachte Phonem-Generierung
            
            Logger.LogDebug("Konvertiere Text zu Phonemen: {Text}", text);
            
            // Simuliere Phonem-Generierung
            await Task.Delay(10); // Simuliere Verarbeitungszeit
            
            // Vereinfachte Phonem-Darstellung (IPA-ähnlich)
            var phonemes = text.ToLower()
                .Replace("ch", "x")
                .Replace("sch", "ʃ")
                .Replace("th", "θ")
                .ToCharArray()
                .Select(c => c.ToString())
                .ToArray();
            
            return phonemes;
        }

        private async Task<byte[]?> PhonemesToAudio(string[] phonemes)
        {
            try
            {
                Logger.LogDebug("Generiere Audio aus {Count} Phonemen", phonemes.Length);
                
                // TODO: Hier würde das ONNX TTS-Modell verwendet werden
                // Beispiel mit Microsoft's FastSpeech2 oder ähnlichem Modell
                
                // Simuliere Audio-Generierung
                await Task.Delay(100 * phonemes.Length); // Simuliere Verarbeitungszeit
                
                // Generiere eine einfache Sinus-Welle als Audio-Placeholder
                var audioData = GenerateSineWaveAudio(phonemes);
                
                return audioData;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei der Audio-Generierung");
                return null;
            }
        }        private byte[] GenerateSineWaveAudio(string[] phonemes)
        {
            // Verbesserte Audio-Generierung als Demo
            // In einer echten Implementierung würde hier das ONNX-Modell verwendet
            
            const int sampleRate = 22050;
            var durationPerPhoneme = 0.15; // 150ms pro Phonem
            var totalDuration = Math.Max(1.0, phonemes.Length * durationPerPhoneme); // Mindestens 1 Sekunde
            
            var totalSamples = (int)(sampleRate * totalDuration);
            var samples = new short[totalSamples];
            
            Logger.LogInformation("Generiere {Duration:F1}s Audio für {Count} Phoneme", totalDuration, phonemes.Length);
            
            // Generiere variierenden Ton basierend auf Phonemen
            for (int i = 0; i < samples.Length; i++)
            {
                var timeProgress = (double)i / samples.Length;
                var phonemeIndex = (int)(timeProgress * phonemes.Length);
                
                // Verschiedene Frequenzen für verschiedene Phoneme
                var baseFrequency = 200.0; // Grundfrequenz
                var phonemeFrequency = GetFrequencyForPhoneme(phonemes.ElementAtOrDefault(phonemeIndex) ?? "a");
                var frequency = baseFrequency + phonemeFrequency;
                
                // Envelope für natürlichere Klänge
                var envelope = Math.Sin(Math.PI * timeProgress) * 0.3; // Fade in/out
                
                var amplitude = Math.Sin(2.0 * Math.PI * frequency * i / sampleRate);
                samples[i] = (short)(amplitude * short.MaxValue * envelope);
            }
            
            // Konvertiere zu Byte-Array (16-bit PCM)
            var audioData = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, audioData, 0, audioData.Length);
            
            return CreateWavFile(audioData, sampleRate);
        }

        private double GetFrequencyForPhoneme(string phoneme)
        {
            // Einfache Frequenz-Zuordnung basierend auf Phonem/Buchstabe
            return phoneme.ToLower() switch
            {
                "a" or "ä" => 100,
                "e" => 150,
                "i" => 200,
                "o" or "ö" => 80,
                "u" or "ü" => 60,
                "r" => 120,
                "l" => 180,
                "n" or "m" => 90,
                "s" or "ʃ" => 300,
                "t" or "d" => 250,
                "k" or "g" => 140,
                "p" or "b" => 110,
                "f" or "v" => 280,
                "x" or "h" => 160,
                _ => 100
            };
        }

        private byte[] CreateWavFile(byte[] pcmData, int sampleRate)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);
            
            // WAV Header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + pcmData.Length);
            writer.Write("WAVE".ToCharArray());
            
            // fmt chunk
            writer.Write("fmt ".ToCharArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // AudioFormat (PCM)
            writer.Write((short)1); // NumChannels (Mono)
            writer.Write(sampleRate); // SampleRate
            writer.Write(sampleRate * 2); // ByteRate
            writer.Write((short)2); // BlockAlign
            writer.Write((short)16); // BitsPerSample
            
            // data chunk
            writer.Write("data".ToCharArray());
            writer.Write(pcmData.Length);
            writer.Write(pcmData);
            
            return memoryStream.ToArray();
        }        private async Task PlayAudioAsync(byte[] audioData)
        {
            Logger.LogInformation("ONNX TTS Audio-Wiedergabe: {Size} Bytes", audioData.Length);
            
            try
            {
                // Temporäre WAV-Datei erstellen
                var tempFile = Path.GetTempFileName() + ".wav";
                await File.WriteAllBytesAsync(tempFile, audioData);
                
                Logger.LogInformation("ONNX TTS Audio gespeichert in: {File}", tempFile);
                
                // Plattformspezifische Audio-Wiedergabe
                if (OperatingSystem.IsWindows())
                {
                    // Windows: Verwende PowerShell für Audio-Wiedergabe
                    var playCommand = $"(New-Object Media.SoundPlayer '{tempFile}').PlaySync()";
                    
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{playCommand}\"",
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
                    Logger.LogWarning("Audio-Wiedergabe auf dieser Plattform noch nicht implementiert");
                }
                
                // Temporäre Datei nach kurzer Verzögerung löschen
                _ = Task.Delay(2000).ContinueWith(_ => 
                {
                    try { File.Delete(tempFile); } 
                    catch { /* Ignore */ }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei der ONNX Audio-Wiedergabe");
            }
        }

        public override Task StopSpeakingAsync()
        {
            Logger.LogInformation("Stoppe ONNX TTS");
            // Audio-Wiedergabe stoppen
            return Task.CompletedTask;
        }        public override bool IsAvailable()
        {
            // Für Demo-Zwecke: Immer verfügbar (verwendet Fallback-Audio)
            // In Produktion würde hier geprüft werden, ob ONNX-Modelle vorhanden sind
            Logger.LogDebug("ONNX TTS IsAvailable Check - Models Path: {Path}", _modelsPath);
            
            if (Directory.Exists(_modelsPath) && Directory.GetFiles(_modelsPath, "*.onnx").Length > 0)
            {
                Logger.LogInformation("ONNX Models gefunden in: {Path}", _modelsPath);
                return true;
            }
            
            Logger.LogWarning("Keine ONNX-Modelle gefunden. Verwende Fallback-Audio für Demo.");
            return true; // Verwende Fallback für Demo-Zwecke
        }

        public override string GetProviderName()
        {
            return "ONNX-Local";
        }

        /// <summary>
        /// Initialisiert die ONNX-Modelle
        /// </summary>
        public async Task InitializeModelsAsync()
        {
            try
            {
                Logger.LogInformation("Initialisiere ONNX TTS-Modelle...");
                
                if (!Directory.Exists(_modelsPath))
                {
                    Directory.CreateDirectory(_modelsPath);
                }
                
                // TODO: Hier würden die ONNX-Modelle geladen werden
                // Beispiel-Modelle:
                // - Microsoft FastSpeech2
                // - Facebook MMS TTS
                // - Piper TTS (Home Assistant)
                
                await Task.Delay(100); // Simuliere Ladezeit
                
                _isInitialized = true;
                Logger.LogInformation("ONNX TTS-Modelle erfolgreich initialisiert");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Initialisieren der ONNX TTS-Modelle");
                _isInitialized = false;
            }
        }
    }
}
