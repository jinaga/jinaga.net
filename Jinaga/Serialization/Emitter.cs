using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace Jinaga.Serialization
{
    class Emitter
    {
        private readonly FactGraph graph;

        public DeserializerCache DeserializerCache { get; private set; }

        private ImmutableDictionary<FactReference, object> objectByReference =
            ImmutableDictionary<FactReference, object>.Empty;

        public Emitter(FactGraph graph, DeserializerCache deserializerCache)
        {
            this.graph = graph;
            DeserializerCache = deserializerCache;
        }

        public object Deserialize(FactReference reference, Type type)
        {
            if (!objectByReference.TryGetValue(reference, out var runtimeFact))
            {
                var (newCache, deserializer) = DeserializerCache.GetDeserializer(type);
                DeserializerCache = newCache;
                try
                {
                    runtimeFact = deserializer(graph.GetFact(reference), this);
                    objectByReference = objectByReference.Add(reference, runtimeFact);
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }

            return runtimeFact;
        }
    }
}
