using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services
{
    /// <summary>
    /// Service für die Kontaktverwaltung mit fest hinterlegten Testdaten
    /// </summary>
    public class ContactService : IContactService
    {
        private readonly ILogger<ContactService> _logger;
        private readonly List<Contact> _contacts;
        private int _nextId = 1;

        public ContactService(ILogger<ContactService> logger)
        {
            _logger = logger;
            _contacts = InitializeTestContacts();
        }

        /// <summary>
        /// Initialisiert die Test-Kontakte
        /// </summary>
        private List<Contact> InitializeTestContacts()
        {
            var contacts = new List<Contact>
            {
                new Contact
                {
                    Id = _nextId++,
                    Name = "Anna Schmidt",
                    Email = "anna.schmidt@example.com",
                    Phone = "+49 30 12345678",
                    Status = ContactStatus.Online,
                    IsFavorite = true,
                    LastSeen = DateTime.Now.AddMinutes(-5),
                    Notes = "Kollegin aus der Entwicklungsabteilung",
                    Avatar = "👩‍💻"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Max Mustermann",
                    Email = "max.mustermann@example.com",
                    Phone = "+49 89 87654321",
                    Status = ContactStatus.Away,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddHours(-2),
                    Notes = "Projektmanager",
                    Avatar = "👨‍💼"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Dr. Sarah Weber",
                    Email = "sarah.weber@example.com",
                    Phone = "+49 40 11223344",
                    Status = ContactStatus.Online,
                    IsFavorite = true,
                    LastSeen = DateTime.Now.AddMinutes(-1),
                    Notes = "Leiterin Forschung & Entwicklung",
                    Avatar = "👩‍🔬"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Tom Fischer",
                    Email = "tom.fischer@example.com",
                    Phone = "+49 69 55667788",
                    Status = ContactStatus.Busy,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddMinutes(-30),
                    Notes = "UI/UX Designer",
                    Avatar = "🎨"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Lisa Klein",
                    Email = "lisa.klein@example.com",
                    Phone = "+49 221 99887766",
                    Status = ContactStatus.Online,
                    IsFavorite = true,
                    LastSeen = DateTime.Now,
                    Notes = "DevOps Engineer",
                    Avatar = "⚙️"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Michael Brown",
                    Email = "michael.brown@example.com",
                    Phone = "+49 711 44556677",
                    Status = ContactStatus.Offline,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddHours(-8),
                    Notes = "Kunde - Stuttgart",
                    Avatar = "🏢"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Julia Hoffmann",
                    Email = "julia.hoffmann@example.com",
                    Phone = "+49 511 33224455",
                    Status = ContactStatus.DoNotDisturb,
                    IsFavorite = false,
                    LastSeen = DateTime.Now.AddHours(-1),
                    Notes = "QA Engineer",
                    Avatar = "🔍"
                },
                new Contact
                {
                    Id = _nextId++,
                    Name = "Robert Wagner",
                    Email = "robert.wagner@example.com",
                    Phone = "+49 351 22113344",
                    Status = ContactStatus.Online,
                    IsFavorite = true,
                    LastSeen = DateTime.Now.AddMinutes(-10),
                    Notes = "Technical Lead",
                    Avatar = "👨‍🚀"
                }
            };

            _logger.LogInformation("Initialisiert {Count} Test-Kontakte", contacts.Count);
            return contacts;
        }

        public Task<List<Contact>> GetContactsAsync()
        {
            _logger.LogDebug("Rufe alle Kontakte ab");
            return Task.FromResult(_contacts.OrderBy(c => c.Name).ToList());
        }

        public Task<Contact?> GetContactByIdAsync(int id)
        {
            _logger.LogDebug("Rufe Kontakt mit ID {Id} ab", id);
            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(contact);
        }

        public Task<Contact?> GetContactByNameAsync(string name)
        {
            _logger.LogDebug("Rufe Kontakt mit Namen '{Name}' ab", name);
            var contact = _contacts.FirstOrDefault(c => 
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(contact);
        }

        public Task<List<Contact>> SearchContactsAsync(string searchTerm)
        {
            _logger.LogDebug("Suche Kontakte mit Begriff '{SearchTerm}'", searchTerm);
            
            if (string.IsNullOrWhiteSpace(searchTerm))
                return GetContactsAsync();

            var results = _contacts.Where(c => 
                c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (c.Email?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.Notes?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(c => c.Name)
                .ToList();

            return Task.FromResult(results);
        }

        public Task<List<Contact>> GetFavoriteContactsAsync()
        {
            _logger.LogDebug("Rufe Favoriten-Kontakte ab");
            var favorites = _contacts.Where(c => c.IsFavorite)
                .OrderBy(c => c.Name)
                .ToList();
            return Task.FromResult(favorites);
        }

        public Task<List<Contact>> GetOnlineContactsAsync()
        {
            _logger.LogDebug("Rufe Online-Kontakte ab");
            var online = _contacts.Where(c => c.Status == ContactStatus.Online)
                .OrderBy(c => c.Name)
                .ToList();
            return Task.FromResult(online);
        }

        public Task<Contact> CreateContactAsync(Contact contact)
        {
            _logger.LogInformation("Erstelle neuen Kontakt: {Name}", contact.Name);
            
            contact.Id = _nextId++;
            contact.CreatedAt = DateTime.Now;
            _contacts.Add(contact);
            
            return Task.FromResult(contact);
        }

        public Task<Contact> UpdateContactAsync(Contact contact)
        {
            _logger.LogInformation("Aktualisiere Kontakt: {Name} (ID: {Id})", contact.Name, contact.Id);
            
            var existingContact = _contacts.FirstOrDefault(c => c.Id == contact.Id);
            if (existingContact != null)
            {
                var index = _contacts.IndexOf(existingContact);
                _contacts[index] = contact;
                return Task.FromResult(contact);
            }
            
            throw new ArgumentException($"Kontakt mit ID {contact.Id} nicht gefunden");
        }

        public Task<bool> DeleteContactAsync(int id)
        {
            _logger.LogInformation("Lösche Kontakt mit ID: {Id}", id);
            
            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            if (contact != null)
            {
                _contacts.Remove(contact);
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }

        public Task<bool> UpdateContactStatusAsync(int id, ContactStatus status)
        {
            _logger.LogDebug("Aktualisiere Status für Kontakt {Id} auf {Status}", id, status);
            
            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            if (contact != null)
            {
                contact.Status = status;
                contact.LastSeen = DateTime.Now;
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }

        public Task<bool> SetFavoriteAsync(int id, bool isFavorite)
        {
            _logger.LogDebug("Setze Favorit-Status für Kontakt {Id} auf {IsFavorite}", id, isFavorite);
            
            var contact = _contacts.FirstOrDefault(c => c.Id == id);
            if (contact != null)
            {
                contact.IsFavorite = isFavorite;
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
    }
}
