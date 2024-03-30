using System;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;

namespace Jinaga.Http
{
    public class WebClient
    {
        private readonly IHttpConnection httpConnection;

        public WebClient(IHttpConnection httpConnection)
        {
            this.httpConnection = httpConnection;
        }

        public Task<LoginResponse> Login(CancellationToken cancellationToken)
        {
            return httpConnection.Get<LoginResponse>("login");
        }
        
        public Task Save(SaveRequest saveMessage)
        {
            return httpConnection.PostJson("save", saveMessage);
        }

        public Task<FeedsResponse> Feeds(string request)
        {
            return httpConnection.PostStringExpectingJson<FeedsResponse>("feeds", request);
        }

        public Task<FeedResponse> Feed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            string queryString = bookmark == null ? "" : $"?b={bookmark}";
            return httpConnection.Get<FeedResponse>($"feeds/{feed}{queryString}");
        }

        public async void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<FeedResponse, Task> onResponse, Action<Exception> onError)
        {
            string queryString = bookmark == null ? "" : $"?b={bookmark}";

            try
            {
                await httpConnection.GetStream<FeedResponse>($"feeds/{feed}{queryString}", onResponse, onError, cancellationToken);
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }

        public Task<FactGraph> Load(LoadRequest request, CancellationToken cancellationToken)
        {
            return httpConnection.PostLoad("load", request);
        }
    }
}
