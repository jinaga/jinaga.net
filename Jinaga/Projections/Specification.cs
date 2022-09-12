using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class Specification
    {
        public Specification(
            ImmutableList<Label> given,
            ImmutableList<Match> matches,
            ProjectionOld projection)
        {
            Given = given;
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<Label> Given { get; }
        public ImmutableList<Match> Matches { get; }
        public ProjectionOld Projection { get; }

        public Specification Apply(ImmutableList<Label> arguments)
        {
            var replacements = Given.Zip(arguments, (parameter, argument) => (parameter, argument))
                .ToImmutableDictionary(pair => pair.parameter.Name, pair => pair.argument.Name);
            var newMatches = Matches.Select(match => match.Apply(replacements)).ToImmutableList();
            var newProjection = Projection.Apply(replacements);
            return new Specification(ImmutableList<Label>.Empty, newMatches, newProjection);
        }
    }
    public class SpecificationOld
    {
        public SpecificationOld(PipelineOld pipeline, ProjectionOld projection)
        {
            Pipeline = pipeline;
            Projection = projection;
        }

        public PipelineOld Pipeline { get; }
        public ProjectionOld Projection { get; }

        public ImmutableList<Inverse> ComputeInverses()
        {
            return Inverter.InvertSpecification(this).ToImmutableList();
        }
    }
}
