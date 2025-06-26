using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services
{
    public interface IChatDatabaseService
    {
        Task InitializeDatabaseAsync();

        Task<List<Chat>> GetAllChatsAsync();

        Task<Chat?> GetChatAsync(int chatId);

        Task<List<Chat>> GetChatsByContactAsync(int contactId);

        Task<int> SaveChatAsync(Chat chat);

        Task<List<Contact>> GetAllContactsAsync();

        Task<Contact?> GetContactAsync(int contactId);

        Task<int> SaveContactAsync(Contact contact);

        Task<Chat?> GetOrCreateChatForContactAsync(int contactId);

        Task<List<Chat>> FindSimilarChatsAsync(float[] embedding, int limit = 5);

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
