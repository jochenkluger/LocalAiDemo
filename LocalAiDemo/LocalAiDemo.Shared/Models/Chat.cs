namespace LocalAiDemo.Shared.Models
{
    public class Chat
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public bool IsActive { get; set; }

        // Legacy PersonId for backward compatibility during migration
        public int PersonId { get; set; }

        // New: ContactId - this is the primary reference now
        public int? ContactId { get; set; }

        public Contact? Contact { get; set; }

        public float[]? EmbeddingVector { get; set; } // For vector search - deprecated, use segments instead

        // New: Daily segments for better thematic vectorization
        public List<ChatSegment> Segments { get; set; } = new List<ChatSegment>();
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public int ChatId { get; set; } // Adding ChatId property
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsUser { get; set; }
        public float[]? EmbeddingVector { get; set; } // For vector search of individual messages

        // New: Reference to segment
        public int? SegmentId { get; set; }

        public ChatSegment? Segment { get; set; }
    }

    /// <summary>
    /// Represents a thematic segment of a chat, typically grouped by day or topic
    /// </summary>
    public class ChatSegment
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public Chat? Chat { get; set; }

        /// <summary>
        /// Date this segment represents (all messages from this day)
        /// </summary>
        public DateTime SegmentDate { get; set; }

        /// <summary>
        /// Generated title/summary of this segment's content
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Concatenated content of all messages in this segment for vectorization
        /// </summary>
        public string CombinedContent { get; set; } = string.Empty;

        /// <summary>
        /// Vector representation of this segment's content
        /// </summary>
        public float[]? EmbeddingVector { get; set; }

        /// <summary>
        /// Number of messages in this segment
        /// </summary>
        public int MessageCount { get; set; }

        /// <summary>
        /// First message timestamp in this segment
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Last message timestamp in this segment
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Messages that belong to this segment
        /// </summary>
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        /// <summary>
        /// When this segment was created/last updated
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Topic keywords extracted from the segment content
        /// </summary>
        public string Keywords { get; set; } = string.Empty;
    }

    /// <summary>
    /// Search result for chat segments with similarity score
    /// </summary>
    public class ChatSegmentSearchResult
    {
        public ChatSegment Segment { get; set; } = new();
        public float SimilarityScore { get; set; }
        public Chat? Chat { get; set; }
        public Contact? Contact { get; set; }

        /// <summary>
        /// Highlighted snippet from the segment content
        /// </summary>
        public string HighlightedSnippet { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result from segment similarity search with similarity score
    /// </summary>
    public class SegmentSimilarityResult
    {
        public ChatSegment Segment { get; set; } = new();
        public float Similarity { get; set; }
    }

    /// <summary>
    /// Statistics about segment vectorization status
    /// </summary>
    public class SegmentVectorizationStats
    {
        public int TotalSegments { get; set; }
        public int VectorizedSegments { get; set; }
        public int UnvectorizedSegments { get; set; }
        public float VectorizationPercentage => TotalSegments > 0 ? (float)VectorizedSegments / TotalSegments * 100 : 0;
    }
}