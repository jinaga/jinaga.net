using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public class Fact
    {
        public Fact(FactReference reference, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            Reference = reference;
            Fields = fields;
            Predecessors = predecessors;
        }

        public string Type => Reference.Type;
        public string Hash => Reference.Hash;
        public FactReference Reference { get; }
        public ImmutableList<Field> Fields { get; }
        public ImmutableList<Predecessor> Predecessors { get; }
    }
}