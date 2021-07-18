using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Serialization;
using Jinaga.Services;

namespace Jinaga
{
    public class Jinaga
    {
        private readonly IStore store;
        private SerializerCache serializerCache = new SerializerCache();
        private DeserializerCache deserializerCache = new DeserializerCache();

        public Jinaga(IStore store)
        {
            this.store = store;
        }

        public async Task<TFact> Fact<TFact>(TFact prototype) where TFact: class
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            var graph = Serialize(prototype);
            var added = await store.Save(graph);
            return Deserialize<TFact>(graph, graph.Last);
        }

        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(TFact start, Specification<TFact, TProjection> specification) where TFact: class
        {
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            var graph = Serialize(start);
            var startReference = graph.Last;
            var pipeline = specification.Pipeline;
            var products = await store.Query(startReference, pipeline.InitialTag, pipeline.Paths);
            var results = await ComputeProjections<TProjection>(pipeline.Projection, products);
            return results;
        }

        private async Task<ImmutableList<TProjection>> ComputeProjections<TProjection>(Projection projection, ImmutableList<Product> products)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    var references = products
                        .Select(product => product.GetFactReference(simple.Tag))
                        .ToImmutableList();
                    var graph = await store.Load(references);
                    var projections = references
                        .Select(reference => Deserialize<TProjection>(graph, reference))
                        .ToImmutableList();
                    return projections;
                default:
                    throw new NotImplementedException();
            }
        }

        private FactGraph Serialize(object prototype)
        {
            lock (this)
            {
                var collector = new Collector(serializerCache);
                collector.Serialize(prototype);
                serializerCache = collector.SerializerCache;
                return collector.Graph;
            }
        }

        private TFact Deserialize<TFact>(FactGraph graph, FactReference reference)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache);
                var fact = emitter.Deserialize<TFact>(reference);
                deserializerCache = emitter.DeserializerCache;
                return fact;
            }
        }
    }
}
