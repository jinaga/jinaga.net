using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes;

/// <summary>
/// Serves scripted responses to a <see cref="System.Net.Http.HttpClient"/> so that a test can
/// drive <see cref="Jinaga.Http.HttpConnection"/> through a chosen sequence of status codes
/// without a network or a replicator.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> respond;
    private readonly List<string> requests = new List<string>();

    /// <param name="respond">
    /// Produces the response for a request. The second argument is the zero-based index of the
    /// call, so a script can answer the first attempt differently from the retry that follows it.
    /// </param>
    public FakeHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        this.respond = respond;
    }

    /// <summary>
    /// Every request this handler received, as "METHOD path", in order. The requests themselves
    /// are disposed by the caller, so only their method and path are kept.
    /// </summary>
    public IReadOnlyList<string> Requests => requests;

    /// <summary>
    /// How many times this handler was asked for the given method and path.
    /// </summary>
    public int CountOf(HttpMethod method, string absolutePath) =>
        requests.Count(r => r == Describe(method, absolutePath));

    private static string Describe(HttpMethod method, string absolutePath) => $"{method} {absolutePath}";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int index = requests.Count;
        requests.Add(Describe(request.Method, request.RequestUri!.IsAbsoluteUri
            ? request.RequestUri.AbsolutePath
            : request.RequestUri.OriginalString.Split('?')[0]));
        var response = respond(request, index);
        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}
