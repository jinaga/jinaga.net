using System.Collections.Generic;
using Jinaga.Records;

namespace Jinaga.Http
{
    public class QueryResponse
    {
        public List<List<FactReference>> Results { get; set; } = new List<List<FactReference>>();
    }
}