using System;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public interface IHttpConnection
    {
        Task<TResponse> Get<TResponse>(string path);
        Task GetStream<T>(string path, Func<T, Task> onResponse, Action<Exception> onError, System.Threading.CancellationToken cancellationToken);
        Task PostJson<TRequest>(string path, TRequest request);
        Task<TResponse> PostJsonExpectingJson<TRequest, TResponse>(string path, TRequest request);
        Task<TResponse> PostStringExpectingJson<TResponse>(string path, string request);
    }
}