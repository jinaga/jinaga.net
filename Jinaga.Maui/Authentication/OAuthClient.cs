using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jinaga.Maui.Authentication;

public class OAuthClient
{
    private readonly ImmutableDictionary<string, string> authUrlByProvider;
    private readonly string accessTokenUrl;
    private readonly string callbackUrl;
    private readonly string clientId;
    private readonly string scope;

    private readonly IHttpClientFactory httpClientFactory;

    private string codeVerifier = "";
    private string state = "";

    public string CallbackUrl => callbackUrl;

    public OAuthClient(AuthenticationSettings authenticationSettings, IHttpClientFactory httpClientFactory)
    {
        authUrlByProvider = authenticationSettings.AuthUrlByProvider;
        accessTokenUrl = authenticationSettings.AccessTokenUrl;
        callbackUrl = authenticationSettings.CallbackUrl;
        clientId = authenticationSettings.ClientId;
        scope = authenticationSettings.Scope;
        this.httpClientFactory = httpClientFactory;
    }

    public string BuildRequestUrl(string provider)
    {
        if (accessTokenUrl == null || clientId == null)
        {
            throw new Exception("Create a file called Settings.Local.cs and set the AccessTokenUrl and ClientId properties."
                + " Do not check Settings.Local.cs into source control.");
        }

        if (!authUrlByProvider.TryGetValue(provider, out var authUrl))
        {
            throw new Exception($"No authentication URL for provider {provider}");
        }

        // Generate random strings for the code verifier and state
        codeVerifier = GenerateRandomString();
        state = GenerateRandomString();

        // Hash the code verifier
        var codeVerifierBytes = Encoding.UTF8.GetBytes(codeVerifier);
        var codeChallengeBytes = SHA256.HashData(codeVerifierBytes);
        var codeChallenge = UrlSafeBase64String(codeChallengeBytes);

        // Build the authorization URL
        var builder = new UriBuilder(authUrl);
        var urlEncodedCallbackUrl = WebUtility.UrlEncode(callbackUrl);
        var urlEncodedScope = WebUtility.UrlEncode(scope);
        builder.Query = $"response_type=code&client_id={clientId}&redirect_uri={urlEncodedCallbackUrl}&scope={urlEncodedScope}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256";
        return builder.ToString();
    }

    public void ValidateState(string receivedState)
    {
        if (receivedState != state)
        {
            throw new Exception("Failed cross-site request forgery check");
        }
    }

    public async Task<TokenResponse> GetTokenResponse(string code)
    {
        if (accessTokenUrl == null || clientId == null)
            throw new Exception("Create a file called Settings.Local.cs and set the AccessTokenUrl and ClientId properties."
                + " Do not check Settings.Local.cs into source control.");

        // Build the access token request
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, accessTokenUrl);
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", clientId },
            { "redirect_uri", callbackUrl },
            { "code_verifier", codeVerifier },
            { "code", code }
        });

        // Send the access token request
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var tokenResponse = await client.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get token response: {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}");
        }
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        // Get the access token
        var token = JsonSerializer.Deserialize<TokenResponse>(tokenContent);
        if (token == null)
        {
            throw new Exception("Unable to parse token response");
        }

        return token;
    }

    public async Task<TokenResponse> RequestNewToken(string refreshToken)
    {
        if (accessTokenUrl == null || clientId == null)
            throw new Exception("Create a file called Settings.Local.cs and set the AccessTokenUrl and ClientId properties."
                + " Do not check Settings.Local.cs into source control.");

        // Build the refresh request
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, accessTokenUrl);
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>{
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "client_id", clientId }
        });

        // Send the refresh request
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var tokenResponse = await client.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            if (tokenResponse.StatusCode == HttpStatusCode.Unauthorized || tokenResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }
            throw new Exception($"Failed to refresh the token: {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}");
        }
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

        // Get the access token
        var token = JsonSerializer.Deserialize<TokenResponse>(tokenContent);
        if (token == null)
        {
            throw new Exception("Unable to parse token response");
        }

        return token;
    }

    private static string GenerateRandomString()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return UrlSafeBase64String(randomBytes);
    }

    private static string UrlSafeBase64String(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}
