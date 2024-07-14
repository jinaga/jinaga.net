using System.Globalization;
using System.Net.Http.Headers;
using Jinaga.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Authentication;

namespace Jinaga.Maui.Authentication;

/// <summary>
/// Provides authentication services, including token management and user authentication.
/// </summary>
public class AuthenticationService : IHttpAuthenticationProvider, IAuthenticationService
{
    private readonly ITokenStorage tokenStorage;
    private readonly UserProvider userProvider;
    private readonly IWebAuthenticator webAuthenticator;
    private readonly OAuthClient oauthClient;
    private readonly JinagaClient jinagaClient;
    private readonly Func<JinagaClient, User, UserProfile, Task> updateUserName;
    private readonly ILogger<AuthenticationService> logger;

    private readonly object stateLock = new();
    private bool initialized;
    private AuthenticationResult authenticationState = AuthenticationResult.Empty;

    /// <summary>
    /// Initializes the authentication service.
    /// </summary>
    /// <param name="tokenStorage">Storage for authentication tokens and public keys</param>
    /// <param name="userProvider">Provides the logged in user to the rest of the application</param>
    /// <param name="webAuthenticator">Initiates the user interface for logging in</param>
    /// <param name="oauthClient">Calls OAuth2 endpoints on the server</param>
    /// <param name="jinagaClient">Fetches the logged-in user fact from the server</param>
    /// <param name="authenticationSettings">Application-provided settings for authentication</param>
    /// <param name="logger">Log activities</param>
    /// <param name="authenticationProviderProxy">Proxies this authentication service to support API calls</param>
    public AuthenticationService(ITokenStorage tokenStorage, UserProvider userProvider, IWebAuthenticator webAuthenticator, OAuthClient oauthClient, JinagaClient jinagaClient, AuthenticationSettings authenticationSettings, ILogger<AuthenticationService> logger, AuthenticationProviderProxy authenticationProviderProxy)
    {
        this.tokenStorage = tokenStorage;
        this.userProvider = userProvider;
        this.webAuthenticator = webAuthenticator;
        this.oauthClient = oauthClient;
        this.jinagaClient = jinagaClient;
        this.logger = logger;
        updateUserName = authenticationSettings.UpdateUserName;

        authenticationProviderProxy.SetProvider(this);
    }

    /// <summary>
    /// Initializes the authentication service. Call at application startup.
    /// </summary>
    /// <returns>True if the user is authenticated</returns>
    public async Task<bool> Initialize()
    {
        lock (stateLock)
        {
            // If called a second time, return true if logged in.
            if (initialized)
            {
                return authenticationState.Token != null;
            }
            initialized = true;
        }

        try
        {
            var loaded = await tokenStorage.LoadTokenAndUser().ConfigureAwait(false);
            if (loaded.Token == null || loaded.User == null)
            {
                // No persisted token, so we are logged out.
                logger.LogInformation("Initialized authentication service with no token");
                return false;
            }

            // Log whether the token is expired.
            if (IsExpired(loaded.Token))
            {
                logger.LogInformation("Initialized authentication service with expired token");
            }
            else
            {
                logger.LogInformation("Initialized authentication service with valid token");
            }

            lock (stateLock)
            {
                authenticationState = loaded;
            }
            userProvider.SetUser(loaded.User);
            return true;
        }
        catch (Exception ex)
        {
            await tokenStorage.SaveTokenAndUser(AuthenticationResult.Empty).ConfigureAwait(false);
            logger.LogError(ex, "Failed to initialize authentication service");
            return false;
        }
    }

    /// <summary>
    /// Log the user in.
    /// </summary>
    /// <param name="provider">The identifier of the authentication provider to use</param>
    /// <returns>True if the user successfully authenticated</returns>
    public async Task<bool> LogIn(string provider)
    {
        lock (stateLock)
        {
            if (authenticationState.Token != null)
            {
                // Already logged in.
                return true;
            }
        }

        try
        {
            logger.LogInformation("Logging in");
            var result = await Authenticate(provider).ConfigureAwait(false);
            if (result.Token == null || result.User == null)
            {
                // Failed to log in.
                logger.LogInformation("Failed to log in");
                return false;
            }

            lock (stateLock)
            {
                authenticationState = result;
            }
            userProvider.SetUser(result.User);
            await tokenStorage.SaveTokenAndUser(result).ConfigureAwait(false);
            logger.LogInformation("Logged in");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log in");
            return false;
        }
    }

    /// <summary>
    /// Log the user out.
    /// </summary>
    /// <returns>Resolves when logout is successful</returns>
    public async Task LogOut()
    {
        var cachedAuthenticationToken = authenticationState.Token;

        if (cachedAuthenticationToken == null)
        {
            // Already logged out.
            return;
        }

        await RevokeToken(cachedAuthenticationToken).ConfigureAwait(false);
        lock (stateLock)
        {
            authenticationState = AuthenticationResult.Empty;
        }
        userProvider.ClearUser();
        await tokenStorage.SaveTokenAndUser(AuthenticationResult.Empty).ConfigureAwait(false);
        logger.LogInformation("Logged out");
    }

    /// <summary>
    /// Set the authentication token in the request headers.
    /// </summary>
    /// <param name="headers">HTTP headers to modify</param>
    public void SetRequestHeaders(HttpRequestHeaders headers)
    {
        var cachedAuthenticationToken= authenticationState.Token;
        if (cachedAuthenticationToken != null)
        {
            headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedAuthenticationToken.AccessToken);
        }
    }

    /// <summary>
    /// Reauthenticate the user. Called when a request fails with a 401 Unauthorized status.
    /// </summary>
    /// <returns>True if the token was successfully refreshed</returns>
    public async Task<JinagaAuthenticationState> Reauthenticate()
    {
        var cachedAuthenticationToken = authenticationState.Token;
        if (cachedAuthenticationToken == null)
        {
            // Not logged in.
            return JinagaAuthenticationState.NotAuthenticated;
        }

        try
        {
            var refreshedToken = await RefreshToken(cachedAuthenticationToken).ConfigureAwait(false);
            if (refreshedToken == null)
            {
                // Failed to refresh token.
                AuthenticationResult authenticationResult;
                lock (stateLock)
                {
                    authenticationResult = new AuthenticationResult(AuthenticationResult.Empty.Token, authenticationState.User);
                    authenticationState = authenticationResult;
                }
                await tokenStorage.SaveTokenAndUser(authenticationResult).ConfigureAwait(false);
                logger.LogInformation("Token refresh failed. Cleared authentication token.");
                return JinagaAuthenticationState.AuthenticationExpired;
            }
            else
            {
                // Refreshed token.
                AuthenticationResult authenticationResult;
                lock (stateLock)
                {
                    authenticationResult = new AuthenticationResult(refreshedToken, authenticationState.User);
                    authenticationState = authenticationResult;
                }
                await tokenStorage.SaveTokenAndUser(authenticationResult).ConfigureAwait(false);
                logger.LogInformation("Token refresh succeeded.");
                return JinagaAuthenticationState.Authenticated;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while refreshing token.");
            return JinagaAuthenticationState.NotAuthenticated;
        }
    }

    private async Task<AuthenticationResult> Authenticate(string provider)
    {
        string requestUrl = oauthClient.BuildRequestUrl(provider);
        var authResult = await webAuthenticator.AuthenticateAsync(
            new Uri(requestUrl),
            new Uri(oauthClient.CallbackUrl)).ConfigureAwait(false);
        if (authResult == null)
        {
            return AuthenticationResult.Empty;
        }

        if (!authResult.Properties.TryGetValue("state", out string? state) ||
            !authResult.Properties.TryGetValue("code", out string? code))
        {
            logger.LogError("Authentication result did not contain expected properties.");
            return AuthenticationResult.Empty;
        }

        oauthClient.ValidateState(state);
        var tokenResponse = await oauthClient.GetTokenResponse(code).ConfigureAwait(false);
        var authenticationToken = ResponseToToken(tokenResponse);
        lock (stateLock)
        {
            // Set the authentication token so that it can be used to get the user
            authenticationState = new AuthenticationResult(authenticationToken, null);
        }
        var (user, profile) = await jinagaClient.Login().ConfigureAwait(false);
        await updateUserName(jinagaClient, user, profile);
        return new AuthenticationResult(authenticationToken, user);
    }

    private async Task<AuthenticationToken?> RefreshToken(AuthenticationToken authenticationToken)
    {
        var response = await oauthClient.RequestNewToken(authenticationToken.RefreshToken).ConfigureAwait(false);
        if (response == null)
        {
            return null;
        }
        var token = ResponseToToken(response);
        return token;
    }

    private async Task RevokeToken(AuthenticationToken cachedAuthenticationToken)
    {
        try
        {
            await oauthClient.RevokeToken(cachedAuthenticationToken.AccessToken, cachedAuthenticationToken.RefreshToken).ConfigureAwait(false);
            logger.LogInformation("Revoked token");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revoke token");
        }
    }

    private static bool IsExpired(AuthenticationToken token)
    {
        if (DateTime.TryParse(token.ExpiryDate, null, DateTimeStyles.RoundtripKind, out var expiryDate))
        {
            return DateTime.UtcNow > expiryDate.AddMinutes(-5);
        }
        return true;
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