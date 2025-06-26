using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.Search;

/// <summary>
/// Main facade service for all chat vectorization operations
/// Combines ChatVectorizationService and AdvancedVectorService for easy access
/// </summary>
public interface IChatVectorService
{
    // Basic vectorization operations
    Task<VectorizationResult> VectorizeAllUnprocessedAsync();
    Task<VectorizationResult> ReVectorizeAllAsync();
    Task<VectorizationStats> GetStatsAsync();

    // Search operations
    Task<List<Chat>> FindSimilarChatsAsync(string query, int limit = 5);
    Task<List<ChatMessage>> FindSimilarMessagesAsync(string query, int limit = 10);
    Task<List<ChatSearchResult>> HybridSearchAsync(string query, int limit = 10);

    // Maintenance operations
    Task<int> CleanupAsync();
    Task RebuildIndicesAsync();

    // Analytics
    Task<List<ChatCluster>> FindChatClustersAsync(int maxClusters = 5);
    Task<ChatSimilarityStats> GetChatSimilarityStatsAsync(int chatId);
    Task<VectorDatabaseStats> GetDatabaseStatsAsync();

    // Individual updates
    Task UpdateChatVectorAsync(int chatId);
    Task UpdateMessageVectorAsync(int messageId);
}