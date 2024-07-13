using System.Net.Http.Headers;
using System.Threading.Tasks;
using Jinaga.Http;

namespace Jinaga.Notebooks.Authentication;

/// <summary>
/// An authentication provider that uses a cookie to authenticate with an Express server.
/// </summary>
public class ExpressAuthenticationHandler : IHttpAuthenticationProvider
{
    public readonly string cookie;

    /// <summary>
    /// Create an authentication provider that uses the connect.sid cookie.
    /// </summary>
    /// <param name="cookie">The value of the connect.sid cookie</param>
    public ExpressAuthenticationHandler(string cookie)
    {
        this.cookie = cookie;
    }

    /// <summary>
    /// Set the Cookie header to include the connect.sid cookie.
    /// </summary>
    /// <param name="headers">The headers of the HTTP request</param>
    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
        headers.Add("Cookie", $"connect.sid={cookie}");
    }

    /// <summary>
    /// Called when the server responds with a 401 Unauthorized. This provider
    /// does not support re-authentication.
    /// </summary>
    /// <returns>True if the provider was able to reauthenticate</returns>
    public Task<JinagaAuthenticationState> Reauthenticate()
    {
        return Task.FromResult(JinagaAuthenticationState.NotAuthenticated);
    }
}