using System.Net.Http.Headers;
using Jinaga.Http;

namespace Jinaga.Maui.Authentication;

/// <summary>
/// Stands in for the authentication service while the service is being
/// constructed, because the service needs the Jinaga client and the client
/// needs an authentication provider.
///
/// The proxy implements <see cref="IIdentitySource"/> whether or not the
/// provider it ends up wrapping does. The client reads that interface once,
/// when it is constructed, which is before <see cref="SetProvider"/> has been
/// called; a proxy that advertised the interface only after the fact would be
/// seen as not having it.
/// </summary>
public class AuthenticationProviderProxy : IHttpAuthenticationProvider, IIdentitySource
{
    private IHttpAuthenticationProvider? provider;
    private IIdentitySource? identitySource;

    /// <summary>
    /// Raised when the wrapped provider reports a different user.
    /// </summary>
    public event IIdentitySource.CurrentUserChanged? OnCurrentUserChanged;

    /// <summary>
    /// The user the wrapped provider reports, or null if it reports none or
    /// does not know who is logged in.
    /// </summary>
    public User? CurrentUser => identitySource?.CurrentUser;

    public void SetProvider(IHttpAuthenticationProvider provider)
    {
        if (identitySource != null)
        {
            identitySource.OnCurrentUserChanged -= CurrentUserChanged;
        }
        this.provider = provider;
        identitySource = provider as IIdentitySource;
        if (identitySource != null)
        {
            identitySource.OnCurrentUserChanged += CurrentUserChanged;
        }
    }

    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
        if (provider == null)
        {
            throw new Exception("No authentication provider is set.");
        }
        provider.SetRequestHeaders(headers);
    }

    public Task<JinagaAuthenticationState> Reauthenticate()
    {
        if (provider == null)
        {
            throw new Exception("No authentication provider is set.");
        }
        return provider.Reauthenticate();
    }

    private void CurrentUserChanged(User? user)
    {
        OnCurrentUserChanged?.Invoke(user);
    }
}
