using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp.Models;
using OllamaSharp;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LocalAiDemo.Shared.Services
{
    public class LocalTextGenerationService
    {
        private IOptions<AppConfiguration> _appConfiguration;
        private string _appDataPath;
        private string _modelFilePath;
        private LLamaWeights _model;
        private IChatClient _chatClient;

        private List<ChatMessage> _chatHistory =
            new List<ChatMessage>(); //https://learn.microsoft.com/de-de/dotnet/ai/microsoft-extensions-ai

        public LocalTextGenerationService(IOptions<AppConfiguration> config)
        {
            _appConfiguration = config;
        }

        public async Task InitializeAsync(Action<double> onProgress = null)
        {
            var selectedModel =
                AvailableModels.Models.FirstOrDefault(x => x.Name == _appConfiguration.Value.GenerationProvider);
            if (selectedModel == null)
            {
                throw new InvalidOperationException("Ausgewähltes Model ist nicht bekannt");
            }

            // Pfad zum ApplicationData-Verzeichnis
            _appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var myAppDataPath = Path.Combine(_appDataPath, "LocalAiDemo");

            if (Directory.Exists(myAppDataPath) == false)
            {
                Directory.CreateDirectory(myAppDataPath);
            }

            _modelFilePath = Path.Combine(_appDataPath, "LocalAiDemo", selectedModel.FileName);

            if (File.Exists(_modelFilePath) == false)
            {
                // Modell herunterladen
                await DownloadModelAsync(selectedModel.Url, _modelFilePath, onProgress);
            }
            else
            {
                onProgress?.Invoke(100);
            }
        }

        public async Task StartChatAsync(Action<double> onProgress = null)
        {
            var parameters = new ModelParams(_modelFilePath)
            {
                //TODO
            };
            var progress = new Progress<float>(p => onProgress?.Invoke(p * 100));
            _model = await LLamaWeights.LoadFromFileAsync(parameters, CancellationToken.None, progress);
            var ex = new StatelessExecutor(_model, parameters);

            _chatClient = ex.AsChatClient();

            //TODO: Function calling not implemented in LlamaSharp --> Use SemanticKernel?
            //IChatClient client = _chatClient
            //    .UseFunctionInvocation()
            //    .Build();

            //ChatOptions options = new() { Tools = [AIFunctionFactory.Create(GetCurrentWeather)] };

            _chatHistory.Clear();
            _chatHistory.Add(new ChatMessage(ChatRole.System, Constants.SystemMessage));
            _chatHistory.Add(new ChatMessage(ChatRole.Assistant, Constants.AgentWelcomeMessage));
        }

        public async Task<string> InferAsync(string message)
        {
            _chatHistory.Add(new ChatMessage(ChatRole.User, message));
            var response = await _chatClient.GetResponseAsync(_chatHistory);
            _chatHistory.AddMessages(response);
            return response.Text;
        }

        /// <summary>
        /// Lädt das Modell herunter und speichert es im Zielverzeichnis.
        /// </summary>
        private async Task DownloadModelAsync(string url, string destinationPath, Action<double> onProgress)
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Fehler beim Herunterladen des Modells: {response.StatusCode}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1L; // -1L für unbekannte Größe
            var downloadedBytes = 0L;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                fileStream.Write(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                // Fortschritt berechnen und melden
                if (totalBytes > 0) // Nur wenn die Größe bekannt ist
                {
                    var progress = (double)downloadedBytes / totalBytes * 100;
                    onProgress?.Invoke(progress);
                }
            }
        }
    }
}