using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der OpenAI's Text-to-Speech API verwendet
    /// </summary>
    public class OpenAiTtsService : TtsServiceBase
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string _apiUrl = "https://api.openai.com/v1/audio/speech";

        public OpenAiTtsService(ILogger<OpenAiTtsService> logger, HttpClient httpClient)
            : base(logger)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            Logger.LogInformation("OpenAI TTS Service initialisiert");
        }

        public override async Task SpeakAsync(string text)
        {
            if (!IsAvailable())
            {
                Logger.LogWarning("OpenAI TTS ist nicht verfügbar - API-Key fehlt");
                return;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit OpenAI TTS für: {Text}", text);

                var audioData = await SynthesizeSpeechAsync(text);

                if (audioData != null)
                {
                    await PlayAudioAsync(audioData);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei OpenAI TTS: {Text}", text);
            }
        }

        private async Task<byte[]?> SynthesizeSpeechAsync(string text)
        {
            try
            {
                var requestData = new
                {
                    model = "tts-1", // oder "tts-1-hd" für höhere Qualität
                    input = text,
                    voice = "alloy", // Verfügbare Stimmen: alloy, echo, fable, onyx, nova, shimmer
                    response_format = "mp3",
                    speed = 1.0
                };

                var json = JsonSerializer.Serialize(requestData);

                using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    Logger.LogError("OpenAI TTS API Fehler: {StatusCode} - {Content}",
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Aufruf der OpenAI TTS API");
                return null;
            }
        }

        private async Task PlayAudioAsync(byte[] audioData)
        {
            // Plattformspezifische Audio-Wiedergabe
            Logger.LogInformation("OpenAI Audio-Wiedergabe erhalten: {Size} Bytes", audioData.Length);

            var tempFile = Path.GetTempFileName() + ".mp3";
            await File.WriteAllBytesAsync(tempFile, audioData);

            Logger.LogInformation("OpenAI Audio gespeichert in: {File}", tempFile);
        }

        public override Task StopSpeakingAsync()
        {
            Logger.LogInformation("Stoppe OpenAI TTS");
            return Task.CompletedTask;
        }

        public override bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }

        public override string GetProviderName()
        {
            return "OpenAI";
        }
    }
}