using LocalAiDemo.Shared.Services;
using LocalAiDemo.Web.Components;
using LocalAiDemo.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Add device-specific services used by the LocalAiDemo.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<IPerformanceService, PerformanceService>();

var app = builder.Build();

// Add Performance Logger Provider to capture all log messages
var performanceService = app.Services.GetRequiredService<IPerformanceService>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
loggerFactory.AddProvider(new LocalAiDemo.Web.Services.PerformanceLoggerProvider(performanceService));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(LocalAiDemo.Shared._Imports).Assembly,
        typeof(LocalAiDemo.Web.Client._Imports).Assembly);

app.Run();