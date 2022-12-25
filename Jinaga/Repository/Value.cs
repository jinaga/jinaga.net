using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Repository
{
    internal class Value
    {
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        public Value(ImmutableList<Match> matches, Projection projection)
        {
            Matches = matches;
            Projection = projection;
        }

        internal static Value Simple(string label)
        {
            return new Value(
                ImmutableList<Match>.Empty,
                new SimpleProjection(label));
        }
    }
}
