using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services.Search;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SQLite;
using System.Reflection;

namespace LocalAiDemo.Shared.Services
{
    public interface IChatDatabaseService
    {
        Task InitializeDatabaseAsync();

        Task<List<Chat>> GetAllChatsAsync();

        Task<Chat?> GetChatAsync(int chatId);

        Task<List<Chat>> GetChatsByContactAsync(int contactId);

        Task<int> SaveChatAsync(Chat chat);

        Task<List<Contact>> GetAllContactsAsync();

        Task<Contact?> GetContactAsync(int contactId);

        Task<int> SaveContactAsync(Contact contact);

        Task<Chat?> GetOrCreateChatForContactAsync(int contactId);

        Task<List<Chat>> FindSimilarChatsAsync(float[] embedding, int limit = 5);

        // New: Chat segment methods
        Task<int> SaveChatSegmentAsync(ChatSegment segment);

        Task<List<ChatSegment>> GetSegmentsForChatAsync(int chatId);

        Task<ChatSegment?> GetChatSegmentAsync(int segmentId);

        Task DeleteChatSegmentAsync(int segmentId);

        Task<List<ChatSegment>> GetAllChatSegmentsAsync();

        Task UpdateChatSegmentAsync(ChatSegment segment);
    }

    public class ChatDatabaseService : IChatDatabaseService
    {
        private readonly SqliteConnection _databaseConnection;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ChatDatabaseService> _logger;
        private readonly SqliteVectorSearchService _sqliteVectorSearchService;
        private bool _vectorSearchAvailable = false; // Track if vector search is available

        public ChatDatabaseService(IEmbeddingService embeddingService, ILogger<ChatDatabaseService> logger,
            SqliteVectorSearchService sqliteVectorSearchService)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            _sqliteVectorSearchService = sqliteVectorSearchService;

            // Set database path in the app's local data folder
            string dbName = "chatdatabase.db3";

            // For MAUI apps, use the app's data directory
#if WINDOWS
            var databasePath =
 Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dbName);
#else
            var databasePath =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), dbName);
#endif

            _logger.LogInformation("Database path: {DbPath}", databasePath);
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);

                // Create the database connection
                _databaseConnection =
                    new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
                _databaseConnection.Open();
                _logger.LogInformation("Database connection created successfully");

                // Initialize vector search capabilities Try to enable vector search if not already enabled
                _logger.LogInformation("Attempting to enable vector search through SqliteVectorSearchService service");

                var enabled = _sqliteVectorSearchService.EnableVectorSearchAsync(_databaseConnection).Result;
                _logger.LogInformation("Vector search enabled: {IsEnabled}", enabled);

                if (enabled == false)
                {
                    throw new Exception("Vector search could not be enabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database connection: {ErrorMessage}", ex.Message);
                // Still create the connection - it might work later
                throw;
            }
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogDebug("Starting database initialization...");

                // Create tables if they don't exist - using direct SQL with Microsoft.Data.Sqlite
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Contact (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            AvatarUrl TEXT,
                            Status TEXT,
                            LastSeen TEXT,
                            Department TEXT
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("Contact table created");
                }

                // For the Chat table, we need to serialize the embedding vector
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"                        CREATE TABLE IF NOT EXISTS Chat (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Title TEXT,
                            CreatedAt TEXT,
                            ContactId INTEGER,
                            IsActive INTEGER,
                            EmbeddingVector BLOB,
                            FOREIGN KEY(ContactId) REFERENCES Contact(Id)
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("Chat table created");
                } // For the ChatMessage table

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ChatMessage (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatId INTEGER,
                            Content TEXT,
                            Timestamp TEXT,
                            IsUser INTEGER,
                            EmbeddingVector BLOB,
                            SegmentId INTEGER,
                            FOREIGN KEY(ChatId) REFERENCES Chat(Id),
                            FOREIGN KEY(SegmentId) REFERENCES ChatSegment(Id)
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("ChatMessage table created");
                }

                // For the ChatSegment table
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ChatSegment (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatId INTEGER,
                            SegmentDate TEXT,
                            Title TEXT,
                            CombinedContent TEXT,
                            EmbeddingVector BLOB,
                            MessageCount INTEGER,
                            StartTime TEXT,
                            EndTime TEXT,
                            CreatedAt TEXT,
                            Keywords TEXT,
                            FOREIGN KEY(ChatId) REFERENCES Chat(Id)
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("ChatSegment table created");
                }

                // Create virtual table for vector search if SQLite supports it
                try
                {
                    // Try to create the vector index table - if this fails, vector search is not available
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            CREATE VIRTUAL TABLE IF NOT EXISTS chat_vectors USING vec0(
                                embedding_vector FLOAT[128],
                                chat_id INTEGER UNINDEXED
                            )";
                        await Task.Run(() => command.ExecuteNonQuery());
                        _logger.LogDebug("Vector search table created successfully - vector search is available");
                        _vectorSearchAvailable = true; // Vector search is available
                    }
                }
                catch (SqliteException ex)
                {
                    _logger.LogDebug("Vector table creation failed (expected if not supported): {ErrorMessage}",
                        ex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating vector table: {ErrorMessage}", ex.Message);
                    _logger.LogInformation("Vector search is not available - will use fallback similarity calculation");
                }

                // Populate with some sample data if empty
                await SeedInitialDataIfNeededAsync();
            }
            catch (Exception ex)
            {
                // Log any initialization errors
                _logger.LogError(ex, "Database initialization error: {ErrorMessage}", ex.Message);
                throw; // Rethrow to allow higher levels to handle or log the error
            }
        }

        private async Task SeedInitialDataIfNeededAsync()
        {
            try
            {
                // Check if we have any contacts
                List<Contact> contacts = new List<Contact>();
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM Contact";
                    var contactCount = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                    _logger.LogDebug("Database check: Found {ContactCount} existing contacts", contactCount);

                    // If we have contacts, load them to contacts list
                    if (contactCount > 0)
                    {
                        command.CommandText = "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Contact";
                        using (var reader = await Task.Run(() => command.ExecuteReader()))
                        {
                            while (await Task.Run(() => reader.Read()))
                            {
                                contacts.Add(new Contact
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Status = Enum.TryParse<ContactStatus>(
                                        reader.IsDBNull(3) ? "Offline" : reader.GetString(3), out var status)
                                        ? status
                                        : ContactStatus.Offline,
                                    LastSeen = reader.IsDBNull(4)
                                        ? DateTime.MinValue
                                        : DateTime.Parse(reader.GetString(4)),
                                    Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                                });
                            }
                        }
                    }
                }

                // Force seeding for testing purposes - remove this in production
                if (contacts.Count < 5) // Ensure we have at least 5 contacts
                {
                    _logger.LogInformation(
                        "Seeding database with sample contact data"); // Clear existing contacts first to avoid duplicates (only for testing)
                    if (contacts.Count > 0)
                    {
                        using (var command = _databaseConnection.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM Contact";
                            await Task.Run(() => command.ExecuteNonQuery());
                            _logger.LogDebug("Cleared existing contact data");
                        }
                    }

                    // Add sample contacts
                    var sampleContacts = new List<Contact>
                    {
                        new Contact
                        {
                            Id = 1, Name = "Maria Schmidt", Status = ContactStatus.Online, Department = "Sales",
                            LastSeen = DateTime.Now, AvatarUrl = "person_1.png"
                        },
                        new Contact
                        {
                            Id = 2, Name = "Thomas Müller", Status = ContactStatus.Away, Department = "Engineering",
                            LastSeen = DateTime.Now.AddHours(-1), AvatarUrl = "person_2.png"
                        },
                        new Contact
                        {
                            Id = 3, Name = "Julia Weber", Status = ContactStatus.Offline, Department = "Marketing",
                            LastSeen = DateTime.Now.AddDays(-1), AvatarUrl = "person_3.png"
                        },
                        new Contact
                        {
                            Id = 4, Name = "Michael Wagner", Status = ContactStatus.Online, Department = "Support",
                            LastSeen = DateTime.Now, AvatarUrl = "person_4.png"
                        },
                        new Contact
                        {
                            Id = 5, Name = "Anna Fischer", Status = ContactStatus.DoNotDisturb,
                            Department = "Management", LastSeen = DateTime.Now.AddMinutes(-30),
                            AvatarUrl = "person_5.png"
                        }
                    };

                    foreach (var contact in sampleContacts)
                    {
                        try
                        {
                            using (var command = _databaseConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                    INSERT INTO Contact (Id, Name, AvatarUrl, Status, LastSeen, Department)
                                    VALUES ($id, $name, $avatar, $status, $lastSeen, $department)";
                                command.Parameters.AddWithValue("$id", contact.Id);
                                command.Parameters.AddWithValue("$name", contact.Name);
                                command.Parameters.AddWithValue("$avatar", contact.AvatarUrl ?? (object)DBNull.Value);
                                command.Parameters.AddWithValue("$status", contact.Status.ToString());
                                command.Parameters.AddWithValue("$lastSeen", contact.LastSeen.ToString("o"));
                                command.Parameters.AddWithValue("$department",
                                    contact.Department ?? (object)DBNull.Value);

                                int count = await Task.Run(() => command.ExecuteNonQuery());
                                _logger.LogDebug("Added {Count} contact: {ContactName}", count, contact.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to add contact {ContactName}: {ErrorMessage}", contact.Name,
                                ex.Message);
                        }
                    }

                    // Verify the contacts were added
                    contacts = new List<Contact>();
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Person";
                        {
                            command.CommandText =
                                "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Contact";
                            using (var reader = await Task.Run(() => command.ExecuteReader()))
                            {
                                while (await Task.Run(() => reader.Read()))
                                {
                                    contacts.Add(new Contact
                                    {
                                        Id = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                        Status = Enum.TryParse<ContactStatus>(
                                            reader.IsDBNull(3) ? "Offline" : reader.GetString(3), out var status)
                                            ? status
                                            : ContactStatus.Offline,
                                        LastSeen = reader.IsDBNull(4)
                                            ? DateTime.MinValue
                                            : DateTime.Parse(reader.GetString(4)),
                                        Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                                    });
                                }
                            }
                        }

                        _logger.LogDebug("After seeding: Found {ContactCount} contacts in database", contacts.Count);

                        // List all contacts for debugging
                        foreach (var contact in contacts)
                        {
                            _logger.LogDebug("Contact in DB: ID={ContactId}, Name={ContactName}, Dept={Department}",
                                contact.Id, contact.Name, contact.Department);
                        }
                    }

                    // Now check for existing chats
                    int chatCount = 0;
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM Chat";
                        chatCount = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                        _logger.LogDebug("Database check: Found {ChatCount} existing chats", chatCount);
                    } // Seed sample chats if none exist

                    if (chatCount == 0 && contacts.Count > 0)
                    {
                        _logger.LogInformation("Seeding database with sample chat data");

                        // Sample chats with different dates spanning several days
                        var sampleChats = new List<Chat>
                        {
                            // Today's chat with Maria (Sales)
                            new Chat
                            {
                                Title = "Produktberatung Herr Meyer",
                                CreatedAt = DateTime.Now.Date.AddHours(9), // 9 AM today
                                ContactId = 1, // Maria Schmidt
                                IsActive = true,
                                Messages = new List<ChatMessage>
                                {
                                    new ChatMessage
                                    {
                                        Content = "Guten Morgen Herr Meyer! Wie ist Ihr Meeting gestern gelaufen?",
                                        Timestamp = DateTime.Now.Date.AddHours(9).AddMinutes(0), IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Guten Morgen Frau Schmidt, das Meeting war sehr produktiv. Wir haben großes Interesse an Ihrer Produktlinie.",
                                        Timestamp = DateTime.Now.Date.AddHours(9).AddMinutes(2), IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Das freut mich zu hören! Wären Sie an einer Produktpräsentation nächste Woche interessiert?",
                                        Timestamp = DateTime.Now.Date.AddHours(9).AddMinutes(3), IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Ja, sehr gerne. Wie wäre es mit Dienstag um 14 Uhr?",
                                        Timestamp = DateTime.Now.Date.AddHours(9).AddMinutes(5), IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Dienstag 14 Uhr passt perfekt. Ich reserviere auch einen Tisch für 18 Uhr im Restaurant 'Zur Eiche', wenn Sie anschließend Zeit für ein Abendessen haben?",
                                        Timestamp = DateTime.Now.Date.AddHours(9).AddMinutes(7), IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Das klingt hervorragend. Ich freue mich auf beides!",
                                        Timestamp = DateTime.Now.Date.AddHours(9).AddMinutes(9), IsUser = false
                                    }
                                }
                            },

                            // Yesterday's chat with Thomas (Engineering)
                            new Chat
                            {
                                Title = "Technische Anfrage Schmidt GmbH",
                                CreatedAt = DateTime.Now.Date.AddDays(-1).AddHours(14), // 2 PM yesterday
                                ContactId = 2, // Thomas Müller
                                IsActive = false,
                                Messages = new List<ChatMessage>
                                {
                                    new ChatMessage
                                    {
                                        Content =
                                            "Hallo Herr Fischer, haben Sie die technischen Spezifikationen für die neue Produktserie schon erhalten?",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(0),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Guten Tag Herr Müller, ja, ich habe sie gestern bekommen. Ich habe einige Fragen zur Implementierung.",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(1),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Gerne. Was genau möchten Sie wissen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(3),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Wie hoch ist die Skalierbarkeit des Systems? Wir planen eine Erweiterung im nächsten Quartal.",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(5),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Das System ist für bis zu 500 Benutzer ausgelegt. Wir könnten das bei einem Mittagessen am Montag näher besprechen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(7),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Montag Mittag klingt gut. Haben Sie einen Restaurantvorschlag?",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(9),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Das 'Bella Italia' bietet einen ruhigen Besprechungsraum. Ich reserviere für 12:30 Uhr.",
                                        Timestamp = DateTime.Now.Date.AddDays(-1).AddHours(14).AddMinutes(11),
                                        IsUser = true
                                    }
                                }
                            },

                            // Chat from 3 days ago with Julia (Marketing)
                            new Chat
                            {
                                Title = "Produkteinführung Herbstkampagne",
                                CreatedAt = DateTime.Now.Date.AddDays(-3).AddHours(11), // 11 AM three days ago
                                ContactId = 3, // Julia Weber
                                IsActive = false,
                                Messages = new List<ChatMessage>
                                {
                                    new ChatMessage
                                    {
                                        Content =
                                            "Frau Schulz, haben Sie Zeit, über die Marketingstrategie für unsere Herbstprodukte zu sprechen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-3).AddHours(11).AddMinutes(0),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Natürlich, Frau Weber. Haben Sie schon einen konkreten Ansatz?",
                                        Timestamp = DateTime.Now.Date.AddDays(-3).AddHours(11).AddMinutes(2),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Ich schlage eine Vorstellung bei einem VIP-Abendessen mit unseren Top-10-Kunden vor.",
                                        Timestamp = DateTime.Now.Date.AddDays(-3).AddHours(11).AddMinutes(5),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Ausgezeichnete Idee! Welche Location schwebt Ihnen vor?",
                                        Timestamp = DateTime.Now.Date.AddDays(-3).AddHours(11).AddMinutes(7),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Das Grand Hotel hat einen exklusiven Saal mit Showbühne. Perfekt für die Produktpräsentation und anschließendes Dinner.",
                                        Timestamp = DateTime.Now.Date.AddDays(-3).AddHours(11).AddMinutes(9),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Das klingt perfekt. Lassen Sie uns morgen bei einem Kaffee die Details besprechen.",
                                        Timestamp = DateTime.Now.Date.AddDays(-3).AddHours(11).AddMinutes(12),
                                        IsUser = false
                                    }
                                }
                            },

                            // Chat from a week ago with Michael (Support)
                            new Chat
                            {
                                Title = "Termin mit Wagner & Co.",
                                CreatedAt = DateTime.Now.Date.AddDays(-7).AddHours(16), // 4 PM a week ago
                                ContactId = 4, // Michael Wagner
                                IsActive = false,
                                Messages = new List<ChatMessage>
                                {
                                    new ChatMessage
                                    {
                                        Content =
                                            "Sehr geehrter Herr Becker, haben Sie nächste Woche Zeit für ein Mittagessen, um die Vertragsverlängerung zu besprechen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-7).AddHours(16).AddMinutes(0),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Guten Tag Herr Wagner. Ja, Mittwoch oder Donnerstag würde mir passen.",
                                        Timestamp = DateTime.Now.Date.AddDays(-7).AddHours(16).AddMinutes(5),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Perfekt, wie wäre Donnerstag 13 Uhr im 'Steakhouse am Markt'?",
                                        Timestamp = DateTime.Now.Date.AddDays(-7).AddHours(16).AddMinutes(10),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Das passt mir gut. Werden Sie auch die neuen Preismodelle mitbringen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-7).AddHours(16).AddMinutes(15),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Ja, ich bereite alle Unterlagen vor und bringe auch Muster der neuen Premium-Produktlinie mit.",
                                        Timestamp = DateTime.Now.Date.AddDays(-7).AddHours(16).AddMinutes(20),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Hervorragend. Ich freue mich auf unser Treffen und das gemeinsame Mittagessen.",
                                        Timestamp = DateTime.Now.Date.AddDays(-7).AddHours(16).AddMinutes(25),
                                        IsUser = false
                                    }
                                }
                            },

                            // Chat from two weeks ago with Anna (Management)
                            new Chat
                            {
                                Title = "Quartalsplanung mit Hauptkunden",
                                CreatedAt = DateTime.Now.Date.AddDays(-14).AddHours(10), // 10 AM two weeks ago
                                ContactId = 5, // Anna Fischer
                                IsActive = false,
                                Messages = new List<ChatMessage>
                                {
                                    new ChatMessage
                                    {
                                        Content =
                                            "Guten Morgen Frau Meier, hätten Sie Zeit für ein Abendessen am Donnerstag, um unsere Strategie für das kommende Quartal zu besprechen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(0),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Guten Morgen Frau Fischer. Donnerstag klingt gut, welches Restaurant schlagen Sie vor?",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(5),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Ich würde das 'Seeblick' vorschlagen. Die haben einen separaten Raum für Geschäftsdinner mit diskreter Atmosphäre.",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(7),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Ausgezeichnete Wahl. Passt 19 Uhr für Sie?",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(10),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "19 Uhr ist perfekt. Ich freue mich sehr, dass Sie Zeit haben. Soll ich die neuen Produktmuster mitbringen?",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(12),
                                        IsUser = true
                                    },
                                    new ChatMessage
                                    {
                                        Content =
                                            "Ja, bitte. Ich bin besonders an der Premium-Serie interessiert, die Sie letzte Woche erwähnt hatten.",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(15),
                                        IsUser = false
                                    },
                                    new ChatMessage
                                    {
                                        Content = "Wunderbar, ich werde alles vorbereiten. Bis Donnerstag!",
                                        Timestamp = DateTime.Now.Date.AddDays(-14).AddHours(10).AddMinutes(17),
                                        IsUser = true
                                    }
                                }
                            }
                        };

                        // Save each chat to the database
                        foreach (var chat in sampleChats)
                        {
                            try
                            {
                                int chatId = await SaveChatAsync(chat);
                                _logger.LogDebug("Added sample chat: {ChatTitle} with ID {ChatId}", chat.Title, chatId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to add sample chat {ChatTitle}: {ErrorMessage}",
                                    chat.Title, ex.Message);
                            }
                        }

                        // Verify chats were added
                        using (var command = _databaseConnection.CreateCommand())
                        {
                            command.CommandText = "SELECT COUNT(*) FROM Chat";
                            chatCount = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                            _logger.LogDebug("After seeding: Found {ChatCount} chats in database", chatCount);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR seeding database: {ErrorMessage}", ex.Message);
            }
        }

        public async Task<List<Chat>> GetAllChatsAsync()
        {
            try
            {
                // Get all chats with their associated person
                var chats = new List<Chat>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Id, Title, CreatedAt, ContactId, IsActive, EmbeddingVector FROM Chat ORDER BY CreatedAt DESC";

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            chats.Add(new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt =
                                    reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                ContactId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5))
                            });
                        }
                    }
                }

                _logger.LogDebug("GetAllChatsAsync: Retrieved {ChatCount} chats",
                    chats.Count); // Load contacts for each chat
                foreach (var chat in chats)
                {
                    if (chat.ContactId.HasValue)
                    {
                        chat.Contact = await GetContactAsync(chat.ContactId.Value);
                    }

                    chat.Messages = await GetChatMessagesAsync(chat.Id);
                }

                return chats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllChatsAsync: {ErrorMessage}", ex.Message);
                return new List<Chat>();
            }
        }

        public async Task<Chat?> GetChatAsync(int chatId)
        {
            try
            {
                Chat? chat = null;

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Id, Title, CreatedAt, ContactId, IsActive, EmbeddingVector FROM Chat WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", chatId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        if (await Task.Run(() => reader.Read()))
                        {
                            chat = new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt =
                                    reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                ContactId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5))
                            };
                        }
                    }
                }

                if (chat != null)
                {
                    if (chat.ContactId.HasValue)
                    {
                        chat.Contact = await GetContactAsync(chat.ContactId.Value);
                    }

                    chat.Messages = await GetChatMessagesAsync(chatId);
                    _logger.LogDebug("Retrieved chat {ChatId} with {MessageCount} messages",
                        chatId, chat.Messages.Count);
                }
                else
                {
                    _logger.LogWarning("Chat with ID {ChatId} not found", chatId);
                }

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat {ChatId}: {ErrorMessage}", chatId, ex.Message);
                return null;
            }
        }

        public async Task<List<Chat>> GetChatsByContactAsync(int contactId)
        {
            try
            {
                var chats = new List<Chat>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Id, Title, CreatedAt, ContactId, IsActive, EmbeddingVector FROM Chat WHERE ContactId = @contactId ORDER BY CreatedAt DESC";
                    command.Parameters.AddWithValue("@contactId", contactId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            chats.Add(new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt =
                                    reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                ContactId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5))
                            });
                        }
                    }
                }

                _logger.LogDebug("Found {ChatCount} chats for contact {ContactId}", chats.Count, contactId);
                foreach (var chat in chats)
                {
                    if (chat.ContactId.HasValue)
                    {
                        chat.Contact = await GetContactAsync(chat.ContactId.Value);
                    }
                }

                return chats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chats for contact {ContactId}: {ErrorMessage}",
                    contactId, ex.Message);
                return new List<Chat>();
            }
        }

        public async Task<Chat?> GetOrCreateChatForContactAsync(int contactId)
        {
            try
            {
                // First try to get an existing active chat for the contact
                var existingChats = await GetChatsByContactAsync(contactId);
                var activeChat = existingChats.FirstOrDefault(c => c.IsActive);

                if (activeChat != null)
                {
                    return activeChat;
                }

                // If no active chat exists, create a new one
                var contact = await GetContactAsync(contactId);
                if (contact == null)
                {
                    _logger.LogError("Cannot create chat for non-existent contact {ContactId}", contactId);
                    return null;
                }

                var newChat = new Chat
                {
                    Title = $"Chat with {contact.Name}",
                    CreatedAt = DateTime.Now,
                    ContactId = contactId,
                    IsActive = true,
                    Messages = new List<ChatMessage>(),
                    Contact = contact
                };

                var chatId = await SaveChatAsync(newChat);
                newChat.Id = chatId;

                _logger.LogDebug("Created new chat {ChatId} for contact {ContactId}", chatId, contactId);
                return newChat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating chat for contact {ContactId}: {ErrorMessage}",
                    contactId, ex.Message);
                return null;
            }
        }

        private async Task<List<ChatMessage>> GetChatMessagesAsync(int chatId)
        {
            try
            {
                var messages = new List<ChatMessage>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Id, ChatId, Content, Timestamp, IsUser, EmbeddingVector FROM ChatMessage WHERE ChatId = @chatId ORDER BY Timestamp";
                    command.Parameters.AddWithValue("@chatId", chatId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            messages.Add(new ChatMessage
                            {
                                Id = reader.GetInt32(0),
                                ChatId = reader.GetInt32(1),
                                Content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Timestamp =
                                    reader.IsDBNull(3) ? DateTime.MinValue : DateTime.Parse(reader.GetString(3)),
                                IsUser = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5))
                            });
                        }
                    }
                }

                _logger.LogDebug("Retrieved {MessageCount} messages for chat {ChatId}", messages.Count, chatId);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for chat {ChatId}: {ErrorMessage}", chatId, ex.Message);
                return new List<ChatMessage>();
            }
        }

        public async Task<int> SaveChatAsync(Chat chat)
        {
            if (chat.Id == 0)
            {
                // Insert new chat
                try
                {
                    // First save the chat
                    chat.CreatedAt = DateTime.Now;

                    // Convert embedding vector to blob for storage
                    if (chat.EmbeddingVector == null)
                    {
                        // Generate embedding from chat title and first message
                        string contentToEmbed = chat.Title ?? string.Empty;
                        if (chat.Messages.Count > 0 && chat.Messages.First().Content != null)
                        {
                            contentToEmbed += " " + chat.Messages.First().Content;
                        }

                        chat.EmbeddingVector = _embeddingService.GenerateEmbedding(contentToEmbed);
                    }

                    _logger.LogDebug("Inserting new chat: Title='{Title}', ContactId={ContactId}",
                        chat.Title, chat.ContactId);

                    // Add the chat to the database
                    int chatId;
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO Chat (Title, CreatedAt, ContactId, IsActive, EmbeddingVector)
                            VALUES (@title, @createdAt, @ContactId, @isActive, @embeddingVector);
                            SELECT last_insert_rowid();";

                        command.Parameters.AddWithValue("@title", chat.Title ?? string.Empty);
                        command.Parameters.AddWithValue("@createdAt", chat.CreatedAt.ToString("o"));
                        command.Parameters.AddWithValue("@ContactId", chat.ContactId);
                        command.Parameters.AddWithValue("@isActive", chat.IsActive ? 1 : 0);

                        var embeddingBytes =
                            chat.EmbeddingVector != null ? SerializeVector(chat.EmbeddingVector) : null;
                        if (embeddingBytes != null)
                        {
                            command.Parameters.AddWithValue("@embeddingVector", embeddingBytes);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@embeddingVector", DBNull.Value);
                        }

                        chatId = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                    }

                    chat.Id = chatId;
                    _logger.LogDebug("Chat inserted successfully with ID: {ChatId}", chat.Id);

                    // Then save all messages
                    if (chat.Messages.Any())
                    {
                        _logger.LogDebug("Saving {MessageCount} messages for chat {ChatId}",
                            chat.Messages.Count, chat.Id);

                        foreach (var message in chat.Messages)
                        {
                            try
                            {
                                // Set ChatId on message
                                message.ChatId = chat.Id;

                                int messageId = await SaveChatMessageAsync(message, chat.Id);
                                _logger.LogDebug("Saved message with ID {MessageId}", messageId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to save message: {ErrorMessage}", ex.Message);
                                // Continue with other messages even if one fails
                            }
                        }
                    }

                    // Only try to add to vector search table if we know it's available
                    if (_vectorSearchAvailable && chat.EmbeddingVector != null)
                    {
                        try
                        {
                            using (var command = _databaseConnection.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO chat_vectors(embedding_vector, chat_id) VALUES (@embeddingVector, @chatId)";

                                var embeddingBytes = SerializeVector(chat.EmbeddingVector);
                                command.Parameters.AddWithValue("@embeddingVector", embeddingBytes);
                                command.Parameters.AddWithValue("@chatId", chat.Id);

                                await Task.Run(() => command.ExecuteNonQuery());
                                _logger.LogDebug("Added chat {ChatId} to vector search index", chat.Id);
                            }
                        }
                        catch (Microsoft.Data.Sqlite.SqliteException ex)
                        {
                            _logger.LogDebug("Error adding to vector search table: {ErrorMessage}", ex.Message);
                            // Don't update _vectorSearchAvailable here as it might be a different issue
                        }
                    }

                    return chat.Id;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to insert chat: {ErrorMessage}", ex.Message);
                    throw; // Re-throw to ensure the error is propagated
                }
            }
            else
            {
                try
                {
                    // Update existing chat
                    _logger.LogDebug("Updating existing chat {ChatId}", chat.Id);

                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            UPDATE Chat
                            SET Title = @title,
                                CreatedAt = @createdAt,
                                ContactId = @ContactId,
                                IsActive = @isActive,
                                EmbeddingVector = @embeddingVector
                            WHERE Id = @id";

                        command.Parameters.AddWithValue("@id", chat.Id);
                        command.Parameters.AddWithValue("@title", chat.Title ?? string.Empty);
                        command.Parameters.AddWithValue("@createdAt", chat.CreatedAt.ToString("o"));
                        command.Parameters.AddWithValue("@ContactId", chat.ContactId);
                        command.Parameters.AddWithValue("@isActive", chat.IsActive ? 1 : 0);

                        var embeddingBytes =
                            chat.EmbeddingVector != null ? SerializeVector(chat.EmbeddingVector) : null;
                        if (embeddingBytes != null)
                        {
                            command.Parameters.AddWithValue("@embeddingVector", embeddingBytes);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@embeddingVector", DBNull.Value);
                        }

                        await Task.Run(() => command.ExecuteNonQuery());
                    }

                    // Handle messages - first get existing messages
                    var existingMessages = await GetChatMessagesAsync(chat.Id);

                    // New messages to add
                    var newMessages = chat.Messages
                        .Where(m => !existingMessages.Any(em => em.Id == m.Id))
                        .ToList();

                    _logger.LogDebug("Found {NewMessageCount} new messages to add to chat {ChatId}",
                        newMessages.Count, chat.Id);

                    foreach (var message in newMessages)
                    {
                        // Make sure ChatId is set on the message
                        message.ChatId = chat.Id;
                        await SaveChatMessageAsync(message, chat.Id);
                    }

                    return chat.Id;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating chat {ChatId}: {ErrorMessage}",
                        chat.Id, ex.Message);
                    throw;
                }
            }
        }

        private async Task<int> SaveChatMessageAsync(ChatMessage message, int chatId)
        {
            try
            {
                _logger.LogDebug("SaveChatMessageAsync: ChatId={ChatId}, Content={MessagePreview}..., IsUser={IsUser}",
                    chatId,
                    message.Content?.Length > 20 ? message.Content?.Substring(0, 20) + "..." : message.Content,
                    message.IsUser);

                // Fix: Ensure the message has a ChatId set
                message.ChatId = chatId;

                // Generate embedding only if vector search is available and content is provided
                if (_vectorSearchAvailable && message.EmbeddingVector == null && !string.IsNullOrEmpty(message.Content))
                {
                    try
                    {
                        message.EmbeddingVector = _embeddingService.GenerateEmbedding(message.Content);
                        _logger.LogDebug("Generated embedding for message in chat {ChatId}", chatId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate embedding: {ErrorMessage}", ex.Message);
                        // Continue without embedding, don't make this a fatal error
                        message.EmbeddingVector = null;
                    }
                }

                if (message.Id == 0)
                {
                    // Insert new message
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO ChatMessage (ChatId, Content, Timestamp, IsUser, EmbeddingVector)
                            VALUES (@chatId, @content, @timestamp, @isUser, @embeddingVector);
                            SELECT last_insert_rowid();";

                        command.Parameters.AddWithValue("@chatId", chatId);
                        command.Parameters.AddWithValue("@content", message.Content ?? string.Empty);
                        command.Parameters.AddWithValue("@timestamp", message.Timestamp.ToString("o"));
                        command.Parameters.AddWithValue("@isUser", message.IsUser ? 1 : 0);

                        if (message.EmbeddingVector != null)
                        {
                            var embeddingBytes = SerializeVector(message.EmbeddingVector);
                            command.Parameters.AddWithValue("@embeddingVector", embeddingBytes);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@embeddingVector", DBNull.Value);
                        }

                        var lastId = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                        message.Id = lastId;
                        _logger.LogDebug("Message inserted with ID: {MessageId}", lastId);

                        return lastId;
                    }
                }
                else
                {
                    // Update existing message
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            UPDATE ChatMessage
                            SET Content = @content,
                                Timestamp = @timestamp,
                                IsUser = @isUser,
                                EmbeddingVector = @embeddingVector
                            WHERE Id = @id AND ChatId = @chatId";

                        command.Parameters.AddWithValue("@content", message.Content ?? string.Empty);
                        command.Parameters.AddWithValue("@timestamp", message.Timestamp.ToString("o"));
                        command.Parameters.AddWithValue("@isUser", message.IsUser ? 1 : 0);

                        if (message.EmbeddingVector != null)
                        {
                            var embeddingBytes = SerializeVector(message.EmbeddingVector);
                            command.Parameters.AddWithValue("@embeddingVector", embeddingBytes);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@embeddingVector", DBNull.Value);
                        }

                        command.Parameters.AddWithValue("@id", message.Id);
                        command.Parameters.AddWithValue("@chatId", chatId);

                        await Task.Run(() => command.ExecuteNonQuery());
                        _logger.LogDebug("Updated message {MessageId} in chat {ChatId}", message.Id, chatId);

                        return message.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveChatMessageAsync error: {ErrorMessage}", ex.Message);
                throw; // Re-throw for higher level handling
            }
        }

        public async Task<List<Contact>> GetAllContactsAsync()
        {
            try
            {
                var contacts = new List<Contact>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT
                            Id,
                            Name,
                            AvatarUrl,
                            Status,
                            LastSeen,
                            Department
                        FROM Contact
                        ORDER BY Name";

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            contacts.Add(new Contact
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Status = Enum.TryParse<ContactStatus>(
                                    reader.IsDBNull(3) ? "Offline" : reader.GetString(3), out var status)
                                    ? status
                                    : ContactStatus.Offline,
                                LastSeen = reader.IsDBNull(4) ? DateTime.MinValue : DateTime.Parse(reader.GetString(4)),
                                Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                            });
                        }
                    }
                }

                _logger.LogDebug("GetAllContactsAsync: Found {ContactCount} contacts", contacts.Count);

                foreach (var contact in contacts)
                {
                    _logger.LogDebug("Contact: ID={ContactId}, Name={ContactName}, Dept={Department}",
                        contact.Id, contact.Name, contact.Department);
                }

                return contacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR in GetAllContactsAsync: {ErrorMessage}", ex.Message);

                // Create a fallback list of contacts for testing
                _logger.LogWarning("Returning fallback contact list due to error");
                return new List<Contact>
                {
                    new Contact
                    {
                        Id = 1, Name = "Maria Schmidt", Status = ContactStatus.Online, Department = "Sales",
                        AvatarUrl = "person_1.png"
                    },
                    new Contact
                    {
                        Id = 2, Name = "Thomas Müller", Status = ContactStatus.Away, Department = "Engineering",
                        AvatarUrl = "person_2.png"
                    }
                };
            }
        }

        public async Task<int> SaveContactAsync(Contact contact)
        {
            try
            {
                if (contact.Id == 0)
                {
                    _logger.LogDebug("Inserting new contact: {ContactName}", contact.Name);

                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO Contact (Name, AvatarUrl, Status, LastSeen, Department)
                            VALUES (@name, @avatarUrl, @status, @lastSeen, @department);
                            SELECT last_insert_rowid();";

                        command.Parameters.AddWithValue("@name", contact.Name);
                        command.Parameters.AddWithValue("@avatarUrl", contact.AvatarUrl ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@status", contact.Status.ToString());
                        command.Parameters.AddWithValue("@lastSeen", contact.LastSeen.ToString("o"));
                        command.Parameters.AddWithValue("@department", contact.Department ?? (object)DBNull.Value);

                        var id = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                        contact.Id = id;
                        return id;
                    }
                }
                else
                {
                    _logger.LogDebug("Updating contact {ContactId}: {ContactName}", contact.Id, contact.Name);

                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            UPDATE Contact
                            SET Name = @name,
                                AvatarUrl = @avatarUrl,
                                Status = @status,
                                LastSeen = @lastSeen,
                                Department = @department
                            WHERE Id = @id";

                        command.Parameters.AddWithValue("@id", contact.Id);
                        command.Parameters.AddWithValue("@name", contact.Name);
                        command.Parameters.AddWithValue("@avatarUrl", contact.AvatarUrl ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@status", contact.Status.ToString());
                        command.Parameters.AddWithValue("@lastSeen", contact.LastSeen.ToString("o"));
                        command.Parameters.AddWithValue("@department", contact.Department ?? (object)DBNull.Value);

                        await Task.Run(() => command.ExecuteNonQuery());
                        return contact.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving contact {ContactName}: {ErrorMessage}",
                    contact.Name, ex.Message);
                throw;
            }
        }

        public async Task<List<Chat>> FindSimilarChatsAsync(float[] embedding, int limit = 5)
        {
            List<Chat> results;

            try
            {
                _logger.LogDebug("FindSimilarChatsAsync: Looking for {Limit} similar chats", limit);

                // Check if vector search is available
                bool useVectorSearch = _vectorSearchAvailable;

                // Only try to use vector search if we know it's available
                if (useVectorSearch)
                {
                    try
                    {
                        var chatIds = new List<int>();
                        using (var command = _databaseConnection.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT chat_id, distance FROM chat_vectors WHERE vec0_search(embedding_vector, @embedding) LIMIT @limit";

                            // Convert embedding to blob parameter
                            var embeddingBytes = SerializeVector(embedding);
                            command.Parameters.AddWithValue("@embedding", embeddingBytes);
                            command.Parameters.AddWithValue("@limit", limit);

                            using (var reader = await Task.Run(() => command.ExecuteReader()))
                            {
                                while (await Task.Run(() => reader.Read()))
                                {
                                    chatIds.Add(reader.GetInt32(0));
                                }
                            }
                        }

                        if (chatIds.Count > 0)
                        {
                            _logger.LogDebug("Vector search found {ResultCount} chats", chatIds.Count);

                            results = new List<Chat>();

                            foreach (var id in chatIds)
                            {
                                var chat = await GetChatAsync(id);
                                if (chat != null)
                                {
                                    results.Add(chat);
                                }
                            }

                            return results;
                        }
                    }
                    catch (Microsoft.Data.Sqlite.SqliteException ex)
                    {
                        _logger.LogWarning(
                            "Vector search failed even though it was marked as available: {ErrorMessage}", ex.Message);
                        _vectorSearchAvailable = false; // Update flag since vector search isn't working

                        // Try to re-enable vector search
                        await _sqliteVectorSearchService.EnableVectorSearchAsync(_databaseConnection);

                        // Fall through to manual search
                    }
                }

                // Fallback: manually compute similarities
                _logger.LogDebug("Using manual similarity calculation");

                // Get all chats with their embeddings
                var allChats = new List<Chat>();
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Title, CreatedAt, ContactId, IsActive, EmbeddingVector FROM Chat";
                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            var chat = new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt =
                                    reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                ContactId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5))
                            };
                            allChats.Add(chat);
                        }
                    }
                } // Load related data for each chat

                foreach (var chat in allChats)
                {
                    if (chat.ContactId.HasValue)
                    {
                        chat.Contact = await GetContactAsync(chat.ContactId.Value);
                    }

                    chat.Messages = await GetChatMessagesAsync(chat.Id);
                }

                // Compute similarities and sort
                results = allChats
                    .Where(c => c.EmbeddingVector != null && c.EmbeddingVector.Length > 0)
                    .Select(c => new
                    {
                        Chat = c,
                        Similarity = _embeddingService.CalculateCosineSimilarity(embedding, c.EmbeddingVector)
                    })
                    .OrderByDescending(r => r.Similarity)
                    .Take(limit)
                    .Select(r => r.Chat)
                    .ToList();

                _logger.LogDebug("Manual search found {ResultCount} similar chats", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FindSimilarChatsAsync: {ErrorMessage}", ex.Message);
                return new List<Chat>();
            }
        }

        // Helper methods for serializing/deserializing vector data
        private byte[]? SerializeVector(float[]? vector)
        {
            if (vector == null) return null;

            byte[] bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private float[]? DeserializeVector(byte[]? bytes)
        {
            if (bytes == null) return null;

            float[] vector = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
            return vector;
        }

        public async Task<Contact?> GetContactAsync(int contactId)
        {
            try
            {
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Contact WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", contactId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        if (await Task.Run(() => reader.Read()))
                        {
                            return new Contact
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Status = Enum.TryParse<ContactStatus>(
                                    reader.IsDBNull(3) ? "Offline" : reader.GetString(3), out var status)
                                    ? status
                                    : ContactStatus.Offline,
                                LastSeen = reader.IsDBNull(4) ? DateTime.MinValue : DateTime.Parse(reader.GetString(4)),
                                Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                            };
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact {ContactId}: {ErrorMessage}", contactId, ex.Message);
                return null;
            }
        }

        #region Chat Segment Methods

        public async Task<int> SaveChatSegmentAsync(ChatSegment segment)
        {
            try
            {
                _logger.LogDebug("Saving chat segment for chat {ChatId}, date {SegmentDate}",
                    segment.ChatId, segment.SegmentDate.ToShortDateString());

                using (var command = _databaseConnection.CreateCommand())
                {
                    if (segment.Id == 0) // Insert new segment
                    {
                        command.CommandText = @"
                            INSERT INTO ChatSegment (ChatId, SegmentDate, Title, CombinedContent, EmbeddingVector, MessageCount, StartTime, EndTime, CreatedAt, Keywords)
                            VALUES (@chatId, @segmentDate, @title, @combinedContent, @embeddingVector, @messageCount, @startTime, @endTime, @createdAt, @keywords);
                            SELECT last_insert_rowid();";
                    }
                    else // Update existing segment
                    {
                        command.CommandText = @"
                            UPDATE ChatSegment
                            SET ChatId = @chatId, SegmentDate = @segmentDate, Title = @title,
                                CombinedContent = @combinedContent, EmbeddingVector = @embeddingVector,
                                MessageCount = @messageCount, StartTime = @startTime, EndTime = @endTime,
                                CreatedAt = @createdAt, Keywords = @keywords
                            WHERE Id = @id;
                            SELECT @id;";
                        command.Parameters.AddWithValue("@id", segment.Id);
                    }

                    command.Parameters.AddWithValue("@chatId", segment.ChatId);
                    command.Parameters.AddWithValue("@segmentDate", segment.SegmentDate.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@title", segment.Title ?? string.Empty);
                    command.Parameters.AddWithValue("@combinedContent", segment.CombinedContent ?? string.Empty);
                    command.Parameters.AddWithValue("@embeddingVector",
                        SerializeVector(segment.EmbeddingVector) ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@messageCount", segment.MessageCount);
                    command.Parameters.AddWithValue("@startTime", segment.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@endTime", segment.EndTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@createdAt", segment.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@keywords", segment.Keywords ?? string.Empty);

                    var segmentId = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                    segment.Id = segmentId;

                    _logger.LogDebug("Chat segment saved with ID {SegmentId}", segmentId);
                    return segmentId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat segment: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<List<ChatSegment>> GetSegmentsForChatAsync(int chatId)
        {
            try
            {
                var segments = new List<ChatSegment>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT Id, ChatId, SegmentDate, Title, CombinedContent, EmbeddingVector,
                               MessageCount, StartTime, EndTime, CreatedAt, Keywords
                        FROM ChatSegment
                        WHERE ChatId = @chatId
                        ORDER BY SegmentDate";
                    command.Parameters.AddWithValue("@chatId", chatId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            var segment = new ChatSegment
                            {
                                Id = reader.GetInt32(0),
                                ChatId = reader.GetInt32(1),
                                SegmentDate = DateTime.Parse(reader.GetString(2)),
                                Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                CombinedContent = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5)),
                                MessageCount = reader.GetInt32(6),
                                StartTime = DateTime.Parse(reader.GetString(7)),
                                EndTime = DateTime.Parse(reader.GetString(8)),
                                CreatedAt = DateTime.Parse(reader.GetString(9)),
                                Keywords = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                            };

                            segments.Add(segment);
                        }
                    }
                }

                _logger.LogDebug("Retrieved {SegmentCount} segments for chat {ChatId}", segments.Count, chatId);
                return segments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving segments for chat {ChatId}: {ErrorMessage}", chatId, ex.Message);
                return new List<ChatSegment>();
            }
        }

        public async Task<ChatSegment?> GetChatSegmentAsync(int segmentId)
        {
            try
            {
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT Id, ChatId, SegmentDate, Title, CombinedContent, EmbeddingVector,
                               MessageCount, StartTime, EndTime, CreatedAt, Keywords
                        FROM ChatSegment
                        WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", segmentId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        if (await Task.Run(() => reader.Read()))
                        {
                            return new ChatSegment
                            {
                                Id = reader.GetInt32(0),
                                ChatId = reader.GetInt32(1),
                                SegmentDate = DateTime.Parse(reader.GetString(2)),
                                Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                CombinedContent = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5)),
                                MessageCount = reader.GetInt32(6),
                                StartTime = DateTime.Parse(reader.GetString(7)),
                                EndTime = DateTime.Parse(reader.GetString(8)),
                                CreatedAt = DateTime.Parse(reader.GetString(9)),
                                Keywords = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                            };
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat segment {SegmentId}: {ErrorMessage}", segmentId,
                    ex.Message);
                return null;
            }
        }

        public async Task DeleteChatSegmentAsync(int segmentId)
        {
            try
            {
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ChatSegment WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", segmentId);

                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("Deleted chat segment {SegmentId}", segmentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting chat segment {SegmentId}: {ErrorMessage}", segmentId, ex.Message);
                throw;
            }
        }

        public async Task<List<ChatSegment>> GetAllChatSegmentsAsync()
        {
            try
            {
                var segments = new List<ChatSegment>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT Id, ChatId, SegmentDate, Title, CombinedContent, EmbeddingVector,
                               MessageCount, StartTime, EndTime, CreatedAt, Keywords
                        FROM ChatSegment
                        ORDER BY ChatId, SegmentDate";

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            var segment = new ChatSegment
                            {
                                Id = reader.GetInt32(0),
                                ChatId = reader.GetInt32(1),
                                SegmentDate = DateTime.Parse(reader.GetString(2)),
                                Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                CombinedContent = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                EmbeddingVector = reader.IsDBNull(5)
                                    ? null
                                    : DeserializeVector((byte[])reader.GetValue(5)),
                                MessageCount = reader.GetInt32(6),
                                StartTime = DateTime.Parse(reader.GetString(7)),
                                EndTime = DateTime.Parse(reader.GetString(8)),
                                CreatedAt = DateTime.Parse(reader.GetString(9)),
                                Keywords = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                            };

                            segments.Add(segment);
                        }
                    }
                }

                _logger.LogDebug("Retrieved {SegmentCount} total segments", segments.Count);
                return segments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all chat segments: {ErrorMessage}", ex.Message);
                return new List<ChatSegment>();
            }
        }

        public async Task UpdateChatSegmentAsync(ChatSegment segment)
        {
            await SaveChatSegmentAsync(segment); // SaveChatSegmentAsync handles both insert and update
        }

        #endregion Chat Segment Methods
    }
}