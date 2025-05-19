using Microsoft.Extensions.Logging;
using SQLite;

namespace LocalAiDemo.Shared.Services
{
    public class SqliteVectorSearchService
    {
        private readonly ILogger<SqliteVectorSearchService> _logger;

        public SqliteVectorSearchService(ILogger<SqliteVectorSearchService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> EnableVectorSearchAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
        {
            try
            {
                _logger.LogInformation("Attempting to enable SQLite vector search...");                // Vector search extension filename varies by platform
#if WINDOWS
                string resourceFileName = "vec0.dll"; // File in resources
#elif ANDROID
                string resourceFileName = "vec0.so"; // File in resources
#elif IOS || MACCATALYST
                string resourceFileName = "vec0.dylib"; // File in resources
#else
                string resourceFileName = "vec0.dll"; // File in resources
#endif

                // First try to find the extension in the app's directory
                string extensionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLiteExtensions", resourceFileName);

                _logger.LogInformation("Looking for sqlite-vec extension at {Path}", extensionPath);

                // If not found in resources, prepare to extract it to app data directory
                if (!File.Exists(extensionPath))
                {
                    string appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SQLiteExtensions");

                    Directory.CreateDirectory(appDataPath);
                    string targetPath = Path.Combine(appDataPath, resourceFileName);
                    _logger.LogInformation("Extension not found in resources, will extract to: {Path}", targetPath);

                    // Extract from embedded resources
                    bool extracted = await ExtractResourceToFileAsync(resourceFileName, targetPath);
                    if (extracted)
                    {
                        extensionPath = targetPath;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract extension");
                        return false;
                    }
                }

                if (!File.Exists(extensionPath))
                {
                    _logger.LogWarning("Could not find or extract sqlite-vec extension at {Path}", extensionPath);
                    return false;
                }

                _logger.LogInformation("Found sqlite-vec extension at {Path}, attempting to load", extensionPath);

                // Create a temporary database to test loading the extension
                string tempDbPath = Path.Combine(Path.GetTempPath(), "vec_load_test.db");

                connection.Open();
                // Enable loading extensions
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA enable_load_extension = 1";
                    command.ExecuteNonQuery();
                }

                connection.LoadExtension(extensionPath);

                _logger.LogInformation("Successfully loaded sqlite-vec extension");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling vector search using native methods: {Message}", ex.Message);
                return false;
            }
        }

        private async Task<bool> ExtractResourceToFileAsync(string resourceFileName, string targetPath)
        {
            try
            {
                _logger.LogInformation("Extracting resource {ResourceName} to {TargetPath}", resourceFileName, targetPath);

                var assembly = typeof(SqliteVectorSearchService).Assembly;
                string[] resourceNames = assembly.GetManifestResourceNames();

                foreach (string resourceName in resourceNames)
                {
                    _logger.LogDebug("Found embedded resource: {ResourceName}", resourceName);

                    // Check if this resource matches our file
                    if (resourceName.EndsWith(resourceFileName))
                    {
                        _logger.LogInformation("Found matching resource: {ResourceName}", resourceName);

                        await using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            await using var fileStream = File.Create(targetPath);
                            await stream.CopyToAsync(fileStream);

                            _logger.LogInformation("Successfully extracted resource to file");
                            return true;
                        }
                    }
                }

                _logger.LogWarning("Could not find the SQLite extension file in any location");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting resource: {Message}", ex.Message);
                return false;
            }
        }
    }
}
