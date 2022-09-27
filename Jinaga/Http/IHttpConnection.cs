using System.Threading.Tasks;

namespace Jinaga.Http
{
    public interface IHttpConnection
    {
        Task<TResponse> Get<TResponse>(string path);
        Task Post<TRequest>(string path, TRequest request);
        Task<TResponse> PostExpectJson<TRequest, TResponse>(string path, TRequest request);
    }
}