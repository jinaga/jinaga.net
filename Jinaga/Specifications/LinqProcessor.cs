using Jinaga.Pipelines;
using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    internal class LinqProcessor
    {
        public static SourceContext FactsOfType(string typeName)
        {
            var unknown = new Label("***", typeName);
            var match = new Match(unknown, ImmutableList<MatchCondition>.Empty);
            var matches = ImmutableList.Create(match);
            var projection = new SimpleProjection(unknown.Name);
            return new SourceContext(matches, projection);
        }
    }
}
