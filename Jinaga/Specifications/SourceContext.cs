using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    public class SourceContext
    {
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        public SourceContext(ImmutableList<Match> matches, Projection projection)
        {
            Matches = matches;
            Projection = projection;
        }
    }
}