using LocalAiDemo.Shared.Services;
using LocalAiDemo.Web.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the LocalAiDemo.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Note: TTS functionality has been simplified and moved directly to the components
// that need it, using JSInterop directly instead of service registration

await builder.Build().RunAsync();