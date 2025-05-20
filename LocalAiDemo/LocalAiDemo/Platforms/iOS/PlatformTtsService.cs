using AVFoundation;
using Foundation;
using LocalAiDemo.Shared.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace LocalAiDemo.Platforms.iOS
{
    public class PlatformTtsService : IPlatformTts, IDisposable
    {
        private readonly ILogger<PlatformTtsService> _logger;
        private AVSpeechSynthesizer _speechSynthesizer;
        private bool _isAvailable;
        
        public PlatformTtsService(ILogger<PlatformTtsService> logger)
        {
            _logger = logger;
            
            try
            {
                // Initialize AVFoundation TTS
                _speechSynthesizer = new AVSpeechSynthesizer();
                _isAvailable = true;
                _logger.LogInformation("iOS TTS successfully initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing iOS TTS");
                _isAvailable = false;
            }
        }
        
        public async Task SpeakAsync(string text)
        {
            if (!_isAvailable)
            {
                _logger.LogWarning("iOS TTS is not available");
                return;
            }
            
            try
            {
                _logger.LogInformation("iOS TTS speaking: {Text}", text);
                
                // Stop current playback
                await StopSpeakingAsync();
                
                // Create utterance with German language
                var speechUtterance = new AVSpeechUtterance(text)
                {
                    Voice = AVSpeechSynthesisVoice.FromLanguage("de-DE"),
                    Rate = AVSpeechUtterance.DefaultSpeechRate,
                    Volume = 1.0f,
                    PitchMultiplier = 1.0f
                };
                
                // If no German voice was found, use the default voice
                if (speechUtterance.Voice == null)
                {
                    _logger.LogWarning("No German voice found, using default voice");
                    speechUtterance.Voice = AVSpeechSynthesisVoice.CurrentLanguageVoice;
                }
                
                // Start speech synthesis
                var taskCompletionSource = new TaskCompletionSource<bool>();
                
                // Event handling for speech output end
                NSNotificationCenter.DefaultCenter.AddObserver(
                    AVSpeechSynthesizer.DidFinishSpeechUtteranceNotification,
                    notification => taskCompletionSource.TrySetResult(true));
                
                // Start speech synthesis
                _speechSynthesizer.SpeakUtterance(speechUtterance);
                
                // Wait for completion or timeout after 30 seconds
                await Task.WhenAny(taskCompletionSource.Task, Task.Delay(30000));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in iOS TTS");
            }
        }
        
        public Task StopSpeakingAsync()
        {
            try
            {
                if (_isAvailable && _speechSynthesizer != null)
                {
                    _logger.LogInformation("Stopping iOS TTS");
                    
                    if (_speechSynthesizer.Speaking)
                    {
                        _speechSynthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping iOS TTS");
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
                // Remove observers
                NSNotificationCenter.DefaultCenter.RemoveObserver(this);
                
                // Clean up resources
                if (_speechSynthesizer != null)
                {
                    if (_speechSynthesizer.Speaking)
                    {
                        _speechSynthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
                    }
                    
                    _speechSynthesizer.Dispose();
                    _speechSynthesizer = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up iOS TTS resources");
            }
        }
    }
}
