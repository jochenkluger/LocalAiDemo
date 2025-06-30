using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.Chat;

public interface IChatService
{
    Task<Models.Chat> CreateNewChatAsync(Contact contact);

    Task<Models.Chat?> GetChatAsync(int chatId);

    Task<List<Models.Chat>> GetAllChatsAsync();

    Task<int> SaveChatAsync(Models.Chat chat);

    /// <summary>
    /// Adds a new message to a chat and automatically updates segments
    /// </summary>
    Task<Models.Chat> AddMessageToChatAsync(int chatId, string content, bool isUser);

    /// <summary>
    /// Searches for relevant chat segments based on semantic similarity
    /// </summary>
    Task<List<ChatSegmentSearchResult>> SearchChatSegmentsAsync(string query, int limit = 10);

    /// <summary>
    /// Searches for relevant segments within a specific chat
    /// </summary>
    Task<List<ChatSegmentSearchResult>> SearchChatSegmentsInChatAsync(int chatId, string query, int limit = 5);
}