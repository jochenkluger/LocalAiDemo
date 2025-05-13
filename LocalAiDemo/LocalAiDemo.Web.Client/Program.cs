using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LocalAiDemo.Shared.Services;
using LocalAiDemo.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the LocalAiDemo.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

await builder.Build().RunAsync();
