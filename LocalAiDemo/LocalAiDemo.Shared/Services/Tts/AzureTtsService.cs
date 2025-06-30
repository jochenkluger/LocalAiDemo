using System.Text;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der Azure Cognitive Services Speech Service verwendet
    /// </summary>
    public class AzureTtsService : TtsServiceBase
    {
        private readonly HttpClient _httpClient;
        private readonly string? _subscriptionKey;
        private readonly string? _serviceRegion;
        private readonly string _serviceUrl;

        public AzureTtsService(ILogger<AzureTtsService> logger, HttpClient httpClient)
            : base(logger)
        {
            _httpClient = httpClient;

            // Diese sollten aus der Konfiguration kommen
            _subscriptionKey = Environment.GetEnvironmentVariable("AZURE_TTS_KEY");
            _serviceRegion = Environment.GetEnvironmentVariable("AZURE_TTS_REGION") ?? "westeurope";
            _serviceUrl = $"https://{_serviceRegion}.tts.speech.microsoft.com/cognitiveservices/v1";

            Logger.LogInformation("Azure TTS Service initialisiert für Region: {Region}", _serviceRegion);
        }

        public override async Task SpeakAsync(string text)
        {
            if (!IsAvailable())
            {
                Logger.LogWarning("Azure TTS ist nicht verfügbar - API-Key fehlt");
                return;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit Azure TTS für: {Text}", text);

                // SSML für deutsche Stimme erstellen
                var ssml = CreateSsml(text);

                // Request an Azure TTS senden
                var audioData = await SynthesizeSpeechAsync(ssml);

                if (audioData != null)
                {
                    // Audio abspielen (implementierung abhängig von Plattform)
                    await PlayAudioAsync(audioData);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei Azure TTS: {Text}", text);
            }
        }

        private string CreateSsml(string text)
        {
            return $@"
                <speak version='1.0' xml:lang='de-DE'>
                    <voice xml:lang='de-DE' xml:gender='Female' name='de-DE-KatjaNeural'>
                        {text}
                    </voice>
                </speak>";
        }

        private async Task<byte[]?> SynthesizeSpeechAsync(string ssml)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _serviceUrl);

                request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                request.Headers.Add("User-Agent", "LocalAiDemo");
                request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");

                request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    Logger.LogError("Azure TTS API Fehler: {StatusCode} - {Content}",
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Aufruf der Azure TTS API");
                return null;
            }
        }

        private async Task PlayAudioAsync(byte[] audioData)
        {
            // TODO: Plattformspezifische Audio-Wiedergabe implementieren Für MAUI könnte man
            // MediaElement oder native Audio-Player verwenden
            Logger.LogInformation("Audio-Wiedergabe erhalten: {Size} Bytes", audioData.Length);

            // Beispiel für temporäre Datei (vereinfacht)
            var tempFile = Path.GetTempFileName() + ".mp3";
            await File.WriteAllBytesAsync(tempFile, audioData);

            // Hier würde die plattformspezifische Wiedergabe stattfinden
            Logger.LogInformation("Audio gespeichert in: {File}", tempFile);
        }

        public override Task StopSpeakingAsync()
        {
            Logger.LogInformation("Stoppe Azure TTS");
            // Audio-Wiedergabe stoppen
            return Task.CompletedTask;
        }

        public override bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_subscriptionKey);
        }

        public override string GetProviderName()
        {
            return "Azure";
        }
    }
}