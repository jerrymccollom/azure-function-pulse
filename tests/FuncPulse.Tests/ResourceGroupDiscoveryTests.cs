using FuncPulse.Core.Models;
using FuncPulse.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FuncPulse.Tests;

public class ResourceGroupDiscoveryTests
{
    [Fact]
    public async Task ListResourceGroupsAsync_InDemoMode_ReturnsThreeDemoGroups()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<FunctionDiscoveryService>.Instance;
        var service = new FunctionDiscoveryService(settings, logger);

        // Act
        var resourceGroups = await service.ListResourceGroupsAsync("demo-sub");

        // Assert
        Assert.NotNull(resourceGroups);
        Assert.Equal(3, resourceGroups.Count);
        Assert.Contains("demo-rg-east", resourceGroups);
        Assert.Contains("demo-rg-west", resourceGroups);
        Assert.Contains("demo-rg-central", resourceGroups);
    }

    [Fact]
    public async Task ListResourceGroupsAsync_InDemoMode_ReturnsResourceGroups()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<FunctionDiscoveryService>.Instance;
        var service = new FunctionDiscoveryService(settings, logger);

        // Act
        var resourceGroups = await service.ListResourceGroupsAsync("demo-sub");

        // Assert
        Assert.NotNull(resourceGroups);
        Assert.Equal(3, resourceGroups.Count);
        // Demo mode returns unsorted list
        Assert.Contains("demo-rg-east", resourceGroups);
        Assert.Contains("demo-rg-west", resourceGroups);
        Assert.Contains("demo-rg-central", resourceGroups);
    }

    [Fact]
    public async Task DiscoverFunctionAppsAsync_InDemoMode_ReturnsThreeApps()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<FunctionDiscoveryService>.Instance;
        var service = new FunctionDiscoveryService(settings, logger);

        // Act
        var apps = await service.DiscoverFunctionAppsAsync("demo-sub", "demo-rg");

        // Assert
        Assert.NotNull(apps);
        Assert.Equal(3, apps.Count);
        Assert.Contains(apps, a => a.Name == "order-processor");
        Assert.Contains(apps, a => a.Name == "data-sync");
        Assert.Contains(apps, a => a.Name == "reporting");
    }

    [Fact]
    public async Task DiscoverFunctionAppsAsync_InDemoMode_PopulatesAppInsightsResourceId()
    {
        // Arrange
        var settings = Options.Create(new AzureSettings 
        { 
            DemoMode = true
        });
        var logger = NullLogger<FunctionDiscoveryService>.Instance;
        var service = new FunctionDiscoveryService(settings, logger);

        // Act
        var apps = await service.DiscoverFunctionAppsAsync("demo-sub", "demo-rg");

        // Assert
        Assert.NotNull(apps);
        Assert.All(apps, app =>
        {
            Assert.NotNull(app.AppInsightsResourceId);
            Assert.Contains("/providers/microsoft.insights/components/", app.AppInsightsResourceId);
        });
    }
}
