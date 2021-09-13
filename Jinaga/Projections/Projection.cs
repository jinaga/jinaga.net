using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public abstract class Projection
    {
        public abstract string ToDescriptiveString(int depth = 0);

        public abstract Projection Apply(Label parameter, Label argument);
    }
}