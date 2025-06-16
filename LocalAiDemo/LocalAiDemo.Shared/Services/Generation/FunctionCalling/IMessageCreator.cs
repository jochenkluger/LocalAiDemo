using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.FunctionCalling
{
    /// <summary>
    /// Interface für den Message Creator, den das LLM aufrufen kann
    /// </summary>
    public interface IMessageCreator
    {        /// <summary>
        /// Erstellt eine Nachricht mit dem angegebenen Text für den angegebenen Empfänger
        /// </summary>
        /// <param name="contactId">ID des Empfänger-Kontakts</param>
        /// <param name="messageText">Text der Nachricht</param>
        /// <returns>ID der erstellten Nachricht</returns>
        Task<string> CreateMessage(int contactId, string messageText);

        /// <summary>
        /// Ruft alle verfügbaren Kontakte ab
        /// </summary>
        /// <returns>Liste der verfügbaren Kontakte</returns>
        Task<List<Contact>> GetAvailableContacts();

        /// <summary>
        /// Sucht nach Kontakten basierend auf einem Suchbegriff
        /// </summary>
        /// <param name="searchTerm">Suchbegriff für Kontakte</param>
        /// <returns>Liste der gefundenen Kontakte</returns>
        Task<List<Contact>> SearchContacts(string searchTerm);

        /// <summary>
        /// Ruft einen spezifischen Kontakt nach Namen ab
        /// </summary>
        /// <param name="contactName">Name des Kontakts</param>
        /// <returns>Kontakt oder null wenn nicht gefunden</returns>
        Task<Contact?> GetContactByName(string contactName);
    }
}
