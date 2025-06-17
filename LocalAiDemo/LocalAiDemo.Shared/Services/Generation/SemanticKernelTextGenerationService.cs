using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLamaSharp.SemanticKernel.ChatCompletion;
using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Generation;
using LocalAiDemo.Shared.Services.FunctionCalling;
using LocalAiDemo.Shared.Services.Generation.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LocalAiDemo.Shared.Services
{
    /// <summary>
    /// Semantic Kernel basierter Text Generation Service mit LlamaSharp und Function Calling Support
    /// </summary>
    public class SemanticKernelTextGenerationService : ITextGenerationService
    {
        private readonly IOptions<AppConfiguration> _appConfiguration;
        private readonly IMessageCreator _messageCreator;
        private readonly ILogger<SemanticKernelTextGenerationService> _logger;

        private string _appDataPath = string.Empty;
        private string _modelFilePath = string.Empty;
        private LLamaWeights? _model;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private IChatClient? _chatClient;
        private List<ChatMessage> _chatHistory = new List<ChatMessage>();
        private Kernel? _kernel;
        private bool _isInitialized = false;

        public SemanticKernelTextGenerationService(
            IOptions<AppConfiguration> config,
            IMessageCreator messageCreator,
            ILogger<SemanticKernelTextGenerationService> logger)
        {
            _appConfiguration = config;
            _messageCreator = messageCreator;
            _logger = logger;
        }

        public async Task InitializeAsync(Action<double>? onProgress = null)
        {
            try
            {
                onProgress?.Invoke(10);

                var selectedModel =
                    AvailableModels.Models.FirstOrDefault(x => x.Name == _appConfiguration.Value.GenerationProvider);
                if (selectedModel == null)
                {
                    throw new InvalidOperationException(
                        $"Ausgewähltes Model {_appConfiguration.Value.GenerationProvider} ist nicht bekannt");
                }

                _logger.LogInformation("Initialisiere Semantic Kernel Service mit LlamaSharp...");

                // Pfad zum ApplicationData-Verzeichnis
                _appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var myAppDataPath = Path.Combine(_appDataPath, "LocalAiDemo");

                if (Directory.Exists(myAppDataPath) == false)
                {
                    Directory.CreateDirectory(myAppDataPath);
                }

                _modelFilePath = Path.Combine(_appDataPath, "LocalAiDemo", selectedModel.FileName);

                onProgress?.Invoke(30);

                if (File.Exists(_modelFilePath) == false)
                {
                    // Modell herunterladen
                    await DownloadModelAsync(selectedModel.Url, _modelFilePath, onProgress);
                }
                else
                {
                    onProgress?.Invoke(70);
                }

                // Semantic Kernel erstellen und LlamaSharp ChatClient hinzufügen
                var builder = Kernel.CreateBuilder();

                // Plugins hinzufügen
                builder.Plugins.AddFromObject(new MessageCreatorPlugin(_messageCreator), "MessageCreator");
                builder.Plugins.AddFromObject(new ContactPlugin(_messageCreator), "Contact");

                _kernel = builder.Build();

                onProgress?.Invoke(100);
                _isInitialized = true;

                _logger.LogInformation("Semantic Kernel Service mit LlamaSharp erfolgreich initialisiert");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Initialisieren des Semantic Kernel Service");
                throw;
            }
        }

        public async Task StartChatAsync(Action<double>? onProgress = null)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Service ist nicht initialisiert. Rufe InitializeAsync() zuerst auf.");
                return;
            }

            try
            {
                var parameters = new ModelParams(_modelFilePath)
                {
                    ContextSize = 4096,
                };

                var progress = new Progress<float>(p => onProgress?.Invoke(p * 100));
                _model = await LLamaWeights.LoadFromFileAsync(parameters, CancellationToken.None, progress);

                // Initialize LlamaSharp context and executor for Semantic Kernel integration
                _context = _model.CreateContext(parameters);
                _executor = new InteractiveExecutor(_context);

                // Create Semantic Kernel builder
                var builder = Kernel.CreateBuilder();

                // Add LlamaSharp ChatCompletion service to the kernel
                builder.Services.AddKeyedSingleton<IChatCompletionService>("llama",
                    (serviceProvider, key) => { return new LLamaSharpChatCompletion(_executor); });

                // Add plugins for function calling
                builder.Plugins.AddFromObject(new MessageCreatorPlugin(_messageCreator), "MessageCreator");
                builder.Plugins.AddFromObject(new ContactPlugin(_messageCreator), "Contact");

                // Build the kernel
                _kernel = builder.Build();

                // Chat Client für Fallback-Implementierung
                _chatClient = _executor.AsChatClient();

                // Chat History initialisieren
                _chatHistory.Clear();                // Systemnachricht
                var systemMessage = Constants.SystemMessage + "\n\n";
                systemMessage +=
                    "Du kannst die verfügbaren Funktionen verwenden, um Nachrichten zu erstellen und Kontakte zu verwalten.\n\n" +
                    "WICHTIGE REGELN für Function Calls:\n" +
                    "- Verwende Funktionen nur, wenn du sicher bist, dass alle erforderlichen Informationen vorhanden sind\n" +
                    "- Wenn ein Benutzer nach einem Kontakt fragt, der nicht existiert, antworte normal ohne Function Call\n" +
                    "- Wenn du unsicher bist oder mehr Informationen benötigst, frage den Benutzer nach Details\n" +
                    "- Function Calls sollten nur verwendet werden, wenn du eine konkrete Aktion ausführen kannst\n" +
                    "- NACH einem Function Call: Gib IMMER eine normale, freundliche Antwort an den Benutzer zurück\n" +
                    "- Erkläre dem Benutzer, was du getan hast, in normaler Sprache\n" +
                    "- Andernfalls antworte normal als hilfsreicher Assistent.";

                _chatHistory.Add(new ChatMessage(ChatRole.System, systemMessage));
                _chatHistory.Add(new ChatMessage(ChatRole.Assistant, Constants.AgentWelcomeMessage));

                _logger.LogInformation(
                    "Chat mit Semantic Kernel Function Calling und InteractiveExecutor initialisiert");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Starten der Chat-Session");
                throw;
            }
        }

        public async Task<string> InferAsync(string message)
        {
            if (!_isInitialized || _kernel == null || _executor == null)
            {
                _logger.LogWarning("Service ist nicht initialisiert");
                return "Fehler: Service ist nicht initialisiert. Bitte starten Sie die Anwendung neu.";
            }

            try
            {
                _logger.LogInformation("Verarbeite Nachricht: {Message}", message);

                _chatHistory.Add(new ChatMessage(ChatRole.User, message));

                // Semantic Kernel ChatCompletion Service abrufen
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

                // Chat History in Semantic Kernel Format konvertieren
                var skChatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();

                foreach (var msg in _chatHistory)
                {
                    switch (msg.Role.Value)
                    {
                        case "system":
                            skChatHistory.AddSystemMessage(msg.Text ?? "");
                            break;

                        case "user":
                            skChatHistory.AddUserMessage(msg.Text ?? "");
                            break;

                        case "assistant":
                            skChatHistory.AddAssistantMessage(msg.Text ?? "");
                            break;
                    }
                }

                // Execution Settings mit Function Calling aktivieren
                var executionSettings = new PromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                };

                // Chat-Antwort mit automatischem Function Calling generieren
                var response = await chatCompletionService.GetChatMessageContentAsync(
                    skChatHistory,
                    executionSettings,
                    _kernel);

                // Antwort zum Chat History hinzufügen
                _chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.Content ?? ""));

                _logger.LogInformation("Antwort generiert: {Response}", response.Content);
                return response.Content ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Generieren der Antwort");
                return $"Fehler beim Generieren der Antwort: {ex.Message}";
            }
        }

        /// <summary>
        /// Lädt das Modell herunter und speichert es im Zielverzeichnis.
        /// </summary>
        private async Task DownloadModelAsync(string url, string destinationPath, Action<double>? onProgress)
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Fehler beim Herunterladen des Modells: {response.StatusCode}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var downloadedBytes = 0L;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                fileStream.Write(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progress = (double)downloadedBytes / totalBytes * 100;
                    onProgress?.Invoke(progress);
                }
            }
        }
    }
}