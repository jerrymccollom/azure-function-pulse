using FuncPulse.Core.Models;
using FuncPulse.Web.Services;

namespace FuncPulse.Tests;

public class DashboardStateTests
{
    [Fact]
    public void Capture_StoresDashboardSnapshotForMatchingSubscription()
    {
        var state = new DashboardState();
        var apps = new List<FunctionAppInfo>
        {
            new()
            {
                ResourceId = "resource-id",
                Name = "my-app",
                Location = "eastus",
                AppInsightsResourceId = "app-insights-id",
                Functions = new List<FunctionInfo>
                {
                    new()
                    {
                        Name = "my-app/MyFunction",
                        TriggerType = "httpTrigger",
                        Metrics = new FunctionMetrics
                        {
                            SuccessCount = 3,
                            FailureCount = 1,
                            TimeSeries =
                            [
                                new MetricDataPoint
                                {
                                    Timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                                    SuccessCount = 3,
                                    FailureCount = 1
                                }
                            ]
                        },
                        RecentInvocations =
                        [
                            new FunctionInvocation
                            {
                                OperationId = "op-1",
                                Timestamp = new DateTime(2026, 1, 1, 12, 5, 0, DateTimeKind.Utc),
                                Duration = TimeSpan.FromMilliseconds(125),
                                Success = true,
                                ResultCode = "200"
                            }
                        ]
                    }
                }
            }
        };

        state.Capture("sub-123", "rg-a", TimeRange.Days7, new[] { "rg-a", "rg-b" }, apps);

        apps[0].Functions[0].Name = "mutated";
        apps[0].Functions[0].Metrics!.SuccessCount = 99;
        apps[0].Functions[0].RecentInvocations[0].OperationId = "mutated-op";

        var restored = state.TryRestore("sub-123", out var snapshot);

        Assert.True(restored);
        Assert.Equal("rg-a", snapshot.ResourceGroup);
        Assert.Equal(TimeRange.Days7, snapshot.SelectedTimeRange);
        Assert.Equal(new[] { "rg-a", "rg-b" }, snapshot.AvailableResourceGroups);
        var restoredApp = snapshot.FunctionApps.Single();
        var restoredFunction = restoredApp.Functions.Single();
        Assert.NotSame(apps[0], restoredApp);
        Assert.Equal("my-app/MyFunction", restoredFunction.Name);
        Assert.Equal(3, restoredFunction.Metrics!.SuccessCount);
        Assert.Equal("op-1", restoredFunction.RecentInvocations.Single().OperationId);
        Assert.True(snapshot.ShouldPromptForRefresh);
    }

    [Fact]
    public void TryRestore_ReturnsFalseForDifferentSubscription()
    {
        var state = new DashboardState();

        state.Capture("sub-123", "rg-a", TimeRange.Hours24, Array.Empty<string>(), Array.Empty<FunctionAppInfo>());

        var restored = state.TryRestore("other-sub", out _);

        Assert.False(restored);
    }

    [Fact]
    public void BuildUri_IncludesEncodedAppInsightsResourceId()
    {
        var app = new FunctionAppInfo
        {
            ResourceId = "resource-id",
            Name = "my app",
            Location = "eastus",
            AppInsightsResourceId = "/subscriptions/demo/resourceGroups/demo/providers/microsoft.insights/components/my-ai",
            Functions = new List<FunctionInfo>()
        };
        var function = new FunctionInfo
        {
            Name = "my-app/My Function",
            TriggerType = "httpTrigger"
        };

        var uri = FunctionDetailsNavigation.BuildUri(app, function);

        Assert.Equal(
            "/function/my%20app/my-app%2FMy%20Function?appInsightsResourceId=%2Fsubscriptions%2Fdemo%2FresourceGroups%2Fdemo%2Fproviders%2Fmicrosoft.insights%2Fcomponents%2Fmy-ai",
            uri);
    }

    [Fact]
    public void FormatDateTime_ThrowsForUnspecifiedValues()
    {
        var state = new DashboardState();

        var act = () => state.FormatDateTime(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified));

        Assert.Throws<ArgumentException>(act);
    }
}
