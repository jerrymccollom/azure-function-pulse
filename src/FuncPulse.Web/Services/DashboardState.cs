using System.Globalization;
using FuncPulse.Core.Models;

namespace FuncPulse.Web.Services;

public enum TimeDisplayMode
{
    Local,
    Utc
}

public sealed class DashboardState
{
    private DashboardSnapshot? _snapshot;
    private TimeDisplayMode _timeDisplayMode = TimeDisplayMode.Local;

    public TimeDisplayMode TimeDisplayMode
    {
        get => _timeDisplayMode;
        set => _timeDisplayMode = value;
    }

    public bool TryRestore(string subscriptionId, out DashboardSnapshot snapshot)
    {
        if (_snapshot == null ||
            !string.Equals(_snapshot.SubscriptionId, subscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            snapshot = default!;
            return false;
        }

        snapshot = _snapshot;
        return true;
    }

    public string FormatDateTime(DateTime dateTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            throw new ArgumentException("Timestamp kind must be Local or Utc.", nameof(dateTime));
        }

        if (_timeDisplayMode == TimeDisplayMode.Local)
        {
            var localTime = dateTime.Kind == DateTimeKind.Utc 
                ? dateTime.ToLocalTime() 
                : dateTime;
            return localTime.ToString(format, CultureInfo.InvariantCulture);
        }
        else
        {
            var utcTime = dateTime.Kind == DateTimeKind.Local 
                ? dateTime.ToUniversalTime() 
                : dateTime;
            return utcTime.ToString(format, CultureInfo.InvariantCulture);
        }
    }

    public string GetTimeZoneLabel()
    {
        return _timeDisplayMode == TimeDisplayMode.Local ? "Local" : "UTC";
    }

    public void Capture(
        string subscriptionId,
        string resourceGroup,
        TimeRange selectedTimeRange,
        IEnumerable<string> availableResourceGroups,
        IEnumerable<FunctionAppInfo> functionApps)
    {
        _snapshot = new DashboardSnapshot(
            subscriptionId,
            resourceGroup,
            selectedTimeRange,
            availableResourceGroups.ToList(),
            functionApps.Select(CloneApp).ToList(),
            ShouldPromptForRefresh: true);
    }

    public void ClearRefreshPrompt()
    {
        if (_snapshot == null)
        {
            return;
        }

        _snapshot = _snapshot with { ShouldPromptForRefresh = false };
    }

    private static FunctionAppInfo CloneApp(FunctionAppInfo app)
    {
        return new FunctionAppInfo
        {
            ResourceId = app.ResourceId,
            Name = app.Name,
            Location = app.Location,
            AppInsightsConnectionString = app.AppInsightsConnectionString,
            AppInsightsResourceId = app.AppInsightsResourceId,
            Functions = app.Functions.Select(CloneFunction).ToList()
        };
    }

    private static FunctionInfo CloneFunction(FunctionInfo function)
    {
        return new FunctionInfo
        {
            Name = function.Name,
            TriggerType = function.TriggerType,
            Metrics = CloneMetrics(function.Metrics),
            RecentInvocations = function.RecentInvocations.Select(CloneInvocation).ToList()
        };
    }

    private static FunctionMetrics? CloneMetrics(FunctionMetrics? metrics)
    {
        if (metrics == null)
        {
            return null;
        }

        return new FunctionMetrics
        {
            SuccessCount = metrics.SuccessCount,
            FailureCount = metrics.FailureCount,
            TimeSeries = metrics.TimeSeries
                .Select(point => new MetricDataPoint
                {
                    Timestamp = point.Timestamp,
                    SuccessCount = point.SuccessCount,
                    FailureCount = point.FailureCount
                })
                .ToList()
        };
    }

    private static FunctionInvocation CloneInvocation(FunctionInvocation invocation)
    {
        return new FunctionInvocation
        {
            OperationId = invocation.OperationId,
            Timestamp = invocation.Timestamp,
            Duration = invocation.Duration,
            Success = invocation.Success,
            ResultCode = invocation.ResultCode,
            ExceptionMessage = invocation.ExceptionMessage
        };
    }
}

public sealed record DashboardSnapshot(
    string SubscriptionId,
    string ResourceGroup,
    TimeRange SelectedTimeRange,
    IReadOnlyList<string> AvailableResourceGroups,
    IReadOnlyList<FunctionAppInfo> FunctionApps,
    bool ShouldPromptForRefresh);
