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
    public class DemoMessageCreator : IMessageCreator
    {
        private readonly ILogger<DemoMessageCreator> _logger;
        private readonly IContactService _contactService;

        public DemoMessageCreator(ILogger<DemoMessageCreator> logger, IContactService contactService)
        {
            _logger = logger;
            _contactService = contactService;
        }        /// <summary>
        /// Erstellt eine Demo-Nachricht mit dem angegebenen Text für den angegebenen Empfänger
        /// </summary>
        /// <param name="contactId">ID des Empfänger-Kontakts</param>
        /// <param name="messageText">Text der Nachricht</param>
        /// <returns>ID der erstellten Nachricht</returns>
        public async Task<string> CreateMessage(int contactId, string messageText)
        {
            var messageId = Guid.NewGuid().ToString();
            
            // Versuche den Kontakt zu finden
            var contact = await _contactService.GetContactByIdAsync(contactId);
            if (contact != null)
            {
                _logger.LogInformation("Nachricht mit ID {MessageId} erstellt für Kontakt {ContactName} (ID: {ContactId}): {MessageText}", 
                    messageId, contact.Name, contact.Id, messageText);
            }
            else
            {
                _logger.LogWarning("Nachricht mit ID {MessageId} erstellt für unbekannte Kontakt-ID {ContactId}: {MessageText}", 
                    messageId, contactId, messageText);
            }
            
            return messageId;
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
