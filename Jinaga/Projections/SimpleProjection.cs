using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class SimpleProjection : Projection
    {
        public string Tag { get; }

        public SimpleProjection(string tag)
        {
            Tag = tag;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return Tag;
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            if (Tag == parameter.Name)
            {
                return new SimpleProjection(argument.Name);
            }
            else
            {
                return this;
            }
        }
    }
}