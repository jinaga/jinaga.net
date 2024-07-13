using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public interface IHttpAuthenticationProvider
    {
        void SetRequestHeaders(HttpRequestHeaders headers);
        Task<JinagaAuthenticationState> Reauthenticate();
    }
}