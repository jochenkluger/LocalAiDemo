using System.ComponentModel;
using LocalAiDemo.Shared.Services.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LocalAiDemo.Shared.Services.Generation.Plugins
{
    /// <summary>
    /// Semantic Kernel Plugin für Chat-Suche Funktionen
    /// </summary>
    public class ChatSearchPlugin
    {
        private readonly IChatService _chatService;
        private readonly ILogger _logger;

        public ChatSearchPlugin(IChatService chatService, ILogger logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [KernelFunction("search_chat_history")]
        [Description(
            "Durchsucht die gesamte Chat-Historie semantisch nach relevanten Informationen zu einem bestimmten Thema oder einer Frage. Diese Funktion nutzt intelligente Vektorsuche, um auch thematisch ähnliche Gespräche zu finden, nicht nur exakte Textübereinstimmungen. Verwende diese Funktion, wenn der Benutzer nach Informationen aus vorherigen Gesprächen fragt oder wenn du Kontext aus der Chat-Historie benötigst.")]
        public async Task<string> SearchChatHistory(
            [Description("Suchbegriff oder Frage, nach der in der Chat-Historie gesucht werden soll")]
            string searchQuery,
            [Description("Maximale Anzahl der zurückzugebenden Ergebnisse (Standard: 5, Maximum: 20)")]
            int limit = 5)
        {
            try
            {
                if (string.IsNullOrEmpty(searchQuery))
                {
                    return "Fehler: Suchbegriff ist erforderlich.";
                }

                // Limit auf sinnvolles Maximum begrenzen
                if (limit <= 0) limit = 5;
                if (limit > 20) limit = 20;

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