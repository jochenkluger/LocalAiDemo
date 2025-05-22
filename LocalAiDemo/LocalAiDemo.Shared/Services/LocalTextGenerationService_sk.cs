//using System;
//using System.Threading.Tasks;
//using LocalAiDemo.Services.Interfaces;

//namespace LocalAiDemo.Services
//{
//    public class LocalGenerationService : IGenerationService
//    {
//        private readonly SemanticKernelInferenceService _inferenceService;
//        private bool _isInitialized = false;

// public event Action<double>? InitializationProgress;

// public LocalGenerationService(SemanticKernelInferenceService inferenceService) {
// _inferenceService = inferenceService; }

// public async Task Initialize() { if (_isInitialized) return;

// // Modell herunterladen, falls erforderlich await _inferenceService.InitializeModelAsync(progress
// => InitializationProgress?.Invoke(progress));

// // Chat initialisieren await _inferenceService.InintializeChat(progress => InitializationProgress?.Invoke(progress));

// _isInitialized = true; }

// public async Task<string> GenerateTextAsync(string input) { if (!_isInitialized) { await
// Initialize(); }

//            return await _inferenceService.RunInferenceAsync(input);
//        }
//    }
//}