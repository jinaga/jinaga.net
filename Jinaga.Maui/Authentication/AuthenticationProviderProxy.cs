using System.Net.Http.Headers;
using Jinaga.Http;

namespace Jinaga.Maui.Authentication;

public class AuthenticationProviderProxy : IHttpAuthenticationProvider
{
    private IHttpAuthenticationProvider? provider;

    public void SetProvider(IHttpAuthenticationProvider provider)
    {
        this.provider = provider;
    }

    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
        if (provider == null)
        {
            throw new Exception("No authentication provider is set.");
        }
        provider.SetRequestHeaders(headers);
    }

    public Task<bool> Reauthenticate()
    {
        if (provider == null)
        {
            throw new Exception("No authentication provider is set.");
        }
        return provider.Reauthenticate();
    }
}