using System;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class WebClient
    {
        private readonly IHttpConnection httpConnection;

        public WebClient(IHttpConnection httpConnection)
        {
            this.httpConnection = httpConnection;
        }

        public Task<LoginResponse> Login()
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
    }
}
