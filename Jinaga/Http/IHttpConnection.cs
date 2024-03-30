using System;
using System.Threading.Tasks;
using Jinaga.Facts;

namespace Jinaga.Http
{
    public interface IHttpConnection
    {
        Task<TResponse> Get<TResponse>(string path);
        Task GetStream<T>(string path, Func<T, Task> onResponse, Action<Exception> onError, System.Threading.CancellationToken cancellationToken);
        Task PostJson<TRequest>(string path, TRequest request);
        Task<FactGraph> PostLoad(string path, LoadRequest request);
        Task<TResponse> PostStringExpectingJson<TResponse>(string path, string request);
    }
}