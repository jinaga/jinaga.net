using System.Collections.Generic;
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
        public abstract IEnumerable<FactReference> AllReferences { get; }
    }

    public class PredecessorSingle : Predecessor
    {
        public PredecessorSingle(string role, FactReference reference) :
            base(role)
        {
            Reference = reference;
        }

        public FactReference Reference { get; }
        public override IEnumerable<FactReference> AllReferences => ImmutableList.Create(Reference);
    }

    public class PredecessorMultiple : Predecessor
    {
        public PredecessorMultiple(string role, ImmutableList<FactReference> references) :
            base(role)
        {
            References = references;
        }

        public ImmutableList<FactReference> References { get;}
        public override IEnumerable<FactReference> AllReferences => References;
    }
}