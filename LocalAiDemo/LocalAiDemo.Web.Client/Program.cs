using LocalAiDemo.Shared.Services;
using LocalAiDemo.Web.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the LocalAiDemo.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<IPerformanceService, PerformanceService>();

// Note: TTS functionality has been simplified and moved directly to the components that need it,
// using JSInterop directly instead of service registration

var app = builder.Build();

// Add Performance Logger Provider to capture all log messages
var performanceService = app.Services.GetRequiredService<IPerformanceService>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
loggerFactory.AddProvider(new LocalAiDemo.Web.Client.Services.PerformanceLoggerProvider(performanceService));

await app.RunAsync();