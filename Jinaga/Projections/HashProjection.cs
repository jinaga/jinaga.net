using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public class HashProjection : Projection
    {
        public string Tag { get; }
        public Type FactRuntimeType { get; }

        public override bool CanRunOnGraph => true;

        public HashProjection(string tag, Type factRuntimeType) :
            base(typeof(string))
        {
            Tag = tag;
            FactRuntimeType = factRuntimeType;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return $"#{Tag}";
        }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(Tag, out var replacement))
            {
                return new HashProjection(replacement, FactRuntimeType);
            }
            else
            {
                return this;
            }
        }
    }
}