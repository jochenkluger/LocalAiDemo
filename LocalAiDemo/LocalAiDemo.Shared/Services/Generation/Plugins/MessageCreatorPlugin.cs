using System.ComponentModel;
using LocalAiDemo.Shared.Services.FunctionCalling;
using Microsoft.SemanticKernel;

namespace LocalAiDemo.Shared.Services.Generation.Plugins
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
        }

        [KernelFunction("create_message")]
        [Description(
            "Schreibt eine Nachricht an einen bestimmten Empfänger. WICHTIG: Du musst zuerst die verfügbaren Kontakte abrufen um die Kontakt-ID zu erhalten, bevor du eine Nachricht erstellen kannst.")]
        public async Task<string> CreateMessage(
            [Description(
                "Die ID des Empfänger-Kontakts (muss zuerst über get_available_contacts ermittelt werden)")]
            int contactId,
            [Description("Der Text der zu sendenden Nachricht")]
            string messageText)
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