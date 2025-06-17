using System;
using System.ComponentModel.DataAnnotations;

namespace LocalAiDemo.Shared.Models
{
    /// <summary>
    /// Repräsentiert einen Kontakt im System
    /// </summary>
    public class Contact
    {        /// <summary>
        /// Eindeutige ID des Kontakts
        /// </summary>
        public int Id { get; set; }        /// <summary>
        /// Name des Kontakts
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;        /// <summary>
        /// E-Mail-Adresse des Kontakts
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Telefonnummer des Kontakts
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Avatar/Profilbild URL oder Base64
        /// </summary>
        public string? Avatar { get; set; }

        /// <summary>
        /// Alternative Avatar URL (für Fallback)
        /// </summary>
        public string AvatarUrl { get; set; } = string.Empty;        /// <summary>
        /// Status des Kontakts (Online, Offline, Away)
        /// </summary>
        public ContactStatus Status { get; set; } = ContactStatus.Offline;

        /// <summary>
        /// Letzte Aktivität des Kontakts
        /// </summary>
        public DateTime LastSeen { get; set; } = DateTime.Now;

        /// <summary>
        /// Abteilung des Kontakts (für Organisationen: Sales, Support, Engineering, etc.)
        /// </summary>
        public string? Department { get; set; }

        /// <summary>
        /// Ob der Kontakt ein Favorit ist
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Zusätzliche Notizen zum Kontakt
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Erstellungsdatum des Kontakts
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Status-Optionen für Kontakte
    /// </summary>
    public enum ContactStatus
    {
        Offline,
        Online,
        Away,
        Busy,        DoNotDisturb
    }
}