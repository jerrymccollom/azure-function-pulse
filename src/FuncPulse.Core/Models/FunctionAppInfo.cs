namespace FuncPulse.Core.Models;

public class FunctionAppInfo
{
    public required string ResourceId { get; set; }
    public required string Name { get; set; }
    public required string Location { get; set; }
    public string? AppInsightsConnectionString { get; set; }
    public string? AppInsightsResourceId { get; set; }
    public List<FunctionInfo> Functions { get; set; } = new();
}

public class FunctionInfo
{
    public required string Name { get; set; }
    public required string TriggerType { get; set; }
    public FunctionMetrics? Metrics { get; set; }
    public List<FunctionInvocation> RecentInvocations { get; set; } = new();
}

public class FunctionMetrics
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int TotalCount => SuccessCount + FailureCount;
    public double FailureRate => TotalCount > 0 ? (double)FailureCount / TotalCount * 100 : 0;
    public List<MetricDataPoint> TimeSeries { get; set; } = new();
    
    public HealthStatus GetHealthStatus()
    {
        if (TotalCount == 0) return HealthStatus.Unknown;
        if (FailureRate < 1) return HealthStatus.Healthy;
        if (FailureRate < 5) return HealthStatus.Warning;
        return HealthStatus.Critical;
    }
}

public class MetricDataPoint
{
    public DateTime Timestamp { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

public enum HealthStatus
{
    Unknown,
    Healthy,
    Warning,
    Critical
}

public class FunctionInvocation
{
    public required string OperationId { get; set; }
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ResultCode { get; set; }
    public string? ExceptionMessage { get; set; }
}

public class InvocationLog
{
    public required string OperationId { get; set; }
    public DateTime Timestamp { get; set; }
    public List<LogEntry> Entries { get; set; } = new();
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
}
