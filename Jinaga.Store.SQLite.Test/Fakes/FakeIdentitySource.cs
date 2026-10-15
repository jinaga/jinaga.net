using Jinaga.Http;
using System.Net.Http.Headers;

namespace Jinaga.Store.SQLite.Test.Fakes;

/// <summary>
/// An authentication provider that also reports who is logged in. A client
/// configured with one of these records the current user as a saver of the
/// facts it queues, and sends a queued fact only while one of its savers is current.
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
