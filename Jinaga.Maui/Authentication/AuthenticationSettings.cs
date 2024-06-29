using System.Collections.Immutable;

namespace Jinaga.Maui.Authentication;
public class AuthenticationSettings
{
    public AuthenticationSettings(ImmutableDictionary<string, string> authUrlByProvider, string accessTokenUrl, string callbackUrl, string clientId, string scope, Func<JinagaClient, User, UserProfile, Task> updateUserName)
    {
        AuthUrlByProvider = authUrlByProvider;
        AccessTokenUrl = accessTokenUrl;
        CallbackUrl = callbackUrl;
        ClientId = clientId;
        Scope = scope;
        UpdateUserName = updateUserName;
    }

    public ImmutableDictionary<string, string> AuthUrlByProvider { get; }
    public string AccessTokenUrl { get; }
    public string CallbackUrl { get; }
    public string ClientId { get; }
    public string Scope { get; }
    public Func<JinagaClient, User, UserProfile, Task> UpdateUserName { get; }
}
