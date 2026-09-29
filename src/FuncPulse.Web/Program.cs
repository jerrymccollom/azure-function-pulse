using FuncPulse.Web.Components;
using FuncPulse.Core.Models;
using FuncPulse.Core.Services;
using FuncPulse.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var timestamp = DateTime.Now.ToString("HH:mm:ss");
Console.WriteLine($"[{timestamp}] FuncPulse starting...");

var builder = WebApplication.CreateBuilder(args);

// Configure console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure Azure AD authentication if not in demo mode
var azureSettings = builder.Configuration.GetSection(AzureSettings.SectionName).Get<AzureSettings>();
var isAzureHosted = FuncPulse.Core.Services.AzureCredentialFactory.IsRunningInAzure();

if (!azureSettings?.DemoMode == true && isAzureHosted)
{
    var azureAdSection = builder.Configuration.GetSection("AzureAd");
    if (azureAdSection.Exists())
    {
        timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] Configuring Azure AD authentication for hosted environment...");
        
        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(options =>
            {
                builder.Configuration.Bind("AzureAd", options);
                options.SaveTokens = true;
            })
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews()
            .AddMicrosoftIdentityUI();
        
        Console.WriteLine($"[{timestamp}] Azure AD authentication configured");
    }
    else
    {
        Console.WriteLine($"[{timestamp}] WARNING: Running in Azure but no AzureAd configuration found. User authentication will not be available.");
    }
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AzureSettings>(
    builder.Configuration.GetSection(AzureSettings.SectionName));
builder.Services.Configure<HealthThresholds>(
    builder.Configuration.GetSection(HealthThresholds.SectionName));

// Log Azure configuration
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
        if (isAzureHosted)
        {
            Console.WriteLine($"[{timestamp}] Running in Azure - will use user-delegated credentials when authenticated");
        }
        else
        {
            Console.WriteLine($"[{timestamp}] Running locally - will use Azure CLI credentials");
        }
    }
    else
    {
        Console.WriteLine($"[{timestamp}] Demo mode enabled - using sample data (no Azure authentication)");
    }
}

// Register authentication services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserCredentialProvider, UserCredentialProvider>();
builder.Services.AddScoped<IAzureCredentialService, AzureCredentialService>();

// Register wrapper services that use user-aware credentials
builder.Services.AddScoped<IFunctionDiscoveryService, WebFunctionDiscoveryService>();
builder.Services.AddScoped<IMetricsService, WebMetricsService>();
builder.Services.AddScoped<ILogsService, WebLogsService>();
builder.Services.AddScoped<IFunctionManagementService, WebFunctionManagementService>();
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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map authentication controllers (for login/logout)
if (!azureSettings?.DemoMode == true && isAzureHosted)
{
    app.MapControllers();
}

timestamp = DateTime.Now.ToString("HH:mm:ss");
Console.WriteLine($"[{timestamp}] Starting Kestrel HTTP listener...");
Console.WriteLine($"[{timestamp}] Application URLs will be displayed below:");

app.Run();
