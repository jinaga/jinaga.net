using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Linq;

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

        public Value Merge(ImmutableList<Match> matches)
        {
            // TODO: Merge the new matches into this value's matches.
            return this;
        }

        public static Value Simple(string label)
        {
            return new Value(
                ImmutableList<Match>.Empty,
                new SimpleProjection(label));
        }

        public override string ToString()
        {
            var matches = string.Join("", this.Matches.Select(m => m.ToDescriptiveString(1)));
            var projection = this.Projection == null ? "" : " => " + this.Projection.ToDescriptiveString(0);
            return $"{{\n{matches}}}{projection}\n";
        }
    }
}
