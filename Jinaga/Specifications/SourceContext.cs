using Jinaga.Projections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Specifications
{
    internal class SourceContext
    {
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }
        public IEnumerable<string> Labels => Matches.Select(match => match.Unknown.Name);

        public SourceContext(ImmutableList<Match> matches, Projection projection)
        {
            Matches = matches;
            Projection = projection;
        }
    }
}