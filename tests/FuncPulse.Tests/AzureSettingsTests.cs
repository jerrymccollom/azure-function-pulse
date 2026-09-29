using FuncPulse.Core.Models;

namespace FuncPulse.Tests;

public class AzureSettingsTests
{
    [Fact]
    public void AzureSettings_ContainsOnlyApplicationLevelAzureOptions()
    {
        var propertyNames = typeof(AzureSettings)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(new[] { "DemoMode", "OperationTimeoutSeconds" }, propertyNames);
    }
}
