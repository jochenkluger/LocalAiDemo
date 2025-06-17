using System;
using System.ComponentModel;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Services.FunctionCalling;
using Microsoft.SemanticKernel;

namespace LocalAiDemo.Shared.Services.Plugins
{
    /// <summary>
    /// Semantic Kernel Plugin für Message-bezogene Funktionen
    /// </summary>
    public class MessageCreatorPlugin
    {
        private readonly IMessageCreator _messageCreator;

        public MessageCreatorPlugin(IMessageCreator messageCreator)
        {
            _messageCreator = messageCreator;
        }        [KernelFunction("create_message")]
        [Description("Erstellt eine Nachricht für einen bestimmten Empfänger. WICHTIG: Du musst zuerst die verfügbaren Kontakte abrufen um die Kontakt-ID zu erhalten, bevor du eine Nachricht erstellen kannst.")]
        public async Task<string> CreateMessage(
            [Description("Die ID des Empfänger-Kontakts (muss zuerst über get_available_contacts, search_contacts oder get_contact_by_name ermittelt werden)")] int contactId,
            [Description("Der Text der zu sendenden Nachricht")] string messageText)
        {
            try
            {
                var messageId = await _messageCreator.CreateMessage(contactId, messageText);
                return $"Nachricht erfolgreich erstellt mit ID: {messageId}. Kontakt-ID: {contactId}";
            }
            catch (Exception ex)
            {
                return $"Fehler beim Erstellen der Nachricht: {ex.Message}";
            }
        }
    }
}
