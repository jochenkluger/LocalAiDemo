using LocalAiDemo.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LocalAiDemo.Shared.Services.Search
{
    /// <summary>
    /// Advanced service for direct SQLite vector operations and maintenance
    /// </summary>
    public interface IAdvancedVectorService
    {
        /// <summary>
        /// Performs a hybrid search combining text search and vector similarity
        /// </summary>
        Task<List<ChatSearchResult>> HybridSearchAsync(string query, int limit = 10);

        /// <summary>
        /// Gets vector similarity statistics for a given chat
        /// </summary>
        Task<ChatSimilarityStats> GetChatSimilarityStatsAsync(int chatId);

        /// <summary>
        /// Finds clusters of similar chats
        /// </summary>
        Task<List<ChatCluster>> FindChatClustersAsync(int maxClusters = 5);

        /// <summary>
        /// Rebuilds the vector search indices
        /// </summary>
        Task RebuildVectorIndicesAsync();

        /// <summary>
        /// Gets detailed vector database statistics
        /// </summary>
        Task<VectorDatabaseStats> GetVectorDatabaseStatsAsync();

        /// <summary>
        /// Cleans up orphaned vector entries using direct SQL
        /// </summary>
        Task<int> CleanupOrphanedVectorEntriesAsync();
    }

    public class AdvancedVectorService : IAdvancedVectorService
    {
        private readonly IChatDatabaseService _chatDatabase;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<AdvancedVectorService> _logger;

        public AdvancedVectorService(
            IChatDatabaseService chatDatabase,
            IEmbeddingService embeddingService,
            ILogger<AdvancedVectorService> logger)
        {
            _chatDatabase = chatDatabase;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<List<ChatSearchResult>> HybridSearchAsync(string query, int limit = 10)
        {
            _logger.LogDebug("Performing hybrid search for: '{Query}'", query);

            try
            {
                var results = new List<ChatSearchResult>();

                // 1. Vector similarity search
                var queryEmbedding = _embeddingService.GenerateEmbedding(query);
                var similarChats = await _chatDatabase.FindSimilarChatsAsync(queryEmbedding, limit * 2);

                // 2. Text-based search
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var textMatches = allChats
                    .Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               c.Messages.Any(m => m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // 3. Combine and score results
                var combinedResults = new Dictionary<int, ChatSearchResult>();

                // Add vector similarity results
                foreach (var chat in similarChats)
                {
                    var similarity = chat.EmbeddingVector != null 
                        ? _embeddingService.CalculateCosineSimilarity(queryEmbedding, chat.EmbeddingVector)
                        : 0f;

                    combinedResults[chat.Id] = new ChatSearchResult
                    {
                        Chat = chat,
                        VectorSimilarity = similarity,
                        TextRelevance = 0f,
                        HybridScore = similarity * 0.7f // 70% weight for vector similarity
                    };
                }

                // Add text match results
                foreach (var chat in textMatches)
                {
                    var textRelevance = CalculateTextRelevance(chat, query);

                    if (combinedResults.ContainsKey(chat.Id))
                    {
                        // Update existing result
                        combinedResults[chat.Id].TextRelevance = textRelevance;
                        combinedResults[chat.Id].HybridScore += textRelevance * 0.3f; // 30% weight for text relevance
                    }
                    else
                    {
                        // Add new result
                        var similarity = chat.EmbeddingVector != null 
                            ? _embeddingService.CalculateCosineSimilarity(queryEmbedding, chat.EmbeddingVector)
                            : 0f;

                        combinedResults[chat.Id] = new ChatSearchResult
                        {
                            Chat = chat,
                            VectorSimilarity = similarity,
                            TextRelevance = textRelevance,
                            HybridScore = similarity * 0.7f + textRelevance * 0.3f
                        };
                    }
                }

                // Sort by hybrid score and return top results
                results = combinedResults.Values
                    .OrderByDescending(r => r.HybridScore)
                    .Take(limit)
                    .ToList();

                _logger.LogDebug("Hybrid search returned {ResultCount} results", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in hybrid search: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<ChatSimilarityStats> GetChatSimilarityStatsAsync(int chatId)
        {
            _logger.LogDebug("Calculating similarity stats for chat {ChatId}", chatId);

            try
            {
                var targetChat = await _chatDatabase.GetChatAsync(chatId);
                if (targetChat?.EmbeddingVector == null)
                {
                    return new ChatSimilarityStats { ChatId = chatId, HasVector = false };
                }

                var allChats = await _chatDatabase.GetAllChatsAsync();
                var otherChats = allChats.Where(c => c.Id != chatId && c.EmbeddingVector != null).ToList();

                var similarities = new List<float>();
                foreach (var chat in otherChats)
                {
                    var similarity = _embeddingService.CalculateCosineSimilarity(
                        targetChat.EmbeddingVector, chat.EmbeddingVector);
                    similarities.Add(similarity);
                }

                if (!similarities.Any())
                {
                    return new ChatSimilarityStats 
                    { 
                        ChatId = chatId, 
                        HasVector = true,
                        ComparableChatsCount = 0
                    };
                }

                return new ChatSimilarityStats
                {
                    ChatId = chatId,
                    HasVector = true,
                    ComparableChatsCount = similarities.Count,
                    AverageSimilarity = similarities.Average(),
                    MaxSimilarity = similarities.Max(),
                    MinSimilarity = similarities.Min(),
                    MedianSimilarity = GetMedian(similarities),
                    StandardDeviation = CalculateStandardDeviation(similarities)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating similarity stats for chat {ChatId}: {ErrorMessage}", 
                    chatId, ex.Message);
                throw;
            }
        }

        public async Task<List<ChatCluster>> FindChatClustersAsync(int maxClusters = 5)
        {
            _logger.LogDebug("Finding chat clusters (max: {MaxClusters})", maxClusters);

            try
            {
                var chatsWithVectors = (await _chatDatabase.GetAllChatsAsync())
                    .Where(c => c.EmbeddingVector != null)
                    .ToList();

                if (chatsWithVectors.Count < 2)
                {
                    _logger.LogDebug("Not enough chats with vectors for clustering");
                    return new List<ChatCluster>();
                }

                // Simple clustering based on similarity thresholds
                var clusters = new List<ChatCluster>();
                var usedChatIds = new HashSet<int>();

                for (int i = 0; i < chatsWithVectors.Count && clusters.Count < maxClusters; i++)
                {
                    var seedChat = chatsWithVectors[i];
                    if (usedChatIds.Contains(seedChat.Id)) continue;

                    var cluster = new ChatCluster
                    {
                        Id = clusters.Count + 1,
                        SeedChat = seedChat,
                        Chats = new List<Chat> { seedChat }
                    };

                    usedChatIds.Add(seedChat.Id);

                    // Find similar chats for this cluster
                    for (int j = i + 1; j < chatsWithVectors.Count; j++)
                    {
                        var candidateChat = chatsWithVectors[j];
                        if (usedChatIds.Contains(candidateChat.Id)) continue;

                        var similarity = _embeddingService.CalculateCosineSimilarity(
                            seedChat.EmbeddingVector!, candidateChat.EmbeddingVector!);

                        if (similarity > 0.7f) // High similarity threshold for clustering
                        {
                            cluster.Chats.Add(candidateChat);
                            usedChatIds.Add(candidateChat.Id);
                        }
                    }

                    // Only add clusters with more than one chat
                    if (cluster.Chats.Count > 1)
                    {
                        cluster.AverageSimilarity = CalculateClusterAverageSimilarity(cluster.Chats);
                        clusters.Add(cluster);
                    }
                }

                _logger.LogDebug("Found {ClusterCount} chat clusters", clusters.Count);
                return clusters;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding chat clusters: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task RebuildVectorIndicesAsync()
        {
            _logger.LogInformation("Rebuilding vector indices...");

            try
            {
                // This is a placeholder for rebuilding vector indices
                // In a real implementation, you would:
                // 1. Drop existing vector tables
                // 2. Recreate them
                // 3. Repopulate with all existing vectors

                await Task.Delay(100); // Placeholder
                _logger.LogInformation("Vector indices rebuilt successfully (placeholder)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding vector indices: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<VectorDatabaseStats> GetVectorDatabaseStatsAsync()
        {
            _logger.LogDebug("Gathering vector database statistics...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var allMessages = allChats.SelectMany(c => c.Messages).ToList();

                var chatVectors = allChats.Where(c => c.EmbeddingVector != null).ToList();
                var messageVectors = allMessages.Where(m => m.EmbeddingVector != null).ToList();

                // Calculate vector dimension (assuming all vectors have the same dimension)
                int vectorDimension = chatVectors.FirstOrDefault()?.EmbeddingVector?.Length ?? 0;

                // Calculate storage estimates
                long chatVectorStorage = chatVectors.Count * vectorDimension * sizeof(float);
                long messageVectorStorage = messageVectors.Count * vectorDimension * sizeof(float);

                return new VectorDatabaseStats
                {
                    TotalChats = allChats.Count,
                    ChatsWithVectors = chatVectors.Count,
                    TotalMessages = allMessages.Count,
                    MessagesWithVectors = messageVectors.Count,
                    VectorDimension = vectorDimension,
                    EstimatedChatVectorStorageBytes = chatVectorStorage,
                    EstimatedMessageVectorStorageBytes = messageVectorStorage,
                    TotalEstimatedStorageBytes = chatVectorStorage + messageVectorStorage,
                    VectorSearchEnabled = chatVectors.Count > 0, // Simplified check
                    LastCalculated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error gathering vector database stats: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<int> CleanupOrphanedVectorEntriesAsync()
        {
            _logger.LogInformation("Cleaning up orphaned vector entries...");

            try
            {
                // This is a placeholder for the actual cleanup implementation
                // In a real implementation, you would need direct SQL access to:
                // 1. Find vector table entries without corresponding chat/message records
                // 2. Delete those orphaned entries

                await Task.Delay(100); // Placeholder
                _logger.LogInformation("Cleanup completed (placeholder implementation)");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up orphaned vectors: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        // Helper methods
        private float CalculateTextRelevance(Chat chat, string query)
        {
            float relevance = 0f;
            var queryLower = query.ToLower();

            // Title match
            if (chat.Title.ToLower().Contains(queryLower))
            {
                relevance += 0.5f;
            }

            // Message content match
            var matchingMessages = chat.Messages.Count(m => 
                m.Content.ToLower().Contains(queryLower));

            if (matchingMessages > 0)
            {
                relevance += Math.Min(0.5f, matchingMessages * 0.1f);
            }

            return Math.Min(1.0f, relevance);
        }

        private float GetMedian(List<float> values)
        {
            var sorted = values.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            
            if (sorted.Count % 2 == 0)
            {
                return (sorted[mid - 1] + sorted[mid]) / 2f;
            }
            
            return sorted[mid];
        }

        private float CalculateStandardDeviation(List<float> values)
        {
            if (!values.Any()) return 0f;

            var average = values.Average();
            var squaredDifferences = values.Select(x => Math.Pow(x - average, 2));
            var variance = squaredDifferences.Average();
            
            return (float)Math.Sqrt(variance);
        }

        private float CalculateClusterAverageSimilarity(List<Chat> chats)
        {
            if (chats.Count < 2) return 0f;

            var similarities = new List<float>();
            
            for (int i = 0; i < chats.Count; i++)
            {
                for (int j = i + 1; j < chats.Count; j++)
                {                    if (chats[i].EmbeddingVector != null && chats[j].EmbeddingVector != null)
                    {
                        var similarity = _embeddingService.CalculateCosineSimilarity(
                            chats[i].EmbeddingVector!, chats[j].EmbeddingVector!);
                        similarities.Add(similarity);
                    }
                }
            }

            return similarities.Any() ? similarities.Average() : 0f;
        }
    }

    // Supporting classes for the advanced vector service
    public class ChatSearchResult
    {
        public Chat Chat { get; set; } = new();
        public float VectorSimilarity { get; set; }
        public float TextRelevance { get; set; }
        public float HybridScore { get; set; }
    }

    public class ChatSimilarityStats
    {
        public int ChatId { get; set; }
        public bool HasVector { get; set; }
        public int ComparableChatsCount { get; set; }
        public float AverageSimilarity { get; set; }
        public float MaxSimilarity { get; set; }
        public float MinSimilarity { get; set; }
        public float MedianSimilarity { get; set; }
        public float StandardDeviation { get; set; }
    }

    public class ChatCluster
    {
        public int Id { get; set; }
        public Chat SeedChat { get; set; } = new();
        public List<Chat> Chats { get; set; } = new();
        public float AverageSimilarity { get; set; }
    }

    public class VectorDatabaseStats
    {
        public int TotalChats { get; set; }
        public int ChatsWithVectors { get; set; }
        public int TotalMessages { get; set; }
        public int MessagesWithVectors { get; set; }
        public int VectorDimension { get; set; }
        public long EstimatedChatVectorStorageBytes { get; set; }
        public long EstimatedMessageVectorStorageBytes { get; set; }
        public long TotalEstimatedStorageBytes { get; set; }
        public bool VectorSearchEnabled { get; set; }
        public DateTime LastCalculated { get; set; }
    }
}
