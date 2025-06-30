using System.ComponentModel;
using LocalAiDemo.Shared.Services.FunctionCalling;
using Microsoft.SemanticKernel;

namespace LocalAiDemo.Shared.Services.Generation.Plugins
{
    /// <summary>
    /// Semantic Kernel Plugin für Contact-bezogene Funktionen
    /// </summary>
    public class ContactPlugin
    {
        private readonly IMessageCreator _messageCreator;

        public ContactPlugin(IMessageCreator messageCreator)
        {
            _messageCreator = messageCreator;
        }

        [KernelFunction("get_available_contacts")]
        [Description(
            "Ruft alle verfügbaren Kontakte ab, um zu sehen, wer für Nachrichten verfügbar ist. Die Antwort enthält die Kontakt-ID die für das Erstellen von Nachrichten benötigt wird.")]
        public async Task<string> GetAvailableContacts()
        {
            try
            {
                var contacts = await _messageCreator.GetAvailableContacts();

                if (contacts == null || !contacts.Any())
                {
                    return "Keine Kontakte verfügbar.";
                }

                var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}").ToList();
                return $"Verfügbare Kontakte:\n{string.Join("\n", contactList)}";
            }
            catch (Exception ex)
            {
                return $"Fehler beim Abrufen der verfügbaren Kontakte: {ex.Message}";
            }
        }

        [KernelFunction("search_contacts")]
        [Description(
            "Sucht nach Kontakten basierend auf einem Suchbegriff (Name, E-Mail oder Notizen). Die Antwort enthält die Kontakt-ID die für das Erstellen von Nachrichten benötigt wird.")]
        public async Task<string> SearchContacts(
            [Description("Suchbegriff für die Kontaktsuche")]
            string searchTerm)
        {
            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                {
                    return "Fehler: Suchbegriff ist erforderlich.";
                }

                var contacts = await _messageCreator.SearchContacts(searchTerm);

                if (contacts == null || !contacts.Any())
                {
                    return $"Keine Kontakte gefunden für Suchbegriff: {searchTerm}";
                }

                var contactList = contacts.Select(c => $"- ID: {c.Id}, Name: {c.Name}, E-Mail: {c.Email}").ToList();
                return $"Gefundene Kontakte für '{searchTerm}':\n{string.Join("\n", contactList)}";
            }
            catch (Exception ex)
            {
                return $"Fehler bei der Kontaktsuche: {ex.Message}";
            }
        }

        [KernelFunction("get_contact_by_name")]
        [Description(
            "Ruft einen spezifischen Kontakt nach Namen ab, um Details zu erhalten. Die Antwort enthält die Kontakt-ID die für das Erstellen von Nachrichten benötigt wird.")]
        public async Task<string> GetContactByName(
            [Description("Name des gesuchten Kontakts")]
            string contactName)
        {
            try
            {
                if (string.IsNullOrEmpty(contactName))
                {
                    return "Fehler: Kontaktname ist erforderlich.";
                }

                var contact = await _messageCreator.GetContactByName(contactName);

                if (contact == null)
                {
                    return $"Kontakt '{contactName}' wurde nicht gefunden.";
                }

                return
                    $"Kontakt gefunden:\n- ID: {contact.Id}\n- Name: {contact.Name}\n- E-Mail: {contact.Email}\n- Telefon: {contact.Phone ?? "Nicht verfügbar"}\n- Notizen: {contact.Notes ?? "Keine Notizen"}";
            }
            catch (Exception ex)
            {
                return $"Fehler beim Abrufen des Kontakts: {ex.Message}";
            }
        }
    }
}