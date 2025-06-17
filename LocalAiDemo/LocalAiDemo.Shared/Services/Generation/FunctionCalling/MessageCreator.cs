using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services.FunctionCalling
{    /// <summary>
    /// Implementierung des IMessageCreator-Interface für Testzwecke
    /// </summary>
    public class MessageCreator : IMessageCreator
    {
        private readonly ILogger<MessageCreator> _logger;
        private readonly IContactService _contactService;
        private readonly IChatDatabaseService _chatDatabaseService;
        private readonly IChatService _chatService;
        private readonly IMessageInjectionService _messageInjectionService;

        public MessageCreator(
            ILogger<MessageCreator> logger,
            IContactService contactService,
            IChatDatabaseService chatDatabaseService,
            IChatService chatService,
            IMessageInjectionService messageInjectionService)
        {
            _logger = logger;
            _contactService = contactService;
            _chatDatabaseService = chatDatabaseService;
            _chatService = chatService;
            _messageInjectionService = messageInjectionService;
        }        /// <summary>
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

                _logger.LogInformation(
                    "Bereite Nachricht für Kontakt {ContactName} (ID: {ContactId}) vor: {MessageText}",
                    contact.Name, contact.Id, messageText);

                // Verwende MessageInjectionService um zur entsprechenden Chat-Ansicht zu navigieren
                _messageInjectionService.NavigateToContactChat(contactId, contact.Name);

                // Füge die Nachricht in die Chat-TextBox ein
                _messageInjectionService.InjectMessage(messageText, contactId, contact.Name);

                _logger.LogInformation(
                    "Nachricht erfolgreich für Überprüfung vorbereitet für Kontakt {ContactName} (ID: {ContactId})",
                    contact.Name, contact.Id);

                return $"Nachricht für {contact.Name} wurde vorbereitet und in die Chat-Ansicht eingefügt. Sie können die Nachricht nun überprüfen und senden.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Vorbereiten der Nachricht für Kontakt {ContactId}", contactId);
                return $"Fehler beim Vorbereiten der Nachricht: {ex.Message}";
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