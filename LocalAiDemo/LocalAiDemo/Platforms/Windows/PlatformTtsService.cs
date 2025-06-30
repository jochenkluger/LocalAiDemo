using LocalAiDemo.Shared.Services.Tts;
using Microsoft.Extensions.Logging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace LocalAiDemo.Platforms.Windows
{
    public class PlatformTtsService : IPlatformTts, IDisposable
    {
        private readonly ILogger<PlatformTtsService> _logger;
        private SpeechSynthesizer? _synthesizer;
        private MediaPlayer? _mediaPlayer;
        private bool _isAvailable;

        public PlatformTtsService(ILogger<PlatformTtsService> logger)
        {
            _logger = logger;

            try
            {
                // Initialize Windows Media TTS
                _synthesizer = new SpeechSynthesizer();
                _mediaPlayer = new MediaPlayer();

                // Try to find a German voice
                var voices = SpeechSynthesizer.AllVoices;
                var germanVoice = voices.FirstOrDefault(v => v.Language.StartsWith("de-"));

                // Set voice if available
                if (germanVoice != null)
                {
                    _synthesizer.Voice = germanVoice;
                    _logger.LogInformation("German voice found: {VoiceName}", germanVoice.DisplayName);
                }

                _isAvailable = true;
                _logger.LogInformation("Windows TTS successfully initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Windows TTS");
                _isAvailable = false;
            }
        }

        public async Task SpeakAsync(string text)
        {
            if (!_isAvailable || _synthesizer == null || _mediaPlayer == null)
            {
                _logger.LogWarning("Windows TTS is not available");
                return;
            }

            try
            {
                _logger.LogInformation("Windows TTS speaking: {Text}", text);

                // Stop current playback
                await StopSpeakingAsync();

                // Synthesize speech
                var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);

                // Set MediaSource and start playback
                _mediaPlayer.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Windows TTS");
            }
        }

        public Task StopSpeakingAsync()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Pause();
                    _mediaPlayer.Source = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Windows TTS");
            }

            return Task.CompletedTask;
        }

        public bool IsAvailable()
        {
            return _isAvailable;
        }

        public void Dispose()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Dispose();
                    _mediaPlayer = null;
                }

                if (_synthesizer != null)
                {
                    _synthesizer.Dispose();
                    _synthesizer = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Windows TTS resources");
            }
        }
    }
}