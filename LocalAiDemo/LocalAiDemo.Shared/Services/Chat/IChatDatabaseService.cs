using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.Chat
{
    public interface IChatDatabaseService
    {
        Task InitializeDatabaseAsync();

        Task<List<Models.Chat>> GetAllChatsAsync();

        Task<Models.Chat?> GetChatAsync(int chatId);

        Task<List<Models.Chat>> GetChatsByContactAsync(int contactId);

        Task<int> SaveChatAsync(Models.Chat chat);

        Task<List<Contact>> GetAllContactsAsync();

        Task<Contact?> GetContactAsync(int contactId);

        Task<int> SaveContactAsync(Contact contact);

        Task<Models.Chat?> GetOrCreateChatForContactAsync(int contactId);

        Task<List<Models.Chat>> FindSimilarChatsAsync(float[] embedding, int limit = 5);

        Task<List<ChatSegment>> FindSimilarSegmentsAsync(float[] embedding, int limit = 10);

        // New: Chat segment methods
        Task<int> SaveChatSegmentAsync(ChatSegment segment);

        Task<List<ChatSegment>> GetSegmentsForChatAsync(int chatId);

        Task<ChatSegment?> GetChatSegmentAsync(int segmentId);

        Task DeleteChatSegmentAsync(int segmentId);

        Task<List<ChatSegment>> GetAllChatSegmentsAsync();

        Task UpdateChatSegmentAsync(ChatSegment segment);
    }
}