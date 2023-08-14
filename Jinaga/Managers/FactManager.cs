using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Products;
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
        private readonly NetworkManager networkManager;
        private readonly ObservableSource observableSource;

        public FactManager(IStore store, NetworkManager networkManager)
        {
            this.store = store;
            this.networkManager = networkManager;
            
            observableSource = new ObservableSource(store);
        }

        private SerializerCache serializerCache = SerializerCache.Empty;
        private DeserializerCache deserializerCache = DeserializerCache.Empty;

        public async Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            var added = await store.Save(graph, cancellationToken);
            await observableSource.Notify(graph, added, cancellationToken);

            var facts = graph.FactReferences
                .Select(r => graph.GetFact(r))
                .ToImmutableList();
            await networkManager.Save(facts, cancellationToken);
            return added;
        }

        public async Task Fetch(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
        {
            await networkManager.Fetch(givenReferences, specification, cancellationToken);
        }

        public async Task Fetch(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            await networkManager.Fetch(givenTuple, specification, cancellationToken);
        }

        public async Task<ImmutableList<ProjectedResult>> Read(FactReferenceTuple givenTuple, Specification specification, Type type, IWatchContext? watchContext, CancellationToken cancellationToken)
        {
            var products = await store.Read(givenTuple, specification, cancellationToken);
            if (products.Count == 0)
            {
                return ImmutableList<ProjectedResult>.Empty;
            }
            var references = products
                .SelectMany(product => product.GetFactReferences())
                .ToImmutableList();
            var graph = await store.Load(references, cancellationToken);
            return DeserializeProductsFromGraph(graph, specification.Projection, products, type, "", watchContext);
        }

        public async Task<ImmutableList<Product>> Query(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
        {
            await networkManager.Fetch(givenReferences, specification, cancellationToken);
            return await store.Read(givenReferences, specification, cancellationToken);
        }

        public async Task<FactGraph> LoadProducts(ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            var references = products
                .SelectMany(product => product.GetFactReferences())
                .ToImmutableList();
            return await store.Load(references, cancellationToken);
        }

        public async Task<ImmutableList<ProjectedResult>> ComputeProjections(
            Projection projection,
            ImmutableList<Product> products,
            Type type,
            IWatchContext? watchContext,
            string path,
            CancellationToken cancellationToken)
        {
            var references = Projector.GetFactReferences(projection, products, type);
            var graph = await store.Load(references, cancellationToken);
            return DeserializeProductsFromGraph(graph, projection, products, type, path, watchContext);
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

        public ImmutableList<ProjectedResult> DeserializeProductsFromGraph(
            FactGraph graph,
            Projection projection,
            ImmutableList<Product> products,
            Type type,
            string path,
            IWatchContext? watchContext)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache, watchContext);
                ImmutableList<ProjectedResult> results = Deserializer.Deserialize(emitter, projection, type, products, path);
                deserializerCache = emitter.DeserializerCache;
                return results;
            }
        }

        public Observer StartObserver(FactReferenceTuple givenTuple, Specification specification, Func<object, Task<Func<Task>>> onAdded)
        {
            var observer = new Observer(specification, givenTuple, this, onAdded);
            observer.Start();
            return observer;
        }

        public SpecificationListener AddSpecificationListener(Specification specification, Func<ImmutableList<Product>, CancellationToken, Task> onResult)
        {
            return observableSource.AddSpecificationListener(specification, onResult);
        }

        public void RemoveSpecificationListener(SpecificationListener listener)
        {
            observableSource.RemoveSpecificationListener(listener);
        }

        public async Task NotifyObservers(FactGraph graph, ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            await observableSource.Notify(graph, facts, cancellationToken);
        }

        public Task<DateTime?> GetMruDate(string specificationHash)
        {
            return store.GetMruDate(specificationHash);
        }

        public Task SetMruDate(string specificationHash, DateTime mruDate)
        {
            return store.SetMruDate(specificationHash, mruDate);
        }
    }
}
