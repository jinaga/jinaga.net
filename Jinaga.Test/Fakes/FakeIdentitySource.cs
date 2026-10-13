#nullable enable

using Jinaga.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// An authentication provider that also reports who is logged in. A client
    /// configured with one of these records the current user as the author of the
    /// facts it queues, and sends a queued fact only while its author is current.
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

        public void SetRequestHeaders(HttpRequestHeaders headers)
        {
        }

        public Task<JinagaAuthenticationState> Reauthenticate()
        {
            return Task.FromResult(JinagaAuthenticationState.NotAuthenticated);
        }
    }

    /// <summary>
    /// An authentication provider that does not report who is logged in, which is
    /// every provider written before the queue could route by author.
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
