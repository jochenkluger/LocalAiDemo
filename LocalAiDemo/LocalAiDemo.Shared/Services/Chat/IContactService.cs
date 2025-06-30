using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.Chat
{
    /// <summary>
    /// Interface für die Kontaktverwaltung
    /// </summary>
    public interface IContactService
    {
        /// <summary>
        /// Ruft alle Kontakte ab
        /// </summary>
        /// <returns>Liste aller Kontakte</returns>
        Task<List<Contact>> GetContactsAsync();

        /// <summary>
        /// Ruft einen Kontakt nach ID ab
        /// </summary>
        /// <param name="id">Kontakt-ID</param>
        /// <returns>Kontakt oder null</returns>
        Task<Contact?> GetContactByIdAsync(int id);

        /// <summary>
        /// Ruft einen Kontakt nach Namen ab
        /// </summary>
        /// <param name="name">Kontakt-Name</param>
        /// <returns>Kontakt oder null</returns>
        Task<Contact?> GetContactByNameAsync(string name);

        /// <summary>
        /// Sucht Kontakte nach Namen (Teilstring-Suche)
        /// </summary>
        /// <param name="searchTerm">Suchbegriff</param>
        /// <returns>Liste der gefundenen Kontakte</returns>
        Task<List<Contact>> SearchContactsAsync(string searchTerm);

        /// <summary>
        /// Ruft alle Favoriten-Kontakte ab
        /// </summary>
        /// <returns>Liste der Favoriten-Kontakte</returns>
        Task<List<Contact>> GetFavoriteContactsAsync();

        /// <summary>
        /// Ruft alle Online-Kontakte ab
        /// </summary>
        /// <returns>Liste der Online-Kontakte</returns>
        Task<List<Contact>> GetOnlineContactsAsync();

        /// <summary>
        /// Erstellt einen neuen Kontakt
        /// </summary>
        /// <param name="contact">Neuer Kontakt</param>
        /// <returns>Erstellter Kontakt mit ID</returns>
        Task<Contact> CreateContactAsync(Contact contact);

        /// <summary>
        /// Aktualisiert einen bestehenden Kontakt
        /// </summary>
        /// <param name="contact">Zu aktualisierender Kontakt</param>
        /// <returns>Aktualisierter Kontakt</returns>
        Task<Contact> UpdateContactAsync(Contact contact);

        /// <summary>
        /// Löscht einen Kontakt
        /// </summary>
        /// <param name="id">ID des zu löschenden Kontakts</param>
        /// <returns>True wenn erfolgreich gelöscht</returns>
        Task<bool> DeleteContactAsync(int id);

        /// <summary>
        /// Aktualisiert den Status eines Kontakts
        /// </summary>
        /// <param name="id">Kontakt-ID</param>
        /// <param name="status">Neuer Status</param>
        /// <returns>True wenn erfolgreich aktualisiert</returns>
        Task<bool> UpdateContactStatusAsync(int id, ContactStatus status);

        /// <summary>
        /// Markiert/Entmarkiert einen Kontakt als Favorit
        /// </summary>
        /// <param name="id">Kontakt-ID</param>
        /// <param name="isFavorite">Favorit-Status</param>
        /// <returns>True wenn erfolgreich aktualisiert</returns>
        Task<bool> SetFavoriteAsync(int id, bool isFavorite);

        /// <summary>
        /// Stellt sicher, dass ein Kontakt für eine Person aus der Chat-Datenbank existiert
        /// </summary>
        /// <param name="personName">Name der Person</param>
        /// <returns>Kontakt oder null wenn Person nicht gefunden</returns>
        Task<Contact?> EnsureContactForPersonAsync(string personName);
    }
}