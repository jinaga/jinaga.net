using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public interface IHttpAuthenticationProvider
    {
        Task SetRequestHeaders(HttpRequestHeaders headers);
        Task<bool> Reauthenticate();
    }
}