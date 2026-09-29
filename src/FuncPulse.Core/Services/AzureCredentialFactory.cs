using Azure.Core;
using Azure.Identity;
using System.Diagnostics;

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
    /// Gets the currently selected subscription from the local Azure CLI context.
    /// </summary>
    public static async Task<string> GetCurrentSubscriptionIdAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "az",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("az");
        }
        startInfo.ArgumentList.Add("account");
        startInfo.ArgumentList.Add("show");
        startInfo.ArgumentList.Add("--query");
        startInfo.ArgumentList.Add("id");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add("tsv");

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the Azure CLI. Ensure 'az' is installed and available on PATH.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        var subscriptionId = (await outputTask).Trim();
        if (process.ExitCode != 0 || string.IsNullOrEmpty(subscriptionId))
        {
            var error = (await errorTask).Trim();
            throw new InvalidOperationException(
                string.IsNullOrEmpty(error)
                    ? "No Azure subscription is selected. Run 'az login' and select a subscription with 'az account set'."
                    : $"Unable to get the selected Azure subscription. Run 'az login' and select a subscription with 'az account set'. {error}");
        }

        return subscriptionId;
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
