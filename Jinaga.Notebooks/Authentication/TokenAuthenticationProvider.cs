using System.Net.Http.Headers;
using System.Threading.Tasks;
using Jinaga.Http;

namespace Jinaga.Notebooks.Authentication;

/// <summary>
/// An authentication provider that uses a bearer token to authenticate.
/// </summary>
class TokenAuthenticationProvider : IHttpAuthenticationProvider
{
    private string token;

    /// <summary>
    /// Create an authentication provider that uses a bearer token.
    /// </summary>
    /// <param name="token">The bearer token (without the word "Bearer")</param>
    public TokenAuthenticationProvider(string token)
    {
        this.token = token;
    }

    /// <summary>
    /// Set the Authorization header to include the bearer token.
    /// </summary>
    /// <param name="headers">The headers of the HTTP request</param>
    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
        headers.Add("Authorization", $"Bearer {token}");
    }

    /// <summary>
    /// Called when the server responds with a 401 Unauthorized. This provider
    /// does not support reauthentication.
    /// </summary>
    /// <returns>True if the provider was able to reauthenticate</returns>
    public Task<bool> Reauthenticate()
    {
        return Task.FromResult(false);
    }
}