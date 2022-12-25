using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Repository
{
    internal class Value
    {
        public ImmutableList<Match> Matches { get; }
        public SimpleProjection Projection { get; }

        public Value(ImmutableList<Match> matches, SimpleProjection projection)
        {
            Matches = matches;
            Projection = projection;
        }
    }
}
