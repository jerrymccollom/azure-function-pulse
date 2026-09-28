using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using FuncPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FuncPulse.Core.Services;

public class FunctionManagementService : IFunctionManagementService
{
    private readonly ArmClient? _armClient;
    private readonly ILogger<FunctionManagementService> _logger;
    private readonly AzureSettings _settings;

    public FunctionManagementService(
        IOptions<AzureSettings> settings,
        ILogger<FunctionManagementService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        if (!_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var credentialType = AzureCredentialFactory.GetCredentialDescription();
            Console.WriteLine($"[{timestamp}] FunctionManagementService: Initializing {credentialType}...");
            
            try
            {
                var credential = AzureCredentialFactory.CreateCredential();
                _armClient = new ArmClient(credential);
                Console.WriteLine($"[{timestamp}] FunctionManagementService: ARM client created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{timestamp}] FunctionManagementService: ERROR creating client - {ex.Message}");
                throw;
            }
        }
    }

    public async Task<FunctionAppState> GetStateAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return FunctionAppState.Running;
        }

        if (_armClient == null)
        {
            return FunctionAppState.Unknown;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] FunctionManagementService: Getting state for {resourceId[..Math.Min(60, resourceId.Length)]}...");

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
            var webSite = await webSiteResource.GetAsync(cancellationToken);
            
            var state = webSite.Value.Data.State?.ToLowerInvariant() switch
            {
                "running" => FunctionAppState.Running,
                "stopped" => FunctionAppState.Stopped,
                _ => FunctionAppState.Unknown
            };

            Console.WriteLine($"[{timestamp}] FunctionManagementService: State is {state}");
            return state;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp}] FunctionManagementService: ERROR getting state - {ex.Message}");
            _logger.LogWarning(ex, "Failed to get state for function app {ResourceId}", resourceId);
            return FunctionAppState.Unknown;
        }
    }

    public async Task<bool> StartAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionManagementService: DEMO MODE - Simulating start");
            await Task.Delay(1000, cancellationToken);
            return true;
        }

        if (_armClient == null)
        {
            return false;
        }

        var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp2}] FunctionManagementService: Starting {resourceId[..Math.Min(60, resourceId.Length)]}...");

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
            await webSiteResource.StartAsync(cancellationToken);
            
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: Start command completed");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: PERMISSION DENIED - Insufficient permissions to start function app");
            _logger.LogWarning(ex, "Permission denied when starting function app {ResourceId}", resourceId);
            throw new UnauthorizedAccessException("Insufficient permissions to start the function app. Ensure you have 'Microsoft.Web/sites/start/action' permission.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: ERROR starting function app - {ex.Message}");
            _logger.LogError(ex, "Failed to start function app {ResourceId}", resourceId);
            throw;
        }
    }

    public async Task<bool> StopAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionManagementService: DEMO MODE - Simulating stop");
            await Task.Delay(1000, cancellationToken);
            return true;
        }

        if (_armClient == null)
        {
            return false;
        }

        var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp2}] FunctionManagementService: Stopping {resourceId[..Math.Min(60, resourceId.Length)]}...");

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
            await webSiteResource.StopAsync(cancellationToken);
            
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: Stop command completed");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: PERMISSION DENIED - Insufficient permissions to stop function app");
            _logger.LogWarning(ex, "Permission denied when stopping function app {ResourceId}", resourceId);
            throw new UnauthorizedAccessException("Insufficient permissions to stop the function app. Ensure you have 'Microsoft.Web/sites/stop/action' permission.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: ERROR stopping function app - {ex.Message}");
            _logger.LogError(ex, "Failed to stop function app {ResourceId}", resourceId);
            throw;
        }
    }

    public async Task<bool> RestartAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionManagementService: DEMO MODE - Simulating restart");
            await Task.Delay(1500, cancellationToken);
            return true;
        }

        if (_armClient == null)
        {
            return false;
        }

        var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp2}] FunctionManagementService: Restarting {resourceId[..Math.Min(60, resourceId.Length)]}...");

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
            await webSiteResource.RestartAsync(softRestart: false, synchronous: false, cancellationToken);
            
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: Restart command completed");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: PERMISSION DENIED - Insufficient permissions to restart function app");
            _logger.LogWarning(ex, "Permission denied when restarting function app {ResourceId}", resourceId);
            throw new UnauthorizedAccessException("Insufficient permissions to restart the function app. Ensure you have 'Microsoft.Web/sites/restart/action' permission.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: ERROR restarting function app - {ex.Message}");
            _logger.LogError(ex, "Failed to restart function app {ResourceId}", resourceId);
            throw;
        }
    }

    public async Task<bool> CanManageAsync(string subscriptionId, string resourceGroup, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return true;
        }

        if (_armClient == null)
        {
            return false;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] FunctionManagementService: Checking management permissions for RG '{resourceGroup}'...");

        try
        {
            var subscription = _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            
            var resourceGroupResource = await subscription.GetResourceGroupAsync(resourceGroup, cancellationToken);
            
            Console.WriteLine($"[{timestamp}] FunctionManagementService: Access check passed - user has at least read access");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 403 || ex.Status == 401)
        {
            Console.WriteLine($"[{timestamp}] FunctionManagementService: No permissions to access resource group");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp}] FunctionManagementService: ERROR checking permissions - {ex.Message}");
            _logger.LogWarning(ex, "Failed to check management permissions for resource group {ResourceGroup}", resourceGroup);
            return false;
        }
    }

    public async Task<bool> EnableFunctionAsync(string appResourceId, string functionName, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionManagementService: DEMO MODE - Simulating enable function");
            await Task.Delay(500, cancellationToken);
            return true;
        }

        if (_armClient == null)
        {
            return false;
        }

        var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
        var shortName = GetShortFunctionName(functionName);
        Console.WriteLine($"[{timestamp2}] FunctionManagementService: Enabling function '{shortName}' in {appResourceId[..Math.Min(60, appResourceId.Length)]}...");

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(appResourceId));
            var appSettings = await webSiteResource.GetApplicationSettingsAsync(cancellationToken);
            var settings = appSettings.Value.Properties;

            var disableKey = $"AzureWebJobs.{shortName}.Disabled";
            
            if (settings.ContainsKey(disableKey))
            {
                settings.Remove(disableKey);
                await webSiteResource.UpdateApplicationSettingsAsync(appSettings.Value, cancellationToken);
                
                Console.WriteLine($"[{timestamp2}] FunctionManagementService: Function enabled successfully");
                return true;
            }
            else
            {
                Console.WriteLine($"[{timestamp2}] FunctionManagementService: Function was already enabled");
                return true;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: PERMISSION DENIED - Insufficient permissions to enable function");
            _logger.LogWarning(ex, "Permission denied when enabling function {FunctionName}", functionName);
            throw new UnauthorizedAccessException("Insufficient permissions to enable the function. Ensure you have 'Microsoft.Web/sites/config/write' permission.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: ERROR enabling function - {ex.Message}");
            _logger.LogError(ex, "Failed to enable function {FunctionName}", functionName);
            throw;
        }
    }

    public async Task<bool> DisableFunctionAsync(string appResourceId, string functionName, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] FunctionManagementService: DEMO MODE - Simulating disable function");
            await Task.Delay(500, cancellationToken);
            return true;
        }

        if (_armClient == null)
        {
            return false;
        }

        var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
        var shortName = GetShortFunctionName(functionName);
        Console.WriteLine($"[{timestamp2}] FunctionManagementService: Disabling function '{shortName}' in {appResourceId[..Math.Min(60, appResourceId.Length)]}...");

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(appResourceId));
            var appSettings = await webSiteResource.GetApplicationSettingsAsync(cancellationToken);
            var settings = appSettings.Value.Properties;

            var disableKey = $"AzureWebJobs.{shortName}.Disabled";
            settings[disableKey] = "true";
            
            await webSiteResource.UpdateApplicationSettingsAsync(appSettings.Value, cancellationToken);
            
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: Function disabled successfully");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: PERMISSION DENIED - Insufficient permissions to disable function");
            _logger.LogWarning(ex, "Permission denied when disabling function {FunctionName}", functionName);
            throw new UnauthorizedAccessException("Insufficient permissions to disable the function. Ensure you have 'Microsoft.Web/sites/config/write' permission.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp2}] FunctionManagementService: ERROR disabling function - {ex.Message}");
            _logger.LogError(ex, "Failed to disable function {FunctionName}", functionName);
            throw;
        }
    }

    public async Task<bool> IsFunctionEnabledAsync(string appResourceId, string functionName, CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return true;
        }

        if (_armClient == null)
        {
            return true;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var shortName = GetShortFunctionName(functionName);

        try
        {
            var webSiteResource = _armClient.GetWebSiteResource(new ResourceIdentifier(appResourceId));
            var appSettings = await webSiteResource.GetApplicationSettingsAsync(cancellationToken);
            var settings = appSettings.Value.Properties;

            var disableKey = $"AzureWebJobs.{shortName}.Disabled";
            
            if (settings.TryGetValue(disableKey, out var value))
            {
                var isDisabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
                Console.WriteLine($"[{timestamp}] FunctionManagementService: Function '{shortName}' is {(isDisabled ? "disabled" : "enabled")}");
                return !isDisabled;
            }
            
            Console.WriteLine($"[{timestamp}] FunctionManagementService: Function '{shortName}' has no disable setting - assumed enabled");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{timestamp}] FunctionManagementService: ERROR checking function status - {ex.Message}");
            _logger.LogWarning(ex, "Failed to check function status for {FunctionName}", functionName);
            return true;
        }
    }

    private string GetShortFunctionName(string fullName)
    {
        var lastSlashIndex = fullName.LastIndexOf('/');
        return lastSlashIndex >= 0 ? fullName.Substring(lastSlashIndex + 1) : fullName;
    }
}
