using Azure.ResourceManager;
using FuncPulse.Core.Models;
using FuncPulse.Core.Services;
using Microsoft.Extensions.Options;

namespace FuncPulse.Web.Services;

/// <summary>
/// Web-tier wrapper services that use user-aware credentials.
/// These services create instances of core services with the appropriate credentials.
/// </summary>

public class WebFunctionDiscoveryService : IFunctionDiscoveryService
{
    private readonly IAzureCredentialService _credentialService;
    private readonly IOptions<AzureSettings> _settings;
    private readonly ILoggerFactory _loggerFactory;

    public WebFunctionDiscoveryService(
        IAzureCredentialService credentialService,
        IOptions<AzureSettings> settings,
        ILoggerFactory loggerFactory)
    {
        _credentialService = credentialService;
        _settings = settings;
        _loggerFactory = loggerFactory;
    }

    public async Task<List<FunctionAppInfo>> DiscoverFunctionAppsAsync(
        string subscriptionId, 
        string resourceGroup, 
        CancellationToken cancellationToken = default)
    {
        if (_settings.Value.DemoMode)
        {
            var demoService = new FunctionDiscoveryService(_settings, _loggerFactory.CreateLogger<FunctionDiscoveryService>());
            return await demoService.DiscoverFunctionAppsAsync(subscriptionId, resourceGroup, cancellationToken);
        }

        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionDiscoveryService(_settings, _loggerFactory.CreateLogger<FunctionDiscoveryService>(), armClient);
        return await service.DiscoverFunctionAppsAsync(subscriptionId, resourceGroup, cancellationToken);
    }

    public async Task<List<string>> ListResourceGroupsAsync(
        string subscriptionId, 
        CancellationToken cancellationToken = default)
    {
        if (_settings.Value.DemoMode)
        {
            var demoService = new FunctionDiscoveryService(_settings, _loggerFactory.CreateLogger<FunctionDiscoveryService>());
            return await demoService.ListResourceGroupsAsync(subscriptionId, cancellationToken);
        }

        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionDiscoveryService(_settings, _loggerFactory.CreateLogger<FunctionDiscoveryService>(), armClient);
        return await service.ListResourceGroupsAsync(subscriptionId, cancellationToken);
    }
}

public class WebMetricsService : IMetricsService
{
    private readonly IAzureCredentialService _credentialService;
    private readonly IOptions<AzureSettings> _settings;
    private readonly ILoggerFactory _loggerFactory;

    public WebMetricsService(
        IAzureCredentialService credentialService,
        IOptions<AzureSettings> settings,
        ILoggerFactory loggerFactory)
    {
        _credentialService = credentialService;
        _settings = settings;
        _loggerFactory = loggerFactory;
    }

    public async Task<FunctionMetrics> GetFunctionMetricsAsync(
        string resourceId, 
        string functionName, 
        TimeRange timeRange, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var service = new MetricsService(_settings, _loggerFactory.CreateLogger<MetricsService>(), credential);
        return await service.GetFunctionMetricsAsync(resourceId, functionName, timeRange, cancellationToken);
    }

    public async Task<Dictionary<string, FunctionMetrics>> GetAppMetricsAsync(
        FunctionAppInfo app, 
        TimeRange timeRange, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var service = new MetricsService(_settings, _loggerFactory.CreateLogger<MetricsService>(), credential);
        return await service.GetAppMetricsAsync(app, timeRange, cancellationToken);
    }

    public async Task<List<FunctionInvocation>> GetInvocationsAsync(
        string appInsightsResourceId, 
        string functionName, 
        TimeRange timeRange, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var service = new MetricsService(_settings, _loggerFactory.CreateLogger<MetricsService>(), credential);
        return await service.GetInvocationsAsync(appInsightsResourceId, functionName, timeRange, cancellationToken);
    }
}

public class WebLogsService : ILogsService
{
    private readonly IAzureCredentialService _credentialService;
    private readonly IOptions<AzureSettings> _settings;
    private readonly ILoggerFactory _loggerFactory;

    public WebLogsService(
        IAzureCredentialService credentialService,
        IOptions<AzureSettings> settings,
        ILoggerFactory loggerFactory)
    {
        _credentialService = credentialService;
        _settings = settings;
        _loggerFactory = loggerFactory;
    }

    public async Task<InvocationLog?> GetInvocationLogsAsync(
        string appInsightsResourceId, 
        string operationId, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var service = new LogsService(_settings, _loggerFactory.CreateLogger<LogsService>(), credential);
        return await service.GetInvocationLogsAsync(appInsightsResourceId, operationId, cancellationToken);
    }
}

public class WebFunctionManagementService : IFunctionManagementService
{
    private readonly IAzureCredentialService _credentialService;
    private readonly IOptions<AzureSettings> _settings;
    private readonly ILoggerFactory _loggerFactory;

    public WebFunctionManagementService(
        IAzureCredentialService credentialService,
        IOptions<AzureSettings> settings,
        ILoggerFactory loggerFactory)
    {
        _credentialService = credentialService;
        _settings = settings;
        _loggerFactory = loggerFactory;
    }

    public async Task<FunctionAppState> GetStateAsync(
        string resourceId, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.GetStateAsync(resourceId, cancellationToken);
    }

    public async Task<bool> StartAsync(
        string resourceId, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.StartAsync(resourceId, cancellationToken);
    }

    public async Task<bool> StopAsync(
        string resourceId, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.StopAsync(resourceId, cancellationToken);
    }

    public async Task<bool> RestartAsync(
        string resourceId, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.RestartAsync(resourceId, cancellationToken);
    }

    public async Task<bool> CanManageAsync(
        string subscriptionId, 
        string resourceGroup, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.CanManageAsync(subscriptionId, resourceGroup, cancellationToken);
    }

    public async Task<bool> EnableFunctionAsync(
        string appResourceId, 
        string functionName, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.EnableFunctionAsync(appResourceId, functionName, cancellationToken);
    }

    public async Task<bool> DisableFunctionAsync(
        string appResourceId, 
        string functionName, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.DisableFunctionAsync(appResourceId, functionName, cancellationToken);
    }

    public async Task<bool> IsFunctionEnabledAsync(
        string appResourceId, 
        string functionName, 
        CancellationToken cancellationToken = default)
    {
        var credential = await _credentialService.CreateCredentialAsync();
        var armClient = new ArmClient(credential);
        var service = new FunctionManagementService(_settings, _loggerFactory.CreateLogger<FunctionManagementService>(), armClient);
        return await service.IsFunctionEnabledAsync(appResourceId, functionName, cancellationToken);
    }
}
