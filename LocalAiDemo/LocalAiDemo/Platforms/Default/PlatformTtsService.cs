using LocalAiDemo.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LocalAiDemo.Platforms.Default
{
    public class PlatformTtsService : IPlatformTts
    {
        private readonly ILogger<PlatformTtsService> _logger;
        
        public PlatformTtsService(ILogger<PlatformTtsService> logger)
        {
            _logger = logger;
            _logger.LogInformation("Default Platform TTS Provider initialized (no native TTS available)");
        }
        
        public Task SpeakAsync(string text)
        {
            _logger.LogWarning("TTS not available on this platform: {Text}", text);
            return Task.CompletedTask;
        }
        
        public Task StopSpeakingAsync()
        {
            return Task.CompletedTask;
        }
        
        public bool IsAvailable()
        {
            return false;  // This implementation is never available, so it will fallback to the browser TTS
        }
    }
}
