using System;
using System.Collections.Generic;

namespace LocalAiDemo.Shared.Models
{
    public class Chat
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public bool IsActive { get; set; }
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsUser { get; set; }
    }
}
