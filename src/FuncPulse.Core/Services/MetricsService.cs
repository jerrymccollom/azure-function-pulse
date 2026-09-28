using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using FuncPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FuncPulse.Core.Services;

public class MetricsService : IMetricsService
{
    private readonly LogsQueryClient? _logsClient;
    private readonly ILogger<MetricsService> _logger;
    private readonly AzureSettings _settings;

    public MetricsService(
        IOptions<AzureSettings> settings,
        ILogger<MetricsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        if (!_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var credentialType = AzureCredentialFactory.GetCredentialDescription();
            Console.WriteLine($"[{timestamp}] MetricsService: Initializing {credentialType} for Application Insights...");
            
            try
            {
                var credential = AzureCredentialFactory.CreateCredential();
                _logsClient = new LogsQueryClient(credential);
                Console.WriteLine($"[{timestamp}] MetricsService: LogsQueryClient created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{timestamp}] MetricsService: ERROR creating client - {ex.Message}");
                throw;
            }
        }
    }

    public async Task<Dictionary<string, FunctionMetrics>> GetAppMetricsAsync(
        FunctionAppInfo app,
        TimeRange timeRange,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, FunctionMetrics>();
        
        if (_settings.DemoMode)
        {
            // Return demo metrics for each function
            foreach (var function in app.Functions)
            {
                var shortName = GetShortFunctionName(function.Name);
                result[shortName] = GenerateDemoMetrics(function.Name, timeRange);
            }
            return result;
        }

        if (_logsClient == null || string.IsNullOrEmpty(app.AppInsightsResourceId))
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (string.IsNullOrEmpty(app.AppInsightsResourceId))
            {
                Console.WriteLine($"[{timestamp}] MetricsService: App '{app.Name}' has no Application Insights configured - returning zeros");
            }
            else
            {
                Console.WriteLine($"[{timestamp}] MetricsService: LogsQueryClient is null - returning zeros");
            }
            
            // Return zero metrics for all functions
            foreach (var function in app.Functions)
            {
                var shortName = GetShortFunctionName(function.Name);
                result[shortName] = new FunctionMetrics();
            }
            return result;
        }

        var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp2}] MetricsService: Querying Application Insights for app '{app.Name}' over {timeRange.ToDisplayString()}...");

        try
        {
            var timeSpan = timeRange.ToTimeSpan();
            
            // Build set of discovered function names to filter metrics
            var discoveredFunctionNames = new HashSet<string>(
                app.Functions.Select(f => GetShortFunctionName(f.Name)),
                StringComparer.OrdinalIgnoreCase);
            
            Console.WriteLine($"[{timestamp2}] MetricsService: Limiting metrics to {discoveredFunctionNames.Count} discovered function(s)");
            
            // Batch query: get all function metrics in one KQL query
            var query = $@"
                requests
                | where timestamp >= ago({FormatTimeSpan(timeSpan)})
                | summarize 
                    SuccessCount = countif(success == true), 
                    FailureCount = countif(success == false) 
                    by name
                | project name, SuccessCount, FailureCount";

            Console.WriteLine($"[{timestamp2}] MetricsService: Executing KQL query against Application Insights...");
            
            var response = await _logsClient.QueryResourceAsync(
                new ResourceIdentifier(app.AppInsightsResourceId),
                query,
                new QueryTimeRange(timeSpan),
                cancellationToken: cancellationToken);

            if (response?.Value?.Table != null)
            {
                var table = response.Value.Table;
                timestamp2 = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp2}] MetricsService: Retrieved {table.Rows.Count} metric rows from Application Insights");
                
                var matchedCount = 0;
                var skippedCount = 0;
                
                foreach (var row in table.Rows)
                {
                    var functionName = row.GetString("name");
                    
                    if (!string.IsNullOrEmpty(functionName))
                    {
                        // Only include metrics for discovered functions
                        if (discoveredFunctionNames.Contains(functionName))
                        {
                            var successCount = row.GetInt32("SuccessCount") ?? 0;
                            var failureCount = row.GetInt32("FailureCount") ?? 0;
                            
                            result[functionName] = new FunctionMetrics
                            {
                                SuccessCount = successCount,
                                FailureCount = failureCount
                            };
                            Console.WriteLine($"[{timestamp2}] MetricsService:   - {functionName}: {successCount} success, {failureCount} failures");
                            matchedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                }
                
                if (skippedCount > 0)
                {
                    timestamp2 = DateTime.Now.ToString("HH:mm:ss");
                    Console.WriteLine($"[{timestamp2}] MetricsService: Skipped {skippedCount} non-function request(s) (HTTP routes, swagger, etc.)");
                }
                
                timestamp2 = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp2}] MetricsService: Matched {matchedCount} discovered function(s) with metrics");
            }
            
            // Ensure all discovered functions have metrics (zeros if not found in results)
            foreach (var function in app.Functions)
            {
                var shortName = GetShortFunctionName(function.Name);
                if (!result.ContainsKey(shortName))
                {
                    result[shortName] = new FunctionMetrics();
                }
            }
            
            timestamp2 = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp2}] MetricsService: App metrics complete - {result.Count} functions");
            
            return result;
        }
        catch (Exception ex)
        {
            timestamp2 = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp2}] MetricsService: ERROR querying Application Insights - {ex.Message}");
            _logger.LogWarning(ex, "Failed to query metrics for app {AppName}", app.Name);
            
            // Return zeros for all functions on error
            foreach (var function in app.Functions)
            {
                var shortName = GetShortFunctionName(function.Name);
                if (!result.ContainsKey(shortName))
                {
                    result[shortName] = new FunctionMetrics();
                }
            }
            
            return result;
        }
    }

    public async Task<FunctionMetrics> GetFunctionMetricsAsync(
        string resourceId, 
        string functionName, 
        TimeRange timeRange, 
        CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return GenerateDemoMetrics(functionName, timeRange);
        }

        // This method is kept for compatibility but returns zeros in real mode
        // Real metrics should use GetAppMetricsAsync for batched queries
        return new FunctionMetrics();
    }

    public async Task<List<FunctionInvocation>> GetInvocationsAsync(
        string appInsightsResourceId,
        string functionName,
        TimeRange timeRange,
        CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return GenerateDemoInvocations(functionName, timeRange);
        }

        if (_logsClient == null || string.IsNullOrEmpty(appInsightsResourceId))
        {
            return new List<FunctionInvocation>();
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] MetricsService: Querying invocations for function '{functionName}'...");

        try
        {
            var timeSpan = timeRange.ToTimeSpan();
            var shortName = GetShortFunctionName(functionName);
            
            var query = $@"
                requests
                | where timestamp >= ago({FormatTimeSpan(timeSpan)})
                | where name == '{shortName}'
                | summarize 
                    arg_max(timestamp, duration, success, resultCode, customDimensions)
                    by operation_Id
                | project 
                    operation_Id, 
                    timestamp, 
                    duration, 
                    success, 
                    resultCode, 
                    customDimensions
                | order by timestamp desc
                | limit 100";

            var response = await _logsClient.QueryResourceAsync(
                new ResourceIdentifier(appInsightsResourceId),
                query,
                new QueryTimeRange(timeSpan),
                cancellationToken: cancellationToken);

            var invocations = new List<FunctionInvocation>();
            
            if (response?.Value?.Table != null)
            {
                var table = response.Value.Table;
                
                foreach (var row in table.Rows)
                {
                    var operationId = row.GetString("operation_Id") ?? Guid.NewGuid().ToString();
                    var ts = row.GetDateTimeOffset("timestamp")?.UtcDateTime ?? DateTime.UtcNow;
                    var duration = row.GetDouble("duration") ?? 0;
                    var success = ParseSuccessValue(row["success"]);
                    var resultCode = row.GetString("resultCode");
                    
                    invocations.Add(new FunctionInvocation
                    {
                        OperationId = operationId,
                        Timestamp = ts,
                        Duration = TimeSpan.FromMilliseconds(duration),
                        Success = success,
                        ResultCode = resultCode,
                        ExceptionMessage = success ? null : "Invocation failed"
                    });
                }
            }
            
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] MetricsService: Retrieved {invocations.Count} invocations");
            
            return invocations;
        }
        catch (Exception ex)
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] MetricsService: ERROR querying invocations - {ex.Message}");
            _logger.LogWarning(ex, "Failed to query invocations for {FunctionName}", functionName);
            return new List<FunctionInvocation>();
        }
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h";
        return $"{(int)timeSpan.TotalMinutes}m";
    }

    internal static bool ParseSuccessValue(object? rawValue)
    {
        return rawValue switch
        {
            null => true,
            bool value => value,
            string value when bool.TryParse(value, out var parsed) => parsed,
            byte value => value != 0,
            short value => value != 0,
            int value => value != 0,
            long value => value != 0,
            double value => value != 0,
            decimal value => value != 0,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            JsonElement { ValueKind: JsonValueKind.String } value when bool.TryParse(value.GetString(), out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.Number } value when value.TryGetInt64(out var parsed) => parsed != 0,
            JsonElement { ValueKind: JsonValueKind.Number } value when value.TryGetDouble(out var parsed) => parsed != 0,
            _ => true
        };
    }

    private string GetShortFunctionName(string fullName)
    {
        var lastSlashIndex = fullName.LastIndexOf('/');
        return lastSlashIndex >= 0 ? fullName.Substring(lastSlashIndex + 1) : fullName;
    }

    private FunctionMetrics GenerateDemoMetrics(string functionName, TimeRange timeRange)
    {
        var random = new Random(functionName.GetHashCode());
        var metrics = new FunctionMetrics();
        var endTime = DateTime.UtcNow;
        var startTime = endTime - timeRange.ToTimeSpan();
        
        var totalInvocations = timeRange switch
        {
            TimeRange.Hours24 => random.Next(100, 1000),
            TimeRange.Days7 => random.Next(500, 5000),
            TimeRange.Days30 => random.Next(2000, 20000),
            _ => random.Next(100, 1000)
        };

        var failureRate = functionName.Contains("Payment") ? random.Next(3, 8) : 
                         functionName.Contains("Sync") ? random.Next(0, 2) :
                         random.Next(0, 5);

        metrics.FailureCount = totalInvocations * failureRate / 100;
        metrics.SuccessCount = totalInvocations - metrics.FailureCount;

        var points = timeRange switch
        {
            TimeRange.Hours24 => 24,
            TimeRange.Days7 => 7,
            TimeRange.Days30 => 30,
            _ => 24
        };

        var interval = (endTime - startTime).TotalMinutes / points;
        for (int i = 0; i < points; i++)
        {
            var timestamp = startTime.AddMinutes(i * interval);
            var pointTotal = totalInvocations / points;
            var pointFailures = metrics.FailureCount / points;
            
            metrics.TimeSeries.Add(new MetricDataPoint
            {
                Timestamp = timestamp,
                SuccessCount = pointTotal - pointFailures,
                FailureCount = pointFailures
            });
        }

        return metrics;
    }

    private List<FunctionInvocation> GenerateDemoInvocations(string functionName, TimeRange timeRange)
    {
        var random = new Random(functionName.GetHashCode());
        var invocations = new List<FunctionInvocation>();
        var now = DateTime.UtcNow;
        
        var count = timeRange switch
        {
            TimeRange.Hours24 => 50,
            TimeRange.Days7 => 100,
            TimeRange.Days30 => 200,
            _ => 50
        };

        bool latestShouldSucceed = true;
        int failureThreshold = 5;

        if (functionName.Contains("Payment"))
        {
            latestShouldSucceed = false;
            failureThreshold = 8;
        }
        else if (functionName.Contains("Sync"))
        {
            latestShouldSucceed = true;
            failureThreshold = 2;
        }

        for (int i = 0; i < count; i++)
        {
            var timestamp = now - TimeSpan.FromMinutes(i * 5);
            bool success;
            
            if (i == 0)
            {
                success = latestShouldSucceed;
            }
            else if (i < 15 && functionName.Contains("Order"))
            {
                success = random.Next(0, 100) >= 10;
            }
            else
            {
                success = random.Next(0, 100) >= failureThreshold;
            }
            
            invocations.Add(new FunctionInvocation
            {
                OperationId = Guid.NewGuid().ToString(),
                Timestamp = timestamp,
                Duration = TimeSpan.FromMilliseconds(random.Next(50, 5000)),
                Success = success,
                ResultCode = success ? "200" : "500",
                ExceptionMessage = success ? null : "Demo exception message"
            });
        }

        return invocations.OrderByDescending(i => i.Timestamp).ToList();
    }
}
