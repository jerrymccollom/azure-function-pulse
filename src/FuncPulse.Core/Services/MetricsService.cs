using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using FuncPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FuncPulse.Core.Services;

public class MetricsService : IMetricsService
{
    private readonly MetricsQueryClient? _metricsClient;
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
            Console.WriteLine($"[{timestamp}] MetricsService: Initializing DefaultAzureCredential for Azure Monitor...");
            
            try
            {
                var credential = new DefaultAzureCredential();
                _metricsClient = new MetricsQueryClient(credential);
                Console.WriteLine($"[{timestamp}] MetricsService: MetricsQueryClient created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{timestamp}] MetricsService: ERROR creating client - {ex.Message}");
                throw;
            }
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

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] MetricsService: Querying metrics for function '{functionName}' over {timeRange.ToDisplayString()}...");

        try
        {
            var endTime = DateTimeOffset.UtcNow;
            var startTime = endTime - timeRange.ToTimeSpan();
            var interval = TimeSpan.Parse(timeRange.ToMetricInterval());

            var metrics = new FunctionMetrics();
            
            if (_metricsClient == null)
            {
                Console.WriteLine($"[{timestamp}] MetricsService: ERROR - MetricsQueryClient is null");
                return metrics;
            }

            var options = new MetricsQueryOptions
            {
                TimeRange = new QueryTimeRange(startTime, endTime),
                Granularity = interval,
                Filter = $"FunctionName eq '{functionName}'"
            };

            try
            {
                Console.WriteLine($"[{timestamp}] MetricsService: Calling Azure Monitor Metrics API...");
                var response = await _metricsClient.QueryResourceAsync(
                    resourceId,
                    new[] { "FunctionExecutionCount" },
                    options,
                    cancellationToken);

                if (response?.Value?.Metrics != null)
                {
                    foreach (var metric in response.Value.Metrics)
                    {
                        foreach (var timeSeries in metric.TimeSeries)
                        {
                            foreach (var dataPoint in timeSeries.Values)
                            {
                                if (dataPoint.Total.HasValue)
                                {
                                    var total = (int)dataPoint.Total.Value;
                                    metrics.SuccessCount += total;
                                    metrics.TimeSeries.Add(new MetricDataPoint
                                    {
                                        Timestamp = dataPoint.TimeStamp.UtcDateTime,
                                        SuccessCount = total,
                                        FailureCount = 0
                                    });
                                }
                            }
                        }
                    }
                }
                
                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] MetricsService: Metrics retrieved - {metrics.TotalCount} invocations");
            }
            catch (Exception ex)
            {
                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] MetricsService: ERROR querying metrics - {ex.Message}");
                _logger.LogWarning(ex, "Failed to query metrics for {FunctionName}", functionName);
            }

            return metrics;
        }
        catch (Exception ex)
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] MetricsService: ERROR - {ex.GetType().Name}: {ex.Message}");
            _logger.LogError(ex, "Error getting metrics for function {FunctionName}", functionName);
            throw;
        }
    }

    public async Task<List<FunctionInvocation>> GetInvocationsAsync(
        string resourceId, 
        string functionName, 
        TimeRange timeRange, 
        CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return GenerateDemoInvocations(functionName, timeRange);
        }

        await Task.CompletedTask;
        return new List<FunctionInvocation>();
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

        for (int i = 0; i < count; i++)
        {
            var timestamp = now - TimeSpan.FromMinutes(random.Next(0, (int)timeRange.ToTimeSpan().TotalMinutes));
            var success = random.Next(0, 100) >= 5;
            
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
