using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Search;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LocalAiDemo.Shared.Services
{
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

    public class ChatSegmentService : IChatSegmentService
    {
        private readonly IChatDatabaseService _chatDatabase;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ChatSegmentService> _logger;

        public ChatSegmentService(
            IChatDatabaseService chatDatabase,
            IEmbeddingService embeddingService,
            ILogger<ChatSegmentService> logger)
        {
            _chatDatabase = chatDatabase;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<List<ChatSegment>> CreateDailySegmentsAsync(int chatId)
        {
            _logger.LogDebug("Creating daily segments for chat {ChatId}", chatId);

            try
            {
                var chat = await _chatDatabase.GetChatAsync(chatId);
                if (chat == null)
                {
                    _logger.LogWarning("Chat {ChatId} not found", chatId);
                    return new List<ChatSegment>();
                }

                // Group messages by date
                var messagesByDate = chat.Messages
                    .Where(m => !string.IsNullOrEmpty(m.Content))
                    .GroupBy(m => m.Timestamp.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                var segments = new List<ChatSegment>();

                foreach (var dateGroup in messagesByDate)
                {
                    var messagesForDate = dateGroup.OrderBy(m => m.Timestamp).ToList();
                    
                    var segment = new ChatSegment
                    {
                        ChatId = chatId,
                        Chat = chat,
                        SegmentDate = dateGroup.Key,
                        Messages = messagesForDate,
                        MessageCount = messagesForDate.Count,
                        StartTime = messagesForDate.First().Timestamp,
                        EndTime = messagesForDate.Last().Timestamp,
                        CreatedAt = DateTime.Now
                    };

                    // Generate combined content and title
                    GenerateSegmentContent(segment);                    // Generate embedding
                    if (!string.IsNullOrEmpty(segment.CombinedContent))
                    {
                        segment.EmbeddingVector = _embeddingService.GenerateEmbedding(segment.CombinedContent);
                    }

                    // Save segment to database
                    var segmentId = await _chatDatabase.SaveChatSegmentAsync(segment);
                    segment.Id = segmentId;

                    segments.Add(segment);
                    _logger.LogDebug("Created and saved segment for {Date} with {MessageCount} messages, ID: {SegmentId}", 
                        dateGroup.Key.ToShortDateString(), messagesForDate.Count, segmentId);
                }

                _logger.LogInformation("Created {SegmentCount} daily segments for chat {ChatId}", 
                    segments.Count, chatId);

                return segments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating daily segments for chat {ChatId}: {ErrorMessage}", 
                    chatId, ex.Message);
                throw;
            }
        }

        public async Task<int> CreateSegmentsForAllChatsAsync()
        {
            _logger.LogInformation("Creating segments for all chats...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                int totalSegmentsCreated = 0;

                foreach (var chat in allChats)
                {
                    try
                    {
                        var segments = await CreateDailySegmentsAsync(chat.Id);
                        totalSegmentsCreated += segments.Count;

                        if (totalSegmentsCreated % 50 == 0)
                        {
                            _logger.LogInformation("Created {TotalSegments} segments so far...", totalSegmentsCreated);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create segments for chat {ChatId}: {ErrorMessage}", 
                            chat.Id, ex.Message);
                    }
                }

                _logger.LogInformation("Completed segment creation. Created {TotalSegments} segments for {ChatCount} chats", 
                    totalSegmentsCreated, allChats.Count);

                return totalSegmentsCreated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating segments for all chats: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<int> VectorizeUnprocessedSegmentsAsync()
        {
            _logger.LogInformation("Vectorizing unprocessed segments...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                int processedCount = 0;

                foreach (var chat in allChats)
                {
                    var segments = await GetSegmentsForChatAsync(chat.Id);
                    var unprocessedSegments = segments.Where(s => s.EmbeddingVector == null && !string.IsNullOrEmpty(s.CombinedContent)).ToList();

                    foreach (var segment in unprocessedSegments)
                    {
                        try
                        {
                            segment.EmbeddingVector = _embeddingService.GenerateEmbedding(segment.CombinedContent);
                            // TODO: Save segment to database
                            processedCount++;

                            if (processedCount % 20 == 0)
                            {
                                _logger.LogInformation("Vectorized {ProcessedCount} segments...", processedCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to vectorize segment {SegmentId}: {ErrorMessage}", 
                                segment.Id, ex.Message);
                        }
                    }
                }

                _logger.LogInformation("Completed segment vectorization. Processed {ProcessedCount} segments", processedCount);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error vectorizing segments: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<int> ReVectorizeAllSegmentsAsync()
        {
            _logger.LogInformation("Re-vectorizing all segments...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                int processedCount = 0;

                foreach (var chat in allChats)
                {
                    var segments = await GetSegmentsForChatAsync(chat.Id);

                    foreach (var segment in segments.Where(s => !string.IsNullOrEmpty(s.CombinedContent)))
                    {
                        try
                        {
                            // Force re-vectorization
                            segment.EmbeddingVector = _embeddingService.GenerateEmbedding(segment.CombinedContent);
                            // TODO: Save segment to database
                            processedCount++;

                            if (processedCount % 20 == 0)
                            {
                                _logger.LogInformation("Re-vectorized {ProcessedCount} segments...", processedCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to re-vectorize segment {SegmentId}: {ErrorMessage}", 
                                segment.Id, ex.Message);
                        }
                    }
                }

                _logger.LogInformation("Completed segment re-vectorization. Processed {ProcessedCount} segments", processedCount);
                return processedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-vectorizing segments: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<List<SegmentSimilarityResult>> FindSimilarSegmentsAsync(string query, int limit = 10)
        {
            _logger.LogDebug("Finding segments similar to: '{Query}'", query);

            try
            {
                var queryEmbedding = _embeddingService.GenerateEmbedding(query);
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var allSegments = new List<ChatSegment>();

                foreach (var chat in allChats)
                {
                    var chatSegments = await GetSegmentsForChatAsync(chat.Id);
                    allSegments.AddRange(chatSegments.Where(s => s.EmbeddingVector != null));
                }

                var similarities = new List<SegmentSimilarityResult>();

                foreach (var segment in allSegments)
                {
                    if (segment.EmbeddingVector != null)
                    {
                        var similarity = _embeddingService.CalculateCosineSimilarity(queryEmbedding, segment.EmbeddingVector);
                        similarities.Add(new SegmentSimilarityResult
                        {
                            Segment = segment,
                            Similarity = similarity,
                            MatchType = "Vector"
                        });
                    }
                }

                var results = similarities
                    .OrderByDescending(s => s.Similarity)
                    .Take(limit)
                    .ToList();

                _logger.LogDebug("Found {ResultCount} similar segments", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar segments: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<List<SegmentSimilarityResult>> FindSimilarSegmentsInChatAsync(int chatId, string query, int limit = 10)
        {
            _logger.LogDebug("Finding segments similar to: '{Query}' in chat {ChatId}", query, chatId);

            try
            {
                var queryEmbedding = _embeddingService.GenerateEmbedding(query);
                var segments = await GetSegmentsForChatAsync(chatId);
                var segmentsWithVectors = segments.Where(s => s.EmbeddingVector != null).ToList();

                var similarities = new List<SegmentSimilarityResult>();

                foreach (var segment in segmentsWithVectors)
                {
                    var similarity = _embeddingService.CalculateCosineSimilarity(queryEmbedding, segment.EmbeddingVector!);
                    similarities.Add(new SegmentSimilarityResult
                    {
                        Segment = segment,
                        Similarity = similarity,
                        MatchType = "Vector"
                    });
                }

                var results = similarities
                    .OrderByDescending(s => s.Similarity)
                    .Take(limit)
                    .ToList();

                _logger.LogDebug("Found {ResultCount} similar segments in chat {ChatId}", results.Count, chatId);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar segments in chat {ChatId}: {ErrorMessage}", 
                    chatId, ex.Message);
                throw;
            }
        }

        public async Task<SegmentVectorizationStats> GetSegmentStatsAsync()
        {
            _logger.LogDebug("Calculating segment vectorization statistics...");

            try
            {
                var allChats = await _chatDatabase.GetAllChatsAsync();
                var allSegments = new List<ChatSegment>();

                foreach (var chat in allChats)
                {
                    var chatSegments = await GetSegmentsForChatAsync(chat.Id);
                    allSegments.AddRange(chatSegments);
                }

                var segmentsWithVectors = allSegments.Count(s => s.EmbeddingVector != null);
                var segmentsWithContent = allSegments.Count(s => !string.IsNullOrEmpty(s.CombinedContent));

                return new SegmentVectorizationStats
                {
                    TotalChats = allChats.Count,
                    TotalSegments = allSegments.Count,
                    SegmentsWithContent = segmentsWithContent,
                    SegmentsWithVectors = segmentsWithVectors,
                    SegmentsWithoutVectors = segmentsWithContent - segmentsWithVectors,
                    VectorizationPercentage = segmentsWithContent > 0 ? (double)segmentsWithVectors / segmentsWithContent * 100 : 0,
                    AverageSegmentsPerChat = allChats.Count > 0 ? (double)allSegments.Count / allChats.Count : 0,
                    AverageMessagesPerSegment = allSegments.Count > 0 ? allSegments.Average(s => s.MessageCount) : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating segment stats: {ErrorMessage}", ex.Message);
                throw;
            }
        }        public async Task UpdateSegmentsForChatAsync(int chatId)
        {
            _logger.LogDebug("Updating segments for chat {ChatId}", chatId);

            try
            {
                var chat = await _chatDatabase.GetChatAsync(chatId);
                if (chat == null)
                {
                    _logger.LogWarning("Chat {ChatId} not found for segment update", chatId);
                    return;
                }

                var today = DateTime.Now.Date;
                
                // Get today's messages
                var todaysMessages = chat.Messages
                    .Where(m => m.Timestamp.Date == today && !string.IsNullOrEmpty(m.Content))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                if (!todaysMessages.Any())
                {
                    _logger.LogDebug("No messages found for today in chat {ChatId}", chatId);
                    return;
                }

                // Check if a segment for today already exists
                var existingSegments = await _chatDatabase.GetSegmentsForChatAsync(chatId);
                var todaysSegment = existingSegments.FirstOrDefault(s => s.SegmentDate.Date == today);

                if (todaysSegment != null)
                {
                    // Update existing segment
                    _logger.LogDebug("Updating existing segment for {Date} in chat {ChatId}", today.ToShortDateString(), chatId);
                    
                    todaysSegment.Messages = todaysMessages;
                    todaysSegment.MessageCount = todaysMessages.Count;
                    todaysSegment.StartTime = todaysMessages.First().Timestamp;
                    todaysSegment.EndTime = todaysMessages.Last().Timestamp;
                    todaysSegment.CreatedAt = DateTime.Now; // Update timestamp
                    
                    // Regenerate content with user context
                    GenerateSegmentContent(todaysSegment);
                    
                    // Regenerate embedding with new content
                    if (!string.IsNullOrEmpty(todaysSegment.CombinedContent))
                    {
                        todaysSegment.EmbeddingVector = _embeddingService.GenerateEmbedding(todaysSegment.CombinedContent);
                        _logger.LogDebug("Re-vectorized segment for {Date} with {MessageCount} messages", 
                            today.ToShortDateString(), todaysMessages.Count);
                    }
                    
                    // Save updated segment
                    await _chatDatabase.SaveChatSegmentAsync(todaysSegment);
                }
                else
                {
                    // Create new segment for today
                    _logger.LogDebug("Creating new segment for {Date} in chat {ChatId}", today.ToShortDateString(), chatId);
                    
                    var newSegment = new ChatSegment
                    {
                        ChatId = chatId,
                        Chat = chat,
                        SegmentDate = today,
                        Messages = todaysMessages,
                        MessageCount = todaysMessages.Count,
                        StartTime = todaysMessages.First().Timestamp,
                        EndTime = todaysMessages.Last().Timestamp,
                        CreatedAt = DateTime.Now
                    };

                    // Generate content and embedding
                    GenerateSegmentContent(newSegment);
                    
                    if (!string.IsNullOrEmpty(newSegment.CombinedContent))
                    {
                        newSegment.EmbeddingVector = _embeddingService.GenerateEmbedding(newSegment.CombinedContent);
                    }

                    // Save new segment
                    var segmentId = await _chatDatabase.SaveChatSegmentAsync(newSegment);
                    newSegment.Id = segmentId;
                    
                    _logger.LogDebug("Created new segment for {Date} with {MessageCount} messages, ID: {SegmentId}", 
                        today.ToShortDateString(), todaysMessages.Count, segmentId);
                }

                _logger.LogInformation("Successfully updated segments for chat {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating segments for chat {ChatId}: {ErrorMessage}", 
                    chatId, ex.Message);
                throw;
            }
        }public async Task<List<ChatSegment>> GetSegmentsForChatAsync(int chatId)
        {
            try
            {
                // First try to load from database
                var segments = await _chatDatabase.GetSegmentsForChatAsync(chatId);
                
                if (segments.Any())
                {
                    _logger.LogDebug("Loaded {SegmentCount} segments from database for chat {ChatId}", 
                        segments.Count, chatId);
                    return segments;
                }

                // If no segments exist, create them
                _logger.LogInformation("No segments found for chat {ChatId}, creating new segments", chatId);
                return await CreateDailySegmentsAsync(chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting segments for chat {ChatId}: {ErrorMessage}", 
                    chatId, ex.Message);
                return new List<ChatSegment>();
            }
        }

        public async Task<List<ChatSegment>> CreateThematicSegmentsAsync(int chatId, float similarityThreshold = 0.7f)
        {
            _logger.LogDebug("Creating thematic segments for chat {ChatId} with threshold {Threshold}", 
                chatId, similarityThreshold);

            try
            {
                var chat = await _chatDatabase.GetChatAsync(chatId);
                if (chat == null)
                {
                    return new List<ChatSegment>();
                }

                var messages = chat.Messages
                    .Where(m => !string.IsNullOrEmpty(m.Content))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                if (!messages.Any())
                {
                    return new List<ChatSegment>();
                }

                // Generate embeddings for all messages first
                var messageEmbeddings = new Dictionary<ChatMessage, float[]>();
                foreach (var message in messages)
                {
                    messageEmbeddings[message] = _embeddingService.GenerateEmbedding(message.Content);
                }

                // Group messages by thematic similarity
                var segments = new List<ChatSegment>();
                var usedMessages = new HashSet<ChatMessage>();

                foreach (var message in messages)
                {
                    if (usedMessages.Contains(message)) continue;

                    var segment = new ChatSegment
                    {
                        ChatId = chatId,
                        Chat = chat,
                        Messages = new List<ChatMessage> { message },
                        StartTime = message.Timestamp,
                        EndTime = message.Timestamp,
                        CreatedAt = DateTime.Now
                    };

                    usedMessages.Add(message);

                    // Find similar messages for this segment
                    var currentEmbedding = messageEmbeddings[message];
                    
                    foreach (var otherMessage in messages)
                    {
                        if (usedMessages.Contains(otherMessage)) continue;

                        var similarity = _embeddingService.CalculateCosineSimilarity(
                            currentEmbedding, messageEmbeddings[otherMessage]);

                        if (similarity >= similarityThreshold)
                        {
                            segment.Messages.Add(otherMessage);
                            usedMessages.Add(otherMessage);
                            
                            // Update segment time boundaries
                            if (otherMessage.Timestamp < segment.StartTime)
                                segment.StartTime = otherMessage.Timestamp;
                            if (otherMessage.Timestamp > segment.EndTime)
                                segment.EndTime = otherMessage.Timestamp;
                        }
                    }

                    // Set segment date to the most common date in the segment
                    segment.SegmentDate = segment.Messages
                        .GroupBy(m => m.Timestamp.Date)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    segment.MessageCount = segment.Messages.Count;
                    
                    // Generate content and embedding
                    GenerateSegmentContent(segment);
                    if (!string.IsNullOrEmpty(segment.CombinedContent))
                    {
                        segment.EmbeddingVector = _embeddingService.GenerateEmbedding(segment.CombinedContent);
                    }

                    segments.Add(segment);
                }

                _logger.LogInformation("Created {SegmentCount} thematic segments for chat {ChatId}", 
                    segments.Count, chatId);

                return segments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating thematic segments for chat {ChatId}: {ErrorMessage}", 
                    chatId, ex.Message);
                throw;
            }
        }        private void GenerateSegmentContent(ChatSegment segment)
        {
            if (!segment.Messages.Any())
            {
                return;
            }

            // Get user name for context
            var userName = segment.Chat?.Person?.Name ?? "Unbekannter Benutzer";
            var userDepartment = segment.Chat?.Person?.Department ?? "";

            // Combine all message content with user context
            var contentBuilder = new StringBuilder();
            
            // Add user context at the beginning
            contentBuilder.AppendLine($"Gespräch mit: {userName}");
            if (!string.IsNullOrEmpty(userDepartment))
            {
                contentBuilder.AppendLine($"Abteilung: {userDepartment}");
            }
            contentBuilder.AppendLine($"Datum: {segment.SegmentDate:dd.MM.yyyy}");
            contentBuilder.AppendLine("---");

            var keywords = new HashSet<string>();

            foreach (var message in segment.Messages.OrderBy(m => m.Timestamp))
            {
                var speaker = message.IsUser ? userName : "KI-Assistent";
                contentBuilder.AppendLine($"[{speaker}]: {message.Content}");
                
                // Extract simple keywords (words longer than 3 characters)
                var words = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 3 && !w.All(char.IsPunctuation))
                    .Select(w => w.Trim('.', ',', '!', '?', ';', ':').ToLower());
                
                foreach (var word in words)
                {
                    keywords.Add(word);
                }
            }

            // Add user name and department to keywords for better searchability
            if (!string.IsNullOrEmpty(userName))
            {
                keywords.Add(userName.ToLower());
            }
            if (!string.IsNullOrEmpty(userDepartment))
            {
                keywords.Add(userDepartment.ToLower());
            }

            segment.CombinedContent = contentBuilder.ToString().Trim();
            segment.Keywords = string.Join(", ", keywords.Take(15)); // Increased to 15 keywords

            // Generate title with user name
            var firstMessage = segment.Messages.OrderBy(m => m.Timestamp).First();
            var preview = firstMessage.Content.Length > 40 
                ? firstMessage.Content.Substring(0, 40) + "..." 
                : firstMessage.Content;
            
            segment.Title = $"{userName} - {segment.SegmentDate:dd.MM.yyyy}: {preview}";
        }
    }

    /// <summary>
    /// Result of a segment similarity search
    /// </summary>
    public class SegmentSimilarityResult
    {
        public ChatSegment Segment { get; set; } = new();
        public float Similarity { get; set; }
        public string MatchType { get; set; } = "Vector"; // Vector, Text, Hybrid
        public string? MatchedKeywords { get; set; }
    }

    /// <summary>
    /// Statistics about segment vectorization
    /// </summary>
    public class SegmentVectorizationStats
    {
        public int TotalChats { get; set; }
        public int TotalSegments { get; set; }
        public int SegmentsWithContent { get; set; }
        public int SegmentsWithVectors { get; set; }
        public int SegmentsWithoutVectors { get; set; }
        public double VectorizationPercentage { get; set; }
        public double AverageSegmentsPerChat { get; set; }
        public double AverageMessagesPerSegment { get; set; }
    }
}
