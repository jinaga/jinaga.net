#nullable enable

using Jinaga.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// An authentication provider that also reports who is logged in. A client
    /// configured with one of these records the current user as a saver of the
    /// facts it queues, and sends a queued fact only while one of its savers is current.
    /// </summary>
    internal class FakeIdentitySource : IHttpAuthenticationProvider, IIdentitySource
    {
        public event IIdentitySource.CurrentUserChanged? OnCurrentUserChanged;

        public User? CurrentUser { get; private set; }

        /// <summary>
        /// Logs a user in, or logs everybody out when given null.
        /// </summary>
        public void SetCurrentUser(User? user)
        {
            CurrentUser = user;
            OnCurrentUserChanged?.Invoke(user);
        }

        /// <summary>
        /// What <see cref="Reauthenticate"/> does. By default it reports that nobody is
        /// authenticated; a test can refresh the token for the same user, or log another
        /// user in, instead.
        /// </summary>
        public Func<Task<JinagaAuthenticationState>> OnReauthenticate { get; set; } =
            () => Task.FromResult(JinagaAuthenticationState.NotAuthenticated);

        public void SetRequestHeaders(HttpRequestHeaders headers)
        {
        }

        public Task<JinagaAuthenticationState> Reauthenticate()
        {
            return OnReauthenticate();
        }
    }

    /// <summary>
    /// An authentication provider that does not report who is logged in, which is
    /// every provider written before the queue could route by who saved each fact.
    /// </summary>
    internal class FakeAuthenticationProviderWithoutIdentity : IHttpAuthenticationProvider
    {
        public void SetRequestHeaders(HttpRequestHeaders headers)
        {
        }

        public Task<JinagaAuthenticationState> Reauthenticate()
        {
            return Task.FromResult(JinagaAuthenticationState.NotAuthenticated);
        }
    }
}
