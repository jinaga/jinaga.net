using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class CompoundProjection : Projection
    {
        private ImmutableList<(string, string)> fields = ImmutableList<(string, string)>.Empty;

        public CompoundProjection()
        {
        }

        private CompoundProjection(ImmutableList<(string, string)> fields)
        {
            this.fields = fields;
        }

        public CompoundProjection With(string name, string tag)
        {
            return new CompoundProjection(fields.Add((name, tag)));
        }

        public override string ToDescriptiveString()
        {
            var fieldString = string.Join("", fields.Select(field => $"        {field.Item1} = {field.Item2}\r\n"));
            return $"{{\r\n{fieldString}    }}";
        }
    }
}