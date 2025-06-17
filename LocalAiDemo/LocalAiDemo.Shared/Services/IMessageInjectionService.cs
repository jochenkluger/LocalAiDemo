using System;

namespace LocalAiDemo.Shared.Services
{    /// <summary>
    /// Service für das Einfügen von Nachrichten in die Chat-UI
    /// </summary>
    public interface IMessageInjectionService
    {
        /// <summary>
        /// Event, das ausgelöst wird, wenn eine Nachricht in die Chat-TextBox eingefügt werden soll
        /// </summary>
        event EventHandler<MessageInjectionEventArgs> MessageInjectionRequested;

        /// <summary>
        /// Event, das ausgelöst wird, wenn zu einem bestimmten Chat navigiert werden soll
        /// </summary>
        event EventHandler<ChatNavigationEventArgs> ChatNavigationRequested;

        /// <summary>
        /// Fügt eine Nachricht in die Chat-TextBox ein
        /// </summary>
        /// <param name="message">Die einzufügende Nachricht</param>
        /// <param name="contactId">ID des Kontakts (optional)</param>
        /// <param name="contactName">Name des Kontakts (optional)</param>
        void InjectMessage(string message, int? contactId = null, string? contactName = null);

        /// <summary>
        /// Navigiert zu einem Chat basierend auf ContactId
        /// </summary>
        /// <param name="contactId">ID des Kontakts</param>
        /// <param name="contactName">Name des Kontakts (optional)</param>
        void NavigateToContactChat(int contactId, string? contactName = null);
    }

    /// <summary>
    /// Event-Argumente für Message-Injection
    /// </summary>
    public class MessageInjectionEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public int? ContactId { get; set; }
        public string? ContactName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }    /// <summary>
    /// Event-Argumente für Chat-Navigation
    /// </summary>
    public class ChatNavigationEventArgs : EventArgs
    {
        public int ContactId { get; set; }
        public string? ContactName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Standard-Implementierung des Message-Injection-Service
    /// </summary>
    public class MessageInjectionService : IMessageInjectionService
    {
        public event EventHandler<MessageInjectionEventArgs>? MessageInjectionRequested;
        public event EventHandler<ChatNavigationEventArgs>? ChatNavigationRequested;

        public void InjectMessage(string message, int? contactId = null, string? contactName = null)
        {
            var args = new MessageInjectionEventArgs
            {
                Message = message,
                ContactId = contactId,
                ContactName = contactName,
                Timestamp = DateTime.Now
            };

            MessageInjectionRequested?.Invoke(this, args);
        }

        public void NavigateToContactChat(int contactId, string? contactName = null)
        {
            var args = new ChatNavigationEventArgs
            {
                ContactId = contactId,
                ContactName = contactName,
                Timestamp = DateTime.Now
            };

            ChatNavigationRequested?.Invoke(this, args);
        }
    }
}
