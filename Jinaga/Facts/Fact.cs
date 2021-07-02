using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public class Fact
    {
        public Fact(string type, ImmutableList<Field> fields)
        {
            Type = type;
            Fields = fields;
        }

        public string Type { get; }
        public ImmutableList<Field> Fields { get; }
    }
}