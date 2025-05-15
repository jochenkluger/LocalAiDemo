namespace LocalAiDemo.Shared.Models
{
    public class Chat
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public bool IsActive { get; set; }
        public int PersonId { get; set; }
        public Person? Person { get; set; }
        public float[]? EmbeddingVector { get; set; } // For vector search
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public int ChatId { get; set; }  // Adding ChatId property
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsUser { get; set; }
        public float[]? EmbeddingVector { get; set; } // For vector search of individual messages
    }
}