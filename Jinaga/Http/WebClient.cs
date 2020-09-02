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
            return httpConnection.get<LoginResponse>("/login");
        }

        public Task<QueryResponse> Query(QueryRequest queryMessage)
        {
            return httpConnection.post<QueryRequest, QueryResponse>("/query", queryMessage);
        }
    }
}
