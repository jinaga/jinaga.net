using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public abstract class Projection
    {
        public abstract string ToDescriptiveString();

        public abstract Projection Apply(Label parameter, Label argument);
    }
}