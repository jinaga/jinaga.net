using System.Collections.Immutable;
using System;
using Jinaga.Pipelines;
using Jinaga.Visualizers;
using System.Linq;

namespace Jinaga.Projections
{
    public class CollectionProjection : Projection
    {
        public CollectionProjection(ImmutableList<Match> matches, Projection projection)
        {
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            return new CollectionProjection(
                Matches.Select(match => match.Apply(replacements)).ToImmutableList(),
                Projection.Apply(replacements));
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            throw new NotImplementedException();
        }

        public override bool CanRunOnGraph => Matches.All(m => m.CanRunOnGraph) && Projection.CanRunOnGraph;

        public override string ToDescriptiveString(int depth = 0)
        {
            var indent = Strings.Indent(depth);
            var matchStrings = Matches.Select(match => match.ToDescriptiveString(depth + 1));
            var matchString = string.Join("", matchStrings);
            var projectionString = Projection.ToDescriptiveString(depth + 1);
            return $"{{\n{matchString}{indent}}}";
        }
    }
}
