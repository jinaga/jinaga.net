using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Microsoft.Extensions.Logging;

namespace Jinaga.Http
{
    public class HttpConnection : IHttpConnection
    {
        private const string JsonContentType = "application/json";
        private const string JinagaGraphContentType = "application/x-jinaga-graph-v1";
        private const string JinagaFeedStreamContentType = "application/x-jinaga-feed-stream";

        private readonly HttpClient httpClient;
        private readonly ILoggerFactory loggerFactory;
        private readonly Action<HttpRequestHeaders> setRequestHeaders;
        private readonly Func<Task<JinagaAuthenticationState>> reauthenticate;
        private readonly Action<JinagaAuthenticationState> setAuthenticationState;
        private readonly RetryConfiguration retryConfiguration;
        private readonly ILogger logger;

        public HttpConnection(Uri baseUrl, ILoggerFactory loggerFactory, Action<HttpRequestHeaders> setRequestHeaders, Func<Task<JinagaAuthenticationState>> reauthenticate, Action<JinagaAuthenticationState> setAuthenticationState, RetryConfiguration? retryConfiguration = null)
        {
            this.httpClient = new HttpClient();
            this.logger = loggerFactory.CreateLogger<HttpConnection>();
            this.retryConfiguration = retryConfiguration ?? new RetryConfiguration();

            if (!baseUrl.AbsoluteUri.EndsWith("/"))
            {
                baseUrl = new Uri(baseUrl.AbsoluteUri + "/");
            }
            httpClient.BaseAddress = baseUrl;
            this.loggerFactory = loggerFactory;
            this.setRequestHeaders = setRequestHeaders;
            this.reauthenticate = reauthenticate;
            this.setAuthenticationState = setAuthenticationState;
        }

        public Task<TResponse> Get<TResponse>(string path)
        {
            return WithHttpClient(() => {
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, path);
                httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
                return httpRequestMessage;
            },
                async httpResponse =>
                {
                    string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var response = MessageSerializer.Deserialize<TResponse>(body);
                    return response;
                });
        }

        public async Task GetStream<T>(string path, Func<T, Task> onResponse, Action<Exception> onError, CancellationToken cancellationToken)
        {
            var observableStream = await GetObservableStream<T>(path, JinagaFeedStreamContentType, cancellationToken).ConfigureAwait(false);
            await observableStream.Start(async line =>
            {
                T response = MessageSerializer.Deserialize<T>(line.TrimEnd('\r', '\n'));
                await onResponse(response);
            }, onError);
        }

        private Task<ObservableStream<TResponse>> GetObservableStream<TResponse>(string path, string contentType, CancellationToken cancellationToken)
        {
            return WithHttpClientStreaming(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
                return request;
            }, async httpResponse =>
            {
                var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return new ObservableStream<TResponse>(httpResponse, stream, loggerFactory, cancellationToken);
            });
        }

        public Task PostJson<TRequest>(string path, TRequest request)
        {
            return WithHttpClient(() =>
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
                    string body = MessageSerializer.Serialize(request);
                    httpRequest.Content = new StringContent(body, Encoding.UTF8, JsonContentType);
                    httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
                    return httpRequest;
                },
                httpResponse => Task.FromResult(true));
        }

        public Task PostGraph(string path, FactGraph graph)
        {
            return WithHttpClient(() =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);

                var content = new StreamContent(stream =>
                {
                    using var graphSerializer = new GraphSerializer(stream);
                    graphSerializer.Serialize(graph);
                });

                content.Headers.ContentType = new MediaTypeHeaderValue(JinagaGraphContentType);
                httpRequest.Content = content;

                return httpRequest;
            },
            httpResponse => Task.FromResult(true));
        }

        public Task<FactGraph> PostLoad(string path, LoadRequest request)
        {
            return WithHttpClient(() =>
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
                    string json = MessageSerializer.Serialize(request);
                    httpRequest.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
                    httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
                    httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JinagaGraphContentType));
                    httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
                    return httpRequest;
                },
                async httpResponse =>
                {
                    // If the content type is application/x-jinaga-graph-v1, then deserialize the response as a graph.
                    // Otherwise, deserialize it as JSON.
                    if (httpResponse.Content.Headers.ContentType.MediaType == JinagaGraphContentType)
                    {
                        var graphDeserializer = new GraphDeserializer();
                        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        graphDeserializer.Deserialize(stream);
                        return graphDeserializer.Graph;
                    }
                    else
                    {
                        string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var response = MessageSerializer.Deserialize<LoadResponse>(body);
                        var builder = new FactGraphBuilder();
                        foreach (var factRecord in response.Facts)
                        {
                            var fact = FactReader.ReadFact(factRecord);
                            builder.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
                        }

                        FactGraph graph = builder.Build();
                        return graph;
                    }
                });
        }

        public Task<TResponse> PostStringExpectingJson<TResponse>(string path, string request)
        {
            return WithHttpClient(() =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
                httpRequest.Content = new StringContent(request);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
                return httpRequest;
            },
            async httpResponse =>
            {
                string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var response = MessageSerializer.Deserialize<TResponse>(body);
                return response;
            });
        }

        public async Task<ImmutableList<string>> GetAcceptedContentTypes(string path)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using var request = new HttpRequestMessage(HttpMethod.Options, path);
                logger.LogTrace("HTTP {method} {baseAddress}{path}", request.Method, httpClient.BaseAddress, request.RequestUri);
                using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                await CheckForError(response, stopwatch).ConfigureAwait(false);
                var acceptedContentTypes = response.Headers
                    .Where(h => h.Key.ToLowerInvariant() == "accept-post")
                    .SelectMany(h => h.Value)
                    .SelectMany(v => v.Split(','))
                    .Select(v => v.Trim())
                    .ToImmutableList();
                return acceptedContentTypes;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HTTP error {message}", ex.Message);
                throw;
            }
        }

        private async Task<T> ExecuteWithRetry<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            if (!retryConfiguration.Enabled)
            {
                return await operation().ConfigureAwait(false);
            }

            var delay = retryConfiguration.InitialDelay;
            var stopwatch = Stopwatch.StartNew();
            var attempts = 0;

            while (true)
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (
                    (ex.InnerException is System.Net.Sockets.SocketException socketEx &&
                    (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused ||
                     socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset ||
                     socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut)) ||
                    ex.Message.Contains("No connection could be made because the target machine actively refused it"))
                {
                    attempts++;
                    if (stopwatch.Elapsed > retryConfiguration.Timeout)
                    {
                        logger.LogError(ex, "Connection failed after {elapsed:g} and {attempts} attempts", stopwatch.Elapsed, attempts);
                        throw;
                    }

                    logger.LogWarning("Connection attempt {attempts} failed, retrying in {delay:g}", attempts, delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    
                    delay = TimeSpan.FromTicks(Math.Min(
                        (long)(delay.Ticks * retryConfiguration.BackoffMultiplier),
                        retryConfiguration.MaxDelay.Ticks
                    ));
                }
            }
        }

        private async Task<T> WithHttpClient<T>(
            Func<HttpRequestMessage> createRequest,
            Func<HttpResponseMessage, Task<T>> processResponse)
        {
            return await ExecuteWithRetry(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                using var request = createRequest();
                logger.LogTrace("HTTP {method} {baseAddress}{path}", request.Method, httpClient.BaseAddress, request.RequestUri);
                setRequestHeaders(request.Headers);
                using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    logger.LogTrace("HTTP response {statusCode}: Re-authenticating", response.StatusCode);
                    var authenticationState = await reauthenticate().ConfigureAwait(false);
                    setAuthenticationState(authenticationState);
                    if (authenticationState == JinagaAuthenticationState.Authenticated)
                    {
                        using var retryRequest = createRequest();
                        setRequestHeaders(retryRequest.Headers);
                        using var retryResponse = await httpClient.SendAsync(retryRequest).ConfigureAwait(false);
                        await CheckForError(retryResponse, stopwatch).ConfigureAwait(false);
                        var retryResult = await processResponse(retryResponse).ConfigureAwait(false);
                        return retryResult;
                    }
                    else
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
                else
                {
                    await CheckForError(response, stopwatch).ConfigureAwait(false);
                    var result = await processResponse(response).ConfigureAwait(false);
                    return result;
                }
            }).ConfigureAwait(false);
        }

        private async Task<T> WithHttpClientStreaming<T>(
            Func<HttpRequestMessage> createRequest,
            Func<HttpResponseMessage, Task<T>> processResponse)
        {
            return await ExecuteWithRetry(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                using var request = createRequest();
                logger.LogTrace("HTTP {method} stream {baseAddress}{path}", request.Method, httpClient.BaseAddress, request.RequestUri);
                setRequestHeaders(request.Headers);
                HttpResponseMessage? response = null;
                try
                {
                    response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.Unauthorized ||
                        response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                    {
                        logger.LogTrace("HTTP response {statusCode}: Re-authenticating", response.StatusCode);
                        var authenticationState = await reauthenticate().ConfigureAwait(false);
                        setAuthenticationState(authenticationState);
                        if (authenticationState == JinagaAuthenticationState.Authenticated)
                        {
                            using var retryRequest = createRequest();
                            setRequestHeaders(retryRequest.Headers);
                            response.Dispose();
                            response = await httpClient.SendAsync(retryRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                            await CheckForError(response, stopwatch).ConfigureAwait(false);
                            var retryResult = await processResponse(response).ConfigureAwait(false);
                            // We've transferred ownership of the response to the ObservableStream.
                            response = null;
                            return retryResult;
                        }
                        else
                        {
                            throw new UnauthorizedAccessException();
                        }
                    }
                    else
                    {
                        await CheckForError(response, stopwatch).ConfigureAwait(false);
                        var result = await processResponse(response).ConfigureAwait(false);
                        // We've transferred ownership of the response to the ObservableStream.
                        response = null;
                        return result;
                    }
                }
                finally
                {
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
            }).ConfigureAwait(false);
        }

        private async Task CheckForError(HttpResponseMessage response, Stopwatch stopwatch)
        {
            if (!response.IsSuccessStatusCode)
            {
                string body = string.Empty;
                try
                {
                    // Read the content of the error from the response.
                    body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    logger.LogError("HTTP error {statusCode} after {elapsedMilliseconds} ms: {body}", response.StatusCode, stopwatch.ElapsedMilliseconds, body);
                }
                catch
                {
                    // Fall back on the default behavior.
                    logger.LogError("HTTP error {statusCode} after {elapsedMilliseconds} ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
                    response.EnsureSuccessStatusCode();
                }
                throw new HttpRequestException($"Error {response.StatusCode}: {body}");
            }
            else
            {
                logger.LogTrace("HTTP response {statusCode} after {elapsedMilliseconds} ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
