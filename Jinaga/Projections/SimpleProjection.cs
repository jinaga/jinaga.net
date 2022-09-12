using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class SimpleProjection : ProjectionOld
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

        public override ProjectionOld Apply(Label parameter, Label argument)
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

        public override ProjectionOld Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(Tag, out var replacement))
            {
                return new SimpleProjection(replacement);
            }
            else
            {
                return this;
            }
        }
    }
}