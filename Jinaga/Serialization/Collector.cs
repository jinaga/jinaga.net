using Jinaga.Cryptography;
using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jinaga.Serialization
{
    public class Collector
    {
        public FactGraph Graph { get; private set; } = FactGraph.Empty;

        public int FactVisitsCount { get; private set; } = 0;
        public SerializerCache SerializerCache { get; private set; }
        private readonly ConditionalWeakTable<object, FactGraph> graphByFact;
        private readonly KeyPair? keyPair;

        private ImmutableHashSet<object> visiting =
            ImmutableHashSet<object>.Empty;
        private ImmutableDictionary<object, FactReference> referenceByObject =
            ImmutableDictionary<object, FactReference>.Empty;

        public Collector(SerializerCache serializerCache, ConditionalWeakTable<object, FactGraph> graphByFact, KeyPair? keyPair = null)
        {
            SerializerCache = serializerCache;
            this.graphByFact = graphByFact;
            this.keyPair = keyPair;
        }

        public FactReference Serialize(object runtimeFact)
        {
            if (!referenceByObject.TryGetValue(runtimeFact, out var reference))
            {
                if (visiting.Contains(runtimeFact))
                {
                    throw new ArgumentException("Jinaga cannot serialize a fact containing a cycle");
                }
                visiting = visiting.Add(runtimeFact);
                FactVisitsCount++;

                if (graphByFact.TryGetValue(runtimeFact, out var graph))
                {
                    Graph = Graph.AddGraph(graph);
                    reference = graph.Last;
                }
                else
                {
                    var runtimeType = runtimeFact.GetType();
                    var fact = SerializeToFact(runtimeType, runtimeFact);
                    reference = fact.Reference;
                    if (keyPair != null)
                    {
                        var signature = keyPair.SignFact(fact);
                        Graph = Graph.Add(fact, new [] { signature });
                    }
                    else
                    {
                        Graph = Graph.Add(fact);
                    }
                }

                visiting = visiting.Remove(runtimeFact);
                referenceByObject = referenceByObject.Add(runtimeFact, reference);
            }
            return reference;
        }

        private Fact SerializeToFact(Type runtimeType, object runtimeFact)
        {
            var (newCache, serializer) = SerializerCache.GetSerializer(runtimeType);
            SerializerCache = newCache;
            try
            {
                return serializer(runtimeFact, this);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }
    }
}
