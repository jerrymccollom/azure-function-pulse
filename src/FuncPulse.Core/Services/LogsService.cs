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
            var query = BuildInvocationLogsQuery(operationId);

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
            var columnNames = table.Columns
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in table.Rows)
            {
                var ts = GetOptionalDateTimeOffset(row, columnNames, "timestamp")?.UtcDateTime ?? DateTime.UtcNow;
                var message = GetOptionalString(row, columnNames, "message") ?? string.Empty;
                var severityLevel = GetOptionalInt32(row, columnNames, "severityLevel");
                var exceptionType = GetOptionalString(row, columnNames, "exceptionType");
                var stackTrace = GetOptionalString(row, columnNames, "stackTrace");

                log.Entries.Add(new LogEntry
                {
                    Timestamp = ts,
                    Level = GetLogLevel(severityLevel, exceptionType),
                    Message = message,
                    ExceptionType = exceptionType,
                    StackTrace = string.IsNullOrWhiteSpace(stackTrace) ? null : stackTrace
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

    internal static string BuildInvocationLogsQuery(string operationId)
    {
        var escapedOperationId = EscapeKqlStringLiteral(operationId);

        return $@"
                let operationId = '{escapedOperationId}';
                union
                (
                    traces
                    | where operation_Id == operationId
                    | project timestamp, itemType, severityLevel, message, exceptionType = '', stackTrace = ''
                ),
                (
                    exceptions
                    | where operation_Id == operationId
                    | extend stackFrames = extract_all(@'""method"":""([^""]+)""', tostring(details))
                    | project
                        timestamp,
                        itemType,
                        severityLevel = 3,
                        message = coalesce(outerMessage, innermostMessage, problemId, type),
                        exceptionType = coalesce(type, innermostType),
                        stackTrace = iff(array_length(stackFrames) > 0, strcat_array(stackFrames, '\n'), iff(isnotempty(innermostMethod), strcat('at ', innermostMethod), ''))
                )
                | where isnotempty(message)
                | summarize arg_max(timestamp, severityLevel, exceptionType, stackTrace) by message, itemType
                | project timestamp, itemType, severityLevel, message, exceptionType, stackTrace
                | order by timestamp asc";
    }

    internal static string EscapeKqlStringLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    internal static string GetLogLevel(int? severityLevel, string? exceptionType) => severityLevel switch
    {
        0 => "Verbose",
        1 => "Information",
        2 => "Warning",
        3 => "Error",
        4 => "Critical",
        null when !string.IsNullOrWhiteSpace(exceptionType) => "Error",
        _ => "Information"
    };

    private static DateTimeOffset? GetOptionalDateTimeOffset(LogsTableRow row, ISet<string> columnNames, string columnName) =>
        columnNames.Contains(columnName) ? row.GetDateTimeOffset(columnName) : null;

    private static int? GetOptionalInt32(LogsTableRow row, ISet<string> columnNames, string columnName) =>
        columnNames.Contains(columnName) ? row.GetInt32(columnName) : null;

    private static string? GetOptionalString(LogsTableRow row, ISet<string> columnNames, string columnName) =>
        columnNames.Contains(columnName) ? row.GetString(columnName) : null;

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
