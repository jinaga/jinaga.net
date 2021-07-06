using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public abstract class Predecessor
    {
        protected Predecessor(string role)
        {
            Role = role;
        }

        public string Role { get; }
    }

    public class PredecessorSingle : Predecessor
    {
        public PredecessorSingle(string role, FactReference reference) :
            base(role)
        {
            Reference = reference;
        }

        public FactReference Reference { get; }
    }

    public class PredecessorMultiple : Predecessor
    {
        public PredecessorMultiple(string role, ImmutableList<FactReference> references) :
            base(role)
        {
            References = references;
        }

        public ImmutableList<FactReference> References { get;}
    }
}