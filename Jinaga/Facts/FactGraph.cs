using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactGraph
    {
        public static FactGraph Empty = new FactGraph(
            ImmutableDictionary<FactReference, FactEnvelope>.Empty,
            ImmutableList<FactReference>.Empty
        );

        private readonly ImmutableDictionary<FactReference, FactEnvelope> envelopeByReference;
        private readonly ImmutableList<FactReference> topologicalOrder;

        private FactGraph(ImmutableDictionary<FactReference, FactEnvelope> envelopeByReference, ImmutableList<FactReference> topologicalOrder)
        {
            this.envelopeByReference = envelopeByReference;
            this.topologicalOrder = topologicalOrder;
        }

        public bool CanAdd(Fact fact) =>
            fact.GetAllPredecessorReferences().All(p => envelopeByReference.ContainsKey(p));

        public FactGraph Add(Fact fact)
        {
            if (envelopeByReference.TryGetValue(fact.Reference, out var envelope))
            {
                return this;
            }

            if (!CanAdd(fact))
            {
                throw new ArgumentException("The fact graph does not contain all of the predecessors of the fact.");
            }

            return new FactGraph(
                envelopeByReference.Add(fact.Reference, new FactEnvelope(fact, ImmutableList<FactSignature>.Empty)),
                topologicalOrder.Add(fact.Reference)
            );
        }

        public FactGraph Add(FactEnvelope envelope)
        {
            if (envelopeByReference.TryGetValue(envelope.Fact.Reference, out var existingEnvelope))
            {
                // Find which signatures are new
                var newSignatures = envelope.Signatures.Where(s =>
                    !existingEnvelope.Signatures.Contains(s));
                // If every new signature is already in the list, then return the existing graph
                if (!newSignatures.Any())
                {
                    return this;
                }
                else
                {
                    // Merge the new signatures with the existing ones
                    var mergedSignatures = existingEnvelope.Signatures
                        .AddRange(newSignatures);
                    return new FactGraph(
                        envelopeByReference.SetItem(
                            envelope.Fact.Reference,
                            new FactEnvelope(envelope.Fact, mergedSignatures)),
                        topologicalOrder
                    );
                }
            }
            else
            {
                // Add the fact envelope
                return new FactGraph(
                    envelopeByReference.Add(envelope.Fact.Reference, envelope),
                    topologicalOrder.Add(envelope.Fact.Reference)
                );
            }
        }

        public FactGraph AddGraph(FactGraph factGraph)
        {
            FactGraph graph = this;
            foreach (var reference in factGraph.topologicalOrder)
            {
                graph = graph.Add(factGraph.envelopeByReference[reference]);
            }
            return graph;
        }

        public ImmutableList<FactReference> FactReferences => topologicalOrder;
        public FactReference Last => topologicalOrder.Last();

        public FactEnvelope GetEnvelope(FactReference factReference) => envelopeByReference[factReference];
        public Fact GetFact(FactReference reference) => envelopeByReference[reference].Fact;
        public ImmutableList<FactSignature> GetSignatures(FactReference reference) => envelopeByReference[reference].Signatures;

        public IEnumerable<FactReference> Predecessors(FactReference reference, string role, string targetType)
        {
            var envelope = envelopeByReference[reference];
            return envelope.Fact.Predecessors
                .Where(p => p.Role == role)
                .SelectMany(p => p.AllReferences)
                .Where(r => r.Type == targetType);
        }

        public FactGraph GetSubgraph(FactReference reference)
        {
            var subgraph = Empty;
            var envelope = envelopeByReference[reference];
            // Recursively add all predecessors
            foreach (var predecessorReference in envelope.Fact.GetAllPredecessorReferences())
            {
                subgraph = subgraph.AddGraph(GetSubgraph(predecessorReference));
            }
            // Add this fact
            subgraph = subgraph.Add(envelope);
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
