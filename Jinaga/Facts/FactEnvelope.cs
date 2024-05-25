using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public class FactEnvelope
    {
        public Fact Fact { get; }
        public ImmutableList<FactSignature> Signatures { get; }

        public FactEnvelope(Fact fact, ImmutableList<FactSignature> signatures)
        {
            Fact = fact;
            Signatures = signatures;
        }

        public FactEnvelope AddSignature(FactSignature signature)
        {
            return new FactEnvelope(Fact, Signatures.Add(signature));
        }
    }
}