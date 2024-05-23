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
            ImmutableDictionary<FactReference, ImmutableList<FactSignature>>.Empty,
            ImmutableList<FactReference>.Empty
        );

        private readonly ImmutableDictionary<FactReference, Fact> factsByReference;
        private readonly ImmutableDictionary<FactReference, ImmutableList<FactSignature>> signaturesByReference;
        private readonly ImmutableList<FactReference> topologicalOrder;

        private FactGraph(ImmutableDictionary<FactReference, Fact> factsByReference, ImmutableDictionary<FactReference, ImmutableList<FactSignature>> signaturesByReference, ImmutableList<FactReference> topologicalOrder)
        {
            this.factsByReference = factsByReference;
            this.signaturesByReference = signaturesByReference;
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
                signaturesByReference,
                topologicalOrder.Add(fact.Reference)
            );
        }

        public FactGraph Add(Fact fact, IEnumerable<FactSignature> signatures)
        {
            if (signaturesByReference.ContainsKey(fact.Reference))
            {
                // Find the existing signatures for the fact, if any
                if (signaturesByReference.TryGetValue(fact.Reference, out var existingSignatures))
                {
                    // If every new signature is already in the list, then return the existing graph
                    if (signatures.All(s => existingSignatures.Contains(s)))
                    {
                        return this;
                    }
                    else
                    {
                        // Merge the new signatures with the existing ones
                        var mergedSignatures = existingSignatures.AddRange(signatures);
                        return new FactGraph(
                            factsByReference,
                            signaturesByReference.SetItem(fact.Reference, mergedSignatures),
                            topologicalOrder
                        );
                    }
                }
                else
                {
                    // Add the new signatures
                    return new FactGraph(
                        factsByReference,
                        signaturesByReference.Add(fact.Reference, signatures.ToImmutableList()),
                        topologicalOrder
                    );
                }
            }
            else
            {
                if (signatures.Any())
                {
                    // Add the fact and the new signatures
                    return new FactGraph(
                        factsByReference.Add(fact.Reference, fact),
                        signaturesByReference.Add(fact.Reference, signatures.ToImmutableList()),
                        topologicalOrder.Add(fact.Reference)
                    );
                }
                else
                {
                    // Add the fact without any signatures
                    return new FactGraph(
                        factsByReference.Add(fact.Reference, fact),
                        signaturesByReference,
                        topologicalOrder.Add(fact.Reference)
                    );
                }
            }
        }

        public FactGraph AddGraph(FactGraph factGraph)
        {
            var newFacts = factGraph.topologicalOrder
                .Select(reference => factGraph.factsByReference[reference])
                .Where(fact => !factsByReference.ContainsKey(fact.Reference))
                .ToImmutableList();

            if (newFacts.Count == 0)
            {
                return this;
            }

            return new FactGraph(
                factsByReference.AddRange(newFacts.Select(fact => new KeyValuePair<FactReference, Fact>(fact.Reference, fact))),
                signaturesByReference,
                topologicalOrder.AddRange(newFacts.Select(fact => fact.Reference))
            );
        }

        public ImmutableList<FactReference> FactReferences => topologicalOrder;
        public FactReference Last => topologicalOrder.Last();

        public Fact GetFact(FactReference reference) => factsByReference[reference];
        public ImmutableList<FactSignature> GetSignatures(FactReference reference) => signaturesByReference[reference];

        public IEnumerable<FactReference> Predecessors(FactReference reference, string role, string targetType)
        {
            var fact = factsByReference[reference];
            return fact.Predecessors
                .Where(p => p.Role == role)
                .SelectMany(p => p.AllReferences)
                .Where(r => r.Type == targetType);
        }

        public FactGraph GetSubgraph(FactReference reference)
        {
            var subgraph = Empty;
            Fact fact = GetFact(reference);
            // Recursively add all predecessors
            foreach (var predecessorReference in fact.GetAllPredecessorReferences())
            {
                subgraph = subgraph.AddGraph(GetSubgraph(predecessorReference));
            }
            // Add this fact
            subgraph = subgraph.Add(fact);
            return subgraph;
        }

        public override bool Equals(object obj)
        {
            // Two graphs are equal if they contain the same fact references.
            if (obj is FactGraph other)
            {
                return topologicalOrder.SequenceEqual(other.topologicalOrder);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return topologicalOrder.Any()
                ? Last.Hash.GetHashCode()
                : 0;
        }
    }
}
