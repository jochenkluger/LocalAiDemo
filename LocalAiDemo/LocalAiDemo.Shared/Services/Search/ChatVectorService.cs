using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Chat;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Search
{
    public class ChatVectorService : IChatVectorService
    {
        private readonly IChatVectorizationService _vectorizationService;
        private readonly IAdvancedVectorService _advancedVectorService;
        private readonly IChatDatabaseService _chatDatabase;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ChatVectorService> _logger;

        public ChatVectorService(
            IChatVectorizationService vectorizationService,
            IAdvancedVectorService advancedVectorService,
            IChatDatabaseService chatDatabase,
            IEmbeddingService embeddingService,
            ILogger<ChatVectorService> logger)
        {
            _vectorizationService = vectorizationService;
            _advancedVectorService = advancedVectorService;
            _chatDatabase = chatDatabase;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<VectorizationResult> VectorizeAllUnprocessedAsync()
        {
            _logger.LogInformation("Starting vectorization of all unprocessed content...");

            try
            {
                var startTime = DateTime.Now;

                // Get initial stats
                var initialStats = await _vectorizationService.GetVectorizationStatsAsync();

                // Vectorize chats
                var chatsProcessed = await _vectorizationService.VectorizeUnprocessedChatsAsync();

                // Vectorize messages
                var messagesProcessed = await _vectorizationService.VectorizeUnprocessedMessagesAsync();

                // Get final stats
                var finalStats = await _vectorizationService.GetVectorizationStatsAsync();

                var duration = DateTime.Now - startTime;

                var result = new VectorizationResult
                {
                    ChatsProcessed = chatsProcessed,
                    MessagesProcessed = messagesProcessed,
                    InitialStats = initialStats,
                    FinalStats = finalStats,
                    Duration = duration,
                    Success = true
                };

                _logger.LogInformation(
                    "Vectorization completed. Processed {ChatsProcessed} chats and {MessagesProcessed} messages in {Duration}",
                    chatsProcessed, messagesProcessed, duration);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during vectorization: {ErrorMessage}", ex.Message);

                return new VectorizationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = TimeSpan.Zero
                };
            }
        }

        public async Task<VectorizationResult> ReVectorizeAllAsync()
        {
            _logger.LogInformation("Starting re-vectorization of all content...");

            try
            {
                var startTime = DateTime.Now;

                // Get initial stats
                var initialStats = await _vectorizationService.GetVectorizationStatsAsync();

                // Re-vectorize chats
                var chatsProcessed = await _vectorizationService.ReVectorizeAllChatsAsync();

                // Re-vectorize messages
                var messagesProcessed = await _vectorizationService.ReVectorizeAllMessagesAsync();

                // Rebuild indices
                await _advancedVectorService.RebuildVectorIndicesAsync();

                // Get final stats
                var finalStats = await _vectorizationService.GetVectorizationStatsAsync();

                var duration = DateTime.Now - startTime;

                var result = new VectorizationResult
                {
                    ChatsProcessed = chatsProcessed,
                    MessagesProcessed = messagesProcessed,
                    InitialStats = initialStats,
                    FinalStats = finalStats,
                    Duration = duration,
                    Success = true
                };

                _logger.LogInformation(
                    "Re-vectorization completed. Processed {ChatsProcessed} chats and {MessagesProcessed} messages in {Duration}",
                    chatsProcessed, messagesProcessed, duration);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during re-vectorization: {ErrorMessage}", ex.Message);

                return new VectorizationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = TimeSpan.Zero
                };
            }
        }

        public async Task<VectorizationStats> GetStatsAsync()
        {
            return await _vectorizationService.GetVectorizationStatsAsync();
        }

        public async Task<List<Models.Chat>> FindSimilarChatsAsync(string query, int limit = 5)
        {
            _logger.LogDebug("Finding similar chats for query: '{Query}'", query);
            try
            {
                // Use the injected embedding service
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);
                return await _chatDatabase.FindSimilarChatsAsync(queryEmbedding, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar chats: {ErrorMessage}", ex.Message);
                return new List<Models.Chat>();
            }
        }

        public async Task<List<ChatMessage>> FindSimilarMessagesAsync(string query, int limit = 10)
        {
            return await _vectorizationService.FindSimilarMessagesAsync(query, limit);
        }

        public async Task<List<ChatSearchResult>> HybridSearchAsync(string query, int limit = 10)
        {
            return await _advancedVectorService.HybridSearchAsync(query, limit);
        }

        public async Task<int> CleanupAsync()
        {
            _logger.LogInformation("Starting vector database cleanup...");

            try
            {
                // Use both cleanup methods
                var vectorizationCleanup = await _vectorizationService.CleanupOrphanedVectorsAsync();
                var advancedCleanup = await _advancedVectorService.CleanupOrphanedVectorEntriesAsync();

                var totalCleaned = vectorizationCleanup + advancedCleanup;
                _logger.LogInformation("Cleanup completed. Removed {TotalCleaned} orphaned entries", totalCleaned);

                return totalCleaned;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task RebuildIndicesAsync()
        {
            await _advancedVectorService.RebuildVectorIndicesAsync();
        }

        public async Task<List<ChatCluster>> FindChatClustersAsync(int maxClusters = 5)
        {
            return await _advancedVectorService.FindChatClustersAsync(maxClusters);
        }

        public async Task<ChatSimilarityStats> GetChatSimilarityStatsAsync(int chatId)
        {
            return await _advancedVectorService.GetChatSimilarityStatsAsync(chatId);
        }

        public async Task<VectorDatabaseStats> GetDatabaseStatsAsync()
        {
            return await _advancedVectorService.GetVectorDatabaseStatsAsync();
        }

        public async Task UpdateChatVectorAsync(int chatId)
        {
            await _vectorizationService.UpdateChatVectorAsync(chatId);
        }

        public async Task UpdateMessageVectorAsync(int messageId)
        {
            await _vectorizationService.UpdateMessageVectorAsync(messageId);
        }
    }
}