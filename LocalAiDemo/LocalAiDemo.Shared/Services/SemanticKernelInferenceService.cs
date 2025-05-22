//using System;
//using System.IO;
//using System.Net.Http;
//using System.Threading;
//using System.Threading.Tasks;
//using LLama.Common;
//using LLama;
//using Microsoft.Maui.Storage;
//using Microsoft.SemanticKernel;
//using LLamaSharp.SemanticKernel.ChatCompletion;
//using Microsoft.SemanticKernel.ChatCompletion;
//using LocalAiDemo.Models;
//using LocalAiDemo.Services;
//using LocalAiDemo.Shared.Models;

//public class SemanticKernelInferenceService
//{
//    private AIModel _selectedModel;
//    private string _modelFilePath;
//    private readonly string _appDataPath;
//    private readonly ModelSelectionService _modelSelectionService;
//    private Kernel _kernel;
//    private LLamaSharpChatCompletion _chat;
//    private LLamaWeights _model;
//    private Microsoft.SemanticKernel.ChatCompletion.ChatHistory _chatHistory;

// public SemanticKernelInferenceService(ModelSelectionService modelSelectionService) {
// _modelSelectionService = modelSelectionService;

// // Pfad zum ApplicationData-Verzeichnis _appDataPath =
// Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); var myAppDataPath =
// Path.Combine(_appDataPath, "LocalAiDemo");

// if (Directory.Exists(myAppDataPath) == false) { Directory.CreateDirectory(myAppDataPath); }

// // Subscribe to model changes _modelSelectionService.ModelChanged += OnModelChanged;

// // Set initial model UpdateModelSettings(); }

// private void UpdateModelSettings() { _selectedModel =
// _modelSelectionService.GetSelectedModel(ModelType.Logic); _modelFilePath =
// Path.Combine(_appDataPath, "LocalAiDemo", _selectedModel.FileName); }

// private void OnModelChanged(AIModel model) { // Nur auf Änderungen an Logic-Modellen reagieren if
// (model.Type == ModelType.Logic) { _selectedModel = model; _modelFilePath =
// Path.Combine(_appDataPath, "LocalAiDemo", _selectedModel.FileName);

// // Reset the model and chat history when model changes _model = null; _chatHistory = null; } }

// /// <summary> /// Initialisiert das Modell und Semantic Kernel. /// </summary> public async Task
// InitializeModelAsync(Action<double> onProgress = null) { // Ensure we have the latest model
// settings UpdateModelSettings();

// if (File.Exists(_modelFilePath) == false) { // Modell herunterladen await
// DownloadModelAsync(_selectedModel.Url, _modelFilePath, onProgress); } else {
// onProgress?.Invoke(100); } }

// public async Task InintializeChat(Action<double> onProgress = null) { var parameters = new
// ModelParams(_modelFilePath); var progress = new Progress<float>(p => onProgress?.Invoke(p *
// 100)); _model = await LLamaWeights.LoadFromFileAsync(parameters, CancellationToken.None,
// progress); var ex = new StatelessExecutor(_model, parameters); _chat = new
// LLamaSharpChatCompletion(ex); _chatHistory = _chat.CreateNewChat("Dies ist eine Konversation
// zwischen einem Assistenten einer Praxis für Psychotherapie für Kinder und Jugendliche und einem
// Therapeuten. \n\n " + "Du bist der Assistent. Du antwortest immer in der Sprache, in der du
// angesprochen wirst und hälst die Antwort kurz."); }

// /// <summary> /// Führt eine Inferenz mit dem initialisierten Semantic Kernel aus. /// </summary>
// public async Task<string> RunInferenceAsync(string input) { if (_chatHistory == null) { throw new
// InvalidOperationException("Das Modell wurde nicht initialisiert. Rufen Sie InitializeModelAsync
// auf."); }

// _chatHistory.AddUserMessage(input); var reply = await
// _chat.GetChatMessageContentAsync(_chatHistory); _chatHistory.AddAssistantMessage(reply.Content);
// return reply.Content; }

// /// <summary> /// Lädt das Modell herunter und speichert es im Zielverzeichnis. /// </summary>
// private async Task DownloadModelAsync(string url, string destinationPath, Action<double>
// onProgress) { using var httpClient = new HttpClient(); using var response = await
// httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

// if (!response.IsSuccessStatusCode) { throw new Exception($"Fehler beim Herunterladen des Modells:
// {response.StatusCode}"); }

// var totalBytes = response.Content.Headers.ContentLength ?? -1L; // -1L für unbekannte Größe var
// downloadedBytes = 0L;

// using var stream = await response.Content.ReadAsStreamAsync(); using var fileStream = new
// FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

// var buffer = new byte[8192]; int bytesRead;

// while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
// fileStream.Write(buffer, 0, bytesRead); downloadedBytes += bytesRead;

//            // Fortschritt berechnen und melden
//            if (totalBytes > 0) // Nur wenn die Größe bekannt ist
//            {
//                var progress = (double)downloadedBytes / totalBytes * 100;
//                onProgress?.Invoke(progress);
//            }
//        }
//    }
//}