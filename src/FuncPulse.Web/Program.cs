using FuncPulse.Web.Components;
using FuncPulse.Core.Models;
using FuncPulse.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AzureSettings>(
    builder.Configuration.GetSection(AzureSettings.SectionName));
builder.Services.Configure<HealthThresholds>(
    builder.Configuration.GetSection(HealthThresholds.SectionName));

builder.Services.AddSingleton<IFunctionDiscoveryService, FunctionDiscoveryService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<ILogsService, LogsService>();

var app = builder.Build();

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

app.Run();
