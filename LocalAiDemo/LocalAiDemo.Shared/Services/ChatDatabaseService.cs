using LocalAiDemo.Shared.Models;
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

        Task<List<Chat>> GetChatsByPersonAsync(int personId);

        Task<int> SaveChatAsync(Chat chat);

        Task<List<Person>> GetAllPersonsAsync();

        Task<int> SavePersonAsync(Person person);

        Task<List<Chat>> FindSimilarChatsAsync(float[] embedding, int limit = 5);
    }
    public class ChatDatabaseService : IChatDatabaseService
    {
        private readonly SqliteConnection _databaseConnection;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ChatDatabaseService> _logger;
        private readonly SqliteVectorSearchService _sqliteVectorSearchService;
        private bool _vectorSearchAvailable = false; // Track if vector search is available

        public ChatDatabaseService(IEmbeddingService embeddingService, ILogger<ChatDatabaseService> logger, SqliteVectorSearchService sqliteVectorSearchService)
        {
            _embeddingService = embeddingService;
            _logger = logger;
            _sqliteVectorSearchService = sqliteVectorSearchService;

            // Set database path in the app's local data folder
            string dbName = "chatdatabase.db3";

            // For MAUI apps, use the app's data directory
#if WINDOWS
            var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dbName);
#else
            var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dbName);
#endif

            _logger.LogInformation("Database path: {DbPath}", databasePath);
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);

                // Create the database connection
                _databaseConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
                _databaseConnection.Open();
                _logger.LogInformation("Database connection created successfully");

                // Initialize vector search capabilities
                // Try to enable vector search if not already enabled
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
                        CREATE TABLE IF NOT EXISTS Person (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            AvatarUrl TEXT,
                            Status TEXT,
                            LastSeen TEXT,
                            Department TEXT
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("Person table created");
                }

                // For the Chat table, we need to serialize the embedding vector
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Chat (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Title TEXT,
                            CreatedAt TEXT,
                            PersonId INTEGER,
                            IsActive INTEGER,
                            EmbeddingVector BLOB,
                            FOREIGN KEY(PersonId) REFERENCES Person(Id)
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("Chat table created");
                }

                // For the ChatMessage table
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
                            FOREIGN KEY(ChatId) REFERENCES Chat(Id)
                        )";
                    await Task.Run(() => command.ExecuteNonQuery());
                    _logger.LogDebug("ChatMessage table created");
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
                    _logger.LogDebug("Vector table creation failed (expected if not supported): {ErrorMessage}", ex.Message);
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
                // Check if we have any persons
                List<Person> persons = new List<Person>();
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM Person";
                    var personCount = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                    _logger.LogDebug("Database check: Found {PersonCount} existing persons", personCount);

                    // If we have persons, load them to persons list
                    if (personCount > 0)
                    {
                        command.CommandText = "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Person";
                        using (var reader = await Task.Run(() => command.ExecuteReader()))
                        {
                            while (await Task.Run(() => reader.Read()))
                            {
                                persons.Add(new Person
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    LastSeen = reader.IsDBNull(4) ? DateTime.MinValue : DateTime.Parse(reader.GetString(4)),
                                    Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                                });
                            }
                        }
                    }
                }

                // Force seeding for testing purposes - remove this in production
                if (persons.Count < 5) // Ensure we have at least 5 persons
                {
                    _logger.LogInformation("Seeding database with sample person data");

                    // Clear existing persons first to avoid duplicates (only for testing)
                    if (persons.Count > 0)
                    {
                        using (var command = _databaseConnection.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM Person";
                            await Task.Run(() => command.ExecuteNonQuery());
                            _logger.LogDebug("Cleared existing person data");
                        }
                    }

                    // Add sample persons
                    var samplePersons = new List<Person>
                    {
                        new Person { Id = 1, Name = "Maria Schmidt", Status = "Online", Department = "Sales", LastSeen = DateTime.Now, AvatarUrl = "person_1.png" },
                        new Person { Id = 2, Name = "Thomas Müller", Status = "Away", Department = "Engineering", LastSeen = DateTime.Now.AddHours(-1), AvatarUrl = "person_2.png" },
                        new Person { Id = 3, Name = "Julia Weber", Status = "Offline", Department = "Marketing", LastSeen = DateTime.Now.AddDays(-1), AvatarUrl = "person_3.png" },
                        new Person { Id = 4, Name = "Michael Wagner", Status = "Online", Department = "Support", LastSeen = DateTime.Now, AvatarUrl = "person_4.png" },
                        new Person { Id = 5, Name = "Anna Fischer", Status = "Do Not Disturb", Department = "Management", LastSeen = DateTime.Now.AddMinutes(-30), AvatarUrl = "person_5.png" }
                    };

                    foreach (var person in samplePersons)
                    {
                        try
                        {
                            using (var command = _databaseConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                    INSERT INTO Person (Id, Name, AvatarUrl, Status, LastSeen, Department)
                                    VALUES ($id, $name, $avatar, $status, $lastSeen, $department)";

                                command.Parameters.AddWithValue("$id", person.Id);
                                command.Parameters.AddWithValue("$name", person.Name);
                                command.Parameters.AddWithValue("$avatar", person.AvatarUrl ?? (object)DBNull.Value);
                                command.Parameters.AddWithValue("$status", person.Status ?? (object)DBNull.Value);
                                command.Parameters.AddWithValue("$lastSeen", person.LastSeen.ToString("o"));
                                command.Parameters.AddWithValue("$department", person.Department ?? (object)DBNull.Value);

                                int count = await Task.Run(() => command.ExecuteNonQuery());
                                _logger.LogDebug("Added {Count} person: {PersonName}", count, person.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to add person {PersonName}: {ErrorMessage}", person.Name, ex.Message);
                        }
                    }

                    // Verify the persons were added
                    persons = new List<Person>();
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Person";
                        using (var reader = await Task.Run(() => command.ExecuteReader()))
                        {
                            while (await Task.Run(() => reader.Read()))
                            {
                                persons.Add(new Person
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    LastSeen = reader.IsDBNull(4) ? DateTime.MinValue : DateTime.Parse(reader.GetString(4)),
                                    Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                                });
                            }
                        }
                    }

                    _logger.LogDebug("After seeding: Found {PersonCount} persons in database", persons.Count);

                    // List all persons for debugging
                    foreach (var person in persons)
                    {
                        _logger.LogDebug("Person in DB: ID={PersonId}, Name={PersonName}, Dept={Department}",
                            person.Id, person.Name, person.Department);
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
                    command.CommandText = "SELECT Id, Title, CreatedAt, PersonId, IsActive, EmbeddingVector FROM Chat ORDER BY CreatedAt DESC";

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            chats.Add(new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt = reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                PersonId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5) ? null : DeserializeVector((byte[])reader.GetValue(5))
                            });
                        }
                    }
                }

                _logger.LogDebug("GetAllChatsAsync: Retrieved {ChatCount} chats", chats.Count);

                // Load persons for each chat
                foreach (var chat in chats)
                {
                    chat.Person = await GetPersonAsync(chat.PersonId);
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
                    command.CommandText = "SELECT Id, Title, CreatedAt, PersonId, IsActive, EmbeddingVector FROM Chat WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", chatId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        if (await Task.Run(() => reader.Read()))
                        {
                            chat = new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt = reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                PersonId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5) ? null : DeserializeVector((byte[])reader.GetValue(5))
                            };
                        }
                    }
                }

                if (chat != null)
                {
                    chat.Person = await GetPersonAsync(chat.PersonId);
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

        public async Task<List<Chat>> GetChatsByPersonAsync(int personId)
        {
            try
            {
                var chats = new List<Chat>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Title, CreatedAt, PersonId, IsActive, EmbeddingVector FROM Chat WHERE PersonId = @personId ORDER BY CreatedAt DESC";
                    command.Parameters.AddWithValue("@personId", personId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            chats.Add(new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt = reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                PersonId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5) ? null : DeserializeVector((byte[])reader.GetValue(5))
                            });
                        }
                    }
                }

                _logger.LogDebug("Found {ChatCount} chats for person {PersonId}", chats.Count, personId);

                foreach (var chat in chats)
                {
                    chat.Person = await GetPersonAsync(chat.PersonId);
                    chat.Messages = await GetChatMessagesAsync(chat.Id);
                }
                return chats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chats for person {PersonId}: {ErrorMessage}",
                    personId, ex.Message);
                return new List<Chat>();
            }
        }

        private async Task<List<ChatMessage>> GetChatMessagesAsync(int chatId)
        {
            try
            {
                var messages = new List<ChatMessage>();

                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, ChatId, Content, Timestamp, IsUser, EmbeddingVector FROM ChatMessage WHERE ChatId = @chatId ORDER BY Timestamp";
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
                                Timestamp = reader.IsDBNull(3) ? DateTime.MinValue : DateTime.Parse(reader.GetString(3)),
                                IsUser = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5) ? null : DeserializeVector((byte[])reader.GetValue(5))
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

                    _logger.LogDebug("Inserting new chat: Title='{Title}', PersonId={PersonId}",
                        chat.Title, chat.PersonId);

                    // Add the chat to the database
                    int chatId;
                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO Chat (Title, CreatedAt, PersonId, IsActive, EmbeddingVector)
                            VALUES (@title, @createdAt, @personId, @isActive, @embeddingVector);
                            SELECT last_insert_rowid();";

                        command.Parameters.AddWithValue("@title", chat.Title ?? string.Empty);
                        command.Parameters.AddWithValue("@createdAt", chat.CreatedAt.ToString("o"));
                        command.Parameters.AddWithValue("@personId", chat.PersonId);
                        command.Parameters.AddWithValue("@isActive", chat.IsActive ? 1 : 0);

                        var embeddingBytes = chat.EmbeddingVector != null ? SerializeVector(chat.EmbeddingVector) : null;
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
                                command.CommandText = "INSERT INTO chat_vectors(embedding_vector, chat_id) VALUES (@embeddingVector, @chatId)";

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
                    throw;  // Re-throw to ensure the error is propagated
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
                                PersonId = @personId, 
                                IsActive = @isActive, 
                                EmbeddingVector = @embeddingVector
                            WHERE Id = @id";

                        command.Parameters.AddWithValue("@id", chat.Id);
                        command.Parameters.AddWithValue("@title", chat.Title ?? string.Empty);
                        command.Parameters.AddWithValue("@createdAt", chat.CreatedAt.ToString("o"));
                        command.Parameters.AddWithValue("@personId", chat.PersonId);
                        command.Parameters.AddWithValue("@isActive", chat.IsActive ? 1 : 0);

                        var embeddingBytes = chat.EmbeddingVector != null ? SerializeVector(chat.EmbeddingVector) : null;
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

        public async Task<List<Person>> GetAllPersonsAsync()
        {
            try
            {
                var persons = new List<Person>();

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
                        FROM Person
                        ORDER BY Name";

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            persons.Add(new Person
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                LastSeen = reader.IsDBNull(4) ? DateTime.MinValue : DateTime.Parse(reader.GetString(4)),
                                Department = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                            });
                        }
                    }
                }

                _logger.LogDebug("GetAllPersonsAsync: Found {PersonCount} persons", persons.Count);

                foreach (var person in persons)
                {
                    _logger.LogDebug("Person: ID={PersonId}, Name={PersonName}, Dept={Department}",
                        person.Id, person.Name, person.Department);
                }
                return persons;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR in GetAllPersonsAsync: {ErrorMessage}", ex.Message);

                // Create a fallback list of persons for testing
                _logger.LogWarning("Returning fallback person list due to error");
                return new List<Person>
                {
                    new Person { Id = 1, Name = "Maria Schmidt", Status = "Online", Department = "Sales", AvatarUrl = "person_1.png" },
                    new Person { Id = 2, Name = "Thomas Müller", Status = "Away", Department = "Engineering", AvatarUrl = "person_2.png" }
                };
            }
        }

        public async Task<int> SavePersonAsync(Person person)
        {
            try
            {
                if (person.Id == 0)
                {
                    _logger.LogDebug("Inserting new person: {PersonName}", person.Name);

                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO Person (Name, AvatarUrl, Status, LastSeen, Department)
                            VALUES (@name, @avatarUrl, @status, @lastSeen, @department);
                            SELECT last_insert_rowid();";

                        command.Parameters.AddWithValue("@name", person.Name);
                        command.Parameters.AddWithValue("@avatarUrl", person.AvatarUrl ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@status", person.Status ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@lastSeen", person.LastSeen.ToString("o"));
                        command.Parameters.AddWithValue("@department", person.Department ?? (object)DBNull.Value);

                        var id = Convert.ToInt32(await Task.Run(() => command.ExecuteScalar()));
                        person.Id = id;
                        return id;
                    }
                }
                else
                {
                    _logger.LogDebug("Updating person {PersonId}: {PersonName}", person.Id, person.Name);

                    using (var command = _databaseConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            UPDATE Person 
                            SET Name = @name, 
                                AvatarUrl = @avatarUrl, 
                                Status = @status, 
                                LastSeen = @lastSeen, 
                                Department = @department
                            WHERE Id = @id";

                        command.Parameters.AddWithValue("@id", person.Id);
                        command.Parameters.AddWithValue("@name", person.Name);
                        command.Parameters.AddWithValue("@avatarUrl", person.AvatarUrl ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@status", person.Status ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@lastSeen", person.LastSeen.ToString("o"));
                        command.Parameters.AddWithValue("@department", person.Department ?? (object)DBNull.Value);

                        await Task.Run(() => command.ExecuteNonQuery());
                        return person.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving person {PersonName}: {ErrorMessage}",
                    person.Name, ex.Message);
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
                            command.CommandText = "SELECT chat_id, distance FROM chat_vectors WHERE vec0_search(embedding_vector, @embedding) LIMIT @limit";

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
                        _logger.LogWarning("Vector search failed even though it was marked as available: {ErrorMessage}", ex.Message);
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
                    command.CommandText = "SELECT Id, Title, CreatedAt, PersonId, IsActive, EmbeddingVector FROM Chat";
                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        while (await Task.Run(() => reader.Read()))
                        {
                            var chat = new Chat
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CreatedAt = reader.IsDBNull(2) ? DateTime.MinValue : DateTime.Parse(reader.GetString(2)),
                                PersonId = reader.GetInt32(3),
                                IsActive = reader.GetInt32(4) == 1,
                                EmbeddingVector = reader.IsDBNull(5) ? null : DeserializeVector((byte[])reader.GetValue(5))
                            };
                            allChats.Add(chat);
                        }
                    }
                }

                // Load related data for each chat
                foreach (var chat in allChats)
                {
                    chat.Person = await GetPersonAsync(chat.PersonId);
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

        private async Task<Person?> GetPersonAsync(int personId)
        {
            try
            {
                using (var command = _databaseConnection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name, AvatarUrl, Status, LastSeen, Department FROM Person WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", personId);

                    using (var reader = await Task.Run(() => command.ExecuteReader()))
                    {
                        if (await Task.Run(() => reader.Read()))
                        {
                            return new Person
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
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
                _logger.LogError(ex, "Error retrieving person {PersonId}: {ErrorMessage}", personId, ex.Message);
                return null;
            }
        }
    }
}