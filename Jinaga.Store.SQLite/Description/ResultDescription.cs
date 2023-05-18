using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Description
{
    internal class ResultDescription
    {
        public QueryDescription QueryDescription { get; set; }
        public ImmutableDictionary<string, ResultDescription> ChildResultDescriptions { get; set; }
    }
}