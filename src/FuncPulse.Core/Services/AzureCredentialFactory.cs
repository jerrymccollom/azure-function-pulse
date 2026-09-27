using Azure.Core;
using Azure.Identity;

namespace FuncPulse.Core.Services;

/// <summary>
/// Factory for creating Azure credentials optimized for the runtime environment.
/// </summary>
public static class AzureCredentialFactory
{
    /// <summary>
    /// Determines if the application is running in Azure (App Service, Container Apps, Functions, etc.)
    /// by checking for Azure-specific environment variables.
    /// </summary>
    public static bool IsRunningInAzure()
    {
        // Check for Azure App Service / Functions
        var websiteInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        
        // Check for Managed Identity endpoints
        var identityEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        var msiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
        
        return !string.IsNullOrEmpty(websiteInstanceId) 
               || !string.IsNullOrEmpty(identityEndpoint) 
               || !string.IsNullOrEmpty(msiEndpoint);
    }
    
    /// <summary>
    /// Creates the appropriate Azure credential based on the runtime environment.
    /// - Local development: Uses AzureCliCredential (skips slow managed identity probe)
    /// - Azure hosted: Uses DefaultAzureCredential (includes managed identity)
    /// </summary>
    public static TokenCredential CreateCredential()
    {
        if (IsRunningInAzure())
        {
            // In Azure: use full DefaultAzureCredential chain (includes Managed Identity)
            return new DefaultAzureCredential();
        }
        else
        {
            // Local development: use AzureCliCredential only (skips 30+ second IMDS timeout)
            return new AzureCliCredential();
        }
    }
    
    /// <summary>
    /// Gets a description of which credential will be used.
    /// </summary>
    public static string GetCredentialDescription()
    {
        if (IsRunningInAzure())
        {
            return "DefaultAzureCredential (Azure hosted - includes Managed Identity)";
        }
        else
        {
            return "AzureCliCredential (local development - skips managed identity probe)";
        }
    }
}
