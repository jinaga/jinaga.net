using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Description
{
    internal class ResultDescription
    {
        public QueryDescription QueryDescription { get; }
        public ImmutableDictionary<string, ResultDescription> ChildResultDescriptions { get; }

        public ResultDescription(QueryDescription queryDescription, ImmutableDictionary<string, ResultDescription> childResultDescriptions)
        {
            QueryDescription = queryDescription;
            ChildResultDescriptions = childResultDescriptions;
        }
    }
}