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
        Assert.Contains("stackTrace = tostring(details)", query);
        Assert.Contains("| where isnotempty(message)", query);
    }

    [Fact]
    public void BuildInvocationLogsQuery_EscapesOperationId()
    {
        var query = LogsService.BuildInvocationLogsQuery("operation-'123");

        Assert.Contains("let operationId = 'operation-''123';", query);
    }
}
