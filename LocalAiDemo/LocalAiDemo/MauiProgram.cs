using LocalAiDemo.Services;
using LocalAiDemo.Shared.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;

namespace LocalAiDemo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Add configuration file
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);
        
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });
            
        // Add device-specific services used by the LocalAiDemo.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        
        // Register our custom services        
        builder.Services.AddSingleton<IAiAssistantService, AiAssistantService>();        
        builder.Services.AddSingleton<IMeasurementService, MeasurementService>();
        builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

        // Configure SQLite for the application
        ConfigureSqlite(builder);        
        builder.Services.AddSingleton<IChatDatabaseService, ChatDatabaseService>();
        builder.Services.AddSingleton<IChatService, ChatService>();
        
        // Register platform-specific TTS services
        RegisterTtsServices(builder);

        // Configure logging
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureSqlite(MauiAppBuilder builder)
    {
        try
        {
            // Register a SQLite configuration service
            builder.Services.AddSingleton<SqliteVectorSearchService>();

            // Configure SQLite for vector search support
            var logger = new LoggerFactory().CreateLogger("SQLiteConfig");
            logger.LogInformation("Configuring SQLite with vector search support");

            // Enable loading extensions in SQLite
            SQLitePCL.Batteries_V2.Init();
        }
        catch (Exception ex)
        {
            var logger = new LoggerFactory().CreateLogger("SQLiteConfig");
            logger.LogError(ex, "Error configuring SQLite: {Message}", ex.Message);
        }
    }
      private static void RegisterTtsServices(MauiAppBuilder builder)
    {
        try
        {
            var logger = new LoggerFactory().CreateLogger("TtsConfig");
            
            // Get preferred provider from configuration
            var preferredProvider = builder.Configuration.GetValue<string>("AppSettings:TTS:PreferredProvider") ?? "System";
            logger.LogInformation("Using TTS provider from configuration: {Provider}", preferredProvider);
            
            // Register platform-specific implementation
#if WINDOWS
            // Use the platform-specific Windows implementation
            builder.Services.AddSingleton<IPlatformTts, Platforms.Windows.PlatformTtsService>();
            logger.LogInformation("Registered Windows Platform TTS provider");
#elif ANDROID
            // Use the platform-specific Android implementation
            builder.Services.AddSingleton<IPlatformTts, Platforms.Android.PlatformTtsService>();
            logger.LogInformation("Registered Android Platform TTS provider");
#elif IOS
            // Use the platform-specific iOS implementation
            builder.Services.AddSingleton<IPlatformTts, Platforms.iOS.PlatformTtsService>();
            logger.LogInformation("Registered iOS Platform TTS provider");
#elif MACCATALYST
            // Use the platform-specific MacCatalyst implementation
            builder.Services.AddSingleton<IPlatformTts, Platforms.MacCatalyst.PlatformTtsService>();
            logger.LogInformation("Registered MacCatalyst Platform TTS provider");
#else
            // Default fallback implementation
            builder.Services.AddSingleton<IPlatformTts, Platforms.Default.PlatformTtsService>();
            logger.LogInformation("Registered Default Platform TTS provider");
#endif

            // Register the correct TTS service based on configuration
            if (preferredProvider == "System")
            {
                // Register system TTS service as the primary service
                builder.Services.AddSingleton<ITtsService, SystemTtsService>();
                logger.LogInformation("Using System TTS provider as configured in appsettings.json");
            }
            else
            {
                // Register browser TTS service as the primary service
                builder.Services.AddSingleton<ITtsService, BrowserTtsService>();
                logger.LogInformation("Using Browser TTS provider as configured in appsettings.json");
            }
            
            logger.LogInformation("TTS services registered successfully");
        }
        catch (Exception ex)
        {
            var logger = new LoggerFactory().CreateLogger("TtsConfig");
            logger.LogError(ex, "Error registering TTS services: {Message}", ex.Message);
        }
    }
}
