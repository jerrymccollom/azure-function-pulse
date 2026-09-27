using FuncPulse.Core.Services;
using Azure.Identity;

namespace FuncPulse.Tests;

public class AzureCredentialFactoryTests
{
    [Fact]
    public void IsRunningInAzure_WithNoAzureEnvVars_ReturnsFalse()
    {
        // Clear any Azure environment variables for test isolation
        var originalWebsiteInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        var originalIdentityEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        var originalMsiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
        
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
            Environment.SetEnvironmentVariable("MSI_ENDPOINT", null);
            
            var result = AzureCredentialFactory.IsRunningInAzure();
            
            Assert.False(result);
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", originalWebsiteInstanceId);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", originalIdentityEndpoint);
            Environment.SetEnvironmentVariable("MSI_ENDPOINT", originalMsiEndpoint);
        }
    }
    
    [Theory]
    [InlineData("WEBSITE_INSTANCE_ID", "some-instance-id")]
    [InlineData("IDENTITY_ENDPOINT", "http://169.254.169.254/")]
    [InlineData("MSI_ENDPOINT", "http://169.254.169.254/")]
    public void IsRunningInAzure_WithAzureEnvVar_ReturnsTrue(string envVarName, string value)
    {
        var original = Environment.GetEnvironmentVariable(envVarName);
        
        try
        {
            Environment.SetEnvironmentVariable(envVarName, value);
            
            var result = AzureCredentialFactory.IsRunningInAzure();
            
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, original);
        }
    }
    
    [Fact]
    public void CreateCredential_LocalEnvironment_ReturnsAzureCliCredential()
    {
        // Ensure we're in "local" mode
        var originalWebsiteInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        var originalIdentityEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        var originalMsiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
        
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
            Environment.SetEnvironmentVariable("MSI_ENDPOINT", null);
            
            var credential = AzureCredentialFactory.CreateCredential();
            
            Assert.IsType<AzureCliCredential>(credential);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", originalWebsiteInstanceId);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", originalIdentityEndpoint);
            Environment.SetEnvironmentVariable("MSI_ENDPOINT", originalMsiEndpoint);
        }
    }
    
    [Fact]
    public void CreateCredential_AzureEnvironment_ReturnsDefaultAzureCredential()
    {
        var original = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "test-instance");
            
            var credential = AzureCredentialFactory.CreateCredential();
            
            Assert.IsType<DefaultAzureCredential>(credential);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original);
        }
    }
    
    [Fact]
    public void GetCredentialDescription_LocalEnvironment_ReturnsAzureCliDescription()
    {
        var originalWebsiteInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        var originalIdentityEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        var originalMsiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
        
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
            Environment.SetEnvironmentVariable("MSI_ENDPOINT", null);
            
            var description = AzureCredentialFactory.GetCredentialDescription();
            
            Assert.Contains("AzureCliCredential", description);
            Assert.Contains("local development", description);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", originalWebsiteInstanceId);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", originalIdentityEndpoint);
            Environment.SetEnvironmentVariable("MSI_ENDPOINT", originalMsiEndpoint);
        }
    }
    
    [Fact]
    public void GetCredentialDescription_AzureEnvironment_ReturnsDefaultCredentialDescription()
    {
        var original = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        
        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "test-instance");
            
            var description = AzureCredentialFactory.GetCredentialDescription();
            
            Assert.Contains("DefaultAzureCredential", description);
            Assert.Contains("Azure hosted", description);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original);
        }
    }
}
