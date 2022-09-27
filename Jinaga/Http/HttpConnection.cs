using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;

namespace Jinaga.Http
{
    public class HttpConnection : IHttpConnection
    {
        private Uri baseUrl;
        private string token;

        public HttpConnection(Uri baseUrl, string token)
        {
            this.baseUrl = baseUrl;
            this.token = token;
        }

        public Task<TResponse> Get<TResponse>(string path)
        {
            return WithHttpClient(async httpClient =>
            {
                using var httpResponse = await httpClient.GetAsync(path);
                string body = await httpResponse.Content.ReadAsStringAsync();
                var response = MessageSerializer.Deserialize<TResponse>(body);
                return response;
            });
        }

        public Task Post<TRequest>(string path, TRequest request)
        {
            return WithHttpClient(async httpClient =>
            {
                var body = MessageSerializer.Serialize(request);
                using var httpResponse = await httpClient.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));
                return httpResponse.EnsureSuccessStatusCode();
            });
        }

        public Task<TResponse> PostExpectJson<TRequest, TResponse>(string path, TRequest request)
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

        private T WithHttpClient<T>(Func<HttpClient, T> func)
        {
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = baseUrl;
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return func(httpClient);
        }
    }
}
