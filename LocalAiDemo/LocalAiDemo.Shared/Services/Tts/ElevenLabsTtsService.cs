using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Tts
{
    /// <summary>
    /// TTS-Service, der ElevenLabs API für hochqualitative Stimmensynthese verwendet
    /// </summary>
    public class ElevenLabsTtsService : TtsServiceBase
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string _baseUrl = "https://api.elevenlabs.io/v1";
        private readonly string _defaultVoiceId = "21m00Tcm4TlvDq8ikWAM"; // Rachel (englisch)
        // Für deutsche Stimmen müsste man andere Voice IDs verwenden

        public ElevenLabsTtsService(ILogger<ElevenLabsTtsService> logger, HttpClient httpClient)
            : base(logger)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");

            Logger.LogInformation("ElevenLabs TTS Service initialisiert");
        }

        public override async Task SpeakAsync(string text)
        {
            if (!IsAvailable())
            {
                Logger.LogWarning("ElevenLabs TTS ist nicht verfügbar - API-Key fehlt");
                return;
            }

            try
            {
                Logger.LogInformation("Generiere Audio mit ElevenLabs TTS für: {Text}", text);

                var audioData = await SynthesizeSpeechAsync(text);

                if (audioData != null)
                {
                    await PlayAudioAsync(audioData);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler bei ElevenLabs TTS: {Text}", text);
            }
        }

        private async Task<byte[]?> SynthesizeSpeechAsync(string text)
        {
            try
            {
                var url = $"{_baseUrl}/text-to-speech/{_defaultVoiceId}";

                var requestData = new
                {
                    text = text,
                    model_id = "eleven_multilingual_v2", // Unterstützt Deutsche Sprache
                    voice_settings = new
                    {
                        stability = 0.5,
                        similarity_boost = 0.5,
                        style = 0.0,
                        use_speaker_boost = true
                    }
                };

                var json = JsonSerializer.Serialize(requestData);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Xi-Api-Key", _apiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    Logger.LogError("ElevenLabs TTS API Fehler: {StatusCode} - {Content}",
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Aufruf der ElevenLabs TTS API");
                return null;
            }
        }

        private async Task PlayAudioAsync(byte[] audioData)
        {
            Logger.LogInformation("ElevenLabs Audio-Wiedergabe erhalten: {Size} Bytes", audioData.Length);

            var tempFile = Path.GetTempFileName() + ".mp3";
            await File.WriteAllBytesAsync(tempFile, audioData);

            Logger.LogInformation("ElevenLabs Audio gespeichert in: {File}", tempFile);
        }

        public override Task StopSpeakingAsync()
        {
            Logger.LogInformation("Stoppe ElevenLabs TTS");
            return Task.CompletedTask;
        }

        public override bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }

        public override string GetProviderName()
        {
            return "ElevenLabs";
        }

        /// <summary>
        /// Lädt verfügbare Stimmen von ElevenLabs
        /// </summary>
        public async Task<List<VoiceInfo>> GetAvailableVoicesAsync()
        {
            try
            {
                var url = $"{_baseUrl}/voices";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Xi-Api-Key", _apiKey);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var voicesResponse = JsonSerializer.Deserialize<VoicesResponse>(jsonResponse);

                    return voicesResponse?.voices ?? new List<VoiceInfo>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fehler beim Laden der verfügbaren Stimmen");
            }

            return new List<VoiceInfo>();
        }

        public class VoiceInfo
        {
            public string voice_id { get; set; } = "";
            public string name { get; set; } = "";
            public string description { get; set; } = "";
            public string category { get; set; } = "";
        }

        public class VoicesResponse
        {
            public List<VoiceInfo> voices { get; set; } = new();
        }
    }
}