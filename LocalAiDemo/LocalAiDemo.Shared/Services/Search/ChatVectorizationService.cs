using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Chat;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.Search
{
    /// <summary>
    /// Service for vectorizing chats and messages in batch operations and providing advanced vector
    /// search capabilities
    /// </summary>
    public interface IChatVectorizationService
    {
        /// <summary>
        /// Vectorizes all chats that don't have embeddings yet
        /// </summary>
        Task<int> VectorizeUnprocessedChatsAsync();

        /// <summary>
        /// Vectorizes all chat messages that don't have embeddings yet
        /// </summary>
        Task<int> VectorizeUnprocessedMessagesAsync();

        /// <summary>
        /// Re-vectorizes all chats (useful when switching embedding models)
        /// </summary>
        Task<int> ReVectorizeAllChatsAsync();

        /// <summary>
        /// Re-vectorizes all chat messages (useful when switching embedding models)
        /// </summary>
        Task<int> ReVectorizeAllMessagesAsync();

        /// <summary>
        /// Finds similar messages across all chats
        /// </summary>
        Task<List<ChatMessage>> FindSimilarMessagesAsync(string query, int limit = 10);

        /// <summary>
        /// Finds similar messages within a specific chat
        /// </summary>
        Task<List<ChatMessage>> FindSimilarMessagesInChatAsync(int chatId, string query, int limit = 10);

        /// <summary>
        /// Cleans up orphaned vector entries (vectors without corresponding chats/messages)
        /// </summary>
        Task<int> CleanupOrphanedVectorsAsync();

        /// <summary>
        /// Gets statistics about vectorization status
        /// </summary>
        Task<VectorizationStats> GetVectorizationStatsAsync();

        /// <summary>
        /// Updates the embedding vector for a specific chat
        /// </summary>
        Task UpdateChatVectorAsync(int chatId);

        /// <summary>
        /// Updates the embedding vector for a specific message
        /// </summary>
        Task UpdateMessageVectorAsync(int messageId);
    }

    public class ChatVectorizationService : IChatVectorizationService
    {
        private readonly IChatDatabaseService _chatDatabase;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ChatVectorizationService> _logger;

        public ChatVectorizationService(
            IChatDatabaseService chatDatabase,
            IEmbeddingService embeddingService,
            ILogger<ChatVectorizationService> logger)
        {
            _chatDatabase = chatDatabase;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<int> VectorizeUnprocessedChatsAsync()
        {
            _logger.LogInformation("Starting vectorization of unprocessed chats...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var unprocessedChats = allChats.Where(c => c.EmbeddingVector == null).ToList();

                _logger.LogInformation(
                    "Found {UnprocessedCount} chats without embeddings out of {TotalCount} total chats",
                    unprocessedChats.Count, allChats.Count);

                int processedCount = 0;

                foreach (var chat in unprocessedChats)
                {
                    try
                    {
                        await UpdateChatVectorAsync(chat.Id);
                        processedCount++;

                        if (processedCount % 10 == 0)
                        {
                            _logger.LogInformation("Processed {ProcessedCount}/{TotalCount} chats",
                                processedCount, unprocessedChats.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to vectorize chat {ChatId}: {ErrorMessage}",
                            chat.Id, ex.Message);
                    }
                }

                _logger.LogInformation("Completed vectorization. Processed {ProcessedCount} chats", processedCount);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat vectorization: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<int> VectorizeUnprocessedMessagesAsync()
        {
            _logger.LogInformation("Starting vectorization of unprocessed messages...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                int processedCount = 0;
                int totalMessages = 0;

                foreach (var chat in allChats)
                {
                    var unprocessedMessages = chat.Messages
                        .Where(m => m.EmbeddingVector == null && !string.IsNullOrEmpty(m.Content)).ToList();
                    totalMessages += unprocessedMessages.Count;

                    foreach (var message in unprocessedMessages)
                    {
                        try
                        {
                            await UpdateMessageVectorAsync(message.Id);
                            processedCount++;

                            if (processedCount % 50 == 0)
                            {
                                _logger.LogInformation("Processed {ProcessedCount} messages...", processedCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to vectorize message {MessageId}: {ErrorMessage}",
                                message.Id, ex.Message);
                        }
                    }
                }

                _logger.LogInformation(
                    "Completed message vectorization. Processed {ProcessedCount} out of {TotalCount} messages",
                    processedCount, totalMessages);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message vectorization: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<int> ReVectorizeAllChatsAsync()
        {
            _logger.LogInformation("Starting re-vectorization of all chats...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                _logger.LogInformation("Re-vectorizing {TotalCount} chats", allChats.Count);

                int processedCount = 0;

                foreach (var chat in allChats)
                {
                    try
                    {
                        // Force re-vectorization by clearing the existing vector
                        chat.EmbeddingVector = null;
                        await UpdateChatVectorAsync(chat.Id);
                        processedCount++;

                        if (processedCount % 10 == 0)
                        {
                            _logger.LogInformation("Re-vectorized {ProcessedCount}/{TotalCount} chats",
                                processedCount, allChats.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to re-vectorize chat {ChatId}: {ErrorMessage}",
                            chat.Id, ex.Message);
                    }
                }

                _logger.LogInformation("Completed re-vectorization. Processed {ProcessedCount} chats", processedCount);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat re-vectorization: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<int> ReVectorizeAllMessagesAsync()
        {
            _logger.LogInformation("Starting re-vectorization of all messages...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                int processedCount = 0;
                int totalMessages = allChats.SelectMany(c => c.Messages).Count();

                _logger.LogInformation("Re-vectorizing {TotalCount} messages across {ChatCount} chats",
                    totalMessages, allChats.Count);

                foreach (var chat in allChats)
                {
                    foreach (var message in chat.Messages.Where(m => !string.IsNullOrEmpty(m.Content)))
                    {
                        try
                        {
                            // Force re-vectorization by clearing the existing vector
                            message.EmbeddingVector = null;
                            await UpdateMessageVectorAsync(message.Id);
                            processedCount++;

                            if (processedCount % 50 == 0)
                            {
                                _logger.LogInformation("Re-vectorized {ProcessedCount}/{TotalCount} messages",
                                    processedCount, totalMessages);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to re-vectorize message {MessageId}: {ErrorMessage}",
                                message.Id, ex.Message);
                        }
                    }
                }

                _logger.LogInformation("Completed message re-vectorization. Processed {ProcessedCount} messages",
                    processedCount);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message re-vectorization: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<List<ChatMessage>> FindSimilarMessagesAsync(string query, int limit = 10)
        {
            _logger.LogDebug("Searching for messages similar to: '{Query}' (limit: {Limit})", query, limit);

            try
            {
                // Generate embedding for the query
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

                // Get all chats with their messages
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var allMessages = allChats.SelectMany(c => c.Messages).Where(m => m.EmbeddingVector != null).ToList();

                // Calculate similarities and sort
                var messageSimilarities = new List<(ChatMessage Message, float Similarity)>();

                foreach (var message in allMessages)
                {
                    if (message.EmbeddingVector != null)
                    {
                        var similarity =
                            _embeddingService.CalculateCosineSimilarity(queryEmbedding, message.EmbeddingVector);
                        messageSimilarities.Add((message, similarity));
                    }
                }

                // Return top results
                var results = messageSimilarities
                    .OrderByDescending(x => x.Similarity)
                    .Take(limit)
                    .Select(x => x.Message)
                    .ToList();

                _logger.LogDebug("Found {ResultCount} similar messages", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar messages: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<List<ChatMessage>> FindSimilarMessagesInChatAsync(int chatId, string query, int limit = 10)
        {
            _logger.LogDebug("Searching for messages similar to: '{Query}' in chat {ChatId} (limit: {Limit})",
                query, chatId, limit);

            try
            {
                // Generate embedding for the query
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

                // Get the specific chat
                var chat = await _chatDatabase.GetChatAsync(chatId);
                if (chat == null)
                {
                    _logger.LogWarning("Chat {ChatId} not found", chatId);
                    return new List<ChatMessage>();
                }

                var messages = chat.Messages.Where(m => m.EmbeddingVector != null).ToList();

                // Calculate similarities and sort
                var messageSimilarities = new List<(ChatMessage Message, float Similarity)>();

                foreach (var message in messages)
                {
                    if (message.EmbeddingVector != null)
                    {
                        var similarity =
                            _embeddingService.CalculateCosineSimilarity(queryEmbedding, message.EmbeddingVector);
                        messageSimilarities.Add((message, similarity));
                    }
                }

                // Return top results
                var results = messageSimilarities
                    .OrderByDescending(x => x.Similarity)
                    .Take(limit)
                    .Select(x => x.Message)
                    .ToList();

                _logger.LogDebug("Found {ResultCount} similar messages in chat {ChatId}", results.Count, chatId);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar messages in chat {ChatId}: {ErrorMessage}",
                    chatId, ex.Message);
                throw;
            }
        }

        public async Task<int> CleanupOrphanedVectorsAsync()
        {
            _logger.LogInformation("Starting cleanup of orphaned vectors...");

            try
            {
                // This would require direct database access to clean up the vector tables For now,
                // we'll return 0 as this is a placeholder for future implementation In a real
                // implementation, you'd need to:
                // 1. Query the vector tables for entries without corresponding chats/messages
                // 2. Delete those entries

                await Task.CompletedTask; // Make it properly async
                _logger.LogInformation("Vector cleanup completed. (Placeholder implementation)");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during vector cleanup: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<VectorizationStats> GetVectorizationStatsAsync()
        {
            _logger.LogDebug("Calculating vectorization statistics...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var allMessages = allChats.SelectMany(c => c.Messages).ToList();

                var chatsWithVectors = allChats.Count(c => c.EmbeddingVector != null);
                var messagesWithVectors = allMessages.Count(m => m.EmbeddingVector != null);
                var nonEmptyMessages = allMessages.Count(m => !string.IsNullOrEmpty(m.Content));

                var stats = new VectorizationStats
                {
                    TotalChats = allChats.Count,
                    ChatsWithVectors = chatsWithVectors,
                    ChatsWithoutVectors = allChats.Count - chatsWithVectors,
                    TotalMessages = allMessages.Count,
                    NonEmptyMessages = nonEmptyMessages,
                    MessagesWithVectors = messagesWithVectors,
                    MessagesWithoutVectors = nonEmptyMessages - messagesWithVectors,
                    ChatVectorizationPercentage =
                        allChats.Count > 0 ? (double)chatsWithVectors / allChats.Count * 100 : 0,
                    MessageVectorizationPercentage =
                        nonEmptyMessages > 0 ? (double)messagesWithVectors / nonEmptyMessages * 100 : 0
                };

                _logger.LogDebug(
                    "Vectorization stats: {ChatsWithVectors}/{TotalChats} chats, {MessagesWithVectors}/{NonEmptyMessages} messages",
                    chatsWithVectors, allChats.Count, messagesWithVectors, nonEmptyMessages);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating vectorization stats: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task UpdateChatVectorAsync(int chatId)
        {
            _logger.LogDebug("Updating vector for chat {ChatId}", chatId);

            try
            {
                var chat = await _chatDatabase.GetChatAsync(chatId);
                if (chat == null)
                {
                    _logger.LogWarning("Chat {ChatId} not found", chatId);
                    return;
                }

                // Generate content to embed from chat title and messages
                string contentToEmbed = chat.Title ?? string.Empty;

                // Add content from messages (up to first few messages to avoid too much content)
                var messageContent = string.Join(" ", chat.Messages
                    .Take(5) // Limit to first 5 messages
                    .Where(m => !string.IsNullOrEmpty(m.Content))
                    .Select(m => m.Content));

                if (!string.IsNullOrEmpty(messageContent))
                {
                    contentToEmbed += " " + messageContent;
                }

                if (string.IsNullOrEmpty(contentToEmbed.Trim()))
                {
                    _logger.LogDebug("No content to embed for chat {ChatId}", chatId);
                    return;
                }

                // Generate embedding
                chat.EmbeddingVector = await _embeddingService.GetEmbeddingAsync(contentToEmbed);

                // Save the updated chat
                await _chatDatabase.SaveChatAsync(chat);

                _logger.LogDebug("Successfully updated vector for chat {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vector for chat {ChatId}: {ErrorMessage}",
                    chatId, ex.Message);
                throw;
            }
        }

        public async Task UpdateMessageVectorAsync(int messageId)
        {
            _logger.LogDebug("Updating vector for message {MessageId}", messageId);

            try
            {
                // Find the message across all chats
                var allChats = await _chatDatabase.GetAllChatsAsync();
                ChatMessage? targetMessage = null;
                Models.Chat? parentChat = null;

                foreach (var chat in allChats)
                {
                    var message = chat.Messages.FirstOrDefault(m => m.Id == messageId);
                    if (message != null)
                    {
                        targetMessage = message;
                        parentChat = chat;
                        break;
                    }
                }

                if (targetMessage == null || parentChat == null)
                {
                    _logger.LogWarning("Message {MessageId} not found", messageId);
                    return;
                }

                if (string.IsNullOrEmpty(targetMessage.Content))
                {
                    _logger.LogDebug("Message {MessageId} has no content to embed", messageId);
                    return;
                }

                // Generate embedding for the message content
                targetMessage.EmbeddingVector = await _embeddingService.GetEmbeddingAsync(targetMessage.Content);

                // Save the updated chat (which includes the message)
                await _chatDatabase.SaveChatAsync(parentChat);

                _logger.LogDebug("Successfully updated vector for message {MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vector for message {MessageId}: {ErrorMessage}",
                    messageId, ex.Message);
                throw;
            }
        }
    }

    /// <summary>
    /// Statistics about the vectorization status of chats and messages
    /// </summary>
    public class VectorizationStats
    {
        public int TotalChats { get; set; }
        public int ChatsWithVectors { get; set; }
        public int ChatsWithoutVectors { get; set; }
        public int TotalMessages { get; set; }
        public int NonEmptyMessages { get; set; }
        public int MessagesWithVectors { get; set; }
        public int MessagesWithoutVectors { get; set; }
        public double ChatVectorizationPercentage { get; set; }
        public double MessageVectorizationPercentage { get; set; }
    }
}