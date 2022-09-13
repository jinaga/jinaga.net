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

        public override string ToDescriptiveString(int depth = 0)
        {
            var indent = Strings.Indent(depth);
            var matchStrings = Matches.Select(match => match.ToDescriptiveString(depth + 1));
            var matchString = string.Join("", matchStrings);
            var projectionString = Projection.ToDescriptiveString(depth + 1);
            return $"{{\n{matchString}{indent}}}";
        }
    }
    public class CollectionProjectionOld : Projection
    {
        public SpecificationOld Specification { get; }

        public CollectionProjectionOld(SpecificationOld specification)
        {
            Specification = specification;
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            return new CollectionProjectionOld(new SpecificationOld(
                Specification.Pipeline.Apply(parameter, argument),
                Specification.Projection.Apply(parameter, argument)
            ));
        }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            throw new NotImplementedException();
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            string pipelineStr = Specification.Pipeline.ToDescriptiveString(depth + 1);
            string projectionStr = Specification.Projection.ToDescriptiveString(depth + 1);
            return $"[\r\n{pipelineStr}    {indent}{projectionStr}\r\n{indent}]";
        }
    }
}
