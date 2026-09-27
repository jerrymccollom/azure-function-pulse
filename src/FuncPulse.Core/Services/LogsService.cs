using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using FuncPulse.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FuncPulse.Core.Services;

public class LogsService : ILogsService
{
    private readonly LogsQueryClient? _logsClient;
    private readonly ILogger<LogsService> _logger;
    private readonly AzureSettings _settings;

    public LogsService(
        IOptions<AzureSettings> settings,
        ILogger<LogsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        if (!_settings.DemoMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var credentialType = AzureCredentialFactory.GetCredentialDescription();
            Console.WriteLine($"[{timestamp}] LogsService: Initializing {credentialType} for Log Analytics...");
            
            try
            {
                var credential = AzureCredentialFactory.CreateCredential();
                _logsClient = new LogsQueryClient(credential);
                Console.WriteLine($"[{timestamp}] LogsService: LogsQueryClient created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{timestamp}] LogsService: ERROR creating client - {ex.Message}");
                throw;
            }
        }
    }

    public async Task<InvocationLog?> GetInvocationLogsAsync(
        string appInsightsResourceId, 
        string operationId, 
        CancellationToken cancellationToken = default)
    {
        if (_settings.DemoMode)
        {
            return GenerateDemoLogs(operationId);
        }

        if (_logsClient == null || string.IsNullOrEmpty(appInsightsResourceId))
        {
            return null;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] LogsService: Querying Application Insights logs for operation {operationId[..Math.Min(8, operationId.Length)]}...");

        try
        {
            var query = $@"
                union requests, exceptions, traces
                | where operation_Id == '{operationId}'
                | project timestamp, itemType, message, severityLevel, outerMessage, type, problemId, details
                | order by timestamp asc";

            Console.WriteLine($"[{timestamp}] LogsService: Executing KQL query against Application Insights resource...");
            var response = await _logsClient.QueryResourceAsync(
                new ResourceIdentifier(appInsightsResourceId),
                query,
                new QueryTimeRange(TimeSpan.FromHours(24)),
                cancellationToken: cancellationToken);

            if (response?.Value == null)
            {
                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] LogsService: No logs found");
                return null;
            }

            var log = new InvocationLog
            {
                OperationId = operationId,
                Timestamp = DateTime.UtcNow
            };

            var table = response.Value.Table;
            foreach (var row in table.Rows)
            {
                var ts = row.GetDateTimeOffset("timestamp")?.UtcDateTime ?? DateTime.UtcNow;
                var itemType = row.GetString("itemType") ?? "trace";
                var message = row.GetString("message") ?? row.GetString("outerMessage") ?? string.Empty;
                var severityLevel = row.GetInt32("severityLevel");
                var exceptionType = row.GetString("type");
                var details = row.GetString("details");

                log.Entries.Add(new LogEntry
                {
                    Timestamp = ts,
                    Level = severityLevel switch
                    {
                        0 => "Verbose",
                        1 => "Information",
                        2 => "Warning",
                        3 => "Error",
                        4 => "Critical",
                        _ => "Information"
                    },
                    Message = message,
                    ExceptionType = exceptionType,
                    StackTrace = details
                });
            }

            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] LogsService: Retrieved {log.Entries.Count} log entries");
            
            return log;
        }
        catch (Exception ex)
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] LogsService: ERROR - {ex.GetType().Name}: {ex.Message}");
            _logger.LogError(ex, "Failed to query logs for operation {OperationId}", operationId);
            return null;
        }
    }

    private InvocationLog GenerateDemoLogs(string operationId)
    {
        var now = DateTime.UtcNow;
        return new InvocationLog
        {
            OperationId = operationId,
            Timestamp = now,
            Entries = new List<LogEntry>
            {
                new()
                {
                    Timestamp = now.AddSeconds(-5),
                    Level = "Information",
                    Message = "Function started execution"
                },
                new()
                {
                    Timestamp = now.AddSeconds(-4),
                    Level = "Information",
                    Message = "Processing request with payload size: 1024 bytes"
                },
                new()
                {
                    Timestamp = now.AddSeconds(-2),
                    Level = "Error",
                    Message = "Operation failed with exception",
                    ExceptionType = "System.InvalidOperationException",
                    StackTrace = @"   at FunctionApp.ProcessOrder(String orderId) in /src/Functions/OrderProcessor.cs:line 42
   at Microsoft.Azure.WebJobs.Host.Executors.FunctionInvoker.InvokeAsync(Object instance, Object[] arguments)
   at Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.InvokeAsync()"
                },
                new()
                {
                    Timestamp = now.AddSeconds(-1),
                    Level = "Error",
                    Message = "Function execution failed"
                }
            }
        };
    }
}
