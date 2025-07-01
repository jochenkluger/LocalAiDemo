using System.Text;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLamaSharp.SemanticKernel.ChatCompletion;
using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Chat;
using LocalAiDemo.Shared.Services.FunctionCalling;
using LocalAiDemo.Shared.Services.Generation;
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
        private readonly IChatService _chatService;
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
        private CancellationTokenSource? _currentOperationCts;

        public SemanticKernelTextGenerationService(
            IOptions<AppConfiguration> config,
            IMessageCreator messageCreator,
            IChatService chatService,
            ILogger<SemanticKernelTextGenerationService> logger)
        {
            _appConfiguration = config;
            _messageCreator = messageCreator;
            _chatService = chatService;
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
                // Validate model file before loading
                if (!File.Exists(_modelFilePath))
                {
                    _logger.LogError("Model file not found: {ModelPath}", _modelFilePath);
                    throw new FileNotFoundException($"Model file not found: {_modelFilePath}");
                }

                var fileInfo = new FileInfo(_modelFilePath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogError("Model file is empty: {ModelPath}", _modelFilePath);
                    throw new InvalidDataException($"Model file is empty: {_modelFilePath}");
                }

                _logger.LogInformation("Loading model from: {ModelPath} (Size: {Size:F1} MB)",
                    _modelFilePath, fileInfo.Length / (1024.0 * 1024.0));

                var parameters = new ModelParams(_modelFilePath)
                {
                    ContextSize = 4096,
                    GpuLayerCount = -1, // -1 = alle verfügbaren Layer auf GPU offloaden
                    Threads = Environment.ProcessorCount, // Alle CPU-Cores nutzen
                    BatchSize = 512, // Größere Batch-Size für bessere GPU-Auslastung
                };
                var progress = new Progress<float>(p => onProgress?.Invoke(p * 100));
                _model = await LLamaWeights.LoadFromFileAsync(parameters, CancellationToken.None, progress);

                _logger.LogInformation(
                    "LlamaSharp Modell geladen - GPU Layer: {GpuLayers}, Threads: {Threads}, Batch Size: {BatchSize}",
                    parameters.GpuLayerCount, parameters.Threads, parameters.BatchSize);

                // Initialize LlamaSharp context and executor for Semantic Kernel integration
                _context = _model.CreateContext(parameters);
                _executor = new InteractiveExecutor(_context);

                // Create Semantic Kernel builder
                var builder = Kernel.CreateBuilder();

                // Add LlamaSharp ChatCompletion service to the kernel
                builder.Services.AddKeyedSingleton<IChatCompletionService>("llama",
                    (serviceProvider, key) =>
                    {
                        return new LLamaSharpChatCompletion(_executor);
                    }); // Add plugins for function calling
                builder.Plugins.AddFromObject(new MessageCreatorPlugin(_messageCreator), "MessageCreator");
                builder.Plugins.AddFromObject(new ContactPlugin(_messageCreator), "Contact");

                // Create ChatSearchPlugin with generic ILogger
                builder.Plugins.AddFromObject(new ChatSearchPlugin(_chatService, _logger), "ChatSearch");

                // Build the kernel
                _kernel = builder.Build();

                // Chat Client für Fallback-Implementierung
                _chatClient = _executor.AsChatClient();

                // Chat History initialisieren
                _chatHistory.Clear(); // Systemnachricht
                var systemMessage = Constants.SystemMessage + "\n\n";
                systemMessage +=
                    "Du bist ein hilfreicher KI-Assistent mit Zugriff auf verschiedene Funktionen. " +
                    "Du kannst die verfügbaren Funktionen verwenden, um Nachrichten zu erstellen, Kontakte zu verwalten und Chat-Historie zu durchsuchen.\n\n" +
                    "VERFÜGBARE FUNKTIONEN:\n" +
                    "1. create_message(contactId, messageText) - Erstellt eine Nachricht für einen Kontakt\n" +
                    "2. get_available_contacts() - Ruft alle verfügbaren Kontakte ab\n" +
                    "3. search_contacts(searchTerm) - Sucht nach Kontakten\n" +
                    "4. get_contact_by_name(contactName) - Sucht einen spezifischen Kontakt\n" +
                    "5. search_chat_history(searchQuery, limit) - Durchsucht die Chat-Historie\n\n" +
                    "WICHTIGE REGELN für Function Calls:\n" +
                    "- Verwende Funktionen nur, wenn du sicher bist, dass alle erforderlichen Informationen vorhanden sind\n" +
                    "- Wenn ein Benutzer nach einem Kontakt fragt, der nicht existiert, antworte normal ohne Function Call\n" +
                    "- Du MUSST zuerst Kontakte abrufen (get_available_contacts, search_contacts oder get_contact_by_name), " +
                    "  bevor du eine Nachricht erstellen kannst (create_message)\n" +
                    "- Wenn du unsicher bist oder mehr Informationen benötigst, frage den Benutzer nach Details\n" +
                    "- Function Calls sollten nur verwendet werden, wenn du eine konkrete Aktion ausführen kannst\n" +
                    "- NACH einem Function Call: Gib IMMER eine normale, freundliche Antwort an den Benutzer zurück\n" +
                    "- Erkläre dem Benutzer, was du getan hast, in normaler Sprache\n" +
                    "- Wenn der Benutzer nach vergangenen Gesprächen oder Informationen fragt, verwende search_chat_history\n" +
                    "- Andernfalls antworte normal als hilfreicher Assistent.\n\n" +
                    "BEISPIELE für Funktionsaufrufe:\n" +
                    "- Benutzer: 'Schreibe eine Nachricht an Max' → Zuerst get_contact_by_name('Max'), dann create_message(contactId, messageText)\n" +
                    "- Benutzer: 'Wer ist verfügbar?' → get_available_contacts()\n" +
                    "- Benutzer: 'Suche nach Müller' → search_contacts('Müller')\n" +
                    "- Benutzer: 'Was haben wir über Projekt X besprochen?' → search_chat_history('Projekt X', 5)";

                _chatHistory.Add(new ChatMessage(ChatRole.System, systemMessage));
                _chatHistory.Add(new ChatMessage(ChatRole.Assistant, Constants.AgentWelcomeMessage));

                _logger.LogInformation(
                    "Chat mit Semantic Kernel Function Calling und InteractiveExecutor initialisiert");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Starten der Chat-Session");

                // Try to recover from corrupted model file
                if (ex is LLama.Exceptions.LoadWeightsFailedException && File.Exists(_modelFilePath))
                {
                    _logger.LogWarning("Model file seems to be corrupted. Attempting to delete and re-download...");
                    try
                    {
                        File.Delete(_modelFilePath);
                        _logger.LogInformation(
                            "Corrupted model file deleted. Please restart the application to re-download.");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, "Failed to delete corrupted model file");
                    }
                }

                throw;
            }
        }

        public async Task<string> InferAsync(string message)
        {
            return await InferAsync(message, CancellationToken.None);
        }

        public async Task<string> InferAsync(string message, CancellationToken cancellationToken)
        {
            // Vorherige Operation abbrechen falls vorhanden
            _currentOperationCts?.Cancel();
            _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                return await InferInternalAsync(message, _currentOperationCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Text-Generierung wurde abgebrochen");
                throw;
            }
            finally
            {
                _currentOperationCts?.Dispose();
                _currentOperationCts = null;
            }
        }

        public void CancelCurrentOperation()
        {
            _logger.LogInformation("CancelCurrentOperation aufgerufen");
            _currentOperationCts?.Cancel();
            _logger.LogInformation("Aktuelle Text-Generierung wird abgebrochen - CancellationTokenSource.Cancel() aufgerufen");
        }

        private async Task<string> InferInternalAsync(string message, CancellationToken cancellationToken)
        {
            if (!_isInitialized || _kernel == null || _executor == null)
            {
                _logger.LogWarning("Service ist nicht initialisiert");
                return "Fehler: Service ist nicht initialisiert. Bitte starten Sie die Anwendung neu.";
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Verarbeite Nachricht: {Message}", message);

                // Check if this model supports function calling
                _logger.LogInformation("Model: {Model}, Chat History Count: {Count}",
                    _appConfiguration.Value.GenerationProvider, _chatHistory.Count);
                _chatHistory.Add(new ChatMessage(ChatRole.User, message));

                cancellationToken.ThrowIfCancellationRequested();

                // Semantic Kernel ChatCompletion Service abrufen
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Available plugins: {Plugins}",
                    string.Join(", ", _kernel.Plugins.Select(p => p.Name)));

                _logger.LogInformation("Available functions: {Functions}",
                    string.Join(", ", _kernel.Plugins.SelectMany(p => p).Select(f => f.Name)));

                // Log detailed plugin info on first message
                if (_chatHistory.Count <= 2) // System + welcome message
                {
                    _logger.LogInformation("Plugin Details:\n{PluginInfo}", GetPluginInfo());
                }

                cancellationToken.ThrowIfCancellationRequested();

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
                } // Execution Settings mit Function Calling aktivieren

                var executionSettings = new PromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["temperature"] = 0.7,
                        ["max_tokens"] = 1000,
                        ["top_p"] = 0.9
                    }
                };

                _logger.LogInformation("Sending request with {FunctionCount} available functions",
                    _kernel.Plugins.SelectMany(p => p).Count());

                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("CancellationToken status before LLM call: IsCancelled={IsCancelled}", cancellationToken.IsCancellationRequested);

                // Chat-Antwort mit automatischem Function Calling generieren
                // Verwende eine robuste Task-basierte Lösung mit Timeout für bessere Cancellation-Unterstützung
                var llmTask = chatCompletionService.GetChatMessageContentAsync(
                    skChatHistory,
                    executionSettings,
                    _kernel,
                    CancellationToken.None); // Verwende KEIN CancellationToken hier, da LlamaSharp es nicht unterstützt

                // Implementiere eigene Cancellation-Logik
                var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
                var completedTask = await Task.WhenAny(llmTask, cancellationTask);

                if (completedTask == cancellationTask)
                {
                    _logger.LogInformation("LLM call wurde durch CancellationToken abgebrochen - werfe OperationCanceledException");
                    cancellationToken.ThrowIfCancellationRequested(); // Das wirft die Exception
                }

                _logger.LogInformation("LLM call hat normal beendet, hole Ergebnis...");
                var response = await llmTask;

                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("LLM call completed successfully, CancellationToken status: IsCancelled={IsCancelled}", cancellationToken.IsCancellationRequested);

                _logger.LogInformation("Received response. Function calls attempted: {HasFunctionCalls}",
                    !string.IsNullOrEmpty(response.Content) && response.Content.Contains("function"));

                // Antwort zum Chat History hinzufügen
                _chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.Content ?? ""));

                _logger.LogInformation("Antwort generiert: {Response}", response.Content);

                // Fallback für manuelles Function Calling versuchen
                return await TryManualFunctionCallingAsync(message, response.Content ?? "", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Text-Generierung wurde abgebrochen (OperationCanceledException)");
                throw; // Re-throw to propagate cancellation
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

        /// <summary>
        /// Fallback method for manual function calling when Semantic Kernel auto function calling
        /// doesn't work
        /// </summary>
        private async Task<string> TryManualFunctionCallingAsync(string userMessage, string aiResponse, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Check if the AI response contains function call instructions
                if (ContainsFunctionCallInstructions(aiResponse))
                {
                    _logger.LogInformation("AI response contains function call instructions, executing them...");
                    _logger.LogInformation("AI response to parse: {AiResponse}", aiResponse);
                    var functionResult = await ExecuteFunctionCallsFromResponse(aiResponse, userMessage, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation("Function execution result: {FunctionResult}", functionResult ?? "(empty)");

                    if (!string.IsNullOrEmpty(functionResult))
                    {
                        var finalResult = $"Ich habe folgende Aktionen für Sie ausgeführt:\n\n{functionResult}";
                        _logger.LogInformation("Returning function result: {FinalResult}", finalResult);
                        return finalResult;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Function execution returned empty result, falling back to original AI response");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Check if the AI response suggests a function call should be made
                if (ShouldAttemptFunctionCall(userMessage, aiResponse))
                {
                    _logger.LogInformation("Attempting manual function calling for user message: {Message}",
                        userMessage);

                    // Try to execute function calls manually based on user intent
                    var functionResult = await ExecuteManualFunctionCall(userMessage, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.IsNullOrEmpty(functionResult))
                    {
                        // Generate a follow-up response that incorporates the function result
                        var followUpPrompt =
                            $"Basierend auf der Benutzeranfrage '{userMessage}' habe ich folgende Funktion ausgeführt und folgendes Ergebnis erhalten:\n\n{functionResult}\n\nBitte antworte dem Benutzer freundlich und erkläre, was geschehen ist.";

                        var skChatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
                        skChatHistory.AddSystemMessage(
                            "Du bist ein hilfsreicher Assistent. Erkläre dem Benutzer freundlich, was passiert ist.");
                        skChatHistory.AddUserMessage(followUpPrompt);

                        var chatCompletionService = _kernel!.GetRequiredService<IChatCompletionService>();
                        
                        // Verwende robuste Cancellation-Logik auch hier
                        var followUpTask = chatCompletionService.GetChatMessageContentAsync(skChatHistory, cancellationToken: CancellationToken.None);
                        var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
                        var completedTask = await Task.WhenAny(followUpTask, cancellationTask);

                        if (completedTask == cancellationTask)
                        {
                            _logger.LogInformation("Follow-up LLM call wurde durch CancellationToken abgebrochen - werfe OperationCanceledException");
                            cancellationToken.ThrowIfCancellationRequested(); // Das wirft die Exception
                        }

                        var followUpResponse = await followUpTask;

                        return followUpResponse.Content ?? aiResponse;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Manual function calling wurde abgebrochen (OperationCanceledException)");
                throw; // Re-throw to propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manual function calling fallback");
            }

            return aiResponse;
        }

        /// <summary>
        /// Checks if the AI response contains function call instructions
        /// </summary>
        private bool ContainsFunctionCallInstructions(string aiResponse)
        {
            var lowerResponse = aiResponse.ToLower();
            var functionNames = new[]
            {
                "get_contact_by_name", "get_available_contacts", "search_contacts", "create_message",
                "search_chat_history"
            };

            foreach (var functionName in functionNames)
            {
                // Check for direct function calls
                if (lowerResponse.Contains(functionName))
                {
                    _logger.LogInformation("Detected function call instruction: {FunctionName} in response: {Response}",
                        functionName, aiResponse);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Executes function calls based on AI response instructions
        /// </summary>
        private async Task<string> ExecuteFunctionCallsFromResponse(string aiResponse, string userMessage, CancellationToken cancellationToken)
        {
            var results = new List<string>();
            Contact? foundContact = null;

            try
            {
                // Parse get_contact_by_name calls - improved to handle numbered lists
                var contactNameMatches = System.Text.RegularExpressions.Regex.Matches(aiResponse,
                    @"(?:\d+\.\s*)?get_contact_by_name\s*\(\s*['""]([^'""]+)['""]?\s*\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in contactNameMatches)
                {
                    var contactName = match.Groups[1].Value;
                    _logger.LogInformation("Executing get_contact_by_name with: {ContactName}", contactName);

                    var contact = await _messageCreator.GetContactByName(contactName);
                    if (contact != null)
                    {
                        foundContact = contact; // Store found contact for potential message creation
                        results.Add($"Kontakt gefunden: {contact.Name} (ID: {contact.Id}, E-Mail: {contact.Email})");
                    }
                    else
                    {
                        results.Add($"Kontakt '{contactName}' wurde nicht gefunden.");
                    }
                }

                // Parse create_message calls with various formats - improved to handle numbered lists
                var messagePatterns = new[]
                {
                    @"(?:\d+\.\s*)?create_message\s*\(\s*(\d+)\s*,\s*['""]([^'""]*)['""]?\s*\)", // create_message(123, "message")
                    @"(?:\d+\.\s*)?create_message\s*\(\s*contact_id\s*,\s*['""]([^'""]*)['""]?\s*\)", // create_message(contact_id, "message")
                    @"(?:\d+\.\s*)?create_message\s*\(\s*contactId\s*,\s*['""]([^'""]*)['""]?\s*\)" // create_message(contactId, "message")
                };

                bool messageCreated = false;
                foreach (var pattern in messagePatterns)
                {
                    var messageMatches = System.Text.RegularExpressions.Regex.Matches(aiResponse, pattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    foreach (System.Text.RegularExpressions.Match messageMatch in messageMatches)
                    {
                        if (messageCreated) break; // Avoid creating duplicate messages

                        string messageText;
                        int contactId;

                        if (messageMatch.Groups.Count == 3)
                        {
                            // Pattern with contact ID and message
                            if (int.TryParse(messageMatch.Groups[1].Value, out contactId))
                            {
                                messageText = messageMatch.Groups[2].Value;
                            }
                            else
                            {
                                // Pattern with contact_id/contactId variable, use found contact
                                messageText = messageMatch.Groups[1].Value;
                                contactId = foundContact?.Id ?? 0;
                            }
                        }
                        else
                        {
                            // Pattern with contact_id/contactId variable
                            messageText = messageMatch.Groups[1].Value;
                            contactId = foundContact?.Id ?? 0;
                        }

                        if (string.IsNullOrEmpty(messageText))
                        {
                            // Extract message content from user request
                            messageText = ExtractMessageFromUserRequest(userMessage, foundContact?.Name ?? "");
                        }

                        if (contactId > 0 && !string.IsNullOrEmpty(messageText))
                        {
                            _logger.LogInformation(
                                "Executing create_message for contact {ContactId} with message: {Message}", contactId,
                                messageText);

                            var messageId = await _messageCreator.CreateMessage(contactId, messageText);
                            var contactName = foundContact?.Name ?? $"Kontakt {contactId}";
                            results.Add($"Nachricht erfolgreich an {contactName} gesendet (ID: {messageId})");
                            messageCreated = true;
                        }
                        else
                        {
                            results.Add("Fehler: Kontakt-ID oder Nachrichtentext fehlt für create_message.");
                        }
                    }

                    if (messageCreated) break;
                } // If no explicit create_message was found but user wants to send a message and we found a contact

                if (!messageCreated && foundContact != null && ContainsMessageIntent(userMessage))
                {
                    var contactName = foundContact.Name ?? "Unknown";
                    var messageText = ExtractMessageFromUserRequest(userMessage, contactName);
                    if (!string.IsNullOrEmpty(messageText))
                    {
                        _logger.LogInformation(
                            "Implicit message creation for contact {ContactId} with message: {Message}",
                            foundContact.Id, messageText);

                        var messageId = await _messageCreator.CreateMessage(foundContact.Id, messageText);
                        results.Add($"Nachricht erfolgreich an {foundContact.Name} gesendet (ID: {messageId})");
                    }
                }

                // Parse get_available_contacts calls
                if (aiResponse.ToLower().Contains("get_available_contacts"))
                {
                    _logger.LogInformation("Executing get_available_contacts");
                    var contacts = await _messageCreator.GetAvailableContacts();
                    if (contacts?.Any() == true)
                    {
                        var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}")
                            .ToList();
                        results.Add($"Verfügbare Kontakte:\n{string.Join("\n", contactList)}");
                    }
                    else
                    {
                        results.Add("Keine Kontakte verfügbar.");
                    }
                } // Parse search_contacts calls - improved to handle numbered lists

                var searchMatches = System.Text.RegularExpressions.Regex.Matches(aiResponse,
                    @"(?:\d+\.\s*)?search_contacts\s*\(\s*['""]([^'""]+)['""]?\s*\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in searchMatches)
                {
                    var searchTerm = match.Groups[1].Value;
                    _logger.LogInformation("Executing search_contacts with: {SearchTerm}", searchTerm);

                    var contacts = await _messageCreator.SearchContacts(searchTerm);
                    if (contacts?.Any() == true)
                    {
                        var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}")
                            .ToList();
                        results.Add($"Gefundene Kontakte für '{searchTerm}':\n{string.Join("\n", contactList)}");
                    }
                    else
                    {
                        results.Add($"Keine Kontakte gefunden für: {searchTerm}");
                    }
                } // Parse search_chat_history calls - improved regex to handle numbered lists and various formats

                var chatSearchMatches = System.Text.RegularExpressions.Regex.Matches(aiResponse,
                    @"(?:\d+\.\s*)?search_chat_history\s*\(\s*['""]([^'""]+)['""]?\s*(?:,\s*(\d+))?\s*\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in chatSearchMatches)
                {
                    var searchQuery = match.Groups[1].Value;
                    var limitStr = match.Groups[2].Value;
                    var limit = int.TryParse(limitStr, out var parsedLimit) ? parsedLimit : 5;

                    _logger.LogInformation("Executing search_chat_history with query: {SearchQuery}, limit: {Limit}",
                        searchQuery, limit);

                    var chatResults = await _chatService.SearchChatSegmentsAsync(searchQuery, limit);
                    if (chatResults?.Any() == true)
                    {
                        var resultText = string.Join("\n", chatResults.Select(r =>
                            $"- {r.Segment.CombinedContent?.Substring(0, Math.Min(150, r.Segment.CombinedContent.Length))}..." +
                            $" (Relevanz: {r.SimilarityScore:F2})"));
                        results.Add($"Chat-Historie Suchergebnisse für '{searchQuery}':\n{resultText}");
                    }
                    else
                    {
                        results.Add($"Keine Ergebnisse in der Chat-Historie für: {searchQuery}");
                    }
                }

                if (results.Any())
                {
                    return string.Join("\n\n", results);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing function calls from AI response");
                return $"Fehler beim Ausführen der Funktionen: {ex.Message}";
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts message content from user request
        /// </summary>
        private string ExtractMessageFromUserRequest(string userMessage, string contactName)
        {
            var lowerMessage = userMessage.ToLower();

            // Look for message content after "dass"
            var dassIndex = lowerMessage.IndexOf("dass ");
            if (dassIndex >= 0)
            {
                var messageContent = userMessage.Substring(dassIndex + 5).Trim();
                // Remove trailing punctuation
                messageContent = messageContent.TrimEnd('.', '!', '?');

                return $"Hallo {contactName},\n\n{messageContent}.\n\nViele Grüße\nJochen Kluger";
            }

            // Look for quoted content
            var quoteIndex = lowerMessage.IndexOf('"');
            if (quoteIndex >= 0)
            {
                var endQuoteIndex = lowerMessage.IndexOf('"', quoteIndex + 1);
                if (endQuoteIndex > quoteIndex)
                {
                    var messageContent = userMessage.Substring(quoteIndex + 1, endQuoteIndex - quoteIndex - 1);
                    return $"Hallo {contactName},\n\n{messageContent}\n\nViele Grüße\nJochen Kluger";
                }
            }

            // Default message
            return $"Hallo {contactName},\n\nIch hoffe, es geht Ihnen gut.\n\nViele Grüße\nJochen Kluger";
        }

        /// <summary>
        /// Determines if a function call should be attempted based on user message and AI response
        /// </summary>
        private bool ShouldAttemptFunctionCall(string userMessage, string aiResponse)
        {
            var lowerMessage = userMessage.ToLower();

            // Check for keywords that suggest function calls
            return lowerMessage.Contains("nachricht") &&
                   (lowerMessage.Contains("schreib") || lowerMessage.Contains("send")) ||
                   lowerMessage.Contains("kontakt") && (lowerMessage.Contains("such") ||
                                                        lowerMessage.Contains("find") ||
                                                        lowerMessage.Contains("zeig")) ||
                   lowerMessage.Contains("verfügbar") ||
                   lowerMessage.Contains("chat") && lowerMessage.Contains("such") ||
                   lowerMessage.Contains("gespräch") && lowerMessage.Contains("such");
        }

        /// <summary>
        /// Executes function calls manually based on user intent
        /// </summary>
        private async Task<string> ExecuteManualFunctionCall(string userMessage, CancellationToken cancellationToken)
        {
            var lowerMessage = userMessage.ToLower();

            try
            {
                // Contact lookup
                if (lowerMessage.Contains("kontakt") || lowerMessage.Contains("verfügbar"))
                {
                    if (lowerMessage.Contains("such"))
                    {
                        // Extract search term
                        var searchTerm = ExtractSearchTerm(userMessage);
                        if (!string.IsNullOrEmpty(searchTerm))
                        {
                            var contacts = await _messageCreator.SearchContacts(searchTerm);
                            if (contacts?.Any() == true)
                            {
                                var contactList = contacts
                                    .Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}").ToList();
                                return $"Gefundene Kontakte für '{searchTerm}':\n{string.Join("\n", contactList)}";
                            }
                            else
                            {
                                return $"Keine Kontakte gefunden für: {searchTerm}";
                            }
                        }
                    }
                    else
                    {
                        // Get all available contacts
                        var contacts = await _messageCreator.GetAvailableContacts();
                        if (contacts?.Any() == true)
                        {
                            var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}")
                                .ToList();
                            return $"Verfügbare Kontakte:\n{string.Join("\n", contactList)}";
                        }
                        else
                        {
                            return "Keine Kontakte verfügbar.";
                        }
                    }
                }

                // Message creation
                if (lowerMessage.Contains("nachricht") &&
                    (lowerMessage.Contains("schreib") || lowerMessage.Contains("send")))
                {
                    var contactName = ExtractContactName(userMessage);
                    if (!string.IsNullOrEmpty(contactName))
                    {
                        var contact = await _messageCreator.GetContactByName(contactName);
                        if (contact != null)
                        {
                            var messageText = ExtractMessageText(userMessage) ??
                                              ExtractMessageFromUserRequest(userMessage, contact.Name);

                            var messageId = await _messageCreator.CreateMessage(contact.Id, messageText);
                            return $"Nachricht erfolgreich an {contact.Name} gesendet (ID: {messageId})";
                        }
                        else
                        {
                            return $"Kontakt '{contactName}' wurde nicht gefunden.";
                        }
                    }
                }

                // Chat history search
                if (lowerMessage.Contains("chat") && lowerMessage.Contains("such") ||
                    lowerMessage.Contains("gespräch") && lowerMessage.Contains("such"))
                {
                    var searchQuery = ExtractSearchQuery(userMessage);
                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        var results = await _chatService.SearchChatSegmentsAsync(searchQuery, 5);
                        if (results?.Any() == true)
                        {
                            var resultText = string.Join("\n", results.Select(r =>
                                $"- {r.Segment.CombinedContent?.Substring(0, Math.Min(100, r.Segment.CombinedContent.Length))}..."));
                            return $"Chat-Historie Suchergebnisse für '{searchQuery}':\n{resultText}";
                        }
                        else
                        {
                            return $"Keine Ergebnisse in der Chat-Historie für: {searchQuery}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing manual function call");
                return $"Fehler beim Ausführen der Funktion: {ex.Message}";
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts search term from user message
        /// </summary>
        private string ExtractSearchTerm(string message)
        {
            // Simple extraction logic - can be improved with NLP
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var searchKeywordIndex = Array.FindIndex(words, w => w.ToLower().Contains("such"));

            if (searchKeywordIndex >= 0 && searchKeywordIndex < words.Length - 1)
            {
                return words[searchKeywordIndex + 1].Trim('\"', '\'', ',', '.');
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts contact name from user message
        /// </summary>
        private string ExtractContactName(string message)
        {
            // Look for patterns like "an Max", "für Maria", etc.
            var patterns = new[] { "an ", "für ", "bei ", "mit " };

            foreach (var pattern in patterns)
            {
                var index = message.ToLower().IndexOf(pattern);
                if (index >= 0)
                {
                    var startIndex = index + pattern.Length;
                    var endIndex = message.IndexOfAny(new[] { ' ', ',', '.', '!', '?' }, startIndex);
                    if (endIndex == -1) endIndex = message.Length;

                    return message.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts message text from user message
        /// </summary>
        private string ExtractMessageText(string message)
        {
            // Look for quoted text or text after keywords
            var quotes = new[] { "\"", "'" };
            foreach (var quote in quotes)
            {
                var startIndex = message.IndexOf(quote);
                if (startIndex >= 0)
                {
                    var endIndex = message.IndexOf(quote, startIndex + 1);
                    if (endIndex > startIndex)
                    {
                        return message.Substring(startIndex + 1, endIndex - startIndex - 1);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts search query from user message for chat history search
        /// </summary>
        private string ExtractSearchQuery(string message)
        {
            // Extract everything after "nach" or similar keywords
            var keywords = new[] { "nach ", "über ", "zu " };

            foreach (var keyword in keywords)
            {
                var index = message.ToLower().IndexOf(keyword);
                if (index >= 0)
                {
                    var startIndex = index + keyword.Length;
                    var query = message.Substring(startIndex).Trim('?', '.', '!');
                    return query;
                }
            }

            return message; // Return the whole message as fallback
        }

        /// <summary>
        /// Gets information about all registered plugins and functions
        /// </summary>
        public string GetPluginInfo()
        {
            if (_kernel == null)
            {
                return "Kernel not initialized";
            }

            var info = new StringBuilder();
            info.AppendLine("=== Registered Plugins and Functions ===");

            foreach (var plugin in _kernel.Plugins)
            {
                info.AppendLine($"Plugin: {plugin.Name}");
                foreach (var function in plugin)
                {
                    info.AppendLine($"  - Function: {function.Name}");
                    info.AppendLine($"    Description: {function.Description}");
                    info.AppendLine($"    Parameters: {function.Metadata.Parameters.Count}");
                    foreach (var param in function.Metadata.Parameters)
                    {
                        info.AppendLine($"      - {param.Name} ({param.ParameterType}): {param.Description}");
                    }
                }

                info.AppendLine();
            }

            return info.ToString();
        }

        /// <summary>
        /// Checks if the user message contains intent to send a message
        /// </summary>
        private bool ContainsMessageIntent(string userMessage)
        {
            var lowerMessage = userMessage.ToLower();
            return lowerMessage.Contains("schreib") || lowerMessage.Contains("send") ||
                   lowerMessage.Contains("nachricht") || lowerMessage.Contains("message") ||
                   lowerMessage.Contains("mitteil") || lowerMessage.Contains("inform");
        }
    }
}