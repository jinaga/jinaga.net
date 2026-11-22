using Jinaga.Visualizers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class CompoundProjection : Projection
    {
        public static CompoundProjection Empty = new CompoundProjection(
            ImmutableDictionary<string, Projection>.Empty,
            typeof(object)
        );

        private ImmutableDictionary<string, Projection> projections;

        public CompoundProjection(ImmutableDictionary<string, Projection> projections, Type type) :
            base(type)
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
                ),
                Type
            );
        }

        public Projection GetProjection(string name)
        {
            if (projections.TryGetValue(name, out var projection))
            {
                return projection;
            }
            else
            {
                throw new ArgumentException($"No projection named {name}");
            }
        }

        public override string  ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            // Ordinal, so the text a specification describes itself with does not depend on the
            // current culture. It is compared against pinned conformance vectors, and
            // FeedBuilder takes a projection's children in this same order.
            var fieldString = string.Join("", projections
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"    {indent}{pair.Key} = {pair.Value.ToDescriptiveString(depth + 1)}\n"));
            return $"{{\n{fieldString}{indent}}}";
        }
    }
}