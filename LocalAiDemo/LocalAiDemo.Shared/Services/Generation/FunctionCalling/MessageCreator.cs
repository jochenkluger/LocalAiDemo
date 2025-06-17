using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.FunctionCalling
{
    /// <summary>
    /// Implementierung des IMessageCreator-Interface für Testzwecke
    /// </summary>
    public class MessageCreator : IMessageCreator
    {
        private readonly ILogger<MessageCreator> _logger;
        private readonly IContactService _contactService;
        private readonly IChatDatabaseService _chatDatabaseService;
        private readonly IChatService _chatService;

        public MessageCreator(
            ILogger<MessageCreator> logger,
            IContactService contactService,
            IChatDatabaseService chatDatabaseService,
            IChatService chatService)
        {
            _logger = logger;
            _contactService = contactService;
            _chatDatabaseService = chatDatabaseService;
            _chatService = chatService;
        }

        /// <summary>
        /// Erstellt eine echte Chat-Nachricht für den angegebenen Kontakt
        /// </summary>
        /// <param name="contactId">ID des Empfänger-Kontakts</param>
        /// <param name="messageText">Text der Nachricht</param>
        /// <returns>ID der erstellten Nachricht</returns>
        public async Task<string> CreateMessage(int contactId, string messageText)
        {
            try
            {
                // Versuche den Kontakt zu finden
                var contact = await _contactService.GetContactByIdAsync(contactId);
                if (contact == null)
                {
                    _logger.LogWarning("Kontakt mit ID {ContactId} nicht gefunden", contactId);
                    return $"Fehler: Kontakt mit ID {contactId} nicht gefunden";
                }

                // Chat für den Kontakt abrufen oder erstellen
                var chat = await _chatDatabaseService.GetOrCreateChatForContactAsync(contactId);
                if (chat == null)
                {
                    _logger.LogError("Konnte Chat für Kontakt {ContactId} nicht erstellen", contactId);
                    return "Fehler: Chat konnte nicht erstellt werden";
                } // Nachricht über ChatService hinzufügen

                var updatedChat = await _chatService.AddMessageToChatAsync(chat.Id, messageText, isUser: false);

                if (updatedChat != null)
                {
                    // Get the latest message from the chat
                    var latestMessage = updatedChat.Messages?.LastOrDefault();
                    if (latestMessage != null)
                    {
                        _logger.LogInformation(
                            "Nachricht mit ID {MessageId} erfolgreich erstellt für Kontakt {ContactName} (ID: {ContactId}) in Chat {ChatId}: {MessageText}",
                            latestMessage.Id, contact.Name, contact.Id, chat.Id, messageText);

                        // Update chat title if needed
                        if (string.IsNullOrEmpty(chat.Title) || chat.Title.StartsWith("Chat with Contact"))
                        {
                            chat.Title = $"Chat mit {contact.Name}";
                            await _chatDatabaseService.SaveChatAsync(chat);
                        }

                        return $"Die Nachricht wurde erfolgreich erstellt mit Id {latestMessage.Id}";
                    }
                }

                _logger.LogError("Nachricht konnte nicht zur Datenbank hinzugefügt werden");
                return "Fehler: Nachricht konnte nicht gespeichert werden";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen der Nachricht für Kontakt {ContactId}", contactId);
                return $"Fehler beim Erstellen der Nachricht: {ex.Message}";
            }
        }

        /// <summary>
        /// Ruft alle verfügbaren Kontakte ab
        /// </summary>
        /// <returns>Liste der verfügbaren Kontakte</returns>
        public async Task<List<Contact>> GetAvailableContacts()
        {
            _logger.LogDebug("Rufe alle verfügbaren Kontakte ab");
            return await _contactService.GetContactsAsync();
        }

        /// <summary>
        /// Sucht nach Kontakten basierend auf einem Suchbegriff
        /// </summary>
        /// <param name="searchTerm">Suchbegriff für Kontakte</param>
        /// <returns>Liste der gefundenen Kontakte</returns>
        public async Task<List<Contact>> SearchContacts(string searchTerm)
        {
            _logger.LogDebug("Suche Kontakte mit Begriff: {SearchTerm}", searchTerm);
            return await _contactService.SearchContactsAsync(searchTerm);
        }

        /// <summary>
        /// Ruft einen spezifischen Kontakt nach Namen ab
        /// </summary>
        /// <param name="contactName">Name des Kontakts</param>
        /// <returns>Kontakt oder null wenn nicht gefunden</returns>
        public async Task<Contact?> GetContactByName(string contactName)
        {
            _logger.LogDebug("Rufe Kontakt nach Namen ab: {ContactName}", contactName);
            return await _contactService.GetContactByNameAsync(contactName);
        }
    }
}