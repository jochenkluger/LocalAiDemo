using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.Chat;

/// <summary>
/// Service for managing chat segments - daily/thematic groupings of messages for better vectorization
/// </summary>
public interface IChatSegmentService
{
    /// <summary>
    /// Creates daily segments for a chat based on message timestamps
    /// </summary>
    Task<List<ChatSegment>> CreateDailySegmentsAsync(int chatId);

    /// <summary>
    /// Creates segments for all chats that don't have segments yet
    /// </summary>
    Task<int> CreateSegmentsForAllChatsAsync();

    /// <summary>
    /// Vectorizes all segments that don't have embeddings yet
    /// </summary>
    Task<int> VectorizeUnprocessedSegmentsAsync();

    /// <summary>
    /// Re-vectorizes all segments (useful when changing embedding models)
    /// </summary>
    Task<int> ReVectorizeAllSegmentsAsync();

    /// <summary>
    /// Finds similar segments across all chats
    /// </summary>
    Task<List<SegmentSimilarityResult>> FindSimilarSegmentsAsync(string query, int limit = 10);

    /// <summary>
    /// Finds similar segments within a specific chat
    /// </summary>
    Task<List<SegmentSimilarityResult>> FindSimilarSegmentsInChatAsync(int chatId, string query, int limit = 10);

    /// <summary>
    /// Gets statistics about segment vectorization
    /// </summary>
    Task<SegmentVectorizationStats> GetSegmentStatsAsync();

    /// <summary>
    /// Updates segments when new messages are added to a chat
    /// </summary>
    Task UpdateSegmentsForChatAsync(int chatId);

    /// <summary>
    /// Gets all segments for a specific chat
    /// </summary>
    Task<List<ChatSegment>> GetSegmentsForChatAsync(int chatId);

    /// <summary>
    /// Creates thematic segments based on content similarity (advanced)
    /// </summary>
    Task<List<ChatSegment>> CreateThematicSegmentsAsync(int chatId, float similarityThreshold = 0.7f);
}