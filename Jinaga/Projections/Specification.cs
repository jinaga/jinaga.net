using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class Specification
    {
        public Specification(Pipeline pipeline, Projection projection)
        {
            Pipeline = pipeline;
            Projection = projection;
        }

        public Pipeline Pipeline { get; }
        public Projection Projection { get; }

        public ImmutableList<Inverse> ComputeInverses()
        {
            return Inverter.InvertSpecification(this).ToImmutableList();
        }
    }
}
