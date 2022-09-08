using System;
using Jinaga.Pipelines;
using Jinaga.Visualizers;

namespace Jinaga.Projections
{
    public class CollectionProjection : ProjectionOld
    {
        public SpecificationOld Specification { get; }

        public CollectionProjection(SpecificationOld specification)
        {
            Specification = specification;
        }

        public override ProjectionOld Apply(Label parameter, Label argument)
        {
            return new CollectionProjection(new SpecificationOld(
                Specification.Pipeline.Apply(parameter, argument),
                Specification.Projection.Apply(parameter, argument)
            ));
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
