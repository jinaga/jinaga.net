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

        private ImmutableHashSet<object> visiting =
            ImmutableHashSet<object>.Empty;
        private ImmutableDictionary<object, FactReference> referenceByObject =
            ImmutableDictionary<object, FactReference>.Empty;

        public Collector(SerializerCache serializerCache, ConditionalWeakTable<object, FactGraph> graphByFact)
        {
            SerializerCache = serializerCache;
            this.graphByFact = graphByFact;
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

                    Graph = Graph.Add(fact);
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
