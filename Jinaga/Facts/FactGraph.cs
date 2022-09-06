using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactGraph
    {
        public static FactGraph Empty = new FactGraph(
            ImmutableDictionary<FactReference, Fact>.Empty,
            ImmutableList<FactReference>.Empty
        );

        private readonly ImmutableDictionary<FactReference, Fact> factsByReference;
        private readonly ImmutableList<FactReference> topologicalOrder;

        private FactGraph(ImmutableDictionary<FactReference, Fact> factsByReference, ImmutableList<FactReference> topologicalOrder)
        {
            this.factsByReference = factsByReference;
            this.topologicalOrder = topologicalOrder;
        }

        public FactGraph Add(Fact fact)
        {
            if (factsByReference.ContainsKey(fact.Reference))
            {
                return this;
            }
            
            return new FactGraph(
                factsByReference.Add(fact.Reference, fact),
                topologicalOrder.Add(fact.Reference)
            );
        }

        public ImmutableList<FactReference> FactReferences => topologicalOrder;
        public FactReference Last => topologicalOrder.Last();

        public Fact GetFact(FactReference reference) => factsByReference[reference];
    }
}
