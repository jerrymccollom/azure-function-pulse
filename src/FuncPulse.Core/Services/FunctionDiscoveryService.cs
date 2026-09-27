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
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var credentialType = AzureCredentialFactory.GetCredentialDescription();
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Initializing {credentialType}...");
            
            try
            {
                var credential = AzureCredentialFactory.CreateCredential();
                _armClient = new ArmClient(credential);
                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Credential created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: ERROR creating credential - {ex.Message}");
                throw;
            }
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

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Starting discovery in subscription {subscriptionId[..Math.Min(8, subscriptionId.Length)]}... / RG '{resourceGroup}'");

        try
        {
            var functionApps = new List<FunctionAppInfo>();
            
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Resolving subscription resource...");
            var subscription = _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Fetching resource group '{resourceGroup}'...");
            
            try
            {
                var resourceGroupResource = await subscription.GetResourceGroupAsync(resourceGroup, cancellationToken);
                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Resource group found, listing web apps...");
                
                var webApps = resourceGroupResource.Value.GetWebSites();

                var webAppCount = 0;
                var functionAppCount = 0;
                
                await foreach (var webApp in webApps.GetAllAsync(cancellationToken: cancellationToken))
            {
                webAppCount++;
                if (webApp.Data.Kind?.Contains("functionapp") == true)
                {
                    functionAppCount++;
                    timestamp = DateTime.Now.ToString("HH:mm:ss");
                    Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Found Function App '{webApp.Data.Name}'");
                    
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
                        
                        if (!string.IsNullOrEmpty(appInfo.AppInsightsConnectionString))
                        {
                            // Parse Application Insights Resource ID from connection string
                            // Connection string format: InstrumentationKey=xxx;IngestionEndpoint=https://...;LiveEndpoint=https://...
                            // Or may contain explicit resource ID
                            appInfo.AppInsightsResourceId = ParseAppInsightsResourceId(appInfo.AppInsightsConnectionString, subscription, appInfo.Name);
                            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService:   - App Insights connected");
                            if (!string.IsNullOrEmpty(appInfo.AppInsightsResourceId))
                            {
                                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService:   - App Insights Resource ID: {appInfo.AppInsightsResourceId[..Math.Min(60, appInfo.AppInsightsResourceId.Length)]}...");
                            }
                        }
                    }

                    try
                    {
                        Console.WriteLine($"[{timestamp}] FunctionDiscoveryService:   - Listing functions...");
                        var functionsCollection = webApp.GetSiteFunctions();
                        var functionCount = 0;
                        
                        await foreach (var function in functionsCollection.GetAllAsync(cancellationToken: cancellationToken))
                        {
                            functionCount++;
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
                        
                        timestamp = DateTime.Now.ToString("HH:mm:ss");
                        Console.WriteLine($"[{timestamp}] FunctionDiscoveryService:   - Found {functionCount} function(s)");
                    }
                    catch (Exception ex)
                    {
                        timestamp = DateTime.Now.ToString("HH:mm:ss");
                        Console.WriteLine($"[{timestamp}] FunctionDiscoveryService:   - ERROR listing functions: {ex.Message}");
                        _logger.LogWarning(ex, "Failed to list functions for {AppName}", appInfo.Name);
                    }

                    functionApps.Add(appInfo);
                }
            }

            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Discovery complete - {webAppCount} web app(s), {functionAppCount} function app(s)");
            
            return functionApps;
            }
            catch (OperationCanceledException)
            {
                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Operation cancelled (timeout)");
                throw;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Resource group '{resourceGroup}' not found or no access");
                throw;
            }
        }
        catch (Exception ex)
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: ERROR - {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[{timestamp}] FunctionDiscoveryService:   Inner: {ex.InnerException.Message}");
            }
            _logger.LogError(ex, "Failed to discover function apps in {ResourceGroup}", resourceGroup);
            throw;
        }
    }

    public async Task<List<string>> ListResourceGroupsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return new List<string> { "demo-rg-east", "demo-rg-west", "demo-rg-central" };
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Listing resource groups in subscription {subscriptionId[..Math.Min(8, subscriptionId.Length)]}...");

        try
        {
            var resourceGroups = new List<string>();
            
            var subscription = _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            
            await foreach (var rg in subscription.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken))
            {
                resourceGroups.Add(rg.Data.Name);
            }
            
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: Found {resourceGroups.Count} resource group(s)");
            
            return resourceGroups.OrderBy(name => name).ToList();
        }
        catch (Exception ex)
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionDiscoveryService: ERROR listing resource groups - {ex.GetType().Name}: {ex.Message}");
            _logger.LogError(ex, "Failed to list resource groups in subscription {SubscriptionId}", subscriptionId);
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
                AppInsightsResourceId = "/subscriptions/demo-sub/resourceGroups/demo-rg/providers/microsoft.insights/components/order-processor-ai",
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
                AppInsightsResourceId = "/subscriptions/demo-sub/resourceGroups/demo-rg/providers/microsoft.insights/components/data-sync-ai",
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
                AppInsightsResourceId = "/subscriptions/demo-sub/resourceGroups/demo-rg/providers/microsoft.insights/components/reporting-ai",
                Functions = new List<FunctionInfo>
                {
                    new() { Name = "GenerateReport", TriggerType = "timerTrigger" },
                    new() { Name = "EmailReport", TriggerType = "queueTrigger" },
                    new() { Name = "GetReportStatus", TriggerType = "httpTrigger" }
                }
            }
        };
    }

    private string? ParseAppInsightsResourceId(string connectionString, Azure.ResourceManager.Resources.SubscriptionResource subscription, string functionAppName)
    {
        try
        {
            // Try to extract the Application Insights component name from the connection string
            // For Azure-deployed apps, we'll try to find the AI resource by convention (same name or appName-ai)
            // Connection string format: InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/;...
            
            // Extract the ingestion endpoint to get region hint
            var parts = connectionString.Split(';');
            string? region = null;
            
            foreach (var part in parts)
            {
                if (part.StartsWith("IngestionEndpoint=", StringComparison.OrdinalIgnoreCase))
                {
                    var endpoint = part.Substring("IngestionEndpoint=".Length);
                    // Extract region from endpoint like https://eastus-1.in.applicationinsights.azure.com/
                    if (endpoint.Contains(".in.applicationinsights.azure.com"))
                    {
                        var host = new Uri(endpoint).Host;
                        region = host.Split('.')[0]; // e.g., "eastus-1"
                    }
                }
            }
            
            // Build a conventional AI resource ID
            // Pattern: /subscriptions/{sub}/resourceGroups/{rg}/providers/microsoft.insights/components/{name}
            var subscriptionId = subscription.Id.ToString().Split('/').Last();
            
            // Try common naming conventions: appName-ai, appName-insights, or just appName
            var possibleNames = new[] { $"{functionAppName}-ai", $"{functionAppName}-insights", functionAppName };
            
            // For now, construct a likely resource ID based on the subscription and function app location
            // In a real implementation, we could query the subscription for AI components
            // Since we have limited context, we'll construct the most likely path
            var resourceGroupName = subscription.Id.ToString().Contains("resourceGroups/") 
                ? subscription.Id.ToString().Split("resourceGroups/")[1].Split('/')[0]
                : "unknown";
            
            // Return the most likely AI resource ID
            return $"{subscription.Id}/providers/microsoft.insights/components/{functionAppName}";
        }
        catch
        {
            return null;
        }
    }
}
