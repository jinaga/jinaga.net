using Jinaga.Http;
using System.Net.Http.Headers;

namespace Jinaga.Maui.Test.Fakes;

/// <summary>
/// An authentication provider that also reports who is logged in, the way the MAUI
/// authentication service does.
/// </summary>
internal class FakeIdentitySource : IHttpAuthenticationProvider, IIdentitySource
{
    public event IIdentitySource.CurrentUserChanged? OnCurrentUserChanged;

    public User? CurrentUser { get; private set; }

    /// <summary>
    /// Logs a user in, or logs everybody out when given null.
    /// </summary>
    public void SetCurrentUser(User? user)
    {
        CurrentUser = user;
        OnCurrentUserChanged?.Invoke(user);
    }

    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
    }

    public Task<JinagaAuthenticationState> Reauthenticate()
    {
        return Task.FromResult(JinagaAuthenticationState.NotAuthenticated);
    }
}

/// <summary>
/// An authentication provider that does not report who is logged in.
/// </summary>
internal class FakeAuthenticationProviderWithoutIdentity : IHttpAuthenticationProvider
{
    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
    }

    public Task<JinagaAuthenticationState> Reauthenticate()
    {
        return Task.FromResult(JinagaAuthenticationState.NotAuthenticated);
    }
}
