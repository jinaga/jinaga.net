using System.Collections.Immutable;

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

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
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

        public override bool CanRunOnGraph => true;
    }
}