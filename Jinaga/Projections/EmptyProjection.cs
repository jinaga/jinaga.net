using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class EmptyProjection : ProjectionOld
    {
        public override ProjectionOld Apply(Label parameter, Label argument)
        {
            return this;
        }

        public override ProjectionOld Apply(ImmutableDictionary<string, string> replacements)
        {
            return this;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return string.Empty;
        }
    }
}
