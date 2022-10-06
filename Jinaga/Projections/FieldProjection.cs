using Jinaga.Pipelines;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public class FieldProjection : Projection
    {
        public string Tag { get; }
        public string FieldName { get; }

        public override bool CanRunOnGraph => true;

        public FieldProjection(string tag, string fieldName)
        {
            Tag = tag;
            FieldName = fieldName;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return $"{Tag}.{FieldName}";
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            if (Tag == parameter.Name)
            {
                return new FieldProjection(argument.Name, FieldName);
            }
            else
            {
                return this;
            }
        }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(Tag, out var replacement))
            {
                return new FieldProjection(replacement, FieldName);
            }
            else
            {
                return this;
            }
        }
    }
}