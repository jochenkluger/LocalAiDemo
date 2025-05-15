using LocalAiDemo.Services;
using LocalAiDemo.Shared.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;

namespace LocalAiDemo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
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
}