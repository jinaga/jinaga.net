using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class FactManager
    {
        private readonly IStore store;
        private readonly NetworkManager networkManager;
        private readonly ObservableSource observableSource;

        private ImmutableList<TaskHandle> pendingTasks = ImmutableList<TaskHandle>.Empty;

        public FactManager(IStore store, NetworkManager networkManager)
        {
            this.store = store;
            this.networkManager = networkManager;
            
            observableSource = new ObservableSource(store);
        }

        private SerializerCache serializerCache = SerializerCache.Empty;
        private DeserializerCache deserializerCache = DeserializerCache.Empty;
        private readonly ConditionalWeakTable<object, FactGraph> graphByFact = new ConditionalWeakTable<object, FactGraph>();

        public async Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            var (graph, profile) = await networkManager.Login(cancellationToken).ConfigureAwait(false);
            await store.Save(graph, cancellationToken).ConfigureAwait(false);
            return (graph, profile);
        }

        public async Task Push(CancellationToken cancellationToken)
        {
            await networkManager.Save(cancellationToken).ConfigureAwait(false);
        }

        public async Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            var added = await store.Save(graph, cancellationToken).ConfigureAwait(false);
            await observableSource.Notify(graph, added, cancellationToken).ConfigureAwait(false);

            // Don't wait on the network manager if we have persistent storage.
            if (store.IsPersistent)
            {
                var handle = new TaskHandle();
                AddBackgroundTask(handle);
                var background = Task.Run(async () =>
                {
                    try
                    {
                        await networkManager.Save(default).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Trust that the network manager raised the OnStatusChanged event.
                    }
                    finally
                    {
                        RemoveBackgroundTask(handle);
                    }
                });
                handle.Task = background;
            }
            else
            {
                await networkManager.Save(cancellationToken).ConfigureAwait(false);
            }
            return added;
        }

        public async Task Fetch(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            await networkManager.Fetch(givenTuple, specification, cancellationToken).ConfigureAwait(false);
        }

        public async Task Subscribe(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            await networkManager.Subscribe(givenTuple, specification, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ImmutableList<ProjectedResult>> Read(FactReferenceTuple givenTuple, Specification specification, Type type, IWatchContext? watchContext, CancellationToken cancellationToken)
        {
            var products = await store.Read(givenTuple, specification, cancellationToken).ConfigureAwait(false);
            if (products.Count == 0)
            {
                return ImmutableList<ProjectedResult>.Empty;
            }
            var references = products
                .SelectMany(product => product.GetFactReferences())
                .ToImmutableList();
            var graph = await store.Load(references, cancellationToken).ConfigureAwait(false);
            return DeserializeProductsFromGraph(graph, specification.Projection, products, type, "", watchContext);
        }

        public async Task<ImmutableList<Product>> Query(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            await networkManager.Fetch(givenTuple, specification, cancellationToken).ConfigureAwait(false);
            return await store.Read(givenTuple, specification, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ImmutableList<Product>> QueryLocal(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            return await store.Read(givenTuple, specification, cancellationToken).ConfigureAwait(false);
        }

        public async Task<FactGraph> LoadProducts(ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            var references = products
                .SelectMany(product => product.GetFactReferences())
                .ToImmutableList();
            return await store.Load(references, cancellationToken).ConfigureAwait(false);
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
            var graph = await store.Load(references, cancellationToken).ConfigureAwait(false);
            return DeserializeProductsFromGraph(graph, projection, products, type, path, watchContext);
        }

        public FactGraph Serialize(object prototype)
        {
            lock (this)
            {
                var collector = new Collector(serializerCache, graphByFact);
                collector.Serialize(prototype);
                serializerCache = collector.SerializerCache;
                return collector.Graph;
            }
        }

        public TFact Deserialize<TFact>(FactGraph graph, FactReference reference)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache, graphByFact);
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
                var emitter = new Emitter(graph, deserializerCache, graphByFact, watchContext);
                ImmutableList<ProjectedResult> results = Deserializer.Deserialize(emitter, projection, type, products, path);
                deserializerCache = emitter.DeserializerCache;
                return results;
            }
        }

        public Observer StartObserver(FactReferenceTuple givenTuple, Specification specification, Func<object, Task<Func<Task>>> onAdded, bool keepAlive)
        {
            var observer = new Observer(specification, givenTuple, this, onAdded);
            observer.Start(keepAlive);
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
            await observableSource.Notify(graph, facts, cancellationToken).ConfigureAwait(false);
        }

        public Task<DateTime?> GetMruDate(string specificationHash)
        {
            return store.GetMruDate(specificationHash);
        }

        public Task SetMruDate(string specificationHash, DateTime mruDate)
        {
            return store.SetMruDate(specificationHash, mruDate);
        }

        public async Task Unload()
        {
            var freezePendingTasks = pendingTasks;
            if (freezePendingTasks.Count > 0)
            {
                await Task.WhenAll(freezePendingTasks
                    .Select(handle => handle.Task)
                );
            }
        }

        private void AddBackgroundTask(TaskHandle handle)
        {
            lock (this)
            {
                pendingTasks = pendingTasks.Add(handle);
            }
        }

        private void RemoveBackgroundTask(TaskHandle handle)
        {
            lock (this)
            {
                pendingTasks = pendingTasks.Remove(handle);
            }
        }
    }
}
