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
                    new() { Name = "my-app/MyFunction", TriggerType = "httpTrigger" }
                }
            }
        };

        state.Capture("sub-123", "rg-a", TimeRange.Days7, new[] { "rg-a", "rg-b" }, apps);

        var restored = state.TryRestore("sub-123", out var snapshot);

        Assert.True(restored);
        Assert.Equal("rg-a", snapshot.ResourceGroup);
        Assert.Equal(TimeRange.Days7, snapshot.SelectedTimeRange);
        Assert.Equal(new[] { "rg-a", "rg-b" }, snapshot.AvailableResourceGroups);
        Assert.Same(apps[0], snapshot.FunctionApps.Single());
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
}
