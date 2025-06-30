using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Search;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Chat
{
    public class ChatService : IChatService
    {
        private readonly IChatDatabaseService _chatDatabase;
        private readonly IEmbeddingService _embeddingService;
        private readonly IChatSegmentService _chatSegmentService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatDatabaseService chatDatabase,
            IEmbeddingService embeddingService,
            IChatSegmentService chatSegmentService,
            ILogger<ChatService> logger)
        {
            _chatDatabase = chatDatabase;
            _embeddingService = embeddingService;
            _chatSegmentService = chatSegmentService;
            _logger = logger;
        }

        public async Task<Models.Chat> CreateNewChatAsync(Contact contact)
        {
            _logger.LogInformation("Creating new chat with contact: {ContactName} (ID: {ContactId})",
                contact.Name, contact.Id);

            // Create a new chat with the selected contact
            var newChat = new Models.Chat
            {
                Title = $"Chat with {contact.Name}",
                CreatedAt = DateTime.Now,
                IsActive = true,
                ContactId = contact.Id,
                Contact = contact,
                Messages = new List<ChatMessage>()
            };

            // Add the first message (welcome message from the contact)
            var firstMessage = new ChatMessage
            {
                Content = $"Hallo, ich bin {contact.Name} vom {contact.Department}. Wie kann ich dir helfen?",
                Timestamp = DateTime.Now,
                IsUser = false
            };
            newChat.Messages.Add(firstMessage);
            _logger.LogDebug("First message created: {MessageContent}", firstMessage.Content);

            // Generate embedding for the chat
            try
            {
                string contentToEmbed = $"{contact.Name} {contact.Department} {newChat.Messages[0].Content}";
                newChat.EmbeddingVector = await _embeddingService.GetEmbeddingAsync(contentToEmbed);
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

        public async Task<Models.Chat?> GetChatAsync(int chatId)
        {
            _logger.LogDebug("Getting chat with ID: {ChatId}", chatId);
            return await _chatDatabase.GetChatAsync(chatId);
        }

        public async Task<List<Models.Chat>> GetAllChatsAsync()
        {
            _logger.LogDebug("Getting all chats");
            return await _chatDatabase.GetAllChatsAsync();
        }

        public async Task<int> SaveChatAsync(Models.Chat chat)
        {
            _logger.LogDebug("Saving chat with ID: {ChatId}", chat.Id);
            return await _chatDatabase.SaveChatAsync(chat);
        }

        public async Task<Models.Chat> AddMessageToChatAsync(int chatId, string content, bool isUser)
        {
            _logger.LogDebug("Adding message to chat {ChatId}: isUser={IsUser}", chatId, isUser);

            try
            {
                var chat = await _chatDatabase.GetChatAsync(chatId);
                if (chat == null)
                {
                    throw new ArgumentException($"Chat with ID {chatId} not found");
                }

                // Create new message
                var newMessage = new ChatMessage
                {
                    ChatId = chatId,
                    Content = content,
                    Timestamp = DateTime.Now,
                    IsUser = isUser
                };

                // Generate embedding for the message
                try
                {
                    newMessage.EmbeddingVector = await _embeddingService.GetEmbeddingAsync(content);
                    _logger.LogDebug("Generated embedding for new message");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate embedding for message");
                }

                // Add message to chat
                chat.Messages.Add(newMessage);

                // Save chat with new message
                await _chatDatabase.SaveChatAsync(chat);

                // Update segments in background (don't await to avoid blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _chatSegmentService.UpdateSegmentsForChatAsync(chatId);
                        _logger.LogDebug("Updated segments for chat {ChatId} after new message", chatId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update segments for chat {ChatId}", chatId);
                    }
                });

                _logger.LogInformation("Added message to chat {ChatId}", chatId);
                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding message to chat {ChatId}: {ErrorMessage}", chatId, ex.Message);
                throw;
            }
        }

        public async Task<List<ChatSegmentSearchResult>> SearchChatSegmentsAsync(string query, int limit = 10)
        {
            _logger.LogDebug("Searching chat segments with query: {Query}", query);

            try
            {
                var segmentResults = await _chatSegmentService.FindSimilarSegmentsAsync(query, limit);
                var searchResults = new List<ChatSegmentSearchResult>();
                foreach (var result in segmentResults)
                {
                    var chat = await _chatDatabase.GetChatAsync(result.Segment.ChatId);
                    var contact = chat?.Contact;

                    searchResults.Add(new ChatSegmentSearchResult
                    {
                        Segment = result.Segment,
                        SimilarityScore = result.Similarity,
                        Chat = chat,
                        Contact = contact,
                        HighlightedSnippet = CreateHighlightedSnippet(result.Segment.CombinedContent, query)
                    });
                }

                _logger.LogInformation("Found {ResultCount} segment results for query: {Query}",
                    searchResults.Count, query);
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching chat segments: {ErrorMessage}", ex.Message);
                return new List<ChatSegmentSearchResult>();
            }
        }

        public async Task<List<ChatSegmentSearchResult>> SearchChatSegmentsInChatAsync(int chatId, string query,
            int limit = 5)
        {
            _logger.LogDebug("Searching segments in chat {ChatId} with query: {Query}", chatId, query);

            try
            {
                var segmentResults = await _chatSegmentService.FindSimilarSegmentsInChatAsync(chatId, query, limit);
                var searchResults = new List<ChatSegmentSearchResult>();
                var chat = await _chatDatabase.GetChatAsync(chatId);
                var contact = chat?.Contact;
                foreach (var result in segmentResults)
                {
                    searchResults.Add(new ChatSegmentSearchResult
                    {
                        Segment = result.Segment,
                        SimilarityScore = result.Similarity,
                        Chat = chat,
                        Contact = contact,
                        HighlightedSnippet = CreateHighlightedSnippet(result.Segment.CombinedContent, query)
                    });
                }

                _logger.LogInformation("Found {ResultCount} segment results in chat {ChatId} for query: {Query}",
                    searchResults.Count, chatId, query);
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching segments in chat {ChatId}: {ErrorMessage}", chatId, ex.Message);
                return new List<ChatSegmentSearchResult>();
            }
        }

        private string CreateHighlightedSnippet(string content, string query, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(query))
                return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;

            var lowerContent = content.ToLower();
            var lowerQuery = query.ToLower();
            var index = lowerContent.IndexOf(lowerQuery);

            if (index == -1)
            {
                // Query not found, return beginning of content
                return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;
            }

            // Calculate snippet bounds
            int start = Math.Max(0, index - 50);
            int end = Math.Min(content.Length, start + maxLength);

            var snippet = content.Substring(start, end - start);

            if (start > 0) snippet = "..." + snippet;
            if (end < content.Length) snippet = snippet + "...";

            return snippet;
        }
    }
}