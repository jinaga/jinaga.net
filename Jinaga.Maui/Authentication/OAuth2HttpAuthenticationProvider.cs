using Jinaga.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Jinaga.Maui.Authentication;

public class OAuth2HttpAuthenticationProvider : IHttpAuthenticationProvider
{
    private const string AuthenticationTokenKey = "Jinaga.AuthenticationToken";

    private readonly IWebAuthenticator webAuthenticator;
    private readonly OAuthClient oauthClient;
    private readonly ILogger<OAuth2HttpAuthenticationProvider> logger;

    private readonly SemaphoreSlim semaphore = new(1);
    volatile private AuthenticationToken authenticationToken;

    internal bool IsLoggedIn => authenticationToken != null;

    public OAuth2HttpAuthenticationProvider(IWebAuthenticator webAuthenticator, OAuthClient oauthClient, ILogger<OAuth2HttpAuthenticationProvider> logger)
    {
        this.webAuthenticator = webAuthenticator;
        this.oauthClient = oauthClient;
        this.logger = logger;
    }

    internal void Initialize()
    {
        var task = Lock(async () =>
        {
            logger.LogInformation("Initializing authentication");
            await LoadToken().ConfigureAwait(false);
            if (authenticationToken != null)
            {
                // Check for token expiration
                if (DateTime.TryParse(authenticationToken.ExpiryDate, null, DateTimeStyles.RoundtripKind, out var expiryDate))
                {
                    if (DateTime.UtcNow > expiryDate.AddMinutes(-5))
                    {
                        var stopwatch = Stopwatch.StartNew();
                        logger.LogInformation("Refreshing token");

                        await RefreshToken().ConfigureAwait(false);
                        await SaveToken().ConfigureAwait(false);

                        logger.LogInformation("Token refreshed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogInformation("Cached token is still valid");
                    }
                }
            }
            else
            {
                logger.LogInformation("No cached token");
            }
            return true;
        });
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger.LogError(t.Exception, "Failed to initialize authentication");
            }
        });
    }

    internal Task<bool> Login(string provider)
    {
        return Lock(async () =>
        {
            logger.LogInformation("Logging in with {Provider}", provider);

            var client = oauthClient;
            string requestUrl = client.BuildRequestUrl(provider);
            var authResult = await webAuthenticator.AuthenticateAsync(
                new Uri(requestUrl),
                new Uri(client.CallbackUrl)).ConfigureAwait(false);
            if (authResult == null)
            {
                logger.LogInformation("Authentication cancelled");
                return false;
            }

            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Received authentication result");

            try
            {
                string state = authResult.Properties["state"];
                string code = authResult.Properties["code"];

                client.ValidateState(state);
                var tokenResponse = await client.GetTokenResponse(code).ConfigureAwait(false);
                authenticationToken = ResponseToToken(tokenResponse);
                await SaveToken().ConfigureAwait(false);
                logger.LogInformation("Token received in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get token");
                return false;
            }
        });
    }

    internal Task LogOut()
    {
        return Lock(async () =>
        {
            logger.LogInformation("Logging out");
            authenticationToken = null;
            await SaveToken().ConfigureAwait(false);
            return true;
        });
    }

    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
        var cachedAuthenticationToken = authenticationToken;
        if (cachedAuthenticationToken != null)
        {
            headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedAuthenticationToken.AccessToken);
        }
    }

    public Task<bool> Reauthenticate()
    {
        return Lock(async () =>
        {
            if (authenticationToken != null)
            {
                await RefreshToken().ConfigureAwait(false);
                await SaveToken().ConfigureAwait(false);
                return true;
            }
            return false;
        });
    }

    private async Task LoadToken()
    {
        string tokenJson = await SecureStorage.GetAsync(AuthenticationTokenKey).ConfigureAwait(false);
        if (tokenJson != null)
        {
            AuthenticationToken authenticationToken = JsonSerializer.Deserialize<AuthenticationToken>(tokenJson);
            this.authenticationToken = authenticationToken;
        }
    }

    private async Task SaveToken()
    {
        if (authenticationToken == null)
        {
            SecureStorage.Remove(AuthenticationTokenKey);
        }
        else
        {
            string tokenJson = JsonSerializer.Serialize(authenticationToken);
            await SecureStorage.SetAsync(AuthenticationTokenKey, tokenJson).ConfigureAwait(false);
        }
    }

    private async Task RefreshToken()
    {
        if (authenticationToken == null)
        {
            throw new InvalidOperationException("Attempting to refresh with no token");
        }

        var tokenResponse = await oauthClient.RequestNewToken(authenticationToken.RefreshToken).ConfigureAwait(false);
        authenticationToken = tokenResponse == null ? null : ResponseToToken(tokenResponse);
    }

    private async Task<T> Lock<T>(Func<Task<T>> action)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static AuthenticationToken ResponseToToken(TokenResponse tokenResponse)
    {
        return new AuthenticationToken
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiryDate = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                .ToString("O", CultureInfo.InvariantCulture)
        };
    }
}