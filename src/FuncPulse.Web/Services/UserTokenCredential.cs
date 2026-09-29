using Azure.Core;
using System.Security.Claims;

namespace FuncPulse.Web.Services;

/// <summary>
/// TokenCredential implementation that uses a user's access token obtained from Azure AD authentication.
/// </summary>
public class UserTokenCredential : TokenCredential
{
    private readonly string _accessToken;
    private readonly DateTimeOffset _expiresOn;

    public UserTokenCredential(string accessToken, DateTimeOffset expiresOn)
    {
        _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        _expiresOn = expiresOn;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken(_accessToken, _expiresOn);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(new AccessToken(_accessToken, _expiresOn));
    }
}
