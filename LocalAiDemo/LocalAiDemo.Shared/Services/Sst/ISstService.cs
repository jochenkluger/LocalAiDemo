using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace LocalAiDemo.Shared.Services.Sst
{
    public interface ISstService
    {
        Task<bool> InitializeSpeechRecognitionAsync<T>(IJSRuntime jsRuntime, DotNetObjectReference<T> dotNetObjectReference) where T : class;
        Task StartSpeechRecognitionAsync(IJSRuntime jsRuntime);
        Task StopSpeechRecognitionAsync(IJSRuntime jsRuntime);
        Task<bool> IsAvailableAsync(IJSRuntime jsRuntime);
        string GetProviderName();
    }

    /// <summary>
    /// Abstrakte Basisklasse für SST-Dienste, die gemeinsame Funktionalität bereitstellt
    /// </summary>
    public abstract class SstServiceBase : ISstService
    {
        protected readonly ILogger Logger;

        protected SstServiceBase(ILogger logger)
        {
            Logger = logger;
        }

        public abstract Task<bool> InitializeSpeechRecognitionAsync<T>(IJSRuntime jsRuntime, DotNetObjectReference<T> dotNetObjectReference) where T : class;
        public abstract Task StartSpeechRecognitionAsync(IJSRuntime jsRuntime);
        public abstract Task StopSpeechRecognitionAsync(IJSRuntime jsRuntime);
        public abstract Task<bool> IsAvailableAsync(IJSRuntime jsRuntime);
        public abstract string GetProviderName();
    }
}
