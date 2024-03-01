using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jinaga.Http
{
    public class HttpConnection : IHttpConnection
    {
        private readonly HttpClient httpClient;
        private readonly ILoggerFactory loggerFactory;
        private readonly Action<HttpRequestHeaders> setRequestHeaders;
        private readonly Func<Task<bool>> reauthenticate;
        private readonly ILogger logger;

        public HttpConnection(Uri baseUrl, ILoggerFactory loggerFactory, Action<HttpRequestHeaders> setRequestHeaders, Func<Task<bool>> reauthenticate)
        {
            this.httpClient = new HttpClient();
            logger = loggerFactory.CreateLogger<HttpConnection>();

            if (!baseUrl.AbsoluteUri.EndsWith("/"))
            {
                baseUrl = new Uri(baseUrl.AbsoluteUri + "/");
            }
            httpClient.BaseAddress = baseUrl;
            this.loggerFactory = loggerFactory;
            this.setRequestHeaders = setRequestHeaders;
            this.reauthenticate = reauthenticate;
        }

        public Task<TResponse> Get<TResponse>(string path)
        {
            return WithHttpClient(() =>
                new HttpRequestMessage(HttpMethod.Get, path),
                async httpResponse =>
                {
                    string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var response = MessageSerializer.Deserialize<TResponse>(body);
                    return response;
                });
        }

        public async void GetStream<T>(string path, Func<T, Task> onResponse, Action<Exception> onError, CancellationToken cancellationToken)
        {
            try
            {
                var observableStream = await GetObservableStream<T>(path, "application/x-jinaga-feed-stream", cancellationToken).ConfigureAwait(false);
                observableStream.Start(async line =>
                {
                    T response = MessageSerializer.Deserialize<T>(line.TrimEnd('\r', '\n'));
                    await onResponse(response);
                }, onError);
            }
            catch (Exception ex)
            {
                onError(ex);
            }
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
                    httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    return httpRequest;
                },
                httpResponse => Task.FromResult(true));
        }

        public Task<TResponse> PostJsonExpectingJson<TRequest, TResponse>(string path, TRequest request)
        {
            return WithHttpClient(() =>
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
                    string json = MessageSerializer.Serialize(request);
                    httpRequest.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
                    httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    return httpRequest;
                },
                async httpResponse =>
                {
                    string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var response = MessageSerializer.Deserialize<TResponse>(body);
                    return response;
                });
        }

        public Task<TResponse> PostStringExpectingJson<TResponse>(string path, string request)
        {
            return WithHttpClient(() =>
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
                httpRequest.Content = new StringContent(request);
                return httpRequest;
            },
            async httpResponse =>
            {
                string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var response = MessageSerializer.Deserialize<TResponse>(body);
                return response;
            });
        }

        private async Task<T> WithHttpClient<T>(
            Func<HttpRequestMessage> createRequest,
            Func<HttpResponseMessage, Task<T>> processResponse)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using var request = createRequest();
                logger.LogInformation("HTTP {method} {baseAddress}{path}", request.Method, httpClient.BaseAddress, request.RequestUri);
                setRequestHeaders(request.Headers);
                using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    logger.LogInformation("HTTP response {statusCode}: Re-authenticating", response.StatusCode);
                    if (await reauthenticate().ConfigureAwait(false))
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HTTP error {message}", ex.Message);
                throw;
            }
        }

        private async Task<T> WithHttpClientStreaming<T>(
            Func<HttpRequestMessage> createRequest,
            Func<HttpResponseMessage, Task<T>> processResponse)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using var request = createRequest();
                logger.LogInformation("HTTP {method} stream {baseAddress}{path}", request.Method, httpClient.BaseAddress, request.RequestUri);
                setRequestHeaders(request.Headers);
                HttpResponseMessage? response = null;
                try
                {
                    response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.Unauthorized ||
                        response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                    {
                        logger.LogInformation("HTTP response {statusCode}: Re-authenticating", response.StatusCode);
                        if (await reauthenticate().ConfigureAwait(false))
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HTTP error {message}", ex.Message);
                throw;
            }
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
                logger.LogInformation("HTTP response {statusCode} after {elapsedMilliseconds} ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
