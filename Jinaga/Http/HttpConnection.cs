using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class HttpConnection : IHttpConnection
    {
        private HttpClient httpClient;

        public HttpConnection(Uri baseUrl, string token)
        {
            this.httpClient = new HttpClient();

            if (!baseUrl.AbsoluteUri.EndsWith("/"))
            {
                baseUrl = new Uri(baseUrl.AbsoluteUri + "/");
            }
            httpClient.BaseAddress = baseUrl;
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public Task<TResponse> Get<TResponse>(string path)
        {
            return WithHttpClient(async httpClient =>
            {
                using var httpResponse = await httpClient.GetAsync(path);
                httpResponse.EnsureSuccessStatusCode();
                string body = await httpResponse.Content.ReadAsStringAsync();
                var response = MessageSerializer.Deserialize<TResponse>(body);
                return response;
            });
        }

        public Task PostJson<TRequest>(string path, TRequest request)
        {
            return WithHttpClient(async httpClient =>
            {
                var body = MessageSerializer.Serialize(request);
                using var httpResponse = await httpClient.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));
                return httpResponse.EnsureSuccessStatusCode();
            });
        }

        public Task<TResponse> PostJsonExpectingJson<TRequest, TResponse>(string path, TRequest request)
        {
            return WithHttpClient(async httpClient =>
            {
                string json = MessageSerializer.Serialize(request);
                using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using var httpResponse = await httpClient.PostAsync(path, content);
                string body = await httpResponse.Content.ReadAsStringAsync();
                var response = MessageSerializer.Deserialize<TResponse>(body);
                return response;
            });
        }

        public Task<TResponse> PostStringExpectingJson<TResponse>(string path, string request)
        {
            return WithHttpClient(async httpClient =>
            {
                using var httpResponse = await httpClient.PostAsync(path, new StringContent(request));
                httpResponse.EnsureSuccessStatusCode();
                string body = await httpResponse.Content.ReadAsStringAsync();
                var response = MessageSerializer.Deserialize<TResponse>(body);
                return response;
            });
        }

        private async Task<T> WithHttpClient<T>(Func<HttpClient, Task<T>> func)
        {
            try
            {
                return await func(httpClient);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"\tERROR {0}", ex.Message);
                throw;
            }
        }
    }
}
