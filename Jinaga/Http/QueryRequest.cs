using Jinaga.Records;

namespace Jinaga.Http
{
    public class QueryRequest
    {
        public FactReference Start { get; set; } = new FactReference();
        public string Query { get; set; } = "";
    }
}