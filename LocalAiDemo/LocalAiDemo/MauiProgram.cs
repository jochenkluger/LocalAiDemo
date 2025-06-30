using LocalAiDemo.Services;
using LocalAiDemo.Shared.Models;
using LocalAiDemo.Shared.Services;
using LocalAiDemo.Shared.Services.Chat;
using LocalAiDemo.Shared.Services.Generation;
using LocalAiDemo.Shared.Services.Search;
using LocalAiDemo.Shared.Services.Stt;
using LocalAiDemo.Shared.Services.Tts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalAiDemo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Add configuration file
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);

        // Configure and register AppConfiguration
        builder.Services.Configure<AppConfiguration>(builder.Configuration.GetSection("AppConfiguration"));

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        // Add device-specific services used by the LocalAiDemo.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>(); // Register our custom services
        RegisterTextGenerationService(builder);
        builder.Services.AddSingleton<IAiAssistantService, AiAssistantService>();
        builder.Services.AddSingleton<IPerformanceService, PerformanceService>();
        builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

        // Configure SQLite for the application
        ConfigureSqlite(builder);
        builder.Services.AddSingleton<IChatDatabaseService, ChatDatabaseService>();
        builder.Services.AddSingleton<IChatService, ChatService>(); // Register vector services
        builder.Services.AddSingleton<IChatVectorizationService, ChatVectorizationService>();
        builder.Services.AddSingleton<IAdvancedVectorService, AdvancedVectorService>();
        builder.Services.AddSingleton<IChatVectorService, ChatVectorService>();
        builder.Services
            .AddSingleton<IChatSegmentService, ChatSegmentService>(); // Register platform-specific TTS services
        RegisterTtsServices(builder);

        // Register Speech-to-Text services
        RegisterSttServices(builder); // Configure logging        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif // Add Performance Logger Provider to capture all log messages
        var app = builder.Build();
        var performanceService = app.Services.GetRequiredService<IPerformanceService>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        loggerFactory.AddProvider(new LocalAiDemo.Services.PerformanceLoggerProvider(performanceService));

        return app;
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

    private static void RegisterTextGenerationService(MauiAppBuilder builder)
    {
        try
        {
            var logger = new LoggerFactory().CreateLogger("TextGenerationConfig");

            // Function Calling Services registrieren
            builder.Services.AddSingleton<IContactService, ContactService>();
            builder.Services.AddSingleton<LocalAiDemo.Shared.Services.FunctionCalling.IMessageCreator,
                LocalAiDemo.Shared.Services.FunctionCalling.MessageCreator>();

            // Message Injection Service registrieren
            builder.Services.AddSingleton<IMessageInjectionService, MessageInjectionService>();

            logger.LogInformation(
                "Function Calling Services registriert"); // Prüfen der Konfiguration für Text Generation
            var serviceProvider = builder.Services.BuildServiceProvider();
            var config = serviceProvider.GetService<IOptions<AppConfiguration>>()?.Value;
            var useSemanticKernel = config?.UseSemanticKernel ?? false;

            logger.LogInformation("UseSemanticKernel setting: {UseSemanticKernel}", useSemanticKernel);

            if (useSemanticKernel)
            {
                // Registrieren des Semantic Kernel Text Generation Service
                builder.Services.AddSingleton<ITextGenerationService, SemanticKernelTextGenerationService>();
                logger.LogInformation("SemanticKernelTextGenerationService registriert");
            }
            else
            {
                // Standard: Verwenden des Local Text Generation Service (Microsoft.Extensions.AI)
                builder.Services.AddSingleton<ITextGenerationService, LocalTextGenerationService>();
                logger.LogInformation("Registriert LocalTextGenerationService (Standard)");
            }
        }
        catch (Exception ex)
        {
            var logger = new LoggerFactory().CreateLogger("TextGenerationConfig");
            logger.LogError(ex, "Fehler beim Registrieren des Text Generation Service: {Message}", ex.Message);

            // Fallback auf Standard-Service
            builder.Services.AddSingleton<ITextGenerationService, LocalTextGenerationService>();
        }
    }

    private static void RegisterTtsServices(MauiAppBuilder builder)
    {
        try
        {
            var logger = new LoggerFactory().CreateLogger("TtsConfig");

            // Get preferred provider from configuration
            var config = builder.Services.BuildServiceProvider().GetService<IOptions<AppConfiguration>>()?.Value;
            var preferredProvider = config?.TtsProvider ?? "System";
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
#endif // Register local TTS services
            builder.Services.AddSingleton<ESpeakTtsService>();
            builder.Services.AddSingleton<PiperTtsService>();
            builder.Services.AddSingleton<OnnxTtsService>();
            builder.Services.AddSingleton<ThorstenOnnxTtsService>();
            logger.LogInformation("Registered local TTS services: eSpeak, Piper, ONNX, Thorsten-ONNX");

            // Register the correct TTS service based on configuration
            if (preferredProvider == "System")
            {
                // Register system TTS service as the primary service
                builder.Services.AddSingleton<ITtsService, SystemTtsService>();
                logger.LogInformation("Using System TTS provider as configured in appsettings.json");
            }
            else if (preferredProvider == "eSpeak")
            {
                // Register eSpeak TTS service as the primary service
                builder.Services.AddSingleton<ITtsService>(provider =>
                    provider.GetRequiredService<ESpeakTtsService>());
                logger.LogInformation("Using eSpeak TTS provider as configured in appsettings.json");
            }
            else if (preferredProvider == "Piper")
            {
                // Register Piper TTS service as the primary service
                builder.Services.AddSingleton<ITtsService>(provider =>
                    provider.GetRequiredService<PiperTtsService>());
                logger.LogInformation("Using Piper TTS provider as configured in appsettings.json");
            }
            else if (preferredProvider == "ONNX")
            {
                // Register ONNX TTS service as the primary service
                builder.Services.AddSingleton<ITtsService>(provider =>
                    provider.GetRequiredService<OnnxTtsService>());
                logger.LogInformation("Using ONNX TTS provider as configured in appsettings.json");
            }
            else if (preferredProvider == "ThorstenOnnx")
            {
                // Register Thorsten ONNX TTS service as the primary service
                builder.Services.AddSingleton<ITtsService>(provider =>
                    provider.GetRequiredService<ThorstenOnnxTtsService>());
                logger.LogInformation("Using Thorsten ONNX TTS provider as configured in appsettings.json");
            }
            else
            {
                // Register browser TTS service as the primary service
                builder.Services.AddSingleton<ITtsService, BrowserTtsService>();
                logger.LogInformation("Using Browser TTS provider as configured in appsettings.json");
            }

            // Always register BrowserTtsService as a separate service for direct injection
            builder.Services.AddSingleton<BrowserTtsService>();

            logger.LogInformation("TTS services registered successfully");
        }
        catch (Exception ex)
        {
            var logger = new LoggerFactory().CreateLogger("TtsConfig");
            logger.LogError(ex, "Error registering TTS services: {Message}", ex.Message);
        }
    }

    private static void RegisterSttServices(MauiAppBuilder builder)
    {
        try
        {
            var logger = new LoggerFactory().CreateLogger("SstConfig");

            // Get preferred provider from configuration
            var config = builder.Services.BuildServiceProvider().GetService<IOptions<AppConfiguration>>()?.Value;
            var preferredProvider = config?.SttProvider ?? "Browser";
            logger.LogInformation("Using STT provider from configuration: {Provider}", preferredProvider);

            // Always register WhisperService as it's needed by WhisperSstService
            builder.Services.AddSingleton<WhisperService>();

            // Register the correct STT service based on configuration
            if (preferredProvider == "Whisper")
            {
                // Register Whisper STT service as the primary service
                builder.Services.AddSingleton<LocalAiDemo.Shared.Services.Stt.WhisperSttService>();
                builder.Services
                    .AddSingleton<LocalAiDemo.Shared.Services.Stt.ISttService,
                        LocalAiDemo.Shared.Services.Stt.WhisperSttService>();
                logger.LogInformation("Using Whisper SST provider as configured in appsettings.json");
            }
            else
            {
                // Register browser STT service as the primary service (default)
                builder.Services.AddSingleton<LocalAiDemo.Shared.Services.Stt.BrowserSttService>();
                builder.Services
                    .AddSingleton<LocalAiDemo.Shared.Services.Stt.ISttService,
                        LocalAiDemo.Shared.Services.Stt.BrowserSttService>();
                logger.LogInformation("Using Browser SST provider as configured in appsettings.json");
            }

            // Always register BrowserSttService as a separate service for direct injection if needed
            builder.Services.AddSingleton<LocalAiDemo.Shared.Services.Stt.BrowserSttService>();

            logger.LogInformation("STT services registered successfully");
        }
        catch (Exception ex)
        {
            var logger = new LoggerFactory().CreateLogger("SttConfig");
            logger.LogError(ex, "Error registering STT services: {Message}", ex.Message);

            // Fallback to Browser STT service
            builder.Services.AddSingleton<LocalAiDemo.Shared.Services.Stt.BrowserSttService>();
            builder.Services
                .AddSingleton<LocalAiDemo.Shared.Services.Stt.ISttService,
                    LocalAiDemo.Shared.Services.Stt.BrowserSttService>();
        }
    }
}