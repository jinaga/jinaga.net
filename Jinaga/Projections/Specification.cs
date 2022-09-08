using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
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
