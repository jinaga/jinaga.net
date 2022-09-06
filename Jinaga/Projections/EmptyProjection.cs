using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class EmptyProjection : Projection
    {
        public override Projection Apply(Label parameter, Label argument)
        {
            return this;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return string.Empty;
        }
    }
}
