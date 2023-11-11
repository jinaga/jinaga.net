using Jinaga.Facts;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite
{
    internal class ResultSetFact
    {
        public string Hash { get; set; }
        public int FactId { get; set; }
        public string Data { get;set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }

    internal class ResultSetTree
    {
        public ResultSetTree()
        {
            ChildResultSets =  ImmutableDictionary<string, ResultSetTree>.Empty;            
        }

        public ImmutableList<ImmutableDictionary<int, ResultSetFact>> ResultSet { get; set; } = ImmutableList<ImmutableDictionary<int, ResultSetFact>>.Empty;
        public ImmutableDictionary<string, ResultSetTree> ChildResultSets { get; set; } = ImmutableDictionary<string, ResultSetTree>.Empty;
    }
}