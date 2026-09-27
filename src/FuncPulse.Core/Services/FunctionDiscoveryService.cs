using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using FuncPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FuncPulse.Core.Services;

public class FunctionDiscoveryService : IFunctionDiscoveryService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<FunctionDiscoveryService> _logger;
    private readonly AzureSettings _settings;

    public FunctionDiscoveryService(
        IOptions<AzureSettings> settings,
        ILogger<FunctionDiscoveryService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        if (_settings.DemoMode)
        {
            _armClient = null!;
        }
        else
        {
            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);
        }
    }

    public async Task<List<FunctionAppInfo>> DiscoverFunctionAppsAsync(
        string subscriptionId, 
        string resourceGroup, 
        CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return GenerateDemoFunctionApps();
        }

        try
        {
            var functionApps = new List<FunctionAppInfo>();
            var subscription = _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            
            var resourceGroupResource = await subscription.GetResourceGroupAsync(resourceGroup, cancellationToken);
            var webApps = resourceGroupResource.Value.GetWebSites();

            await foreach (var webApp in webApps.GetAllAsync(cancellationToken: cancellationToken))
            {
                if (webApp.Data.Kind?.Contains("functionapp") == true)
                {
                    var appInfo = new FunctionAppInfo
                    {
                        ResourceId = webApp.Id.ToString(),
                        Name = webApp.Data.Name,
                        Location = webApp.Data.Location.ToString()
                    };

                    var appSettings = webApp.Data.SiteConfig?.AppSettings;
                    if (appSettings != null)
                    {
                        var connectionStringSetting = appSettings
                            .FirstOrDefault(s => s.Name == "APPLICATIONINSIGHTS_CONNECTION_STRING");
                        appInfo.AppInsightsConnectionString = connectionStringSetting?.Value;
                    }

                    try
                    {
                        var functionsCollection = webApp.GetSiteFunctions();
                        await foreach (var function in functionsCollection.GetAllAsync(cancellationToken: cancellationToken))
                        {
                            var triggerType = "Unknown";
                            if (function.Data.Config != null)
                            {
                                try
                                {
                                    var configJson = System.Text.Json.JsonDocument.Parse(function.Data.Config);
                                    if (configJson.RootElement.TryGetProperty("bindings", out var bindings))
                                    {
                                        if (bindings.ValueKind == System.Text.Json.JsonValueKind.Array && bindings.GetArrayLength() > 0)
                                        {
                                            var firstBinding = bindings[0];
                                            if (firstBinding.TryGetProperty("type", out var typeProperty))
                                            {
                                                triggerType = typeProperty.GetString() ?? "Unknown";
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore JSON parsing errors
                                }
                            }

                            appInfo.Functions.Add(new FunctionInfo
                            {
                                Name = function.Data.Name,
                                TriggerType = triggerType
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to list functions for {AppName}", appInfo.Name);
                    }

                    functionApps.Add(appInfo);
                }
            }

            return functionApps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover function apps in {ResourceGroup}", resourceGroup);
            throw;
        }
    }

    private List<FunctionAppInfo> GenerateDemoFunctionApps()
    {
        var random = new Random(42);
        return new List<FunctionAppInfo>
        {
            new()
            {
                ResourceId = "/subscriptions/demo-sub/resourceGroups/demo-rg/providers/Microsoft.Web/sites/order-processor",
                Name = "order-processor",
                Location = "East US",
                AppInsightsConnectionString = "InstrumentationKey=demo-key",
                Functions = new List<FunctionInfo>
                {
                    new() { Name = "ProcessOrder", TriggerType = "queueTrigger" },
                    new() { Name = "ValidatePayment", TriggerType = "httpTrigger" },
                    new() { Name = "SendNotification", TriggerType = "serviceBusTrigger" }
                }
            },
            new()
            {
                ResourceId = "/subscriptions/demo-sub/resourceGroups/demo-rg/providers/Microsoft.Web/sites/data-sync",
                Name = "data-sync",
                Location = "West US 2",
                AppInsightsConnectionString = "InstrumentationKey=demo-key",
                Functions = new List<FunctionInfo>
                {
                    new() { Name = "DailySyncJob", TriggerType = "timerTrigger" },
                    new() { Name = "SyncCustomers", TriggerType = "httpTrigger" }
                }
            },
            new()
            {
                ResourceId = "/subscriptions/demo-sub/resourceGroups/demo-rg/providers/Microsoft.Web/sites/reporting",
                Name = "reporting",
                Location = "Central US",
                Functions = new List<FunctionInfo>
                {
                    new() { Name = "GenerateReport", TriggerType = "timerTrigger" },
                    new() { Name = "EmailReport", TriggerType = "queueTrigger" },
                    new() { Name = "GetReportStatus", TriggerType = "httpTrigger" }
                }
            }
        };
    }
}
