using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;

namespace Jinaga.Http
{
    public interface IHttpConnection
    {
        Task<TResponse> Get<TResponse>(string path);
        Task GetStream<T>(string path, Func<T, Task> onResponse, Action<Exception> onError, System.Threading.CancellationToken cancellationToken);
        Task PostJson<TRequest>(string path, TRequest request);
        Task PostGraph(string path, FactGraph graph);

        /// <summary>
        /// Posts the graph, calling <paramref name="beforeEachAttempt"/> before every attempt to
        /// send it, including a retry. The callback stops the post by throwing. By default it is
        /// called once, before the post.
        /// </summary>
        Task PostGraph(string path, FactGraph graph, Action beforeEachAttempt)
        {
            beforeEachAttempt();
            return PostGraph(path, graph);
        }

        /// <summary>
        /// Posts the request as JSON, calling <paramref name="beforeEachAttempt"/> before every
        /// attempt to send it, including a retry. The callback stops the post by throwing. By
        /// default it is called once, before the post.
        /// </summary>
        Task PostJson<TRequest>(string path, TRequest request, Action beforeEachAttempt)
        {
            beforeEachAttempt();
            return PostJson(path, request);
        }
        Task<FactGraph> PostLoad(string path, LoadRequest request);
        Task<TResponse> PostStringExpectingJson<TResponse>(string path, string request);
        Task<ImmutableList<string>> GetAcceptedContentTypes(string path);
    }
}