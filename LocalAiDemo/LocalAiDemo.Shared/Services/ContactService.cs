using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services
{
    /// <summary>
    /// Service für die Kontaktverwaltung basierend auf den Namen aus der Chat-Datenbank
    /// </summary>
    public class ContactService : IContactService
    {
        private readonly ILogger<ContactService> _logger;
        private readonly IChatDatabaseService _chatDatabaseService;
        private List<Contact> _contacts;
        private int _nextId = 1;
        private bool _initialized = false;

        public ContactService(ILogger<ContactService> logger, IChatDatabaseService chatDatabaseService)
        {
            _logger = logger;
            _chatDatabaseService = chatDatabaseService;
            _contacts = new List<Contact>();
        }        /// <summary>
        /// Initialisiert die Kontakte basierend auf den Personen aus der Chat-Datenbank
        /// </summary>
        private async Task InitializeContactsFromDatabaseAsync()
        {
            if (_initialized) return;

            try
            {                _logger.LogDebug("Initialisiere Kontakte direkt ohne Person-Dependency");
                
                // Removed Person dependency - using direct Contact initialization
                _contacts.Clear();
                _nextId = 1;                // Create sample contacts directly since we removed Person dependency
                var sampleContacts = new List<Contact>
                {
                    new Contact { Id = 1, Name = "Maria Schmidt", Email = "maria.schmidt@company.com", Phone = GeneratePhoneNumber(), Status = ContactStatus.Online, Department = "Sales", LastSeen = DateTime.Now, Avatar = GetAvatarForDepartment("Sales") },
                    new Contact { Id = 2, Name = "Thomas Müller", Email = "thomas.mueller@company.com", Phone = GeneratePhoneNumber(), Status = ContactStatus.Away, Department = "Engineering", LastSeen = DateTime.Now.AddHours(-1), Avatar = GetAvatarForDepartment("Engineering") },
                    new Contact { Id = 3, Name = "Julia Weber", Email = "julia.weber@company.com", Phone = GeneratePhoneNumber(), Status = ContactStatus.Offline, Department = "Marketing", LastSeen = DateTime.Now.AddDays(-1), Avatar = GetAvatarForDepartment("Marketing") },
                    new Contact { Id = 4, Name = "Michael Wagner", Email = "michael.wagner@company.com", Phone = GeneratePhoneNumber(), Status = ContactStatus.Online, Department = "Support", LastSeen = DateTime.Now, Avatar = GetAvatarForDepartment("Support") },
                    new Contact { Id = 5, Name = "Anna Fischer", Email = "anna.fischer@company.com", Phone = GeneratePhoneNumber(), Status = ContactStatus.DoNotDisturb, Department = "Management", LastSeen = DateTime.Now.AddMinutes(-30), Avatar = GetAvatarForDepartment("Management") }
                };

                foreach (var contact in sampleContacts)
                {
                    _contacts.Add(contact);
                    _logger.LogDebug("Kontakt erstellt: {Name} (ID: {Id})", contact.Name, contact.Id);
                }

                // Nächste ID für neue Kontakte setzen
                _nextId = _contacts.Any() ? _contacts.Max(c => c.Id) + 1 : 1;

                _initialized = true;
                _logger.LogInformation("Erfolgreich {Count} Kontakte aus der Datenbank initialisiert", _contacts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Initialisieren der Kontakte aus der Datenbank");
                
                // Fallback zu Dummy-Kontakten wenn Datenbank nicht verfügbar
                await InitializeFallbackContactsAsync();
            }
        }

        /// <summary>
        /// Fallback-Methode für den Fall, dass die Datenbank nicht verfügbar ist
        /// </summary>
        private async Task InitializeFallbackContactsAsync()
        {
            _logger.LogWarning("Verwende Fallback-Kontakte da Datenbank nicht verfügbar");
            
            _contacts = new List<Contact>
            {
                new Contact
                {
                    Id = _nextId++,
                    Name = "Maria Schmidt",
                    Email = "maria.schmidt@example.com",
                    Phone = "+49 30 12345678",
                    Status = ContactStatus.Online,
                    IsFavorite = true,
                    LastSeen = DateTime.Now.AddMinutes(-5),
                    Notes = "Sales",
                    Avatar = "👩‍�"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Thomas Müller",
                    Email = "thomas.mueller@example.com",
                    Phone = "+49 89 87654321",
                    Status = ContactStatus.Away,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddHours(-1),
                    Notes = "Engineering",
                    Avatar = "👨‍💻"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Julia Weber",
                    Email = "julia.weber@example.com",
                    Phone = "+49 40 11223344",
                    Status = ContactStatus.Offline,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddDays(-1),
                    Notes = "Marketing",
                    Avatar = "👩‍🎨"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Michael Wagner",
                    Email = "michael.wagner@example.com",
                    Phone = "+49 351 22113344",
                    Status = ContactStatus.Online,
                    IsFavorite = true,
                    LastSeen = DateTime.Now.AddMinutes(-10),
                    Notes = "Support",
                    Avatar = "👨‍🔧"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Anna Fischer",
                    Email = "anna.fischer@example.com",
                    Phone = "+49 511 33224455",
                    Status = ContactStatus.DoNotDisturb,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddMinutes(-30),
                    Notes = "Management",
                    Avatar = "�‍💼"
                }
            };

            _initialized = true;
            await Task.CompletedTask;
        }

        /// <summary>
        /// Generiert eine E-Mail-Adresse basierend auf dem Namen
        /// </summary>
        private string GenerateEmailFromName(string name)
        {
            var parts = name.Split(' ');
            if (parts.Length >= 2)
            {
                var firstName = parts[0].ToLower();
                var lastName = parts[^1].ToLower(); // Letzter Teil als Nachname
                return $"{firstName}.{lastName}@example.com";
            }
            return $"{name.ToLower().Replace(" ", ".")}@example.com";
        }

        /// <summary>
        /// Generiert eine zufällige Telefonnummer
        /// </summary>
        private string GeneratePhoneNumber()
        {
            var random = new Random();
            return $"+49 {random.Next(30, 999)} {random.Next(1000000, 9999999)}";
        }        // Method removed - no longer needed as we only use Contact now

        /// <summary>
        /// Gibt einen Avatar für die Abteilung zurück
        /// </summary>
        private string GetAvatarForDepartment(string? department)
        {
            return department?.ToLower() switch
            {
                "sales" => "👩‍💼",
                "engineering" => "👨‍💻",
                "marketing" => "👩‍🎨",
                "support" => "👨‍🔧",
                "management" => "👩‍💼",
                "development" => "👨‍💻",
                "design" => "🎨",
                "hr" => "👥",
                _ => "👤"
            };
        }        public async Task<List<Contact>> GetContactsAsync()
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Rufe alle Kontakte ab");
            return _contacts.OrderBy(c => c.Name).ToList();
        }

        public async Task<Contact?> GetContactByIdAsync(int id)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Rufe Kontakt mit ID {Id} ab", id);
            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            return contact;
        }

        public async Task<Contact?> GetContactByNameAsync(string name)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Rufe Kontakt mit Namen '{Name}' ab", name);
            var contact = _contacts.FirstOrDefault(c =>                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            return contact;
        }

        public async Task<List<Contact>> SearchContactsAsync(string searchTerm)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Suche Kontakte mit Begriff '{SearchTerm}'", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetContactsAsync();

            var results = _contacts.Where(c =>
                    c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (c.Email?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.Notes?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(c => c.Name)
                .ToList();

            return results;
        }

        public async Task<List<Contact>> GetFavoriteContactsAsync()
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Rufe Favoriten-Kontakte ab");
            var favorites = _contacts.Where(c => c.IsFavorite)                .OrderBy(c => c.Name)
                .ToList();
            return favorites;
        }

        public async Task<List<Contact>> GetOnlineContactsAsync()
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Rufe Online-Kontakte ab");
            var online = _contacts.Where(c => c.Status == ContactStatus.Online)
                .OrderBy(c => c.Name)
                .ToList();
            return online;
        }

        public async Task<Contact> CreateContactAsync(Contact contact)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogInformation("Erstelle neuen Kontakt: {Name}", contact.Name);

            contact.Id = _nextId++;
            contact.CreatedAt = DateTime.Now;
            _contacts.Add(contact);

            return contact;
        }

        public async Task<Contact> UpdateContactAsync(Contact contact)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogInformation("Aktualisiere Kontakt: {Name} (ID: {Id})", contact.Name, contact.Id);            var existingContact = _contacts.FirstOrDefault(c => c.Id == contact.Id);
            if (existingContact != null)
            {
                var index = _contacts.IndexOf(existingContact);
                _contacts[index] = contact;
                return contact;
            }

            throw new ArgumentException($"Kontakt mit ID {contact.Id} nicht gefunden");
        }

        public async Task<bool> DeleteContactAsync(int id)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogInformation("Lösche Kontakt mit ID: {Id}", id);

            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            if (contact != null)
            {
                _contacts.Remove(contact);
                return true;
            }

            return false;
        }

        public async Task<bool> UpdateContactStatusAsync(int id, ContactStatus status)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Aktualisiere Status für Kontakt {Id} auf {Status}", id, status);            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            if (contact != null)
            {
                contact.Status = status;
                contact.LastSeen = DateTime.Now;
                return true;
            }

            return false;
        }

        public async Task<bool> SetFavoriteAsync(int id, bool isFavorite)
        {
            await InitializeContactsFromDatabaseAsync();
            _logger.LogDebug("Setze Favorit-Status für Kontakt {Id} auf {IsFavorite}", id, isFavorite);

            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            if (contact != null)
            {
                contact.IsFavorite = isFavorite;
                return true;
            }

            return false;
        }        /// <summary>
        /// Erstellt oder aktualisiert einen Kontakt für eine Person aus der Chat-Datenbank
        /// </summary>
        /// <param name="personName">Name der Person</param>
        /// <returns>Der entsprechende Kontakt oder null</returns>
        public async Task<Contact?> EnsureContactForPersonAsync(string personName)
        {
            await InitializeContactsFromDatabaseAsync();
            
            // Prüfen ob Kontakt bereits existiert
            var existingContact = await GetContactByNameAsync(personName);
            if (existingContact != null)
            {
                return existingContact;
            }

            // Neue Person in der Datenbank prüfen            // Person dependency removed - returning null as this functionality is deprecated
            _logger.LogWarning("CreateContactFromPersonAsync called but Person functionality is removed. PersonName: {PersonName}", personName);
            return null;
        }
    }
}
