using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Web;

namespace FuncPulse.Web.Services;

/// <summary>
/// Factory for creating Azure credentials optimized for the runtime environment and user context.
/// Extends the core credential factory with support for user-delegated access in hosted scenarios.
/// </summary>
public interface IAzureCredentialService
{
    /// <summary>
    /// Creates the appropriate Azure credential based on the runtime environment and user authentication state.
    /// - Local development: Uses AzureCliCredential
    /// - Azure hosted with authenticated user: Uses user's delegated token
    /// - Azure hosted without auth: Uses DefaultAzureCredential (Managed Identity)
    /// </summary>
    Task<TokenCredential> CreateCredentialAsync();

    /// <summary>
    /// Gets a description of which credential is being used.
    /// </summary>
    Task<string> GetCredentialDescriptionAsync();

    /// <summary>
    /// Gets whether the current user is authenticated via Azure AD.
    /// </summary>
    bool IsUserAuthenticated { get; }

    /// <summary>
    /// Gets the authenticated user's display name (email).
    /// </summary>
    string? UserDisplayName { get; }
}

public class AzureCredentialService : IAzureCredentialService
{
    private readonly IUserCredentialProvider _userCredentialProvider;
    private readonly ILogger<AzureCredentialService> _logger;

    // Azure Management API scope for user-delegated access
    private static readonly string[] AzureManagementScopes = new[] 
    { 
        "https://management.azure.com/.default" 
    };

    public AzureCredentialService(
        IUserCredentialProvider userCredentialProvider,
        ILogger<AzureCredentialService> logger)
    {
        _userCredentialProvider = userCredentialProvider;
        _logger = logger;
    }

    public bool IsUserAuthenticated => _userCredentialProvider.IsUserAuthenticated;

    public string? UserDisplayName => _userCredentialProvider.UserPrincipalName;

    public async Task<TokenCredential> CreateCredentialAsync()
    {
        // If user is authenticated in hosted scenario, use their delegated token
        if (FuncPulse.Core.Services.AzureCredentialFactory.IsRunningInAzure() 
            && _userCredentialProvider.IsUserAuthenticated)
        {
            _logger.LogInformation("Creating user-delegated credential for {User}", UserDisplayName);
            return await _userCredentialProvider.GetCredentialAsync();
        }

        // Otherwise use the default factory logic (CLI for local, DefaultAzureCredential for Azure)
        _logger.LogDebug("Using default credential factory");
        return FuncPulse.Core.Services.AzureCredentialFactory.CreateCredential();
    }

    public async Task<string> GetCredentialDescriptionAsync()
    {
        if (FuncPulse.Core.Services.AzureCredentialFactory.IsRunningInAzure() 
            && _userCredentialProvider.IsUserAuthenticated)
        {
            return $"User-delegated token for {UserDisplayName}";
        }

        return FuncPulse.Core.Services.AzureCredentialFactory.GetCredentialDescription();
    }
}

/// <summary>
/// Fallback implementation of <see cref="IAzureCredentialService"/> used when Azure AD sign-in
/// is not configured (e.g. running locally or in demo mode). There is no authenticated user in
/// this scenario, so credentials always come from the default factory (Azure CLI/Managed Identity).
/// </summary>
public class DefaultAzureCredentialService : IAzureCredentialService
{
    public bool IsUserAuthenticated => false;

    public string? UserDisplayName => null;

    public Task<TokenCredential> CreateCredentialAsync()
        => Task.FromResult(FuncPulse.Core.Services.AzureCredentialFactory.CreateCredential());

    public Task<string> GetCredentialDescriptionAsync()
        => Task.FromResult(FuncPulse.Core.Services.AzureCredentialFactory.GetCredentialDescription());
}
