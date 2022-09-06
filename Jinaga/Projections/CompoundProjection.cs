using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;
using Jinaga.Visualizers;

namespace Jinaga.Projections
{
    public class CompoundProjection : Projection
    {
        private ImmutableDictionary<string, Projection> projections =
            ImmutableDictionary<string, Projection>.Empty;

        public CompoundProjection()
        {
        }

        public CompoundProjection(ImmutableDictionary<string, Projection> projections)
        {
            this.projections = projections;
        }

        public IEnumerable<string> Names => projections.Keys;

        public CompoundProjection With(string name, Projection projection)
        {
            return new CompoundProjection(projections.Add(name, projection));
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            return new CompoundProjection(projections
                .ToImmutableDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Apply(parameter, argument)
                )
            );
        }

        public override ImmutableList<(string name, Specification specification)> GetNamedSpecifications()
        {
            var namedSpecifications =
                from projection in projections
                let name = projection.Key
                where projection.Value is CollectionProjection
                let collection = (CollectionProjection)projection.Value
                select (name, collection.Specification);
            var nested =
                from projection in projections
                from namedSpecification in projection.Value.GetNamedSpecifications()
                select namedSpecification;
            return namedSpecifications.Concat(nested).ToImmutableList();
        }

        public Projection GetProjection(string name)
        {
            return projections[name];
        }

        public override string  ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            var fieldString = string.Join("", projections
                .OrderBy(pair => pair.Key)
                .Select(pair => $"    {indent}{pair.Key} = {pair.Value.ToDescriptiveString(depth + 1)}\r\n"));
            return $"{{\r\n{fieldString}{indent}}}";
        }
    }
}