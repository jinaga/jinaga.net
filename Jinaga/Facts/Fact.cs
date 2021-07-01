using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public class Fact
    {
        public string Type { get; set; }
        public ImmutableList<Field> Fields { get; set; }
    }
}