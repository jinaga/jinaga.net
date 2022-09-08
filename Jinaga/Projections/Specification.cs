using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class Specification
    {
        public Specification(
            ImmutableList<Label> given,
            ImmutableList<Match> matches
        )
        {
            Given = given;
            Matches = matches;
        }

        public ImmutableList<Label> Given { get; }
        public ImmutableList<Match> Matches { get; }
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
