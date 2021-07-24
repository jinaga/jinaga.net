using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class FactManager
    {
        private readonly IStore store;

        public FactManager(IStore store)
        {
            this.store = store;
        }

        private SerializerCache serializerCache = new SerializerCache();
        private DeserializerCache deserializerCache = new DeserializerCache();
        private ImmutableList<IObserver> observers = ImmutableList<IObserver>.Empty;

        public async Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            var added = await store.Save(graph, cancellationToken);
            foreach (var observer in observers)
            {
                await observer.FactsAdded(added, graph, cancellationToken);
            }
            return added;
        }

        public async Task<ImmutableList<Product>> Query(FactReference startReference, Pipeline pipeline, CancellationToken cancellationToken)
        {
            return await store.Query(startReference, pipeline, cancellationToken);
        }

        public async Task<ImmutableList<Product>> QueryAll(ImmutableList<FactReference> startReferences, Pipeline  pipeline, CancellationToken cancellationToken)
        {
            return await store.QueryAll(startReferences, pipeline, cancellationToken);
        }

        public async Task<ImmutableList<ProductProjection<TProjection>>> ComputeProjections<TProjection>(Projection projection, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    var productReferences = products
                        .Select(product => (product, reference: product.GetFactReference(simple.Tag)))
                        .ToImmutableList();
                    var references = productReferences
                        .Select(pair => pair.reference)
                        .ToImmutableList();
                    var graph = await store.Load(references, cancellationToken);
                    var productProjections = productReferences
                        .Select(pair => new ProductProjection<TProjection>(pair.product,
                            Deserialize<TProjection>(graph, pair.reference)))
                        .ToImmutableList();
                    return productProjections;
                default:
                    throw new NotImplementedException();
            }
        }

        public FactGraph Serialize(object prototype)
        {
            lock (this)
            {
                var collector = new Collector(serializerCache);
                collector.Serialize(prototype);
                serializerCache = collector.SerializerCache;
                return collector.Graph;
            }
        }

        public TFact Deserialize<TFact>(FactGraph graph, FactReference reference)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache);
                var fact = emitter.Deserialize<TFact>(reference);
                deserializerCache = emitter.DeserializerCache;
                return fact;
            }
        }

        public void AddObserver(IObserver observer)
        {
            lock (this)
            {
                observers = observers.Add(observer);
            }
        }

        public void RemoveObserver(IObserver observer)
        {
            lock (this)
            {
                observers = observers.Remove(observer);
            }
        }
    }
}
