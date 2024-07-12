using System.Text.Json;

namespace Jinaga.Maui.Authentication;

public class SecureTokenStorage : ITokenStorage
{
    public const string PublicKeyKey = "Jinaga.PublicKey";
    public const string AuthenticationTokenKey = "Jinaga.AuthenticationToken";

    public async Task<AuthenticationResult> LoadTokenAndUser()
    {
        string? tokenJson = await SecureStorage.GetAsync(AuthenticationTokenKey).ConfigureAwait(false);
        string? publicKey = await SecureStorage.GetAsync(PublicKeyKey).ConfigureAwait(false);
        if (tokenJson == null || publicKey == null)
        {
            return new AuthenticationResult(null, null);
        }

        var authenticationToken = JsonSerializer.Deserialize<AuthenticationToken>(tokenJson);
        var user = new User(publicKey);
        return new AuthenticationResult(authenticationToken, user);
    }

    public async Task SaveTokenAndUser(AuthenticationResult result)
    {
        if (result.Token == null)
        {
            SecureStorage.Remove(AuthenticationTokenKey);
        }
        else
        {
            string tokenJson = JsonSerializer.Serialize(result.Token);
            await SecureStorage.SetAsync(AuthenticationTokenKey, tokenJson).ConfigureAwait(false);
        }
        if (result.User == null)
        {
            SecureStorage.Remove(PublicKeyKey);
        }
        else
        {
            await SecureStorage.SetAsync(PublicKeyKey, result.User.publicKey).ConfigureAwait(false);
        }
    }
}
