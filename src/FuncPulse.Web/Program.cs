using FuncPulse.Web.Components;
using FuncPulse.Core.Models;
using FuncPulse.Core.Services;
using FuncPulse.Web.Services;

var timestamp = DateTime.Now.ToString("HH:mm:ss");
Console.WriteLine($"[{timestamp}] FuncPulse starting...");

var builder = WebApplication.CreateBuilder(args);

// Configure console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AzureSettings>(
    builder.Configuration.GetSection(AzureSettings.SectionName));
builder.Services.Configure<HealthThresholds>(
    builder.Configuration.GetSection(HealthThresholds.SectionName));

// Log Azure configuration
var azureSettings = builder.Configuration.GetSection(AzureSettings.SectionName).Get<AzureSettings>();
if (azureSettings != null)
{
    timestamp = DateTime.Now.ToString("HH:mm:ss");
    Console.WriteLine($"[{timestamp}] Configuration loaded:");
    Console.WriteLine($"  - DemoMode: {azureSettings.DemoMode}");
    Console.WriteLine($"  - SubscriptionId: {(string.IsNullOrEmpty(azureSettings.SubscriptionId) ? "<not set>" : $"{azureSettings.SubscriptionId[..Math.Min(8, azureSettings.SubscriptionId.Length)]}...")}");
    Console.WriteLine($"  - ResourceGroup: {(string.IsNullOrEmpty(azureSettings.ResourceGroup) ? "<not set>" : azureSettings.ResourceGroup)}");
    Console.WriteLine($"  - TenantId: {(string.IsNullOrEmpty(azureSettings.TenantId) ? "<not set>" : $"{azureSettings.TenantId[..Math.Min(8, azureSettings.TenantId.Length)]}...")}");
    
    if (!azureSettings.DemoMode)
    {
        Console.WriteLine($"[{timestamp}] Azure production mode enabled - verbose logging active");
        Console.WriteLine($"[{timestamp}] Will use DefaultAzureCredential (tries: Environment → Managed Identity → Azure CLI → PowerShell → Visual Studio)");
    }
    else
    {
        Console.WriteLine($"[{timestamp}] Demo mode enabled - using sample data (no Azure authentication)");
    }
}

builder.Services.AddSingleton<IFunctionDiscoveryService, FunctionDiscoveryService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<ILogsService, LogsService>();
builder.Services.AddScoped<DashboardState>();

timestamp = DateTime.Now.ToString("HH:mm:ss");
Console.WriteLine($"[{timestamp}] Building application...");

var app = builder.Build();

timestamp = DateTime.Now.ToString("HH:mm:ss");
Console.WriteLine($"[{timestamp}] Configuring HTTP pipeline...");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

timestamp = DateTime.Now.ToString("HH:mm:ss");
Console.WriteLine($"[{timestamp}] Starting Kestrel HTTP listener...");
Console.WriteLine($"[{timestamp}] Application URLs will be displayed below:");

app.Run();
