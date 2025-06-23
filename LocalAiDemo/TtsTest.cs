using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LocalAiDemo.Shared.Services.Tts;

// Einfaches Test-Programm für Thorsten ONNX TTS
var services = new ServiceCollection();

// Logging konfigurieren
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// TTS Service registrieren
services.AddSingleton<ThorstenOnnxTtsService>();

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
var ttsService = serviceProvider.GetRequiredService<ThorstenOnnxTtsService>();

logger.LogInformation("=== Thorsten ONNX TTS Test ===");

try
{
    // Teste Download und TTS
    var result = await ttsService.ManualTestAsync();
    
    if (result)
    {
        logger.LogInformation("✅ Thorsten ONNX TTS Test erfolgreich!");
    }
    else
    {
        logger.LogError("❌ Thorsten ONNX TTS Test fehlgeschlagen!");
    }
    
    // Zeige Modell-Informationen
    var modelInfo = await ttsService.GetModelInfoAsync();
    logger.LogInformation("📊 Modell-Informationen:");
    logger.LogInformation("   - Modelle verfügbar: {Available}", modelInfo.ModelsAvailable);
    logger.LogInformation("   - Modell-Pfad: {Path}", modelInfo.ModelsPath);
    logger.LogInformation("   - Gesamtgröße: {Size:F2} MB", modelInfo.TotalSizeMB);
    logger.LogInformation("   - Verfügbare Dateien: {Files}", string.Join(", ", modelInfo.AvailableModelFiles));
}
catch (Exception ex)
{
    logger.LogError(ex, "Fehler beim Testen des TTS-Service");
}

logger.LogInformation("Test beendet. Drücken Sie eine Taste zum Beenden...");
Console.ReadKey();
