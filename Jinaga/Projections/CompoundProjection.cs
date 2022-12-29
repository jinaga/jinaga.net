using Jinaga.Visualizers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class CompoundProjection : Projection
    {
        private ImmutableDictionary<string, Projection> projections;

        public CompoundProjection(ImmutableDictionary<string, Projection> projections)
        {
            this.projections = projections;
        }

        public IEnumerable<string> Names => projections.Keys;

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            return new CompoundProjection(projections
                .ToImmutableDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Apply(replacements)
                )
            );
        }

        public Projection GetProjection(string name)
        {
            return projections[name];
        }

        public override bool CanRunOnGraph => projections.Values.All(p => p.CanRunOnGraph);

        public override string  ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            var fieldString = string.Join("", projections
                .OrderBy(pair => pair.Key)
                .Select(pair => $"    {indent}{pair.Key} = {pair.Value.ToDescriptiveString(depth + 1)}\n"));
            return $"{{\n{fieldString}{indent}}}";
        }
    }
}