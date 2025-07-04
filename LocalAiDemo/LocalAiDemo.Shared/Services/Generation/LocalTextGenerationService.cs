﻿using System.Diagnostics;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Chat;
using LocalAiDemo.Shared.Services.FunctionCalling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LocalAiDemo.Shared.Services.Generation
{
    public class LocalTextGenerationService : ITextGenerationService
    {
        private readonly IOptions<AppConfiguration> _appConfiguration;
        private readonly IMessageCreator _messageCreator;
        private readonly IChatService _chatService;
        private readonly ILogger<LocalTextGenerationService> _logger;
        private string _appDataPath = string.Empty;
        private string _modelFilePath = string.Empty;
        private LLamaWeights? _model;
        private IChatClient? _chatClient; //https://learn.microsoft.com/de-de/dotnet/ai/microsoft-extensions-ai
        private List<ChatMessage> _chatHistory = new List<ChatMessage>();
        private readonly List<FunctionCallingHelper.FunctionDescription> _availableFunctions = new();
        private CancellationTokenSource? _currentOperationCts;

        public LocalTextGenerationService(
            IOptions<AppConfiguration> config,
            IMessageCreator messageCreator,
            IChatService chatService,
            ILogger<LocalTextGenerationService> logger)
        {
            _appConfiguration = config;
            _messageCreator = messageCreator;
            _chatService = chatService;
            _logger = logger; // Verfügbare Funktionen definieren
            _availableFunctions = FunctionCallingHelper.GetAllFunctionDescriptions();
        }

        public async Task InitializeAsync(Action<double>? onProgress = null)
        {
            var selectedModel =
                AvailableModels.Models.FirstOrDefault(x => x.Name == _appConfiguration.Value.GenerationProvider);
            if (selectedModel == null)
            {
                throw new InvalidOperationException(
                    $"Ausgewähltes Model {_appConfiguration.Value.GenerationProvider} ist nicht bekannt");
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

        public async Task StartChatAsync(Action<double>? onProgress = null)
        {
            var parameters = new ModelParams(_modelFilePath)
            {
                ContextSize = 4096,
                GpuLayerCount = -1, 
                Threads = Environment.ProcessorCount, // Alle CPU-Cores nutzen
                BatchSize = 512, // Größere Batch-Size für bessere Auslastung
            };
            var progress = new Progress<float>(p => onProgress?.Invoke(p * 100));
            _model = await LLamaWeights.LoadFromFileAsync(parameters, CancellationToken.None, progress);

            _logger.LogInformation(
                "LlamaSharp Modell geladen - GPU Layer: {GpuLayers}, Threads: {Threads}, Batch Size: {BatchSize}",
                parameters.GpuLayerCount, parameters.Threads, parameters.BatchSize);

            var ex = new StatelessExecutor(_model, parameters);

            _chatClient = ex.AsChatClient();

            // Function Calling kann in LlamaSharp nicht direkt verwendet werden Stattdessen
            // erweitern wir die Systemnachricht mit Funktionsbeschreibungen
            _chatHistory.Clear();

            // Basis-Systemnachricht
            var systemMessage = Constants.SystemMessage + "\n\n";

            // Funktionsbeschreibungen hinzufügen
            systemMessage += FunctionCallingHelper.GenerateFunctionSystemMessage(_availableFunctions);

            _chatHistory.Add(new ChatMessage(ChatRole.System, systemMessage));
            _chatHistory.Add(new ChatMessage(ChatRole.Assistant, Constants.AgentWelcomeMessage));

            _logger.LogInformation("Chat mit Function Calling initialisiert");
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
            _currentOperationCts?.Cancel();
            _logger.LogInformation("Aktuelle Text-Generierung wird abgebrochen");
        }

        private async Task<string> InferInternalAsync(string message, CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();

            _chatHistory.Add(new ChatMessage(ChatRole.User, message));

            // Sicherstellen, dass _chatClient nicht null ist
            if (_chatClient == null)
            {
                _logger.LogError("Chat-Client wurde nicht initialisiert");
                return "Fehler: Chat-Client wurde nicht initialisiert.";
            }

            _logger.LogInformation("Starte Inferenz für Nachricht: {Message}", message);

            // Loop für mehrere Function Calls nacheinander
            const int maxFunctionCalls = 15; // Verhindert Endlosschleifen
            int functionCallCount = 0;
            var executedFunctions = new List<string>(); // Tracking der ausgeführten Funktionen

            while (functionCallCount < maxFunctionCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                stopwatch.Reset();
                stopwatch.Start();
                // LLM-Antwort generieren
                var response = await _chatClient.GetResponseAsync(_chatHistory, cancellationToken: cancellationToken);
                stopwatch.Stop();

                _logger.LogInformation("LLM Antwort nach {duration}ms: {response}", stopwatch.ElapsedMilliseconds,
                    response);

                cancellationToken.ThrowIfCancellationRequested();

                // Prüfen, ob die Antwort einen Funktionsaufruf enthält
                var functionCall = FunctionCallingHelper.ExtractFunctionCall(response.Text);
                if (functionCall == null)
                {
                    // Kein Function Call mehr - normale Antwort
                    _chatHistory.AddMessages(response);
                    _logger.LogInformation(
                        "Inferenz abgeschlossen. Ausgeführte Funktionen: [{Functions}]. Finale Antwort generiert.",
                        string.Join(", ", executedFunctions));
                    return response.Text;
                } // Function Call erkannt - ausführen

                functionCallCount++;
                executedFunctions.Add(functionCall.Name);
                _logger.LogInformation("Function Call {Count}/{Max} erkannt: {FunctionName}", functionCallCount,
                    maxFunctionCalls, functionCall.Name);

                string functionResponse = await ExecuteFunctionAsync(functionCall, cancellationToken);

                // Function Call und Antwort zum Chat-Verlauf hinzufügen
                _chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.Text));
                _chatHistory.Add(new ChatMessage(ChatRole.System, functionResponse));

                _logger.LogDebug("Function Call {FunctionName} ausgeführt. Antwort: {Response}", functionCall.Name,
                    functionResponse);
            } // Maximum erreicht - Warnung und letzte Antwort zurückgeben

            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogWarning(
                "Maximum von {MaxCalls} Function Calls erreicht. Ausgeführte Funktionen: [{Functions}]. Breche ab.",
                maxFunctionCalls, string.Join(", ", executedFunctions));
            var finalResponse = await _chatClient.GetResponseAsync(_chatHistory, cancellationToken: cancellationToken);
            _chatHistory.AddMessages(finalResponse);
            return finalResponse.Text;
        }

        /// <summary>
        /// Führt einen Function Call aus und gibt die Antwort zurück
        /// </summary>
        /// <param name="functionCall">Der auszuführende Function Call</param>
        /// <param name="cancellationToken">Token zum Abbrechen der Operation</param>
        /// <returns>Die Antwort der Funktion</returns>
        private async Task<string> ExecuteFunctionAsync(FunctionCallingHelper.FunctionCall functionCall, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogDebug("Führe Funktion {FunctionName} mit Argumenten aus: {Arguments}",
                    functionCall.Name,
                    string.Join(", ", functionCall.Arguments.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                var result = functionCall.Name.ToLowerInvariant() switch
                {
                    "createmessage" => await ExecuteCreateMessageFunction(functionCall, cancellationToken),
                    "getavailablecontacts" => await ExecuteGetAvailableContactsFunction(cancellationToken),
                    "searchcontacts" => await ExecuteSearchContactsFunction(functionCall, cancellationToken),
                    "getcontactbyname" => await ExecuteGetContactByNameFunction(functionCall, cancellationToken),
                    "searchchathistory" => await ExecuteSearchChatHistoryFunction(functionCall, cancellationToken),
                    _ =>
                        $"Unbekannte Funktion: {functionCall.Name}. Verfügbare Funktionen: CreateMessage, GetAvailableContacts, SearchContacts, GetContactByName, SearchChatHistory"
                };

                _logger.LogDebug("Funktion {FunctionName} erfolgreich ausgeführt", functionCall.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Ausführen der Funktion {FunctionName}", functionCall.Name);
                return $"Fehler beim Ausführen der Funktion {functionCall.Name}: {ex.Message}";
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

        /// <summary>
        /// Führt die CreateMessage-Funktion aus
        /// </summary>
        private async Task<string> ExecuteCreateMessageFunction(FunctionCallingHelper.FunctionCall functionCall, CancellationToken cancellationToken)
        {
            try
            {
                var contactIdStr = functionCall.Arguments.GetValueOrDefault("contactId")?.ToString() ?? "";
                var messageText = functionCall.Arguments.GetValueOrDefault("messageText")?.ToString() ?? "";

                if (string.IsNullOrEmpty(contactIdStr) || string.IsNullOrEmpty(messageText))
                {
                    return "Fehler: Kontakt-ID und Nachrichtentext sind erforderlich.";
                }

                if (!int.TryParse(contactIdStr, out int contactId))
                {
                    return "Fehler: Kontakt-ID muss eine gültige Zahl sein.";
                }

                var messageResponse = await _messageCreator.CreateMessage(contactId, messageText);
                return messageResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Ausführen der CreateMessage-Funktion");
                return $"Fehler beim Erstellen der Nachricht: {ex.Message}";
            }
        }

        /// <summary>
        /// Führt die GetAvailableContacts-Funktion aus
        /// </summary>
        private async Task<string> ExecuteGetAvailableContactsFunction(CancellationToken cancellationToken)
        {
            try
            {
                var contacts = await _messageCreator.GetAvailableContacts();

                if (contacts == null || !contacts.Any())
                {
                    return "Keine Kontakte verfügbar.";
                }

                var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}").ToList();
                return $"Verfügbare Kontakte:\n{string.Join("\n", contactList)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Abrufen der verfügbaren Kontakte");
                return $"Fehler beim Abrufen der Kontakte: {ex.Message}";
            }
        }

        /// <summary>
        /// Führt die SearchContacts-Funktion aus
        /// </summary>
        private async Task<string> ExecuteSearchContactsFunction(FunctionCallingHelper.FunctionCall functionCall, CancellationToken cancellationToken)
        {
            try
            {
                var searchTerm = functionCall.Arguments.GetValueOrDefault("searchTerm")?.ToString() ?? "";

                if (string.IsNullOrEmpty(searchTerm))
                {
                    return "Fehler: Suchbegriff ist erforderlich.";
                }

                var contacts = await _messageCreator.SearchContacts(searchTerm);

                if (contacts == null || !contacts.Any())
                {
                    return $"Keine Kontakte gefunden für Suchbegriff: {searchTerm}";
                }

                var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}").ToList();
                return $"Gefundene Kontakte für '{searchTerm}':\n{string.Join("\n", contactList)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Suchen von Kontakten");
                return $"Fehler bei der Kontaktsuche: {ex.Message}";
            }
        }

        /// <summary>
        /// Führt die GetContactByName-Funktion aus
        /// </summary>
        private async Task<string> ExecuteGetContactByNameFunction(FunctionCallingHelper.FunctionCall functionCall, CancellationToken cancellationToken)
        {
            try
            {
                var contactName = functionCall.Arguments.GetValueOrDefault("contactName")?.ToString() ?? "";

                if (string.IsNullOrEmpty(contactName))
                {
                    return "Fehler: Kontaktname ist erforderlich.";
                }

                var contact = await _messageCreator.GetContactByName(contactName);

                if (contact == null)
                {
                    return $"Kontakt '{contactName}' wurde nicht gefunden.";
                }

                return
                    $"Kontakt gefunden:\n- ID: {contact.Id}\n- Name: {contact.Name}\n- E-Mail: {contact.Email}\n- Telefon: {contact.Phone ?? "Nicht verfügbar"}\n- Notizen: {contact.Notes ?? "Keine Notizen"}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Abrufen des Kontakts nach Namen");
                return $"Fehler beim Abrufen des Kontakts: {ex.Message}";
            }
        }

        /// <summary>
        /// Führt die SearchChatHistory-Funktion aus
        /// </summary>
        private async Task<string> ExecuteSearchChatHistoryFunction(FunctionCallingHelper.FunctionCall functionCall, CancellationToken cancellationToken)
        {
            try
            {
                var searchQuery = functionCall.Arguments.GetValueOrDefault("searchQuery")?.ToString() ?? "";
                var limitValue = functionCall.Arguments.GetValueOrDefault("limit");

                if (string.IsNullOrEmpty(searchQuery))
                {
                    return "Fehler: Suchbegriff ist erforderlich.";
                }

                // Parse limit parameter
                int limit = 5; // Default value
                if (limitValue != null)
                {
                    if (int.TryParse(limitValue.ToString(), out var parsedLimit))
                    {
                        limit = Math.Max(1, Math.Min(20, parsedLimit)); // Between 1 and 20
                    }
                }

                _logger.LogInformation("Chat-Suche mit Query: {Query}, Limit: {Limit}", searchQuery, limit);

                var searchResults = await _chatService.SearchChatSegmentsAsync(searchQuery, limit);

                if (searchResults == null || !searchResults.Any())
                {
                    return $"Keine relevanten Informationen in der Chat-Historie für '{searchQuery}' gefunden.";
                }

                var resultTexts = searchResults.Select(result =>
                {
                    var contactName = result.Chat?.Contact?.Name ?? "Unbekannter Kontakt";
                    var department = result.Chat?.Contact?.Department ?? "";
                    var date = result.Segment.SegmentDate.ToShortDateString();
                    var relevance = Math.Round(result.SimilarityScore * 100, 1);

                    var departmentInfo = !string.IsNullOrEmpty(department) ? $" ({department})" : "";

                    return $"• {contactName}{departmentInfo} - {date} (Relevanz: {relevance}%)\n" +
                           $"  {result.HighlightedSnippet}";
                }).ToList();

                var responseText = $"Gefundene relevante Informationen in der Chat-Historie für '{searchQuery}':\n\n" +
                                   string.Join("\n\n", resultTexts);

                _logger.LogInformation("Chat-Suche erfolgreich: {ResultCount} Ergebnisse gefunden",
                    searchResults.Count);

                return responseText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Chat-Suche: {ErrorMessage}", ex.Message);
                return $"Fehler bei der Suche in der Chat-Historie: {ex.Message}";
            }
        }
    }
}