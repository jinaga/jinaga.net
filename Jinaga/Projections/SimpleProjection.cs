using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public class SimpleProjection : Projection
    {
        public string Tag { get; }

        public SimpleProjection(string tag, Type type) :
            base(type)
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
                return new SimpleProjection(replacement, Type);
            }
            else
            {
                return this;
            }
        }

        public override bool CanRunOnGraph => true;
    }
}