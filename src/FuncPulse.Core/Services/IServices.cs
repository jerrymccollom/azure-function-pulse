using FuncPulse.Core.Models;

namespace FuncPulse.Core.Services;

public interface IFunctionDiscoveryService
{
    Task<List<FunctionAppInfo>> DiscoverFunctionAppsAsync(string subscriptionId, string resourceGroup, CancellationToken cancellationToken = default);
    Task<List<string>> ListResourceGroupsAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

public interface IMetricsService
{
    Task<FunctionMetrics> GetFunctionMetricsAsync(string resourceId, string functionName, TimeRange timeRange, CancellationToken cancellationToken = default);
    Task<Dictionary<string, FunctionMetrics>> GetAppMetricsAsync(FunctionAppInfo app, TimeRange timeRange, CancellationToken cancellationToken = default);
    Task<List<FunctionInvocation>> GetInvocationsAsync(string appInsightsResourceId, string functionName, TimeRange timeRange, CancellationToken cancellationToken = default);
}

public interface ILogsService
{
    Task<InvocationLog?> GetInvocationLogsAsync(string appInsightsResourceId, string operationId, CancellationToken cancellationToken = default);
}

public interface IFunctionManagementService
{
    Task<FunctionAppState> GetStateAsync(string resourceId, CancellationToken cancellationToken = default);
    Task<bool> StartAsync(string resourceId, CancellationToken cancellationToken = default);
    Task<bool> StopAsync(string resourceId, CancellationToken cancellationToken = default);
    Task<bool> RestartAsync(string resourceId, CancellationToken cancellationToken = default);
    Task<bool> CanManageAsync(string subscriptionId, string resourceGroup, CancellationToken cancellationToken = default);
    Task<bool> EnableFunctionAsync(string appResourceId, string functionName, CancellationToken cancellationToken = default);
    Task<bool> DisableFunctionAsync(string appResourceId, string functionName, CancellationToken cancellationToken = default);
    Task<bool> IsFunctionEnabledAsync(string appResourceId, string functionName, CancellationToken cancellationToken = default);
}

public enum FunctionAppState
{
    Unknown,
    Running,
    Stopped,
    Starting,
    Stopping
}
