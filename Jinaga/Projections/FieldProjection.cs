using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public class FieldProjection : Projection
    {
        public string Tag { get; }
        public Type FactRuntimeType { get; }
        public string FieldName { get; }

        public override bool CanRunOnGraph => true;

        public FieldProjection(string tag, Type factRuntimeType, string fieldName, Type type) :
            base(type)
        {
            Tag = tag;
            FactRuntimeType = factRuntimeType;
            FieldName = fieldName;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return $"{Tag}.{FieldName}";
        }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(Tag, out var replacement))
            {
                return new FieldProjection(replacement, FactRuntimeType, FieldName, Type);
            }
            else
            {
                return this;
            }
        }
    }
}