using System.Threading.Tasks;

namespace Jinaga.Http
{
    public interface IHttpConnection
    {
        Task<TResponse> get<TResponse>(string path);
        Task<TResponse> post<TRequest, TResponse>(string path, TRequest request);
    }
}