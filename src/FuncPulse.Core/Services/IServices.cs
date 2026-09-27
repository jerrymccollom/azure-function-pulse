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
    Task<List<FunctionInvocation>> GetInvocationsAsync(string resourceId, string functionName, TimeRange timeRange, CancellationToken cancellationToken = default);
}

public interface ILogsService
{
    Task<InvocationLog?> GetInvocationLogsAsync(string appInsightsResourceId, string operationId, CancellationToken cancellationToken = default);
}
