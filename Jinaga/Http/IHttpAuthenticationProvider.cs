using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    /// <summary>
    /// Adds authentication details to HTTP requests.
    /// </summary>
    public interface IHttpAuthenticationProvider
    {
        /// <summary>
        /// Set the authentication token in the request headers.
        /// </summary>
        /// <param name="headers">HTTP headers to modify</param>
        void SetRequestHeaders(HttpRequestHeaders headers);
        /// <summary>
        /// Reauthenticate the user. Called when a request fails with a 401 Unauthorized status.
        /// </summary>
        /// <returns>True if the token was successfully refreshed</returns>
        Task<JinagaAuthenticationState> Reauthenticate();
    }
}