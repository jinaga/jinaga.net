using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace Jinaga.Serialization
{
    class Collector
    {
        public FactGraph Graph { get; private set; } = new FactGraph();

        public int FactVisitsCount { get; private set; } = 0;
        public SerializerCache SerializerCache { get; private set; }

        public ImmutableHashSet<object> visiting =
            ImmutableHashSet<object>.Empty;
        public ImmutableDictionary<object, FactReference> referenceByObject =
            ImmutableDictionary<object, FactReference>.Empty;

        public Collector(SerializerCache serializerCache)
        {
            SerializerCache = serializerCache;
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

                var runtimeType = runtimeFact.GetType();
                var (newCache, serializer) = SerializerCache.GetSerializer(runtimeType);
                SerializerCache = newCache;
                try
                {
                    var fact = serializer(runtimeFact, this);
                    reference = fact.Reference;

                    Graph = Graph.Add(fact);
                    referenceByObject = referenceByObject.Add(runtimeFact, reference);
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }
            return reference;
        }
    }
}
