namespace FuncPulse.Core.Models;

public enum TimeRange
{
    Hours24,
    Days7,
    Days30
}

public static class TimeRangeExtensions
{
    public static TimeSpan ToTimeSpan(this TimeRange range) => range switch
    {
        TimeRange.Hours24 => TimeSpan.FromHours(24),
        TimeRange.Days7 => TimeSpan.FromDays(7),
        TimeRange.Days30 => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(24)
    };

    public static string ToMetricInterval(this TimeRange range) => range switch
    {
        TimeRange.Hours24 => "PT1H",
        TimeRange.Days7 => "P1D",
        TimeRange.Days30 => "P1D",
        _ => "PT1H"
    };

    public static string ToDisplayString(this TimeRange range) => range switch
    {
        TimeRange.Hours24 => "24 Hours",
        TimeRange.Days7 => "7 Days",
        TimeRange.Days30 => "30 Days",
        _ => "24 Hours"
    };
}
