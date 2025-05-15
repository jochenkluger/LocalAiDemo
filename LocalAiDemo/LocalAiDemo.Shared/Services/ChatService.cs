using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services
{
    public interface IChatService
    {
        Task<Chat> CreateNewChatAsync(Person person);

        Task<Chat?> GetChatAsync(int chatId);

        Task<List<Chat>> GetAllChatsAsync();

        Task<int> SaveChatAsync(Chat chat);
    }

    public class ChatService : IChatService
    {
        private readonly IChatDatabaseService _chatDatabase;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatDatabaseService chatDatabase,
            IEmbeddingService embeddingService,
            ILogger<ChatService> logger)
        {
            _chatDatabase = chatDatabase;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<Chat> CreateNewChatAsync(Person person)
        {
            _logger.LogInformation("Creating new chat with person: {PersonName} (ID: {PersonId})",
                person.Name, person.Id);

            // Create a new chat with the selected person
            var newChat = new Chat
            {
                Title = $"Chat with {person.Name}",
                CreatedAt = DateTime.Now,
                IsActive = true,
                PersonId = person.Id,
                Person = person,
                Messages = new List<ChatMessage>()
            };

            // Add the first message (welcome message from the person)
            var firstMessage = new ChatMessage
            {
                Content = $"Hallo, ich bin {person.Name} vom {person.Department}. Wie kann ich dir helfen?",
                Timestamp = DateTime.Now,
                IsUser = false
            };
            newChat.Messages.Add(firstMessage);
            _logger.LogDebug("First message created: {MessageContent}", firstMessage.Content);

            // Generate embedding for the chat
            try
            {
                string contentToEmbed = $"{person.Name} {person.Department} {newChat.Messages[0].Content}";
                newChat.EmbeddingVector = _embeddingService.GenerateEmbedding(contentToEmbed);
                _logger.LogDebug("Embedding generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding: {ErrorMessage}", ex.Message);
                // Continue without embedding
            }

            // Save to database
            try
            {
                _logger.LogDebug("Saving chat to database");
                var chatId = await _chatDatabase.SaveChatAsync(newChat);
                newChat.Id = chatId;
                _logger.LogInformation("Chat saved with ID: {ChatId}", chatId);

                return newChat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat: {ErrorMessage}", ex.Message);
                throw; // Rethrow to allow proper error handling by caller
            }
        }

        public async Task<Chat?> GetChatAsync(int chatId)
        {
            _logger.LogDebug("Getting chat with ID: {ChatId}", chatId);
            return await _chatDatabase.GetChatAsync(chatId);
        }

        public async Task<List<Chat>> GetAllChatsAsync()
        {
            _logger.LogDebug("Getting all chats");
            return await _chatDatabase.GetAllChatsAsync();
        }

        public async Task<int> SaveChatAsync(Chat chat)
        {
            _logger.LogDebug("Saving chat with ID: {ChatId}", chat.Id);
            return await _chatDatabase.SaveChatAsync(chat);
        }
    }
}