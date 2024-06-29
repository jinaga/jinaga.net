namespace Jinaga.Maui.Authentication;
public class AuthenticationService
{
    private const string PublicKeyKey = "Jinaga.PublicKey";

    private readonly OAuth2HttpAuthenticationProvider authenticationProvider;
    private readonly JinagaClient jinagaClient;
    private readonly Func<JinagaClient, User, UserProfile, Task> updateUserName;

    private volatile User? user;

    private readonly SemaphoreSlim semaphore = new(1);

    public AuthenticationService(OAuth2HttpAuthenticationProvider authenticationProvider, JinagaClient jinagaClient, AuthenticationSettings authenticationSettings)
    {
        this.authenticationProvider = authenticationProvider;
        this.jinagaClient = jinagaClient;
        updateUserName = authenticationSettings.UpdateUserName;
    }

    public async Task<User?> Initialize()
    {
        authenticationProvider.Initialize();
        await LoadUser().ConfigureAwait(false);
        return await GetUser(jinagaClient).ConfigureAwait(false);
    }

    public async Task<User?> Login(string provider)
    {
        var loggedIn = await authenticationProvider.Login(provider).ConfigureAwait(false);
        if (!loggedIn)
        {
            return null;
        }
        return await GetUser(jinagaClient).ConfigureAwait(false);
    }

    public async Task LogOut()
    {
        await authenticationProvider.LogOut().ConfigureAwait(false);
        await ClearUser().ConfigureAwait(false);
    }

    private Task<User?> GetUser(JinagaClient jinagaClient)
    {
        return Lock(async () =>
        {
            if (!authenticationProvider.IsLoggedIn)
            {
                return null;
            }

            if (user == null)
            {
                // Get the logged in user.
                var (user, profile) = await jinagaClient.Login().ConfigureAwait(false);

                if (user != null)
                {
                    this.user = user;
                    await SaveUser().ConfigureAwait(false);

                    await updateUserName(jinagaClient, user, profile).ConfigureAwait(false);
                }
            }
            return user;
        });
    }

    private Task<bool> ClearUser()
    {
        return Lock(async () =>
        {
            user = null;
            await SaveUser().ConfigureAwait(false);
            return true;
        });
    }

    private async Task LoadUser()
    {
        string? publicKey = await SecureStorage.GetAsync(PublicKeyKey).ConfigureAwait(false);
        if (publicKey != null)
        {
            user = new User(publicKey);
        }
    }

    private async Task SaveUser()
    {
        if (user == null)
        {
            SecureStorage.Remove(PublicKeyKey);
        }
        else
        {
            await SecureStorage.SetAsync(PublicKeyKey, user.publicKey).ConfigureAwait(false);
        }
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
}
