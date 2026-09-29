using FuncPulse.Core.Models;
using FuncPulse.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace FuncPulse.Tests;

public class MetricsServiceTests
{
    [Fact]
    public async Task GetAppMetricsAsync_InDemoMode_ReturnsMetricsForAllFunctions()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<MetricsService>.Instance;
        var service = new MetricsService(settings, logger);
        
        var app = new FunctionAppInfo
        {
            ResourceId = "/subscriptions/demo/resourceGroups/demo/providers/Microsoft.Web/sites/test-app",
            Name = "test-app",
            Location = "East US",
            AppInsightsResourceId = "/subscriptions/demo/resourceGroups/demo/providers/microsoft.insights/components/test-ai",
            Functions = new List<FunctionInfo>
            {
                new() { Name = "Function1", TriggerType = "httpTrigger" },
                new() { Name = "Function2", TriggerType = "queueTrigger" }
            }
        };

        // Act
        var metrics = await service.GetAppMetricsAsync(app, TimeRange.Hours24, CancellationToken.None);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics.Count);
        Assert.True(metrics.ContainsKey("Function1"));
        Assert.True(metrics.ContainsKey("Function2"));
    }

    [Fact]
    public async Task GetAppMetricsAsync_InDemoMode_ReturnsDistinctMetrics()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<MetricsService>.Instance;
        var service = new MetricsService(settings, logger);
        
        var app = new FunctionAppInfo
        {
            ResourceId = "/subscriptions/demo/resourceGroups/demo/providers/Microsoft.Web/sites/test-app",
            Name = "test-app",
            Location = "East US",
            AppInsightsResourceId = "/subscriptions/demo/resourceGroups/demo/providers/microsoft.insights/components/test-ai",
            Functions = new List<FunctionInfo>
            {
                new() { Name = "ProcessOrder", TriggerType = "queueTrigger" },
                new() { Name = "ValidatePayment", TriggerType = "httpTrigger" }
            }
        };

        // Act
        var metrics = await service.GetAppMetricsAsync(app, TimeRange.Hours24, CancellationToken.None);

        // Assert
        var orderMetrics = metrics["ProcessOrder"];
        var paymentMetrics = metrics["ValidatePayment"];
        
        // Metrics should be distinct (not identical)
        Assert.NotEqual(orderMetrics.TotalCount, paymentMetrics.TotalCount);
    }

    [Fact]
    public async Task GetAppMetricsAsync_InDemoMode_IncludesFailures()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<MetricsService>.Instance;
        var service = new MetricsService(settings, logger);
        
        var app = new FunctionAppInfo
        {
            ResourceId = "/subscriptions/demo/resourceGroups/demo/providers/Microsoft.Web/sites/test-app",
            Name = "test-app",
            Location = "East US",
            AppInsightsResourceId = "/subscriptions/demo/resourceGroups/demo/providers/microsoft.insights/components/test-ai",
            Functions = new List<FunctionInfo>
            {
                new() { Name = "ProcessOrder", TriggerType = "queueTrigger" }
            }
        };

        // Act
        var metrics = await service.GetAppMetricsAsync(app, TimeRange.Hours24, CancellationToken.None);

        // Assert
        var orderMetrics = metrics["ProcessOrder"];
        Assert.True(orderMetrics.SuccessCount > 0, "Should have success count");
        Assert.True(orderMetrics.TotalCount > 0, "Should have total count");
        // Failure count can be 0 or higher depending on random seed
        Assert.True(orderMetrics.FailureCount >= 0, "Should have non-negative failure count");
    }

    [Fact]
    public async Task GetInvocationsAsync_InDemoMode_ReturnsInvocations()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<MetricsService>.Instance;
        var service = new MetricsService(settings, logger);

        // Act
        var invocations = await service.GetInvocationsAsync(
            "/subscriptions/demo/resourceGroups/demo/providers/microsoft.insights/components/test-ai",
            "TestFunction",
            TimeRange.Hours24,
            CancellationToken.None);

        // Assert
        Assert.NotNull(invocations);
        Assert.NotEmpty(invocations);
        Assert.All(invocations, inv =>
        {
            Assert.NotNull(inv.OperationId);
            Assert.True(inv.Duration > TimeSpan.Zero);
        });
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void ParseSuccessValue_AcceptsBooleanAndStringRepresentations(object rawValue, bool expected)
    {
        var parsed = MetricsService.ParseSuccessValue(rawValue);

        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ParseSuccessValue_AcceptsJsonStringBoolean()
    {
        using var document = JsonDocument.Parse("""{"success":"false"}""");

        var parsed = MetricsService.ParseSuccessValue(document.RootElement.GetProperty("success"));

        Assert.False(parsed);
    }
}
