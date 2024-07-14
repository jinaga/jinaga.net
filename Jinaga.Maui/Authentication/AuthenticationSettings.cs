using System.Collections.Immutable;

namespace Jinaga.Maui.Authentication;
public class AuthenticationSettings
{
    public AuthenticationSettings(ImmutableDictionary<string, string> authUrlByProvider, string accessTokenUrl, string revokeUrl, string callbackUrl, string clientId, string scope, Func<JinagaClient, User, UserProfile, Task> updateUserName)
    {
        AuthUrlByProvider = authUrlByProvider;
        AccessTokenUrl = accessTokenUrl;
        RevokeUrl = revokeUrl;
        CallbackUrl = callbackUrl;
        ClientId = clientId;
        Scope = scope;
        UpdateUserName = updateUserName;
    }

    public ImmutableDictionary<string, string> AuthUrlByProvider { get; }
    public string AccessTokenUrl { get; }
    public string RevokeUrl { get; }
    public string CallbackUrl { get; }
    public string ClientId { get; }
    public string Scope { get; }
    public Func<JinagaClient, User, UserProfile, Task> UpdateUserName { get; }
}
