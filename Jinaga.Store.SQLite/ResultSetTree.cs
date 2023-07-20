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
        public ImmutableList<ImmutableDictionary<int, ResultSetFact>> ResultSet { get; set; }
        public ImmutableDictionary<string, ResultSetTree> ChildResultSets { get; set; }
    }
}