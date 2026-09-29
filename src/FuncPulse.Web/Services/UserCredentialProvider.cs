using Azure.Core;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace FuncPulse.Web.Services;

/// <summary>
/// Provides Azure credentials based on the current user's authentication state.
/// When running in Azure App Service with an authenticated user, uses the user's delegated token.
/// Otherwise, falls back to local/managed identity credentials.
/// </summary>
public interface IUserCredentialProvider
{
    /// <summary>
    /// Gets whether the current user is authenticated via Azure AD.
    /// </summary>
    bool IsUserAuthenticated { get; }

    /// <summary>
    /// Gets the authenticated user's principal name (email).
    /// </summary>
    string? UserPrincipalName { get; }

    /// <summary>
    /// Creates an Azure credential for the current context (user-delegated or default).
    /// </summary>
    Task<TokenCredential> GetCredentialAsync();
}

public class UserCredentialProvider : IUserCredentialProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<UserCredentialProvider> _logger;

    // Azure Management API scope for user-delegated access
    private static readonly string[] AzureManagementScopes = new[] 
    { 
        "https://management.azure.com/.default" 
    };

    public UserCredentialProvider(
        IHttpContextAccessor httpContextAccessor,
        ITokenAcquisition tokenAcquisition,
        ILogger<UserCredentialProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenAcquisition = tokenAcquisition;
        _logger = logger;
    }

    public bool IsUserAuthenticated
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true;
        }
    }

    public string? UserPrincipalName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            return user.FindFirst(ClaimTypes.Name)?.Value 
                ?? user.FindFirst("preferred_username")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value;
        }
    }

    public async Task<TokenCredential> GetCredentialAsync()
    {
        if (!IsUserAuthenticated)
        {
            _logger.LogDebug("No authenticated user, using default credential factory");
            return FuncPulse.Core.Services.AzureCredentialFactory.CreateCredential();
        }

        try
        {
            _logger.LogInformation("Getting user-delegated token for Azure Management API");
            
            // Get an access token for Azure Management on behalf of the signed-in user
            var authResult = await _tokenAcquisition.GetAuthenticationResultForUserAsync(
                AzureManagementScopes);

            _logger.LogInformation("Successfully acquired user-delegated token for {User}", UserPrincipalName);
            
            return new UserTokenCredential(authResult.AccessToken, authResult.ExpiresOn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user-delegated token, falling back to default credential");
            return FuncPulse.Core.Services.AzureCredentialFactory.CreateCredential();
        }
    }
}
