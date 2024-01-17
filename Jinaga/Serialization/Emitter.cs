using Jinaga.Facts;
using Jinaga.Observers;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Serialization
{
    public interface IFactProxy
    {
        FactGraph Graph { get; }
    }

    class Emitter
    {
        public FactGraph Graph { get; }

        public DeserializerCache DeserializerCache { get; private set; }

        private ImmutableDictionary<FactReference, object> objectByReference =
            ImmutableDictionary<FactReference, object>.Empty;

        public IWatchContext? WatchContext { get; }

        public Emitter(FactGraph graph, DeserializerCache deserializerCache, IWatchContext? watchContext = null)
        {
            this.Graph = graph;
            DeserializerCache = deserializerCache;
            WatchContext = watchContext;
        }

        public TFact Deserialize<TFact>(FactReference reference)
        {
            Type type = typeof(TFact);
            object runtimeFact = DeserializeToType(reference, type);

            return (TFact)runtimeFact;
        }

        public object DeserializeToType(FactReference reference, Type type)
        {
            if (!objectByReference.TryGetValue(reference, out var runtimeFact))
            {
                var (newCache, deserializer) = DeserializerCache.GetDeserializer(type);
                DeserializerCache = newCache;
                try
                {
                    runtimeFact = deserializer(Graph.GetFact(reference), this);
                    objectByReference = objectByReference.Add(reference, runtimeFact);
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }

            return runtimeFact;
        }

        public TFact[] DeserializeArray<TFact>(ImmutableList<FactReference> references)
        {
            return references
                .Select(reference => Deserialize<TFact>(reference))
                .ToArray();
        }

        public FactGraph GetSubgraph(Fact fact)
        {
            return Graph.GetSubgraph(fact.Reference);
        }
    }
}
