using Jinaga.Records;

namespace Jinaga.Http
{
    public class QueryRequest
    {
        public FactReference Start { get; set; }
        public string Query { get; set; }
    }
}