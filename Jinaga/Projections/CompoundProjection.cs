using Jinaga.Visualizers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    /// <summary>
    /// A projection of several named components.
    /// </summary>
    /// <remarks>
    /// The components are ordered by name, ordinal, and this type is where that order lives.
    /// The order reaches results — it is the order of the feeds the projection produces, and the
    /// order <see cref="ToDescriptiveString"/> prints the components in — so it is a property of
    /// the projection rather than of each caller. Ordinal, so the order does not depend on the
    /// current culture. Every enumeration of <see cref="Names"/>, and of the components
    /// <see cref="Apply"/> rebuilds, comes out in that one order with no help from the caller.
    /// </remarks>
    public class CompoundProjection : Projection
    {
        public static CompoundProjection Empty = new CompoundProjection(
            ImmutableDictionary<string, Projection>.Empty,
            typeof(object)
        );

        private ImmutableSortedDictionary<string, Projection> projections;

        public CompoundProjection(ImmutableDictionary<string, Projection> projections, Type type) :
            base(type)
        {
            this.projections = projections.ToImmutableSortedDictionary(StringComparer.Ordinal);
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
            var fieldString = string.Join("", projections
                .Select(pair => $"    {indent}{pair.Key} = {pair.Value.ToDescriptiveString(depth + 1)}\n"));
            return $"{{\n{fieldString}{indent}}}";
        }
    }
}