using System;
using Jinaga.Pipelines;
using Jinaga.Visualizers;

namespace Jinaga.Projections
{
    public class CollectionProjection : Projection
    {
        private readonly Specification specification;

        public CollectionProjection(Specification specification)
        {
            this.specification = specification;
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            throw new NotImplementedException();
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            string pipelineStr = specification.Pipeline.ToDescriptiveString(depth + 1);
            string projectionStr = specification.Projection.ToDescriptiveString(depth + 1);
            return $"[\r\n{pipelineStr}    {indent}{projectionStr}\r\n{indent}]";
        }
    }
}
