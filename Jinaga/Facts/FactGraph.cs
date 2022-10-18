using System;
using System.Collections.Generic;
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

        public bool CanAdd(Fact fact) =>
            fact.GetAllPredecessorReferences().All(p => factsByReference.ContainsKey(p));

        public FactGraph Add(Fact fact)
        {
            if (factsByReference.ContainsKey(fact.Reference))
            {
                return this;
            }

            if (!CanAdd(fact))
            {
                throw new ArgumentException("The fact graph does not contain all of the predecessors of the fact.");
            }

            return new FactGraph(
                factsByReference.Add(fact.Reference, fact),
                topologicalOrder.Add(fact.Reference)
            );
        }

        public ImmutableList<FactReference> FactReferences => topologicalOrder;
        public FactReference Last => topologicalOrder.Last();

        public Fact GetFact(FactReference reference) => factsByReference[reference];

        public IEnumerable<FactReference> Predecessors(FactReference reference, string role, string targetType)
        {
            var fact = factsByReference[reference];
            return fact.Predecessors
                .Where(p => p.Role == role)
                .SelectMany(p => p.AllReferences)
                .Where(r => r.Type == targetType);
        }
    }
}
