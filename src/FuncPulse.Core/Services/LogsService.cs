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
            var credential = new DefaultAzureCredential();
            _logsClient = new LogsQueryClient(credential);
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

        try
        {
            var query = $@"
                union requests, exceptions, traces
                | where operation_Id == '{operationId}'
                | project timestamp, itemType, message, severityLevel, outerMessage, type, problemId, details
                | order by timestamp asc";

            var response = await _logsClient.QueryWorkspaceAsync(
                appInsightsResourceId,
                query,
                new QueryTimeRange(TimeSpan.FromHours(24)),
                cancellationToken: cancellationToken);

            if (response?.Value == null)
            {
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
                var timestamp = row.GetDateTimeOffset("timestamp")?.UtcDateTime ?? DateTime.UtcNow;
                var itemType = row.GetString("itemType") ?? "trace";
                var message = row.GetString("message") ?? row.GetString("outerMessage") ?? string.Empty;
                var severityLevel = row.GetInt32("severityLevel");
                var exceptionType = row.GetString("type");
                var details = row.GetString("details");

                log.Entries.Add(new LogEntry
                {
                    Timestamp = timestamp,
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

            return log;
        }
        catch (Exception ex)
        {
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
