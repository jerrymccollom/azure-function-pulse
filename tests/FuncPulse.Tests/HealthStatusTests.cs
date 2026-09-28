using FuncPulse.Core.Models;

namespace FuncPulse.Tests;

public class HealthStatusTests
{
    [Fact]
    public void GetHealthStatus_WithInvocations_ReturnsUnknownWhenNoInvocations()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 10,
            FailureCount = 5
        };

        var status = metrics.GetHealthStatus(new List<FunctionInvocation>());

        Assert.Equal(HealthStatus.Unknown, status);
    }

    [Fact]
    public void GetHealthStatus_WithInvocations_ReturnsCriticalWhenLatestFailed()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 10,
            FailureCount = 1
        };

        var invocations = new List<FunctionInvocation>
        {
            new()
            {
                OperationId = "op-1",
                Timestamp = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(100),
                Success = false,
                ResultCode = "500"
            },
            new()
            {
                OperationId = "op-2",
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Duration = TimeSpan.FromMilliseconds(100),
                Success = true,
                ResultCode = "200"
            }
        };

        var status = metrics.GetHealthStatus(invocations);

        Assert.Equal(HealthStatus.Critical, status);
    }

    [Fact]
    public void GetHealthStatus_WithInvocations_ReturnsHealthyWhenLatestSucceededAndNoRecentFailures()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 20,
            FailureCount = 0
        };

        var invocations = new List<FunctionInvocation>();
        for (int i = 0; i < 20; i++)
        {
            invocations.Add(new FunctionInvocation
            {
                OperationId = $"op-{i}",
                Timestamp = DateTime.UtcNow.AddMinutes(-i * 5),
                Duration = TimeSpan.FromMilliseconds(100),
                Success = true,
                ResultCode = "200"
            });
        }

        var status = metrics.GetHealthStatus(invocations);

        Assert.Equal(HealthStatus.Healthy, status);
    }

    [Fact]
    public void GetHealthStatus_WithInvocations_ReturnsWarningWhenLatestSucceededButRecentFailures()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 18,
            FailureCount = 2
        };

        var invocations = new List<FunctionInvocation>
        {
            new()
            {
                OperationId = "op-1",
                Timestamp = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(100),
                Success = true,
                ResultCode = "200"
            },
            new()
            {
                OperationId = "op-2",
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Duration = TimeSpan.FromMilliseconds(100),
                Success = true,
                ResultCode = "200"
            },
            new()
            {
                OperationId = "op-3",
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                Duration = TimeSpan.FromMilliseconds(100),
                Success = false,
                ResultCode = "500"
            }
        };

        var status = metrics.GetHealthStatus(invocations);

        Assert.Equal(HealthStatus.Warning, status);
    }

    [Fact]
    public void GetHealthStatus_WithInvocations_IgnoresOldFailuresBeyond20Invocations()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 25,
            FailureCount = 5
        };

        var invocations = new List<FunctionInvocation>();
        
        for (int i = 0; i < 20; i++)
        {
            invocations.Add(new FunctionInvocation
            {
                OperationId = $"op-{i}",
                Timestamp = DateTime.UtcNow.AddMinutes(-i * 5),
                Duration = TimeSpan.FromMilliseconds(100),
                Success = true,
                ResultCode = "200"
            });
        }

        for (int i = 20; i < 30; i++)
        {
            invocations.Add(new FunctionInvocation
            {
                OperationId = $"op-{i}",
                Timestamp = DateTime.UtcNow.AddMinutes(-i * 5),
                Duration = TimeSpan.FromMilliseconds(100),
                Success = false,
                ResultCode = "500"
            });
        }

        var status = metrics.GetHealthStatus(invocations);

        Assert.Equal(HealthStatus.Healthy, status);
    }

    [Fact]
    public void GetHealthStatus_WithoutInvocations_FallsBackToAggregateLogic()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 95,
            FailureCount = 5
        };

        var status = metrics.GetHealthStatus();

        Assert.Equal(HealthStatus.Critical, status);
    }

    [Fact]
    public void GetHealthStatus_WithoutInvocations_ReturnsHealthyForLowFailureRate()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 100,
            FailureCount = 0
        };

        var status = metrics.GetHealthStatus();

        Assert.Equal(HealthStatus.Healthy, status);
    }

    [Fact]
    public void GetHealthStatus_WithoutInvocations_ReturnsCriticalForHighFailureRate()
    {
        var metrics = new FunctionMetrics
        {
            SuccessCount = 90,
            FailureCount = 10
        };

        var status = metrics.GetHealthStatus();

        Assert.Equal(HealthStatus.Critical, status);
    }
}
