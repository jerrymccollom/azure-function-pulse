using FuncPulse.Core.Services;

namespace FuncPulse.Tests;

public class LogsServiceTests
{
    [Fact]
    public void BuildInvocationLogsQuery_UsesTraceAndExceptionSources()
    {
        var query = LogsService.BuildInvocationLogsQuery("operation-123");

        Assert.Contains("traces", query);
        Assert.Contains("exceptions", query);
        Assert.DoesNotContain("requests", query);
        Assert.Contains("message = coalesce(outerMessage, innermostMessage, problemId, type)", query);
        Assert.Contains("extend stackFrames = extract_all", query);
        Assert.Contains("stackTrace = iff(array_length(stackFrames) > 0", query);
        Assert.Contains("| where isnotempty(message)", query);
    }

    [Fact]
    public void BuildInvocationLogsQuery_EscapesOperationId()
    {
        var query = LogsService.BuildInvocationLogsQuery("operation-'123");

        Assert.Contains("let operationId = 'operation-''123';", query);
    }

    [Theory]
    [InlineData(0, null, "Verbose")]
    [InlineData(1, null, "Information")]
    [InlineData(2, null, "Warning")]
    [InlineData(3, null, "Error")]
    [InlineData(4, null, "Critical")]
    [InlineData(null, null, "Information")]
    [InlineData(null, "System.InvalidOperationException", "Error")]
    public void GetLogLevel_HandlesMissingSeverityLevel(int? severityLevel, string? exceptionType, string expectedLevel)
    {
        var level = LogsService.GetLogLevel(severityLevel, exceptionType);

        Assert.Equal(expectedLevel, level);
    }
}
